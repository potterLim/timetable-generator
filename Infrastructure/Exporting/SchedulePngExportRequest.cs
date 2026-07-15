using System;
using TimetableGenerator.Presentation.Schedules;

namespace TimetableGenerator.Infrastructure.Exporting;

public sealed class SchedulePngExportRequest
{
    public ScheduleGridViewModel ScheduleGrid { get; }

    public ScheduleExportNumber ScheduleNumber { get; }

    public ScheduleExportDirectoryPath DestinationDirectory { get; }

    public ScheduleExportBaseName BaseName { get; }

    public SchedulePngExportRequest(
        ScheduleGridViewModel scheduleGrid,
        ScheduleExportNumber scheduleNumber,
        ScheduleExportDirectoryPath destinationDirectory,
        ScheduleExportBaseName baseName)
    {
        if (scheduleGrid == null)
        {
            throw new ArgumentNullException(nameof(scheduleGrid));
        }

        if (scheduleNumber.IsValid == false)
        {
            throw new ArgumentException("A valid schedule number is required.", nameof(scheduleNumber));
        }

        if (destinationDirectory.IsValid == false)
        {
            throw new ArgumentException(
                "A valid export destination directory is required.",
                nameof(destinationDirectory));
        }

        if (baseName == null)
        {
            throw new ArgumentNullException(nameof(baseName));
        }

        ScheduleGrid = scheduleGrid;
        ScheduleNumber = scheduleNumber;
        DestinationDirectory = destinationDirectory;
        BaseName = baseName;
    }
}
