$script:PRODUCT_EXECUTABLE_BASE_NAME = "TimetableGenerator"
$script:PRODUCT_DISPLAY_NAME = "Timetable Generator"
$script:PRODUCT_COMPANY_NAME = "potterLim"
$script:MACOS_APPLICATION_NAME = "Timetable Generator.app"
$script:MACOS_EVENTKIT_BRIDGE_FILE_NAME = "libTimetableGenerator.EventKitBridge.dylib"
$script:MACOS_CALENDAR_USAGE_DESCRIPTION = "시간표를 Apple Calendar에 내보내고 앱이 만든 일정을 안전하게 갱신하려면 캘린더 전체 접근이 필요합니다."
$script:PLACEHOLDER_BUNDLE_IDENTIFIERS = @(
    "com.example.timetable",
    "com.example.timetablegenerator"
)
$desktopProjectPath = Join-Path $PSScriptRoot "../../src/TimetableGenerator.Desktop/TimetableGenerator.Desktop.csproj"
$script:REQUIRED_NOTICE_FILE_NAMES = @(Get-RequiredThirdPartyNoticeFileNames -ProjectPath $desktopProjectPath)
$script:CONFIGURATION_FILE_NAMES = @(
    "catalog-source.local.json",
    "google-calendar.local.json"
)
