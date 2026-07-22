using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

using TimetableGenerator.Application.Planning;
using TimetableGenerator.Desktop.Presentation.Catalog;
using TimetableGenerator.Domain.Catalogs;
using TimetableGenerator.Domain.Planning;
using TimetableGenerator.Domain.Scheduling;

namespace TimetableGenerator.Desktop.Presentation.Models;

internal sealed class CourseSearchItem : ObservableObject
{
    private readonly ObservableCollection<CourseSelectionOption> mSelectionOptions;

    private CourseSelectionOption mSelectedSelectionOption;

    private bool mIsAdded;

    private ECourseSelectionAction mCourseSelectionAction;

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

    public string CourseBrowserMetadataDisplayText
    {
        get
        {
            if (HasSingleOfferingDetails)
            {
                return InstructorCreditDisplayText + " · "
                    + SelectedSelectionOption.EnglishInstructionDisplayText;
            }

            return InstructorCreditDisplayText;
        }
    }

    public string EnglishInstructionAccessibleText
    {
        get
        {
            return SelectedSelectionOption.EnglishInstructionAccessibleText;
        }
    }

    public string CourseBrowserAccessibleName
    {
        get
        {
            string accessibleName = Code + ", " + Name + ", "
                + InstructorDisplayText + ", " + CreditDisplayText;
            if (HasSingleOfferingDetails)
            {
                return accessibleName + ", " + EnglishInstructionAccessibleText;
            }

            return accessibleName;
        }
    }

    public string SingleOfferingDetailsDisplayText { get; }

    public bool HasSingleOfferingDetails
    {
        get
        {
            return Projection.Offerings.Count == 1;
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
                raisePropertyChanged(nameof(EnglishInstructionAccessibleText));
                raisePropertyChanged(nameof(AddButtonAccessibleName));
                raisePropertyChanged(nameof(AddButtonHelpText));
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

    public string SelectionAccessibleName
    {
        get
        {
            return Name + ", 추가할 분반 선택";
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

    public bool IsDirectAddButtonVisible
    {
        get
        {
            return IsAdded == false;
        }
    }

    public bool IsSelectionButtonVisible
    {
        get
        {
            return false;
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
                return Name + "은 현재 시간표에 추가되어 있습니다.";
            }

            if (Projection.Offerings.Count > 1)
            {
                return Name + " 수강 선택 설정 열기";
            }

            if (SelectedSelectionOption.IsDirectAdd)
            {
                return Name + ", " + SelectedSelectionOption.AccessibleName
                    + ", 현재 시간표에 추가";
            }

            if (ScheduledOfferingCount > 1)
            {
                return Name + "의 분반 선호 설정 열기";
            }

            return Name + "을 현재 시간표에 추가";
        }
    }

    public string AddButtonHelpText
    {
        get
        {
            if (Projection.Offerings.Count > 1)
            {
                return "분반별 선호를 설정합니다.";
            }

            if (SelectedSelectionOption.IsDirectAdd)
            {
                return "선택한 분반: " + SelectedSelectionOption.AccessibleName;
            }

            return "분반별 선호를 설정합니다.";
        }
    }

    public string AddButtonToolTipText
    {
        get
        {
            if (Projection.Offerings.Count > 1)
            {
                return "수강 선택 설정";
            }

            if (SelectedSelectionOption.IsDirectAdd)
            {
                return SelectedSelectionOption.DisplayName + " 추가";
            }

            if (ScheduledOfferingCount > 1)
            {
                return "분반 선호 설정";
            }

            return "시간표에 추가";
        }
    }

    public string SelectedCourseActionAccessibleName
    {
        get
        {
            return mCourseSelectionAction switch
            {
                ECourseSelectionAction.Remove => Name + "을 시간표에서 제거",
                ECourseSelectionAction.Edit => Name + " 수강 선택 수정",
                _ => Name + "은 현재 시간표에 추가되어 있지 않습니다.",
            };
        }
    }

    public string SelectedCourseActionToolTipText
    {
        get
        {
            return mCourseSelectionAction switch
            {
                ECourseSelectionAction.Remove => "시간표에서 제거",
                ECourseSelectionAction.Edit => "수강 선택 수정",
                _ => string.Empty,
            };
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

        mSelectionOptions = new ObservableCollection<CourseSelectionOption>(selectionOptions);
        mSelectedSelectionOption = mSelectionOptions[0];
        mCourseSelectionAction = ECourseSelectionAction.None;
        InstructorDisplayText = createInstructorSummary(projection);
        SingleOfferingDetailsDisplayText = createSingleOfferingDetails(projection);
    }

    public CourseSearchMatch? FindSearchMatchOrNull(CourseSearchQuery query)
    {
        if (query == null)
        {
            throw new ArgumentNullException(nameof(query));
        }

        if (query.IsEmpty)
        {
            return null;
        }

        if (query.IsExactMatch(Code))
        {
            return new CourseSearchMatch(this, ECourseSearchMatchKind.ExactCourseCode);
        }

        if (query.IsPrefixMatch(Code))
        {
            return new CourseSearchMatch(this, ECourseSearchMatchKind.CourseCodePrefix);
        }

        if (query.IsExactMatch(Name) || query.IsExactMatch(EnglishName))
        {
            return new CourseSearchMatch(this, ECourseSearchMatchKind.ExactCourseTitle);
        }

        if (query.IsPrefixMatch(Name) || query.IsPrefixMatch(EnglishName))
        {
            return new CourseSearchMatch(this, ECourseSearchMatchKind.CourseTitlePrefix);
        }

        if (query.IsContainedIn(Name) || query.IsContainedIn(EnglishName))
        {
            return new CourseSearchMatch(this, ECourseSearchMatchKind.CourseTitleContains);
        }

        foreach (CatalogOfferingProjection offering in Projection.Offerings)
        {
            if (query.IsContainedIn(offering.InstructorSummary))
            {
                return new CourseSearchMatch(this, ECourseSearchMatchKind.InstructorContains);
            }
        }

        return null;
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

        CourseSelectionOption? matchingOptionOrNull = findSelectionOptionOrNull(selectionOrNull);
        if (matchingOptionOrNull == null)
        {
            matchingOptionOrNull = createPersistedSelectionOption(selectionOrNull);
            mSelectionOptions.Add(matchingOptionOrNull);
        }

        SelectedSelectionOption = matchingOptionOrNull;
        markAdded();
    }

    public void SynchronizeCourseChoiceGroup(CourseChoiceGroup courseChoiceGroup)
    {
        if (courseChoiceGroup == null)
        {
            throw new ArgumentNullException(nameof(courseChoiceGroup));
        }

        foreach (CourseCandidate courseCandidate
            in courseChoiceGroup.CourseCandidates)
        {
            if (courseCandidate.CourseId == CourseId)
            {
                markAdded();
                return;
            }
        }

        throw new ArgumentException(
            "The synchronized course choice group must contain this course.",
            nameof(courseChoiceGroup));
    }

    public void SynchronizeSelectedAction(ECourseSelectionAction selectionAction)
    {
        if (Enum.IsDefined(typeof(ECourseSelectionAction), selectionAction) == false)
        {
            throw new ArgumentOutOfRangeException(nameof(selectionAction));
        }

        bool isActionExpected = IsAdded;
        bool hasAction = selectionAction != ECourseSelectionAction.None;
        if (isActionExpected != hasAction)
        {
            throw new ArgumentException(
                "Selected courses require an available selected-course action.",
                nameof(selectionAction));
        }

        if (mCourseSelectionAction != selectionAction)
        {
            mCourseSelectionAction = selectionAction;
            raisePropertyChanged(nameof(SelectedCourseActionAccessibleName));
            raisePropertyChanged(nameof(SelectedCourseActionToolTipText));
        }
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
            if (projection.ScheduledOfferingIds.Count == 1)
            {
                CatalogOfferingProjection scheduledOffering = findOffering(
                    projection,
                    projection.ScheduledOfferingIds[0]);
                string scheduledDisplayName =
                    scheduledOffering.Offering.SectionCode.Value
                    + "분반 · " + scheduledOffering.ScheduleSummary;
                options.Add(CourseSelectionOption.CreateDirectAdd(
                    scheduledSelection,
                    EMeetingScheduleStatus.Scheduled,
                    scheduledDisplayName,
                    scheduledOffering.EnglishInstructionPercentage));
            }
            else
            {
                string scheduledDisplayName = "시간이 정해진 "
                    + projection.ScheduledOfferingIds.Count
                    + "개 분반의 선호 설정";
                options.Add(CourseSelectionOption.CreatePreferenceEditor(scheduledSelection, scheduledDisplayName));
            }
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
            options.Add(CourseSelectionOption.CreateDirectAdd(
                selection,
                EMeetingScheduleStatus.NotProvided,
                displayName,
                offering.EnglishInstructionPercentage));
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

    private static string createSingleOfferingDetails(CatalogCourseProjection projection)
    {
        if (projection.Offerings.Count != 1)
        {
            return string.Empty;
        }

        CatalogOfferingProjection offering = projection.Offerings[0];
        return offering.ScheduleSummary + " · " + offering.LocationSummary;
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

    private CourseSelectionOption? findSelectionOptionOrNull(PlanningCourseSelection selection)
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

    private CourseSelectionOption createPersistedSelectionOption(PlanningCourseSelection selection)
    {
        if (selection.Kind == EPlanningCourseSelectionKind.ScheduledAlternatives)
        {
            IReadOnlyList<OfferingId> offeringIds = selection.GetScheduledOfferingIds();
            if (offeringIds.Count == 1)
            {
                CatalogOfferingProjection offering = findOffering(Projection, offeringIds[0]);
                return CourseSelectionOption.CreateDirectAdd(
                    selection,
                    EMeetingScheduleStatus.Scheduled,
                    offering.Offering.SectionCode.Value
                        + "분반 · 저장된 분반 선택",
                    offering.EnglishInstructionPercentage);
            }

            return CourseSelectionOption.CreatePreferenceEditor(
                selection,
                "저장된 " + offeringIds.Count + "개 분반에서 자동 선택");
        }

        if (selection.Kind == EPlanningCourseSelectionKind.TimeNotProvidedOffering)
        {
            CatalogOfferingProjection offering = findOffering(
                Projection,
                selection.GetTimeNotProvidedOfferingId());
            return CourseSelectionOption.CreateDirectAdd(
                selection,
                EMeetingScheduleStatus.NotProvided,
                offering.Offering.SectionCode.Value
                    + "분반 · 저장된 시간 미정 선택",
                offering.EnglishInstructionPercentage);
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
            raisePropertyChanged(nameof(IsDirectAddButtonVisible));
            raisePropertyChanged(nameof(IsSelectionButtonVisible));
            raisePropertyChanged(nameof(IsSelectionEnabled));
        }
    }
}
