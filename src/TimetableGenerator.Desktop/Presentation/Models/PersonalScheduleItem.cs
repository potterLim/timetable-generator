using System;
using System.Collections.Generic;
using TimetableGenerator.Domain.Planning;
using TimetableGenerator.Domain.Scheduling;

namespace TimetableGenerator.Desktop.Presentation.Models;

internal sealed class PersonalScheduleItem
{
    public PersonalSchedule Schedule { get; }

    public PersonalScheduleId Id
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

    public string TimeSummary
    {
        get
        {
            List<EDay> days = new List<EDay>();
            foreach (WeeklyTimeRange timeRange in Schedule.TimeRanges)
            {
                days.Add(timeRange.Day);
            }

            DailyTimeRange firstTimeRange = Schedule.TimeRanges[0].TimeRange;
            return ScheduleBoardDayRange.CreateShortDayTimeDisplayText(
                days,
                firstTimeRange);
        }
    }

    public string DetailsSummary
    {
        get
        {
            List<string> details = new List<string>();
            if (Schedule.Details.SectionOrNull != null)
            {
                details.Add("분반: " + Schedule.Details.SectionOrNull.Value);
            }

            if (Schedule.Details.InstructorOrNull != null)
            {
                details.Add("담당: " + Schedule.Details.InstructorOrNull.Value);
            }

            if (Schedule.Details.LocationOrNull != null)
            {
                details.Add("장소: " + Schedule.Details.LocationOrNull.Value);
            }

            return string.Join(" · ", details);
        }
    }

    public bool HasDetails
    {
        get
        {
            return DetailsSummary.Length > 0;
        }
    }

    public string EditButtonAccessibleName
    {
        get
        {
            return Title + " 개인 일정 수정";
        }
    }

    public string RemoveButtonAccessibleName
    {
        get
        {
            return Title + " 개인 일정 삭제";
        }
    }

    public PersonalScheduleItem(PersonalSchedule schedule)
    {
        if (schedule == null)
        {
            throw new ArgumentNullException(nameof(schedule));
        }

        Schedule = schedule;
    }
}
