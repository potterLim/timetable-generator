using System;
using System.Collections.Generic;
using TimetableGenerator.Domain.Planning;
using TimetableGenerator.Domain.Scheduling;

namespace TimetableGenerator.Application.Scheduling;

internal sealed class ScheduleSearchNode
{
    private readonly IReadOnlyList<ScheduledOffering> mSelectedOfferings;

    private readonly IReadOnlyList<UnscheduledOfferingSelection>
        mSelectedUnscheduledSelections;

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

    public IReadOnlyList<UnscheduledOfferingSelection>
        SelectedUnscheduledSelections
    {
        get
        {
            return mSelectedUnscheduledSelections;
        }
    }

    public RecommendationScore Score { get; }

    private ScheduleSearchNode(
        int nextGroupIndex,
        IReadOnlyList<ScheduledOffering> selectedOfferings,
        IReadOnlyList<UnscheduledOfferingSelection> selectedUnscheduledSelections,
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

        if (selectedUnscheduledSelections == null)
        {
            throw new ArgumentNullException(nameof(selectedUnscheduledSelections));
        }

        NextGroupIndex = nextGroupIndex;
        mSelectedOfferings = selectedOfferings;
        mSelectedUnscheduledSelections = selectedUnscheduledSelections;
        mOccupiedSlots = occupiedSlots;
        Score = score;
    }

    public static ScheduleSearchNode CreateRoot()
    {
        return new ScheduleSearchNode(
            0,
            Array.Empty<ScheduledOffering>(),
            Array.Empty<UnscheduledOfferingSelection>(),
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
        List<UnscheduledOfferingSelection> selectedUnscheduledSelections =
            new List<UnscheduledOfferingSelection>(
                mSelectedUnscheduledSelections);

        HashSet<MeetingSlot> occupiedSlots = new HashSet<MeetingSlot>(mOccupiedSlots);
        if (offeringCandidate.IsScheduled)
        {
            ScheduledOffering scheduledOffering =
                offeringCandidate.GetScheduledOffering();
            selectedOfferings.Add(scheduledOffering);
            foreach (MeetingSlot meetingSlot in scheduledOffering.MeetingSlots)
            {
                bool hasAddedSlot = occupiedSlots.Add(meetingSlot);
                if (hasAddedSlot == false)
                {
                    throw new InvalidOperationException(
                        "A validated offering contains an occupied meeting slot.");
                }
            }
        }
        else
        {
            selectedUnscheduledSelections.Add(
                offeringCandidate.GetUnscheduledSelection());
        }

        return new ScheduleSearchNode(
            NextGroupIndex + 1,
            selectedOfferings.AsReadOnly(),
            selectedUnscheduledSelections.AsReadOnly(),
            occupiedSlots,
            Score.Add(offeringCandidate.Score));
    }
}
