using System;
using System.Collections.Generic;
using TimetableGenerator.Domain.Catalogs;

namespace TimetableGenerator.Domain.Planning;

public sealed class CourseCandidate
{
    private readonly IReadOnlyList<OfferingCandidate> mOfferingCandidates;

    public CourseId CourseId { get; }

    public IReadOnlyList<OfferingCandidate> OfferingCandidates
    {
        get
        {
            return mOfferingCandidates;
        }
    }

    public CourseCandidate(
        CourseId courseId,
        IEnumerable<OfferingCandidate> offeringCandidates)
    {
        if (courseId == null)
        {
            throw new ArgumentNullException(nameof(courseId));
        }

        if (offeringCandidates == null)
        {
            throw new ArgumentNullException(nameof(offeringCandidates));
        }

        List<OfferingCandidate> copiedCandidates = new List<OfferingCandidate>();
        HashSet<OfferingId> offeringIds = new HashSet<OfferingId>();
        bool hasEligibleOffering = false;
        foreach (OfferingCandidate offeringCandidate in offeringCandidates)
        {
            if (offeringCandidate == null)
            {
                throw new ArgumentException(
                    "Course candidates cannot contain null offering candidates.",
                    nameof(offeringCandidates));
            }

            if (offeringIds.Add(offeringCandidate.OfferingId) == false)
            {
                throw new ArgumentException(
                    "Course candidates cannot contain duplicate offering IDs.",
                    nameof(offeringCandidates));
            }

            if (offeringCandidate.IsEligible)
            {
                hasEligibleOffering = true;
            }

            copiedCandidates.Add(offeringCandidate);
        }

        if (copiedCandidates.Count == 0)
        {
            throw new ArgumentException(
                "Course candidates require at least one offering candidate.",
                nameof(offeringCandidates));
        }

        if (hasEligibleOffering == false)
        {
            throw new ArgumentException(
                "Course candidates require at least one eligible offering.",
                nameof(offeringCandidates));
        }

        CourseId = courseId;
        mOfferingCandidates = copiedCandidates.AsReadOnly();
    }
}
