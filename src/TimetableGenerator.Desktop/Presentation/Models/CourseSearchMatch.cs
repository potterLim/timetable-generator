using System;

namespace TimetableGenerator.Desktop.Presentation.Models;

internal sealed class CourseSearchMatch
{
    public CourseSearchItem Course { get; }

    public ECourseSearchMatchKind Kind { get; }

    public CourseSearchMatch(CourseSearchItem course, ECourseSearchMatchKind kind)
    {
        if (course == null)
        {
            throw new ArgumentNullException(nameof(course));
        }

        if (Enum.IsDefined(typeof(ECourseSearchMatchKind), kind) == false)
        {
            throw new ArgumentOutOfRangeException(nameof(kind));
        }

        Course = course;
        Kind = kind;
    }
}
