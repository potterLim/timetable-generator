Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

. (Join-Path $PSScriptRoot "../Distribution/Common.ps1")
. (Join-Path $PSScriptRoot "../Distribution/BinaryValidation.ps1")
. (Join-Path $PSScriptRoot "ProductIdentity.ps1")
. (Join-Path $PSScriptRoot "PathUtilities.ps1")
. (Join-Path $PSScriptRoot "ArtifactValidation.ps1")
. (Join-Path $PSScriptRoot "ConfigurationValidation.ps1")
. (Join-Path $PSScriptRoot "ArchiveValidation.ps1")
. (Join-Path $PSScriptRoot "WindowsFinalization.ps1")
. (Join-Path $PSScriptRoot "MacOSFinalization.ps1")
. (Join-Path $PSScriptRoot "AggregateFinalization.ps1")
. (Join-Path $PSScriptRoot "ReleaseFinalization.ps1")

Export-ModuleMember -Function Invoke-TimetableGeneratorReleaseFinalization
