using System;
using System.Collections.Generic;

namespace TimetableGenerator.CatalogJson;

public sealed class CatalogDataQualityMetadata
{
    private readonly IReadOnlyList<CatalogManualReview> mManualReviews;

    public EScheduleNormalizationSource ScheduleNormalizationSource { get; }

    public int SourceEnglishScheduleMismatchCount { get; }

    public int RoomNotProvidedCount { get; }

    public int EnrollmentNotProvidedCount { get; }

    public int InstructorUnconfirmedCount { get; }

    public int MultiInstructorDisplayCount { get; }

    public int SourceRemarkLookupOnlyCount { get; }

    public IReadOnlyList<CatalogManualReview> ManualReviews
    {
        get
        {
            return mManualReviews;
        }
    }

    public CatalogDataQualityMetadata(
        EScheduleNormalizationSource scheduleNormalizationSource,
        int sourceEnglishScheduleMismatchCount,
        int roomNotProvidedCount,
        int enrollmentNotProvidedCount,
        int instructorUnconfirmedCount,
        int multiInstructorDisplayCount,
        int sourceRemarkLookupOnlyCount,
        IEnumerable<CatalogManualReview> manualReviews)
    {
        validateCount(sourceEnglishScheduleMismatchCount, nameof(sourceEnglishScheduleMismatchCount));
        validateCount(roomNotProvidedCount, nameof(roomNotProvidedCount));
        validateCount(enrollmentNotProvidedCount, nameof(enrollmentNotProvidedCount));
        validateCount(instructorUnconfirmedCount, nameof(instructorUnconfirmedCount));
        validateCount(multiInstructorDisplayCount, nameof(multiInstructorDisplayCount));
        validateCount(sourceRemarkLookupOnlyCount, nameof(sourceRemarkLookupOnlyCount));
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

    private static void validateCount(int count, string parameterName)
    {
        if (count < 0)
        {
            throw new ArgumentOutOfRangeException(
                parameterName,
                count,
                "Data quality counts cannot be negative.");
        }
    }
}
