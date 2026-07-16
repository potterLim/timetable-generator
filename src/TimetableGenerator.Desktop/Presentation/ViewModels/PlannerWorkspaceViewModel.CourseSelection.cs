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

    public ICommand AddCourseCommand { get; }

    public ICommand RemoveCourseCommand { get; }

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
        return string.Compare(left.Code, right.Code, StringComparison.Ordinal);
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

        mSession.AddCourse(course.CreateSelection());
        afterPlanContentMutation();
    }

    private void removeCourse(PlanCourseItem course)
    {
        throwIfDisposed();
        mSession.RemoveCourse(course.CourseId);
        afterPlanContentMutation();
    }

    private void refreshVisibleCourses()
    {
        VisibleCourses.Clear();
        foreach (CourseSearchItem course in mAllCourses)
        {
            if (matchesCurrentFilters(course))
            {
                VisibleCourses.Add(course);
            }
        }

        raisePropertyChanged(nameof(VisibleCourseHeading));
    }

    private bool matchesCurrentFilters(CourseSearchItem course)
    {
        return SelectedUnitFilter.Matches(course.Projection)
            && SelectedRequirementFilter.Matches(course.Projection)
            && course.MatchesSearchText(SearchText);
    }

    private void synchronizeCourseSelectionState()
    {
        PlanningPlan activePlan = ActivePlan.Plan;
        foreach (CourseSearchItem course in mAllCourses)
        {
            PlanningCourseSelection? selectionOrNull = findCourseSelection(
                activePlan,
                course.CourseId);
            course.SynchronizeSelection(selectionOrNull);
        }
    }

    private static PlanningCourseSelection? findCourseSelection(
        PlanningPlan plan,
        CourseId courseId)
    {
        foreach (ScheduledCourseChoice choice in plan.ScheduledCourseChoices)
        {
            if (choice.CourseId == courseId)
            {
                return PlanningCourseSelection.CreateScheduledAlternatives(
                    choice.CourseId,
                    choice.OfferingIds);
            }
        }

        foreach (UnscheduledOfferingSelection selection
            in plan.UnscheduledOfferingSelections)
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
}
