using System;
using System.Collections.Generic;
using System.Threading;
using TimetableGenerator.Domain.Planning;
using TimetableGenerator.Domain.Scheduling;

namespace TimetableGenerator.Application.Scheduling;

internal sealed class ScheduleRecommendationGenerationState
{
    public IReadOnlyList<ValidatedScheduleChoice> ScheduledChoices { get; }

    public IReadOnlyList<UnscheduledOfferingSelection> UnscheduledSelections { get; }

    public IReadOnlyList<PersonalSchedule> PersonalSchedules { get; }

    public ScheduleRecommendationLimit MaximumRecommendationCount { get; }

    public CancellationToken CancellationToken { get; }

    public List<ScheduledOffering> SelectedOfferings { get; }

    public HashSet<MeetingSlot> OccupiedSlots { get; }

    public List<ScheduleRecommendation> Recommendations { get; }

    public EScheduleRecommendationCompletion Completion { get; private set; }

    public bool ShouldStop
    {
        get
        {
            return Completion != EScheduleRecommendationCompletion.Completed;
        }
    }

    public ScheduleRecommendationGenerationState(
        IReadOnlyList<ValidatedScheduleChoice> scheduledChoices,
        IReadOnlyList<UnscheduledOfferingSelection> unscheduledSelections,
        IReadOnlyList<PersonalSchedule> personalSchedules,
        ScheduleRecommendationLimit maximumRecommendationCount,
        CancellationToken cancellationToken)
    {
        if (scheduledChoices == null)
        {
            throw new ArgumentNullException(nameof(scheduledChoices));
        }

        if (unscheduledSelections == null)
        {
            throw new ArgumentNullException(nameof(unscheduledSelections));
        }

        if (personalSchedules == null)
        {
            throw new ArgumentNullException(nameof(personalSchedules));
        }

        if (maximumRecommendationCount.IsValid == false)
        {
            throw new ArgumentException(
                "Recommendation generation state requires a valid result limit.",
                nameof(maximumRecommendationCount));
        }

        ScheduledChoices = scheduledChoices;
        UnscheduledSelections = unscheduledSelections;
        PersonalSchedules = personalSchedules;
        MaximumRecommendationCount = maximumRecommendationCount;
        CancellationToken = cancellationToken;
        SelectedOfferings = new List<ScheduledOffering>(scheduledChoices.Count);
        OccupiedSlots = new HashSet<MeetingSlot>();
        Recommendations = new List<ScheduleRecommendation>();
        Completion = EScheduleRecommendationCompletion.Completed;
    }

    public void MarkCanceled()
    {
        Completion = EScheduleRecommendationCompletion.Canceled;
    }

    public void MarkMaximumRecommendationCountReached()
    {
        Completion = EScheduleRecommendationCompletion.MaximumRecommendationCountReached;
    }
}
