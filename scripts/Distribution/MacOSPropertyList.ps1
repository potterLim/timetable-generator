function Get-InfoPlistContents {
    param(
        [Parameter(Mandatory)]
        [string] $Path
    )

    $settings = [System.Xml.XmlReaderSettings]::new()
    $settings.DtdProcessing = [System.Xml.DtdProcessing]::Ignore
    $settings.XmlResolver = $null
    $reader = [System.Xml.XmlReader]::Create($Path, $settings)
    try {
        $document = [System.Xml.XmlDocument]::new()
        $document.XmlResolver = $null
        $document.Load($reader)
    }
    finally {
        $reader.Dispose()
    }

    $dictionary = $document.SelectSingleNode("/plist/dict")
    if ($null -eq $dictionary) {
        throw "Info.plist에 최상위 dict가 없습니다: $Path"
    }

    $elements = @($dictionary.ChildNodes | Where-Object NodeType -eq ([System.Xml.XmlNodeType]::Element))
    $keys = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::Ordinal)
    $stringValues = @{}
    $trueKeys = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::Ordinal)
    $arrayStringValues = @{}
    for ($index = 0; $index -lt $elements.Count; $index += 2) {
        if ($index + 1 -ge $elements.Count -or $elements[$index].Name -ne "key") {
            throw "Info.plist key/value 구조가 유효하지 않습니다: $Path"
        }

        $key = $elements[$index].InnerText
        if (-not $keys.Add($key)) {
            throw "Info.plist에 중복 key가 있습니다: $key"
        }

        $valueElement = $elements[$index + 1]
        if ($valueElement.Name -eq "string") {
            $stringValues[$key] = $valueElement.InnerText
        }
        elseif ($valueElement.Name -eq "true") {
            $null = $trueKeys.Add($key)
        }
        elseif ($valueElement.Name -eq "array") {
            $arrayStringValues[$key] = @(
                $valueElement.ChildNodes |
                    Where-Object Name -eq "string" |
                    ForEach-Object InnerText
            )
        }
    }

    return [pscustomobject]@{
        KeyCount = $keys.Count
        StringValues = $stringValues
        TrueKeys = $trueKeys
        ArrayStringValues = $arrayStringValues
    }
}

function Assert-MacOSEntitlements {
    param(
        [Parameter(Mandatory)]
        [string] $Path
    )

    $contents = Get-InfoPlistContents -Path $Path
    $requiredEntitlements = @(
        "com.apple.security.cs.allow-jit",
        "com.apple.security.automation.apple-events"
    )
    if ($contents.KeyCount -ne $requiredEntitlements.Count) {
        throw "macOS entitlement 구성이 예상과 일치하지 않습니다: $Path"
    }

    foreach ($requiredEntitlement in $requiredEntitlements) {
        if (-not $contents.TrueKeys.Contains($requiredEntitlement)) {
            throw "macOS entitlement에 $requiredEntitlement=true가 없습니다: $Path"
        }
    }
}

function Assert-MacOSInfoPlist {
    param(
        [Parameter(Mandatory)]
        [string] $Path,

        [Parameter(Mandatory)]
        [string] $ExecutableName,

        [Parameter(Mandatory)]
        [string] $BundleIdentifier,

        [Parameter(Mandatory)]
        [string] $ProductVersion
    )

    $contents = Get-InfoPlistContents -Path $Path
    $values = $contents.StringValues
    $expectedValues = [ordered]@{
        CFBundleDevelopmentRegion = "ko"
        CFBundleDisplayName = "시간표"
        CFBundleExecutable = $ExecutableName
        CFBundleIconFile = "AppIcon.icns"
        CFBundleIdentifier = $BundleIdentifier
        CFBundleInfoDictionaryVersion = "6.0"
        CFBundleName = "시간표"
        CFBundlePackageType = "APPL"
        CFBundleShortVersionString = $ProductVersion
        CFBundleVersion = $ProductVersion
        LSApplicationCategoryType = "public.app-category.education"
        LSMinimumSystemVersion = "14.0"
        NSAppleEventsUsageDescription = "시간표를 Apple 캘린더에 내보내기 위해 캘린더 앱을 사용합니다."
        NSPrincipalClass = "NSApplication"
    }

    foreach ($expectedValue in $expectedValues.GetEnumerator()) {
        if (-not $values.ContainsKey($expectedValue.Key) -or $values[$expectedValue.Key] -ne $expectedValue.Value) {
            throw "Info.plist의 $($expectedValue.Key) 값이 예상과 일치하지 않습니다: $Path"
        }
    }

    $supportedPlatforms = @($contents.ArrayStringValues.CFBundleSupportedPlatforms)
    if ($supportedPlatforms.Count -ne 1 -or $supportedPlatforms[0] -ne "MacOSX") {
        throw "Info.plist의 CFBundleSupportedPlatforms 값이 유효하지 않습니다: $Path"
    }

    foreach ($trueKey in @("NSHighResolutionCapable", "NSSupportsAutomaticGraphicsSwitching")) {
        if (-not $contents.TrueKeys.Contains($trueKey)) {
            throw "Info.plist의 $trueKey 값이 true가 아닙니다: $Path"
        }
    }

    if ($IsMacOS) {
        & plutil -lint -- $Path
        if ($LASTEXITCODE -ne 0) {
            throw "macOS Info.plist 검증에 실패했습니다: $Path"
        }
    }
}
