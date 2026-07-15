using System;

namespace TimetableGenerator.Infrastructure.Exporting;

public readonly record struct SchedulePngExportProgressPosition
{
    public int ProcessedScheduleCount { get; }

    public int TotalScheduleCount { get; }

    public bool IsValid
    {
        get
        {
            return TotalScheduleCount > 0 &&
                ProcessedScheduleCount > 0 &&
                ProcessedScheduleCount <= TotalScheduleCount;
        }
    }

    internal SchedulePngExportProgressPosition(
        int processedScheduleCount,
        int totalScheduleCount)
    {
        if (totalScheduleCount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(totalScheduleCount));
        }

        if (processedScheduleCount <= 0 ||
            processedScheduleCount > totalScheduleCount)
        {
            throw new ArgumentOutOfRangeException(nameof(processedScheduleCount));
        }

        ProcessedScheduleCount = processedScheduleCount;
        TotalScheduleCount = totalScheduleCount;
    }
}
