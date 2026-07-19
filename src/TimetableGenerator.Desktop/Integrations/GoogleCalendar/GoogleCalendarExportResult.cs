using System;

namespace TimetableGenerator.Desktop.Integrations.GoogleCalendar;

internal sealed class GoogleCalendarExportResult
{
    public EGoogleCalendarExportStatus Status { get; }

    public GoogleCalendarId? CalendarIdOrNull { get; }

    public int CreatedEventCount { get; }

    public int UpdatedEventCount { get; }

    public int DeletedEventCount { get; }

    public string? DiagnosticCodeOrNull { get; }

    private GoogleCalendarExportResult(
        EGoogleCalendarExportStatus status,
        GoogleCalendarId? calendarIdOrNull,
        int createdEventCount,
        int updatedEventCount,
        int deletedEventCount,
        string? diagnosticCodeOrNull)
    {
        if (Enum.IsDefined(typeof(EGoogleCalendarExportStatus), status) == false
            || status == EGoogleCalendarExportStatus.None)
        {
            throw new ArgumentOutOfRangeException(nameof(status));
        }

        ensureNonNegative(createdEventCount, nameof(createdEventCount));
        ensureNonNegative(updatedEventCount, nameof(updatedEventCount));
        ensureNonNegative(deletedEventCount, nameof(deletedEventCount));

        Status = status;
        CalendarIdOrNull = calendarIdOrNull;
        CreatedEventCount = createdEventCount;
        UpdatedEventCount = updatedEventCount;
        DeletedEventCount = deletedEventCount;
        DiagnosticCodeOrNull = diagnosticCodeOrNull;
    }

    public static GoogleCalendarExportResult Complete(
        GoogleCalendarId calendarId,
        GoogleCalendarReconciliationResult reconciliationResult)
    {
        if (calendarId == null)
        {
            throw new ArgumentNullException(nameof(calendarId));
        }

        return new GoogleCalendarExportResult(
            EGoogleCalendarExportStatus.Success,
            calendarId,
            reconciliationResult.CreatedEventCount,
            reconciliationResult.UpdatedEventCount,
            reconciliationResult.DeletedEventCount,
            null);
    }

    public static GoogleCalendarExportResult Fail(
        EGoogleCalendarExportStatus status,
        string? diagnosticCodeOrNull)
    {
        if (status == EGoogleCalendarExportStatus.None
            || status == EGoogleCalendarExportStatus.Success)
        {
            throw new ArgumentException(
                "Completed Google Calendar exports require completion details.",
                nameof(status));
        }

        return new GoogleCalendarExportResult(status, null, 0, 0, 0, diagnosticCodeOrNull);
    }

    private static void ensureNonNegative(int value, string parameterName)
    {
        if (value < 0)
        {
            throw new ArgumentOutOfRangeException(parameterName);
        }
    }
}
