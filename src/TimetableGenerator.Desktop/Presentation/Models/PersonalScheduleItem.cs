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
            List<string> dayNames = new List<string>();
            foreach (WeeklyTimeRange timeRange in Schedule.TimeRanges)
            {
                dayNames.Add(getDayName(timeRange.Day));
            }

            DailyTimeRange firstTimeRange = Schedule.TimeRanges[0].TimeRange;
            return string.Join("·", dayNames)
                + " · "
                + firstTimeRange.Start
                + "–"
                + firstTimeRange.End;
        }
    }

    public string DetailsSummary
    {
        get
        {
            List<string> details = new List<string>();
            if (Schedule.Details.SectionOrNull != null)
            {
                details.Add("분반 " + Schedule.Details.SectionOrNull.Value);
            }

            if (Schedule.Details.InstructorOrNull != null)
            {
                details.Add(Schedule.Details.InstructorOrNull.Value);
            }

            if (Schedule.Details.LocationOrNull != null)
            {
                details.Add(Schedule.Details.LocationOrNull.Value);
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

    private static string getDayName(EDay day)
    {
        switch (day)
        {
            case EDay.Monday:
                return "월";
            case EDay.Tuesday:
                return "화";
            case EDay.Wednesday:
                return "수";
            case EDay.Thursday:
                return "목";
            case EDay.Friday:
                return "금";
            case EDay.Saturday:
                return "토";
            case EDay.Sunday:
                return "일";
            default:
                throw new ArgumentOutOfRangeException(
                    nameof(day),
                    day,
                    "Unknown personal schedule day.");
        }
    }
}
