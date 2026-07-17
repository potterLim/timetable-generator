using System;
using System.Collections.Generic;

using TimetableGenerator.Domain.Catalogs;

namespace TimetableGenerator.Domain.Planning;

public sealed class ScheduleRecommendationBookmark
{
    private readonly IReadOnlyList<OfferingId> mScheduledOfferingIds;

    public IReadOnlyList<OfferingId> ScheduledOfferingIds
    {
        get
        {
            return mScheduledOfferingIds;
        }
    }

    public ScheduleRecommendationBookmark(
        IEnumerable<OfferingId> scheduledOfferingIds)
    {
        if (scheduledOfferingIds == null)
        {
            throw new ArgumentNullException(nameof(scheduledOfferingIds));
        }

        List<OfferingId> copiedOfferingIds = new List<OfferingId>();
        HashSet<OfferingId> uniqueOfferingIds = new HashSet<OfferingId>();
        foreach (OfferingId offeringId in scheduledOfferingIds)
        {
            if (offeringId == null)
            {
                throw new ArgumentException(
                    "Schedule recommendation bookmarks cannot contain null offering IDs.",
                    nameof(scheduledOfferingIds));
            }

            if (uniqueOfferingIds.Add(offeringId) == false)
            {
                throw new ArgumentException(
                    "Schedule recommendation bookmarks require unique offering IDs.",
                    nameof(scheduledOfferingIds));
            }

            copiedOfferingIds.Add(offeringId);
        }

        if (copiedOfferingIds.Count == 0)
        {
            throw new ArgumentException(
                "Schedule recommendation bookmarks require at least one offering ID.",
                nameof(scheduledOfferingIds));
        }

        copiedOfferingIds.Sort(compareOfferingIds);
        mScheduledOfferingIds = copiedOfferingIds.AsReadOnly();
    }

    public bool HasSameScheduledOfferingIds(
        IEnumerable<OfferingId> scheduledOfferingIds)
    {
        if (scheduledOfferingIds == null)
        {
            throw new ArgumentNullException(nameof(scheduledOfferingIds));
        }

        HashSet<OfferingId> candidateOfferingIds = new HashSet<OfferingId>();
        foreach (OfferingId offeringId in scheduledOfferingIds)
        {
            if (offeringId == null)
            {
                throw new ArgumentException(
                    "Compared recommendation offerings cannot contain null IDs.",
                    nameof(scheduledOfferingIds));
            }

            if (candidateOfferingIds.Add(offeringId) == false)
            {
                return false;
            }
        }

        if (candidateOfferingIds.Count != mScheduledOfferingIds.Count)
        {
            return false;
        }

        foreach (OfferingId offeringId in mScheduledOfferingIds)
        {
            if (candidateOfferingIds.Contains(offeringId) == false)
            {
                return false;
            }
        }

        return true;
    }

    public bool ContainsScheduledOffering(OfferingId offeringId)
    {
        if (offeringId == null)
        {
            throw new ArgumentNullException(nameof(offeringId));
        }

        foreach (OfferingId bookmarkedOfferingId in mScheduledOfferingIds)
        {
            if (bookmarkedOfferingId == offeringId)
            {
                return true;
            }
        }

        return false;
    }

    private static int compareOfferingIds(OfferingId left, OfferingId right)
    {
        return string.Compare(left.Value, right.Value, StringComparison.Ordinal);
    }
}
