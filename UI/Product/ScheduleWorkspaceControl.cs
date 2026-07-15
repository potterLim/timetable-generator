using System;
using System.Drawing;
using System.Windows.Forms;
using TimetableGenerator.Presentation.Schedules;

namespace TimetableGenerator.UI.Product;

internal sealed class ScheduleWorkspaceControl : UserControl
{
    private const int WORKSPACE_HEADER_HEIGHT = 92;
    private const int NAVIGATION_BUTTON_MINIMUM_WIDTH = 88;

    private readonly Panel mHeaderPanel;
    private readonly Panel mHeaderTextPanel;
    private readonly Label mScheduleTitleLabel;
    private readonly Label mScheduleSummaryLabel;
    private readonly FlowLayoutPanel mNavigationPanel;
    private readonly ProductButton mPreviousButton;
    private readonly ProductButton mNextButton;
    private readonly ScheduleGridControl mScheduleGrid;
    private readonly Font mTitleFont;
    private readonly Font mSummaryFont;

    internal event EventHandler PreviousScheduleRequested;
    internal event EventHandler NextScheduleRequested;

    internal ScheduleWorkspaceControl()
    {
        AutoScaleMode = AutoScaleMode.Dpi;
        BackColor = DesignTokens.WINDOW_BACKGROUND_COLOR;
        AccessibleName = "일정 작업 영역";
        AccessibleDescription = "선택한 일정의 시간표와 요약을 표시합니다.";
        AccessibleRole = AccessibleRole.Pane;

        mTitleFont = DesignTokens.createAppTitleFont(Font);
        mSummaryFont = DesignTokens.createBodyFont(Font);

        mHeaderPanel = new Panel();
        mHeaderPanel.Dock = DockStyle.Top;
        mHeaderPanel.BackColor = DesignTokens.WINDOW_BACKGROUND_COLOR;

        mNavigationPanel = new FlowLayoutPanel();
        mNavigationPanel.Dock = DockStyle.Right;
        mNavigationPanel.AutoSize = true;
        mNavigationPanel.AutoSizeMode = AutoSizeMode.GrowAndShrink;
        mNavigationPanel.FlowDirection = FlowDirection.LeftToRight;
        mNavigationPanel.WrapContents = false;
        mNavigationPanel.BackColor = Color.Transparent;

        mPreviousButton = new ProductButton(
            "이전",
            EAppIcon.Previous,
            EProductButtonVariant.Quiet);
        mPreviousButton.AccessibleDescription = "이전 일정을 표시합니다.";
        mPreviousButton.Click += onPreviousButtonClick;

        mNextButton = new ProductButton(
            "다음",
            EAppIcon.Next,
            EProductButtonVariant.Quiet);
        mNextButton.AccessibleDescription = "다음 일정을 표시합니다.";
        mNextButton.Click += onNextButtonClick;

        mNavigationPanel.Controls.Add(mPreviousButton);
        mNavigationPanel.Controls.Add(mNextButton);

        mHeaderTextPanel = new Panel();
        mHeaderTextPanel.Dock = DockStyle.Fill;
        mHeaderTextPanel.BackColor = Color.Transparent;

        mScheduleTitleLabel = new Label();
        mScheduleTitleLabel.Dock = DockStyle.Top;
        mScheduleTitleLabel.AutoEllipsis = true;
        mScheduleTitleLabel.Font = mTitleFont;
        mScheduleTitleLabel.ForeColor = DesignTokens.TEXT_PRIMARY_COLOR;
        mScheduleTitleLabel.Text = string.Empty;
        mScheduleTitleLabel.TextAlign = ContentAlignment.MiddleLeft;

        mScheduleSummaryLabel = new Label();
        mScheduleSummaryLabel.Dock = DockStyle.Fill;
        mScheduleSummaryLabel.AutoEllipsis = true;
        mScheduleSummaryLabel.Font = mSummaryFont;
        mScheduleSummaryLabel.ForeColor = DesignTokens.TEXT_SECONDARY_COLOR;
        mScheduleSummaryLabel.Text = string.Empty;
        mScheduleSummaryLabel.TextAlign = ContentAlignment.TopLeft;

        mHeaderTextPanel.Controls.Add(mScheduleSummaryLabel);
        mHeaderTextPanel.Controls.Add(mScheduleTitleLabel);
        mHeaderPanel.Controls.Add(mHeaderTextPanel);
        mHeaderPanel.Controls.Add(mNavigationPanel);

        mScheduleGrid = new ScheduleGridControl();
        mScheduleGrid.Dock = DockStyle.Fill;

        Controls.Add(mScheduleGrid);
        Controls.Add(mHeaderPanel);

        applyDpiMetrics();
    }

    internal void showSchedule(
        ScheduleNumber scheduleNumber,
        ScheduleGridViewModel schedule,
        ECommandAvailability previousAvailability,
        ECommandAvailability nextAvailability)
    {
        if (scheduleNumber.IsValid == false)
        {
            throw new ArgumentException("A valid schedule number is required.", nameof(scheduleNumber));
        }

        if (schedule == null)
        {
            throw new ArgumentNullException(nameof(schedule));
        }

        if (Enum.IsDefined(typeof(ECommandAvailability), previousAvailability) == false)
        {
            throw new ArgumentOutOfRangeException(nameof(previousAvailability));
        }

        if (Enum.IsDefined(typeof(ECommandAvailability), nextAvailability) == false)
        {
            throw new ArgumentOutOfRangeException(nameof(nextAvailability));
        }

        mScheduleTitleLabel.Text = "일정 " + scheduleNumber;
        mScheduleTitleLabel.AccessibleName = mScheduleTitleLabel.Text;
        mScheduleSummaryLabel.Text = ScheduleSummaryTextFormatter.formatWorkspaceSummary(
            schedule.Summary);
        mScheduleSummaryLabel.AccessibleName = mScheduleSummaryLabel.Text;
        mPreviousButton.Enabled = previousAvailability == ECommandAvailability.Enabled;
        mNextButton.Enabled = nextAvailability == ECommandAvailability.Enabled;
        mScheduleGrid.showSchedule(schedule);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            mTitleFont.Dispose();
            mSummaryFont.Dispose();
        }

        base.Dispose(disposing);
    }

    protected override void OnDpiChangedAfterParent(EventArgs eventArgs)
    {
        base.OnDpiChangedAfterParent(eventArgs);
        applyDpiMetrics();
    }

    private void applyDpiMetrics()
    {
        int horizontalPadding = DesignTokens.scaleLogicalPixel(this, DesignTokens.SPACE_24);
        int verticalPadding = DesignTokens.scaleLogicalPixel(this, DesignTokens.SPACE_16);
        int controlGap = DesignTokens.scaleLogicalPixel(this, DesignTokens.SPACE_8);
        int buttonMinimumWidth = DesignTokens.scaleLogicalPixel(
            this,
            NAVIGATION_BUTTON_MINIMUM_WIDTH);

        Padding = new Padding(
            horizontalPadding,
            0,
            horizontalPadding,
            DesignTokens.scaleLogicalPixel(this, DesignTokens.SPACE_24));
        mHeaderPanel.Height = DesignTokens.scaleLogicalPixel(this, WORKSPACE_HEADER_HEIGHT);
        mHeaderPanel.Padding = new Padding(0, verticalPadding, 0, verticalPadding);
        mNavigationPanel.Padding = new Padding(0, 0, 0, 0);
        mPreviousButton.MinimumSize = new Size(
            buttonMinimumWidth,
            mPreviousButton.MinimumSize.Height);
        mNextButton.MinimumSize = new Size(
            buttonMinimumWidth,
            mNextButton.MinimumSize.Height);
        mPreviousButton.Margin = new Padding(0, 0, controlGap, 0);
        mNextButton.Margin = new Padding(0);

        int titleHeight = TextRenderer.MeasureText(
            mScheduleTitleLabel.Text,
            mTitleFont,
            Size.Empty,
            TextFormatFlags.NoPadding | TextFormatFlags.SingleLine).Height;
        mScheduleTitleLabel.Height = titleHeight +
            DesignTokens.scaleLogicalPixel(this, DesignTokens.SPACE_8);
    }

    private void onPreviousButtonClick(object sender, EventArgs eventArgs)
    {
        EventHandler eventHandler = PreviousScheduleRequested;
        if (eventHandler != null)
        {
            eventHandler(this, EventArgs.Empty);
        }
    }

    private void onNextButtonClick(object sender, EventArgs eventArgs)
    {
        EventHandler eventHandler = NextScheduleRequested;
        if (eventHandler != null)
        {
            eventHandler(this, EventArgs.Empty);
        }
    }
}
