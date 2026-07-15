using System;
using TimetableGenerator.HandongCatalogGenerator.Domain;

namespace TimetableGenerator.HandongCatalogGenerator.Handong.Normalization;

internal sealed class HandongCourseNameNormalizationResult
{
    public KoreanCourseName KoreanName { get; }

    public EnglishCourseName EnglishName { get; }

    public HandongCourseNameNormalizationResult(
        KoreanCourseName koreanName,
        EnglishCourseName englishName)
    {
        if (koreanName == null)
        {
            throw new ArgumentNullException(nameof(koreanName));
        }

        if (englishName == null)
        {
            throw new ArgumentNullException(nameof(englishName));
        }

        KoreanName = koreanName;
        EnglishName = englishName;
    }
}
