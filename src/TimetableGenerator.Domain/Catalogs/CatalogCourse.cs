using System;

namespace TimetableGenerator.Domain.Catalogs;

public sealed class CatalogCourse
{
    public CourseId Id { get; }

    public CourseCode Code { get; }

    public KoreanCourseName KoreanName { get; }

    public EnglishCourseName EnglishName { get; }

    public CourseCredits Credits { get; }

    public CatalogCourse(
        CourseId id,
        CourseCode code,
        KoreanCourseName koreanName,
        EnglishCourseName englishName,
        CourseCredits credits)
    {
        if (id == null)
        {
            throw new ArgumentNullException(nameof(id));
        }

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

        if (credits.IsValid == false)
        {
            throw new ArgumentException("Catalog courses require valid course credits.", nameof(credits));
        }

        Id = id;
        Code = code;
        KoreanName = koreanName;
        EnglishName = englishName;
        Credits = credits;
    }
}
