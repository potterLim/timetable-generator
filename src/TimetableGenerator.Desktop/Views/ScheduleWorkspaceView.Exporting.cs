using System;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;

using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Interactivity;
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
    private static readonly TimeSpan EXPORT_STATUS_DURATION = TimeSpan.FromSeconds(3.5);

    private readonly IGoogleCalendarExporter mGoogleCalendarExporter;

    private readonly IGoogleCalendarWebNavigator mGoogleCalendarWebNavigator;

    private readonly IAppleCalendarExporter mAppleCalendarExporter;

    private readonly ICalendarTimeZoneProvider mCalendarTimeZoneProvider;

    private readonly AsyncDelegateCommand mExportGoogleCalendarCommand;

    private readonly AsyncDelegateCommand mExportAppleCalendarCommand;

    private readonly CancellationTokenSource mLifetimeCancellationSource;

    private readonly DispatcherTimer mExportStatusTimer;

    private Task mExportResourceReleaseTask;

    private Exception? mExportResourceReleaseExceptionOrNull;

    private PlannerWorkspaceViewModel? mObservedWorkspaceOrNull;

    private EExportStatus? mActiveExportStatusOrNull;

    private bool mIsExportInProgress;

    private bool mIsExportStatusTransient;

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
        mExportStatusTimer = new DispatcherTimer();
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

    private async Task exportGoogleCalendarAsync()
    {
        if (tryBeginExportOperation() == false)
        {
            return;
        }

        try
        {
            CancellationToken cancellationToken = mLifetimeCancellationSource.Token;
            cancellationToken.ThrowIfCancellationRequested();
            CalendarExportDocument document = createCalendarExportDocument(ECalendarExportProvider.Google);
            GoogleCalendarExportPlan exportPlan = GoogleCalendarExportPlan.CreateFromDocument(document);
            showPersistentExportStatus("Google 캘린더로 내보내는 중입니다.", EExportStatus.Information);
            GoogleCalendarExportResult result = await mGoogleCalendarExporter.ExportAsync(exportPlan, this, cancellationToken);
            showGoogleCalendarExportResult(result);
            if (result.Status == EGoogleCalendarExportStatus.Success)
            {
                _ = mGoogleCalendarWebNavigator.TryOpen();
            }
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
            CancellationToken cancellationToken = mLifetimeCancellationSource.Token;
            cancellationToken.ThrowIfCancellationRequested();
            CalendarExportDocument document = createCalendarExportDocument(ECalendarExportProvider.Apple);
            showPersistentExportStatus("Apple 캘린더로 내보내는 중입니다.", EExportStatus.Information);
            AppleCalendarExportResult result = await mAppleCalendarExporter.ExportAsync(document, this, cancellationToken);
            showAppleCalendarExportResult(result);
        }
        finally
        {
            completeExportOperation();
        }
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
        if (mIsExportInProgress || workspaceOrNull == null || workspaceOrNull.CanExportSchedule == false)
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

    private void showGoogleCalendarExportResult(GoogleCalendarExportResult result)
    {
        if (result == null)
        {
            throw new ArgumentNullException(nameof(result));
        }

        switch (result.Status)
        {
            case EGoogleCalendarExportStatus.Success:
                showTransientExportStatus("Google 캘린더로 내보냈습니다.", EExportStatus.Success);
                break;
            case EGoogleCalendarExportStatus.NotConfigured:
                showTransientExportStatus("Google 캘린더 연결을 아직 사용할 수 없습니다.", EExportStatus.Information);
                break;
            case EGoogleCalendarExportStatus.AuthenticationCancelled:
            case EGoogleCalendarExportStatus.Cancelled:
                clearExportStatus();
                break;
            case EGoogleCalendarExportStatus.AuthenticationFailed:
                if (string.Equals(result.DiagnosticCodeOrNull, "authorization_timeout", StringComparison.Ordinal))
                {
                    showPersistentExportStatus("Google 로그인 시간이 만료되었습니다. 다시 시도해 주세요.", EExportStatus.Failure);
                }
                else
                {
                    showPersistentExportStatus("Google 캘린더 연결을 완료하지 못했습니다.", EExportStatus.Failure);
                }

                break;
            case EGoogleCalendarExportStatus.AccessDenied:
                showPersistentExportStatus("Google 캘린더 권한을 확인해 주세요.", EExportStatus.Failure);
                break;
            case EGoogleCalendarExportStatus.NetworkFailed:
                showPersistentExportStatus("Google 캘린더에 연결하지 못했습니다. 네트워크를 확인해 주세요.", EExportStatus.Failure);
                break;
            case EGoogleCalendarExportStatus.Failed:
                showPersistentExportStatus("Google 캘린더에 반영하지 못했습니다. 다시 시도해 주세요.", EExportStatus.Failure);
                break;
            case EGoogleCalendarExportStatus.None:
            default:
                throw new ArgumentOutOfRangeException(nameof(result), result.Status, "Unknown Google Calendar export status.");
        }
    }

    private void showGoogleCalendarExportFailure(Exception exception)
    {
        showCalendarExportFailure(exception, "Google 캘린더에 반영하지 못했습니다. 다시 시도해 주세요.");
    }

    private void showAppleCalendarExportResult(AppleCalendarExportResult result)
    {
        if (result == null)
        {
            throw new ArgumentNullException(nameof(result));
        }

        switch (result.Status)
        {
            case EAppleCalendarExportStatus.Success:
                showTransientExportStatus("Apple 캘린더로 내보냈습니다.", EExportStatus.Success);
                break;
            case EAppleCalendarExportStatus.Cancelled:
                clearExportStatus();
                break;
            case EAppleCalendarExportStatus.Unavailable:
                showTransientExportStatus("Apple 캘린더를 사용할 수 없습니다.", EExportStatus.Information);
                break;
            case EAppleCalendarExportStatus.AccessDenied:
                showPersistentExportStatus("Apple 캘린더 접근 권한을 확인해 주세요.", EExportStatus.Failure);
                break;
            case EAppleCalendarExportStatus.Failed:
                showPersistentExportStatus("Apple 캘린더로 내보내지 못했습니다. 다시 시도해 주세요.", EExportStatus.Failure);
                break;
            case EAppleCalendarExportStatus.None:
            default:
                throw new ArgumentOutOfRangeException(nameof(result), result.Status, "Unknown Apple Calendar export status.");
        }
    }

    private void showAppleCalendarExportFailure(Exception exception)
    {
        showCalendarExportFailure(exception, "Apple 캘린더로 내보내지 못했습니다. 다시 시도해 주세요.");
    }

    private void showCalendarExportFailure(Exception exception, string fallbackMessage)
    {
        if (exception is NotSupportedException)
        {
            showPersistentExportStatus("이 학기는 캘린더 내보내기를 아직 지원하지 않습니다.", EExportStatus.Failure);
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

        showPersistentExportStatus(message, EExportStatus.Failure);
    }

    private void clearExportStatus()
    {
        mExportStatusTimer.Stop();
        mActiveExportStatusOrNull = null;
        mIsExportStatusTransient = false;

        Border? statusToastOrNull = this.FindControl<Border>("ExportStatusToast");
        TextBlock? statusTextOrNull = this.FindControl<TextBlock>("ExportStatusText");
        FluentIcon? statusIconOrNull = this.FindControl<FluentIcon>("ExportStatusIcon");
        Button? dismissButtonOrNull = this.FindControl<Button>("DismissExportStatusButton");
        if (statusToastOrNull == null
            || statusTextOrNull == null
            || statusIconOrNull == null
            || dismissButtonOrNull == null)
        {
            return;
        }

        AutomationProperties.SetLiveSetting(statusTextOrNull, AutomationLiveSetting.Off);
        statusTextOrNull.Text = string.Empty;
        setExportStatusClasses(statusTextOrNull, null);
        setExportStatusClasses(statusToastOrNull, null);
        setExportStatusClasses(statusIconOrNull, null);
        dismissButtonOrNull.IsVisible = false;
        statusToastOrNull.IsHitTestVisible = false;
        statusToastOrNull.IsVisible = false;
    }

    private void showPersistentExportStatus(string message, EExportStatus status)
    {
        mExportStatusTimer.Stop();
        mIsExportStatusTransient = false;
        showExportStatusCore(message, status);
    }

    private void showTransientExportStatus(string message, EExportStatus status)
    {
        if (status == EExportStatus.Failure)
        {
            throw new InvalidOperationException("Failure export statuses must remain visible until dismissed.");
        }

        mExportStatusTimer.Stop();
        mIsExportStatusTransient = true;
        showExportStatusCore(message, status);
        mExportStatusTimer.Start();
    }

    private void showExportStatusCore(string message, EExportStatus status)
    {
        Border? statusToastOrNull = this.FindControl<Border>("ExportStatusToast");
        TextBlock? statusTextOrNull = this.FindControl<TextBlock>("ExportStatusText");
        FluentIcon? statusIconOrNull = this.FindControl<FluentIcon>("ExportStatusIcon");
        Button? dismissButtonOrNull = this.FindControl<Button>("DismissExportStatusButton");
        if (statusToastOrNull == null
            || statusTextOrNull == null
            || statusIconOrNull == null
            || dismissButtonOrNull == null)
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
                throw new ArgumentOutOfRangeException(nameof(status), status, "Unknown export status.");
        }

        setExportStatusClasses(statusTextOrNull, status);
        setExportStatusClasses(statusToastOrNull, status);
        setExportStatusClasses(statusIconOrNull, status);
        mActiveExportStatusOrNull = status;
        statusIconOrNull.Icon = statusIcon;
        dismissButtonOrNull.IsVisible = status == EExportStatus.Failure;
        statusToastOrNull.IsHitTestVisible = status == EExportStatus.Failure;
        statusToastOrNull.IsVisible = true;
        AutomationProperties.SetLiveSetting(statusTextOrNull, AutomationLiveSetting.Polite);
        statusTextOrNull.Text = message;
    }

    private static void setExportStatusClasses(StyledElement element, EExportStatus? activeStatusOrNull)
    {
        element.Classes.Set("success", activeStatusOrNull == EExportStatus.Success);
        element.Classes.Set("information", activeStatusOrNull == EExportStatus.Information);
        element.Classes.Set("error", activeStatusOrNull == EExportStatus.Failure);
    }

    private void onExportStatusTimerTick(object? senderOrNull, EventArgs eventArgs)
    {
        if (mIsExportStatusTransient)
        {
            clearExportStatus();
        }
    }

    private void onDismissExportStatusButtonClick(object? senderOrNull, RoutedEventArgs eventArgs)
    {
        clearExportFailureStatus();
        Button? exportButtonOrNull = this.FindControl<Button>("ExportScheduleButton");
        exportButtonOrNull?.Focus();
        eventArgs.Handled = true;
    }

    private void onDataContextChanged(object? senderOrNull, EventArgs eventArgs)
    {
        PlannerWorkspaceViewModel? workspaceOrNull = DataContext as PlannerWorkspaceViewModel;
        if (ReferenceEquals(mObservedWorkspaceOrNull, workspaceOrNull))
        {
            return;
        }

        observeWorkspace(workspaceOrNull);
        clearExportFailureStatus();
    }

    private void observeWorkspace(PlannerWorkspaceViewModel? workspaceOrNull)
    {
        if (mObservedWorkspaceOrNull != null)
        {
            mObservedWorkspaceOrNull.PropertyChanged -= onWorkspacePropertyChanged;
        }

        mObservedWorkspaceOrNull = workspaceOrNull;
        if (mObservedWorkspaceOrNull != null)
        {
            mObservedWorkspaceOrNull.PropertyChanged += onWorkspacePropertyChanged;
        }
    }

    private void onWorkspacePropertyChanged(object? senderOrNull, PropertyChangedEventArgs eventArgs)
    {
        if (ReferenceEquals(senderOrNull, mObservedWorkspaceOrNull) == false
            || string.Equals(eventArgs.PropertyName, nameof(PlannerWorkspaceViewModel.DisplayedScheduleBoard), StringComparison.Ordinal) == false)
        {
            return;
        }

        clearExportFailureStatus();
    }

    private void clearExportFailureStatus()
    {
        if (mActiveExportStatusOrNull == EExportStatus.Failure)
        {
            clearExportStatus();
        }
    }

    private void onDetachedFromVisualTree(object? senderOrNull, VisualTreeAttachmentEventArgs eventArgs)
    {
        DetachedFromVisualTree -= onDetachedFromVisualTree;
        DataContextChanged -= onDataContextChanged;
        observeWorkspace(null);
        mExportStatusTimer.Stop();
        mExportStatusTimer.Tick -= onExportStatusTimerTick;
        mLifetimeCancellationSource.Cancel();
        mExportResourceReleaseTask = releaseExportResourcesAsync();
    }

    private async Task releaseExportResourcesAsync()
    {
        try
        {
            await Task.WhenAll(mExportPngCommand.ExecutionTask, mExportAllPngCommand.ExecutionTask, mExportGoogleCalendarCommand.ExecutionTask, mExportAppleCalendarCommand.ExecutionTask);
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
