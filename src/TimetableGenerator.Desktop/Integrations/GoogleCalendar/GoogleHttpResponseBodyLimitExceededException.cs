using System;

namespace TimetableGenerator.Desktop.Integrations.GoogleCalendar;

internal sealed class GoogleHttpResponseBodyLimitExceededException : Exception
{
    public GoogleHttpResponseBodyLimitExceededException(long maximumByteCount)
        : base(
            "The Google response body exceeded the configured limit of "
                + maximumByteCount
                + " bytes.")
    {
        if (maximumByteCount <= 0L)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumByteCount));
        }
    }
}
