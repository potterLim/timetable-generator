using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

using TimetableGenerator.Desktop.Presentation.Catalog;
using TimetableGenerator.Domain.Catalogs;

namespace TimetableGenerator.Desktop.Presentation.Models;

internal sealed partial class CourseSearchItem : ObservableObject
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

    public string EnglishName
    {
        get
        {
            return Projection.Course.EnglishName.Value;
        }
    }

    public CourseCredits Credits
    {
        get
        {
            return Projection.Course.Credits;
        }
    }

    public CourseSearchItem(CatalogCourseProjection projection)
    {
        if (projection == null)
        {
            throw new ArgumentNullException(nameof(projection));
        }

        Projection = projection;
        List<CourseSelectionOption> selectionOptions = createSelectionOptions(projection);
        if (selectionOptions.Count == 0)
        {
            throw new ArgumentException("Searchable courses require at least one selectable offering.", nameof(projection));
        }

        mSelectionOptions = new ObservableCollection<CourseSelectionOption>(selectionOptions);
        mSelectedSelectionOption = mSelectionOptions[0];
        mCourseSelectionAction = ECourseSelectionAction.None;
        InstructorDisplayText = createInstructorSummary(projection);
        SingleOfferingDetailsDisplayText = createSingleOfferingDetails(projection);
    }
}
