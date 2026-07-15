using System;
using System.Collections.Generic;
using TimetableGenerator.Presentation.Schedules;

namespace TimetableGenerator.Infrastructure.Exporting;

public sealed class SchedulePngBatchExportRequest
{
    private readonly IReadOnlyList<ScheduleGridViewModel> mScheduleGrids;

    public IReadOnlyList<ScheduleGridViewModel> ScheduleGrids
    {
        get
        {
            return mScheduleGrids;
        }
    }

    public ScheduleExportDirectoryPath DestinationDirectory { get; }

    public ScheduleExportBaseName BaseName { get; }

    public SchedulePngBatchExportRequest(
        IEnumerable<ScheduleGridViewModel> scheduleGrids,
        ScheduleExportDirectoryPath destinationDirectory,
        ScheduleExportBaseName baseName)
    {
        if (scheduleGrids == null)
        {
            throw new ArgumentNullException(nameof(scheduleGrids));
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

        List<ScheduleGridViewModel> copiedScheduleGrids = new List<ScheduleGridViewModel>();
        foreach (ScheduleGridViewModel scheduleGrid in scheduleGrids)
        {
            if (scheduleGrid == null)
            {
                throw new ArgumentException(
                    "Batch export requests cannot contain null schedules.",
                    nameof(scheduleGrids));
            }

            copiedScheduleGrids.Add(scheduleGrid);
        }

        if (copiedScheduleGrids.Count == 0)
        {
            throw new ArgumentException(
                "Batch export requests require at least one schedule.",
                nameof(scheduleGrids));
        }

        mScheduleGrids = copiedScheduleGrids.AsReadOnly();
        DestinationDirectory = destinationDirectory;
        BaseName = baseName;
    }
}
