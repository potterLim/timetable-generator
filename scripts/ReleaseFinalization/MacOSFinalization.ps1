function Invoke-NativeCommand {
    param(
        [Parameter(Mandatory)]
        [string] $Command,

        [Parameter(Mandatory)]
        [string[]] $Arguments,

        [Parameter(Mandatory)]
        [string] $FailureMessage
    )

    & $Command @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "$FailureMessage (exit code: $LASTEXITCODE)"
    }
}

function Get-MacOSPlistValue {
    param(
        [Parameter(Mandatory)]
        [string] $InfoPlistPath,

        [Parameter(Mandatory)]
        [string] $Key
    )

    $value = & plutil -extract $Key raw -o - -- $InfoPlistPath
    if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace([string] $value)) {
        throw "Info.plist에서 $Key 값을 읽을 수 없습니다: $InfoPlistPath"
    }

    return ([string] $value).Trim()
}

function Assert-MacOSSignatureAndNotarization {
    param(
        [Parameter(Mandatory)]
        [string] $ApplicationPath,

        [Parameter(Mandatory)]
        [string] $MainExecutablePath
    )

    Invoke-NativeCommand `
        -Command "codesign" `
        -Arguments @("--verify", "--deep", "--strict", "--verbose=2", "--", $ApplicationPath) `
        -FailureMessage "macOS codesign strict 검증에 실패했습니다."
    Invoke-NativeCommand `
        -Command "spctl" `
        -Arguments @("--assess", "--type", "execute", "--verbose=4", "--", $ApplicationPath) `
        -FailureMessage "macOS Gatekeeper 평가에 실패했습니다."
    Invoke-NativeCommand `
        -Command "xcrun" `
        -Arguments @("stapler", "validate", $ApplicationPath) `
        -FailureMessage "macOS notarization ticket 검증에 실패했습니다."

    $signingDetails = (& codesign -d --verbose=4 -- $ApplicationPath 2>&1) -join "`n"
    if ($LASTEXITCODE -ne 0 -or
        $signingDetails.Contains(
            "Authority=Developer ID Application:",
            [System.StringComparison]::Ordinal) -eq $false -or
        $signingDetails -notmatch "flags=.*\(runtime\)") {
        throw "macOS 앱이 Developer ID Application과 hardened runtime으로 서명되지 않았습니다."
    }

    $entitlements = (& codesign -d --entitlements :- -- $MainExecutablePath 2>&1) -join "`n"
    if ($LASTEXITCODE -ne 0 -or
        $entitlements.Contains("com.apple.security.cs.allow-jit", [System.StringComparison]::Ordinal) -eq $false -or
        $entitlements.Contains("com.apple.security.automation.apple-events", [System.StringComparison]::Ordinal) -eq $false) {
        throw "서명된 macOS 앱에 필수 JIT 또는 Apple Events entitlement가 없습니다."
    }
}

function Invoke-MacOSFinalization {
    param(
        [Parameter(Mandatory)]
        [string] $RepositoryRoot,

        [Parameter(Mandatory)]
        [string] $Version,

        [Parameter(Mandatory)]
        [ValidateSet("osx-arm64")]
        [string] $Runtime,

        [Parameter(Mandatory)]
        [string] $BundleIdentifier,

        [string] $SourcePath,

        [string] $OutputRoot,

        [switch] $AllowUnsigned
    )

    if ($IsMacOS -eq $false) {
        throw "macOS Release 최종화는 macOS에서만 실행할 수 있습니다."
    }

    foreach ($commandName in @("codesign", "ditto", "plutil", "spctl", "xcrun")) {
        if ($null -eq (Get-Command $commandName -CommandType Application -ErrorAction SilentlyContinue)) {
            throw "macOS Release 최종화에 필요한 명령을 찾을 수 없습니다: $commandName"
        }
    }

    if ($script:PLACEHOLDER_BUNDLE_IDENTIFIERS -contains $BundleIdentifier) {
        throw "placeholder Bundle ID는 Release 최종화에 사용할 수 없습니다."
    }

    $applicationPath = if ([string]::IsNullOrWhiteSpace($SourcePath)) {
        Resolve-PathFromRepository `
            -RepositoryRoot $RepositoryRoot `
            -Path "artifacts/publish/$Runtime/$($script:MACOS_APPLICATION_NAME)"
    }
    else {
        Resolve-PathFromRepository -RepositoryRoot $RepositoryRoot -Path $SourcePath
    }
    if ((Test-Path -LiteralPath $applicationPath -PathType Container) -eq $false) {
        throw "macOS 앱 번들을 찾을 수 없습니다: $applicationPath"
    }
    Assert-TreeHasNoReparsePoint -Path $applicationPath

    $contentsPath = Join-Path $applicationPath "Contents"
    $macOSPath = Join-Path $contentsPath "MacOS"
    $resourcesPath = Join-Path $contentsPath "Resources"
    $executablePath = Join-Path $macOSPath $script:PRODUCT_EXECUTABLE_BASE_NAME
    $infoPlistPath = Join-Path $contentsPath "Info.plist"
    foreach ($requiredPath in @(
        $executablePath,
        (Join-Path $macOSPath "$($script:PRODUCT_EXECUTABLE_BASE_NAME).dll"),
        (Join-Path $macOSPath "$($script:PRODUCT_EXECUTABLE_BASE_NAME).deps.json"),
        (Join-Path $macOSPath "$($script:PRODUCT_EXECUTABLE_BASE_NAME).runtimeconfig.json"),
        (Join-Path $macOSPath "libcoreclr.dylib"),
        $infoPlistPath,
        (Join-Path $resourcesPath "AppIcon.icns"))) {
        Assert-NonEmptyFile -Path $requiredPath
    }

    Assert-NoDebugSymbols -Path $applicationPath
    Assert-RequiredConfigurationFiles -Path $macOSPath
    Assert-RequiredNoticeFiles -Path (Join-Path $resourcesPath "ThirdPartyNotices")
    Invoke-NativeCommand `
        -Command "plutil" `
        -Arguments @("-lint", "--", $infoPlistPath) `
        -FailureMessage "macOS Info.plist 검증에 실패했습니다."
    $expectedPlistValues = @{
        CFBundleDisplayName = $script:PRODUCT_DISPLAY_NAME
        CFBundleExecutable = $script:PRODUCT_EXECUTABLE_BASE_NAME
        CFBundleName = $script:PRODUCT_DISPLAY_NAME
        CFBundlePackageType = "APPL"
    }
    foreach ($key in $expectedPlistValues.Keys) {
        $actualValue = Get-MacOSPlistValue -InfoPlistPath $infoPlistPath -Key $key
        $expectedValue = [string] $expectedPlistValues[$key]
        if ($actualValue -cne $expectedValue) {
            throw "macOS 제품 메타데이터가 예상과 일치하지 않습니다: $key ($actualValue)"
        }
    }

    $actualVersion = Get-MacOSPlistValue `
        -InfoPlistPath $infoPlistPath `
        -Key "CFBundleShortVersionString"
    if ($actualVersion -ne $Version) {
        throw "macOS 앱 버전이 요청한 Release 버전과 일치하지 않습니다: $actualVersion"
    }

    $actualBundleVersion = Get-MacOSPlistValue `
        -InfoPlistPath $infoPlistPath `
        -Key "CFBundleVersion"
    if ($actualBundleVersion -ne $Version) {
        throw "macOS 앱 build 버전이 요청한 Release 버전과 일치하지 않습니다: $actualBundleVersion"
    }

    $actualBundleIdentifier = Get-MacOSPlistValue `
        -InfoPlistPath $infoPlistPath `
        -Key "CFBundleIdentifier"
    if ($actualBundleIdentifier -ne $BundleIdentifier) {
        throw "macOS Bundle ID가 요청한 값과 일치하지 않습니다: $actualBundleIdentifier"
    }

    $usageDescription = Get-MacOSPlistValue `
        -InfoPlistPath $infoPlistPath `
        -Key "NSAppleEventsUsageDescription"
    if ([string]::IsNullOrWhiteSpace($usageDescription)) {
        throw "macOS 앱에 Apple Events 사용 설명이 없습니다."
    }
    Assert-MacOSPublishedBinaryArchitectures `
        -Path $applicationPath `
        -RuntimeIdentifier $Runtime

    if ($AllowUnsigned -eq $false) {
        Assert-MacOSSignatureAndNotarization `
            -ApplicationPath $applicationPath `
            -MainExecutablePath $executablePath
    }

    $releaseRoot = Resolve-ReleaseOutputRoot `
        -RepositoryRoot $RepositoryRoot `
        -Version $Version `
        -RequestedOutputRoot $OutputRoot `
        -SourcePath $applicationPath `
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
    Remove-ExistingReleaseFile `
        -OutputRoot $releaseRoot `
        -Path $archivePath `
        -ExpectedFileName $archiveFileName
    try {
        Invoke-NativeCommand `
            -Command "ditto" `
            -Arguments @(
                "-c",
                "-k",
                "--sequesterRsrc",
                "--keepParent",
                $applicationPath,
                $archivePath) `
            -FailureMessage "macOS 최종 Release ZIP 생성에 실패했습니다."

        $applicationPrefix = "$($script:MACOS_APPLICATION_NAME)/"
        $contentsPrefix = "${applicationPrefix}Contents/"
        $requiredEntries = @(
            "${contentsPrefix}Info.plist",
            "${contentsPrefix}MacOS/$($script:PRODUCT_EXECUTABLE_BASE_NAME)",
            "${contentsPrefix}MacOS/$($script:PRODUCT_EXECUTABLE_BASE_NAME).dll",
            "${contentsPrefix}MacOS/$($script:PRODUCT_EXECUTABLE_BASE_NAME).deps.json",
            "${contentsPrefix}MacOS/$($script:PRODUCT_EXECUTABLE_BASE_NAME).runtimeconfig.json",
            "${contentsPrefix}MacOS/libcoreclr.dylib",
            "${contentsPrefix}Resources/AppIcon.icns"
        )
        foreach ($configurationFileName in $script:CONFIGURATION_FILE_NAMES) {
            $requiredEntries += "${contentsPrefix}MacOS/$configurationFileName"
        }
        foreach ($noticeFileName in $script:REQUIRED_NOTICE_FILE_NAMES) {
            $requiredEntries += "${contentsPrefix}Resources/ThirdPartyNotices/$noticeFileName"
        }

        Assert-ArchiveEntries `
            -ArchivePath $archivePath `
            -RequiredEntryNames $requiredEntries `
            -RequiredPrefix $applicationPrefix `
            -AllowMacOSMetadataEntries
    }
    catch {
        Remove-ExistingReleaseFile `
            -OutputRoot $releaseRoot `
            -Path $archivePath `
            -ExpectedFileName $archiveFileName
        throw
    }
    Write-Host "macOS Release ZIP을 생성했습니다: $archivePath"
}
