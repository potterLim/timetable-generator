using System;

namespace TimetableGenerator.CatalogJson;

public sealed class CatalogIndexCounts
{
    public int CourseCount { get; }

    public int OfferingCount { get; }

    public CatalogIndexCounts(int courseCount, int offeringCount)
    {
        if (courseCount <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(courseCount),
                courseCount,
                "Catalog indexes require a positive course count.");
        }

        if (offeringCount <= 0)
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
