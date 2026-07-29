using System;
using System.Collections.Generic;

using TimetableGenerator.Desktop.Presentation.Models;
using PresentationScheduleRecommendation = TimetableGenerator.Desktop.Presentation.Models.ScheduleRecommendation;

namespace TimetableGenerator.Desktop.Presentation.Recommendations;

internal sealed class RecommendationProjectionBatch
{
    public IReadOnlyList<ScheduleRecommendationViewItem> Recommendations { get; }

    public IReadOnlyList<PresentationScheduleRecommendation> PngExportCandidateSchedules { get; }

    public PresentationScheduleRecommendation PersonalSchedulePreview { get; }

    public ScheduleBoardDayRange DayRange { get; }

    public bool HasUnsatisfiedScheduleConstraints { get; }

    public bool HasAdditionalRecommendations { get; }

    public RecommendationProjectionBatch(
        IReadOnlyList<ScheduleRecommendationViewItem> recommendations,
        IReadOnlyList<PresentationScheduleRecommendation> pngExportCandidateSchedules,
        PresentationScheduleRecommendation personalSchedulePreview,
        ScheduleBoardDayRange dayRange,
        bool hasUnsatisfiedScheduleConstraints,
        bool hasAdditionalRecommendations)
    {
        if (recommendations == null)
        {
            throw new ArgumentNullException(nameof(recommendations));
        }

        if (pngExportCandidateSchedules == null)
        {
            throw new ArgumentNullException(nameof(pngExportCandidateSchedules));
        }

        if (personalSchedulePreview == null)
        {
            throw new ArgumentNullException(nameof(personalSchedulePreview));
        }

        if (dayRange == null)
        {
            throw new ArgumentNullException(nameof(dayRange));
        }

        Recommendations = recommendations;
        PngExportCandidateSchedules = pngExportCandidateSchedules;
        PersonalSchedulePreview = personalSchedulePreview;
        DayRange = dayRange;
        HasUnsatisfiedScheduleConstraints = hasUnsatisfiedScheduleConstraints;
        HasAdditionalRecommendations = hasAdditionalRecommendations;
    }
}
