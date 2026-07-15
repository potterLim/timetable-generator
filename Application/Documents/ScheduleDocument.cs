using System;
using System.Collections.Generic;
using TimetableGenerator.Infrastructure.Csv;

namespace TimetableGenerator.Application.Documents;

public sealed class ScheduleDocument
{
    public CsvInputFilePath SourceFilePath { get; }

    private readonly IReadOnlyList<ScheduleDocumentSchedule> mSchedules;

    public IReadOnlyList<ScheduleDocumentSchedule> Schedules
    {
        get
        {
            return mSchedules;
        }
    }

    public int ScheduleCount
    {
        get
        {
            return mSchedules.Count;
        }
    }

    internal ScheduleDocument(
        CsvInputFilePath sourceFilePath,
        IEnumerable<ScheduleDocumentSchedule> schedules)
    {
        if (sourceFilePath.IsValid == false)
        {
            throw new ArgumentException(
                "Schedule documents require a valid CSV source file path.",
                nameof(sourceFilePath));
        }

        if (schedules == null)
        {
            throw new ArgumentNullException(nameof(schedules));
        }

        List<ScheduleDocumentSchedule> copiedSchedules =
            new List<ScheduleDocumentSchedule>();
        foreach (ScheduleDocumentSchedule schedule in schedules)
        {
            if (schedule == null)
            {
                throw new ArgumentException(
                    "Schedule documents cannot contain null schedules.",
                    nameof(schedules));
            }

            copiedSchedules.Add(schedule);
        }

        if (copiedSchedules.Count == 0)
        {
            throw new ArgumentException(
                "Schedule documents require at least one generated schedule.",
                nameof(schedules));
        }

        SourceFilePath = sourceFilePath;
        mSchedules = copiedSchedules.AsReadOnly();
    }
}
