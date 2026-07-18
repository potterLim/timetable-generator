using System;

using TimetableGenerator.Domain.Catalogs;
using TimetableGenerator.Domain.Scheduling;

namespace TimetableGenerator.Desktop.Presentation.Models;

internal sealed class CourseScheduleEntry : ScheduleEntry
{
    public CourseId CourseId { get; }

    public OfferingId OfferingId { get; }

    public ScheduleCourseDetails CourseDetails { get; }

    public CourseSectionCode SectionCode { get; }

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

    public string SectionDisplayText
    {
        get
        {
            return SectionCode.Value + "분반";
        }
    }

    public string InstructorDisplayText
    {
        get
        {
            return CourseDetails.InstructorSummary.Value;
        }
    }

    public bool HasConfirmedInstructor
    {
        get
        {
            return CourseDetails.InstructorSummary.IsConfirmed;
        }
    }

    public string LocationDisplayText
    {
        get
        {
            return CourseDetails.LocationSummary.Value;
        }
    }

    public bool HasAssignedLocation
    {
        get
        {
            return CourseDetails.LocationSummary.IsAssigned;
        }
    }

    public AcademicPeriod Period { get; }

    public ECourseAccent Accent { get; }

    public CourseScheduleEntry(
        CourseId courseId,
        OfferingId offeringId,
        ScheduleCourseDetails courseDetails,
        CourseSectionCode sectionCode,
        EDay day,
        AcademicPeriod period,
        ECourseAccent accent)
        : base(day, AcademicPeriodTimeTable.GetTimeRange(period))
    {
        if (courseId == null)
        {
            throw new ArgumentNullException(nameof(courseId));
        }

        if (offeringId == null)
        {
            throw new ArgumentNullException(nameof(offeringId));
        }

        if (courseDetails == null)
        {
            throw new ArgumentNullException(nameof(courseDetails));
        }

        if (sectionCode == null)
        {
            throw new ArgumentNullException(nameof(sectionCode));
        }

        if (period.IsValid == false)
        {
            throw new ArgumentException("Academic periods must be valid.", nameof(period));
        }

        CourseId = courseId;
        OfferingId = offeringId;
        CourseDetails = courseDetails;
        SectionCode = sectionCode;
        Period = period;
        Accent = accent;
    }
}
