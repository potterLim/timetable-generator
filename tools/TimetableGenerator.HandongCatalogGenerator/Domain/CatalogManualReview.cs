using System;

namespace TimetableGenerator.HandongCatalogGenerator.Domain;

internal sealed class CatalogManualReview
{
    public CourseCode CourseCode { get; }

    public EManualReviewField Field { get; }

    public EManualReviewReason Reason { get; }

    public ManualReviewSourceValue SourceValue { get; }

    public CatalogManualReview(
        CourseCode courseCode,
        EManualReviewField field,
        EManualReviewReason reason,
        ManualReviewSourceValue sourceValue)
    {
        if (courseCode == null)
        {
            throw new ArgumentNullException(nameof(courseCode));
        }

        if (Enum.IsDefined(typeof(EManualReviewField), field) == false)
        {
            throw new ArgumentOutOfRangeException(nameof(field));
        }

        if (Enum.IsDefined(typeof(EManualReviewReason), reason) == false)
        {
            throw new ArgumentOutOfRangeException(nameof(reason));
        }

        if (sourceValue == null)
        {
            throw new ArgumentNullException(nameof(sourceValue));
        }

        CourseCode = courseCode;
        Field = field;
        Reason = reason;
        SourceValue = sourceValue;
    }
}
