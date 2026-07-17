using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows.Input;

using TimetableGenerator.Desktop.Presentation.Catalog;
using TimetableGenerator.Desktop.Presentation.Models;
using TimetableGenerator.Domain.Catalogs;
using TimetableGenerator.Domain.Planning;

namespace TimetableGenerator.Desktop.Presentation.ViewModels;

internal sealed partial class PlannerWorkspaceViewModel
{
    private const int MAXIMUM_ALTERNATIVE_SEARCH_RESULT_COUNT = 8;

    private readonly DelegateCommand mSaveCourseChoiceCommand;

    private CourseChoiceGroupId? mEditingCourseChoiceGroupIdOrNull;

    private bool mIsCourseChoiceEditorVisible;

    private string mAlternativeCourseSearchText;

    public ObservableCollection<CourseChoiceDraftCourseItem> CourseChoiceDraftCourses
    {
        get;
    }

    public ObservableCollection<CourseChoiceAlternativeSearchItem>
        AlternativeCourseSearchResults
    {
        get;
    }

    public bool IsCourseChoiceEditorVisible
    {
        get
        {
            return mIsCourseChoiceEditorVisible;
        }
    }

    public string CourseChoiceEditorTitle
    {
        get
        {
            return mEditingCourseChoiceGroupIdOrNull.HasValue
                ? "수강 선택 수정"
                : "수강 선택 설정";
        }
    }

    public string CourseChoiceEditorDescription
    {
        get
        {
            if (CourseChoiceDraftCourses.Count > 1)
            {
                return "과목별 분반을 정하면 이 중 한 과목만 추천합니다.";
            }

            return "선호 분반을 우선하고, 가능 분반은 대안으로 사용합니다.";
        }
    }

    public string AlternativeCourseSearchText
    {
        get
        {
            return mAlternativeCourseSearchText;
        }
        set
        {
            string normalizedValue = value;
            if (normalizedValue == null)
            {
                normalizedValue = string.Empty;
            }

            if (setProperty(ref mAlternativeCourseSearchText, normalizedValue))
            {
                refreshAlternativeCourseSearchResults();
            }
        }
    }

    public bool HasAlternativeCourseSearchResults
    {
        get
        {
            return AlternativeCourseSearchResults.Count > 0;
        }
    }

    public bool HasAlternativeCourseSearchText
    {
        get
        {
            return string.IsNullOrWhiteSpace(AlternativeCourseSearchText) == false;
        }
    }

    public bool HasNoAlternativeCourseSearchResults
    {
        get
        {
            return HasAlternativeCourseSearchText
                && HasAlternativeCourseSearchResults == false;
        }
    }

    public bool CanSaveCourseChoice
    {
        get
        {
            if (CourseChoiceDraftCourses.Count == 0)
            {
                return false;
            }

            foreach (CourseChoiceDraftCourseItem course in CourseChoiceDraftCourses)
            {
                if (course.HasEligibleOffering == false)
                {
                    return false;
                }
            }

            return true;
        }
    }

    public bool HasIncompleteCourseChoice
    {
        get
        {
            return CanSaveCourseChoice == false;
        }
    }

    public string CourseChoiceValidationMessage
    {
        get
        {
            return "과목마다 선호 또는 가능 분반을 하나 이상 선택하세요.";
        }
    }

    public ICommand BeginEditCourseChoiceGroupCommand { get; }

    public ICommand RemoveCourseChoiceGroupCommand { get; }

    public ICommand RemoveCourseChoiceDraftCourseCommand { get; }

    public ICommand AddAlternativeCourseCommand { get; }

    public ICommand SaveCourseChoiceCommand
    {
        get
        {
            return mSaveCourseChoiceCommand;
        }
    }

    public ICommand CancelCourseChoiceEditCommand { get; }

    private void addScheduledCourse(CourseSearchItem course)
    {
        if (course.ScheduledOfferingCount == 1)
        {
            CourseChoiceGroup courseChoiceGroup = createSingleOfferingGroup(course);
            mSession.AddCourseChoiceGroup(courseChoiceGroup);
            afterPlanContentMutation();
            return;
        }

        beginAddCourseChoice(course);
    }

    private void beginAddCourseChoice(CourseSearchItem course)
    {
        closePersonalScheduleEditingState();
        closePlanEditingState();
        prepareCourseChoiceDraft(null);
        addDraftCourse(course.Projection, null);
        openCourseChoiceEditor();
    }

    private void beginEditCourseChoiceGroup(PlanCourseChoiceGroupItem groupItem)
    {
        throwIfDisposed();
        if (groupItem == null)
        {
            throw new ArgumentNullException(nameof(groupItem));
        }

        CourseChoiceGroup group = findActiveCourseChoiceGroup(groupItem.GroupId);
        closePersonalScheduleEditingState();
        closePlanEditingState();
        prepareCourseChoiceDraft(group.Id);
        foreach (CourseCandidate courseCandidate in group.CourseCandidates)
        {
            CatalogCourseProjection projection = mCatalogProjection.FindCourseById(
                courseCandidate.CourseId);
            addDraftCourse(projection, courseCandidate.OfferingCandidates);
        }

        openCourseChoiceEditor();
    }

    private void removeCourseChoiceGroup(PlanCourseChoiceGroupItem groupItem)
    {
        throwIfDisposed();
        if (groupItem == null)
        {
            throw new ArgumentNullException(nameof(groupItem));
        }

        findActiveCourseChoiceGroup(groupItem.GroupId);
        mSession.RemoveCourseChoiceGroup(groupItem.GroupId);
        afterPlanContentMutation();
    }

    private void removeCourseChoiceDraftCourse(CourseChoiceDraftCourseItem course)
    {
        if (course == null)
        {
            throw new ArgumentNullException(nameof(course));
        }

        if (CourseChoiceDraftCourses.Count <= 1)
        {
            return;
        }

        bool hasRemovedCourse = CourseChoiceDraftCourses.Remove(course);
        if (hasRemovedCourse == false)
        {
            throw new ArgumentException(
                "The removed draft course must belong to the active editor.",
                nameof(course));
        }

        course.DraftChanged -= onCourseChoiceDraftChanged;
        updateCourseChoiceDraftState();
    }

    private void addAlternativeCourse(CourseChoiceAlternativeSearchItem course)
    {
        if (course == null)
        {
            throw new ArgumentNullException(nameof(course));
        }

        if (containsDraftCourse(course.CourseId)
            || isCourseSelectedOutsideEditedGroup(course.CourseId))
        {
            return;
        }

        addDraftCourse(course.Projection, null);
        AlternativeCourseSearchText = string.Empty;
        updateCourseChoiceDraftState();
    }

    private void saveCourseChoice()
    {
        throwIfDisposed();
        if (CanSaveCourseChoice == false)
        {
            return;
        }

        List<CourseCandidate> courseCandidates = new List<CourseCandidate>();
        foreach (CourseChoiceDraftCourseItem draftCourse in CourseChoiceDraftCourses)
        {
            courseCandidates.Add(draftCourse.CreateCandidate());
        }

        CourseChoiceGroupId groupId = mEditingCourseChoiceGroupIdOrNull.HasValue
            ? mEditingCourseChoiceGroupIdOrNull.Value
            : CourseChoiceGroupId.CreateNew();
        CourseChoiceGroup group = new CourseChoiceGroup(
            groupId,
            ECourseChoiceCardinality.ExactlyOne,
            courseCandidates);
        if (mEditingCourseChoiceGroupIdOrNull.HasValue)
        {
            mSession.UpdateCourseChoiceGroup(group);
        }
        else
        {
            mSession.AddCourseChoiceGroup(group);
        }

        closeCourseChoiceEditingState();
        afterPlanContentMutation();
    }

    private void cancelCourseChoiceEdit()
    {
        closeCourseChoiceEditingState();
    }

    private static CourseChoiceGroup createSingleOfferingGroup(CourseSearchItem course)
    {
        OfferingId offeringId = course.Projection.ScheduledOfferingIds[0];
        CourseCandidate courseCandidate = new CourseCandidate(
            course.CourseId,
            new OfferingCandidate[]
            {
                new OfferingCandidate(
                    offeringId,
                    EOfferingPreference.Acceptable),
            });
        return new CourseChoiceGroup(
            CourseChoiceGroupId.CreateNew(),
            ECourseChoiceCardinality.ExactlyOne,
            new CourseCandidate[] { courseCandidate });
    }

    private void prepareCourseChoiceDraft(
        CourseChoiceGroupId? editingCourseChoiceGroupIdOrNull)
    {
        foreach (CourseChoiceDraftCourseItem course in CourseChoiceDraftCourses)
        {
            course.DraftChanged -= onCourseChoiceDraftChanged;
        }

        CourseChoiceDraftCourses.Clear();
        AlternativeCourseSearchResults.Clear();
        mEditingCourseChoiceGroupIdOrNull = editingCourseChoiceGroupIdOrNull;
        mAlternativeCourseSearchText = string.Empty;
        raisePropertyChanged(nameof(AlternativeCourseSearchText));
        raisePropertyChanged(nameof(CourseChoiceEditorTitle));
    }

    private void addDraftCourse(
        CatalogCourseProjection projection,
        IEnumerable<OfferingCandidate>? savedCandidatesOrNull)
    {
        CourseChoiceDraftCourseItem draftCourse = new CourseChoiceDraftCourseItem(
            projection,
            savedCandidatesOrNull);
        draftCourse.DraftChanged += onCourseChoiceDraftChanged;
        CourseChoiceDraftCourses.Add(draftCourse);
        updateCourseChoiceDraftState();
    }

    private void openCourseChoiceEditor()
    {
        mIsCourseChoiceEditorVisible = true;
        raiseCourseChoiceEditorStateChanged();
    }

    private void closeCourseChoiceEditingState()
    {
        if (mIsCourseChoiceEditorVisible == false)
        {
            return;
        }

        mIsCourseChoiceEditorVisible = false;
        raiseCourseChoiceEditorStateChanged();
    }

    private void updateCourseChoiceDraftState()
    {
        bool canRemoveCourse = CourseChoiceDraftCourses.Count > 1;
        foreach (CourseChoiceDraftCourseItem course in CourseChoiceDraftCourses)
        {
            course.SetCanRemove(canRemoveCourse);
        }

        raisePropertyChanged(nameof(CourseChoiceEditorDescription));
        raisePropertyChanged(nameof(CanSaveCourseChoice));
        raisePropertyChanged(nameof(HasIncompleteCourseChoice));
        mSaveCourseChoiceCommand.NotifyCanExecuteChanged();
        refreshAlternativeCourseSearchResults();
    }

    private void onCourseChoiceDraftChanged(
        object? senderOrNull,
        EventArgs eventArgs)
    {
        updateCourseChoiceDraftState();
    }

    private void refreshAlternativeCourseSearchResults()
    {
        AlternativeCourseSearchResults.Clear();
        if (string.IsNullOrWhiteSpace(AlternativeCourseSearchText))
        {
            raiseAlternativeSearchStateChanged();
            return;
        }

        foreach (CourseSearchItem course in mAllCourses)
        {
            if (course.ScheduledOfferingCount == 0
                || containsDraftCourse(course.CourseId)
                || isCourseSelectedOutsideEditedGroup(course.CourseId)
                || course.MatchesSearchText(AlternativeCourseSearchText) == false)
            {
                continue;
            }

            AlternativeCourseSearchResults.Add(
                new CourseChoiceAlternativeSearchItem(course.Projection));
            if (AlternativeCourseSearchResults.Count
                >= MAXIMUM_ALTERNATIVE_SEARCH_RESULT_COUNT)
            {
                break;
            }
        }

        raiseAlternativeSearchStateChanged();
    }

    private bool containsDraftCourse(CourseId courseId)
    {
        foreach (CourseChoiceDraftCourseItem course in CourseChoiceDraftCourses)
        {
            if (course.CourseId == courseId)
            {
                return true;
            }
        }

        return false;
    }

    private bool isCourseSelectedOutsideEditedGroup(CourseId courseId)
    {
        foreach (CourseChoiceGroup group in ActivePlan.Plan.CourseChoiceGroups)
        {
            if (mEditingCourseChoiceGroupIdOrNull.HasValue
                && group.Id == mEditingCourseChoiceGroupIdOrNull.Value)
            {
                continue;
            }

            foreach (CourseCandidate courseCandidate in group.CourseCandidates)
            {
                if (courseCandidate.CourseId == courseId)
                {
                    return true;
                }
            }
        }

        foreach (UnscheduledOfferingSelection selection
            in ActivePlan.Plan.UnscheduledOfferingSelections)
        {
            if (selection.CourseId == courseId)
            {
                return true;
            }
        }

        return false;
    }

    private CourseChoiceGroup findActiveCourseChoiceGroup(
        CourseChoiceGroupId groupId)
    {
        foreach (CourseChoiceGroup group in ActivePlan.Plan.CourseChoiceGroups)
        {
            if (group.Id == groupId)
            {
                return group;
            }
        }

        throw new ArgumentException(
            "The course choice group must belong to the active plan.",
            nameof(groupId));
    }

    private void raiseAlternativeSearchStateChanged()
    {
        raisePropertyChanged(nameof(HasAlternativeCourseSearchResults));
        raisePropertyChanged(nameof(HasAlternativeCourseSearchText));
        raisePropertyChanged(nameof(HasNoAlternativeCourseSearchResults));
    }

    private void raiseCourseChoiceEditorStateChanged()
    {
        raisePropertyChanged(nameof(IsCourseChoiceEditorVisible));
        raisePropertyChanged(nameof(IsWorkspaceInteractionEnabled));
    }
}
