using System;

namespace TimetableGenerator.Infrastructure.Exporting;

public sealed class SchedulePngExportProgress
{
    public int ProcessedScheduleCount { get; }

    public int TotalScheduleCount { get; }

    public ScheduleExportNumber ScheduleNumber { get; }

    public ESchedulePngExportItemStatus ItemStatus { get; }

    internal SchedulePngExportProgress(
        int processedScheduleCount,
        int totalScheduleCount,
        ScheduleExportNumber scheduleNumber,
        ESchedulePngExportItemStatus itemStatus)
    {
        if (totalScheduleCount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(totalScheduleCount));
        }

        if (processedScheduleCount <= 0 || processedScheduleCount > totalScheduleCount)
        {
            throw new ArgumentOutOfRangeException(nameof(processedScheduleCount));
        }

        if (scheduleNumber.IsValid == false)
        {
            throw new ArgumentException("A valid schedule number is required.", nameof(scheduleNumber));
        }

        if (Enum.IsDefined(typeof(ESchedulePngExportItemStatus), itemStatus) == false)
        {
            throw new ArgumentOutOfRangeException(nameof(itemStatus));
        }

        ProcessedScheduleCount = processedScheduleCount;
        TotalScheduleCount = totalScheduleCount;
        ScheduleNumber = scheduleNumber;
        ItemStatus = itemStatus;
    }
}
