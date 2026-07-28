using System;
using System.Collections.Generic;

namespace TimetableGenerator.HandongCatalogGenerator.Domain;

internal sealed class CatalogDataQuality
{
    private readonly IReadOnlyList<CatalogManualReview> mManualReviews;

    public EScheduleNormalizationSource ScheduleNormalizationSource { get; }

    public CatalogItemCount EnglishScheduleMismatchCount { get; }

    public CatalogItemCount RoomNotProvidedCount { get; }

    public CatalogItemCount EnrollmentNotProvidedCount { get; }

    public CatalogItemCount InstructorUnconfirmedCount { get; }

    public CatalogItemCount MultiInstructorDisplayCount { get; }

    public CatalogItemCount SourceRemarkLookupOnlyCount { get; }

    public IReadOnlyList<CatalogManualReview> ManualReviews
    {
        get
        {
            return mManualReviews;
        }
    }

    public CatalogDataQuality(
        EScheduleNormalizationSource scheduleNormalizationSource,
        CatalogItemCount englishScheduleMismatchCount,
        CatalogItemCount roomNotProvidedCount,
        CatalogItemCount enrollmentNotProvidedCount,
        CatalogItemCount instructorUnconfirmedCount,
        CatalogItemCount multiInstructorDisplayCount,
        CatalogItemCount sourceRemarkLookupOnlyCount,
        IEnumerable<CatalogManualReview> manualReviews)
    {
        if (Enum.IsDefined(typeof(EScheduleNormalizationSource), scheduleNormalizationSource) == false)
        {
            throw new ArgumentOutOfRangeException(nameof(scheduleNormalizationSource));
        }

        if (manualReviews == null)
        {
            throw new ArgumentNullException(nameof(manualReviews));
        }

        List<CatalogManualReview> copiedManualReviews = new List<CatalogManualReview>();
        foreach (CatalogManualReview manualReview in manualReviews)
        {
            if (manualReview == null)
            {
                throw new ArgumentException("Data quality cannot contain null manual reviews.", nameof(manualReviews));
            }

            copiedManualReviews.Add(manualReview);
        }

        ScheduleNormalizationSource = scheduleNormalizationSource;
        EnglishScheduleMismatchCount = englishScheduleMismatchCount;
        RoomNotProvidedCount = roomNotProvidedCount;
        EnrollmentNotProvidedCount = enrollmentNotProvidedCount;
        InstructorUnconfirmedCount = instructorUnconfirmedCount;
        MultiInstructorDisplayCount = multiInstructorDisplayCount;
        SourceRemarkLookupOnlyCount = sourceRemarkLookupOnlyCount;
        mManualReviews = copiedManualReviews.AsReadOnly();
    }
}
