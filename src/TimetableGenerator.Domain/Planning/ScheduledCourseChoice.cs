using System;
using System.Collections.Generic;
using TimetableGenerator.Domain.Catalogs;

namespace TimetableGenerator.Domain.Planning;

public sealed class ScheduledCourseChoice
{
    private readonly IReadOnlyList<OfferingId> mOfferingIds;

    public CourseId CourseId { get; }

    public IReadOnlyList<OfferingId> OfferingIds
    {
        get
        {
            return mOfferingIds;
        }
    }

    public ScheduledCourseChoice(
        CourseId courseId,
        IEnumerable<OfferingId> offeringIds)
    {
        if (courseId == null)
        {
            throw new ArgumentNullException(nameof(courseId));
        }

        if (offeringIds == null)
        {
            throw new ArgumentNullException(nameof(offeringIds));
        }

        List<OfferingId> copiedOfferingIds = new List<OfferingId>();
        HashSet<OfferingId> uniqueOfferingIds = new HashSet<OfferingId>();
        foreach (OfferingId offeringId in offeringIds)
        {
            if (offeringId == null)
            {
                throw new ArgumentException(
                    "Scheduled course choices cannot contain null offering IDs.",
                    nameof(offeringIds));
            }

            if (uniqueOfferingIds.Add(offeringId) == false)
            {
                throw new ArgumentException(
                    "Scheduled course choices cannot contain duplicate offering IDs.",
                    nameof(offeringIds));
            }

            copiedOfferingIds.Add(offeringId);
        }

        if (copiedOfferingIds.Count == 0)
        {
            throw new ArgumentException(
                "Scheduled course choices require at least one offering ID.",
                nameof(offeringIds));
        }

        CourseId = courseId;
        mOfferingIds = copiedOfferingIds.AsReadOnly();
    }
}
