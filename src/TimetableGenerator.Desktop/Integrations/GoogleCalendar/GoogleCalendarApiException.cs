using System;
using System.Net;

namespace TimetableGenerator.Desktop.Integrations.GoogleCalendar;

internal sealed class GoogleCalendarApiException : Exception
{
    public HttpStatusCode StatusCode { get; }

    public string DiagnosticCode { get; }

    public EGoogleCalendarApiFailureKind FailureKind { get; }

    public GoogleCalendarApiException(HttpStatusCode statusCode, string diagnosticCode)
        : this(statusCode, diagnosticCode, EGoogleCalendarApiFailureKind.Permanent)
    {
    }

    public GoogleCalendarApiException(HttpStatusCode statusCode, string diagnosticCode, EGoogleCalendarApiFailureKind failureKind)
        : base("The Google Calendar API request failed with status " + (int)statusCode + ".")
    {
        if (diagnosticCode == null)
        {
            throw new ArgumentNullException(nameof(diagnosticCode));
        }

        if (Enum.IsDefined(typeof(EGoogleCalendarApiFailureKind), failureKind) == false || failureKind == EGoogleCalendarApiFailureKind.None)
        {
            throw new ArgumentOutOfRangeException(nameof(failureKind));
        }

        StatusCode = statusCode;
        DiagnosticCode = diagnosticCode;
        FailureKind = failureKind;
    }
}
