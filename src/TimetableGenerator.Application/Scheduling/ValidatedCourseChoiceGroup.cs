using System;
using System.Collections.Generic;
using TimetableGenerator.Domain.Planning;

namespace TimetableGenerator.Application.Scheduling;

internal sealed class ValidatedCourseChoiceGroup
{
    private readonly IReadOnlyList<ValidatedOfferingCandidate> mOfferingCandidates;

    public IReadOnlyList<ValidatedOfferingCandidate> OfferingCandidates
    {
        get
        {
            return mOfferingCandidates;
        }
    }

    public RecommendationScore MinimumScore { get; }

    public ValidatedCourseChoiceGroup(IEnumerable<ValidatedOfferingCandidate> offeringCandidates)
    {
        if (offeringCandidates == null)
        {
            throw new ArgumentNullException(nameof(offeringCandidates));
        }

        List<ValidatedOfferingCandidate> preferredCandidates = new List<ValidatedOfferingCandidate>();
        List<ValidatedOfferingCandidate> acceptableCandidates = new List<ValidatedOfferingCandidate>();
        foreach (ValidatedOfferingCandidate offeringCandidate
            in offeringCandidates)
        {
            if (offeringCandidate == null)
            {
                throw new ArgumentException(
                    "Validated course choice groups cannot contain null candidates.",
                    nameof(offeringCandidates));
            }

            if (offeringCandidate.Preference == EOfferingPreference.Preferred)
            {
                preferredCandidates.Add(offeringCandidate);
            }
            else
            {
                acceptableCandidates.Add(offeringCandidate);
            }
        }

        if (preferredCandidates.Count == 0 && acceptableCandidates.Count == 0)
        {
            throw new ArgumentException(
                "Validated course choice groups require an eligible offering.",
                nameof(offeringCandidates));
        }

        List<ValidatedOfferingCandidate> copiedCandidates =
            new List<ValidatedOfferingCandidate>(
                preferredCandidates.Count + acceptableCandidates.Count);
        copiedCandidates.AddRange(preferredCandidates);
        copiedCandidates.AddRange(acceptableCandidates);
        mOfferingCandidates = copiedCandidates.AsReadOnly();
        MinimumScore = preferredCandidates.Count > 0
            ? RecommendationScore.ZERO
            : new RecommendationScore(1);
    }
}
