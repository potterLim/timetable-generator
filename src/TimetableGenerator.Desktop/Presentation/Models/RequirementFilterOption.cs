using System;

using TimetableGenerator.CatalogJson;
using TimetableGenerator.Desktop.Presentation.Catalog;

namespace TimetableGenerator.Desktop.Presentation.Models;

internal sealed class RequirementFilterOption
{
    private readonly ERequirementType? mRequirementTypeOrNull;

    public ECourseFilterScope Scope { get; }

    public string DisplayName { get; }

    private RequirementFilterOption(ECourseFilterScope scope, ERequirementType? requirementTypeOrNull, string displayName)
    {
        Scope = scope;
        mRequirementTypeOrNull = requirementTypeOrNull;
        DisplayName = displayName;
    }

    public static RequirementFilterOption CreateAll()
    {
        return new RequirementFilterOption(ECourseFilterScope.All, null, "이수구분 전체");
    }

    public static RequirementFilterOption CreateSpecific(ERequirementType requirementType)
    {
        if (Enum.IsDefined(typeof(ERequirementType), requirementType) == false)
        {
            throw new ArgumentOutOfRangeException(nameof(requirementType));
        }

        return new RequirementFilterOption(ECourseFilterScope.Specific, requirementType, findDisplayName(requirementType));
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

        if (mRequirementTypeOrNull.HasValue == false)
        {
            throw new InvalidOperationException("Specific course filters require an exact requirement type.");
        }

        foreach (ERequirementType requirementType in course.RequirementTypes)
        {
            if (requirementType == mRequirementTypeOrNull.Value)
            {
                return true;
            }
        }

        return false;
    }

    private static string findDisplayName(ERequirementType requirementType)
    {
        switch (requirementType)
        {
            case ERequirementType.GeneralRequired:
                return "교양필수";
            case ERequirementType.GeneralElectiveRequired:
                return "교양선택필수";
            case ERequirementType.GeneralElective:
                return "교양선택";
            case ERequirementType.MajorRequired:
                return "전공필수";
            case ERequirementType.MajorElective:
                return "전공선택";
            case ERequirementType.FreeElective:
                return "자유선택";
            default:
                throw new ArgumentOutOfRangeException(nameof(requirementType), requirementType, "Unknown requirement type.");
        }
    }
}
