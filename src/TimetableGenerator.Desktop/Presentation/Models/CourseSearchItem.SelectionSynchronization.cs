using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

using TimetableGenerator.Application.Planning;
using TimetableGenerator.Desktop.Presentation.Catalog;
using TimetableGenerator.Domain.Catalogs;
using TimetableGenerator.Domain.Planning;
using TimetableGenerator.Domain.Scheduling;

namespace TimetableGenerator.Desktop.Presentation.Models;

internal sealed partial class CourseSearchItem
{
    private readonly ObservableCollection<CourseSelectionOption> mSelectionOptions;

    private CourseSelectionOption mSelectedSelectionOption;

    private bool mIsAdded;

    private ECourseSelectionAction mCourseSelectionAction;

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
                throw new ArgumentException("The selected course option must belong to this course.", nameof(value));
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

    public bool IsAdded
    {
        get
        {
            return mIsAdded;
        }
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
            throw new ArgumentException("The synchronized selection must belong to this course.", nameof(selectionOrNull));
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

        foreach (CourseCandidate courseCandidate in courseChoiceGroup.CourseCandidates)
        {
            if (courseCandidate.CourseId == CourseId)
            {
                markAdded();
                return;
            }
        }

        throw new ArgumentException("The synchronized course choice group must contain this course.", nameof(courseChoiceGroup));
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
            throw new ArgumentException("Selected courses require an available selected-course action.", nameof(selectionAction));
        }

        if (mCourseSelectionAction != selectionAction)
        {
            mCourseSelectionAction = selectionAction;
            raisePropertyChanged(nameof(SelectedCourseActionAccessibleName));
            raisePropertyChanged(nameof(SelectedCourseActionToolTipText));
        }
    }

    private static List<CourseSelectionOption> createSelectionOptions(CatalogCourseProjection projection)
    {
        List<CourseSelectionOption> options = new List<CourseSelectionOption>();
        if (projection.ScheduledOfferingIds.Count > 0)
        {
            PlanningCourseSelection scheduledSelection = PlanningCourseSelection.CreateScheduledAlternatives(projection.Course.Id, projection.ScheduledOfferingIds);
            if (projection.ScheduledOfferingIds.Count == 1)
            {
                CatalogOfferingProjection scheduledOffering = findOffering(projection, projection.ScheduledOfferingIds[0]);
                string scheduledDisplayName = scheduledOffering.Offering.SectionCode.Value + "분반 · " + scheduledOffering.ScheduleSummary;
                options.Add(CourseSelectionOption.CreateDirectAdd(scheduledSelection, EMeetingScheduleStatus.Scheduled, scheduledDisplayName, scheduledOffering.EnglishInstructionPercentage));
            }
            else
            {
                string scheduledDisplayName = "시간이 정해진 " + projection.ScheduledOfferingIds.Count + "개 분반의 선호 설정";
                options.Add(CourseSelectionOption.CreatePreferenceEditor(scheduledSelection, scheduledDisplayName));
            }
        }

        foreach (OfferingId offeringId in projection.TimeNotProvidedOfferingIds)
        {
            CatalogOfferingProjection offering = findOffering(projection, offeringId);
            PlanningCourseSelection selection = PlanningCourseSelection.CreateTimeNotProvidedOffering(projection.Course.Id, offeringId);
            string displayName = offering.Offering.SectionCode.Value + "분반 · 시간 미정 · " + offering.InstructorSummary;
            options.Add(CourseSelectionOption.CreateDirectAdd(selection, EMeetingScheduleStatus.NotProvided, displayName, offering.EnglishInstructionPercentage));
        }

        return options;
    }

    private static CatalogOfferingProjection findOffering(CatalogCourseProjection projection, OfferingId offeringId)
    {
        foreach (CatalogOfferingProjection offering in projection.Offerings)
        {
            if (offering.Offering.Id == offeringId)
            {
                return offering;
            }
        }

        throw new InvalidOperationException("A projected course did not contain one of its declared offering IDs.");
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
                return CourseSelectionOption.CreateDirectAdd(selection, EMeetingScheduleStatus.Scheduled, offering.Offering.SectionCode.Value + "분반 · 저장된 분반 선택", offering.EnglishInstructionPercentage);
            }

            return CourseSelectionOption.CreatePreferenceEditor(selection, "저장된 " + offeringIds.Count + "개 분반에서 자동 선택");
        }

        if (selection.Kind == EPlanningCourseSelectionKind.TimeNotProvidedOffering)
        {
            CatalogOfferingProjection offering = findOffering(Projection, selection.GetTimeNotProvidedOfferingId());
            return CourseSelectionOption.CreateDirectAdd(selection, EMeetingScheduleStatus.NotProvided, offering.Offering.SectionCode.Value + "분반 · 저장된 시간 미정 선택", offering.EnglishInstructionPercentage);
        }

        throw new ArgumentOutOfRangeException(nameof(selection), selection.Kind, "Unknown planning course selection kind.");
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
            raisePropertyChanged(nameof(IsSelectionEnabled));
        }
    }
}
