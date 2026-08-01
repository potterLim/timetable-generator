#requires -Version 7.0

<#
.SYNOPSIS
Validates desktop applications and creates final GitHub Release assets.

.DESCRIPTION
Publishing and finalization are intentionally separate operations. Run
publish-desktop.ps1 first. Windows releases may use the explicitly selected
signed or unsigned product policy. macOS releases must be signed, notarized,
and stapled before finalization.

AllowUnsigned is for local archive-structure smoke tests only. It always
produces an archive whose name contains "unsigned-smoke". Aggregate never
accepts those archives.

.EXAMPLE
pwsh ./scripts/finalize-desktop-release.ps1 -Stage Windows -Version 1.0.2

.EXAMPLE
pwsh ./scripts/finalize-desktop-release.ps1 -Stage Windows -Version 1.0.2 `
  -WindowsSignatureMode Unsigned

.EXAMPLE
pwsh ./scripts/finalize-desktop-release.ps1 -Stage MacOS -Runtime osx-arm64 `
  -Version 1.0.2 -BundleIdentifier io.github.potterlim.timetable

.EXAMPLE
pwsh ./scripts/finalize-desktop-release.ps1 -Stage Aggregate -Version 1.0.2 `
  -WindowsSignatureMode Unsigned
#>

[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [ValidateSet("Windows", "MacOS", "Aggregate")]
    [string] $Stage,

    [Parameter(Mandatory)]
    [ValidatePattern("^\d+\.\d+\.\d+$")]
    [string] $Version,

    [ValidateSet("osx-arm64")]
    [string] $Runtime,

    [ValidatePattern("^[A-Za-z0-9-]+(?:\.[A-Za-z0-9-]+)+$")]
    [string] $BundleIdentifier = "io.github.potterlim.timetable",

    [ValidateSet("Signed", "Unsigned")]
    [string] $WindowsSignatureMode = "Signed",

    [string] $SourcePath,

    [string] $OutputRoot,

    [switch] $AllowUnsigned
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$repositoryRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot ".."))
$modulePath = Join-Path $PSScriptRoot "ReleaseFinalization/TimetableGenerator.ReleaseFinalization.psm1"

Import-Module -Name $modulePath -Force

$parameters = @{
    Stage = $Stage
    Version = $Version
    RepositoryRoot = $repositoryRoot
    WindowsSignatureMode = $WindowsSignatureMode
    AllowUnsigned = $AllowUnsigned
}

if ([string]::IsNullOrWhiteSpace($Runtime) -eq $false) {
    $parameters.Runtime = $Runtime
}

if ([string]::IsNullOrWhiteSpace($BundleIdentifier) -eq $false) {
    $parameters.BundleIdentifier = $BundleIdentifier
}

if ([string]::IsNullOrWhiteSpace($SourcePath) -eq $false) {
    $parameters.SourcePath = $SourcePath
}

if ([string]::IsNullOrWhiteSpace($OutputRoot) -eq $false) {
    $parameters.OutputRoot = $OutputRoot
}

Invoke-TimetableGeneratorReleaseFinalization @parameters
