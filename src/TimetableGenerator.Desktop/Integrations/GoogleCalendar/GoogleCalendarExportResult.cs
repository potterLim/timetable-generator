using System;
using TimetableGenerator.Domain.Planning;

namespace TimetableGenerator.Desktop.Integrations.GoogleCalendar;

internal sealed class GoogleCalendarExportResult
{
    public EGoogleCalendarExportStatus Status { get; }

    public GoogleCalendarId? CalendarIdOrNull { get; }

    public PlanName? CalendarNameOrNull { get; }

    public int CreatedEventCount { get; }

    public int UpdatedEventCount { get; }

    public int DeletedEventCount { get; }

    public string? DiagnosticCodeOrNull { get; }

    private GoogleCalendarExportResult(
        EGoogleCalendarExportStatus status,
        GoogleCalendarId? calendarIdOrNull,
        PlanName? calendarNameOrNull,
        int createdEventCount,
        int updatedEventCount,
        int deletedEventCount,
        string? diagnosticCodeOrNull)
    {
        if (Enum.IsDefined(typeof(EGoogleCalendarExportStatus), status) == false || status == EGoogleCalendarExportStatus.None)
        {
            throw new ArgumentOutOfRangeException(nameof(status));
        }

        ensureNonNegative(createdEventCount, nameof(createdEventCount));
        ensureNonNegative(updatedEventCount, nameof(updatedEventCount));
        ensureNonNegative(deletedEventCount, nameof(deletedEventCount));

        Status = status;
        CalendarIdOrNull = calendarIdOrNull;
        CalendarNameOrNull = calendarNameOrNull;
        CreatedEventCount = createdEventCount;
        UpdatedEventCount = updatedEventCount;
        DeletedEventCount = deletedEventCount;
        DiagnosticCodeOrNull = diagnosticCodeOrNull;
    }

    public static GoogleCalendarExportResult Complete(GoogleCalendarId calendarId, PlanName calendarName, GoogleCalendarReconciliationResult reconciliationResult)
    {
        if (calendarId == null)
        {
            throw new ArgumentNullException(nameof(calendarId));
        }

        if (calendarName == null)
        {
            throw new ArgumentNullException(nameof(calendarName));
        }

        return new GoogleCalendarExportResult(
            EGoogleCalendarExportStatus.Success,
            calendarId,
            calendarName,
            reconciliationResult.CreatedEventCount,
            reconciliationResult.UpdatedEventCount,
            reconciliationResult.DeletedEventCount,
            null);
    }

    public static GoogleCalendarExportResult Fail(EGoogleCalendarExportStatus status, string? diagnosticCodeOrNull)
    {
        if (status == EGoogleCalendarExportStatus.None || status == EGoogleCalendarExportStatus.Success)
        {
            throw new ArgumentException("Completed Google Calendar exports require completion details.", nameof(status));
        }

        return new GoogleCalendarExportResult(status, null, null, 0, 0, 0, diagnosticCodeOrNull);
    }

    private static void ensureNonNegative(int value, string parameterName)
    {
        if (value < 0)
        {
            throw new ArgumentOutOfRangeException(parameterName);
        }
    }
}
