$script:PRODUCT_EXECUTABLE_BASE_NAME = "TimetableGenerator"
$script:PRODUCT_DISPLAY_NAME = "Timetable Generator"
$script:PRODUCT_COMPANY_NAME = "potterLim"
$script:MACOS_APPLICATION_NAME = "Timetable Generator.app"
$script:PLACEHOLDER_BUNDLE_IDENTIFIERS = @(
    "com.example.timetable",
    "com.example.timetablegenerator"
)
$desktopProjectPath = Join-Path `
    $PSScriptRoot `
    "../../src/TimetableGenerator.Desktop/TimetableGenerator.Desktop.csproj"
$script:REQUIRED_NOTICE_FILE_NAMES = @(
    Get-RequiredThirdPartyNoticeFileNames -ProjectPath $desktopProjectPath
)
$script:CONFIGURATION_FILE_NAMES = @(
    "catalog-source.local.json",
    "google-calendar.local.json"
)
