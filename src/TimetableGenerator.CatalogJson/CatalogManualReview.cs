using System;
using TimetableGenerator.Domain.Catalogs;

namespace TimetableGenerator.CatalogJson;

public sealed class CatalogManualReview
{
    public CourseId CourseId { get; }

    public EManualReviewField Field { get; }

    public EManualReviewReason Reason { get; }

    public CatalogManualReviewSourceValue SourceValue { get; }

    public CatalogManualReview(
        CourseId courseId,
        EManualReviewField field,
        EManualReviewReason reason,
        CatalogManualReviewSourceValue sourceValue)
    {
        if (courseId == null)
        {
            throw new ArgumentNullException(nameof(courseId));
        }

        if (sourceValue == null)
        {
            throw new ArgumentNullException(nameof(sourceValue));
        }

        CourseId = courseId;
        Field = field;
        Reason = reason;
        SourceValue = sourceValue;
    }
}
