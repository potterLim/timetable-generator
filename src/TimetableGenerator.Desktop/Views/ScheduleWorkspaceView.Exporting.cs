using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

using TimetableGenerator.Desktop.Exporting;
using TimetableGenerator.Desktop.Exporting.AppleCalendar;
using TimetableGenerator.Desktop.Exporting.Calendar;
using TimetableGenerator.Desktop.Integrations.GoogleCalendar;
using TimetableGenerator.Desktop.Presentation;
using TimetableGenerator.Desktop.Presentation.Models;
using TimetableGenerator.Desktop.Presentation.ViewModels;
using TimetableGenerator.Domain.Planning;

namespace TimetableGenerator.Desktop.Views;

internal sealed partial class ScheduleWorkspaceView
{
    private readonly IGoogleCalendarExporter mGoogleCalendarExporter;

    private readonly IGoogleCalendarWebNavigator mGoogleCalendarWebNavigator;

    private readonly IAppleCalendarExporter mAppleCalendarExporter;

    private readonly ICalendarTimeZoneProvider mCalendarTimeZoneProvider;

    private readonly AsyncDelegateCommand mExportGoogleCalendarCommand;

    private readonly AsyncDelegateCommand mExportAppleCalendarCommand;

    private readonly CancellationTokenSource mLifetimeCancellationSource;

    private CancellationTokenSource? mActiveExportCancellationSourceOrNull;

    private Task mExportResourceReleaseTask;

    private Exception? mExportResourceReleaseExceptionOrNull;

    private bool mIsExportInProgress;

    private bool mIsExportShutdownRequested;

    private bool mIsDetached;

    private bool mIsExportResourceReleaseStarted;

    public ICommand ExportGoogleCalendarCommand
    {
        get
        {
            return mExportGoogleCalendarCommand;
        }
    }

    public ICommand ExportAppleCalendarCommand
    {
        get
        {
            return mExportAppleCalendarCommand;
        }
    }

    public bool IsAppleCalendarExportAvailable
    {
        get
        {
            return mAppleCalendarExporter.IsAvailable;
        }
    }

    internal Task ExportResourceReleaseTask
    {
        get
        {
            return mExportResourceReleaseTask;
        }
    }

    internal Exception? ExportResourceReleaseExceptionOrNull
    {
        get
        {
            return mExportResourceReleaseExceptionOrNull;
        }
    }

    public ScheduleWorkspaceView()
        : this(ScheduleExportCompositionRoot.CreateDefault(), EXPORT_STATUS_DURATION)
    {
    }

    internal ScheduleWorkspaceView(ScheduleExportServices exportServices)
        : this(exportServices, EXPORT_STATUS_DURATION)
    {
    }

    internal ScheduleWorkspaceView(ScheduleExportServices exportServices, TimeSpan exportStatusDuration)
    {
        if (exportServices == null)
        {
            throw new ArgumentNullException(nameof(exportServices));
        }

        if (exportStatusDuration <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(exportStatusDuration));
        }

        mPngExporter = exportServices.PngExporter;
        mGoogleCalendarExporter = exportServices.GoogleCalendarExporter;
        mGoogleCalendarWebNavigator = exportServices.GoogleCalendarWebNavigator;
        mAppleCalendarExporter = exportServices.AppleCalendarExporter;
        mCalendarTimeZoneProvider = exportServices.CalendarTimeZoneProvider;
        mLifetimeCancellationSource = new CancellationTokenSource();
        mExportStatusTimer.Interval = exportStatusDuration;
        mExportStatusTimer.Tick += onExportStatusTimerTick;
        mExportResourceReleaseTask = Task.CompletedTask;
        mPresentationMode = EScheduleWorkspacePresentationMode.Board;
        mExportPngCommand = new AsyncDelegateCommand(exportPngAsync, showPngExportFailure);
        mExportAllPngCommand = new AsyncDelegateCommand(exportAllPngAsync, showPngExportFailure);
        mExportGoogleCalendarCommand = new AsyncDelegateCommand(exportGoogleCalendarAsync, showGoogleCalendarExportFailure);
        mExportAppleCalendarCommand = new AsyncDelegateCommand(exportAppleCalendarAsync, showAppleCalendarExportFailure);
        mToggleSchedulePresentationCommand = new DelegateCommand(toggleSchedulePresentation);
        mEditPersonalScheduleCommand = new ParameterizedCommand<PersonalScheduleId>(beginEditPersonalSchedule);
        AvaloniaXamlLoader.Load(this);
        DataContextChanged += onDataContextChanged;
        observeWorkspace(DataContext as PlannerWorkspaceViewModel);
        DetachedFromVisualTree += onDetachedFromVisualTree;
    }

    private CalendarExportDocument createCalendarExportDocument(ECalendarExportProvider provider)
    {
        PlannerWorkspaceViewModel workspace = getRequiredWorkspace();
        PlanTabItem? activePlanOrNull = workspace.ActivePlanOrNull;
        ScheduleBoardPresentation? scheduleBoardOrNull = workspace.DisplayedScheduleBoard;
        if (activePlanOrNull == null || scheduleBoardOrNull == null)
        {
            throw new InvalidOperationException("Schedule export requires an active plan.");
        }

        AcademicTermCalendarMetadata academicCalendar = AcademicTermCalendarMetadataRegistry.findByTerm(scheduleBoardOrNull.AcademicTerm, mCalendarTimeZoneProvider.GetTimeZoneId());
        switch (provider)
        {
            case ECalendarExportProvider.Google:
                return ScheduleCalendarProjector.ProjectForGoogleCalendar(activePlanOrNull.PlanId, activePlanOrNull.Name, scheduleBoardOrNull.InstitutionName, workspace.DisplayedSchedule, academicCalendar);
            case ECalendarExportProvider.Apple:
                return ScheduleCalendarProjector.ProjectForAppleCalendar(activePlanOrNull.PlanId, activePlanOrNull.Name, scheduleBoardOrNull.InstitutionName, workspace.DisplayedSchedule, academicCalendar);
            case ECalendarExportProvider.None:
            default:
                throw new ArgumentOutOfRangeException(nameof(provider), provider, "Schedule exports require a supported calendar provider.");
        }
    }

    private PlannerWorkspaceViewModel getRequiredWorkspace()
    {
        PlannerWorkspaceViewModel? workspaceOrNull = DataContext as PlannerWorkspaceViewModel;
        if (workspaceOrNull == null)
        {
            throw new InvalidOperationException("Schedule export requires a planning workspace.");
        }

        return workspaceOrNull;
    }

    private bool tryBeginExportOperation()
    {
        PlannerWorkspaceViewModel? workspaceOrNull = DataContext as PlannerWorkspaceViewModel;
        if (mIsExportInProgress
            || mIsExportShutdownRequested
            || mIsDetached
            || workspaceOrNull == null
            || workspaceOrNull.CanExportSchedule == false)
        {
            return false;
        }

        if (mActiveExportCancellationSourceOrNull != null)
        {
            throw new InvalidOperationException("A previous schedule export cancellation source was not released.");
        }

        mActiveExportCancellationSourceOrNull = CancellationTokenSource.CreateLinkedTokenSource(mLifetimeCancellationSource.Token);
        mIsExportInProgress = true;
        disableExportButton();
        clearExportStatus();
        return true;
    }

    private void completeExportOperation()
    {
        CancellationTokenSource? activeExportCancellationSourceOrNull = mActiveExportCancellationSourceOrNull;
        mActiveExportCancellationSourceOrNull = null;
        activeExportCancellationSourceOrNull?.Dispose();
        mIsExportInProgress = false;
        if (mIsExportShutdownRequested == false && mIsDetached == false)
        {
            enableExportButton();
        }
    }

    private CancellationToken getActiveExportCancellationToken()
    {
        CancellationTokenSource? activeExportCancellationSourceOrNull = mActiveExportCancellationSourceOrNull;
        if (mIsExportInProgress == false || activeExportCancellationSourceOrNull == null)
        {
            throw new InvalidOperationException("Schedule export cancellation is unavailable outside an active export.");
        }

        return activeExportCancellationSourceOrNull.Token;
    }

    private void disableExportButton()
    {
        Button? exportButtonOrNull = this.FindControl<Button>("ExportScheduleButton");
        if (exportButtonOrNull != null)
        {
            exportButtonOrNull.SetCurrentValue(IsEnabledProperty, false);
        }
    }

    private void enableExportButton()
    {
        Button? exportButtonOrNull = this.FindControl<Button>("ExportScheduleButton");
        if (exportButtonOrNull != null)
        {
            exportButtonOrNull.SetCurrentValue(IsEnabledProperty, true);
        }
    }

    private void onDetachedFromVisualTree(object? senderOrNull, VisualTreeAttachmentEventArgs eventArgs)
    {
        if (mIsDetached)
        {
            return;
        }

        mIsDetached = true;
        DetachedFromVisualTree -= onDetachedFromVisualTree;
        DataContextChanged -= onDataContextChanged;
        observeWorkspace(null);
        mExportStatusTimer.Stop();
        mExportStatusTimer.Tick -= onExportStatusTimerTick;
        beginExportShutdown();
        cancelCancellationSource(mLifetimeCancellationSource);
        if (mIsExportResourceReleaseStarted == false)
        {
            mIsExportResourceReleaseStarted = true;
            mExportResourceReleaseTask = releaseExportResourcesAsync();
        }
    }

    private async Task releaseExportResourcesAsync()
    {
        Exception? releaseExceptionOrNull = null;
        try
        {
            await waitForExportOperationsAsync();
        }
        catch (Exception exception)
        {
            releaseExceptionOrNull = exception;
        }
        finally
        {
            try
            {
                mGoogleCalendarExporter.Dispose();
            }
            catch (Exception exception)
            {
                if (releaseExceptionOrNull == null)
                {
                    releaseExceptionOrNull = exception;
                }
                else
                {
                    releaseExceptionOrNull = new AggregateException(releaseExceptionOrNull, exception);
                }
            }

            CancellationTokenSource? activeExportCancellationSourceOrNull = mActiveExportCancellationSourceOrNull;
            mActiveExportCancellationSourceOrNull = null;
            activeExportCancellationSourceOrNull?.Dispose();
            mLifetimeCancellationSource.Dispose();
            mExportResourceReleaseExceptionOrNull = releaseExceptionOrNull;
        }
    }

    internal void beginExportShutdown()
    {
        blockNewExportsForShutdown();
        cancelCancellationSource(mActiveExportCancellationSourceOrNull);
    }

    internal void blockNewExportsForShutdown()
    {
        if (mIsExportShutdownRequested)
        {
            return;
        }

        mIsExportShutdownRequested = true;
        disableExportButton();
    }

    internal async Task completeExportShutdownAsync(CancellationToken cancellationToken)
    {
        beginExportShutdown();
        await waitForExportOperationsAsync().WaitAsync(cancellationToken);
    }

    internal void cancelExportShutdown()
    {
        if (mIsDetached || mIsExportShutdownRequested == false)
        {
            return;
        }

        mIsExportShutdownRequested = false;
        if (mIsExportInProgress == false)
        {
            enableExportButton();
        }
    }

    private Task waitForExportOperationsAsync()
    {
        return Task.WhenAll(
            mExportPngCommand.ExecutionTask,
            mExportAllPngCommand.ExecutionTask,
            mExportGoogleCalendarCommand.ExecutionTask,
            mExportAppleCalendarCommand.ExecutionTask);
    }

    private static void cancelCancellationSource(CancellationTokenSource? cancellationSourceOrNull)
    {
        try
        {
            cancellationSourceOrNull?.Cancel();
        }
        catch (Exception exception)
        {
            Trace.TraceWarning("Schedule export cancellation continued after a cancellation callback failed: {0}", exception);
        }
    }
}
