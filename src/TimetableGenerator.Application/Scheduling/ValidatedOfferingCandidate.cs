using System;
using TimetableGenerator.Domain.Planning;
using TimetableGenerator.Domain.Scheduling;

namespace TimetableGenerator.Application.Scheduling;

internal sealed class ValidatedOfferingCandidate
{
    public ScheduledOffering Offering { get; }

    public EOfferingPreference Preference { get; }

    public RecommendationScore Score { get; }

    public ValidatedOfferingCandidate(
        ScheduledOffering offering,
        EOfferingPreference preference)
    {
        if (offering == null)
        {
            throw new ArgumentNullException(nameof(offering));
        }

        if (preference != EOfferingPreference.Preferred
            && preference != EOfferingPreference.Acceptable)
        {
            throw new ArgumentOutOfRangeException(nameof(preference));
        }

        Offering = offering;
        Preference = preference;
        Score = preference == EOfferingPreference.Preferred
            ? RecommendationScore.ZERO
            : new RecommendationScore(1);
    }
}
