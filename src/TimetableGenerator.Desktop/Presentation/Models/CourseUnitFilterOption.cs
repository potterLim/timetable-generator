using System;

using TimetableGenerator.CatalogJson;
using TimetableGenerator.Desktop.Presentation.Catalog;

namespace TimetableGenerator.Desktop.Presentation.Models;

internal sealed class CourseUnitFilterOption
{
    private readonly OfferingUnitName? mOfferingUnitNameOrNull;

    public ECourseFilterScope Scope { get; }

    public string DisplayName { get; }

    private CourseUnitFilterOption(
        ECourseFilterScope scope,
        OfferingUnitName? offeringUnitNameOrNull,
        string displayName)
    {
        Scope = scope;
        mOfferingUnitNameOrNull = offeringUnitNameOrNull;
        DisplayName = displayName;
    }

    public static CourseUnitFilterOption CreateAll()
    {
        return new CourseUnitFilterOption(ECourseFilterScope.All, null, "개설 단위 전체");
    }

    public static CourseUnitFilterOption CreateSpecific(OfferingUnitName offeringUnitName)
    {
        if (offeringUnitName == null)
        {
            throw new ArgumentNullException(nameof(offeringUnitName));
        }

        return new CourseUnitFilterOption(
            ECourseFilterScope.Specific,
            offeringUnitName,
            offeringUnitName.Value);
    }

    public bool Matches(CatalogCourseProjection course)
    {
        if (course == null)
        {
            throw new ArgumentNullException(nameof(course));
        }

        if (Scope == ECourseFilterScope.All)
        {
            return true;
        }

        if (mOfferingUnitNameOrNull == null)
        {
            throw new InvalidOperationException("Specific course filters require an offering unit name.");
        }

        foreach (OfferingUnitName offeringUnitName in course.OfferingUnitNames)
        {
            if (offeringUnitName == mOfferingUnitNameOrNull)
            {
                return true;
            }
        }

        return false;
    }
}
