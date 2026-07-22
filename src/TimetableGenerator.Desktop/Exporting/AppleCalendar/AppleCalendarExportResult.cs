using System;

using TimetableGenerator.Domain.Planning;

namespace TimetableGenerator.Desktop.Exporting.AppleCalendar;

internal sealed class AppleCalendarExportResult
{
    public EAppleCalendarExportStatus Status { get; }

    public AppleCalendarId? CalendarIdOrNull { get; }

    public PlanName? CalendarNameOrNull { get; }

    public int CreatedEventCount { get; }

    public int DeletedEventCount { get; }

    public string? DiagnosticCodeOrNull { get; }

    private AppleCalendarExportResult(
        EAppleCalendarExportStatus status,
        AppleCalendarId? calendarIdOrNull,
        PlanName? calendarNameOrNull,
        int createdEventCount,
        int deletedEventCount,
        string? diagnosticCodeOrNull)
    {
        if (Enum.IsDefined(typeof(EAppleCalendarExportStatus), status) == false
            || status == EAppleCalendarExportStatus.None)
        {
            throw new ArgumentOutOfRangeException(nameof(status));
        }

        if (createdEventCount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(createdEventCount));
        }

        if (deletedEventCount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(deletedEventCount));
        }

        Status = status;
        CalendarIdOrNull = calendarIdOrNull;
        CalendarNameOrNull = calendarNameOrNull;
        CreatedEventCount = createdEventCount;
        DeletedEventCount = deletedEventCount;
        DiagnosticCodeOrNull = diagnosticCodeOrNull;
    }

    public static AppleCalendarExportResult Complete(AppleCalendarNativeExportResult nativeResult)
    {
        if (nativeResult == null)
        {
            throw new ArgumentNullException(nameof(nativeResult));
        }

        return new AppleCalendarExportResult(
            EAppleCalendarExportStatus.Success,
            nativeResult.CalendarId,
            nativeResult.CalendarName,
            nativeResult.CreatedEventCount,
            nativeResult.DeletedEventCount,
            null);
    }

    public static AppleCalendarExportResult Fail(
        EAppleCalendarExportStatus status,
        string? diagnosticCodeOrNull)
    {
        if (status == EAppleCalendarExportStatus.None || status == EAppleCalendarExportStatus.Success)
        {
            throw new ArgumentException(
                "Completed Apple Calendar exports require completion details.",
                nameof(status));
        }

        return new AppleCalendarExportResult(status, null, null, 0, 0, diagnosticCodeOrNull);
    }
}
