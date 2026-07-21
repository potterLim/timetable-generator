#requires -Version 7.0

<#
.SYNOPSIS
Validates signed desktop applications and creates final GitHub Release assets.

.DESCRIPTION
Publishing and finalization are intentionally separate operations. Run
publish-desktop.ps1 first, sign the Windows executable, and sign, notarize,
and staple each macOS application before running this command.

AllowUnsigned is for local archive-structure smoke tests only. It always
produces an archive whose name contains "unsigned-smoke". Aggregate never
accepts those archives.

.EXAMPLE
pwsh ./scripts/finalize-desktop-release.ps1 -Stage Windows -Version 1.0.0

.EXAMPLE
pwsh ./scripts/finalize-desktop-release.ps1 -Stage MacOS -Runtime osx-arm64 `
  -Version 1.0.0 -BundleIdentifier io.github.potterlim.timetable

.EXAMPLE
pwsh ./scripts/finalize-desktop-release.ps1 -Stage Aggregate -Version 1.0.0
#>

[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [ValidateSet("Windows", "MacOS", "Aggregate")]
    [string] $Stage,

    [Parameter(Mandatory)]
    [ValidatePattern("^\d+\.\d+\.\d+$")]
    [string] $Version,

    [ValidateSet("osx-x64", "osx-arm64")]
    [string] $Runtime,

    [ValidatePattern("^[A-Za-z0-9-]+(?:\.[A-Za-z0-9-]+)+$")]
    [string] $BundleIdentifier = "io.github.potterlim.timetable",

    [string] $SourcePath,

    [string] $OutputRoot,

    [switch] $AllowUnsigned
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$repositoryRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot ".."))
$modulePath = Join-Path `
    $PSScriptRoot `
    "ReleaseFinalization/TimetableGenerator.ReleaseFinalization.psm1"

Import-Module -Name $modulePath -Force

$parameters = @{
    Stage = $Stage
    Version = $Version
    RepositoryRoot = $repositoryRoot
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
