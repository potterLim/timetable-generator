using System;
using System.Windows.Input;

using Avalonia.Controls;
using Avalonia.Input;

using FluentIcons.Avalonia;
using FluentIcons.Common;

using TimetableGenerator.Desktop.Presentation;
using TimetableGenerator.Desktop.Presentation.ViewModels;
using TimetableGenerator.Domain.Planning;

namespace TimetableGenerator.Desktop.Views;

internal sealed partial class ScheduleWorkspaceView : UserControl
{
    private readonly DelegateCommand mToggleSchedulePresentationCommand;

    private readonly ParameterizedCommand<PersonalScheduleId>
        mEditPersonalScheduleCommand;

    private EScheduleWorkspacePresentationMode mPresentationMode;

    public ICommand EditPersonalScheduleCommand
    {
        get
        {
            return mEditPersonalScheduleCommand;
        }
    }

    public ICommand ToggleSchedulePresentationCommand
    {
        get
        {
            return mToggleSchedulePresentationCommand;
        }
    }

    private void toggleSchedulePresentation()
    {
        EScheduleWorkspacePresentationMode nextMode =
            mPresentationMode == EScheduleWorkspacePresentationMode.Board
                ? EScheduleWorkspacePresentationMode.List
                : EScheduleWorkspacePresentationMode.Board;
        applyPresentationMode(nextMode);
    }

    private void applyPresentationMode(
        EScheduleWorkspacePresentationMode presentationMode)
    {
        ScheduleBoardView? scheduleBoardOrNull =
            this.FindControl<ScheduleBoardView>("ScheduleBoard");
        Border? scheduleListOrNull =
            this.FindControl<Border>("ScheduleListContainer");
        Button? modeButtonOrNull =
            this.FindControl<Button>("ScheduleViewModeButton");
        FluentIcon? modeIconOrNull =
            this.FindControl<FluentIcon>("ScheduleViewModeIcon");
        TextBlock? modeTextOrNull =
            this.FindControl<TextBlock>("ScheduleViewModeText");
        if (scheduleBoardOrNull == null
            || scheduleListOrNull == null
            || modeButtonOrNull == null
            || modeIconOrNull == null
            || modeTextOrNull == null)
        {
            throw new InvalidOperationException(
                "Schedule presentation controls are unavailable.");
        }

        bool isListMode =
            presentationMode == EScheduleWorkspacePresentationMode.List;
        scheduleBoardOrNull.IsVisible = isListMode == false;
        scheduleListOrNull.IsVisible = isListMode;
        modeIconOrNull.Icon = isListMode ? Icon.CalendarWeekStart : Icon.List;
        modeTextOrNull.Text = isListMode ? "시간표 보기" : "목록 보기";
        string automationName = isListMode
            ? "시간표를 주간 표로 보기"
            : "시간표를 목록으로 보기";
        string toolTip = isListMode ? "시간표로 보기" : "목록으로 보기";
        Avalonia.Automation.AutomationProperties.SetName(
            modeButtonOrNull,
            automationName);
        ToolTip.SetTip(modeButtonOrNull, toolTip);
        mPresentationMode = presentationMode;
    }

    private void beginEditPersonalSchedule(PersonalScheduleId scheduleId)
    {
        PlannerWorkspaceViewModel? workspaceOrNull =
            DataContext as PlannerWorkspaceViewModel;
        if (workspaceOrNull == null)
        {
            throw new InvalidOperationException(
                "Personal schedule editing requires a planning workspace.");
        }

        workspaceOrNull.BeginEditPersonalScheduleCommand.Execute(scheduleId);
    }

    private void onScheduleContentSurfacePointerPressed(
        object? senderOrNull,
        PointerPressedEventArgs eventArgs)
    {
        PointerPoint currentPoint = eventArgs.GetCurrentPoint(this);
        if (currentPoint.Properties.PointerUpdateKind
            != PointerUpdateKind.LeftButtonPressed)
        {
            return;
        }

        PlannerWorkspaceViewModel? workspaceOrNull =
            DataContext as PlannerWorkspaceViewModel;
        workspaceOrNull?.CloseInspectorPaneCommand.Execute(null);
    }
}
