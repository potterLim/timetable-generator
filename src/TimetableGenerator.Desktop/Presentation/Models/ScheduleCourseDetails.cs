using System;

using TimetableGenerator.Domain.Catalogs;

namespace TimetableGenerator.Desktop.Presentation.Models;

internal sealed class ScheduleCourseDetails
{
    public CourseCode Code { get; }

    public KoreanCourseName Name { get; }

    public CourseCredits Credits { get; }

    public ScheduleInstructorSummary InstructorSummary { get; }

    public ScheduleLocationSummary LocationSummary { get; }

    public string CreditsDisplayText
    {
        get
        {
            return Credits + "학점";
        }
    }

    public ScheduleCourseDetails(
        CourseCode code,
        KoreanCourseName name,
        CourseCredits credits,
        ScheduleInstructorSummary instructorSummary,
        ScheduleLocationSummary locationSummary)
    {
        if (code == null)
        {
            throw new ArgumentNullException(nameof(code));
        }

        if (name == null)
        {
            throw new ArgumentNullException(nameof(name));
        }

        if (credits.IsValid == false)
        {
            throw new ArgumentException("Schedule course details require valid course credits.", nameof(credits));
        }

        if (instructorSummary == null)
        {
            throw new ArgumentNullException(nameof(instructorSummary));
        }

        if (locationSummary == null)
        {
            throw new ArgumentNullException(nameof(locationSummary));
        }

        Code = code;
        Name = name;
        Credits = credits;
        InstructorSummary = instructorSummary;
        LocationSummary = locationSummary;
    }
}
