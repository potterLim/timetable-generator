using System;
using TimetableGenerator.HandongCatalogGenerator.Domain;

namespace TimetableGenerator.HandongCatalogGenerator.Handong.Normalization;

internal sealed class HandongOfferingNormalizationResult
{
    public CatalogCourse Course { get; }

    public CatalogOffering Offering { get; }

    public EEnglishScheduleComparison EnglishScheduleComparison { get; }

    public HandongOfferingNormalizationResult(CatalogCourse course, CatalogOffering offering, EEnglishScheduleComparison englishScheduleComparison)
    {
        if (course == null)
        {
            throw new ArgumentNullException(nameof(course));
        }

        if (offering == null)
        {
            throw new ArgumentNullException(nameof(offering));
        }

        if (Enum.IsDefined(typeof(EEnglishScheduleComparison), englishScheduleComparison) == false)
        {
            throw new ArgumentOutOfRangeException(nameof(englishScheduleComparison));
        }

        if (course.Code != offering.Key.CourseCode)
        {
            throw new ArgumentException("The normalized course and offering must use the same code.");
        }

        Course = course;
        Offering = offering;
        EnglishScheduleComparison = englishScheduleComparison;
    }
}
