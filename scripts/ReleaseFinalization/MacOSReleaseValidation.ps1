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

function Test-MacOSPlistKeyExists {
    param(
        [Parameter(Mandatory)]
        [string] $InfoPlistPath,

        [Parameter(Mandatory)]
        [string] $Key
    )

    $null = & plutil -extract $Key raw -o - -- $InfoPlistPath 2>$null
    return $LASTEXITCODE -eq 0
}

function Get-MacOSDeveloperIDTeamIdentifierFromSigningDetails {
    param(
        [Parameter(Mandatory)]
        [string] $SigningDetails,

        [Parameter(Mandatory)]
        [string] $ArtifactDescription
    )

    $isDeveloperIDApplication = $SigningDetails.Contains("Authority=Developer ID Application:", [System.StringComparison]::Ordinal)
    $hasHardenedRuntime = $SigningDetails -match "(?m)^CodeDirectory .*\bflags=.*\(runtime\)"
    $teamIdentifierMatch = [System.Text.RegularExpressions.Regex]::Match($SigningDetails, "(?m)^TeamIdentifier=([A-Z0-9]+)\r?$")
    if (-not $isDeveloperIDApplication -or -not $hasHardenedRuntime -or -not $teamIdentifierMatch.Success) {
        throw "$ArtifactDescription이(가) Developer ID Application과 hardened runtime으로 서명되지 않았거나 TeamIdentifier가 없습니다."
    }

    return $teamIdentifierMatch.Groups[1].Value
}

function Get-MacOSDeveloperIDTeamIdentifier {
    param(
        [Parameter(Mandatory)]
        [string] $Path,

        [Parameter(Mandatory)]
        [string] $ArtifactDescription
    )

    $signingDetails = (& codesign -d --verbose=4 -- $Path 2>&1) -join "`n"
    if ($LASTEXITCODE -ne 0) {
        throw "$ArtifactDescription의 코드 서명 정보를 읽을 수 없습니다."
    }

    return Get-MacOSDeveloperIDTeamIdentifierFromSigningDetails -SigningDetails $signingDetails -ArtifactDescription $ArtifactDescription
}

function Assert-MacOSMatchingTeamIdentifiers {
    param(
        [Parameter(Mandatory)]
        [string] $ApplicationTeamIdentifier,

        [Parameter(Mandatory)]
        [string] $MainExecutableTeamIdentifier,

        [Parameter(Mandatory)]
        [string] $EventKitBridgeTeamIdentifier
    )

    if ([string]::IsNullOrWhiteSpace($ApplicationTeamIdentifier) -or
        $ApplicationTeamIdentifier -cne $MainExecutableTeamIdentifier -or
        $ApplicationTeamIdentifier -cne $EventKitBridgeTeamIdentifier) {
        throw "macOS 앱, 주 실행 파일, EventKit 네이티브 모듈의 TeamIdentifier가 정확히 일치하지 않습니다."
    }
}

function Assert-MacOSSignatureAndNotarization {
    param(
        [Parameter(Mandatory)]
        [string] $ApplicationPath,

        [Parameter(Mandatory)]
        [string] $MainExecutablePath,

        [Parameter(Mandatory)]
        [string] $EventKitBridgePath
    )

    Invoke-NativeCommand -Command "codesign" -Arguments @("--verify", "--deep", "--strict", "--verbose=2", "--", $ApplicationPath) -FailureMessage "macOS codesign strict 검증에 실패했습니다."
    Invoke-NativeCommand -Command "spctl" -Arguments @("--assess", "--type", "execute", "--verbose=4", "--", $ApplicationPath) -FailureMessage "macOS Gatekeeper 평가에 실패했습니다."
    Invoke-NativeCommand -Command "xcrun" -Arguments @("stapler", "validate", $ApplicationPath) -FailureMessage "macOS notarization ticket 검증에 실패했습니다."
    Invoke-NativeCommand -Command "codesign" -Arguments @("--verify", "--strict", "--verbose=2", "--", $MainExecutablePath) -FailureMessage "macOS 주 실행 파일의 codesign 검증에 실패했습니다."
    Invoke-NativeCommand -Command "codesign" -Arguments @("--verify", "--strict", "--verbose=2", "--", $EventKitBridgePath) -FailureMessage "EventKit 네이티브 모듈의 codesign 검증에 실패했습니다."

    $applicationTeamIdentifier = Get-MacOSDeveloperIDTeamIdentifier -Path $ApplicationPath -ArtifactDescription "macOS 앱"
    $mainExecutableTeamIdentifier = Get-MacOSDeveloperIDTeamIdentifier -Path $MainExecutablePath -ArtifactDescription "macOS 주 실행 파일"
    $eventKitBridgeTeamIdentifier = Get-MacOSDeveloperIDTeamIdentifier -Path $EventKitBridgePath -ArtifactDescription "EventKit 네이티브 모듈"
    Assert-MacOSMatchingTeamIdentifiers -ApplicationTeamIdentifier $applicationTeamIdentifier -MainExecutableTeamIdentifier $mainExecutableTeamIdentifier -EventKitBridgeTeamIdentifier $eventKitBridgeTeamIdentifier

    $entitlements = (& codesign -d --entitlements :- -- $MainExecutablePath 2>&1) -join "`n"
    if ($LASTEXITCODE -ne 0 -or
        $entitlements.Contains("com.apple.security.cs.allow-jit", [System.StringComparison]::Ordinal) -eq $false -or
        $entitlements.Contains("com.apple.security.personal-information.calendars", [System.StringComparison]::Ordinal) -eq $false -or
        $entitlements.Contains("com.apple.security.automation.apple-events", [System.StringComparison]::Ordinal)) {
        throw "서명된 macOS 앱의 JIT, Calendar 또는 Apple Events entitlement 구성이 유효하지 않습니다."
    }
}

function Get-MacOSReleaseBundleLayout {
    param(
        [Parameter(Mandatory)]
        [string] $ApplicationPath
    )

    $contentsPath = Join-Path $ApplicationPath "Contents"
    $macOSPath = Join-Path $contentsPath "MacOS"
    $resourcesPath = Join-Path $contentsPath "Resources"
    return [pscustomobject] @{
        ApplicationPath = $ApplicationPath
        ContentsPath = $contentsPath
        MacOSPath = $macOSPath
        ResourcesPath = $resourcesPath
        ExecutablePath = Join-Path $macOSPath $script:PRODUCT_EXECUTABLE_BASE_NAME
        EventKitBridgePath = Join-Path $macOSPath $script:MACOS_EVENTKIT_BRIDGE_FILE_NAME
        InfoPlistPath = Join-Path $contentsPath "Info.plist"
    }
}

function Assert-MacOSReleaseBundle {
    param(
        [Parameter(Mandatory)]
        [object] $Layout,

        [Parameter(Mandatory)]
        [string] $Version,

        [Parameter(Mandatory)]
        [ValidateSet("osx-arm64")]
        [string] $Runtime,

        [Parameter(Mandatory)]
        [string] $BundleIdentifier,

        [switch] $AllowUnsigned
    )

    foreach ($requiredPath in @(
        $Layout.ExecutablePath,
        (Join-Path $Layout.MacOSPath "$($script:PRODUCT_EXECUTABLE_BASE_NAME).dll"),
        (Join-Path $Layout.MacOSPath "$($script:PRODUCT_EXECUTABLE_BASE_NAME).deps.json"),
        (Join-Path $Layout.MacOSPath "$($script:PRODUCT_EXECUTABLE_BASE_NAME).runtimeconfig.json"),
        (Join-Path $Layout.MacOSPath "libcoreclr.dylib"),
        $Layout.EventKitBridgePath,
        $Layout.InfoPlistPath,
        (Join-Path $Layout.ResourcesPath "AppIcon.icns"))) {
        Assert-NonEmptyFile -Path $requiredPath
    }

    Assert-MacOSEventKitBridgeBinary -Path $Layout.EventKitBridgePath -RuntimeIdentifier $Runtime
    Assert-NoDebugSymbols -Path $Layout.ApplicationPath
    Assert-RequiredConfigurationFiles -Path $Layout.MacOSPath
    Assert-RequiredNoticeFiles -Path (Join-Path $Layout.ResourcesPath "ThirdPartyNotices")
    Invoke-NativeCommand -Command "plutil" -Arguments @("-lint", "--", $Layout.InfoPlistPath) -FailureMessage "macOS Info.plist 검증에 실패했습니다."

    $expectedPlistValues = @{
        CFBundleDisplayName = $script:PRODUCT_DISPLAY_NAME
        CFBundleExecutable = $script:PRODUCT_EXECUTABLE_BASE_NAME
        CFBundleName = $script:PRODUCT_DISPLAY_NAME
        CFBundlePackageType = "APPL"
    }
    foreach ($key in $expectedPlistValues.Keys) {
        $actualValue = Get-MacOSPlistValue -InfoPlistPath $Layout.InfoPlistPath -Key $key
        $expectedValue = [string] $expectedPlistValues[$key]
        if ($actualValue -cne $expectedValue) {
            throw "macOS 제품 메타데이터가 예상과 일치하지 않습니다: $key ($actualValue)"
        }
    }

    $actualVersion = Get-MacOSPlistValue -InfoPlistPath $Layout.InfoPlistPath -Key "CFBundleShortVersionString"
    if ($actualVersion -ne $Version) {
        throw "macOS 앱 버전이 요청한 Release 버전과 일치하지 않습니다: $actualVersion"
    }

    $actualBundleVersion = Get-MacOSPlistValue -InfoPlistPath $Layout.InfoPlistPath -Key "CFBundleVersion"
    if ($actualBundleVersion -ne $Version) {
        throw "macOS 앱 build 버전이 요청한 Release 버전과 일치하지 않습니다: $actualBundleVersion"
    }

    $actualBundleIdentifier = Get-MacOSPlistValue -InfoPlistPath $Layout.InfoPlistPath -Key "CFBundleIdentifier"
    if ($actualBundleIdentifier -ne $BundleIdentifier) {
        throw "macOS Bundle ID가 요청한 값과 일치하지 않습니다: $actualBundleIdentifier"
    }

    $usageDescription = Get-MacOSPlistValue -InfoPlistPath $Layout.InfoPlistPath -Key "NSCalendarsFullAccessUsageDescription"
    if ($usageDescription -cne $script:MACOS_CALENDAR_USAGE_DESCRIPTION) {
        throw "macOS 앱의 캘린더 전체 접근 사용 설명이 예상과 일치하지 않습니다."
    }
    if (Test-MacOSPlistKeyExists -InfoPlistPath $Layout.InfoPlistPath -Key "NSAppleEventsUsageDescription") {
        throw "macOS 앱에 사용하지 않는 Apple Events 권한 설명이 남아 있습니다."
    }
    Assert-MacOSPublishedBinaryArchitectures -Path $Layout.ApplicationPath -RuntimeIdentifier $Runtime

    if ($AllowUnsigned -eq $false) {
        Assert-MacOSSignatureAndNotarization -ApplicationPath $Layout.ApplicationPath -MainExecutablePath $Layout.ExecutablePath -EventKitBridgePath $Layout.EventKitBridgePath
    }
}
