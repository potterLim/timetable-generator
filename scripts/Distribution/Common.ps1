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

function Invoke-DotNetCommand {
    param(
        [Parameter(Mandatory)]
        [string[]] $Arguments
    )

    & dotnet @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet 명령이 종료 코드 $LASTEXITCODE 로 실패했습니다."
    }
}

function Get-DesktopProjectMetadata {
    param(
        [Parameter(Mandatory)]
        [string] $ProjectPath
    )

    $metadataJson = & dotnet msbuild $ProjectPath `
        --nologo `
        -getProperty:Version `
        -getProperty:AssemblyName `
        -property:Configuration=Release

    if ($LASTEXITCODE -ne 0) {
        throw "게시 프로젝트 메타데이터를 읽을 수 없습니다."
    }

    $metadata = $metadataJson | ConvertFrom-Json
    $assemblyName = [string] $metadata.Properties.AssemblyName
    $version = [string] $metadata.Properties.Version
    if ([string]::IsNullOrWhiteSpace($assemblyName) -or [string]::IsNullOrWhiteSpace($version)) {
        throw "게시 프로젝트의 AssemblyName 또는 Version이 비어 있습니다."
    }

    return [pscustomobject]@{
        AssemblyName = $assemblyName
        Version = $version
    }
}

function Resolve-DistributionOutputRoot {
    param(
        [Parameter(Mandatory)]
        [string] $RepositoryRoot,

        [string] $RequestedOutputRoot
    )

    $resolvedRepositoryRoot = Get-NormalizedFullPath -Path $RepositoryRoot

    if ([string]::IsNullOrWhiteSpace($RequestedOutputRoot)) {
        $candidate = Join-Path $resolvedRepositoryRoot "artifacts/publish"
    }
    elseif ([System.IO.Path]::IsPathRooted($RequestedOutputRoot)) {
        $candidate = $RequestedOutputRoot
    }
    else {
        $candidate = Join-Path $resolvedRepositoryRoot $RequestedOutputRoot
    }

    $resolvedOutputRoot = Get-NormalizedFullPath -Path $candidate
    $volumeRoot = Get-NormalizedFullPath -Path ([System.IO.Path]::GetPathRoot($resolvedOutputRoot))
    $comparison = Get-PathComparison

    if ($resolvedOutputRoot.Equals($volumeRoot, $comparison)) {
        throw "파일 시스템 루트는 게시 출력 위치로 사용할 수 없습니다: $resolvedOutputRoot"
    }

    if ($resolvedOutputRoot.Equals($resolvedRepositoryRoot, $comparison)) {
        throw "저장소 루트는 게시 출력 위치로 사용할 수 없습니다: $resolvedOutputRoot"
    }

    return $resolvedOutputRoot
}

function Assert-DirectChildPath {
    param(
        [Parameter(Mandatory)]
        [string] $ParentPath,

        [Parameter(Mandatory)]
        [string] $ChildPath,

        [Parameter(Mandatory)]
        [string] $ExpectedLeafName
    )

    $resolvedParent = Get-NormalizedFullPath -Path $ParentPath
    $resolvedChild = Get-NormalizedFullPath -Path $ChildPath
    $childParent = [System.IO.DirectoryInfo]::new($resolvedChild).Parent
    $comparison = Get-PathComparison

    if ($null -eq $childParent -or -not $childParent.FullName.Equals($resolvedParent, $comparison)) {
        throw "정리 대상은 게시 출력 위치의 직접 하위 항목이어야 합니다: $resolvedChild"
    }

    $actualLeafName = [System.IO.Path]::GetFileName($resolvedChild)
    if (-not $actualLeafName.Equals($ExpectedLeafName, $comparison)) {
        throw "정리 대상 이름이 예상과 일치하지 않습니다: $resolvedChild"
    }
}

function Reset-DistributionDirectory {
    param(
        [Parameter(Mandatory)]
        [string] $OutputRoot,

        [Parameter(Mandatory)]
        [string] $Path,

        [Parameter(Mandatory)]
        [string] $ExpectedLeafName
    )

    Assert-DirectChildPath `
        -ParentPath $OutputRoot `
        -ChildPath $Path `
        -ExpectedLeafName $ExpectedLeafName

    if (Test-Path -LiteralPath $Path) {
        $existingItem = Get-Item -LiteralPath $Path -Force
        if (-not $existingItem.PSIsContainer) {
            throw "게시 디렉터리 위치에 파일이 있습니다: $Path"
        }

        $isReparsePoint = ($existingItem.Attributes -band [System.IO.FileAttributes]::ReparsePoint) -ne 0
        if ($isReparsePoint) {
            Remove-Item -LiteralPath $Path -Force
        }
        else {
            Remove-Item -LiteralPath $Path -Recurse -Force
        }
    }

    $null = New-Item -ItemType Directory -Path $Path -Force
}

function Remove-ExistingDistributionArchive {
    param(
        [Parameter(Mandatory)]
        [string] $OutputRoot,

        [Parameter(Mandatory)]
        [string] $ArchivePath,

        [Parameter(Mandatory)]
        [string] $ExpectedFileName
    )

    Assert-DirectChildPath `
        -ParentPath $OutputRoot `
        -ChildPath $ArchivePath `
        -ExpectedLeafName $ExpectedFileName

    if (-not (Test-Path -LiteralPath $ArchivePath)) {
        return
    }

    $existingItem = Get-Item -LiteralPath $ArchivePath -Force
    if ($existingItem.PSIsContainer) {
        throw "게시 archive 위치에 디렉터리가 있습니다: $ArchivePath"
    }

    Remove-Item -LiteralPath $ArchivePath -Force
}

function Assert-NonEmptyFile {
    param(
        [Parameter(Mandatory)]
        [string] $Path
    )

    $file = Get-Item -LiteralPath $Path -ErrorAction SilentlyContinue
    if ($null -eq $file -or $file.PSIsContainer -or $file.Length -eq 0) {
        throw "필수 게시 파일이 없거나 비어 있습니다: $Path"
    }
}

function Remove-DebugSymbols {
    param(
        [Parameter(Mandatory)]
        [string] $Path
    )

    $debugSymbols = @(Get-ChildItem -LiteralPath $Path -Filter "*.pdb" -File -Recurse)
    foreach ($debugSymbol in $debugSymbols) {
        Remove-Item -LiteralPath $debugSymbol.FullName -Force
    }
}

function Invoke-SelfContainedPublish {
    param(
        [Parameter(Mandatory)]
        [string] $ProjectPath,

        [Parameter(Mandatory)]
        [string] $RuntimeIdentifier,

        [Parameter(Mandatory)]
        [string] $DestinationPath,

        [Parameter(Mandatory)]
        [string] $ProductVersion,

        [switch] $NoRestore
    )

    if (-not $NoRestore) {
        Invoke-DotNetCommand -Arguments @(
            "restore",
            $ProjectPath,
            "--locked-mode",
            "--nologo"
        )
    }

    $arguments = @(
        "publish",
        $ProjectPath,
        "--configuration", "Release",
        "--runtime", $RuntimeIdentifier,
        "--self-contained", "true",
        "--output", $DestinationPath,
        "--no-restore",
        "--nologo",
        "/m:1",
        "/nodeReuse:false",
        "-p:Version=$ProductVersion",
        "-p:ContinuousIntegrationBuild=true",
        "-p:DebugSymbols=false",
        "-p:DebugType=None",
        "-p:PublishSingleFile=false",
        "-p:PublishTrimmed=false"
    )

    Invoke-DotNetCommand -Arguments $arguments
}
