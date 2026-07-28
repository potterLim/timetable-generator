using System;
using TimetableGenerator.Domain.Catalogs;
using TimetableGenerator.Domain.Planning;
using TimetableGenerator.Domain.Scheduling;

namespace TimetableGenerator.Application.Scheduling;

internal sealed class ValidatedOfferingCandidate
{
    private readonly ScheduledOffering? mScheduledOfferingOrNull;

    private readonly UnscheduledOfferingSelection? mUnscheduledSelectionOrNull;

    public CourseId CourseId { get; }

    public OfferingId OfferingId { get; }

    public bool IsScheduled
    {
        get
        {
            return mScheduledOfferingOrNull != null;
        }
    }

    public EOfferingPreference Preference { get; }

    public RecommendationScore Score { get; }

    public ValidatedOfferingCandidate(ScheduledOffering offering, EOfferingPreference preference)
    {
        if (offering == null)
        {
            throw new ArgumentNullException(nameof(offering));
        }

        validatePreference(preference);

        mScheduledOfferingOrNull = offering;
        mUnscheduledSelectionOrNull = null;
        CourseId = offering.CourseId;
        OfferingId = offering.OfferingId;
        Preference = preference;
        Score = createScore(preference);
    }

    public ValidatedOfferingCandidate(UnscheduledOfferingSelection selection, EOfferingPreference preference)
    {
        if (selection == null)
        {
            throw new ArgumentNullException(nameof(selection));
        }

        validatePreference(preference);

        mScheduledOfferingOrNull = null;
        mUnscheduledSelectionOrNull = selection;
        CourseId = selection.CourseId;
        OfferingId = selection.OfferingId;
        Preference = preference;
        Score = createScore(preference);
    }

    public ScheduledOffering GetScheduledOffering()
    {
        if (mScheduledOfferingOrNull == null)
        {
            throw new InvalidOperationException("Time-not-provided candidates do not have a scheduled offering.");
        }

        return mScheduledOfferingOrNull;
    }

    public UnscheduledOfferingSelection GetUnscheduledSelection()
    {
        if (mUnscheduledSelectionOrNull == null)
        {
            throw new InvalidOperationException("Scheduled candidates do not have an unscheduled selection.");
        }

        return mUnscheduledSelectionOrNull;
    }

    private static RecommendationScore createScore(EOfferingPreference preference)
    {
        validatePreference(preference);
        return preference == EOfferingPreference.Preferred ? RecommendationScore.ZERO : new RecommendationScore(1);
    }

    private static void validatePreference(EOfferingPreference preference)
    {
        if (preference != EOfferingPreference.Preferred && preference != EOfferingPreference.Acceptable)
        {
            throw new ArgumentOutOfRangeException(nameof(preference));
        }
    }
}
