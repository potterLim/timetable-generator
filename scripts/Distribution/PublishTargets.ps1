function Publish-WindowsTarget {
    param(
        [Parameter(Mandatory)]
        [string] $ProjectPath,

        [Parameter(Mandatory)]
        [string] $OutputRoot,

        [Parameter(Mandatory)]
        [string] $ExecutableName,

        [Parameter(Mandatory)]
        [string] $ProductVersion,

        [switch] $NoRestore
    )

    $runtimeIdentifier = "win-x64"
    $publishPath = Join-Path $OutputRoot $runtimeIdentifier
    Reset-DistributionDirectory `
        -OutputRoot $OutputRoot `
        -Path $publishPath `
        -ExpectedLeafName $runtimeIdentifier
    Invoke-SelfContainedPublish `
        -ProjectPath $ProjectPath `
        -RuntimeIdentifier $runtimeIdentifier `
        -DestinationPath $publishPath `
        -ProductVersion $ProductVersion `
        -NoRestore:$NoRestore

    $executablePath = Join-Path $publishPath "$ExecutableName.exe"
    $thirdPartyNoticePath = Join-Path $publishPath "ThirdPartyNotices"
    $nativeBinaryPaths = @(
        $executablePath,
        (Join-Path $publishPath "coreclr.dll"),
        (Join-Path $publishPath "hostfxr.dll"),
        (Join-Path $publishPath "hostpolicy.dll")
    )
    foreach ($nativeBinaryPath in $nativeBinaryPaths) {
        Assert-NonEmptyFile -Path $nativeBinaryPath
        Assert-WindowsX64PeBinary -Path $nativeBinaryPath
    }
    Assert-RequiredThirdPartyNoticeFiles `
        -ProjectPath $ProjectPath `
        -PublishedNoticePath $thirdPartyNoticePath

    Remove-DebugSymbols -Path $publishPath

    $archiveFileName = "TimetableGenerator-$ProductVersion-$runtimeIdentifier-unsigned.zip"
    New-DistributionArchive `
        -SourcePath $publishPath `
        -OutputRoot $OutputRoot `
        -ArchiveFileName $archiveFileName `
        -ArchiveRootName "TimetableGenerator-$ProductVersion" `
        -ArchivePlatform "Windows"
}

function Publish-MacOSTarget {
    param(
        [Parameter(Mandatory)]
        [ValidateSet("osx-arm64")]
        [string] $RuntimeIdentifier,

        [Parameter(Mandatory)]
        [string] $ProjectPath,

        [Parameter(Mandatory)]
        [string] $InfoPlistTemplatePath,

        [Parameter(Mandatory)]
        [string] $AppIconPath,

        [Parameter(Mandatory)]
        [string] $OutputRoot,

        [Parameter(Mandatory)]
        [string] $ExecutableName,

        [Parameter(Mandatory)]
        [string] $ProductVersion,

        [Parameter(Mandatory)]
        [string] $BundleIdentifier,

        [switch] $NoRestore
    )

    $runtimeOutputPath = Join-Path $OutputRoot $RuntimeIdentifier
    $bundlePath = Join-Path $runtimeOutputPath "Timetable Generator.app"
    $contentsPath = Join-Path $bundlePath "Contents"
    $macOSPath = Join-Path $contentsPath "MacOS"
    $resourcesPath = Join-Path $contentsPath "Resources"
    $eventKitBridgePath = Join-Path $macOSPath "libTimetableGenerator.EventKitBridge.dylib"

    Reset-DistributionDirectory `
        -OutputRoot $OutputRoot `
        -Path $runtimeOutputPath `
        -ExpectedLeafName $RuntimeIdentifier
    $null = New-Item -ItemType Directory -Path $macOSPath -Force
    $null = New-Item -ItemType Directory -Path $resourcesPath -Force

    Invoke-SelfContainedPublish `
        -ProjectPath $ProjectPath `
        -RuntimeIdentifier $RuntimeIdentifier `
        -DestinationPath $macOSPath `
        -ProductVersion $ProductVersion `
        -NoRestore:$NoRestore

    $publishedThirdPartyNoticesPath = Join-Path $macOSPath "ThirdPartyNotices"
    $bundledThirdPartyNoticesPath = Join-Path $resourcesPath "ThirdPartyNotices"
    Assert-RequiredThirdPartyNoticeFiles `
        -ProjectPath $ProjectPath `
        -PublishedNoticePath $publishedThirdPartyNoticesPath
    Move-Item `
        -LiteralPath $publishedThirdPartyNoticesPath `
        -Destination $bundledThirdPartyNoticesPath

    $infoPlist = [System.IO.File]::ReadAllText($InfoPlistTemplatePath)
    $infoPlist = $infoPlist.Replace("__EXECUTABLE_NAME__", $ExecutableName)
    $infoPlist = $infoPlist.Replace("__BUNDLE_IDENTIFIER__", $BundleIdentifier)
    $infoPlist = $infoPlist.Replace("__VERSION__", $ProductVersion)
    if ($infoPlist.Contains("__", [System.StringComparison]::Ordinal)) {
        throw "Info.plist 템플릿에 치환되지 않은 토큰이 있습니다."
    }

    $infoPlistPath = Join-Path $contentsPath "Info.plist"
    $utf8WithoutBom = [System.Text.UTF8Encoding]::new($false)
    [System.IO.File]::WriteAllText($infoPlistPath, $infoPlist, $utf8WithoutBom)
    $macOSIconPath = Join-Path $resourcesPath "AppIcon.icns"
    New-MacOSAppIcon -SourcePath $AppIconPath -DestinationPath $macOSIconPath

    $executablePath = Join-Path $macOSPath $ExecutableName
    Assert-NonEmptyFile -Path $executablePath
    Assert-NonEmptyFile -Path (Join-Path $macOSPath "libcoreclr.dylib")
    Assert-MacOSEventKitBridgeBinary -Path $eventKitBridgePath -RuntimeIdentifier $RuntimeIdentifier
    Assert-NonEmptyFile -Path $infoPlistPath
    Assert-NonEmptyFile -Path $macOSIconPath
    Assert-RequiredThirdPartyNoticeFiles `
        -ProjectPath $ProjectPath `
        -PublishedNoticePath $bundledThirdPartyNoticesPath
    Assert-MacOSPublishedBinaryArchitectures `
        -Path $macOSPath `
        -RuntimeIdentifier $RuntimeIdentifier
    Assert-MacOSInfoPlist `
        -Path $infoPlistPath `
        -ExecutableName $ExecutableName `
        -BundleIdentifier $BundleIdentifier `
        -ProductVersion $ProductVersion
    Remove-DebugSymbols -Path $bundlePath

    if ($IsMacOS) {
        Set-UnixExecutableFileMode -Path $executablePath
    }

    $archiveFileName = "TimetableGenerator-$ProductVersion-$RuntimeIdentifier-unsigned.zip"
    New-DistributionArchive `
        -SourcePath $bundlePath `
        -OutputRoot $OutputRoot `
        -ArchiveFileName $archiveFileName `
        -ArchiveRootName "Timetable Generator.app" `
        -ArchivePlatform "MacOS"
}
