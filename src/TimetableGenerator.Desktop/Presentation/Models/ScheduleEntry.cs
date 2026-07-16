using System;

using TimetableGenerator.Domain.Scheduling;

namespace TimetableGenerator.Desktop.Presentation.Models;

internal sealed class ScheduleEntry
{
    public ScheduleCourseDetails CourseDetails { get; }

    public string Code
    {
        get
        {
            return CourseDetails.Code.Value;
        }
    }

    public string Name
    {
        get
        {
            return CourseDetails.Name.Value;
        }
    }

    public string InstructorDisplayText
    {
        get
        {
            return CourseDetails.InstructorSummary.Value;
        }
    }

    public string LocationDisplayText
    {
        get
        {
            return CourseDetails.LocationSummary.Value;
        }
    }

    public EDay Day { get; }

    public AcademicPeriod Period { get; }

    public ECourseAccent Accent { get; }

    public ScheduleEntry(
        ScheduleCourseDetails courseDetails,
        EDay day,
        AcademicPeriod period,
        ECourseAccent accent)
    {
        if (courseDetails == null)
        {
            throw new ArgumentNullException(nameof(courseDetails));
        }

        ensureSupportedDay(day);
        if (period.IsValid == false)
        {
            throw new ArgumentException("Academic periods must be valid.", nameof(period));
        }

        CourseDetails = courseDetails;
        Day = day;
        Period = period;
        Accent = accent;
    }

    private static void ensureSupportedDay(EDay day)
    {
        switch (day)
        {
            case EDay.Monday:
            case EDay.Tuesday:
            case EDay.Wednesday:
            case EDay.Thursday:
            case EDay.Friday:
                return;
            default:
                throw new ArgumentOutOfRangeException(
                    nameof(day),
                    day,
                    "The planning workspace supports weekdays only.");
        }
    }
}
