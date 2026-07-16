using System;
using System.Collections.Generic;

using TimetableGenerator.CatalogJson;
using TimetableGenerator.Desktop.Presentation.Models;
using TimetableGenerator.Domain.Catalogs;
using TimetableGenerator.Domain.Scheduling;

namespace TimetableGenerator.Desktop.Presentation.Catalog;

internal sealed class CatalogCourseProjection
{
    private readonly IReadOnlyList<CatalogOfferingProjection> mOfferings;

    private readonly IReadOnlyList<OfferingId> mScheduledOfferingIds;

    private readonly IReadOnlyList<OfferingId> mTimeNotProvidedOfferingIds;

    private readonly IReadOnlyList<OfferingUnitName> mOfferingUnitNames;

    private readonly IReadOnlyList<ERequirementType> mRequirementTypes;

    public CatalogCourse Course { get; }

    public ECourseAccent Accent { get; }

    public IReadOnlyList<CatalogOfferingProjection> Offerings
    {
        get
        {
            return mOfferings;
        }
    }

    public IReadOnlyList<OfferingId> ScheduledOfferingIds
    {
        get
        {
            return mScheduledOfferingIds;
        }
    }

    public IReadOnlyList<OfferingId> TimeNotProvidedOfferingIds
    {
        get
        {
            return mTimeNotProvidedOfferingIds;
        }
    }

    public IReadOnlyList<OfferingUnitName> OfferingUnitNames
    {
        get
        {
            return mOfferingUnitNames;
        }
    }

    public IReadOnlyList<ERequirementType> RequirementTypes
    {
        get
        {
            return mRequirementTypes;
        }
    }

    public CatalogCourseProjection(
        CatalogCourse course,
        ECourseAccent accent,
        IEnumerable<CatalogOfferingProjection> offerings)
    {
        if (course == null)
        {
            throw new ArgumentNullException(nameof(course));
        }

        if (Enum.IsDefined(typeof(ECourseAccent), accent) == false)
        {
            throw new ArgumentOutOfRangeException(nameof(accent));
        }

        if (offerings == null)
        {
            throw new ArgumentNullException(nameof(offerings));
        }

        List<CatalogOfferingProjection> copiedOfferings =
            new List<CatalogOfferingProjection>();
        List<OfferingId> scheduledOfferingIds = new List<OfferingId>();
        List<OfferingId> timeNotProvidedOfferingIds = new List<OfferingId>();
        HashSet<OfferingId> uniqueOfferingIds = new HashSet<OfferingId>();
        SortedDictionary<string, OfferingUnitName> offeringUnitNamesByValue =
            new SortedDictionary<string, OfferingUnitName>(StringComparer.Ordinal);
        SortedSet<ERequirementType> requirementTypes = new SortedSet<ERequirementType>();

        foreach (CatalogOfferingProjection offering in offerings)
        {
            validateOffering(course, offering, uniqueOfferingIds);
            copiedOfferings.Add(offering);

            if (offering.Offering.MeetingSchedule.Status
                == EMeetingScheduleStatus.Scheduled)
            {
                scheduledOfferingIds.Add(offering.Offering.Id);
            }
            else
            {
                timeNotProvidedOfferingIds.Add(offering.Offering.Id);
            }

            OfferingUnitName offeringUnitName =
                offering.Metadata.Classification.OfferingUnitName;
            offeringUnitNamesByValue.TryAdd(offeringUnitName.Value, offeringUnitName);
            ERequirementType requirementType =
                offering.Metadata.Classification.RequirementType;
            if (Enum.IsDefined(typeof(ERequirementType), requirementType) == false)
            {
                throw new ArgumentException(
                    "Course projections require defined requirement types.",
                    nameof(offerings));
            }

            requirementTypes.Add(requirementType);
        }

        Course = course;
        Accent = accent;
        mOfferings = copiedOfferings.AsReadOnly();
        mScheduledOfferingIds = scheduledOfferingIds.AsReadOnly();
        mTimeNotProvidedOfferingIds = timeNotProvidedOfferingIds.AsReadOnly();
        mOfferingUnitNames = new List<OfferingUnitName>(
            offeringUnitNamesByValue.Values).AsReadOnly();
        mRequirementTypes = new List<ERequirementType>(requirementTypes).AsReadOnly();
    }

    private static void validateOffering(
        CatalogCourse course,
        CatalogOfferingProjection offering,
        ISet<OfferingId> uniqueOfferingIds)
    {
        if (offering == null)
        {
            throw new ArgumentException(
                "Course projections cannot contain null offerings.",
                nameof(offering));
        }

        if (offering.Offering.CourseId != course.Id)
        {
            throw new ArgumentException(
                "Every projected offering must belong to the projected course.",
                nameof(offering));
        }

        if (uniqueOfferingIds.Add(offering.Offering.Id) == false)
        {
            throw new ArgumentException(
                "Course projections cannot contain duplicate offerings.",
                nameof(offering));
        }
    }
}
