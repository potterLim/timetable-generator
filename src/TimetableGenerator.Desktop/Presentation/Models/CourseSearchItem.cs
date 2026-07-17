using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text;

using TimetableGenerator.Application.Planning;
using TimetableGenerator.Desktop.Presentation.Catalog;
using TimetableGenerator.Domain.Catalogs;
using TimetableGenerator.Domain.Scheduling;

namespace TimetableGenerator.Desktop.Presentation.Models;

internal sealed class CourseSearchItem : ObservableObject
{
    private readonly ObservableCollection<CourseSelectionOption> mSelectionOptions;

    private readonly string mSearchIndex;

    private CourseSelectionOption mSelectedSelectionOption;

    private bool mIsAdded;

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

    public string InstructorDisplayText { get; }

    public CourseCredits Credits
    {
        get
        {
            return Projection.Course.Credits;
        }
    }

    public string CreditDisplayText
    {
        get
        {
            return Credits + "학점";
        }
    }

    public string InstructorCreditDisplayText
    {
        get
        {
            return InstructorDisplayText + " · " + CreditDisplayText;
        }
    }

    public string MeetingDisplayText { get; }

    public string LocationDisplayText { get; }

    public string MeetingLocationDisplayText
    {
        get
        {
            return MeetingDisplayText + " · " + LocationDisplayText;
        }
    }

    public ECourseAccent Accent
    {
        get
        {
            return Projection.Accent;
        }
    }

    public IReadOnlyList<CourseSelectionOption> SelectionOptions
    {
        get
        {
            return mSelectionOptions;
        }
    }

    public CourseSelectionOption SelectedSelectionOption
    {
        get
        {
            return mSelectedSelectionOption;
        }
        set
        {
            if (value == null)
            {
                throw new ArgumentNullException(nameof(value));
            }

            if (containsSelectionOption(value) == false)
            {
                throw new ArgumentException(
                    "The selected course option must belong to this course.",
                    nameof(value));
            }

            if (setProperty(ref mSelectedSelectionOption, value))
            {
                raisePropertyChanged(nameof(AddButtonAccessibleName));
                raisePropertyChanged(nameof(AddButtonToolTipText));
            }
        }
    }

    public bool HasMultipleSelectionOptions
    {
        get
        {
            return SelectionOptions.Count > 1;
        }
    }

    public bool IsSelectedOptionTimeNotProvided
    {
        get
        {
            return SelectedSelectionOption.IsTimeNotProvided;
        }
    }

    public int ScheduledOfferingCount
    {
        get
        {
            return Projection.ScheduledOfferingIds.Count;
        }
    }

    public bool IsBlue
    {
        get
        {
            return Accent == ECourseAccent.Blue;
        }
    }

    public bool IsPurple
    {
        get
        {
            return Accent == ECourseAccent.Purple;
        }
    }

    public bool IsGreen
    {
        get
        {
            return Accent == ECourseAccent.Green;
        }
    }

    public bool IsAdded
    {
        get
        {
            return mIsAdded;
        }
    }

    public bool IsSelectionEnabled
    {
        get
        {
            return IsAdded == false;
        }
    }

    public string AddButtonAccessibleName
    {
        get
        {
            if (IsAdded)
            {
                return Name + "은 현재 계획에 추가되어 있습니다.";
            }

            if (IsSelectedOptionTimeNotProvided)
            {
                return Name + "의 선택한 시간 미정 분반을 현재 계획에 추가";
            }

            if (ScheduledOfferingCount > 1)
            {
                return Name + "의 분반 선호 설정 열기";
            }

            return Name + "을 현재 계획에 추가";
        }
    }

    public string AddButtonToolTipText
    {
        get
        {
            if (IsSelectedOptionTimeNotProvided)
            {
                return "시간 미정 분반 추가";
            }

            if (ScheduledOfferingCount > 1)
            {
                return "분반 선호 설정";
            }

            return "계획에 추가";
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
            throw new ArgumentException(
                "Searchable courses require at least one selectable offering.",
                nameof(projection));
        }

        mSelectionOptions = new ObservableCollection<CourseSelectionOption>(
            selectionOptions);
        mSelectedSelectionOption = mSelectionOptions[0];
        InstructorDisplayText = createInstructorSummary(projection);
        MeetingDisplayText = createMeetingSummary(projection);
        LocationDisplayText = createLocationSummary(projection);
        mSearchIndex = createSearchIndex(projection);
    }

    public bool MatchesSearchText(string searchText)
    {
        if (searchText == null)
        {
            throw new ArgumentNullException(nameof(searchText));
        }

        if (string.IsNullOrWhiteSpace(searchText))
        {
            return true;
        }

        return mSearchIndex.Contains(
            searchText.Trim(),
            StringComparison.CurrentCultureIgnoreCase);
    }

    public PlanningCourseSelection CreateSelection()
    {
        return SelectedSelectionOption.Selection;
    }

    public void SynchronizeSelection(PlanningCourseSelection? selectionOrNull)
    {
        if (selectionOrNull == null)
        {
            markRemoved();
            return;
        }

        if (selectionOrNull.CourseId != CourseId)
        {
            throw new ArgumentException(
                "The synchronized selection must belong to this course.",
                nameof(selectionOrNull));
        }

        CourseSelectionOption? matchingOptionOrNull = findSelectionOptionOrNull(
            selectionOrNull);
        if (matchingOptionOrNull == null)
        {
            matchingOptionOrNull = createPersistedSelectionOption(selectionOrNull);
            mSelectionOptions.Add(matchingOptionOrNull);
        }

        SelectedSelectionOption = matchingOptionOrNull;
        markAdded();
    }

    private static List<CourseSelectionOption> createSelectionOptions(
        CatalogCourseProjection projection)
    {
        List<CourseSelectionOption> options = new List<CourseSelectionOption>();
        if (projection.ScheduledOfferingIds.Count > 0)
        {
            PlanningCourseSelection scheduledSelection =
                PlanningCourseSelection.CreateScheduledAlternatives(
                    projection.Course.Id,
                    projection.ScheduledOfferingIds);
            string scheduledDisplayName = "시간표가 있는 "
                + projection.ScheduledOfferingIds.Count
                + "개 분반의 선호 설정";
            options.Add(new CourseSelectionOption(
                scheduledSelection,
                EMeetingScheduleStatus.Scheduled,
                scheduledDisplayName));
        }

        foreach (OfferingId offeringId in projection.TimeNotProvidedOfferingIds)
        {
            CatalogOfferingProjection offering = findOffering(projection, offeringId);
            PlanningCourseSelection selection =
                PlanningCourseSelection.CreateTimeNotProvidedOffering(
                    projection.Course.Id,
                    offeringId);
            string displayName = offering.Offering.SectionCode.Value
                + "분반 · 시간 미정 · "
                + offering.InstructorSummary;
            options.Add(new CourseSelectionOption(
                selection,
                EMeetingScheduleStatus.NotProvided,
                displayName));
        }

        return options;
    }

    private static CatalogOfferingProjection findOffering(
        CatalogCourseProjection projection,
        OfferingId offeringId)
    {
        foreach (CatalogOfferingProjection offering in projection.Offerings)
        {
            if (offering.Offering.Id == offeringId)
            {
                return offering;
            }
        }

        throw new InvalidOperationException(
            "A projected course did not contain one of its declared offering IDs.");
    }

    private static string createInstructorSummary(CatalogCourseProjection projection)
    {
        if (projection.Offerings.Count == 1)
        {
            return projection.Offerings[0].InstructorSummary;
        }

        return projection.Offerings.Count + "개 분반";
    }

    private static string createMeetingSummary(CatalogCourseProjection projection)
    {
        int scheduledCount = projection.ScheduledOfferingIds.Count;
        int timeNotProvidedCount = projection.TimeNotProvidedOfferingIds.Count;
        if (projection.Offerings.Count == 1)
        {
            return projection.Offerings[0].ScheduleSummary;
        }

        if (scheduledCount > 0 && timeNotProvidedCount > 0)
        {
            return scheduledCount
                + "개 시간표 분반 · 시간 미정 "
                + timeNotProvidedCount
                + "개";
        }

        if (scheduledCount > 0)
        {
            return scheduledCount + "개 분반 · 선호할 분반을 직접 설정";
        }

        return timeNotProvidedCount + "개 시간 미정 분반 중 직접 선택";
    }

    private static string createLocationSummary(CatalogCourseProjection projection)
    {
        if (projection.Offerings.Count == 1)
        {
            return projection.Offerings[0].LocationSummary;
        }

        return "분반별 강의실";
    }

    private static string createSearchIndex(CatalogCourseProjection projection)
    {
        StringBuilder searchIndex = new StringBuilder();
        searchIndex.Append(projection.Course.Code.Value);
        searchIndex.Append(' ');
        searchIndex.Append(projection.Course.KoreanName.Value);
        searchIndex.Append(' ');
        searchIndex.Append(projection.Course.EnglishName.Value);
        foreach (CatalogOfferingProjection offering in projection.Offerings)
        {
            searchIndex.Append(' ');
            searchIndex.Append(offering.InstructorSummary);
            searchIndex.Append(' ');
            searchIndex.Append(offering.Metadata.Classification.OfferingUnitName.Value);
            searchIndex.Append(' ');
            searchIndex.Append(offering.Offering.SectionCode.Value);
        }

        return searchIndex.ToString();
    }

    private bool containsSelectionOption(CourseSelectionOption option)
    {
        foreach (CourseSelectionOption candidate in SelectionOptions)
        {
            if (ReferenceEquals(candidate, option))
            {
                return true;
            }
        }

        return false;
    }

    private CourseSelectionOption? findSelectionOptionOrNull(
        PlanningCourseSelection selection)
    {
        foreach (CourseSelectionOption option in SelectionOptions)
        {
            if (option.Represents(selection))
            {
                return option;
            }
        }

        return null;
    }

    private CourseSelectionOption createPersistedSelectionOption(
        PlanningCourseSelection selection)
    {
        if (selection.Kind == EPlanningCourseSelectionKind.ScheduledAlternatives)
        {
            int offeringCount = selection.GetScheduledOfferingIds().Count;
            return new CourseSelectionOption(
                selection,
                EMeetingScheduleStatus.Scheduled,
                "저장된 " + offeringCount + "개 분반에서 자동 선택");
        }

        if (selection.Kind == EPlanningCourseSelectionKind.TimeNotProvidedOffering)
        {
            CatalogOfferingProjection offering = findOffering(
                Projection,
                selection.GetTimeNotProvidedOfferingId());
            return new CourseSelectionOption(
                selection,
                EMeetingScheduleStatus.NotProvided,
                offering.Offering.SectionCode.Value
                    + "분반 · 저장된 시간 미정 선택");
        }

        throw new ArgumentOutOfRangeException(
            nameof(selection),
            selection.Kind,
            "Unknown planning course selection kind.");
    }

    private void markAdded()
    {
        setSelectionState(ESelectionState.Selected);
    }

    private void markRemoved()
    {
        setSelectionState(ESelectionState.NotSelected);
    }

    private void setSelectionState(ESelectionState selectionState)
    {
        bool isAdded = selectionState == ESelectionState.Selected;
        if (setProperty(ref mIsAdded, isAdded, nameof(IsAdded)))
        {
            raisePropertyChanged(nameof(AddButtonAccessibleName));
            raisePropertyChanged(nameof(AddButtonToolTipText));
            raisePropertyChanged(nameof(IsSelectionEnabled));
        }
    }
}
