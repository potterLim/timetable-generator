using System;

namespace TimetableGenerator.Desktop.Exporting.AppleCalendar;

internal sealed class AppleCalendarNativeBridgeException : Exception
{
    public EAppleCalendarNativeFailureKind FailureKind { get; }

    public string DiagnosticCode { get; }

    public AppleCalendarNativeBridgeException(
        EAppleCalendarNativeFailureKind failureKind,
        string diagnosticCode)
        : this(failureKind, diagnosticCode, null)
    {
    }

    public AppleCalendarNativeBridgeException(
        EAppleCalendarNativeFailureKind failureKind,
        string diagnosticCode,
        Exception? innerExceptionOrNull)
        : base("The native Apple Calendar operation failed.", innerExceptionOrNull)
    {
        if (Enum.IsDefined(typeof(EAppleCalendarNativeFailureKind), failureKind) == false
            || failureKind == EAppleCalendarNativeFailureKind.None)
        {
            throw new ArgumentOutOfRangeException(nameof(failureKind));
        }

        if (string.IsNullOrWhiteSpace(diagnosticCode))
        {
            throw new ArgumentException(
                "Apple Calendar failures require a diagnostic code.",
                nameof(diagnosticCode));
        }

        FailureKind = failureKind;
        DiagnosticCode = diagnosticCode.Trim();
    }
}
