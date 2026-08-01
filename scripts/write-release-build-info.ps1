#requires -Version 7.0

[CmdletBinding()]
param(
    [ValidatePattern("^\d+\.\d+\.\d+$")]
    [string] $Version,

    [string] $OutputRoot,

    [switch] $RequireClean,

    [switch] $Force
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Get-PathComparison {
    if ($IsWindows) {
        return [System.StringComparison]::OrdinalIgnoreCase
    }

    return [System.StringComparison]::Ordinal
}

function Get-NormalizedFullPath {
    param(
        [Parameter(Mandatory)]
        [string] $Path
    )

    $fullPath = [System.IO.Path]::GetFullPath($Path)
    $volumeRoot = [System.IO.Path]::GetPathRoot($fullPath)
    $comparison = Get-PathComparison
    if ($fullPath.Equals($volumeRoot, $comparison)) {
        return $fullPath
    }

    return $fullPath.TrimEnd(
        [System.IO.Path]::DirectorySeparatorChar,
        [System.IO.Path]::AltDirectorySeparatorChar)
}

function Invoke-CapturedCommand {
    param(
        [Parameter(Mandatory)]
        [string] $FileName,

        [Parameter(Mandatory)]
        [string[]] $Arguments,

        [Parameter(Mandatory)]
        [string] $WorkingDirectory
    )

    $startInfo = [System.Diagnostics.ProcessStartInfo]::new()
    $startInfo.FileName = $FileName
    $startInfo.WorkingDirectory = $WorkingDirectory
    $startInfo.UseShellExecute = $false
    $startInfo.CreateNoWindow = $true
    $startInfo.RedirectStandardOutput = $true
    $startInfo.RedirectStandardError = $true

    foreach ($argument in $Arguments) {
        $startInfo.ArgumentList.Add($argument)
    }

    $process = [System.Diagnostics.Process]::new()
    $process.StartInfo = $startInfo
    try {
        if (-not $process.Start()) {
            throw "명령을 시작할 수 없습니다: $FileName"
        }

        $standardOutputTask = $process.StandardOutput.ReadToEndAsync()
        $standardErrorTask = $process.StandardError.ReadToEndAsync()
        $process.WaitForExit()
        $standardOutput = $standardOutputTask.GetAwaiter().GetResult()
        $standardError = $standardErrorTask.GetAwaiter().GetResult()

        if ($process.ExitCode -ne 0) {
            $failureDetail = $standardError.Trim()
            if ([string]::IsNullOrWhiteSpace($failureDetail)) {
                $failureDetail = "표준 오류 출력이 없습니다."
            }

            throw "명령이 종료 코드 $($process.ExitCode)로 실패했습니다: $FileName`n$failureDetail"
        }

        return [pscustomobject]@{
            StandardOutput = $standardOutput
            StandardError = $standardError
        }
    }
    finally {
        $process.Dispose()
    }
}

function Get-ProjectVersion {
    param(
        [Parameter(Mandatory)]
        [string] $ProjectPath,

        [Parameter(Mandatory)]
        [string] $RepositoryRoot
    )

    $result = Invoke-CapturedCommand `
        -FileName "dotnet" `
        -Arguments @(
            "msbuild",
            $ProjectPath,
            "--nologo",
            "-getProperty:Version",
            "-property:Configuration=Release") `
        -WorkingDirectory $RepositoryRoot

    $projectVersion = $result.StandardOutput.Trim()
    if ($projectVersion -notmatch "^\d+\.\d+\.\d+$") {
        throw "Desktop 프로젝트 버전이 major.minor.patch 숫자 형식이 아닙니다: $projectVersion"
    }

    return $projectVersion
}

function Get-PlatformRuntimeIdentifier {
    $architecture = [System.Runtime.InteropServices.RuntimeInformation]::ProcessArchitecture
    $architectureName = $architecture.ToString().ToLowerInvariant()

    if ([System.Runtime.InteropServices.RuntimeInformation]::IsOSPlatform(
            [System.Runtime.InteropServices.OSPlatform]::Windows)) {
        return "win-$architectureName"
    }

    if ([System.Runtime.InteropServices.RuntimeInformation]::IsOSPlatform(
            [System.Runtime.InteropServices.OSPlatform]::OSX)) {
        return "osx-$architectureName"
    }

    if ([System.Runtime.InteropServices.RuntimeInformation]::IsOSPlatform(
            [System.Runtime.InteropServices.OSPlatform]::Linux)) {
        return "linux-$architectureName"
    }

    return "unknown-$architectureName"
}

function Resolve-EvidenceOutputRoot {
    param(
        [Parameter(Mandatory)]
        [string] $RepositoryRoot,

        [string] $RequestedOutputRoot
    )

    if ([string]::IsNullOrWhiteSpace($RequestedOutputRoot)) {
        $candidate = Join-Path $RepositoryRoot "artifacts/release-evidence"
    }
    elseif ([System.IO.Path]::IsPathRooted($RequestedOutputRoot)) {
        $candidate = $RequestedOutputRoot
    }
    else {
        $candidate = Join-Path $RepositoryRoot $RequestedOutputRoot
    }

    $resolvedRepositoryRoot = Get-NormalizedFullPath -Path $RepositoryRoot
    $resolvedOutputRoot = Get-NormalizedFullPath -Path $candidate
    $volumeRoot = Get-NormalizedFullPath -Path (
        [System.IO.Path]::GetPathRoot($resolvedOutputRoot))
    $comparison = Get-PathComparison

    if ($resolvedOutputRoot.Equals($volumeRoot, $comparison)) {
        throw "파일 시스템 루트는 증거 출력 위치로 사용할 수 없습니다: $resolvedOutputRoot"
    }

    if ($resolvedOutputRoot.Equals($resolvedRepositoryRoot, $comparison)) {
        throw "저장소 루트는 증거 출력 위치로 사용할 수 없습니다: $resolvedOutputRoot"
    }

    if (Test-Path -LiteralPath $resolvedOutputRoot -PathType Leaf) {
        throw "증거 출력 위치에 파일이 있습니다: $resolvedOutputRoot"
    }

    return $resolvedOutputRoot
}

function Add-RawOutputSection {
    param(
        [Parameter(Mandatory)]
        [System.Text.StringBuilder] $Builder,

        [Parameter(Mandatory)]
        [string] $Heading,

        [Parameter(Mandatory)]
        [AllowEmptyString()]
        [string] $RawOutput
    )

    $null = $Builder.AppendLine()
    $null = $Builder.AppendLine($Heading)
    $null = $Builder.Append($RawOutput)
    if (-not $RawOutput.EndsWith([System.Environment]::NewLine, [System.StringComparison]::Ordinal)) {
        $null = $Builder.AppendLine()
    }
}

$repositoryRoot = Get-NormalizedFullPath -Path (Join-Path $PSScriptRoot "..")
$projectPath = Join-Path $repositoryRoot "src/TimetableGenerator.Desktop/TimetableGenerator.Desktop.csproj"
if (-not (Test-Path -LiteralPath $projectPath -PathType Leaf)) {
    throw "Desktop 프로젝트를 찾을 수 없습니다: $projectPath"
}

$projectVersion = Get-ProjectVersion -ProjectPath $projectPath -RepositoryRoot $repositoryRoot
$releaseVersion = if ([string]::IsNullOrWhiteSpace($Version)) {
    $projectVersion
}
else {
    $Version
}

$runtimeIdentifier = Get-PlatformRuntimeIdentifier
$resolvedOutputRoot = Resolve-EvidenceOutputRoot -RepositoryRoot $repositoryRoot -RequestedOutputRoot $OutputRoot
$evidenceDirectory = Join-Path (Join-Path $resolvedOutputRoot $releaseVersion) $runtimeIdentifier
$evidencePath = Join-Path $evidenceDirectory "build-info.txt"
if ((Test-Path -LiteralPath $evidencePath -PathType Leaf) -and -not $Force) {
    throw "이미 기록된 배포 빌드 환경 증거가 있습니다. 교체하려면 -Force를 명시하세요: $evidencePath"
}

$dotNetVersion = Invoke-CapturedCommand -FileName "dotnet" -Arguments @("--version") -WorkingDirectory $repositoryRoot
$dotNetInfo = Invoke-CapturedCommand -FileName "dotnet" -Arguments @("--info") -WorkingDirectory $repositoryRoot
$gitHead = Invoke-CapturedCommand -FileName "git" -Arguments @("rev-parse", "--verify", "HEAD") -WorkingDirectory $repositoryRoot
$gitStatus = Invoke-CapturedCommand -FileName "git" -Arguments @("status", "--porcelain=v1", "--untracked-files=normal") -WorkingDirectory $repositoryRoot

$repositoryState = if ([string]::IsNullOrEmpty($gitStatus.StandardOutput)) {
    "clean"
}
else {
    "dirty"
}
if ($RequireClean -and $repositoryState -ne "clean") {
    throw "최종 배포 증거는 변경 사항이 없는 Git 상태에서만 기록할 수 있습니다."
}

$recordedAtUtc = [System.DateTimeOffset]::UtcNow.ToString("O", [System.Globalization.CultureInfo]::InvariantCulture)
$osDescription = [System.Runtime.InteropServices.RuntimeInformation]::OSDescription
$osArchitecture = [System.Runtime.InteropServices.RuntimeInformation]::OSArchitecture
$processArchitecture = [System.Runtime.InteropServices.RuntimeInformation]::ProcessArchitecture

$builder = [System.Text.StringBuilder]::new()
$null = $builder.AppendLine("Release build environment evidence")
$null = $builder.AppendLine("Schema-Version: 1")
$null = $builder.AppendLine("Release-Version: $releaseVersion")
$null = $builder.AppendLine("Project-Version: $projectVersion")
$null = $builder.AppendLine("Recorded-At-Utc: $recordedAtUtc")
$null = $builder.AppendLine("Repository-Commit: $($gitHead.StandardOutput.Trim())")
$null = $builder.AppendLine("Repository-Status: $repositoryState")
$null = $builder.AppendLine("Runtime-Identifier: $runtimeIdentifier")
$null = $builder.AppendLine("Operating-System: $osDescription")
$null = $builder.AppendLine("OS-Architecture: $osArchitecture")
$null = $builder.AppendLine("Process-Architecture: $processArchitecture")

Add-RawOutputSection `
    -Builder $builder `
    -Heading "[dotnet --version]" `
    -RawOutput $dotNetVersion.StandardOutput
if (-not [string]::IsNullOrWhiteSpace($dotNetVersion.StandardError)) {
    Add-RawOutputSection `
        -Builder $builder `
        -Heading "[dotnet --version stderr]" `
        -RawOutput $dotNetVersion.StandardError
}

Add-RawOutputSection `
    -Builder $builder `
    -Heading "[dotnet --info]" `
    -RawOutput $dotNetInfo.StandardOutput
if (-not [string]::IsNullOrWhiteSpace($dotNetInfo.StandardError)) {
    Add-RawOutputSection `
        -Builder $builder `
        -Heading "[dotnet --info stderr]" `
        -RawOutput $dotNetInfo.StandardError
}

$null = New-Item -ItemType Directory -Path $evidenceDirectory -Force
$utf8WithoutByteOrderMark = [System.Text.UTF8Encoding]::new($false)
[System.IO.File]::WriteAllText(
    $evidencePath,
    $builder.ToString(),
    $utf8WithoutByteOrderMark)

Write-Host "배포 빌드 환경 증거를 기록했습니다: $evidencePath"
