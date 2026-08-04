using System;
using System.Windows.Input;
using Avalonia.Controls;
using FluentIcons.Avalonia;
using FluentIcons.Common;
using TimetableGenerator.Desktop.Exporting.Calendar;
using TimetableGenerator.Desktop.Presentation;
using TimetableGenerator.Desktop.Presentation.ViewModels;
using TimetableGenerator.Domain.Planning;

namespace TimetableGenerator.Desktop.Views;

internal sealed partial class ScheduleWorkspaceView :
    UserControl,
    ICalendarNameConflictResolver
{
    private readonly DelegateCommand mToggleSchedulePresentationCommand;

    private readonly ParameterizedCommand<PersonalScheduleId> mEditPersonalScheduleCommand;

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
        EScheduleWorkspacePresentationMode nextMode;
        if (mPresentationMode == EScheduleWorkspacePresentationMode.Board)
        {
            nextMode = EScheduleWorkspacePresentationMode.List;
        }
        else
        {
            nextMode = EScheduleWorkspacePresentationMode.Board;
        }
        applyPresentationMode(nextMode);
    }

    private void applyPresentationMode(EScheduleWorkspacePresentationMode presentationMode)
    {
        ScheduleBoardView? scheduleBoardOrNull = this.FindControl<ScheduleBoardView>("ScheduleBoard");
        Border? scheduleListOrNull = this.FindControl<Border>("ScheduleListContainer");
        Button? modeButtonOrNull = this.FindControl<Button>("ScheduleViewModeButton");
        FluentIcon? modeIconOrNull = this.FindControl<FluentIcon>("ScheduleViewModeIcon");
        TextBlock? modeTextOrNull = this.FindControl<TextBlock>("ScheduleViewModeText");
        if (scheduleBoardOrNull == null
            || scheduleListOrNull == null
            || modeButtonOrNull == null
            || modeIconOrNull == null
            || modeTextOrNull == null)
        {
            throw new InvalidOperationException("Schedule presentation controls are unavailable.");
        }

        bool isListMode = presentationMode == EScheduleWorkspacePresentationMode.List;
        scheduleBoardOrNull.IsVisible = isListMode == false;
        scheduleListOrNull.IsVisible = isListMode;
        Icon modeIcon = Icon.List;
        string modeText = "일정 목록";
        string automationName = "일정 목록으로 보기";
        if (isListMode)
        {
            modeIcon = Icon.CalendarWeekStart;
            modeText = "주간 시간표";
            automationName = "주간 시간표로 보기";
        }
        modeIconOrNull.Icon = modeIcon;
        modeTextOrNull.Text = modeText;
        string toolTip = automationName;
        Avalonia.Automation.AutomationProperties.SetName(modeButtonOrNull, automationName);
        ToolTip.SetTip(modeButtonOrNull, toolTip);
        mPresentationMode = presentationMode;
    }

    private void beginEditPersonalSchedule(PersonalScheduleId scheduleId)
    {
        PlannerWorkspaceViewModel? workspaceOrNull = DataContext as PlannerWorkspaceViewModel;
        if (workspaceOrNull == null)
        {
            throw new InvalidOperationException("Personal schedule editing requires a planning workspace.");
        }

        workspaceOrNull.BeginEditPersonalScheduleCommand.Execute(scheduleId);
    }
}
