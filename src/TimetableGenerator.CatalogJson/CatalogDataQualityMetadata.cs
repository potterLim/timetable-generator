using System;
using System.Collections.Generic;

namespace TimetableGenerator.CatalogJson;

public sealed class CatalogDataQualityMetadata
{
    private readonly IReadOnlyList<CatalogManualReview> mManualReviews;

    public EScheduleNormalizationSource ScheduleNormalizationSource { get; }

    public CatalogSourceEnglishScheduleMismatchCount SourceEnglishScheduleMismatchCount { get; }

    public CatalogRoomNotProvidedCount RoomNotProvidedCount { get; }

    public CatalogEnrollmentNotProvidedCount EnrollmentNotProvidedCount { get; }

    public CatalogInstructorUnconfirmedCount InstructorUnconfirmedCount { get; }

    public CatalogMultiInstructorDisplayCount MultiInstructorDisplayCount { get; }

    public CatalogSourceRemarkLookupOnlyCount SourceRemarkLookupOnlyCount { get; }

    public IReadOnlyList<CatalogManualReview> ManualReviews
    {
        get
        {
            return mManualReviews;
        }
    }

    public CatalogDataQualityMetadata(
        EScheduleNormalizationSource scheduleNormalizationSource,
        CatalogSourceEnglishScheduleMismatchCount sourceEnglishScheduleMismatchCount,
        CatalogRoomNotProvidedCount roomNotProvidedCount,
        CatalogEnrollmentNotProvidedCount enrollmentNotProvidedCount,
        CatalogInstructorUnconfirmedCount instructorUnconfirmedCount,
        CatalogMultiInstructorDisplayCount multiInstructorDisplayCount,
        CatalogSourceRemarkLookupOnlyCount sourceRemarkLookupOnlyCount,
        IEnumerable<CatalogManualReview> manualReviews)
    {
        if (Enum.IsDefined(
            typeof(EScheduleNormalizationSource),
            scheduleNormalizationSource) == false)
        {
            throw new ArgumentOutOfRangeException(
                nameof(scheduleNormalizationSource),
                scheduleNormalizationSource,
                "The schedule normalization source is unsupported.");
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
                throw new ArgumentException(
                    "Data quality metadata cannot contain null manual reviews.",
                    nameof(manualReviews));
            }

            copiedManualReviews.Add(manualReview);
        }

        ScheduleNormalizationSource = scheduleNormalizationSource;
        SourceEnglishScheduleMismatchCount = sourceEnglishScheduleMismatchCount;
        RoomNotProvidedCount = roomNotProvidedCount;
        EnrollmentNotProvidedCount = enrollmentNotProvidedCount;
        InstructorUnconfirmedCount = instructorUnconfirmedCount;
        MultiInstructorDisplayCount = multiInstructorDisplayCount;
        SourceRemarkLookupOnlyCount = sourceRemarkLookupOnlyCount;
        mManualReviews = copiedManualReviews.AsReadOnly();
    }
}
