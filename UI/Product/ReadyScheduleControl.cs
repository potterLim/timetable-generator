using System;
using System.Collections.Generic;
using System.Windows.Forms;
using TimetableGenerator.Presentation.Schedules;

namespace TimetableGenerator.UI.Product;

internal sealed class ReadyScheduleControl : UserControl
{
    private const int COMPACT_SIDEBAR_DIVISOR = 3;

    private readonly SplitContainer mSplitContainer;
    private readonly ScheduleSidebarControl mSidebar;
    private readonly ScheduleWorkspaceControl mWorkspace;
    private IReadOnlyList<ScheduleGridViewModel> mSchedules;

    internal event EventHandler<ScheduleSelectionChangedEventArgs> SelectedScheduleChanged;

    internal ReadyScheduleControl()
    {
        AutoScaleMode = AutoScaleMode.Dpi;
        BackColor = DesignTokens.WINDOW_BACKGROUND_COLOR;
        AccessibleName = "생성된 일정";
        AccessibleDescription = "일정 목록과 선택한 시간표를 표시합니다.";
        AccessibleRole = AccessibleRole.Pane;
        MinimumSize = new System.Drawing.Size(720, 520);

        List<ScheduleGridViewModel> noSchedules = new List<ScheduleGridViewModel>();
        mSchedules = noSchedules.AsReadOnly();

        mSidebar = new ScheduleSidebarControl();
        mSidebar.Dock = DockStyle.Fill;
        mSidebar.SelectedScheduleChanged += onSidebarSelectedScheduleChanged;

        mWorkspace = new ScheduleWorkspaceControl();
        mWorkspace.Dock = DockStyle.Fill;
        mWorkspace.PreviousScheduleRequested += onPreviousScheduleRequested;
        mWorkspace.NextScheduleRequested += onNextScheduleRequested;

        mSplitContainer = new SplitContainer();
        mSplitContainer.Dock = DockStyle.Fill;
        mSplitContainer.Orientation = Orientation.Vertical;
        mSplitContainer.FixedPanel = FixedPanel.Panel1;
        mSplitContainer.IsSplitterFixed = true;
        mSplitContainer.Panel1MinSize = 0;
        mSplitContainer.Panel2MinSize = 0;
        mSplitContainer.TabStop = false;
        mSplitContainer.BackColor = DesignTokens.SUBTLE_BORDER_COLOR;
        mSplitContainer.Panel1.Controls.Add(mSidebar);
        mSplitContainer.Panel2.Controls.Add(mWorkspace);

        Controls.Add(mSplitContainer);
        applyDpiMetrics();
    }

    internal void showSchedules(
        IReadOnlyList<ScheduleGridViewModel> schedules,
        ScheduleIndex initialScheduleIndex)
    {
        if (schedules == null)
        {
            throw new ArgumentNullException(nameof(schedules));
        }

        if (schedules.Count == 0)
        {
            throw new ArgumentException("The ready screen requires at least one schedule.", nameof(schedules));
        }

        if (initialScheduleIndex.Value >= schedules.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(initialScheduleIndex));
        }

        List<ScheduleGridViewModel> copiedSchedules = new List<ScheduleGridViewModel>(
            schedules.Count);
        foreach (ScheduleGridViewModel schedule in schedules)
        {
            if (schedule == null)
            {
                throw new ArgumentException("Schedule lists cannot contain null values.", nameof(schedules));
            }

            copiedSchedules.Add(schedule);
        }

        mSchedules = copiedSchedules.AsReadOnly();
        mSidebar.showSchedules(mSchedules, initialScheduleIndex);
    }

    internal void selectSchedule(ScheduleIndex scheduleIndex)
    {
        if (scheduleIndex.Value >= mSchedules.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(scheduleIndex));
        }

        mSidebar.selectSchedule(scheduleIndex);
    }

    protected override void OnDpiChangedAfterParent(EventArgs eventArgs)
    {
        base.OnDpiChangedAfterParent(eventArgs);
        applyDpiMetrics();
    }

    protected override void OnSizeChanged(EventArgs eventArgs)
    {
        base.OnSizeChanged(eventArgs);
        applySidebarWidth();
    }

    private void applyDpiMetrics()
    {
        mSplitContainer.SplitterWidth = DesignTokens.scaleLogicalPixel(
            this,
            DesignTokens.BORDER_WIDTH);
        applySidebarWidth();
    }

    private void applySidebarWidth()
    {
        if (mSplitContainer == null)
        {
            return;
        }

        if (mSplitContainer.Width <= mSplitContainer.SplitterWidth)
        {
            return;
        }

        int preferredSidebarWidth = DesignTokens.scaleLogicalPixel(
            this,
            DesignTokens.SIDEBAR_WIDTH);
        int compactSidebarWidth = Math.Max(
            1,
            mSplitContainer.Width / COMPACT_SIDEBAR_DIVISOR);
        int appliedSidebarWidth = Math.Min(preferredSidebarWidth, compactSidebarWidth);
        int maximumSplitterDistance = mSplitContainer.Width - mSplitContainer.SplitterWidth;
        mSplitContainer.SplitterDistance = Math.Min(
            appliedSidebarWidth,
            maximumSplitterDistance);
    }

    private void onSidebarSelectedScheduleChanged(
        object sender,
        ScheduleSelectionChangedEventArgs eventArgs)
    {
        if (eventArgs.SelectedIndex.Value >= mSchedules.Count)
        {
            throw new InvalidOperationException("The selected schedule index is outside the ready screen data.");
        }

        ECommandAvailability previousAvailability = eventArgs.SelectedIndex.HasPrevious
            ? ECommandAvailability.Enabled
            : ECommandAvailability.Disabled;
        ECommandAvailability nextAvailability =
            eventArgs.SelectedIndex.Value < mSchedules.Count - 1
                ? ECommandAvailability.Enabled
                : ECommandAvailability.Disabled;

        mWorkspace.showSchedule(
            eventArgs.SelectedScheduleNumber,
            eventArgs.SelectedSchedule,
            previousAvailability,
            nextAvailability);

        EventHandler<ScheduleSelectionChangedEventArgs> eventHandler = SelectedScheduleChanged;
        if (eventHandler != null)
        {
            eventHandler(this, eventArgs);
        }
    }

    private void onPreviousScheduleRequested(object sender, EventArgs eventArgs)
    {
        ScheduleIndex selectedScheduleIndex = mSidebar.getSelectedScheduleIndex();
        if (selectedScheduleIndex.HasPrevious == false)
        {
            return;
        }

        mSidebar.selectSchedule(selectedScheduleIndex.GetPrevious());
    }

    private void onNextScheduleRequested(object sender, EventArgs eventArgs)
    {
        ScheduleIndex selectedScheduleIndex = mSidebar.getSelectedScheduleIndex();
        ScheduleIndex nextScheduleIndex = selectedScheduleIndex.GetNext();
        if (nextScheduleIndex.Value >= mSchedules.Count)
        {
            return;
        }

        mSidebar.selectSchedule(nextScheduleIndex);
    }
}
