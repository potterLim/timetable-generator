using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows.Input;

using Avalonia.Controls;

using TimetableGenerator.Desktop.Presentation.Layout;
using TimetableGenerator.Desktop.Presentation.Models;

namespace TimetableGenerator.Desktop.Presentation.ViewModels;

internal sealed class PlannerWorkspaceViewModel : ObservableObject
{
    private const int DISPLAYED_RECOMMENDATION_COUNT = 24;
    private const double EXTRA_WIDE_COURSE_PANE_WIDTH = 368.0;
    private const double WIDE_COURSE_PANE_WIDTH = 336.0;
    private const double COLLAPSED_COURSE_PANE_WIDTH = 320.0;
    private const double EXTRA_WIDE_INSPECTOR_PANE_WIDTH = 326.0;
    private const double WIDE_INSPECTOR_PANE_WIDTH = 304.0;
    private const double COLLAPSED_INSPECTOR_PANE_WIDTH = 320.0;

    private readonly IReadOnlyList<CourseSearchItem> mAllCourses;
    private readonly IReadOnlyList<ScheduleRecommendation> mRecommendations;

    private string mSearchText;
    private CourseDepartmentFilterOption mSelectedDepartmentFilter;
    private RequirementFilterOption mSelectedRequirementFilter;
    private PlanTabItem mActivePlan;
    private int mRecommendationIndex;
    private EWorkspaceLayoutMode mLayoutMode;
    private bool mIsCoursePaneOpen;
    private bool mIsInspectorPaneOpen;
    private SplitViewDisplayMode mCoursePaneDisplayMode;
    private SplitViewDisplayMode mInspectorPaneDisplayMode;
    private double mCoursePaneWidth;
    private double mInspectorPaneWidth;

    public ObservableCollection<CourseSearchItem> VisibleCourses { get; }

    public ObservableCollection<CourseDepartmentFilterOption> DepartmentFilters { get; }

    public ObservableCollection<RequirementFilterOption> RequirementFilters { get; }

    public ObservableCollection<PlanTabItem> Plans { get; }

    public string SearchText
    {
        get
        {
            return mSearchText;
        }
        set
        {
            if (setProperty(ref mSearchText, value))
            {
                refreshVisibleCourses();
            }
        }
    }

    public CourseDepartmentFilterOption SelectedDepartmentFilter
    {
        get
        {
            return mSelectedDepartmentFilter;
        }
        set
        {
            if (setProperty(ref mSelectedDepartmentFilter, value))
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
            if (setProperty(ref mSelectedRequirementFilter, value))
            {
                refreshVisibleCourses();
            }
        }
    }

    public PlanTabItem ActivePlan
    {
        get
        {
            return mActivePlan;
        }
        set
        {
            if (setProperty(ref mActivePlan, value))
            {
                synchronizeCourseSelectionState();
            }
        }
    }

    public string VisibleCourseHeading
    {
        get
        {
            if (string.IsNullOrWhiteSpace(SearchText) &&
                SelectedDepartmentFilter.Value == ECourseDepartmentFilter.All &&
                SelectedRequirementFilter.Value == ERequirementFilter.All)
            {
                return "전체 과목 (132개)";
            }

            return "검색 결과 (" + VisibleCourses.Count + "개)";
        }
    }

    public ScheduleRecommendation ActiveRecommendation
    {
        get
        {
            int sampleIndex = mRecommendationIndex % mRecommendations.Count;
            return mRecommendations[sampleIndex];
        }
    }

    public string RecommendationSummary
    {
        get
        {
            return (mRecommendationIndex + 1) + " / " + DISPLAYED_RECOMMENDATION_COUNT;
        }
    }

    public EWorkspaceLayoutMode LayoutMode
    {
        get
        {
            return mLayoutMode;
        }
    }

    public bool IsCoursePaneOpen
    {
        get
        {
            return mIsCoursePaneOpen;
        }
        set
        {
            setProperty(ref mIsCoursePaneOpen, value);
        }
    }

    public bool IsInspectorPaneOpen
    {
        get
        {
            return mIsInspectorPaneOpen;
        }
        set
        {
            setProperty(ref mIsInspectorPaneOpen, value);
        }
    }

    public SplitViewDisplayMode CoursePaneDisplayMode
    {
        get
        {
            return mCoursePaneDisplayMode;
        }
    }

    public SplitViewDisplayMode InspectorPaneDisplayMode
    {
        get
        {
            return mInspectorPaneDisplayMode;
        }
    }

    public double CoursePaneWidth
    {
        get
        {
            return mCoursePaneWidth;
        }
    }

    public double InspectorPaneWidth
    {
        get
        {
            return mInspectorPaneWidth;
        }
    }

    public bool IsCoursePaneToggleVisible
    {
        get
        {
            return LayoutMode == EWorkspaceLayoutMode.Compact;
        }
    }

    public bool IsInspectorPaneToggleVisible
    {
        get
        {
            return LayoutMode == EWorkspaceLayoutMode.Medium ||
                LayoutMode == EWorkspaceLayoutMode.Compact;
        }
    }

    public ICommand AddCourseCommand { get; }

    public ICommand RemoveCourseCommand { get; }

    public ICommand AddPlanCommand { get; }

    public ICommand PreviousRecommendationCommand { get; }

    public ICommand NextRecommendationCommand { get; }

    public ICommand ToggleCoursePaneCommand { get; }

    public ICommand ToggleInspectorPaneCommand { get; }

    public PlannerWorkspaceViewModel(
        IEnumerable<CourseSearchItem> courses,
        IEnumerable<PlanTabItem> plans,
        IEnumerable<ScheduleRecommendation> recommendations)
    {
        ArgumentNullException.ThrowIfNull(courses);
        ArgumentNullException.ThrowIfNull(plans);
        ArgumentNullException.ThrowIfNull(recommendations);

        mAllCourses = new List<CourseSearchItem>(courses).AsReadOnly();
        mRecommendations = new List<ScheduleRecommendation>(recommendations).AsReadOnly();
        if (mRecommendations.Count == 0)
        {
            throw new ArgumentException("At least one sample recommendation is required.", nameof(recommendations));
        }

        VisibleCourses = new ObservableCollection<CourseSearchItem>();
        DepartmentFilters = createDepartmentFilters();
        RequirementFilters = createRequirementFilters();
        Plans = new ObservableCollection<PlanTabItem>(plans);
        if (Plans.Count == 0)
        {
            throw new ArgumentException("At least one plan is required.", nameof(plans));
        }

        mSearchText = string.Empty;
        mSelectedDepartmentFilter = DepartmentFilters[0];
        mSelectedRequirementFilter = RequirementFilters[0];
        mActivePlan = Plans[0];
        mRecommendationIndex = 0;
        mLayoutMode = EWorkspaceLayoutMode.ExtraWide;
        mIsCoursePaneOpen = true;
        mIsInspectorPaneOpen = true;
        mCoursePaneDisplayMode = SplitViewDisplayMode.Inline;
        mInspectorPaneDisplayMode = SplitViewDisplayMode.Inline;
        mCoursePaneWidth = EXTRA_WIDE_COURSE_PANE_WIDTH;
        mInspectorPaneWidth = EXTRA_WIDE_INSPECTOR_PANE_WIDTH;

        AddCourseCommand = new ParameterizedCommand<CourseSearchItem>(addCourse);
        RemoveCourseCommand = new ParameterizedCommand<PlanCourseItem>(removeCourse);
        AddPlanCommand = new DelegateCommand(addPlan);
        PreviousRecommendationCommand = new DelegateCommand(selectPreviousRecommendation);
        NextRecommendationCommand = new DelegateCommand(selectNextRecommendation);
        ToggleCoursePaneCommand = new DelegateCommand(toggleCoursePane);
        ToggleInspectorPaneCommand = new DelegateCommand(toggleInspectorPane);

        refreshVisibleCourses();
        synchronizeCourseSelectionState();
    }

    internal void applyWorkspaceWidth(WorkspaceWidth workspaceWidth)
    {
        EWorkspaceLayoutMode newLayoutMode = WorkspaceLayoutPolicy.FindLayoutMode(workspaceWidth);
        if (newLayoutMode == LayoutMode)
        {
            return;
        }

        mLayoutMode = newLayoutMode;
        configurePanesForLayoutMode();
        raisePropertyChanged(nameof(LayoutMode));
        raisePropertyChanged(nameof(IsCoursePaneToggleVisible));
        raisePropertyChanged(nameof(IsInspectorPaneToggleVisible));
    }

    internal void closeOverlayPanes()
    {
        if (CoursePaneDisplayMode == SplitViewDisplayMode.Overlay)
        {
            IsCoursePaneOpen = false;
        }

        if (InspectorPaneDisplayMode == SplitViewDisplayMode.Overlay)
        {
            IsInspectorPaneOpen = false;
        }
    }

    private static ObservableCollection<CourseDepartmentFilterOption> createDepartmentFilters()
    {
        ObservableCollection<CourseDepartmentFilterOption> filters =
            new ObservableCollection<CourseDepartmentFilterOption>();
        filters.Add(new CourseDepartmentFilterOption(ECourseDepartmentFilter.All, "학부 전체"));
        filters.Add(new CourseDepartmentFilterOption(ECourseDepartmentFilter.Computing, "AI·컴퓨터공학"));
        filters.Add(new CourseDepartmentFilterOption(ECourseDepartmentFilter.GeneralStudies, "글로벌리더십"));
        filters.Add(new CourseDepartmentFilterOption(ECourseDepartmentFilter.Business, "경영경제"));
        return filters;
    }

    private static ObservableCollection<RequirementFilterOption> createRequirementFilters()
    {
        ObservableCollection<RequirementFilterOption> filters =
            new ObservableCollection<RequirementFilterOption>();
        filters.Add(new RequirementFilterOption(ERequirementFilter.All, "이수구분 전체"));
        filters.Add(new RequirementFilterOption(ERequirementFilter.Required, "전공·교양 필수"));
        filters.Add(new RequirementFilterOption(ERequirementFilter.Elective, "선택"));
        return filters;
    }

    private void addCourse(CourseSearchItem course)
    {
        if (ActivePlan.ContainsCourse(course.CourseId))
        {
            return;
        }

        ActivePlan.AddCourse(course.CreatePlanCourseItem());
        course.MarkAdded();
    }

    private void removeCourse(PlanCourseItem course)
    {
        ActivePlan.RemoveCourse(course);
        synchronizeCourseSelectionState();
    }

    private void addPlan()
    {
        int nextPlanNumber = Plans.Count + 1;
        PlanTabItem newPlan = new PlanTabItem(
            new PlanId(nextPlanNumber),
            new PlanName("새 계획 " + nextPlanNumber),
            new ObservableCollection<PlanCourseItem>(),
            new ObservableCollection<PlanCourseItem>());
        Plans.Add(newPlan);
        ActivePlan = newPlan;
    }

    private void selectPreviousRecommendation()
    {
        --mRecommendationIndex;
        if (mRecommendationIndex < 0)
        {
            mRecommendationIndex = DISPLAYED_RECOMMENDATION_COUNT - 1;
        }

        notifyRecommendationChanged();
    }

    private void selectNextRecommendation()
    {
        ++mRecommendationIndex;
        if (mRecommendationIndex >= DISPLAYED_RECOMMENDATION_COUNT)
        {
            mRecommendationIndex = 0;
        }

        notifyRecommendationChanged();
    }

    private void toggleCoursePane()
    {
        IsCoursePaneOpen = IsCoursePaneOpen == false;
    }

    private void toggleInspectorPane()
    {
        IsInspectorPaneOpen = IsInspectorPaneOpen == false;
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
        if (SelectedDepartmentFilter.Value != ECourseDepartmentFilter.All &&
            course.Department != SelectedDepartmentFilter.Value)
        {
            return false;
        }

        if (SelectedRequirementFilter.Value != ERequirementFilter.All &&
            course.Requirement != SelectedRequirementFilter.Value)
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(SearchText))
        {
            return true;
        }

        string normalizedSearchText = SearchText.Trim();
        return course.Code.Contains(normalizedSearchText, StringComparison.CurrentCultureIgnoreCase) ||
            course.Name.Contains(normalizedSearchText, StringComparison.CurrentCultureIgnoreCase) ||
            course.InstructorDisplayText.Contains(normalizedSearchText, StringComparison.CurrentCultureIgnoreCase);
    }

    private void synchronizeCourseSelectionState()
    {
        foreach (CourseSearchItem course in mAllCourses)
        {
            if (ActivePlan.ContainsCourse(course.CourseId))
            {
                course.MarkAdded();
            }
            else
            {
                course.MarkRemoved();
            }
        }
    }

    private void notifyRecommendationChanged()
    {
        raisePropertyChanged(nameof(ActiveRecommendation));
        raisePropertyChanged(nameof(RecommendationSummary));
    }

    private void configurePanesForLayoutMode()
    {
        switch (LayoutMode)
        {
            case EWorkspaceLayoutMode.ExtraWide:
                setCoursePaneState(
                    SplitViewDisplayMode.Inline,
                    EXTRA_WIDE_COURSE_PANE_WIDTH,
                    EPaneOpenState.Open);
                setInspectorPaneState(
                    SplitViewDisplayMode.Inline,
                    EXTRA_WIDE_INSPECTOR_PANE_WIDTH,
                    EPaneOpenState.Open);
                break;
            case EWorkspaceLayoutMode.Wide:
                setCoursePaneState(
                    SplitViewDisplayMode.Inline,
                    WIDE_COURSE_PANE_WIDTH,
                    EPaneOpenState.Open);
                setInspectorPaneState(
                    SplitViewDisplayMode.Inline,
                    WIDE_INSPECTOR_PANE_WIDTH,
                    EPaneOpenState.Open);
                break;
            case EWorkspaceLayoutMode.Medium:
                setCoursePaneState(
                    SplitViewDisplayMode.Inline,
                    COLLAPSED_COURSE_PANE_WIDTH,
                    EPaneOpenState.Open);
                setInspectorPaneState(
                    SplitViewDisplayMode.Overlay,
                    COLLAPSED_INSPECTOR_PANE_WIDTH,
                    EPaneOpenState.Closed);
                break;
            case EWorkspaceLayoutMode.Compact:
                setCoursePaneState(
                    SplitViewDisplayMode.Overlay,
                    COLLAPSED_COURSE_PANE_WIDTH,
                    EPaneOpenState.Closed);
                setInspectorPaneState(
                    SplitViewDisplayMode.Overlay,
                    COLLAPSED_INSPECTOR_PANE_WIDTH,
                    EPaneOpenState.Closed);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(LayoutMode), LayoutMode, "Unknown workspace layout mode.");
        }
    }

    private void setCoursePaneState(
        SplitViewDisplayMode displayMode,
        double paneWidth,
        EPaneOpenState paneOpenState)
    {
        mCoursePaneDisplayMode = displayMode;
        mCoursePaneWidth = paneWidth;
        IsCoursePaneOpen = paneOpenState == EPaneOpenState.Open;
        raisePropertyChanged(nameof(CoursePaneDisplayMode));
        raisePropertyChanged(nameof(CoursePaneWidth));
    }

    private void setInspectorPaneState(
        SplitViewDisplayMode displayMode,
        double paneWidth,
        EPaneOpenState paneOpenState)
    {
        mInspectorPaneDisplayMode = displayMode;
        mInspectorPaneWidth = paneWidth;
        IsInspectorPaneOpen = paneOpenState == EPaneOpenState.Open;
        raisePropertyChanged(nameof(InspectorPaneDisplayMode));
        raisePropertyChanged(nameof(InspectorPaneWidth));
    }
}
