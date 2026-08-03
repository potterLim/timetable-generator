using System;

using TimetableGenerator.Desktop.Presentation.Catalog;

namespace TimetableGenerator.Desktop.Presentation.Models;

internal sealed partial class CourseSearchItem
{
    public CourseSearchMatch? FindSearchMatchOrNull(CourseSearchQuery query)
    {
        if (query == null)
        {
            throw new ArgumentNullException(nameof(query));
        }

        if (query.IsEmpty)
        {
            return null;
        }

        if (query.IsExactMatch(Code))
        {
            return new CourseSearchMatch(this, ECourseSearchMatchKind.ExactCourseCode);
        }

        if (query.IsPrefixMatch(Code))
        {
            return new CourseSearchMatch(this, ECourseSearchMatchKind.CourseCodePrefix);
        }

        if (query.IsExactMatch(Name) || query.IsExactMatch(EnglishName))
        {
            return new CourseSearchMatch(this, ECourseSearchMatchKind.ExactCourseTitle);
        }

        if (query.IsPrefixMatch(Name) || query.IsPrefixMatch(EnglishName))
        {
            return new CourseSearchMatch(this, ECourseSearchMatchKind.CourseTitlePrefix);
        }

        if (query.IsContainedIn(Name) || query.IsContainedIn(EnglishName))
        {
            return new CourseSearchMatch(this, ECourseSearchMatchKind.CourseTitleContains);
        }

        foreach (CatalogOfferingProjection offering in Projection.Offerings)
        {
            if (query.IsContainedIn(offering.InstructorSummary))
            {
                return new CourseSearchMatch(this, ECourseSearchMatchKind.InstructorContains);
            }
        }

        return null;
    }
}
