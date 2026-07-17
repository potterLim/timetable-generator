using System;

using TimetableGenerator.Desktop.Presentation.Catalog;
using TimetableGenerator.Domain.Catalogs;

namespace TimetableGenerator.Desktop.Presentation.Models;

internal sealed class CourseChoiceAlternativeSearchItem
{
    public CatalogCourseProjection Projection { get; }

    public CourseId CourseId
    {
        get
        {
            return Projection.Course.Id;
        }
    }

    public string Code
    {
        get
        {
            return Projection.Course.Code.Value;
        }
    }

    public string Name
    {
        get
        {
            return Projection.Course.KoreanName.Value;
        }
    }

    public string DetailText
    {
        get
        {
            return Projection.ScheduledOfferingIds.Count
                + "개 분반 · "
                + Projection.Course.Credits
                + "학점";
        }
    }

    public string AddButtonAccessibleName
    {
        get
        {
            return Name + "을 대안 과목으로 추가";
        }
    }

    public CourseChoiceAlternativeSearchItem(CatalogCourseProjection projection)
    {
        if (projection == null)
        {
            throw new ArgumentNullException(nameof(projection));
        }

        if (projection.ScheduledOfferingIds.Count == 0)
        {
            throw new ArgumentException(
                "Alternative course search items require scheduled offerings.",
                nameof(projection));
        }

        Projection = projection;
    }
}
