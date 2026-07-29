using System;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;

using Avalonia.Controls;
using Avalonia.Threading;

using TimetableGenerator.Application.Planning;
using TimetableGenerator.Desktop.Presentation.Catalog;
using TimetableGenerator.Desktop.Presentation.Layout;
using TimetableGenerator.Desktop.Presentation.Models;
using TimetableGenerator.Desktop.Presentation.Recommendations;
using TimetableGenerator.Desktop.Storage;
using TimetableGenerator.Domain.Catalogs;
using TimetableGenerator.Domain.Planning;

namespace TimetableGenerator.Desktop.Presentation.ViewModels;

internal sealed partial class PlannerWorkspaceViewModel : ObservableObject, IDisposable
{
    private readonly CourseCatalogProjection mCatalogProjection;

    private readonly PlanningWorkspaceSession mSession;

    private readonly RecommendationCalculationPolicy mRecommendationCalculationPolicy;

    private bool mIsDisposed;

    public string InstitutionTermDisplayText
    {
        get
        {
            CourseCatalog catalog = mCatalogProjection.Document.Catalog;
            return catalog.InstitutionName.Value + " · " + catalog.Term.Id;
        }
    }

    public string InstitutionName
    {
        get
        {
            return mCatalogProjection.Document.Catalog.InstitutionName.Value;
        }
    }

    public string AcademicTermDisplayText
    {
        get
        {
            return mCatalogProjection.Document.Catalog.Term.Id;
        }
    }

    public PlannerWorkspaceViewModel(CourseCatalogProjection catalogProjection, PlanningWorkspaceSession session, PlanningWorkspaceAutosaveQueue autosaveQueue, IScheduleRecommendationProvider recommendationProvider)
        : this(catalogProjection, session, autosaveQueue, recommendationProvider, RecommendationCalculationPolicy.Default)
    {
    }

    internal PlannerWorkspaceViewModel(
        CourseCatalogProjection catalogProjection,
        PlanningWorkspaceSession session,
        PlanningWorkspaceAutosaveQueue autosaveQueue,
        IScheduleRecommendationProvider recommendationProvider,
        RecommendationCalculationPolicy recommendationCalculationPolicy)
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

        if (recommendationCalculationPolicy == null)
        {
            throw new ArgumentNullException(nameof(recommendationCalculationPolicy));
        }

        if (ReferenceEquals(catalogProjection.Document.Catalog, session.Catalog) == false)
        {
            throw new ArgumentException("The workspace session and presentation projection must share a catalog.", nameof(session));
        }

        mCatalogProjection = catalogProjection;
        mSession = session;
        mAutosaveQueue = autosaveQueue;
        mRecommendationProvider = recommendationProvider;
        mRecommendationCalculationPolicy = recommendationCalculationPolicy;
        mAllCourses = createCourseItems(catalogProjection);
        mAlternativeCourseSearchItemsByCourseId = createAlternativeCourseSearchItemsByCourseId(mAllCourses);
        mRecommendations = Array.Empty<ScheduleRecommendationViewItem>();
        mPngExportCandidateSchedules = Array.Empty<ScheduleRecommendation>();
        mPersonalSchedulePreview = EMPTY_RECOMMENDATION;
        mRecommendationDayRange = ScheduleBoardDayRange.CreateForEntries(EMPTY_RECOMMENDATION.Entries);

        VisibleCourses = new ObservableCollection<CourseSearchItem>();
        CourseChoiceDraftCourses = new ObservableCollection<CourseChoiceDraftCourseItem>();
        AlternativeCourseSearchResults = new ObservableCollection<CourseChoiceAlternativeSearchItem>();
        UnitFilters = createUnitFilters(catalogProjection);
        RequirementFilters = createRequirementFilters(catalogProjection);
        Plans = new ObservableCollection<PlanTabItem>();

        mSearchText = string.Empty;
        mSelectedUnitFilter = UnitFilters[0];
        mSelectedRequirementFilter = RequirementFilters[0];
        rebuildPlanItems();
        mActivePlanOrNull = findPlanItemOrNull(mSession.Workspace.ActivePlanIdOrNull);
        mPlanNameDraft = string.Empty;
        if (mActivePlanOrNull != null)
        {
            mPlanNameDraft = mActivePlanOrNull.DisplayName;
        }

        mPlanNameValidationMessage = string.Empty;
        mAutosaveStatus = EPlanningWorkspaceAutosaveStatus.Saved;
        mAutosaveStatusText = string.Empty;
        mAutosaveSavingIndicatorTimer = new DispatcherTimer();
        mAutosaveSavingIndicatorTimer.Interval = AUTOSAVE_SAVING_INDICATOR_DELAY;
        mAutosaveSavingIndicatorTimer.Tick += onAutosaveSavingIndicatorTimerTick;
        mRecommendationCancellationSource = new CancellationTokenSource();
        mRecommendationRefreshTask = Task.CompletedTask;
        mRecommendationCalculationState = ERecommendationCalculationState.Ready;
        mRecommendationCalculationError = string.Empty;
        mRecommendationExpansionState = ERecommendationExpansionState.Unavailable;
        mLayoutMode = EWorkspaceLayoutMode.ExtraWide;
        mIsCoursePaneOpen = true;
        mIsInspectorPaneOpen = true;
        mCoursePaneDisplayMode = SplitViewDisplayMode.Inline;
        mInspectorPaneDisplayMode = SplitViewDisplayMode.Inline;
        mCoursePaneWidth = EXTRA_WIDE_COURSE_PANE_WIDTH;
        mInspectorPaneWidth = EXTRA_WIDE_INSPECTOR_PANE_WIDTH;
        mPersonalScheduleDayOptions = createPersonalScheduleDayOptions();
        mPersonalScheduleTitleDraft = string.Empty;
        mPersonalScheduleSectionDraft = string.Empty;
        mPersonalScheduleInstructorDraft = string.Empty;
        mPersonalScheduleLocationDraft = string.Empty;
        mPersonalScheduleValidationError = EPersonalScheduleDraftValidationError.None;
        mPersonalScheduleStartTimeOrNull = DEFAULT_PERSONAL_SCHEDULE_START_TIME;
        mPersonalScheduleEndTimeOrNull = DEFAULT_PERSONAL_SCHEDULE_END_TIME;
        mAlternativeCourseSearchText = string.Empty;

        AddCourseCommand = new ParameterizedCommand<CourseSearchItem>(addCourse);
        RemoveTimeNotProvidedCourseCommand = new ParameterizedCommand<TimeNotProvidedCourseItem>(removeTimeNotProvidedCourse);
        BeginEditCourseChoiceGroupCommand = new ParameterizedCommand<PlanCourseChoiceGroupItem>(beginEditCourseChoiceGroup);
        RemoveCourseChoiceGroupCommand = new ParameterizedCommand<PlanCourseChoiceGroupItem>(removeCourseChoiceGroup);
        RemoveCourseChoiceDraftCourseCommand = new ParameterizedCommand<CourseChoiceDraftCourseItem>(removeCourseChoiceDraftCourse);
        AddAlternativeCourseCommand = new ParameterizedCommand<CourseChoiceAlternativeSearchItem>(addAlternativeCourse);
        mSaveCourseChoiceCommand = new DelegateCommand(
            saveCourseChoice,
            delegate
            {
                return CanSaveCourseChoice;
            });
        CancelCourseChoiceEditCommand = new DelegateCommand(cancelCourseChoiceEdit);
        BeginAddPersonalScheduleCommand = new DelegateCommand(beginAddPersonalSchedule);
        BeginEditPersonalScheduleCommand = new ParameterizedCommand<PersonalScheduleId>(beginEditPersonalSchedule);
        SavePersonalScheduleCommand = new DelegateCommand(savePersonalSchedule);
        CancelPersonalScheduleEditCommand = new DelegateCommand(cancelPersonalScheduleEdit);
        BeginDeletePersonalScheduleCommand = new ParameterizedCommand<PersonalScheduleItem>(beginDeletePersonalSchedule);
        ConfirmDeletePersonalScheduleCommand = new DelegateCommand(confirmDeletePersonalSchedule);
        CancelDeletePersonalScheduleCommand = new DelegateCommand(cancelDeletePersonalSchedule);
        AddPlanCommand = new DelegateCommand(beginCreatePlan);
        mPreviousRecommendationCommand = new DelegateCommand(selectPreviousRecommendation, canNavigateRecommendations);
        mNextRecommendationCommand = new DelegateCommand(selectNextRecommendation, canNavigateRecommendations);
        ToggleCoursePaneCommand = new DelegateCommand(toggleCoursePane);
        OpenInspectorPaneCommand = new DelegateCommand(openInspectorPane);
        CloseInspectorPaneCommand = new DelegateCommand(closeInspectorPane);
        BeginRenamePlanCommand = new DelegateCommand(beginRenamePlan);
        ConfirmPlanNameCommand = new DelegateCommand(confirmPlanName);
        CancelPlanNameCommand = new DelegateCommand(cancelPlanNameEditing);
        mBeginDeletePlanCommand = new DelegateCommand(beginDeletePlan, canDeletePlan);
        ConfirmDeletePlanCommand = new DelegateCommand(confirmDeletePlan);
        CancelDeletePlanCommand = new DelegateCommand(cancelDeletePlan);
        mBeginClearActivePlanCommand = new DelegateCommand(beginClearActivePlan, canClearActivePlan);
        ConfirmClearActivePlanCommand = new DelegateCommand(confirmClearActivePlan);
        CancelClearActivePlanCommand = new DelegateCommand(cancelClearActivePlan);
        mRetryAutosaveCommand = new DelegateCommand(retryAutosave, canRetryAutosave);
        mRetryRecommendationCommand = new DelegateCommand(requestRecommendationRefresh, canRetryRecommendation);
        mCalculateAllRecommendationsCommand = new DelegateCommand(calculateAllRecommendations, canCalculateAllRecommendations);
        mCancelAllRecommendationsCommand = new DelegateCommand(cancelAllRecommendations, canCancelAllRecommendations);

        mAutosaveQueue.StateChanged += onAutosaveStateChanged;
        refreshVisibleCourses();
        synchronizeCourseSelectionState();
        requestRecommendationRefresh();
    }

    public void Dispose()
    {
        if (mIsDisposed)
        {
            return;
        }

        mIsDisposed = true;
        mAutosaveSavingIndicatorTimer.Stop();
        mAutosaveSavingIndicatorTimer.Tick -= onAutosaveSavingIndicatorTimerTick;
        mRecommendationCancellationSource.Cancel();
        mRecommendationCancellationSource.Dispose();
        mExhaustiveRecommendationCancellationSourceOrNull?.Cancel();
        mAutosaveQueue.StateChanged -= onAutosaveStateChanged;
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

    private void afterWorkspaceMetadataMutation()
    {
        raisePropertyChanged(nameof(DisplayedScheduleBoard));
        mAutosaveQueue.RequestSave(mSession.Workspace);
    }

    private void throwIfDisposed()
    {
        if (mIsDisposed)
        {
            throw new ObjectDisposedException(nameof(PlannerWorkspaceViewModel));
        }
    }
}
