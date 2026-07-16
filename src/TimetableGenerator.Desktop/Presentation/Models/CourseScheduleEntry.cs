using System;
using TimetableGenerator.Domain.Scheduling;

namespace TimetableGenerator.Desktop.Presentation.Models;

internal sealed class CourseScheduleEntry : ScheduleEntry
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

    public AcademicPeriod Period { get; }

    public ECourseAccent Accent { get; }

    public CourseScheduleEntry(
        ScheduleCourseDetails courseDetails,
        EDay day,
        AcademicPeriod period,
        ECourseAccent accent)
        : base(day, AcademicPeriodTimeTable.GetTimeRange(period))
    {
        if (courseDetails == null)
        {
            throw new ArgumentNullException(nameof(courseDetails));
        }

        if (period.IsValid == false)
        {
            throw new ArgumentException("Academic periods must be valid.", nameof(period));
        }

        CourseDetails = courseDetails;
        Period = period;
        Accent = accent;
    }
}
