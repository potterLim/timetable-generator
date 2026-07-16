using System;
using TimetableGenerator.Domain.Catalogs;

namespace TimetableGenerator.CatalogJson;

public sealed class CatalogManualReview
{
    public CourseId CourseId { get; }

    public EManualReviewField Field { get; }

    public EManualReviewReason Reason { get; }

    public string SourceValue { get; }

    public CatalogManualReview(
        CourseId courseId,
        EManualReviewField field,
        EManualReviewReason reason,
        string sourceValue)
    {
        if (courseId == null)
        {
            throw new ArgumentNullException(nameof(courseId));
        }

        if (string.IsNullOrWhiteSpace(sourceValue))
        {
            throw new ArgumentException(
                "Manual review source values cannot be empty.",
                nameof(sourceValue));
        }

        CourseId = courseId;
        Field = field;
        Reason = reason;
        SourceValue = sourceValue;
    }
}
