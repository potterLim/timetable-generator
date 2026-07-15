using System;

namespace TimetableGenerator.HandongCatalogGenerator.Domain;

internal readonly record struct CourseOfferingKey
{
    public CourseCode CourseCode { get; }

    public CourseSectionCode SectionCode { get; }

    public CourseOfferingKey(CourseCode courseCode, CourseSectionCode sectionCode)
    {
        if (courseCode == null)
        {
            throw new ArgumentNullException(nameof(courseCode));
        }

        if (sectionCode == null)
        {
            throw new ArgumentNullException(nameof(sectionCode));
        }

        CourseCode = courseCode;
        SectionCode = sectionCode;
    }
}
