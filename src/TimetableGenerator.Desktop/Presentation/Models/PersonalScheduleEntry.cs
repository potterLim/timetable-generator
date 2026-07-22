using System;
using TimetableGenerator.Domain.Planning;
using TimetableGenerator.Domain.Scheduling;

namespace TimetableGenerator.Desktop.Presentation.Models;

internal sealed class PersonalScheduleEntry : ScheduleEntry
{
    public PersonalSchedule Schedule { get; }

    public PersonalScheduleId ScheduleId
    {
        get
        {
            return Schedule.Id;
        }
    }

    public string Title
    {
        get
        {
            return Schedule.Title.Value;
        }
    }

    public bool HasSection
    {
        get
        {
            return Schedule.Details.SectionOrNull != null;
        }
    }

    public bool HasInstructor
    {
        get
        {
            return Schedule.Details.InstructorOrNull != null;
        }
    }

    public bool HasLocation
    {
        get
        {
            return Schedule.Details.LocationOrNull != null;
        }
    }

    public string SectionDisplayText
    {
        get
        {
            if (Schedule.Details.SectionOrNull == null)
            {
                return string.Empty;
            }

            return Schedule.Details.SectionOrNull.Value;
        }
    }

    public string InstructorDisplayText
    {
        get
        {
            if (Schedule.Details.InstructorOrNull == null)
            {
                return string.Empty;
            }

            return Schedule.Details.InstructorOrNull.Value;
        }
    }

    public string LocationDisplayText
    {
        get
        {
            if (Schedule.Details.LocationOrNull == null)
            {
                return string.Empty;
            }

            return Schedule.Details.LocationOrNull.Value;
        }
    }

    public PersonalScheduleEntry(PersonalSchedule schedule, WeeklyTimeRange timeRange)
        : base(timeRange.Day, timeRange.TimeRange)
    {
        if (schedule == null)
        {
            throw new ArgumentNullException(nameof(schedule));
        }

        bool hasTimeRange = false;
        foreach (WeeklyTimeRange scheduleTimeRange in schedule.TimeRanges)
        {
            if (scheduleTimeRange == timeRange)
            {
                hasTimeRange = true;
                break;
            }
        }

        if (hasTimeRange == false)
        {
            throw new ArgumentException(
                "Personal schedule entries must reference one of their schedule ranges.",
                nameof(timeRange));
        }

        Schedule = schedule;
    }
}
