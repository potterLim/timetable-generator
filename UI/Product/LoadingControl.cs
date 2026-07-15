using System;
using System.Drawing;
using System.Windows.Forms;
using TimetableGenerator.Infrastructure.Csv;
using TimetableGenerator.Infrastructure.Exporting;

namespace TimetableGenerator.UI.Product;

internal sealed class LoadingControl : UserControl
{
    private const int CONTENT_MAXIMUM_WIDTH = 560;
    private const int PROGRESS_HEIGHT = 6;
    private const int MARQUEE_ANIMATION_SPEED_MILLISECONDS = 24;

    private readonly TableLayoutPanel mContentLayout;
    private readonly AppIconControl mIconControl;
    private readonly Label mTitleLabel;
    private readonly Label mDescriptionLabel;
    private readonly ProgressBar mProgressBar;
    private readonly ProductButton mCancelButton;
    private readonly Font mTitleFont;
    private readonly Font mDescriptionFont;

    internal event EventHandler? CancelRequested;

    internal LoadingControl()
    {
        AutoScaleDimensions = new SizeF(DesignTokens.BASE_DPI, DesignTokens.BASE_DPI);
        AutoScaleMode = AutoScaleMode.Dpi;
        AutoScroll = true;
        BackColor = DesignTokens.WINDOW_BACKGROUND_COLOR;
        AccessibleName = "시간표 생성 중";
        AccessibleRole = AccessibleRole.Grouping;
        TabStop = false;

        mTitleFont = DesignTokens.createWelcomeTitleFont(Font);
        mDescriptionFont = DesignTokens.createWelcomeDescriptionFont(Font);

        mContentLayout = createContentLayout();
        mIconControl = new AppIconControl(EAppIcon.Busy, DesignTokens.ACCENT_COLOR, "처리 중");
        mIconControl.Anchor = AnchorStyles.None;
        mTitleLabel = createTitleLabel();
        mDescriptionLabel = createDescriptionLabel();
        mProgressBar = createProgressBar();
        mCancelButton = new ProductButton("취소", EAppIcon.None, EProductButtonVariant.Secondary);
        mCancelButton.Anchor = AnchorStyles.None;
        mCancelButton.AccessibleDescription = "현재 CSV 처리를 취소합니다.";
        mCancelButton.Click += onCancelButtonClick;

        mContentLayout.Controls.Add(mIconControl, 0, 0);
        mContentLayout.Controls.Add(mTitleLabel, 0, 1);
        mContentLayout.Controls.Add(mDescriptionLabel, 0, 2);
        mContentLayout.Controls.Add(mProgressBar, 0, 3);
        mContentLayout.Controls.Add(mCancelButton, 0, 4);
        Controls.Add(mContentLayout);
        layoutContent();
    }

    internal void showDocumentLoading(CsvInputFileName fileName)
    {
        if (fileName == null)
        {
            throw new ArgumentNullException(nameof(fileName));
        }

        mTitleLabel.Text = "시간표를 만들고 있어요";
        mDescriptionLabel.Text = fileName.Value +
            "의 과목을 확인하고 가능한 조합을 계산하고 있습니다.";
        AccessibleName = "시간표 생성 중";
        AccessibleDescription = mDescriptionLabel.Text;
        mProgressBar.AccessibleName = "시간표 생성 진행 중";
        mCancelButton.AccessibleDescription = "현재 시간표 생성을 취소합니다.";
        resetProgress();
    }

    internal void showScheduleExportStarting(ScheduleExportChoice exportChoice)
    {
        if (exportChoice == null)
        {
            throw new ArgumentNullException(nameof(exportChoice));
        }

        mTitleLabel.Text = "PNG를 내보내고 있어요";
        if (exportChoice.Scope == EScheduleExportScope.AllSchedules)
        {
            mDescriptionLabel.Text = "모든 시간표를 선택한 폴더에 저장할 준비를 하고 있습니다.";
        }
        else
        {
            mDescriptionLabel.Text = "현재 시간표를 선택한 폴더에 저장할 준비를 하고 있습니다.";
        }

        AccessibleName = "PNG 내보내기 중";
        AccessibleDescription = mDescriptionLabel.Text;
        mProgressBar.AccessibleName = "PNG 내보내기 진행 중";
        mCancelButton.AccessibleDescription = "현재 PNG 내보내기를 취소합니다.";
        resetProgress();
    }

    internal void showScheduleExportProgress(SchedulePngExportProgress progress)
    {
        if (progress == null)
        {
            throw new ArgumentNullException(nameof(progress));
        }

        mProgressBar.Style = ProgressBarStyle.Continuous;
        mProgressBar.Minimum = 0;
        mProgressBar.Maximum = progress.TotalScheduleCount;
        mProgressBar.Value = progress.ProcessedScheduleCount;
        mDescriptionLabel.Text = progress.ProcessedScheduleCount + " / " +
            progress.TotalScheduleCount + " · 시간표 " +
            progress.ScheduleNumber.Value + "번을 처리했습니다.";
        AccessibleDescription = mDescriptionLabel.Text;
        mProgressBar.AccessibleDescription = mDescriptionLabel.Text;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            mTitleFont.Dispose();
            mDescriptionFont.Dispose();
        }

        base.Dispose(disposing);
    }

    protected override void OnSizeChanged(EventArgs eventArgs)
    {
        base.OnSizeChanged(eventArgs);
        layoutContent();
    }

    protected override void OnDpiChangedAfterParent(EventArgs eventArgs)
    {
        base.OnDpiChangedAfterParent(eventArgs);
        layoutContent();
    }

    private TableLayoutPanel createContentLayout()
    {
        TableLayoutPanel contentLayout = new TableLayoutPanel();
        contentLayout.ColumnCount = 1;
        contentLayout.RowCount = 5;
        contentLayout.BackColor = Color.Transparent;
        contentLayout.Margin = Padding.Empty;
        contentLayout.Padding = Padding.Empty;
        contentLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100.0f));
        contentLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, DesignTokens.WELCOME_ICON_SIZE));
        contentLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, DesignTokens.SPACE_48));
        contentLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, DesignTokens.SPACE_48));
        contentLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, DesignTokens.SPACE_24));
        contentLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, DesignTokens.WELCOME_PRIMARY_BUTTON_MINIMUM_HEIGHT));
        contentLayout.TabStop = false;
        return contentLayout;
    }

    private Label createTitleLabel()
    {
        Label titleLabel = new Label();
        titleLabel.Dock = DockStyle.Fill;
        titleLabel.Text = "시간표를 만들고 있어요";
        titleLabel.Font = mTitleFont;
        titleLabel.ForeColor = DesignTokens.TEXT_PRIMARY_COLOR;
        titleLabel.TextAlign = ContentAlignment.MiddleCenter;
        titleLabel.AccessibleRole = AccessibleRole.StaticText;
        titleLabel.TabStop = false;
        return titleLabel;
    }

    private Label createDescriptionLabel()
    {
        Label descriptionLabel = new Label();
        descriptionLabel.Dock = DockStyle.Fill;
        descriptionLabel.Text = "과목을 확인하고 가능한 조합을 계산하고 있습니다.";
        descriptionLabel.Font = mDescriptionFont;
        descriptionLabel.ForeColor = DesignTokens.TEXT_SECONDARY_COLOR;
        descriptionLabel.TextAlign = ContentAlignment.MiddleCenter;
        descriptionLabel.AutoEllipsis = true;
        descriptionLabel.AccessibleRole = AccessibleRole.StaticText;
        descriptionLabel.TabStop = false;
        return descriptionLabel;
    }

    private ProgressBar createProgressBar()
    {
        ProgressBar progressBar = new ProgressBar();
        progressBar.Anchor = AnchorStyles.Left | AnchorStyles.Right;
        progressBar.Height = PROGRESS_HEIGHT;
        progressBar.Style = ProgressBarStyle.Marquee;
        progressBar.MarqueeAnimationSpeed = MARQUEE_ANIMATION_SPEED_MILLISECONDS;
        progressBar.AccessibleName = "시간표 생성 진행 중";
        progressBar.AccessibleRole = AccessibleRole.ProgressBar;
        progressBar.TabStop = false;
        return progressBar;
    }

    private void onCancelButtonClick(object? senderOrNull, EventArgs eventArgs)
    {
        mCancelButton.Enabled = false;
        mDescriptionLabel.Text = "현재 작업을 안전하게 취소하고 있습니다.";
        if (CancelRequested != null)
        {
            CancelRequested(this, EventArgs.Empty);
        }
    }

    private void resetProgress()
    {
        mProgressBar.Style = ProgressBarStyle.Marquee;
        mProgressBar.MarqueeAnimationSpeed = MARQUEE_ANIMATION_SPEED_MILLISECONDS;
        mCancelButton.Enabled = true;
    }

    private void layoutContent()
    {
        // WinForms can raise SizeChanged while DPI autoscaling is still constructing this control.
        if (mContentLayout == null)
        {
            return;
        }

        int outerPadding = DesignTokens.scaleLogicalPixel(this, DesignTokens.SPACE_32);
        int maximumContentWidth = DesignTokens.scaleLogicalPixel(this, CONTENT_MAXIMUM_WIDTH);
        int availableContentWidth = Math.Max(0, ClientSize.Width - (outerPadding * 2));
        int contentWidth = Math.Min(maximumContentWidth, availableContentWidth);
        Size preferredSize = mContentLayout.GetPreferredSize(new Size(contentWidth, 0));
        int contentHeight = preferredSize.Height;
        int scrollableContentHeight = contentHeight + (outerPadding * 2);
        bool hasVerticalOverflow = scrollableContentHeight > ClientSize.Height;
        int verticalScrollbarWidth = hasVerticalOverflow
            ? SystemInformation.VerticalScrollBarWidth
            : 0;
        int viewportWidth = Math.Max(0, ClientSize.Width - verticalScrollbarWidth);
        availableContentWidth = Math.Max(0, viewportWidth - (outerPadding * 2));
        contentWidth = Math.Min(maximumContentWidth, availableContentWidth);
        preferredSize = mContentLayout.GetPreferredSize(new Size(contentWidth, 0));
        contentHeight = preferredSize.Height;
        scrollableContentHeight = contentHeight + (outerPadding * 2);
        AutoScrollMinSize = new Size(0, scrollableContentHeight);

        Point scrollOffset = AutoScrollPosition;
        int contentX = Math.Max(outerPadding, (viewportWidth - contentWidth) / 2);
        int contentY = Math.Max(outerPadding, (ClientSize.Height - contentHeight) / 2);
        mContentLayout.Bounds = new Rectangle(
            contentX + scrollOffset.X,
            contentY + scrollOffset.Y,
            contentWidth,
            contentHeight);

        int progressHeight = DesignTokens.scaleLogicalPixel(this, PROGRESS_HEIGHT);
        mProgressBar.Height = progressHeight;
        int actionHeight = DesignTokens.scaleLogicalPixel(
            this,
            DesignTokens.WELCOME_PRIMARY_BUTTON_MINIMUM_HEIGHT);
        mCancelButton.MinimumSize = new Size(mCancelButton.MinimumSize.Width, actionHeight);
    }
}
