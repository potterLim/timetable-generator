namespace TimetableGenerator.Desktop.Exporting.AppleCalendar;

internal enum EAppleCalendarExportProgressStage
{
    None = 0,
    CheckingCalendar = 1,
    SavingEvents = 2,
    Finalizing = 3,
}
