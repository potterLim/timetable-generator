function Assert-DistributionRuntimeHost {
    param(
        [Parameter(Mandatory)]
        [ValidateSet("win-x64", "osx-arm64")]
        [string] $Runtime
    )

    if ($Runtime -eq "win-x64" -and -not $IsWindows) {
        throw "win-x64 게시 산출물은 Windows에서만 만들 수 있습니다."
    }

    if ($Runtime -eq "osx-arm64" -and -not $IsMacOS) {
        throw "osx-arm64 게시 산출물은 macOS에서만 만들 수 있습니다."
    }
}

function Publish-TimetableGeneratorDesktop {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [string] $RepositoryRoot,

        [Parameter(Mandatory)]
        [ValidateSet("win-x64", "osx-arm64")]
        [string] $Runtime,

        [ValidatePattern("^\d+\.\d+\.\d+$")]
        [string] $Version,

        [ValidatePattern("^[A-Za-z0-9-]+(?:\.[A-Za-z0-9-]+)+$")]
        [string] $BundleIdentifier = "io.github.potterlim.timetable",

        [string] $OutputRoot,

        [switch] $NoRestore
    )

    Assert-DistributionRuntimeHost -Runtime $Runtime

    $resolvedRepositoryRoot = [System.IO.Path]::GetFullPath($RepositoryRoot)
    $projectPath = Join-Path $resolvedRepositoryRoot "src/TimetableGenerator.Desktop/TimetableGenerator.Desktop.csproj"
    $windowsManifestPath = Join-Path $resolvedRepositoryRoot "src/TimetableGenerator.Desktop/Platforms/Windows/app.manifest"
    $infoPlistTemplatePath = Join-Path $resolvedRepositoryRoot "src/TimetableGenerator.Desktop/Platforms/macOS/Info.plist.template"
    $entitlementsPath = Join-Path $resolvedRepositoryRoot "src/TimetableGenerator.Desktop/Platforms/macOS/TimetableGenerator.entitlements"
    $appIconPath = Join-Path $resolvedRepositoryRoot "src/TimetableGenerator.Desktop/Assets/AppIcon.png"

    foreach ($requiredFilePath in @(
        $projectPath,
        $windowsManifestPath,
        $infoPlistTemplatePath,
        $entitlementsPath,
        $appIconPath)) {
        if (-not (Test-Path -LiteralPath $requiredFilePath -PathType Leaf)) {
            throw "필수 배포 파일을 찾을 수 없습니다: $requiredFilePath"
        }
    }

    Assert-WindowsApplicationManifest -Path $windowsManifestPath
    Assert-MacOSEntitlements -Path $entitlementsPath

    $resolvedOutputRoot = Resolve-DistributionOutputRoot -RepositoryRoot $resolvedRepositoryRoot -RequestedOutputRoot $OutputRoot

    $metadata = Get-DesktopProjectMetadata -ProjectPath $projectPath
    $executableName = [string] $metadata.AssemblyName
    if ([string]::IsNullOrWhiteSpace($Version)) {
        $Version = [string] $metadata.Version
    }

    if ($Version -notmatch "^\d+\.\d+\.\d+$") {
        throw "제품 버전은 major.minor.patch 숫자 형식이어야 합니다: $Version"
    }

    $archiveTimestamp = Get-RepositoryCommitArchiveTimestamp -RepositoryRoot $resolvedRepositoryRoot
    $selectedRuntimes = @($Runtime)
    $replaceableEntryNames = [System.Collections.Generic.List[string]]::new()
    foreach ($runtimeIdentifier in $selectedRuntimes) {
        $replaceableEntryNames.Add($runtimeIdentifier)
        $replaceableEntryNames.Add("TimetableGenerator-$Version-$runtimeIdentifier-unsigned.zip")
    }
    $replaceableEntryNames.Add("checksums.sha256")
    Initialize-DistributionOutputRoot `
        -OutputRoot $resolvedOutputRoot `
        -ReplaceableEntryNames $replaceableEntryNames.ToArray()

    $archivePaths = [System.Collections.Generic.List[string]]::new()

    foreach ($runtimeIdentifier in $selectedRuntimes) {
        switch ($runtimeIdentifier) {
            "win-x64" {
                Publish-WindowsTarget `
                    -ProjectPath $projectPath `
                    -OutputRoot $resolvedOutputRoot `
                    -ExecutableName $executableName `
                    -ProductVersion $Version `
                    -ArchiveTimestamp $archiveTimestamp `
                    -NoRestore:$NoRestore
                $archivePaths.Add((Join-Path $resolvedOutputRoot "TimetableGenerator-$Version-$runtimeIdentifier-unsigned.zip"))
            }
            "osx-arm64" {
                Publish-MacOSTarget `
                    -RuntimeIdentifier $runtimeIdentifier `
                    -ProjectPath $projectPath `
                    -InfoPlistTemplatePath $infoPlistTemplatePath `
                    -AppIconPath $appIconPath `
                    -OutputRoot $resolvedOutputRoot `
                    -ExecutableName $executableName `
                    -ProductVersion $Version `
                    -BundleIdentifier $BundleIdentifier `
                    -ArchiveTimestamp $archiveTimestamp `
                    -NoRestore:$NoRestore
                $archivePaths.Add((Join-Path $resolvedOutputRoot "TimetableGenerator-$Version-$runtimeIdentifier-unsigned.zip"))
            }
            default {
                throw "지원하지 않는 runtime identifier입니다: $runtimeIdentifier"
            }
        }
    }

    Write-DistributionChecksums `
        -OutputRoot $resolvedOutputRoot `
        -ArchivePaths $archivePaths.ToArray()
    Write-Host "검증된 unsigned 게시 산출물을 생성했습니다: $resolvedOutputRoot"
}
