using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;

using FluentIcons.Avalonia;
using FluentIcons.Common;

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
    private static readonly TimeSpan EXPORT_STATUS_DURATION =
        TimeSpan.FromSeconds(3.5);

    private readonly IGoogleCalendarExporter mGoogleCalendarExporter;

    private readonly IAppleCalendarImporter mAppleCalendarImporter;

    private readonly IcsCalendarFileStore mIcsFileStore;

    private readonly ICalendarExportClock mCalendarExportClock;

    private readonly ICalendarTimeZoneProvider mCalendarTimeZoneProvider;

    private readonly AsyncDelegateCommand mExportGoogleCalendarCommand;

    private readonly AsyncDelegateCommand mExportAppleCalendarCommand;

    private readonly CancellationTokenSource mLifetimeCancellationSource;

    private readonly DispatcherTimer mExportStatusTimer;

    private Task mExportResourceReleaseTask;

    private Exception? mExportResourceReleaseExceptionOrNull;

    private bool mIsExportInProgress;

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
            return mAppleCalendarImporter.IsAvailable;
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
        : this(ScheduleExportCompositionRoot.CreateDefault())
    {
    }

    internal ScheduleWorkspaceView(ScheduleExportServices exportServices)
    {
        if (exportServices == null)
        {
            throw new ArgumentNullException(nameof(exportServices));
        }

        mPngExporter = exportServices.PngExporter;
        mGoogleCalendarExporter = exportServices.GoogleCalendarExporter;
        mAppleCalendarImporter = exportServices.AppleCalendarImporter;
        mIcsFileStore = exportServices.IcsFileStore;
        mCalendarExportClock = exportServices.Clock;
        mCalendarTimeZoneProvider = exportServices.CalendarTimeZoneProvider;
        mLifetimeCancellationSource = new CancellationTokenSource();
        mExportStatusTimer = new DispatcherTimer();
        mExportStatusTimer.Interval = EXPORT_STATUS_DURATION;
        mExportStatusTimer.Tick += onExportStatusTimerTick;
        mExportResourceReleaseTask = Task.CompletedTask;
        mPresentationMode = EScheduleWorkspacePresentationMode.Board;
        mExportPngCommand = new AsyncDelegateCommand(
            exportPngAsync,
            showPngExportFailure);
        mExportAllPngCommand = new AsyncDelegateCommand(
            exportAllPngAsync,
            showPngExportFailure);
        mExportGoogleCalendarCommand = new AsyncDelegateCommand(
            exportGoogleCalendarAsync,
            showGoogleCalendarExportFailure);
        mExportAppleCalendarCommand = new AsyncDelegateCommand(
            exportAppleCalendarAsync,
            showAppleCalendarExportFailure);
        mToggleSchedulePresentationCommand = new DelegateCommand(
            toggleSchedulePresentation);
        mEditPersonalScheduleCommand =
            new ParameterizedCommand<PersonalScheduleId>(
                beginEditPersonalSchedule);
        AvaloniaXamlLoader.Load(this);
        DetachedFromVisualTree += onDetachedFromVisualTree;
    }

    private async Task exportGoogleCalendarAsync()
    {
        if (tryBeginExportOperation() == false)
        {
            return;
        }

        try
        {
            CancellationToken cancellationToken =
                mLifetimeCancellationSource.Token;
            cancellationToken.ThrowIfCancellationRequested();
            CalendarExportDocument document = createCalendarExportDocument();
            GoogleCalendarExportPlan exportPlan =
                GoogleCalendarExportPlan.CreateFromDocument(document);
            showPersistentExportStatus(
                "Google 캘린더에 시간표를 반영하는 중입니다: '"
                    + document.CalendarName.Value
                    + "'",
                EExportStatus.Information);
            GoogleCalendarExportResult result =
                await mGoogleCalendarExporter.ExportAsync(
                    exportPlan,
                    cancellationToken);
            showGoogleCalendarExportResult(
                result,
                document.CalendarName);
        }
        finally
        {
            completeExportOperation();
        }
    }

    private async Task exportAppleCalendarAsync()
    {
        if (tryBeginExportOperation() == false)
        {
            return;
        }

        try
        {
            CancellationToken cancellationToken =
                mLifetimeCancellationSource.Token;
            cancellationToken.ThrowIfCancellationRequested();
            CalendarExportDocument document = createCalendarExportDocument();
            IcsCalendarFilePath importFilePath = await mIcsFileStore.SaveAsync(
                document,
                mCalendarExportClock.GetCurrentTimestamp(),
                cancellationToken);
            await mAppleCalendarImporter.OpenImportAsync(
                importFilePath,
                cancellationToken);
            showTransientExportStatus(
                "Apple 캘린더에서 가져오기를 확인해 주세요.",
                EExportStatus.Information);
        }
        finally
        {
            completeExportOperation();
        }
    }

    private CalendarExportDocument createCalendarExportDocument()
    {
        PlannerWorkspaceViewModel workspace = getRequiredWorkspace();
        PlanTabItem? activePlanOrNull = workspace.ActivePlanOrNull;
        ScheduleBoardPresentation? scheduleBoardOrNull =
            workspace.DisplayedScheduleBoard;
        if (activePlanOrNull == null || scheduleBoardOrNull == null)
        {
            throw new InvalidOperationException(
                "Schedule export requires an active plan.");
        }

        AcademicTermCalendarMetadata academicCalendar =
            AcademicTermCalendarMetadataRegistry.findByTerm(
                scheduleBoardOrNull.AcademicTerm,
                mCalendarTimeZoneProvider.GetTimeZoneId());
        return ScheduleCalendarProjector.Project(
            activePlanOrNull.PlanId,
            activePlanOrNull.Name,
            workspace.DisplayedSchedule,
            academicCalendar);
    }

    private PlannerWorkspaceViewModel getRequiredWorkspace()
    {
        PlannerWorkspaceViewModel? workspaceOrNull =
            DataContext as PlannerWorkspaceViewModel;
        if (workspaceOrNull == null)
        {
            throw new InvalidOperationException(
                "Schedule export requires a planning workspace.");
        }

        return workspaceOrNull;
    }

    private bool tryBeginExportOperation()
    {
        PlannerWorkspaceViewModel? workspaceOrNull =
            DataContext as PlannerWorkspaceViewModel;
        if (mIsExportInProgress
            || workspaceOrNull == null
            || workspaceOrNull.CanExportSchedule == false)
        {
            return false;
        }

        mIsExportInProgress = true;
        disableExportButton();
        clearExportStatus();
        return true;
    }

    private void completeExportOperation()
    {
        mIsExportInProgress = false;
        enableExportButton();
    }

    private void disableExportButton()
    {
        Button? exportButtonOrNull =
            this.FindControl<Button>("ExportScheduleButton");
        if (exportButtonOrNull != null)
        {
            exportButtonOrNull.SetCurrentValue(
                IsEnabledProperty,
                false);
        }
    }

    private void enableExportButton()
    {
        Button? exportButtonOrNull =
            this.FindControl<Button>("ExportScheduleButton");
        if (exportButtonOrNull != null)
        {
            exportButtonOrNull.SetCurrentValue(
                IsEnabledProperty,
                true);
        }
    }

    private void showGoogleCalendarExportResult(
        GoogleCalendarExportResult result,
        PlanName calendarName)
    {
        if (result == null)
        {
            throw new ArgumentNullException(nameof(result));
        }

        if (calendarName == null)
        {
            throw new ArgumentNullException(nameof(calendarName));
        }

        switch (result.Status)
        {
            case EGoogleCalendarExportStatus.Success:
                showTransientExportStatus(
                    "Google 캘린더에 시간표를 반영했습니다: '"
                        + calendarName.Value
                        + "'",
                    EExportStatus.Success);
                break;
            case EGoogleCalendarExportStatus.NotConfigured:
                showTransientExportStatus(
                    "Google 캘린더 연결을 아직 사용할 수 없습니다.",
                    EExportStatus.Information);
                break;
            case EGoogleCalendarExportStatus.AuthenticationCancelled:
                showTransientExportStatus(
                    "Google 캘린더 연결을 취소했습니다.",
                    EExportStatus.Information);
                break;
            case EGoogleCalendarExportStatus.AuthenticationFailed:
                showTransientExportStatus(
                    "Google 캘린더 연결을 완료하지 못했습니다.",
                    EExportStatus.Failure);
                break;
            case EGoogleCalendarExportStatus.AccessDenied:
                showTransientExportStatus(
                    "Google 캘린더 권한을 확인해 주세요.",
                    EExportStatus.Failure);
                break;
            case EGoogleCalendarExportStatus.NetworkFailed:
                showTransientExportStatus(
                    "Google 캘린더에 연결하지 못했습니다. 네트워크를 확인해 주세요.",
                    EExportStatus.Failure);
                break;
            case EGoogleCalendarExportStatus.Failed:
                showTransientExportStatus(
                    "Google 캘린더에 반영하지 못했습니다. 다시 시도해 주세요.",
                    EExportStatus.Failure);
                break;
            case EGoogleCalendarExportStatus.None:
            default:
                throw new ArgumentOutOfRangeException(
                    nameof(result),
                    result.Status,
                    "Unknown Google Calendar export status.");
        }
    }

    private void showGoogleCalendarExportFailure(Exception exception)
    {
        showCalendarExportFailure(
            exception,
            "Google 캘린더에 반영하지 못했습니다. 다시 시도해 주세요.");
    }

    private void showAppleCalendarExportFailure(Exception exception)
    {
        showCalendarExportFailure(
            exception,
            "Apple 캘린더를 열지 못했습니다. 다시 시도해 주세요.");
    }

    private void showCalendarExportFailure(
        Exception exception,
        string fallbackMessage)
    {
        if (exception is NotSupportedException)
        {
            showTransientExportStatus(
                "이 학기는 캘린더 내보내기를 아직 지원하지 않습니다.",
                EExportStatus.Failure);
            return;
        }

        showExportFailure(exception, fallbackMessage);
    }

    private void showExportFailure(Exception exception, string message)
    {
        if (exception is OperationCanceledException)
        {
            clearExportStatus();
            return;
        }

        showTransientExportStatus(message, EExportStatus.Failure);
    }

    private void clearExportStatus()
    {
        mExportStatusTimer.Stop();

        Border? statusToastOrNull =
            this.FindControl<Border>("ExportStatusToast");
        TextBlock? statusTextOrNull =
            this.FindControl<TextBlock>("ExportStatusText");
        FluentIcon? statusIconOrNull =
            this.FindControl<FluentIcon>("ExportStatusIcon");
        if (statusToastOrNull == null
            || statusTextOrNull == null
            || statusIconOrNull == null)
        {
            return;
        }

        statusTextOrNull.Text = string.Empty;
        setExportStatusClasses(statusTextOrNull, null);
        setExportStatusClasses(statusToastOrNull, null);
        setExportStatusClasses(statusIconOrNull, null);
        statusToastOrNull.IsVisible = false;
    }

    private void showPersistentExportStatus(
        string message,
        EExportStatus status)
    {
        mExportStatusTimer.Stop();
        showExportStatusCore(message, status);
    }

    private void showTransientExportStatus(
        string message,
        EExportStatus status)
    {
        mExportStatusTimer.Stop();
        showExportStatusCore(message, status);
        mExportStatusTimer.Start();
    }

    private void showExportStatusCore(string message, EExportStatus status)
    {
        Border? statusToastOrNull =
            this.FindControl<Border>("ExportStatusToast");
        TextBlock? statusTextOrNull =
            this.FindControl<TextBlock>("ExportStatusText");
        FluentIcon? statusIconOrNull =
            this.FindControl<FluentIcon>("ExportStatusIcon");
        if (statusToastOrNull == null
            || statusTextOrNull == null
            || statusIconOrNull == null)
        {
            return;
        }

        Icon statusIcon;
        switch (status)
        {
            case EExportStatus.Success:
                statusIcon = Icon.CheckmarkCircle;
                break;
            case EExportStatus.Information:
                statusIcon = Icon.Info;
                break;
            case EExportStatus.Failure:
                statusIcon = Icon.Warning;
                break;
            default:
                throw new ArgumentOutOfRangeException(
                    nameof(status),
                    status,
                    "Unknown export status.");
        }

        setExportStatusClasses(statusTextOrNull, status);
        setExportStatusClasses(statusToastOrNull, status);
        setExportStatusClasses(statusIconOrNull, status);
        statusIconOrNull.Icon = statusIcon;
        statusToastOrNull.IsVisible = true;
        statusTextOrNull.Text = message;
    }

    private static void setExportStatusClasses(
        StyledElement element,
        EExportStatus? activeStatusOrNull)
    {
        element.Classes.Set(
            "success",
            activeStatusOrNull == EExportStatus.Success);
        element.Classes.Set(
            "information",
            activeStatusOrNull == EExportStatus.Information);
        element.Classes.Set(
            "error",
            activeStatusOrNull == EExportStatus.Failure);
    }

    private void onExportStatusTimerTick(
        object? senderOrNull,
        EventArgs eventArgs)
    {
        clearExportStatus();
    }

    private void onDetachedFromVisualTree(
        object? senderOrNull,
        VisualTreeAttachmentEventArgs eventArgs)
    {
        DetachedFromVisualTree -= onDetachedFromVisualTree;
        mExportStatusTimer.Stop();
        mExportStatusTimer.Tick -= onExportStatusTimerTick;
        mLifetimeCancellationSource.Cancel();
        mExportResourceReleaseTask = releaseExportResourcesAsync();
    }

    private async Task releaseExportResourcesAsync()
    {
        try
        {
            await Task.WhenAll(
                mExportPngCommand.ExecutionTask,
                mExportAllPngCommand.ExecutionTask,
                mExportGoogleCalendarCommand.ExecutionTask,
                mExportAppleCalendarCommand.ExecutionTask);
            mGoogleCalendarExporter.Dispose();
        }
        catch (Exception exception)
        {
            mExportResourceReleaseExceptionOrNull = exception;
        }
        finally
        {
            mLifetimeCancellationSource.Dispose();
        }
    }
}
