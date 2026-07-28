using System;
using System.Collections.Generic;

using TimetableGenerator.Desktop.Presentation.Models;
using TimetableGenerator.Domain.Planning;

namespace TimetableGenerator.Desktop.Exporting;

internal sealed class SchedulePngExportBatch
{
    private readonly IReadOnlyList<ScheduleBoardPresentation> mCandidates;

    public PlanName PlanName { get; }

    public IReadOnlyList<ScheduleBoardPresentation> Candidates
    {
        get
        {
            return mCandidates;
        }
    }

    public SchedulePngExportBatch(IReadOnlyList<ScheduleBoardPresentation> candidates)
    {
        ArgumentNullException.ThrowIfNull(candidates);
        if (candidates.Count <= 1)
        {
            throw new ArgumentException("A batch PNG export requires multiple timetable candidates.", nameof(candidates));
        }

        ScheduleBoardPresentation firstCandidate = candidates[0];
        if (firstCandidate == null)
        {
            throw new ArgumentException("A batch PNG export cannot contain null candidates.", nameof(candidates));
        }

        PlanName = firstCandidate.PlanName;
        List<ScheduleBoardPresentation> copiedCandidates = new List<ScheduleBoardPresentation>(candidates.Count);
        foreach (ScheduleBoardPresentation candidate in candidates)
        {
            if (candidate == null)
            {
                throw new ArgumentException("A batch PNG export cannot contain null candidates.", nameof(candidates));
            }

            if (candidate.PlanName != PlanName)
            {
                throw new ArgumentException("Every PNG export candidate must belong to the same plan.", nameof(candidates));
            }

            copiedCandidates.Add(candidate);
        }

        mCandidates = copiedCandidates.AsReadOnly();
    }
}
