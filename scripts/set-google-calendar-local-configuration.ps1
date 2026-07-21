[CmdletBinding()]
param(
    [string] $Path = (
        Join-Path $PSScriptRoot `
            "../src/TimetableGenerator.Desktop/google-calendar.local.json")
)

$ErrorActionPreference = "Stop"

$resolvedPath = [System.IO.Path]::GetFullPath($Path)
if ([System.IO.File]::Exists($resolvedPath) -eq $false) {
    throw "기존 Google Calendar 로컬 설정 파일을 찾을 수 없습니다: $resolvedPath"
}

try {
    $configuration = Get-Content -LiteralPath $resolvedPath -Raw | ConvertFrom-Json
}
catch {
    throw "Google Calendar 로컬 설정 파일이 유효한 JSON이 아닙니다: $resolvedPath"
}

$clientId = [string] $configuration.clientId
if ([string]::IsNullOrWhiteSpace($clientId) -or
    $clientId -notmatch "^[A-Za-z0-9-]+\.apps\.googleusercontent\.com$") {
    throw "Google Calendar 로컬 설정에 유효한 clientId가 없습니다: $resolvedPath"
}

$clientSecret = [string] (Get-Clipboard -Raw)
if ([string]::IsNullOrWhiteSpace($clientSecret) -or
    $clientSecret.Length -gt 1024 -or
    $clientSecret -ne $clientSecret.Trim() -or
    $clientSecret.IndexOfAny([char[]] @(0..31 + 127)) -ge 0) {
    throw "클립보드에 유효한 Desktop OAuth client secret이 없습니다."
}

$updatedConfiguration = [ordered] @{
    schemaVersion = 2
    clientId = $clientId
    clientSecret = $clientSecret
}
$json = $updatedConfiguration | ConvertTo-Json
$utf8WithoutBom = [System.Text.UTF8Encoding]::new($false)
[System.IO.File]::WriteAllText($resolvedPath, $json + [Environment]::NewLine, $utf8WithoutBom)
Set-Clipboard -Value $null

Write-Host "Google Calendar 로컬 설정을 schemaVersion 2로 갱신했습니다."
