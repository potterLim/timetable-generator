function Invoke-AggregateFinalization {
    param(
        [Parameter(Mandatory)]
        [string] $RepositoryRoot,

        [Parameter(Mandatory)]
        [string] $Version,

        [string] $SourcePath,

        [string] $OutputRoot,

        [ValidateSet("Signed", "Unsigned")]
        [string] $WindowsSignatureMode = "Signed",

        [switch] $AllowUnsigned
    )

    if ($AllowUnsigned) {
        throw "Aggregate는 unsigned smoke archive를 허용하지 않습니다."
    }

    $releaseRoot = if ([string]::IsNullOrWhiteSpace($SourcePath)) {
        Resolve-ReleaseOutputRoot `
            -RepositoryRoot $RepositoryRoot `
            -Version $Version `
            -RequestedOutputRoot $OutputRoot
    }
    else {
        Resolve-PathFromRepository -RepositoryRoot $RepositoryRoot -Path $SourcePath
    }
    if ((Test-Path -LiteralPath $releaseRoot -PathType Container) -eq $false) {
        throw "최종 Release 자산 디렉터리를 찾을 수 없습니다: $releaseRoot"
    }

    $expectedArchiveFileNames = @(Get-ExpectedReleaseArchiveFileNames `
        -Version $Version `
        -WindowsSignatureMode $WindowsSignatureMode)
    Assert-ReleaseOutputRootContents `
        -OutputRoot $releaseRoot `
        -AllowedFileNames @($expectedArchiveFileNames + "checksums.sha256")
    $actualArchiveFileNames = @(
        Get-ChildItem -LiteralPath $releaseRoot -Filter "*.zip" -File |
            ForEach-Object Name |
            Sort-Object -CaseSensitive
    )
    if ($actualArchiveFileNames.Count -ne $expectedArchiveFileNames.Count) {
        throw "Aggregate에는 정확히 두 개의 최종 ZIP만 있어야 합니다: $releaseRoot"
    }

    for ($index = 0; $index -lt $expectedArchiveFileNames.Count; $index++) {
        if ($actualArchiveFileNames[$index] -cne $expectedArchiveFileNames[$index]) {
            throw "최종 ZIP 구성이 예상과 일치하지 않습니다. 예상: $($expectedArchiveFileNames -join ', ')"
        }
    }

    foreach ($archiveFileName in $expectedArchiveFileNames) {
        $archivePath = Join-Path $releaseRoot $archiveFileName
        $requiredPrefix = if ($archiveFileName.Contains("win-x64", [System.StringComparison]::Ordinal)) {
            "TimetableGenerator-$Version/"
        }
        else {
            "$($script:MACOS_APPLICATION_NAME)/"
        }
        $contentsPrefix = "${requiredPrefix}Contents/"
        $requiredEntries = if ($archiveFileName.Contains("win-x64", [System.StringComparison]::Ordinal)) {
            @(
                "$requiredPrefix$($script:PRODUCT_EXECUTABLE_BASE_NAME).exe",
                "$requiredPrefix$($script:PRODUCT_EXECUTABLE_BASE_NAME).dll",
                "$requiredPrefix$($script:PRODUCT_EXECUTABLE_BASE_NAME).deps.json",
                "$requiredPrefix$($script:PRODUCT_EXECUTABLE_BASE_NAME).runtimeconfig.json",
                "${requiredPrefix}coreclr.dll"
            )
        }
        else {
            @(
                "${contentsPrefix}Info.plist",
                "${contentsPrefix}MacOS/$($script:PRODUCT_EXECUTABLE_BASE_NAME)",
                "${contentsPrefix}MacOS/$($script:PRODUCT_EXECUTABLE_BASE_NAME).dll",
                "${contentsPrefix}MacOS/$($script:PRODUCT_EXECUTABLE_BASE_NAME).deps.json",
                "${contentsPrefix}MacOS/$($script:PRODUCT_EXECUTABLE_BASE_NAME).runtimeconfig.json",
                "${contentsPrefix}MacOS/libcoreclr.dylib",
                "${contentsPrefix}MacOS/$($script:MACOS_EVENTKIT_BRIDGE_FILE_NAME)",
                "${contentsPrefix}Resources/AppIcon.icns"
            )
        }
        foreach ($configurationFileName in $script:CONFIGURATION_FILE_NAMES) {
            $relativePath = if ($archiveFileName.Contains("win-x64", [System.StringComparison]::Ordinal)) {
                "$requiredPrefix$configurationFileName"
            }
            else {
                "${contentsPrefix}MacOS/$configurationFileName"
            }
            $requiredEntries += $relativePath
        }
        foreach ($noticeFileName in $script:REQUIRED_NOTICE_FILE_NAMES) {
            $relativePath = if ($archiveFileName.Contains("win-x64", [System.StringComparison]::Ordinal)) {
                "${requiredPrefix}ThirdPartyNotices/$noticeFileName"
            }
            else {
                "${contentsPrefix}Resources/ThirdPartyNotices/$noticeFileName"
            }
            $requiredEntries += $relativePath
        }

        $archiveEntryParameters = @{
            ArchivePath = $archivePath
            RequiredEntryNames = $requiredEntries
            RequiredPrefix = $requiredPrefix
        }
        if ($archiveFileName.Contains("osx-", [System.StringComparison]::Ordinal)) {
            $archiveEntryParameters.AllowMacOSMetadataEntries = $true
        }

        Assert-ArchiveEntries @archiveEntryParameters
    }

    $checksumFileName = "checksums.sha256"
    $checksumPath = Join-Path $releaseRoot $checksumFileName
    Remove-ExistingReleaseFile `
        -OutputRoot $releaseRoot `
        -Path $checksumPath `
        -ExpectedFileName $checksumFileName
    $checksumLines = foreach ($archiveFileName in $expectedArchiveFileNames) {
        $archivePath = Join-Path $releaseRoot $archiveFileName
        $hash = (Get-FileHash -LiteralPath $archivePath -Algorithm SHA256).Hash.ToLowerInvariant()
        "$hash  $archiveFileName"
    }
    $encoding = [System.Text.UTF8Encoding]::new($false)
    [System.IO.File]::WriteAllLines($checksumPath, $checksumLines, $encoding)
    Assert-NonEmptyFile -Path $checksumPath
    Write-Host "최종 Release checksum을 생성했습니다: $checksumPath"
}
