using System;

namespace TimetableGenerator.CatalogJson;

public sealed class CatalogIndexCounts
{
    public CatalogCourseCount CourseCount { get; }

    public CatalogOfferingCount OfferingCount { get; }

    public CatalogIndexCounts(CatalogCourseCount courseCount, CatalogOfferingCount offeringCount)
    {
        if (courseCount.IsValid == false)
        {
            throw new ArgumentOutOfRangeException(
                nameof(courseCount),
                courseCount,
                "Catalog indexes require a positive course count.");
        }

        if (offeringCount.IsValid == false)
        {
            throw new ArgumentOutOfRangeException(
                nameof(offeringCount),
                offeringCount,
                "Catalog indexes require a positive offering count.");
        }

        CourseCount = courseCount;
        OfferingCount = offeringCount;
    }
}
