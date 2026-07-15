using System;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;
using TimetableGenerator.Infrastructure.Csv;

namespace TimetableGenerator.UI.Product;

internal sealed class AppHeaderControl : UserControl
{
    private const int COMPACT_LAYOUT_BREAKPOINT = 1_000;
    private const int COMPACT_BUTTON_MINIMUM_WIDTH = 44;
    private const string CSV_OPEN_BUTTON_TEXT = "CSV 불러오기";
    private const string PNG_EXPORT_BUTTON_TEXT = "PNG 내보내기";
    private const string OUTPUT_FOLDER_BUTTON_TEXT = "폴더 열기";

    private readonly TableLayoutPanel mLayout;
    private readonly AppLogoControl mAppLogoControl;
    private readonly Panel mCurrentFilePanel;
    private readonly Label mCurrentFileLabel;
    private readonly FlowLayoutPanel mCommandPanel;
    private readonly ProductButton mCsvOpenButton;
    private readonly ProductButton mPngExportButton;
    private readonly ProductButton mOutputFolderButton;
    private readonly ToolTip mCommandToolTip;

    internal event EventHandler? CsvOpenRequested;
    internal event EventHandler? PngExportRequested;
    internal event EventHandler? OutputFolderOpenRequested;

    internal AppHeaderControl()
    {
        AutoScaleDimensions = new SizeF(DesignTokens.BASE_DPI, DesignTokens.BASE_DPI);
        AutoScaleMode = AutoScaleMode.Dpi;
        BackColor = DesignTokens.SURFACE_COLOR;
        AccessibleName = "애플리케이션 명령";
        AccessibleRole = AccessibleRole.ToolBar;
        TabStop = false;

        mLayout = createLayout();
        mAppLogoControl = new AppLogoControl();
        mCurrentFilePanel = createCurrentFilePanel();
        mCurrentFileLabel = createCurrentFileLabel();
        mCommandPanel = createCommandPanel();

        mCsvOpenButton = createHeaderButton(
            CSV_OPEN_BUTTON_TEXT,
            "시간표 CSV 파일 불러오기",
            EAppIcon.File);
        mPngExportButton = createHeaderButton(
            PNG_EXPORT_BUTTON_TEXT,
            "선택한 시간표를 PNG 이미지로 내보내기",
            EAppIcon.ImageExport);
        mOutputFolderButton = createHeaderButton(
            OUTPUT_FOLDER_BUTTON_TEXT,
            "마지막으로 내보낸 폴더 열기",
            EAppIcon.FolderOpen);

        mCsvOpenButton.Click += onCsvOpenButtonClick;
        mPngExportButton.Click += onPngExportButtonClick;
        mOutputFolderButton.Click += onOutputFolderButtonClick;

        mCommandToolTip = new ToolTip();
        mCommandToolTip.SetToolTip(mCsvOpenButton, CSV_OPEN_BUTTON_TEXT);
        mCommandToolTip.SetToolTip(mPngExportButton, PNG_EXPORT_BUTTON_TEXT);
        mCommandToolTip.SetToolTip(mOutputFolderButton, OUTPUT_FOLDER_BUTTON_TEXT);

        mCurrentFilePanel.Controls.Add(mCurrentFileLabel);

        mCommandPanel.Controls.Add(mCsvOpenButton);
        mCommandPanel.Controls.Add(mPngExportButton);
        mCommandPanel.Controls.Add(mOutputFolderButton);

        mLayout.Controls.Add(mAppLogoControl, 0, 0);
        mLayout.Controls.Add(mCurrentFilePanel, 1, 0);
        mLayout.Controls.Add(mCommandPanel, 2, 0);
        Controls.Add(mLayout);

        setPngExportAvailability(ECommandAvailability.Disabled);
        setOutputFolderAvailability(ECommandAvailability.Disabled);
        clearCurrentFileName();
        applyMetrics();
    }

    internal void showCurrentFileName(CsvInputFileName fileName)
    {
        if (fileName == null)
        {
            throw new ArgumentNullException(nameof(fileName));
        }

        mCurrentFileLabel.Text = fileName.Value;
        mCurrentFileLabel.AccessibleName = "현재 파일 " + fileName.Value;
        mCurrentFilePanel.Visible = true;
    }

    internal void clearCurrentFileName()
    {
        mCurrentFileLabel.Text = string.Empty;
        mCurrentFileLabel.AccessibleName = "현재 선택된 CSV 파일 없음";
        mCurrentFilePanel.Visible = false;
    }

    internal void setCsvOpenAvailability(ECommandAvailability commandAvailability)
    {
        mCsvOpenButton.Enabled = isCommandEnabled(commandAvailability);
    }

    internal void setPngExportAvailability(ECommandAvailability commandAvailability)
    {
        mPngExportButton.Enabled = isCommandEnabled(commandAvailability);
    }

    internal void setOutputFolderAvailability(ECommandAvailability commandAvailability)
    {
        mOutputFolderButton.Enabled = isCommandEnabled(commandAvailability);
    }

    protected override void OnDpiChangedAfterParent(EventArgs eventArgs)
    {
        base.OnDpiChangedAfterParent(eventArgs);
        applyMetrics();
    }

    protected override void OnSizeChanged(EventArgs eventArgs)
    {
        base.OnSizeChanged(eventArgs);
        applyCommandPresentation();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            mCommandToolTip.Dispose();
        }

        base.Dispose(disposing);
    }

    private TableLayoutPanel createLayout()
    {
        TableLayoutPanel layout = new TableLayoutPanel();
        layout.ColumnCount = 3;
        layout.RowCount = 1;
        layout.Dock = DockStyle.Fill;
        layout.Margin = Padding.Empty;
        layout.Padding = new Padding(DesignTokens.SPACE_24, DesignTokens.SPACE_12, DesignTokens.SPACE_24, DesignTokens.SPACE_12);
        layout.BackColor = DesignTokens.SURFACE_COLOR;
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100.0f));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100.0f));
        return layout;
    }

    private Panel createCurrentFilePanel()
    {
        Panel currentFilePanel = new Panel();
        currentFilePanel.Dock = DockStyle.Fill;
        currentFilePanel.Margin = new Padding(DesignTokens.SPACE_20, 0, DesignTokens.SPACE_16, 0);
        currentFilePanel.Padding = new Padding(DesignTokens.SPACE_32, 0, 0, 0);
        currentFilePanel.AccessibleName = "현재 파일";
        currentFilePanel.AccessibleRole = AccessibleRole.Grouping;
        currentFilePanel.TabStop = false;
        currentFilePanel.Paint += onCurrentFilePanelPaint;
        return currentFilePanel;
    }

    private Label createCurrentFileLabel()
    {
        Label currentFileLabel = new Label();
        currentFileLabel.Dock = DockStyle.Fill;
        currentFileLabel.AutoEllipsis = true;
        currentFileLabel.TextAlign = ContentAlignment.MiddleLeft;
        currentFileLabel.ForeColor = DesignTokens.TEXT_PRIMARY_COLOR;
        currentFileLabel.BackColor = Color.Transparent;
        currentFileLabel.AccessibleRole = AccessibleRole.StaticText;
        currentFileLabel.TabStop = false;
        return currentFileLabel;
    }

    private FlowLayoutPanel createCommandPanel()
    {
        FlowLayoutPanel commandPanel = new FlowLayoutPanel();
        commandPanel.AutoSize = true;
        commandPanel.AutoSizeMode = AutoSizeMode.GrowAndShrink;
        commandPanel.Dock = DockStyle.Fill;
        commandPanel.FlowDirection = FlowDirection.LeftToRight;
        commandPanel.WrapContents = false;
        commandPanel.Margin = Padding.Empty;
        commandPanel.Padding = Padding.Empty;
        commandPanel.BackColor = DesignTokens.SURFACE_COLOR;
        commandPanel.AccessibleName = "파일 명령";
        commandPanel.AccessibleRole = AccessibleRole.ToolBar;
        commandPanel.TabStop = false;
        return commandPanel;
    }

    private ProductButton createHeaderButton(
        string text,
        string accessibleDescription,
        EAppIcon appIcon)
    {
        ProductButton button = new ProductButton(text, appIcon, EProductButtonVariant.Secondary);
        button.AccessibleDescription = accessibleDescription;
        button.Margin = new Padding(DesignTokens.SPACE_4, 0, DesignTokens.SPACE_4, 0);
        return button;
    }

    private void onCurrentFilePanelPaint(object? senderOrNull, PaintEventArgs paintEventArgs)
    {
        int separatorX = 0;
        using (Pen separatorPen = new Pen(DesignTokens.BORDER_COLOR, DesignTokens.scaleLogicalPixel(this, DesignTokens.BORDER_WIDTH)))
        {
            paintEventArgs.Graphics.DrawLine(separatorPen, separatorX, 0, separatorX, mCurrentFilePanel.ClientSize.Height);
        }

        int iconSize = DesignTokens.scaleLogicalPixel(this, DesignTokens.BUTTON_ICON_SIZE);
        int iconX = DesignTokens.scaleLogicalPixel(this, DesignTokens.SPACE_12);
        int iconY = (mCurrentFilePanel.ClientSize.Height - iconSize) / 2;
        Rectangle iconBounds = new Rectangle(iconX, iconY, iconSize, iconSize);
        AppIconPainter.drawIcon(
            paintEventArgs.Graphics,
            iconBounds,
            EAppIcon.File,
            DesignTokens.TEXT_SECONDARY_COLOR);
    }

    private void onCsvOpenButtonClick(object? senderOrNull, EventArgs eventArgs)
    {
        if (CsvOpenRequested != null)
        {
            CsvOpenRequested(this, EventArgs.Empty);
        }
    }

    private void onPngExportButtonClick(object? senderOrNull, EventArgs eventArgs)
    {
        if (PngExportRequested != null)
        {
            PngExportRequested(this, EventArgs.Empty);
        }
    }

    private void onOutputFolderButtonClick(object? senderOrNull, EventArgs eventArgs)
    {
        if (OutputFolderOpenRequested != null)
        {
            OutputFolderOpenRequested(this, EventArgs.Empty);
        }
    }

    private void applyMetrics()
    {
        Height = DesignTokens.scaleLogicalPixel(this, DesignTokens.APP_HEADER_HEIGHT);
        MinimumSize = new Size(0, Height);

        int buttonMinimumHeight = DesignTokens.scaleLogicalPixel(this, DesignTokens.BUTTON_MINIMUM_HEIGHT);
        mCsvOpenButton.MinimumSize = new Size(mCsvOpenButton.MinimumSize.Width, buttonMinimumHeight);
        mPngExportButton.MinimumSize = new Size(mPngExportButton.MinimumSize.Width, buttonMinimumHeight);
        mOutputFolderButton.MinimumSize = new Size(mOutputFolderButton.MinimumSize.Width, buttonMinimumHeight);

        applyCommandPresentation();
        mCurrentFilePanel.Invalidate();
    }

    private void applyCommandPresentation()
    {
        if (mCsvOpenButton == null)
        {
            return;
        }

        int compactBreakpoint = DesignTokens.scaleLogicalPixel(
            this,
            COMPACT_LAYOUT_BREAKPOINT);
        bool isCompactLayout = ClientSize.Width < compactBreakpoint;
        int buttonMinimumWidth = isCompactLayout
            ? DesignTokens.scaleLogicalPixel(this, COMPACT_BUTTON_MINIMUM_WIDTH)
            : DesignTokens.scaleLogicalPixel(this, DesignTokens.HEADER_BUTTON_MINIMUM_WIDTH);
        int buttonMinimumHeight = DesignTokens.scaleLogicalPixel(
            this,
            DesignTokens.BUTTON_MINIMUM_HEIGHT);
        Size buttonMinimumSize = new Size(buttonMinimumWidth, buttonMinimumHeight);

        mCsvOpenButton.Text = isCompactLayout ? string.Empty : CSV_OPEN_BUTTON_TEXT;
        mPngExportButton.Text = isCompactLayout ? string.Empty : PNG_EXPORT_BUTTON_TEXT;
        mOutputFolderButton.Text = isCompactLayout ? string.Empty : OUTPUT_FOLDER_BUTTON_TEXT;
        mCsvOpenButton.MinimumSize = buttonMinimumSize;
        mPngExportButton.MinimumSize = buttonMinimumSize;
        mOutputFolderButton.MinimumSize = buttonMinimumSize;
    }

    private static bool isCommandEnabled(ECommandAvailability commandAvailability)
    {
        switch (commandAvailability)
        {
            case ECommandAvailability.Enabled:
                return true;
            case ECommandAvailability.Disabled:
                return false;
            default:
                Debug.Fail("Unexpected command availability: " + commandAvailability);
                return false;
        }
    }
}
