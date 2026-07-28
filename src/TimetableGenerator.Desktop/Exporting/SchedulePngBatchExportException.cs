using System;
using System.Collections.Generic;

namespace TimetableGenerator.Desktop.Exporting;

internal sealed class SchedulePngBatchExportException : Exception
{
    public int SuccessfulCount { get; }

    public int FailedCount { get; }

    public SchedulePngBatchExportException(int successfulCount, int failedCount, IReadOnlyList<Exception> failures)
        : base("One or more timetable PNG candidates could not be staged.", new AggregateException(failures))
    {
        if (successfulCount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(successfulCount));
        }

        if (failedCount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(failedCount));
        }

        ArgumentNullException.ThrowIfNull(failures);
        if (failures.Count == 0)
        {
            throw new ArgumentException("At least one PNG export failure is required.", nameof(failures));
        }

        SuccessfulCount = successfulCount;
        FailedCount = failedCount;
    }
}
