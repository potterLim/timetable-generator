using System;

namespace TimetableGenerator.Infrastructure.Exporting;

public sealed class SchedulePngExportProgress
{
    public SchedulePngExportProgressPosition Position { get; }

    public int ProcessedScheduleCount
    {
        get
        {
            return Position.ProcessedScheduleCount;
        }
    }

    public int TotalScheduleCount
    {
        get
        {
            return Position.TotalScheduleCount;
        }
    }

    public ScheduleExportNumber ScheduleNumber { get; }

    public ESchedulePngExportItemStatus ItemStatus { get; }

    internal SchedulePngExportProgress(
        SchedulePngExportProgressPosition position,
        ScheduleExportNumber scheduleNumber,
        ESchedulePngExportItemStatus itemStatus)
    {
        if (position.IsValid == false)
        {
            throw new ArgumentException(
                "A valid PNG export progress position is required.",
                nameof(position));
        }

        if (scheduleNumber.IsValid == false)
        {
            throw new ArgumentException("A valid schedule number is required.", nameof(scheduleNumber));
        }

        if (Enum.IsDefined(typeof(ESchedulePngExportItemStatus), itemStatus) == false)
        {
            throw new ArgumentOutOfRangeException(nameof(itemStatus));
        }

        Position = position;
        ScheduleNumber = scheduleNumber;
        ItemStatus = itemStatus;
    }
}
