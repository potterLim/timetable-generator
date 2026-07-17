using System;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;

using Avalonia.Controls;

using TimetableGenerator.Application.Planning;
using TimetableGenerator.Application.Scheduling;
using TimetableGenerator.Desktop.Presentation.Catalog;
using TimetableGenerator.Desktop.Presentation.Layout;
using TimetableGenerator.Desktop.Presentation.Models;
using TimetableGenerator.Desktop.Presentation.Recommendations;
using TimetableGenerator.Desktop.Storage;
using TimetableGenerator.Domain.Catalogs;
using PresentationScheduleRecommendation =
    TimetableGenerator.Desktop.Presentation.Models.ScheduleRecommendation;

namespace TimetableGenerator.Desktop.Presentation.ViewModels;

internal sealed partial class PlannerWorkspaceViewModel : ObservableObject, IDisposable
{
    private readonly CourseCatalogProjection mCatalogProjection;

    private readonly PlanningWorkspaceSession mSession;

    private bool mIsDisposed;

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

    public string AcademicTermDisplayText
    {
        get
        {
            return mCatalogProjection.Document.Catalog.Term.Id;
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
        mPersonalSchedulePreview = EMPTY_RECOMMENDATION;

        VisibleCourses = new ObservableCollection<CourseSearchItem>();
        CourseChoiceDraftCourses =
            new ObservableCollection<CourseChoiceDraftCourseItem>();
        AlternativeCourseSearchResults =
            new ObservableCollection<CourseChoiceAlternativeSearchItem>();
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
        RemoveTimeNotProvidedCourseCommand =
            new ParameterizedCommand<TimeNotProvidedCourseItem>(
                removeTimeNotProvidedCourse);
        BeginEditCourseChoiceGroupCommand =
            new ParameterizedCommand<PlanCourseChoiceGroupItem>(
                beginEditCourseChoiceGroup);
        RemoveCourseChoiceGroupCommand =
            new ParameterizedCommand<PlanCourseChoiceGroupItem>(
                removeCourseChoiceGroup);
        RemoveCourseChoiceDraftCourseCommand =
            new ParameterizedCommand<CourseChoiceDraftCourseItem>(
                removeCourseChoiceDraftCourse);
        AddAlternativeCourseCommand =
            new ParameterizedCommand<CourseChoiceAlternativeSearchItem>(
                addAlternativeCourse);
        mSaveCourseChoiceCommand = new DelegateCommand(
            saveCourseChoice,
            delegate
            {
                return CanSaveCourseChoice;
            });
        CancelCourseChoiceEditCommand = new DelegateCommand(
            cancelCourseChoiceEdit);
        BeginAddPersonalScheduleCommand = new DelegateCommand(
            beginAddPersonalSchedule);
        BeginEditPersonalScheduleCommand =
            new ParameterizedCommand<PersonalScheduleItem>(
                beginEditPersonalSchedule);
        SavePersonalScheduleCommand = new DelegateCommand(savePersonalSchedule);
        CancelPersonalScheduleEditCommand = new DelegateCommand(
            cancelPersonalScheduleEdit);
        BeginDeletePersonalScheduleCommand =
            new ParameterizedCommand<PersonalScheduleItem>(
                beginDeletePersonalSchedule);
        ConfirmDeletePersonalScheduleCommand = new DelegateCommand(
            confirmDeletePersonalSchedule);
        CancelDeletePersonalScheduleCommand = new DelegateCommand(
            cancelDeletePersonalSchedule);
        AddPlanCommand = new DelegateCommand(addPlan);
        mPreviousRecommendationCommand = new DelegateCommand(
            selectPreviousRecommendation,
            canNavigateRecommendations);
        mNextRecommendationCommand = new DelegateCommand(
            selectNextRecommendation,
            canNavigateRecommendations);
        ToggleCoursePaneCommand = new DelegateCommand(toggleCoursePane);
        ToggleInspectorPaneCommand = new DelegateCommand(toggleInspectorPane);
        OpenInspectorPaneCommand = new DelegateCommand(openInspectorPane);
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

    private void throwIfDisposed()
    {
        if (mIsDisposed)
        {
            throw new ObjectDisposedException(nameof(PlannerWorkspaceViewModel));
        }
    }
}
