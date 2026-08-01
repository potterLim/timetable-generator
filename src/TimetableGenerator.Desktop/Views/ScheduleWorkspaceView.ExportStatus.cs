using System;
using System.ComponentModel;

using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;

using FluentIcons.Avalonia;
using FluentIcons.Common;

using TimetableGenerator.Desktop.Exporting;
using TimetableGenerator.Desktop.Presentation.ViewModels;

namespace TimetableGenerator.Desktop.Views;

internal sealed partial class ScheduleWorkspaceView
{
    private static readonly TimeSpan EXPORT_STATUS_DURATION = TimeSpan.FromSeconds(3.5);

    private readonly DispatcherTimer mExportStatusTimer = new DispatcherTimer();

    private PlannerWorkspaceViewModel? mObservedWorkspaceOrNull;

    private EExportStatus? mActiveExportStatusOrNull;

    private bool mIsExportStatusTransient;

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
        if (ReferenceEquals(senderOrNull, mObservedWorkspaceOrNull) == false || string.Equals(eventArgs.PropertyName, nameof(PlannerWorkspaceViewModel.DisplayedScheduleBoard), StringComparison.Ordinal) == false)
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
}
