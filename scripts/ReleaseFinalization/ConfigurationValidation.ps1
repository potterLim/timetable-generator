function Assert-RequiredConfigurationFiles {
    param(
        [Parameter(Mandatory)]
        [string] $Path
    )

    $catalogConfigurationPath = Join-Path $Path "catalog-source.local.json"
    $googleConfigurationPath = Join-Path $Path "google-calendar.local.json"
    Assert-CatalogConfiguration -Path $catalogConfigurationPath
    Assert-GoogleCalendarConfiguration -Path $googleConfigurationPath
}

function Read-ReleaseConfigurationJsonObject {
    param(
        [Parameter(Mandatory)]
        [string] $Path
    )

    Assert-NonEmptyFile -Path $Path
    $file = Get-Item -LiteralPath $Path
    if ($file.Length -gt 16384) {
        throw "Release 설정 파일이 제품 크기 제한을 초과합니다: $Path"
    }

    try {
        $stream = [System.IO.File]::OpenRead($Path)
        try {
            $document = [System.Text.Json.JsonDocument]::Parse($stream)
            try {
                if ($document.RootElement.ValueKind -ne [System.Text.Json.JsonValueKind]::Object) {
                    throw "Release 설정 파일의 JSON root는 object여야 합니다: $Path"
                }

                return $document.RootElement.Clone()
            }
            finally {
                $document.Dispose()
            }
        }
        finally {
            $stream.Dispose()
        }
    }
    catch [System.Text.Json.JsonException] {
        throw "Release 설정 파일이 유효한 JSON이 아닙니다: $Path"
    }
}

function Assert-ExactJsonProperties {
    param(
        [Parameter(Mandatory)]
        [System.Text.Json.JsonElement] $Element,

        [Parameter(Mandatory)]
        [string[]] $ExpectedPropertyNames,

        [Parameter(Mandatory)]
        [string] $Path
    )

    $expectedNames = [System.Collections.Generic.HashSet[string]]::new($ExpectedPropertyNames, [System.StringComparer]::Ordinal)
    $discoveredNames = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::Ordinal)
    foreach ($property in $Element.EnumerateObject()) {
        if ($expectedNames.Contains($property.Name) -eq $false) {
            throw "Release 설정 파일에 허용되지 않은 속성이 있습니다: $Path ($($property.Name))"
        }

        if ($discoveredNames.Add($property.Name) -eq $false) {
            throw "Release 설정 파일에 중복 속성이 있습니다: $Path ($($property.Name))"
        }
    }

    if ($discoveredNames.SetEquals($expectedNames) -eq $false) {
        throw "Release 설정 파일에 필수 속성이 없습니다: $Path"
    }
}

function Assert-SchemaVersionOne {
    param(
        [Parameter(Mandatory)]
        [System.Text.Json.JsonElement] $Element,

        [Parameter(Mandatory)]
        [string] $Path
    )

    $schemaVersionElement = $Element.GetProperty("schemaVersion")
    [int] $schemaVersion = 0
    if ($schemaVersionElement.ValueKind -ne [System.Text.Json.JsonValueKind]::Number -or
        $schemaVersionElement.TryGetInt32([ref] $schemaVersion) -eq $false -or
        $schemaVersion -ne 1) {
        throw "Release 설정 파일의 schemaVersion은 1이어야 합니다: $Path"
    }
}

function Assert-SchemaVersionTwo {
    param(
        [Parameter(Mandatory)]
        [System.Text.Json.JsonElement] $Element,

        [Parameter(Mandatory)]
        [string] $Path
    )

    $schemaVersionElement = $Element.GetProperty("schemaVersion")
    [int] $schemaVersion = 0
    if ($schemaVersionElement.ValueKind -ne [System.Text.Json.JsonValueKind]::Number -or
        $schemaVersionElement.TryGetInt32([ref] $schemaVersion) -eq $false -or
        $schemaVersion -ne 2) {
        throw "Release Google OAuth 설정 파일의 schemaVersion은 2여야 합니다: $Path"
    }
}

function Assert-GoogleOAuthClientSecret {
    param(
        [Parameter(Mandatory)]
        [System.Text.Json.JsonElement] $Element,

        [Parameter(Mandatory)]
        [string] $Path
    )

    if ($Element.ValueKind -ne [System.Text.Json.JsonValueKind]::String) {
        throw "Release Google OAuth clientSecret은 문자열이어야 합니다: $Path"
    }

    $clientSecret = [string] $Element.GetString()
    if ([string]::IsNullOrEmpty($clientSecret) -or
        $clientSecret.Length -gt 1024 -or
        [string]::Equals($clientSecret, $clientSecret.Trim(), [System.StringComparison]::Ordinal) -eq $false) {
        throw "Release Google OAuth clientSecret의 형식이 유효하지 않습니다: $Path"
    }

    foreach ($character in $clientSecret.ToCharArray()) {
        if ([char]::IsControl($character)) {
            throw "Release Google OAuth clientSecret의 형식이 유효하지 않습니다: $Path"
        }
    }
}

function Assert-CatalogConfiguration {
    param(
        [Parameter(Mandatory)]
        [string] $Path
    )

    $element = Read-ReleaseConfigurationJsonObject -Path $Path
    Assert-ExactJsonProperties `
        -Element $element `
        -ExpectedPropertyNames @("schemaVersion", "indexUri") `
        -Path $Path
    Assert-SchemaVersionOne -Element $element -Path $Path

    $indexUriElement = $element.GetProperty("indexUri")
    if ($indexUriElement.ValueKind -ne [System.Text.Json.JsonValueKind]::String) {
        throw "Release catalog indexUri는 문자열이어야 합니다: $Path"
    }

    $indexUriText = [string] $indexUriElement.GetString()
    $indexUri = $null
    if ([string]::IsNullOrWhiteSpace($indexUriText) -or
        [System.Uri]::TryCreate($indexUriText.Trim(), [System.UriKind]::Absolute, [ref] $indexUri) -eq $false -or
        $null -eq $indexUri -or
        $indexUri.Scheme -ne [System.Uri]::UriSchemeHttps -or
        [string]::IsNullOrWhiteSpace($indexUri.Host) -or
        $indexUri.UserInfo.Length -ne 0 -or
        $indexUri.Fragment.Length -ne 0) {
        throw "Release catalog indexUri는 자격 증명과 fragment가 없는 HTTPS URL이어야 합니다: $Path"
    }
}

function Assert-GoogleCalendarConfiguration {
    param(
        [Parameter(Mandatory)]
        [string] $Path
    )

    $element = Read-ReleaseConfigurationJsonObject -Path $Path
    Assert-ExactJsonProperties `
        -Element $element `
        -ExpectedPropertyNames @("schemaVersion", "clientId", "clientSecret") `
        -Path $Path
    Assert-SchemaVersionTwo -Element $element -Path $Path

    $clientIdElement = $element.GetProperty("clientId")
    if ($clientIdElement.ValueKind -ne [System.Text.Json.JsonValueKind]::String) {
        throw "Release Google OAuth clientId는 문자열이어야 합니다: $Path"
    }

    $clientId = ([string] $clientIdElement.GetString()).Trim()
    if ($clientId -notmatch "^[A-Za-z0-9-]+\.apps\.googleusercontent\.com$" -or
        $clientId.Contains("example", [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Release Google OAuth clientId가 유효한 Desktop client ID가 아닙니다: $Path"
    }

    Assert-GoogleOAuthClientSecret `
        -Element $element.GetProperty("clientSecret") `
        -Path $Path
}

function Assert-RequiredNoticeFiles {
    param(
        [Parameter(Mandatory)]
        [string] $Path
    )

    foreach ($fileName in $script:REQUIRED_NOTICE_FILE_NAMES) {
        Assert-NonEmptyFile -Path (Join-Path $Path $fileName)
    }
}
