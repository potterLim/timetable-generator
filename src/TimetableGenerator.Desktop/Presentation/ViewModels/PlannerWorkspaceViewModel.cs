using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;

using Avalonia.Controls;
using Avalonia.Threading;

using TimetableGenerator.Application.Planning;
using TimetableGenerator.Application.Scheduling;
using TimetableGenerator.CatalogJson;
using TimetableGenerator.Desktop.Presentation.Catalog;
using TimetableGenerator.Desktop.Presentation.Layout;
using TimetableGenerator.Desktop.Presentation.Models;
using TimetableGenerator.Desktop.Presentation.Recommendations;
using TimetableGenerator.Desktop.Storage;
using TimetableGenerator.Domain.Catalogs;
using TimetableGenerator.Domain.Planning;
using TimetableGenerator.Domain.Scheduling;
using ApplicationScheduleRecommendation =
    TimetableGenerator.Application.Scheduling.ScheduleRecommendation;
using PresentationScheduleRecommendation =
    TimetableGenerator.Desktop.Presentation.Models.ScheduleRecommendation;

namespace TimetableGenerator.Desktop.Presentation.ViewModels;

internal sealed class PlannerWorkspaceViewModel : ObservableObject, IDisposable
{
    private const double COLLAPSED_COURSE_PANE_WIDTH = 320.0;
    private const double COLLAPSED_INSPECTOR_PANE_WIDTH = 320.0;
    private const int DISPLAYED_RECOMMENDATION_COUNT = 24;
    private const double EXTRA_WIDE_COURSE_PANE_WIDTH = 368.0;
    private const double EXTRA_WIDE_INSPECTOR_PANE_WIDTH = 326.0;
    private const double WIDE_COURSE_PANE_WIDTH = 336.0;
    private const double WIDE_INSPECTOR_PANE_WIDTH = 304.0;

    private static readonly PresentationScheduleRecommendation EMPTY_RECOMMENDATION =
        new PresentationScheduleRecommendation(Array.Empty<ScheduleEntry>());

    private readonly CourseCatalogProjection mCatalogProjection;

    private readonly PlanningWorkspaceSession mSession;

    private readonly PlanningWorkspaceAutosaveQueue mAutosaveQueue;

    private readonly IScheduleRecommendationProvider mRecommendationProvider;

    private readonly IReadOnlyList<CourseSearchItem> mAllCourses;

    private readonly DelegateCommand mPreviousRecommendationCommand;

    private readonly DelegateCommand mNextRecommendationCommand;

    private readonly DelegateCommand mConfirmRenamePlanCommand;

    private readonly DelegateCommand mBeginDeletePlanCommand;

    private readonly DelegateCommand mRetryAutosaveCommand;

    private readonly DelegateCommand mRetryRecommendationCommand;

    private IReadOnlyList<PresentationScheduleRecommendation> mRecommendations;

    private string mSearchText;

    private CourseUnitFilterOption mSelectedUnitFilter;

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

    private bool mIsRenamingPlan;

    private bool mIsDeletePlanConfirmationVisible;

    private string mPlanNameDraft;

    private string mPlanNameValidationMessage;

    private EPlanningWorkspaceAutosaveStatus mAutosaveStatus;

    private string mAutosaveStatusText;

    private CancellationTokenSource mRecommendationCancellationSource;

    private Task mRecommendationRefreshTask;

    private ERecommendationCalculationState mRecommendationCalculationState;

    private string mRecommendationCalculationError;

    private bool mIsDisposed;

    private bool mIsRebuildingPlanItems;

    public ObservableCollection<CourseSearchItem> VisibleCourses { get; }

    public ObservableCollection<CourseUnitFilterOption> UnitFilters { get; }

    public ObservableCollection<RequirementFilterOption> RequirementFilters { get; }

    public ObservableCollection<PlanTabItem> Plans { get; }

    public string InstitutionTermDisplayText
    {
        get
        {
            CourseCatalog catalog = mCatalogProjection.Document.Catalog;
            return catalog.InstitutionName.Value + "  ·  " + catalog.Term.Id;
        }
    }

    public string InstitutionName
    {
        get
        {
            return mCatalogProjection.Document.Catalog.InstitutionName.Value;
        }
    }

    public string CatalogRevisionDisplayText
    {
        get
        {
            return "과목 데이터 r"
                + mCatalogProjection.Document.Catalog.Revision.Value.ToString("D4");
        }
    }

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

    public PlanTabItem ActivePlan
    {
        get
        {
            return mActivePlan;
        }
        set
        {
            activatePlan(value);
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

    public PresentationScheduleRecommendation ActiveRecommendation
    {
        get
        {
            if (mRecommendations.Count == 0)
            {
                return EMPTY_RECOMMENDATION;
            }

            return mRecommendations[mRecommendationIndex];
        }
    }

    public string RecommendationSummary
    {
        get
        {
            if (mRecommendations.Count == 0)
            {
                return "0 / 0";
            }

            return (mRecommendationIndex + 1) + " / " + mRecommendations.Count;
        }
    }

    public bool HasRecommendations
    {
        get
        {
            return mRecommendations.Count > 0;
        }
    }

    public bool HasScheduleEntries
    {
        get
        {
            return ActiveRecommendation.Entries.Count > 0;
        }
    }

    public bool IsScheduleEmpty
    {
        get
        {
            return HasScheduleEntries == false
                && IsRecommendationCalculating == false
                && HasRecommendationCalculationError == false;
        }
    }

    public bool IsRecommendationCalculating
    {
        get
        {
            return mRecommendationCalculationState
                == ERecommendationCalculationState.Calculating;
        }
    }

    public bool HasRecommendationCalculationError
    {
        get
        {
            return mRecommendationCalculationState
                == ERecommendationCalculationState.Failed;
        }
    }

    public string RecommendationCalculationError
    {
        get
        {
            return mRecommendationCalculationError;
        }
    }

    public string RecommendationInsight
    {
        get
        {
            if (IsRecommendationCalculating)
            {
                return "가능한 분반 조합을 계산하고 있습니다.";
            }

            if (HasRecommendationCalculationError)
            {
                return "추천 시간표를 계산하지 못했습니다.";
            }

            if (HasRecommendations == false)
            {
                return "과목 선택을 바꾸면 충돌 없는 조합을 다시 계산합니다.";
            }

            if (HasScheduleEntries == false)
            {
                return "시간 미정 과목은 충돌 자동 검증에서 제외됩니다.";
            }

            HashSet<EDay> scheduledDays = new HashSet<EDay>();
            HashSet<string> courseCodes = new HashSet<string>(StringComparer.Ordinal);
            foreach (ScheduleEntry entry in ActiveRecommendation.Entries)
            {
                scheduledDays.Add(entry.Day);
                courseCodes.Add(entry.Code);
            }

            int freeWeekdayCount = 5 - scheduledDays.Count;
            return courseCodes.Count
                + "개 시간표 과목 · 공강 "
                + freeWeekdayCount
                + "일";
        }
    }

    public string EmptyScheduleTitle
    {
        get
        {
            if (ActivePlan.SelectedCourseCount == 0)
            {
                return "과목을 추가해 시간표를 시작하세요";
            }

            if (ActivePlan.ScheduledCourses.Count == 0)
            {
                return "시간이 제공된 과목이 아직 없어요";
            }

            return "충돌 없는 시간표 조합을 찾지 못했어요";
        }
    }

    public string EmptyScheduleMessage
    {
        get
        {
            if (ActivePlan.SelectedCourseCount == 0)
            {
                return "왼쪽 과목 목록에서 원하는 과목을 선택하면 가능한 분반 조합을 바로 계산합니다.";
            }

            if (ActivePlan.ScheduledCourses.Count == 0)
            {
                return "선택한 과목은 모두 시간 미정입니다. 오른쪽 계획에서 확인할 수 있습니다.";
            }

            return "겹치는 과목을 제거하거나 다른 과목을 선택해 다시 계산해 보세요.";
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
            return LayoutMode == EWorkspaceLayoutMode.Medium
                || LayoutMode == EWorkspaceLayoutMode.Compact;
        }
    }

    public bool IsRenamingPlan
    {
        get
        {
            return mIsRenamingPlan;
        }
    }

    public bool IsDeletePlanConfirmationVisible
    {
        get
        {
            return mIsDeletePlanConfirmationVisible;
        }
    }

    public string PlanNameDraft
    {
        get
        {
            return mPlanNameDraft;
        }
        set
        {
            string normalizedValue = value;
            if (normalizedValue == null)
            {
                normalizedValue = string.Empty;
            }

            if (setProperty(ref mPlanNameDraft, normalizedValue))
            {
                clearPlanNameValidationMessage();
            }
        }
    }

    public string PlanNameValidationMessage
    {
        get
        {
            return mPlanNameValidationMessage;
        }
    }

    public bool HasPlanNameValidationMessage
    {
        get
        {
            return string.IsNullOrEmpty(PlanNameValidationMessage) == false;
        }
    }

    public bool CanDeleteActivePlan
    {
        get
        {
            return Plans.Count > 1;
        }
    }

    public EPlanningWorkspaceAutosaveStatus AutosaveStatus
    {
        get
        {
            return mAutosaveStatus;
        }
    }

    public string AutosaveStatusText
    {
        get
        {
            return mAutosaveStatusText;
        }
    }

    public bool IsAutosaveSaved
    {
        get
        {
            return AutosaveStatus == EPlanningWorkspaceAutosaveStatus.Saved;
        }
    }

    public bool IsAutosaveSaving
    {
        get
        {
            return AutosaveStatus == EPlanningWorkspaceAutosaveStatus.Saving;
        }
    }

    public bool HasAutosaveError
    {
        get
        {
            return AutosaveStatus == EPlanningWorkspaceAutosaveStatus.Failed;
        }
    }

    public ICommand AddCourseCommand { get; }

    public ICommand RemoveCourseCommand { get; }

    public ICommand AddPlanCommand { get; }

    public ICommand PreviousRecommendationCommand
    {
        get
        {
            return mPreviousRecommendationCommand;
        }
    }

    public ICommand NextRecommendationCommand
    {
        get
        {
            return mNextRecommendationCommand;
        }
    }

    public ICommand ToggleCoursePaneCommand { get; }

    public ICommand ToggleInspectorPaneCommand { get; }

    public ICommand BeginRenamePlanCommand { get; }

    public ICommand ConfirmRenamePlanCommand
    {
        get
        {
            return mConfirmRenamePlanCommand;
        }
    }

    public ICommand CancelRenamePlanCommand { get; }

    public ICommand BeginDeletePlanCommand
    {
        get
        {
            return mBeginDeletePlanCommand;
        }
    }

    public ICommand ConfirmDeletePlanCommand { get; }

    public ICommand CancelDeletePlanCommand { get; }

    public ICommand RetryAutosaveCommand
    {
        get
        {
            return mRetryAutosaveCommand;
        }
    }

    public ICommand RetryRecommendationCommand
    {
        get
        {
            return mRetryRecommendationCommand;
        }
    }

    internal Task RecommendationRefreshTask
    {
        get
        {
            return mRecommendationRefreshTask;
        }
    }

    public PlannerWorkspaceViewModel(
        CourseCatalogProjection catalogProjection,
        PlanningWorkspaceSession session,
        PlanningWorkspaceAutosaveQueue autosaveQueue,
        IScheduleRecommendationProvider recommendationProvider)
    {
        if (catalogProjection == null)
        {
            throw new ArgumentNullException(nameof(catalogProjection));
        }

        if (session == null)
        {
            throw new ArgumentNullException(nameof(session));
        }

        if (autosaveQueue == null)
        {
            throw new ArgumentNullException(nameof(autosaveQueue));
        }

        if (recommendationProvider == null)
        {
            throw new ArgumentNullException(nameof(recommendationProvider));
        }

        if (ReferenceEquals(catalogProjection.Document.Catalog, session.Catalog) == false)
        {
            throw new ArgumentException(
                "The workspace session and presentation projection must share a catalog.",
                nameof(session));
        }

        mCatalogProjection = catalogProjection;
        mSession = session;
        mAutosaveQueue = autosaveQueue;
        mRecommendationProvider = recommendationProvider;
        mAllCourses = createCourseItems(catalogProjection);
        mRecommendations = Array.Empty<PresentationScheduleRecommendation>();

        VisibleCourses = new ObservableCollection<CourseSearchItem>();
        UnitFilters = createUnitFilters(catalogProjection);
        RequirementFilters = createRequirementFilters(catalogProjection);
        Plans = new ObservableCollection<PlanTabItem>();

        mSearchText = string.Empty;
        mSelectedUnitFilter = UnitFilters[0];
        mSelectedRequirementFilter = RequirementFilters[0];
        rebuildPlanItems();
        mActivePlan = findPlanItem(mSession.Workspace.ActivePlanId);
        mPlanNameDraft = mActivePlan.DisplayName;
        mPlanNameValidationMessage = string.Empty;
        mAutosaveStatus = EPlanningWorkspaceAutosaveStatus.Saved;
        mAutosaveStatusText = "자동 저장됨";
        mRecommendationCancellationSource = new CancellationTokenSource();
        mRecommendationRefreshTask = Task.CompletedTask;
        mRecommendationCalculationState = ERecommendationCalculationState.Calculating;
        mRecommendationCalculationError = string.Empty;
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
        mPreviousRecommendationCommand = new DelegateCommand(
            selectPreviousRecommendation,
            canNavigateRecommendations);
        mNextRecommendationCommand = new DelegateCommand(
            selectNextRecommendation,
            canNavigateRecommendations);
        ToggleCoursePaneCommand = new DelegateCommand(toggleCoursePane);
        ToggleInspectorPaneCommand = new DelegateCommand(toggleInspectorPane);
        BeginRenamePlanCommand = new DelegateCommand(beginRenamePlan);
        mConfirmRenamePlanCommand = new DelegateCommand(confirmRenamePlan);
        CancelRenamePlanCommand = new DelegateCommand(cancelRenamePlan);
        mBeginDeletePlanCommand = new DelegateCommand(
            beginDeletePlan,
            canDeletePlan);
        ConfirmDeletePlanCommand = new DelegateCommand(confirmDeletePlan);
        CancelDeletePlanCommand = new DelegateCommand(cancelDeletePlan);
        mRetryAutosaveCommand = new DelegateCommand(
            retryAutosave,
            canRetryAutosave);
        mRetryRecommendationCommand = new DelegateCommand(
            requestRecommendationRefresh,
            canRetryRecommendation);

        mAutosaveQueue.StateChanged += onAutosaveStateChanged;
        refreshVisibleCourses();
        synchronizeCourseSelectionState();
        requestRecommendationRefresh();
    }

    public Task FlushAutosaveAsync(CancellationToken cancellationToken)
    {
        throwIfDisposed();
        return mAutosaveQueue.FlushAsync(cancellationToken);
    }

    public Task CompleteAutosaveAsync(CancellationToken cancellationToken)
    {
        throwIfDisposed();
        return mAutosaveQueue.CompleteAsync(cancellationToken);
    }

    public void Dispose()
    {
        if (mIsDisposed)
        {
            return;
        }

        mIsDisposed = true;
        mRecommendationCancellationSource.Cancel();
        mRecommendationCancellationSource.Dispose();
        mAutosaveQueue.StateChanged -= onAutosaveStateChanged;
    }

    internal void applyWorkspaceWidth(WorkspaceWidth workspaceWidth)
    {
        EWorkspaceLayoutMode newLayoutMode = WorkspaceLayoutPolicy.FindLayoutMode(
            workspaceWidth);
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

    private void activatePlan(PlanTabItem plan)
    {
        throwIfDisposed();
        if (plan == null)
        {
            return;
        }

        if (mIsRebuildingPlanItems)
        {
            return;
        }

        if (ReferenceEquals(mActivePlan, plan))
        {
            return;
        }

        requirePlanItem(plan);
        closePlanEditingState();
        mSession.ActivatePlan(plan.PlanId);
        mActivePlan = plan;
        raisePropertyChanged(nameof(ActivePlan));
        afterWorkspaceMutation();
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

    private void addPlan()
    {
        throwIfDisposed();
        int nextPlanNumber = Plans.Count + 1;
        mSession.AddPlan(
            PlanId.CreateNew(),
            new PlanName("새 계획 " + nextPlanNumber));
        rebuildPlanItemsAndNotify();
        beginRenamePlan();
        afterWorkspaceMutation();
    }

    private void beginRenamePlan()
    {
        PlanNameDraft = ActivePlan.DisplayName;
        clearPlanNameValidationMessage();
        setRenamingPlanVisibility(true);
        setDeletePlanConfirmationVisibility(false);
    }

    private void confirmRenamePlan()
    {
        throwIfDisposed();
        if (IsRenamingPlan == false)
        {
            return;
        }

        try
        {
            PlanName newName = new PlanName(PlanNameDraft);
            mSession.RenamePlan(ActivePlan.PlanId, newName);
            rebuildPlanItemsAndNotify();
            setRenamingPlanVisibility(false);
            afterWorkspaceMutation();
        }
        catch (ArgumentException)
        {
            mPlanNameValidationMessage = "계획 이름은 1~80자로 입력해 주세요.";
            raisePropertyChanged(nameof(PlanNameValidationMessage));
            raisePropertyChanged(nameof(HasPlanNameValidationMessage));
        }
    }

    private void cancelRenamePlan()
    {
        PlanNameDraft = ActivePlan.DisplayName;
        clearPlanNameValidationMessage();
        setRenamingPlanVisibility(false);
    }

    private void beginDeletePlan()
    {
        if (CanDeleteActivePlan == false)
        {
            return;
        }

        setDeletePlanConfirmationVisibility(true);
        setRenamingPlanVisibility(false);
    }

    private void confirmDeletePlan()
    {
        throwIfDisposed();
        if (IsDeletePlanConfirmationVisible == false
            || CanDeleteActivePlan == false)
        {
            return;
        }

        mSession.RemovePlan(ActivePlan.PlanId);
        rebuildPlanItemsAndNotify();
        setDeletePlanConfirmationVisibility(false);
        afterWorkspaceMutation();
    }

    private void cancelDeletePlan()
    {
        setDeletePlanConfirmationVisibility(false);
    }

    private bool canDeletePlan()
    {
        return CanDeleteActivePlan;
    }

    private void selectPreviousRecommendation()
    {
        if (canNavigateRecommendations() == false)
        {
            return;
        }

        --mRecommendationIndex;
        if (mRecommendationIndex < 0)
        {
            mRecommendationIndex = mRecommendations.Count - 1;
        }

        notifyRecommendationChanged();
    }

    private void selectNextRecommendation()
    {
        if (canNavigateRecommendations() == false)
        {
            return;
        }

        ++mRecommendationIndex;
        if (mRecommendationIndex >= mRecommendations.Count)
        {
            mRecommendationIndex = 0;
        }

        notifyRecommendationChanged();
    }

    private bool canNavigateRecommendations()
    {
        return mRecommendations.Count > 1;
    }

    private void toggleCoursePane()
    {
        IsCoursePaneOpen = IsCoursePaneOpen == false;
    }

    private void toggleInspectorPane()
    {
        IsInspectorPaneOpen = IsInspectorPaneOpen == false;
    }

    private void retryAutosave()
    {
        mAutosaveQueue.RequestSave(mSession.Workspace);
    }

    private void closePlanEditingState()
    {
        setRenamingPlanVisibility(false);
        setDeletePlanConfirmationVisibility(false);
        clearPlanNameValidationMessage();
    }

    private bool canRetryAutosave()
    {
        return HasAutosaveError;
    }

    private bool canRetryRecommendation()
    {
        return HasRecommendationCalculationError;
    }

    private void afterPlanContentMutation()
    {
        rebuildPlanItemsAndNotify();
        afterWorkspaceMutation();
    }

    private void afterWorkspaceMutation()
    {
        synchronizeCourseSelectionState();
        requestRecommendationRefresh();
        mAutosaveQueue.RequestSave(mSession.Workspace);
    }

    private void rebuildPlanItemsAndNotify()
    {
        rebuildPlanItems();
        mActivePlan = findPlanItem(mSession.Workspace.ActivePlanId);
        mPlanNameDraft = mActivePlan.DisplayName;
        raisePropertyChanged(nameof(ActivePlan));
        raisePropertyChanged(nameof(PlanNameDraft));
        raisePropertyChanged(nameof(CanDeleteActivePlan));
        mBeginDeletePlanCommand.NotifyCanExecuteChanged();
    }

    private void rebuildPlanItems()
    {
        mIsRebuildingPlanItems = true;
        try
        {
            Plans.Clear();
            foreach (PlanningPlan plan in mSession.Workspace.Plans)
            {
                Plans.Add(new PlanTabItem(plan, mCatalogProjection));
            }
        }
        finally
        {
            mIsRebuildingPlanItems = false;
        }
    }

    private PlanTabItem findPlanItem(PlanId planId)
    {
        foreach (PlanTabItem plan in Plans)
        {
            if (plan.PlanId == planId)
            {
                return plan;
            }
        }

        throw new InvalidOperationException(
            "The active planning session plan was not projected for the UI.");
    }

    private void requirePlanItem(PlanTabItem plan)
    {
        foreach (PlanTabItem candidate in Plans)
        {
            if (ReferenceEquals(candidate, plan))
            {
                return;
            }
        }

        throw new ArgumentException(
            "The active plan must belong to this workspace.",
            nameof(plan));
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

    private void requestRecommendationRefresh()
    {
        throwIfDisposed();
        mRecommendationCancellationSource.Cancel();
        mRecommendationCancellationSource.Dispose();
        CancellationTokenSource cancellationSource = new CancellationTokenSource();
        mRecommendationCancellationSource = cancellationSource;
        PlanningPlan planSnapshot = mSession.Workspace.GetActivePlan();

        mRecommendations = Array.Empty<PresentationScheduleRecommendation>();
        mRecommendationIndex = 0;
        mRecommendationCalculationState = ERecommendationCalculationState.Calculating;
        mRecommendationCalculationError = string.Empty;
        notifyRecommendationChanged();
        notifyRecommendationCalculationStateChanged();
        mRecommendationRefreshTask = calculateRecommendationsAsync(
            planSnapshot,
            cancellationSource);
    }

    private async Task calculateRecommendationsAsync(
        PlanningPlan planSnapshot,
        CancellationTokenSource cancellationSource)
    {
        try
        {
            ScheduleRecommendationLimit recommendationLimit =
                new ScheduleRecommendationLimit(DISPLAYED_RECOMMENDATION_COUNT);
            ScheduleRecommendationResult result = await Task.Run(
                delegate
                {
                    return mRecommendationProvider.Generate(
                        planSnapshot,
                        recommendationLimit,
                        cancellationSource.Token);
                },
                cancellationSource.Token).ConfigureAwait(false);

            if (cancellationSource.IsCancellationRequested)
            {
                return;
            }

            await Dispatcher.UIThread.InvokeAsync(
                delegate
                {
                    if (canApplyRecommendationResult(cancellationSource))
                    {
                        applyRecommendationResult(result);
                    }
                });
        }
        catch (OperationCanceledException)
            when (cancellationSource.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            await Dispatcher.UIThread.InvokeAsync(
                delegate
                {
                    if (canApplyRecommendationResult(cancellationSource))
                    {
                        showRecommendationFailure(exception);
                    }
                });
        }
    }

    private bool canApplyRecommendationResult(
        CancellationTokenSource cancellationSource)
    {
        return mIsDisposed == false
            && cancellationSource.IsCancellationRequested == false
            && ReferenceEquals(
                mRecommendationCancellationSource,
                cancellationSource);
    }

    private void applyRecommendationResult(ScheduleRecommendationResult result)
    {
        if (result.HasValidationError)
        {
            throw new InvalidOperationException(
                "The active plan stopped matching its verified catalog: "
                + result.ValidationError
                + ".");
        }

        if (result.Completion == EScheduleRecommendationCompletion.Canceled)
        {
            throw new InvalidOperationException(
                "Recommendation calculation ended without an active cancellation request.");
        }

        List<PresentationScheduleRecommendation> recommendations =
            new List<PresentationScheduleRecommendation>();
        foreach (ApplicationScheduleRecommendation recommendation
            in result.Recommendations)
        {
            recommendations.Add(ScheduleRecommendationProjector.Project(
                recommendation,
                mCatalogProjection));
        }

        mRecommendations = recommendations.AsReadOnly();
        mRecommendationIndex = 0;
        mRecommendationCalculationState = ERecommendationCalculationState.Ready;
        mRecommendationCalculationError = string.Empty;
        notifyRecommendationChanged();
        notifyRecommendationCalculationStateChanged();
    }

    private void showRecommendationFailure(Exception exception)
    {
        mRecommendations = Array.Empty<PresentationScheduleRecommendation>();
        mRecommendationIndex = 0;
        mRecommendationCalculationState = ERecommendationCalculationState.Failed;
        mRecommendationCalculationError =
            "과목 선택은 그대로 보존했습니다. 잠시 후 다시 계산해 주세요.";
        System.Diagnostics.Debug.WriteLine(exception);
        notifyRecommendationChanged();
        notifyRecommendationCalculationStateChanged();
    }

    private void notifyRecommendationCalculationStateChanged()
    {
        raisePropertyChanged(nameof(IsRecommendationCalculating));
        raisePropertyChanged(nameof(HasRecommendationCalculationError));
        raisePropertyChanged(nameof(RecommendationCalculationError));
        mRetryRecommendationCommand.NotifyCanExecuteChanged();
    }

    private void notifyRecommendationChanged()
    {
        raisePropertyChanged(nameof(ActiveRecommendation));
        raisePropertyChanged(nameof(RecommendationSummary));
        raisePropertyChanged(nameof(HasRecommendations));
        raisePropertyChanged(nameof(HasScheduleEntries));
        raisePropertyChanged(nameof(IsScheduleEmpty));
        raisePropertyChanged(nameof(RecommendationInsight));
        raisePropertyChanged(nameof(EmptyScheduleTitle));
        raisePropertyChanged(nameof(EmptyScheduleMessage));
        mPreviousRecommendationCommand.NotifyCanExecuteChanged();
        mNextRecommendationCommand.NotifyCanExecuteChanged();
    }

    private void onAutosaveStateChanged(
        object? senderOrNull,
        PlanningWorkspaceAutosaveStateChangedEventArgs eventArgs)
    {
        PlanningWorkspaceAutosaveState state = eventArgs.State;
        Dispatcher.UIThread.Post(
            delegate
            {
                if (mIsDisposed == false)
                {
                    applyAutosaveState(state);
                }
            });
    }

    private void applyAutosaveState(PlanningWorkspaceAutosaveState state)
    {
        mAutosaveStatus = state.Status;
        switch (state.Status)
        {
            case EPlanningWorkspaceAutosaveStatus.Saving:
                mAutosaveStatusText = "저장 중...";
                break;
            case EPlanningWorkspaceAutosaveStatus.Saved:
                mAutosaveStatusText = "자동 저장됨";
                break;
            case EPlanningWorkspaceAutosaveStatus.Failed:
                mAutosaveStatusText = "저장하지 못함";
                break;
            default:
                throw new ArgumentOutOfRangeException(
                    nameof(state),
                    state.Status,
                    "Unknown autosave state.");
        }

        raisePropertyChanged(nameof(AutosaveStatus));
        raisePropertyChanged(nameof(AutosaveStatusText));
        raisePropertyChanged(nameof(IsAutosaveSaved));
        raisePropertyChanged(nameof(IsAutosaveSaving));
        raisePropertyChanged(nameof(HasAutosaveError));
        mRetryAutosaveCommand.NotifyCanExecuteChanged();
    }

    private void setRenamingPlanVisibility(bool isVisible)
    {
        setProperty(ref mIsRenamingPlan, isVisible, nameof(IsRenamingPlan));
    }

    private void setDeletePlanConfirmationVisibility(bool isVisible)
    {
        setProperty(
            ref mIsDeletePlanConfirmationVisible,
            isVisible,
            nameof(IsDeletePlanConfirmationVisible));
    }

    private void clearPlanNameValidationMessage()
    {
        if (string.IsNullOrEmpty(mPlanNameValidationMessage))
        {
            return;
        }

        mPlanNameValidationMessage = string.Empty;
        raisePropertyChanged(nameof(PlanNameValidationMessage));
        raisePropertyChanged(nameof(HasPlanNameValidationMessage));
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
                throw new ArgumentOutOfRangeException(
                    nameof(LayoutMode),
                    LayoutMode,
                    "Unknown workspace layout mode.");
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

    private void throwIfDisposed()
    {
        if (mIsDisposed)
        {
            throw new ObjectDisposedException(nameof(PlannerWorkspaceViewModel));
        }
    }
}
