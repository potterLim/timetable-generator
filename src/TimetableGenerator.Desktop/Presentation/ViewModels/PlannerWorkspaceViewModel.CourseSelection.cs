using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows.Input;

using TimetableGenerator.Application.Planning;
using TimetableGenerator.CatalogJson;
using TimetableGenerator.Desktop.Presentation.Catalog;
using TimetableGenerator.Desktop.Presentation.Models;
using TimetableGenerator.Domain.Catalogs;
using TimetableGenerator.Domain.Planning;

namespace TimetableGenerator.Desktop.Presentation.ViewModels;

internal sealed partial class PlannerWorkspaceViewModel
{
    private readonly IReadOnlyList<CourseSearchItem> mAllCourses;

    private DelegateCommand? mResetCourseSearchCommandOrNull;

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

    public ICommand RemoveTimeNotProvidedCourseCommand { get; }

    public ICommand ResetCourseSearchCommand
    {
        get
        {
            mResetCourseSearchCommandOrNull ??=
                new DelegateCommand(resetCourseSearch);
            return mResetCourseSearchCommandOrNull;
        }
    }

    public ICommand EditOrRemoveSelectedCourseCommand
    {
        get
        {
            mEditOrRemoveSelectedCourseCommandOrNull ??=
                new ParameterizedCommand<CourseSearchItem>(
                    editOrRemoveSelectedCourse);
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
        if (ActivePlan.ContainsCourse(course.CourseId))
        {
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

            if (group.CourseCandidates.Count == 1)
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
        VisibleCourses.Clear();
        CourseSearchQuery query = CourseSearchQuery.Create(SearchText);
        if (query.IsEmpty)
        {
            foreach (CourseSearchItem course in mAllCourses)
            {
                if (matchesCurrentFilters(course))
                {
                    VisibleCourses.Add(course);
                }
            }
        }
        else
        {
            addRankedVisibleCourses(query);
        }

        raisePropertyChanged(nameof(VisibleCourseHeading));
        raisePropertyChanged(nameof(CourseSearchResultState));
        raisePropertyChanged(nameof(HasVisibleCourses));
        raisePropertyChanged(nameof(HasNoVisibleCourses));
    }

    private bool matchesCurrentFilters(CourseSearchItem course)
    {
        return SelectedUnitFilter.Matches(course.Projection)
            && SelectedRequirementFilter.Matches(course.Projection);
    }

    private void addRankedVisibleCourses(CourseSearchQuery query)
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
        foreach (CourseSearchMatch match in matches)
        {
            VisibleCourses.Add(match.Course);
        }
    }

    private void synchronizeCourseSelectionState()
    {
        foreach (CourseSearchItem course in mAllCourses)
        {
            course.SynchronizeSelection(
                findActiveCourseSelectionOrNull(course));
            course.SynchronizeSelectedAction(
                findActiveCourseSelectionAction(course.CourseId));
        }
    }

    private ECourseSelectionAction findActiveCourseSelectionAction(
        CourseId courseId)
    {
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
                return group.CourseCandidates.Count == 1
                    ? ECourseSelectionAction.Remove
                    : ECourseSelectionAction.Edit;
            }
        }

        return ECourseSelectionAction.None;
    }

    private PlanningCourseSelection? findActiveCourseSelectionOrNull(
        CourseSearchItem course)
    {
        foreach (UnscheduledOfferingSelection selection
            in ActivePlan.Plan.UnscheduledOfferingSelections)
        {
            if (selection.CourseId == course.CourseId)
            {
                return PlanningCourseSelection.CreateTimeNotProvidedOffering(
                    selection.CourseId,
                    selection.OfferingId);
            }
        }

        foreach (CourseChoiceGroup group in ActivePlan.Plan.CourseChoiceGroups)
        {
            foreach (CourseCandidate candidate in group.CourseCandidates)
            {
                if (candidate.CourseId == course.CourseId)
                {
                    return PlanningCourseSelection.CreateScheduledAlternatives(
                        candidate.CourseId,
                        course.Projection.ScheduledOfferingIds);
                }
            }
        }

        return null;
    }
}
