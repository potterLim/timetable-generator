using System;

namespace TimetableGenerator.HandongCatalogGenerator.Domain;

internal sealed class CatalogCourse
{
    public CourseCode Code { get; }

    public KoreanCourseName KoreanName { get; }

    public EnglishCourseName EnglishName { get; }

    public CourseCredits Credits { get; }

    public SourceRecordNumber FirstSourceRecordNumber { get; }

    public CatalogCourse(
        CourseCode code,
        KoreanCourseName koreanName,
        EnglishCourseName englishName,
        CourseCredits credits,
        SourceRecordNumber firstSourceRecordNumber)
    {
        if (code == null)
        {
            throw new ArgumentNullException(nameof(code));
        }

        if (koreanName == null)
        {
            throw new ArgumentNullException(nameof(koreanName));
        }

        if (englishName == null)
        {
            throw new ArgumentNullException(nameof(englishName));
        }

        if (firstSourceRecordNumber.Value <= 0)
        {
            throw new ArgumentException(
                "Catalog courses require a valid source record number.",
                nameof(firstSourceRecordNumber));
        }

        Code = code;
        KoreanName = koreanName;
        EnglishName = englishName;
        Credits = credits;
        FirstSourceRecordNumber = firstSourceRecordNumber;
    }
}
