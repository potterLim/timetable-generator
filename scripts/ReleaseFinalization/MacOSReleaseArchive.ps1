function New-MacOSReleaseArchive {
    param(
        [Parameter(Mandatory)]
        [string] $RepositoryRoot,

        [Parameter(Mandatory)]
        [string] $Version,

        [Parameter(Mandatory)]
        [ValidateSet("osx-arm64")]
        [string] $Runtime,

        [Parameter(Mandatory)]
        [string] $ApplicationPath,

        [string] $OutputRoot,

        [switch] $AllowUnsigned
    )

    $releaseRoot = Resolve-ReleaseOutputRoot `
        -RepositoryRoot $RepositoryRoot `
        -Version $Version `
        -RequestedOutputRoot $OutputRoot `
        -SourcePath $ApplicationPath `
        -AllowUnsigned:$AllowUnsigned
    $allowedFileNames = @(Get-AllowedReleaseOutputFileNames -Version $Version -AllowUnsigned:$AllowUnsigned)
    Assert-ReleaseOutputRootContents -OutputRoot $releaseRoot -AllowedFileNames $allowedFileNames
    $archiveFileName = if ($AllowUnsigned) {
        "TimetableGenerator-$Version-$Runtime-unsigned-smoke.zip"
    }
    else {
        "TimetableGenerator-$Version-$Runtime.zip"
    }
    $archivePath = Join-Path $releaseRoot $archiveFileName
    Remove-ExistingReleaseFile -OutputRoot $releaseRoot -Path $archivePath -ExpectedFileName $archiveFileName
    try {
        Invoke-NativeCommand -Command "ditto" -Arguments @(
            "-c",
            "-k",
            "--sequesterRsrc",
            "--keepParent",
            $ApplicationPath,
            $archivePath
        ) -FailureMessage "macOS 최종 Release ZIP 생성에 실패했습니다."

        $applicationPrefix = "$($script:MACOS_APPLICATION_NAME)/"
        $contentsPrefix = "${applicationPrefix}Contents/"
        $requiredEntries = @(
            "${contentsPrefix}Info.plist",
            "${contentsPrefix}MacOS/$($script:PRODUCT_EXECUTABLE_BASE_NAME)",
            "${contentsPrefix}MacOS/$($script:PRODUCT_EXECUTABLE_BASE_NAME).dll",
            "${contentsPrefix}MacOS/$($script:PRODUCT_EXECUTABLE_BASE_NAME).deps.json",
            "${contentsPrefix}MacOS/$($script:PRODUCT_EXECUTABLE_BASE_NAME).runtimeconfig.json",
            "${contentsPrefix}MacOS/libcoreclr.dylib",
            "${contentsPrefix}MacOS/$($script:MACOS_EVENTKIT_BRIDGE_FILE_NAME)",
            "${contentsPrefix}Resources/AppIcon.icns"
        )
        foreach ($configurationFileName in $script:CONFIGURATION_FILE_NAMES) {
            $requiredEntries += "${contentsPrefix}MacOS/$configurationFileName"
        }
        foreach ($noticeFileName in $script:REQUIRED_NOTICE_FILE_NAMES) {
            $requiredEntries += "${contentsPrefix}Resources/ThirdPartyNotices/$noticeFileName"
        }

        Assert-ArchiveEntries -ArchivePath $archivePath -RequiredEntryNames $requiredEntries -RequiredPrefix $applicationPrefix -AllowMacOSMetadataEntries
    }
    catch {
        Remove-ExistingReleaseFile -OutputRoot $releaseRoot -Path $archivePath -ExpectedFileName $archiveFileName
        throw
    }

    Write-Host "macOS Release ZIP을 생성했습니다: $archivePath"
}
