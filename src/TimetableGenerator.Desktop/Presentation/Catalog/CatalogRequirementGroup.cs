using System;
using System.Collections.Generic;

using TimetableGenerator.CatalogJson;
using TimetableGenerator.Domain.Catalogs;

namespace TimetableGenerator.Desktop.Presentation.Catalog;

internal sealed class CatalogRequirementGroup
{
    private readonly IReadOnlyList<CatalogCourseProjection> mCourses;

    public ERequirementType RequirementType { get; }

    public IReadOnlyList<CatalogCourseProjection> Courses
    {
        get
        {
            return mCourses;
        }
    }

    public CatalogRequirementGroup(
        ERequirementType requirementType,
        IEnumerable<CatalogCourseProjection> courses)
    {
        if (Enum.IsDefined(typeof(ERequirementType), requirementType) == false)
        {
            throw new ArgumentOutOfRangeException(nameof(requirementType));
        }

        if (courses == null)
        {
            throw new ArgumentNullException(nameof(courses));
        }

        List<CatalogCourseProjection> copiedCourses =
            new List<CatalogCourseProjection>();
        HashSet<CourseId> uniqueCourseIds = new HashSet<CourseId>();
        foreach (CatalogCourseProjection course in courses)
        {
            if (course == null)
            {
                throw new ArgumentException(
                    "Requirement groups cannot contain null course projections.",
                    nameof(courses));
            }

            if (hasRequirementType(course, requirementType) == false)
            {
                throw new ArgumentException(
                    "Requirement groups can contain only matching courses.",
                    nameof(courses));
            }

            if (uniqueCourseIds.Add(course.Course.Id) == false)
            {
                throw new ArgumentException(
                    "Requirement groups cannot contain duplicate courses.",
                    nameof(courses));
            }

            copiedCourses.Add(course);
        }

        if (copiedCourses.Count == 0)
        {
            throw new ArgumentException(
                "Requirement groups require at least one course.",
                nameof(courses));
        }

        RequirementType = requirementType;
        mCourses = copiedCourses.AsReadOnly();
    }

    private static bool hasRequirementType(
        CatalogCourseProjection course,
        ERequirementType requirementType)
    {
        foreach (ERequirementType courseRequirementType in course.RequirementTypes)
        {
            if (courseRequirementType == requirementType)
            {
                return true;
            }
        }

        return false;
    }
}
