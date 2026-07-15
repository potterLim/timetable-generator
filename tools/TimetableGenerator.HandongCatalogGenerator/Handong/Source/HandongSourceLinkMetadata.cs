using System;
using TimetableGenerator.HandongCatalogGenerator.Domain;

namespace TimetableGenerator.HandongCatalogGenerator.Handong.Source;

internal sealed record HandongSourceLinkMetadata
{
    public AcademicTerm AcademicTerm { get; }
    public CourseCode CourseCode { get; }
    public CourseSectionCode CourseSectionCode { get; }

    public HandongSourceLinkMetadata(
        AcademicTerm academicTerm,
        CourseCode courseCode,
        CourseSectionCode courseSectionCode)
    {
        ArgumentNullException.ThrowIfNull(courseCode);
        ArgumentNullException.ThrowIfNull(courseSectionCode);

        AcademicTerm = academicTerm;
        CourseCode = courseCode;
        CourseSectionCode = courseSectionCode;
    }
}
