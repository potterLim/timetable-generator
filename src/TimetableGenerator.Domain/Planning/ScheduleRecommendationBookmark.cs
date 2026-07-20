using System;
using System.Collections.Generic;

using TimetableGenerator.Domain.Catalogs;

namespace TimetableGenerator.Domain.Planning;

public sealed class ScheduleRecommendationBookmark
{
    private readonly IReadOnlyList<OfferingId> mSelectedOfferingIds;

    public IReadOnlyList<OfferingId> SelectedOfferingIds
    {
        get
        {
            return mSelectedOfferingIds;
        }
    }

    public IReadOnlyList<OfferingId> ScheduledOfferingIds
    {
        get
        {
            return mSelectedOfferingIds;
        }
    }

    public ScheduleRecommendationBookmark(
        IEnumerable<OfferingId> selectedOfferingIds)
    {
        if (selectedOfferingIds == null)
        {
            throw new ArgumentNullException(nameof(selectedOfferingIds));
        }

        List<OfferingId> copiedOfferingIds = new List<OfferingId>();
        HashSet<OfferingId> uniqueOfferingIds = new HashSet<OfferingId>();
        foreach (OfferingId offeringId in selectedOfferingIds)
        {
            if (offeringId == null)
            {
                throw new ArgumentException(
                    "Schedule recommendation bookmarks cannot contain null offering IDs.",
                    nameof(selectedOfferingIds));
            }

            if (uniqueOfferingIds.Add(offeringId) == false)
            {
                throw new ArgumentException(
                    "Schedule recommendation bookmarks require unique offering IDs.",
                    nameof(selectedOfferingIds));
            }

            copiedOfferingIds.Add(offeringId);
        }

        if (copiedOfferingIds.Count == 0)
        {
            throw new ArgumentException(
                "Schedule recommendation bookmarks require at least one offering ID.",
                nameof(selectedOfferingIds));
        }

        copiedOfferingIds.Sort(compareOfferingIds);
        mSelectedOfferingIds = copiedOfferingIds.AsReadOnly();
    }

    public bool HasSameOfferingIds(IEnumerable<OfferingId> offeringIds)
    {
        return hasSameOfferingIds(offeringIds, nameof(offeringIds));
    }

    public bool HasSameScheduledOfferingIds(
        IEnumerable<OfferingId> scheduledOfferingIds)
    {
        return hasSameOfferingIds(
            scheduledOfferingIds,
            nameof(scheduledOfferingIds));
    }

    public bool ContainsOffering(OfferingId offeringId)
    {
        if (offeringId == null)
        {
            throw new ArgumentNullException(nameof(offeringId));
        }

        foreach (OfferingId bookmarkedOfferingId in mSelectedOfferingIds)
        {
            if (bookmarkedOfferingId == offeringId)
            {
                return true;
            }
        }

        return false;
    }

    public bool ContainsScheduledOffering(OfferingId offeringId)
    {
        return ContainsOffering(offeringId);
    }

    private bool hasSameOfferingIds(
        IEnumerable<OfferingId> offeringIds,
        string parameterName)
    {
        if (offeringIds == null)
        {
            throw new ArgumentNullException(parameterName);
        }

        HashSet<OfferingId> candidateOfferingIds = new HashSet<OfferingId>();
        foreach (OfferingId offeringId in offeringIds)
        {
            if (offeringId == null)
            {
                throw new ArgumentException(
                    "Compared recommendation offerings cannot contain null IDs.",
                    parameterName);
            }

            if (candidateOfferingIds.Add(offeringId) == false)
            {
                return false;
            }
        }

        if (candidateOfferingIds.Count != mSelectedOfferingIds.Count)
        {
            return false;
        }

        foreach (OfferingId offeringId in mSelectedOfferingIds)
        {
            if (candidateOfferingIds.Contains(offeringId) == false)
            {
                return false;
            }
        }

        return true;
    }

    private static int compareOfferingIds(OfferingId left, OfferingId right)
    {
        return string.Compare(left.Value, right.Value, StringComparison.Ordinal);
    }
}
