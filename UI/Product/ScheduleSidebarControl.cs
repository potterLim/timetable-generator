using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using TimetableGenerator.Presentation.Schedules;

namespace TimetableGenerator.UI.Product;

internal sealed class ScheduleSidebarControl : UserControl
{
    private const int SELECTED_INDICATOR_WIDTH = 3;

    private readonly Panel mHeaderPanel;
    private readonly Label mHeaderTitleLabel;
    private readonly Label mScheduleCountLabel;
    private readonly ListBox mScheduleListBox;
    private readonly Font mItemTitleFont;
    private readonly Font mItemSummaryFont;
    private bool mIsBindingSchedules;

    internal event EventHandler<ScheduleSelectionChangedEventArgs> SelectedScheduleChanged;

    internal ScheduleSidebarControl()
    {
        AutoScaleMode = AutoScaleMode.Dpi;
        BackColor = DesignTokens.SIDEBAR_BACKGROUND_COLOR;
        AccessibleName = "일정 목록";
        AccessibleDescription = "생성된 일정 사이를 이동합니다.";
        AccessibleRole = AccessibleRole.Pane;

        SetStyle(ControlStyles.AllPaintingInWmPaint, true);
        SetStyle(ControlStyles.OptimizedDoubleBuffer, true);
        SetStyle(ControlStyles.ResizeRedraw, true);

        mItemTitleFont = DesignTokens.createSidebarItemTitleFont(Font);
        mItemSummaryFont = DesignTokens.createSidebarItemSummaryFont(Font);

        mHeaderPanel = new Panel();
        mHeaderPanel.Dock = DockStyle.Top;
        mHeaderPanel.BackColor = DesignTokens.SIDEBAR_BACKGROUND_COLOR;

        mHeaderTitleLabel = new Label();
        mHeaderTitleLabel.AutoSize = true;
        mHeaderTitleLabel.Text = "일정";
        mHeaderTitleLabel.Font = mItemTitleFont;
        mHeaderTitleLabel.ForeColor = DesignTokens.TEXT_PRIMARY_COLOR;
        mHeaderTitleLabel.Dock = DockStyle.Left;
        mHeaderTitleLabel.TextAlign = ContentAlignment.MiddleLeft;

        mScheduleCountLabel = new Label();
        mScheduleCountLabel.AutoSize = true;
        mScheduleCountLabel.Text = string.Empty;
        mScheduleCountLabel.Font = mItemSummaryFont;
        mScheduleCountLabel.ForeColor = DesignTokens.TEXT_SECONDARY_COLOR;
        mScheduleCountLabel.Dock = DockStyle.Right;
        mScheduleCountLabel.TextAlign = ContentAlignment.MiddleRight;

        mHeaderPanel.Controls.Add(mScheduleCountLabel);
        mHeaderPanel.Controls.Add(mHeaderTitleLabel);

        mScheduleListBox = new ListBox();
        mScheduleListBox.Dock = DockStyle.Fill;
        mScheduleListBox.BackColor = DesignTokens.SIDEBAR_BACKGROUND_COLOR;
        mScheduleListBox.ForeColor = DesignTokens.TEXT_PRIMARY_COLOR;
        mScheduleListBox.BorderStyle = BorderStyle.None;
        mScheduleListBox.DrawMode = DrawMode.OwnerDrawFixed;
        mScheduleListBox.IntegralHeight = false;
        mScheduleListBox.SelectionMode = SelectionMode.One;
        mScheduleListBox.TabStop = true;
        mScheduleListBox.AccessibleName = "생성된 일정";
        mScheduleListBox.AccessibleDescription = "위쪽 및 아래쪽 화살표 키로 일정을 선택합니다.";
        mScheduleListBox.DrawItem += onScheduleListBoxDrawItem;
        mScheduleListBox.SelectedIndexChanged += onScheduleListBoxSelectedIndexChanged;

        Controls.Add(mScheduleListBox);
        Controls.Add(mHeaderPanel);

        applyDpiMetrics();
    }

    internal void showSchedules(
        IReadOnlyList<ScheduleGridViewModel> schedules,
        ScheduleIndex selectedIndex)
    {
        if (schedules == null)
        {
            throw new ArgumentNullException(nameof(schedules));
        }

        if (schedules.Count == 0)
        {
            throw new ArgumentException("The ready screen requires at least one schedule.", nameof(schedules));
        }

        if (selectedIndex.Value >= schedules.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(selectedIndex));
        }

        mIsBindingSchedules = true;
        mScheduleListBox.BeginUpdate();
        try
        {
            mScheduleListBox.Items.Clear();
            for (int scheduleIndexValue = 0; scheduleIndexValue < schedules.Count; ++scheduleIndexValue)
            {
                ScheduleGridViewModel schedule = schedules[scheduleIndexValue];
                if (schedule == null)
                {
                    throw new ArgumentException("Schedule lists cannot contain null values.", nameof(schedules));
                }

                ScheduleIndex scheduleIndex = new ScheduleIndex(scheduleIndexValue);
                ScheduleSidebarItem sidebarItem = new ScheduleSidebarItem(scheduleIndex, schedule);
                mScheduleListBox.Items.Add(sidebarItem);
            }

            mScheduleCountLabel.Text = schedules.Count + "개";
        }
        finally
        {
            mScheduleListBox.EndUpdate();
            mIsBindingSchedules = false;
        }

        mScheduleListBox.SelectedIndex = selectedIndex.Value;
    }

    internal void selectSchedule(ScheduleIndex scheduleIndex)
    {
        if (scheduleIndex.Value >= mScheduleListBox.Items.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(scheduleIndex));
        }

        mScheduleListBox.SelectedIndex = scheduleIndex.Value;
        mScheduleListBox.Focus();
    }

    internal ScheduleIndex getSelectedScheduleIndex()
    {
        if (mScheduleListBox.SelectedItem == null)
        {
            throw new InvalidOperationException("No schedule is selected.");
        }

        ScheduleSidebarItem selectedItem = (ScheduleSidebarItem)mScheduleListBox.SelectedItem;
        return selectedItem.ScheduleIndex;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            mItemTitleFont.Dispose();
            mItemSummaryFont.Dispose();
        }

        base.Dispose(disposing);
    }

    protected override void OnDpiChangedAfterParent(EventArgs eventArgs)
    {
        base.OnDpiChangedAfterParent(eventArgs);
        applyDpiMetrics();
        mScheduleListBox.Invalidate();
    }

    private void applyDpiMetrics()
    {
        int horizontalPadding = DesignTokens.scaleLogicalPixel(this, DesignTokens.SPACE_16);
        int verticalPadding = DesignTokens.scaleLogicalPixel(this, DesignTokens.SPACE_12);
        mHeaderPanel.Height = DesignTokens.scaleLogicalPixel(this, DesignTokens.SIDEBAR_HEADER_HEIGHT);
        mHeaderPanel.Padding = new Padding(
            horizontalPadding,
            verticalPadding,
            horizontalPadding,
            verticalPadding);
        mScheduleListBox.ItemHeight = DesignTokens.scaleLogicalPixel(
            this,
            DesignTokens.SIDEBAR_ITEM_HEIGHT);
    }

    private void onScheduleListBoxSelectedIndexChanged(object sender, EventArgs eventArgs)
    {
        if (mIsBindingSchedules || mScheduleListBox.SelectedItem == null)
        {
            return;
        }

        ScheduleSidebarItem selectedItem = (ScheduleSidebarItem)mScheduleListBox.SelectedItem;
        ScheduleSelectionChangedEventArgs selectionChangedEventArgs =
            new ScheduleSelectionChangedEventArgs(
                selectedItem.ScheduleIndex,
                selectedItem.Schedule);
        EventHandler<ScheduleSelectionChangedEventArgs> eventHandler = SelectedScheduleChanged;
        if (eventHandler != null)
        {
            eventHandler(this, selectionChangedEventArgs);
        }
    }

    private void onScheduleListBoxDrawItem(object sender, DrawItemEventArgs drawItemEventArgs)
    {
        if (drawItemEventArgs.Index < 0 || drawItemEventArgs.Index >= mScheduleListBox.Items.Count)
        {
            return;
        }

        ScheduleSidebarItem sidebarItem =
            (ScheduleSidebarItem)mScheduleListBox.Items[drawItemEventArgs.Index];
        bool isSelected =
            (drawItemEventArgs.State & DrawItemState.Selected) == DrawItemState.Selected;
        Color backgroundColor = isSelected
            ? DesignTokens.ACCENT_TINT_COLOR
            : DesignTokens.SIDEBAR_BACKGROUND_COLOR;

        using (SolidBrush backgroundBrush = new SolidBrush(backgroundColor))
        {
            drawItemEventArgs.Graphics.FillRectangle(backgroundBrush, drawItemEventArgs.Bounds);
        }

        if (isSelected)
        {
            int indicatorWidth = DesignTokens.scaleLogicalPixel(this, SELECTED_INDICATOR_WIDTH);
            Rectangle indicatorBounds = new Rectangle(
                drawItemEventArgs.Bounds.Left,
                drawItemEventArgs.Bounds.Top,
                indicatorWidth,
                drawItemEventArgs.Bounds.Height);
            using (SolidBrush indicatorBrush = new SolidBrush(DesignTokens.ACCENT_COLOR))
            {
                drawItemEventArgs.Graphics.FillRectangle(indicatorBrush, indicatorBounds);
            }
        }

        int horizontalPadding = DesignTokens.scaleLogicalPixel(this, DesignTokens.SPACE_16);
        int verticalPadding = DesignTokens.scaleLogicalPixel(this, DesignTokens.SPACE_12);
        int contentLeft = drawItemEventArgs.Bounds.Left + horizontalPadding;
        int contentWidth = Math.Max(0, drawItemEventArgs.Bounds.Width - (horizontalPadding * 2));
        int titleHeight = TextRenderer.MeasureText(
            drawItemEventArgs.Graphics,
            sidebarItem.Title,
            mItemTitleFont,
            new Size(contentWidth, drawItemEventArgs.Bounds.Height),
            TextFormatFlags.NoPadding | TextFormatFlags.SingleLine).Height;

        Rectangle titleBounds = new Rectangle(
            contentLeft,
            drawItemEventArgs.Bounds.Top + verticalPadding,
            contentWidth,
            titleHeight);
        Rectangle summaryBounds = new Rectangle(
            contentLeft,
            titleBounds.Bottom + DesignTokens.scaleLogicalPixel(this, DesignTokens.SPACE_4),
            contentWidth,
            Math.Max(0, drawItemEventArgs.Bounds.Bottom - titleBounds.Bottom - verticalPadding));

        TextRenderer.DrawText(
            drawItemEventArgs.Graphics,
            sidebarItem.Title,
            mItemTitleFont,
            titleBounds,
            DesignTokens.TEXT_PRIMARY_COLOR,
            TextFormatFlags.Left | TextFormatFlags.NoPadding | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);
        TextRenderer.DrawText(
            drawItemEventArgs.Graphics,
            sidebarItem.Summary,
            mItemSummaryFont,
            summaryBounds,
            DesignTokens.TEXT_SECONDARY_COLOR,
            TextFormatFlags.Left | TextFormatFlags.Top | TextFormatFlags.NoPadding | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);

        if ((drawItemEventArgs.State & DrawItemState.Focus) == DrawItemState.Focus)
        {
            Rectangle focusBounds = ProductDrawing.insetRectangle(
                drawItemEventArgs.Bounds,
                DesignTokens.scaleLogicalPixel(this, DesignTokens.SPACE_4));
            ControlPaint.DrawFocusRectangle(
                drawItemEventArgs.Graphics,
                focusBounds,
                DesignTokens.TEXT_PRIMARY_COLOR,
                backgroundColor);
        }
    }
}
