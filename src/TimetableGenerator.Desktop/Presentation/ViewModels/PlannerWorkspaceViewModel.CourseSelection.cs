using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows.Input;

using TimetableGenerator.Application.Planning;
using TimetableGenerator.CatalogJson;
using TimetableGenerator.Desktop.Presentation.Catalog;
using TimetableGenerator.Desktop.Presentation.Collections;
using TimetableGenerator.Desktop.Presentation.Models;
using TimetableGenerator.Domain.Catalogs;
using TimetableGenerator.Domain.Planning;

namespace TimetableGenerator.Desktop.Presentation.ViewModels;

internal sealed partial class PlannerWorkspaceViewModel
{
    private readonly IReadOnlyList<CourseSearchItem> mAllCourses;

    private DelegateCommand? mResetCourseSearchCommandOrNull;

    private ParameterizedCommand<CourseSelectionOption>?
        mAddCourseSelectionOptionCommandOrNull;

    private ParameterizedCommand<CourseSearchItem>?
        mEditOrRemoveSelectedCourseCommandOrNull;

    private string mSearchText;

    private CourseUnitFilterOption mSelectedUnitFilter;

    private RequirementFilterOption mSelectedRequirementFilter;

    public ObservableCollection<CourseSearchItem> VisibleCourses { get; }

    public ObservableCollection<CourseUnitFilterOption> UnitFilters { get; }

    public ObservableCollection<RequirementFilterOption> RequirementFilters { get; }

    public string SearchText
    {
        get
        {
            return mSearchText;
        }
        set
        {
            string normalizedValue = value;
            if (normalizedValue == null)
            {
                normalizedValue = string.Empty;
            }

            if (setProperty(ref mSearchText, normalizedValue))
            {
                refreshVisibleCourses();
            }
        }
    }

    public CourseUnitFilterOption SelectedUnitFilter
    {
        get
        {
            return mSelectedUnitFilter;
        }
        set
        {
            if (value == null)
            {
                throw new ArgumentNullException(nameof(value));
            }

            if (setProperty(ref mSelectedUnitFilter, value))
            {
                refreshVisibleCourses();
            }
        }
    }

    public RequirementFilterOption SelectedRequirementFilter
    {
        get
        {
            return mSelectedRequirementFilter;
        }
        set
        {
            if (value == null)
            {
                throw new ArgumentNullException(nameof(value));
            }

            if (setProperty(ref mSelectedRequirementFilter, value))
            {
                refreshVisibleCourses();
            }
        }
    }

    public string VisibleCourseHeading
    {
        get
        {
            bool hasDefaultFilters = SelectedUnitFilter.Scope == ECourseFilterScope.All
                && SelectedRequirementFilter.Scope == ECourseFilterScope.All;
            if (string.IsNullOrWhiteSpace(SearchText) && hasDefaultFilters)
            {
                return "전체 과목 (" + mAllCourses.Count + "개)";
            }

            return "검색 결과 (" + VisibleCourses.Count + "개)";
        }
    }

    public ECourseSearchResultState CourseSearchResultState
    {
        get
        {
            return VisibleCourses.Count == 0
                ? ECourseSearchResultState.Empty
                : ECourseSearchResultState.Populated;
        }
    }

    public bool HasVisibleCourses
    {
        get
        {
            return CourseSearchResultState == ECourseSearchResultState.Populated;
        }
    }

    public bool HasNoVisibleCourses
    {
        get
        {
            return CourseSearchResultState == ECourseSearchResultState.Empty;
        }
    }

    public ICommand AddCourseCommand { get; }

    public ICommand AddCourseSelectionOptionCommand
    {
        get
        {
            if (mAddCourseSelectionOptionCommandOrNull == null)
            {
                mAddCourseSelectionOptionCommandOrNull =
                    new ParameterizedCommand<CourseSelectionOption>(
                        addCourseSelectionOption);
            }

            return mAddCourseSelectionOptionCommandOrNull;
        }
    }

    public ICommand RemoveTimeNotProvidedCourseCommand { get; }

    public ICommand ResetCourseSearchCommand
    {
        get
        {
            if (mResetCourseSearchCommandOrNull == null)
            {
                mResetCourseSearchCommandOrNull =
                    new DelegateCommand(resetCourseSearch);
            }

            return mResetCourseSearchCommandOrNull;
        }
    }

    public ICommand EditOrRemoveSelectedCourseCommand
    {
        get
        {
            if (mEditOrRemoveSelectedCourseCommandOrNull == null)
            {
                mEditOrRemoveSelectedCourseCommandOrNull =
                    new ParameterizedCommand<CourseSearchItem>(
                        editOrRemoveSelectedCourse);
            }

            return mEditOrRemoveSelectedCourseCommandOrNull;
        }
    }

    private static IReadOnlyList<CourseSearchItem> createCourseItems(
        CourseCatalogProjection catalogProjection)
    {
        List<CourseSearchItem> courses = new List<CourseSearchItem>();
        foreach (CatalogCourseProjection course in catalogProjection.Courses)
        {
            courses.Add(new CourseSearchItem(course));
        }

        courses.Sort(compareCourseItems);
        return courses.AsReadOnly();
    }

    private static int compareCourseItems(CourseSearchItem left, CourseSearchItem right)
    {
        int codeComparison = string.Compare(
            left.Code,
            right.Code,
            StringComparison.OrdinalIgnoreCase);
        if (codeComparison != 0)
        {
            return codeComparison;
        }

        int titleComparison = string.Compare(
            left.Name,
            right.Name,
            StringComparison.OrdinalIgnoreCase);
        if (titleComparison != 0)
        {
            return titleComparison;
        }

        return string.Compare(
            left.CourseId.Value,
            right.CourseId.Value,
            StringComparison.Ordinal);
    }

    private static int compareCourseSearchMatches(
        CourseSearchMatch left,
        CourseSearchMatch right)
    {
        int kindComparison = left.Kind.CompareTo(right.Kind);
        if (kindComparison != 0)
        {
            return kindComparison;
        }

        return compareCourseItems(left.Course, right.Course);
    }

    private static ObservableCollection<CourseUnitFilterOption> createUnitFilters(
        CourseCatalogProjection catalogProjection)
    {
        ObservableCollection<CourseUnitFilterOption> filters =
            new ObservableCollection<CourseUnitFilterOption>();
        filters.Add(CourseUnitFilterOption.CreateAll());
        foreach (OfferingUnitName offeringUnitName in catalogProjection.OfferingUnitNames)
        {
            filters.Add(CourseUnitFilterOption.CreateSpecific(offeringUnitName));
        }

        return filters;
    }

    private static ObservableCollection<RequirementFilterOption> createRequirementFilters(
        CourseCatalogProjection catalogProjection)
    {
        ObservableCollection<RequirementFilterOption> filters =
            new ObservableCollection<RequirementFilterOption>();
        filters.Add(RequirementFilterOption.CreateAll());
        foreach (CatalogRequirementGroup requirementGroup
            in catalogProjection.RequirementGroups)
        {
            filters.Add(RequirementFilterOption.CreateSpecific(
                requirementGroup.RequirementType));
        }

        return filters;
    }

    private void addCourse(CourseSearchItem course)
    {
        throwIfDisposed();
        if (mActivePlanOrNull == null)
        {
            return;
        }

        if (ActivePlan.ContainsCourse(course.CourseId))
        {
            return;
        }

        if (course.Projection.Offerings.Count > 1)
        {
            beginAddCourseChoice(course);
            return;
        }

        if (course.IsSelectedOptionTimeNotProvided)
        {
            mSession.AddCourse(course.CreateSelection());
            afterPlanContentMutation();
            return;
        }

        addScheduledCourse(course);
    }

    private void addCourseSelectionOption(CourseSelectionOption selectionOption)
    {
        throwIfDisposed();
        if (selectionOption == null)
        {
            throw new ArgumentNullException(nameof(selectionOption));
        }

        CourseSearchItem course = findCourseById(
            selectionOption.Selection.CourseId);
        if (course.IsAdded)
        {
            return;
        }

        course.SelectedSelectionOption = selectionOption;
        addCourse(course);
    }

    private void removeTimeNotProvidedCourse(TimeNotProvidedCourseItem course)
    {
        throwIfDisposed();
        mSession.RemoveCourse(course.CourseId);
        afterPlanContentMutation();
    }

    private void editOrRemoveSelectedCourse(CourseSearchItem course)
    {
        throwIfDisposed();
        if (course == null)
        {
            throw new ArgumentNullException(nameof(course));
        }

        foreach (UnscheduledOfferingSelection selection
            in ActivePlan.Plan.UnscheduledOfferingSelections)
        {
            if (selection.CourseId == course.CourseId)
            {
                mSession.RemoveCourse(course.CourseId);
                afterPlanContentMutation();
                return;
            }
        }

        foreach (CourseChoiceGroup group in ActivePlan.Plan.CourseChoiceGroups)
        {
            if (groupContainsCourse(group, course.CourseId) == false)
            {
                continue;
            }

            CourseCandidate selectedCourseCandidate = findCourseCandidate(
                group,
                course.CourseId);
            if (group.CourseCandidates.Count == 1
                && selectedCourseCandidate.OfferingCandidates.Count == 1)
            {
                mSession.RemoveCourseChoiceGroup(group.Id);
                afterPlanContentMutation();
                return;
            }

            PlanCourseChoiceGroupItem groupItem =
                findActivePlanCourseChoiceGroupItem(group.Id);
            beginEditCourseChoiceGroup(groupItem);
            return;
        }
    }

    private static bool groupContainsCourse(
        CourseChoiceGroup group,
        CourseId courseId)
    {
        foreach (CourseCandidate candidate in group.CourseCandidates)
        {
            if (candidate.CourseId == courseId)
            {
                return true;
            }
        }

        return false;
    }

    private static CourseCandidate findCourseCandidate(
        CourseChoiceGroup group,
        CourseId courseId)
    {
        foreach (CourseCandidate candidate in group.CourseCandidates)
        {
            if (candidate.CourseId == courseId)
            {
                return candidate;
            }
        }

        throw new ArgumentException(
            "The selected course must belong to the course choice group.",
            nameof(courseId));
    }

    private CourseSearchItem findCourseById(CourseId courseId)
    {
        foreach (CourseSearchItem course in mAllCourses)
        {
            if (course.CourseId == courseId)
            {
                return course;
            }
        }

        throw new ArgumentException(
            "The selected course option must belong to the active catalog.",
            nameof(courseId));
    }

    private PlanCourseChoiceGroupItem findActivePlanCourseChoiceGroupItem(
        CourseChoiceGroupId groupId)
    {
        foreach (PlanCourseChoiceGroupItem groupItem
            in ActivePlan.CourseChoiceGroups)
        {
            if (groupItem.GroupId == groupId)
            {
                return groupItem;
            }
        }

        throw new InvalidOperationException(
            "The active plan did not contain its projected course choice group.");
    }

    private void resetCourseSearch()
    {
        throwIfDisposed();
        bool hasChanged = false;
        if (mSearchText.Length > 0)
        {
            mSearchText = string.Empty;
            raisePropertyChanged(nameof(SearchText));
            hasChanged = true;
        }

        CourseUnitFilterOption defaultUnitFilter = UnitFilters[0];
        if (ReferenceEquals(mSelectedUnitFilter, defaultUnitFilter) == false)
        {
            mSelectedUnitFilter = defaultUnitFilter;
            raisePropertyChanged(nameof(SelectedUnitFilter));
            hasChanged = true;
        }

        RequirementFilterOption defaultRequirementFilter = RequirementFilters[0];
        if (ReferenceEquals(
            mSelectedRequirementFilter,
            defaultRequirementFilter) == false)
        {
            mSelectedRequirementFilter = defaultRequirementFilter;
            raisePropertyChanged(nameof(SelectedRequirementFilter));
            hasChanged = true;
        }

        if (hasChanged)
        {
            refreshVisibleCourses();
        }
    }

    private void refreshVisibleCourses()
    {
        CourseSearchQuery query = CourseSearchQuery.Create(SearchText);
        IReadOnlyList<CourseSearchItem> visibleCourses = findVisibleCourses(query);
        KeyedObservableCollectionSynchronizer.Synchronize(
            VisibleCourses,
            visibleCourses,
            findCourseSearchItemId);

        raisePropertyChanged(nameof(VisibleCourseHeading));
        raisePropertyChanged(nameof(CourseSearchResultState));
        raisePropertyChanged(nameof(HasVisibleCourses));
        raisePropertyChanged(nameof(HasNoVisibleCourses));
    }

    private IReadOnlyList<CourseSearchItem> findVisibleCourses(
        CourseSearchQuery query)
    {
        if (query.IsEmpty)
        {
            List<CourseSearchItem> visibleCourses = new List<CourseSearchItem>();
            foreach (CourseSearchItem course in mAllCourses)
            {
                if (matchesCurrentFilters(course))
                {
                    visibleCourses.Add(course);
                }
            }

            return visibleCourses;
        }

        return findRankedVisibleCourses(query);
    }

    private bool matchesCurrentFilters(CourseSearchItem course)
    {
        return SelectedUnitFilter.Matches(course.Projection)
            && SelectedRequirementFilter.Matches(course.Projection);
    }

    private IReadOnlyList<CourseSearchItem> findRankedVisibleCourses(
        CourseSearchQuery query)
    {
        List<CourseSearchMatch> matches = new List<CourseSearchMatch>();
        foreach (CourseSearchItem course in mAllCourses)
        {
            if (matchesCurrentFilters(course) == false)
            {
                continue;
            }

            CourseSearchMatch? matchOrNull = course.FindSearchMatchOrNull(query);
            if (matchOrNull != null)
            {
                matches.Add(matchOrNull);
            }
        }

        matches.Sort(compareCourseSearchMatches);
        List<CourseSearchItem> visibleCourses = new List<CourseSearchItem>();
        foreach (CourseSearchMatch match in matches)
        {
            visibleCourses.Add(match.Course);
        }

        return visibleCourses;
    }

    private static CourseId findCourseSearchItemId(CourseSearchItem course)
    {
        return course.CourseId;
    }

    private void synchronizeCourseSelectionState()
    {
        foreach (CourseSearchItem course in mAllCourses)
        {
            CourseChoiceGroup? courseChoiceGroupOrNull =
                findActiveCourseChoiceGroupOrNull(course.CourseId);
            if (courseChoiceGroupOrNull == null)
            {
                course.SynchronizeSelection(
                    findActiveUnscheduledSelectionOrNull(course.CourseId));
            }
            else
            {
                course.SynchronizeCourseChoiceGroup(courseChoiceGroupOrNull);
            }

            course.SynchronizeSelectedAction(
                findActiveCourseSelectionAction(course.CourseId));
        }
    }

    private ECourseSelectionAction findActiveCourseSelectionAction(
        CourseId courseId)
    {
        if (mActivePlanOrNull == null)
        {
            return ECourseSelectionAction.None;
        }

        foreach (UnscheduledOfferingSelection selection
            in ActivePlan.Plan.UnscheduledOfferingSelections)
        {
            if (selection.CourseId == courseId)
            {
                return ECourseSelectionAction.Remove;
            }
        }

        foreach (CourseChoiceGroup group in ActivePlan.Plan.CourseChoiceGroups)
        {
            if (groupContainsCourse(group, courseId))
            {
                CourseCandidate selectedCourseCandidate = findCourseCandidate(
                    group,
                    courseId);
                bool isDirectSelection = group.CourseCandidates.Count == 1
                    && selectedCourseCandidate.OfferingCandidates.Count == 1;
                return isDirectSelection
                    ? ECourseSelectionAction.Remove
                    : ECourseSelectionAction.Edit;
            }
        }

        return ECourseSelectionAction.None;
    }

    private PlanningCourseSelection? findActiveUnscheduledSelectionOrNull(
        CourseId courseId)
    {
        if (mActivePlanOrNull == null)
        {
            return null;
        }

        foreach (UnscheduledOfferingSelection selection
            in ActivePlan.Plan.UnscheduledOfferingSelections)
        {
            if (selection.CourseId == courseId)
            {
                return PlanningCourseSelection.CreateTimeNotProvidedOffering(
                    selection.CourseId,
                    selection.OfferingId);
            }
        }

        return null;
    }

    private CourseChoiceGroup? findActiveCourseChoiceGroupOrNull(
        CourseId courseId)
    {
        if (mActivePlanOrNull == null)
        {
            return null;
        }

        foreach (CourseChoiceGroup group in ActivePlan.Plan.CourseChoiceGroups)
        {
            if (groupContainsCourse(group, courseId))
            {
                return group;
            }
        }

        return null;
    }
}
