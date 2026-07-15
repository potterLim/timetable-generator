using System;
using System.Collections.Generic;
using TimetableGenerator.Core.Domain;

namespace TimetableGenerator.Core.Application.Scheduling;

public sealed class ScheduleGenerationResult
{
    private readonly IReadOnlyList<GeneratedSchedule> mSchedules;

    public IReadOnlyList<GeneratedSchedule> Schedules
    {
        get
        {
            return mSchedules;
        }
    }

    public EScheduleGenerationCompletion Completion { get; }

    public bool IsCompleted
    {
        get
        {
            return Completion == EScheduleGenerationCompletion.Completed;
        }
    }

    public bool HasReachedMaximumScheduleCount
    {
        get
        {
            return Completion == EScheduleGenerationCompletion.MaximumScheduleCountReached;
        }
    }

    public bool IsCanceled
    {
        get
        {
            return Completion == EScheduleGenerationCompletion.Canceled;
        }
    }

    internal ScheduleGenerationResult(
        IEnumerable<GeneratedSchedule> schedules,
        EScheduleGenerationCompletion completion)
    {
        if (schedules == null)
        {
            throw new ArgumentNullException(nameof(schedules));
        }

        if (Enum.IsDefined(typeof(EScheduleGenerationCompletion), completion) == false)
        {
            throw new ArgumentOutOfRangeException(nameof(completion));
        }

        List<GeneratedSchedule> copiedSchedules = new List<GeneratedSchedule>();
        foreach (GeneratedSchedule schedule in schedules)
        {
            if (schedule == null)
            {
                throw new ArgumentException("Generation results cannot contain null schedules.", nameof(schedules));
            }

            copiedSchedules.Add(schedule);
        }

        mSchedules = copiedSchedules.AsReadOnly();
        Completion = completion;
    }
}
