#requires -Version 7.0

[CmdletBinding()]
param(
    [ValidateSet("all", "win-x64", "osx-x64", "osx-arm64")]
    [string] $Runtime = "all",

    [ValidatePattern("^\d+\.\d+\.\d+$")]
    [string] $Version,

    [ValidatePattern("^[A-Za-z0-9-]+(?:\.[A-Za-z0-9-]+)+$")]
    [string] $BundleIdentifier = "com.example.timetablegenerator",

    [string] $OutputRoot,

    [switch] $NoRestore
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"
$env:AVALONIA_TELEMETRY_OPTOUT = "1"

$repositoryRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot ".."))
$modulePath = Join-Path $PSScriptRoot "Distribution/TimetableGenerator.Distribution.psm1"

Import-Module -Name $modulePath -Force

$publishParameters = @{
    RepositoryRoot = $repositoryRoot
    Runtime = $Runtime
    BundleIdentifier = $BundleIdentifier
    NoRestore = $NoRestore
}

if (-not [string]::IsNullOrWhiteSpace($Version)) {
    $publishParameters.Version = $Version
}

if (-not [string]::IsNullOrWhiteSpace($OutputRoot)) {
    $publishParameters.OutputRoot = $OutputRoot
}

Publish-TimetableGeneratorDesktop @publishParameters
