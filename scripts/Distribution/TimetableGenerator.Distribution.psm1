Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$implementationFiles = @(
    "Common.ps1",
    "BinaryValidation.ps1",
    "MacOSEventKitBridgeValidation.ps1",
    "WindowsManifest.ps1",
    "MacOSPropertyList.ps1",
    "MacOSIcon.ps1",
    "Archive.ps1",
    "PublishTargets.ps1",
    "Distribution.ps1"
)

foreach ($implementationFile in $implementationFiles) {
    . (Join-Path $PSScriptRoot $implementationFile)
}

Export-ModuleMember -Function Publish-TimetableGeneratorDesktop
