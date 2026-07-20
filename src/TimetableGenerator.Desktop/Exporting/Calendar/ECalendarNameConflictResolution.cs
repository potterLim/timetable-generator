namespace TimetableGenerator.Desktop.Exporting.Calendar;

internal enum ECalendarNameConflictResolution
{
    None = 0,
    ReplaceExisting = 1,
    CreateWithAvailableName = 2,
    Cancel = 3,
}
