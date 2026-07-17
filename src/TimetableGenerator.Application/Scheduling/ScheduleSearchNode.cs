using System;
using System.Collections.Generic;
using TimetableGenerator.Domain.Scheduling;

namespace TimetableGenerator.Application.Scheduling;

internal sealed class ScheduleSearchNode
{
    private readonly IReadOnlyList<ScheduledOffering> mSelectedOfferings;

    private readonly HashSet<MeetingSlot> mOccupiedSlots;

    public int NextGroupIndex { get; }

    public IReadOnlyList<ScheduledOffering> SelectedOfferings
    {
        get
        {
            return mSelectedOfferings;
        }
    }

    public IReadOnlySet<MeetingSlot> OccupiedSlots
    {
        get
        {
            return mOccupiedSlots;
        }
    }

    public RecommendationScore Score { get; }

    private ScheduleSearchNode(
        int nextGroupIndex,
        IReadOnlyList<ScheduledOffering> selectedOfferings,
        HashSet<MeetingSlot> occupiedSlots,
        RecommendationScore score)
    {
        if (nextGroupIndex < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(nextGroupIndex));
        }

        if (selectedOfferings == null)
        {
            throw new ArgumentNullException(nameof(selectedOfferings));
        }

        if (occupiedSlots == null)
        {
            throw new ArgumentNullException(nameof(occupiedSlots));
        }

        NextGroupIndex = nextGroupIndex;
        mSelectedOfferings = selectedOfferings;
        mOccupiedSlots = occupiedSlots;
        Score = score;
    }

    public static ScheduleSearchNode CreateRoot()
    {
        return new ScheduleSearchNode(
            0,
            Array.Empty<ScheduledOffering>(),
            new HashSet<MeetingSlot>(),
            RecommendationScore.ZERO);
    }

    public ScheduleSearchNode CreateChild(
        ValidatedOfferingCandidate offeringCandidate)
    {
        if (offeringCandidate == null)
        {
            throw new ArgumentNullException(nameof(offeringCandidate));
        }

        List<ScheduledOffering> selectedOfferings =
            new List<ScheduledOffering>(mSelectedOfferings);
        selectedOfferings.Add(offeringCandidate.Offering);

        HashSet<MeetingSlot> occupiedSlots = new HashSet<MeetingSlot>(mOccupiedSlots);
        foreach (MeetingSlot meetingSlot in offeringCandidate.Offering.MeetingSlots)
        {
            bool hasAddedSlot = occupiedSlots.Add(meetingSlot);
            if (hasAddedSlot == false)
            {
                throw new InvalidOperationException(
                    "A validated offering contains an occupied meeting slot.");
            }
        }

        return new ScheduleSearchNode(
            NextGroupIndex + 1,
            selectedOfferings.AsReadOnly(),
            occupiedSlots,
            Score.Add(offeringCandidate.Score));
    }
}
