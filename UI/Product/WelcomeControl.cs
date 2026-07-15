using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using TimetableGenerator.Infrastructure.Csv;

namespace TimetableGenerator.UI.Product;

internal sealed class WelcomeControl : UserControl
{
    private const string IDLE_DROP_MESSAGE = "CSV 파일을 여기에 놓거나";
    private const string READY_DROP_MESSAGE = "CSV 파일을 놓아 불러오기";
    private const string INVALID_DROP_MESSAGE = "CSV 파일 하나만 놓을 수 있어요";

    private readonly Panel mContentPanel;
    private readonly AppIconControl mHeroIconControl;
    private readonly Label mTitleLabel;
    private readonly Label mDescriptionLabel;
    private readonly Panel mDropZonePanel;
    private readonly Label mDropInstructionLabel;
    private readonly ProductButton mCsvSelectButton;
    private readonly LinkLabel mExampleFormatLink;
    private readonly TableLayoutPanel mStepLayout;

    private readonly Font mTitleFont;
    private readonly Font mDescriptionFont;
    private readonly Font mBodyFont;

    private EFileDropVisualState mFileDropVisualState;

    internal event EventHandler? CsvOpenRequested;
    internal event EventHandler? ExampleFormatRequested;
    internal event EventHandler<CsvFileDroppedEventArgs>? CsvFileDropped;

    internal WelcomeControl()
    {
        AutoScaleDimensions = new SizeF(DesignTokens.BASE_DPI, DesignTokens.BASE_DPI);
        AutoScaleMode = AutoScaleMode.Dpi;
        AllowDrop = true;
        AutoScroll = true;
        BackColor = DesignTokens.WINDOW_BACKGROUND_COLOR;
        AccessibleName = "시간표 시작 화면";
        AccessibleDescription = "CSV 파일을 선택하거나 끌어 놓아 시간표 조합을 만듭니다.";
        AccessibleRole = AccessibleRole.Grouping;
        TabStop = false;

        mTitleFont = DesignTokens.createWelcomeTitleFont(Font);
        mDescriptionFont = DesignTokens.createWelcomeDescriptionFont(Font);
        mBodyFont = DesignTokens.createBodyFont(Font);

        mContentPanel = createContentPanel();
        mHeroIconControl = createHeroIconControl();
        mTitleLabel = createTitleLabel();
        mDescriptionLabel = createDescriptionLabel();
        mDropZonePanel = createDropZonePanel();
        mDropInstructionLabel = createDropInstructionLabel();
        mCsvSelectButton = createCsvSelectButton();
        mExampleFormatLink = createExampleFormatLink();
        mStepLayout = createStepLayout();

        TableLayoutPanel dropContentLayout = createDropContentLayout();
        dropContentLayout.Controls.Add(mDropInstructionLabel, 0, 1);
        dropContentLayout.Controls.Add(mCsvSelectButton, 0, 2);
        mDropZonePanel.Controls.Add(dropContentLayout);

        mContentPanel.Controls.Add(mHeroIconControl);
        mContentPanel.Controls.Add(mTitleLabel);
        mContentPanel.Controls.Add(mDescriptionLabel);
        mContentPanel.Controls.Add(mDropZonePanel);
        mContentPanel.Controls.Add(mExampleFormatLink);
        mContentPanel.Controls.Add(mStepLayout);
        Controls.Add(mContentPanel);

        attachDropEvents(this);
        attachDropEvents(mContentPanel);
        attachDropEvents(mDropZonePanel);
        attachDropEvents(mDropInstructionLabel);
        attachDropEvents(mCsvSelectButton);

        showFileDropVisualState(EFileDropVisualState.Idle);
        layoutContent();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            mTitleFont.Dispose();
            mDescriptionFont.Dispose();
            mBodyFont.Dispose();
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
        mDropZonePanel.Invalidate();
    }

    private Panel createContentPanel()
    {
        Panel contentPanel = new Panel();
        contentPanel.BackColor = DesignTokens.WINDOW_BACKGROUND_COLOR;
        contentPanel.Margin = Padding.Empty;
        contentPanel.TabStop = false;
        return contentPanel;
    }

    private AppIconControl createHeroIconControl()
    {
        AppIconControl heroIconControl = new AppIconControl(
            EAppIcon.Calendar,
            DesignTokens.ACCENT_COLOR,
            "시간표 달력");
        heroIconControl.BackColor = Color.Transparent;
        return heroIconControl;
    }

    private Label createTitleLabel()
    {
        Label titleLabel = new Label();
        titleLabel.Text = "내게 맞는 시간표를 찾아보세요";
        titleLabel.Font = mTitleFont;
        titleLabel.ForeColor = DesignTokens.TEXT_PRIMARY_COLOR;
        titleLabel.TextAlign = ContentAlignment.MiddleCenter;
        titleLabel.AutoEllipsis = true;
        titleLabel.AccessibleRole = AccessibleRole.StaticText;
        titleLabel.TabStop = false;
        return titleLabel;
    }

    private Label createDescriptionLabel()
    {
        Label descriptionLabel = new Label();
        descriptionLabel.Text = "CSV 파일을 불러오면 충돌 없는 모든 조합을 자동으로 만들어요.";
        descriptionLabel.Font = mDescriptionFont;
        descriptionLabel.ForeColor = DesignTokens.TEXT_SECONDARY_COLOR;
        descriptionLabel.TextAlign = ContentAlignment.MiddleCenter;
        descriptionLabel.AutoEllipsis = true;
        descriptionLabel.AccessibleRole = AccessibleRole.StaticText;
        descriptionLabel.TabStop = false;
        return descriptionLabel;
    }

    private Panel createDropZonePanel()
    {
        Panel dropZonePanel = new Panel();
        dropZonePanel.AllowDrop = true;
        dropZonePanel.BackColor = DesignTokens.SURFACE_COLOR;
        dropZonePanel.AccessibleName = "CSV 파일 놓기 영역";
        dropZonePanel.AccessibleDescription = "CSV 파일 하나를 끌어 놓거나 파일 선택 버튼을 사용하세요.";
        dropZonePanel.AccessibleRole = AccessibleRole.Grouping;
        dropZonePanel.TabStop = false;
        dropZonePanel.Paint += onDropZonePanelPaint;
        return dropZonePanel;
    }

    private Label createDropInstructionLabel()
    {
        Label dropInstructionLabel = new Label();
        dropInstructionLabel.Dock = DockStyle.Fill;
        dropInstructionLabel.Font = mDescriptionFont;
        dropInstructionLabel.ForeColor = DesignTokens.TEXT_PRIMARY_COLOR;
        dropInstructionLabel.TextAlign = ContentAlignment.MiddleCenter;
        dropInstructionLabel.AccessibleRole = AccessibleRole.StaticText;
        dropInstructionLabel.AllowDrop = true;
        dropInstructionLabel.TabStop = false;
        return dropInstructionLabel;
    }

    private ProductButton createCsvSelectButton()
    {
        ProductButton csvSelectButton = new ProductButton(
            "CSV 파일 선택",
            EAppIcon.FolderOpen,
            EProductButtonVariant.Primary);
        csvSelectButton.Anchor = AnchorStyles.None;
        csvSelectButton.AccessibleDescription = "CSV 파일 선택 창을 엽니다.";
        csvSelectButton.AllowDrop = true;
        csvSelectButton.Click += onCsvSelectButtonClick;
        return csvSelectButton;
    }

    private LinkLabel createExampleFormatLink()
    {
        LinkLabel exampleFormatLink = new LinkLabel();
        exampleFormatLink.Text = "예제 CSV 형식 보기";
        exampleFormatLink.Font = mBodyFont;
        exampleFormatLink.LinkColor = DesignTokens.ACCENT_COLOR;
        exampleFormatLink.ActiveLinkColor = DesignTokens.ACCENT_PRESSED_COLOR;
        exampleFormatLink.VisitedLinkColor = DesignTokens.ACCENT_COLOR;
        exampleFormatLink.TextAlign = ContentAlignment.MiddleCenter;
        exampleFormatLink.AccessibleName = "예제 CSV 형식 보기";
        exampleFormatLink.AccessibleDescription = "CSV 작성 형식 안내를 엽니다.";
        exampleFormatLink.AccessibleRole = AccessibleRole.Link;
        exampleFormatLink.TabStop = true;
        exampleFormatLink.LinkClicked += onExampleFormatLinkClicked;
        return exampleFormatLink;
    }

    private TableLayoutPanel createStepLayout()
    {
        TableLayoutPanel stepLayout = new TableLayoutPanel();
        stepLayout.ColumnCount = 3;
        stepLayout.RowCount = 1;
        stepLayout.Margin = Padding.Empty;
        stepLayout.Padding = Padding.Empty;
        stepLayout.BackColor = Color.Transparent;
        stepLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.333f));
        stepLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.333f));
        stepLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.334f));
        stepLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100.0f));
        stepLayout.TabStop = false;

        stepLayout.Controls.Add(createStepLabel("1. 과목 정보 준비"), 0, 0);
        stepLayout.Controls.Add(createStepLabel("2. CSV 불러오기"), 1, 0);
        stepLayout.Controls.Add(createStepLabel("3. 시간표 비교"), 2, 0);
        return stepLayout;
    }

    private Label createStepLabel(string text)
    {
        Label stepLabel = new Label();
        stepLabel.Dock = DockStyle.Fill;
        stepLabel.Text = text;
        stepLabel.Font = mBodyFont;
        stepLabel.ForeColor = DesignTokens.TEXT_PRIMARY_COLOR;
        stepLabel.TextAlign = ContentAlignment.MiddleCenter;
        stepLabel.AutoEllipsis = true;
        stepLabel.AccessibleRole = AccessibleRole.StaticText;
        stepLabel.TabStop = false;
        return stepLabel;
    }

    private TableLayoutPanel createDropContentLayout()
    {
        TableLayoutPanel dropContentLayout = new TableLayoutPanel();
        dropContentLayout.ColumnCount = 1;
        dropContentLayout.RowCount = 4;
        dropContentLayout.Dock = DockStyle.Fill;
        dropContentLayout.Padding = new Padding(DesignTokens.SPACE_24);
        dropContentLayout.BackColor = Color.Transparent;
        dropContentLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100.0f));
        dropContentLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 50.0f));
        dropContentLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        dropContentLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        dropContentLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 50.0f));
        dropContentLayout.TabStop = false;
        return dropContentLayout;
    }

    private void attachDropEvents(Control control)
    {
        control.AllowDrop = true;
        control.DragEnter += onCsvDragEnter;
        control.DragOver += onCsvDragEnter;
        control.DragLeave += onCsvDragLeave;
        control.DragDrop += onCsvDragDrop;
    }

    private void onCsvSelectButtonClick(object? senderOrNull, EventArgs eventArgs)
    {
        if (CsvOpenRequested != null)
        {
            CsvOpenRequested(this, EventArgs.Empty);
        }
    }

    private void onExampleFormatLinkClicked(object? senderOrNull, LinkLabelLinkClickedEventArgs eventArgs)
    {
        if (ExampleFormatRequested != null)
        {
            ExampleFormatRequested(this, EventArgs.Empty);
        }
    }

    private void onCsvDragEnter(object? senderOrNull, DragEventArgs dragEventArgs)
    {
        CsvInputFilePath droppedCsvFilePath;
        if (tryFindSingleCsvFilePath(dragEventArgs, out droppedCsvFilePath))
        {
            dragEventArgs.Effect = DragDropEffects.Copy;
            showFileDropVisualState(EFileDropVisualState.Ready);
            return;
        }

        dragEventArgs.Effect = DragDropEffects.None;
        showFileDropVisualState(EFileDropVisualState.Invalid);
    }

    private void onCsvDragLeave(object? senderOrNull, EventArgs eventArgs)
    {
        showFileDropVisualState(EFileDropVisualState.Idle);
    }

    private void onCsvDragDrop(object? senderOrNull, DragEventArgs dragEventArgs)
    {
        CsvInputFilePath droppedCsvFilePath;
        if (tryFindSingleCsvFilePath(dragEventArgs, out droppedCsvFilePath) == false)
        {
            showFileDropVisualState(EFileDropVisualState.Invalid);
            return;
        }

        showFileDropVisualState(EFileDropVisualState.Idle);
        if (CsvFileDropped != null)
        {
            CsvFileDropped(this, new CsvFileDroppedEventArgs(droppedCsvFilePath));
        }
    }

    private void onDropZonePanelPaint(object? senderOrNull, PaintEventArgs paintEventArgs)
    {
        Rectangle borderBounds = ProductDrawing.insetRectangle(mDropZonePanel.ClientRectangle, 1);
        int cornerRadius = DesignTokens.scaleLogicalPixel(this, DesignTokens.CORNER_RADIUS_MEDIUM);
        Color borderColor = findDropZoneBorderColor();

        GraphicsState graphicsState = paintEventArgs.Graphics.Save();
        try
        {
            paintEventArgs.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using (GraphicsPath borderPath =
                ProductDrawing.createRoundedRectanglePath(borderBounds, cornerRadius))
            {
                using (Pen borderPen = new Pen(
                    borderColor,
                    DesignTokens.scaleLogicalPixel(
                        this,
                        DesignTokens.FOCUS_RING_WIDTH)))
                {
                    borderPen.DashStyle = DashStyle.Dash;
                    paintEventArgs.Graphics.DrawPath(borderPen, borderPath);
                }
            }
        }
        finally
        {
            paintEventArgs.Graphics.Restore(graphicsState);
        }
    }

    private void showFileDropVisualState(EFileDropVisualState fileDropVisualState)
    {
        mFileDropVisualState = fileDropVisualState;

        switch (mFileDropVisualState)
        {
            case EFileDropVisualState.Idle:
                mDropInstructionLabel.Text = IDLE_DROP_MESSAGE;
                mDropInstructionLabel.ForeColor = DesignTokens.TEXT_PRIMARY_COLOR;
                break;
            case EFileDropVisualState.Ready:
                mDropInstructionLabel.Text = READY_DROP_MESSAGE;
                mDropInstructionLabel.ForeColor = DesignTokens.ACCENT_COLOR;
                break;
            case EFileDropVisualState.Invalid:
                mDropInstructionLabel.Text = INVALID_DROP_MESSAGE;
                mDropInstructionLabel.ForeColor = DesignTokens.ERROR_COLOR;
                break;
            default:
                Debug.Fail("Unexpected file drop visual state: " + mFileDropVisualState);
                mDropInstructionLabel.Text = IDLE_DROP_MESSAGE;
                mDropInstructionLabel.ForeColor = DesignTokens.TEXT_PRIMARY_COLOR;
                break;
        }

        mDropZonePanel.Invalidate();
    }

    private Color findDropZoneBorderColor()
    {
        switch (mFileDropVisualState)
        {
            case EFileDropVisualState.Idle:
                return DesignTokens.ACCENT_COLOR;
            case EFileDropVisualState.Ready:
                return DesignTokens.ACCENT_HOVER_COLOR;
            case EFileDropVisualState.Invalid:
                return DesignTokens.ERROR_COLOR;
            default:
                Debug.Fail("Unexpected file drop visual state: " + mFileDropVisualState);
                return DesignTokens.ACCENT_COLOR;
        }
    }

    private bool tryFindSingleCsvFilePath(
        DragEventArgs dragEventArgs,
        out CsvInputFilePath droppedCsvFilePath)
    {
        droppedCsvFilePath = default(CsvInputFilePath);
        if (dragEventArgs.Data == null || dragEventArgs.Data.GetDataPresent(DataFormats.FileDrop) == false)
        {
            return false;
        }

        object? droppedDataOrNull = dragEventArgs.Data.GetData(DataFormats.FileDrop);
        string[]? droppedFilePathsOrNull = droppedDataOrNull as string[];
        if (droppedFilePathsOrNull == null || droppedFilePathsOrNull.Length != 1)
        {
            return false;
        }

        try
        {
            droppedCsvFilePath = new CsvInputFilePath(droppedFilePathsOrNull[0]);
        }
        catch (Exception exception) when (
            exception is ArgumentException ||
            exception is NotSupportedException)
        {
            return false;
        }

        return true;
    }

    private void layoutContent()
    {
        // WinForms can raise SizeChanged while DPI autoscaling is still constructing this control.
        if (mContentPanel == null)
        {
            return;
        }

        int outerPadding = DesignTokens.scaleLogicalPixel(this, DesignTokens.SPACE_32);
        int maximumContentWidth = DesignTokens.scaleLogicalPixel(this, DesignTokens.WELCOME_CONTENT_MAXIMUM_WIDTH);
        int iconSize = DesignTokens.scaleLogicalPixel(this, DesignTokens.WELCOME_ICON_SIZE);
        int titleHeight = DesignTokens.scaleLogicalPixel(this, DesignTokens.SPACE_48);
        int descriptionHeight = DesignTokens.scaleLogicalPixel(this, DesignTokens.SPACE_32);
        int dropZoneHeight = DesignTokens.scaleLogicalPixel(this, DesignTokens.WELCOME_DROP_ZONE_HEIGHT);
        int linkHeight = DesignTokens.scaleLogicalPixel(this, DesignTokens.SPACE_32);
        int stepsHeight = DesignTokens.scaleLogicalPixel(this, DesignTokens.SPACE_40);

        int titleTopGap = DesignTokens.scaleLogicalPixel(this, DesignTokens.SPACE_16);
        int descriptionTopGap = DesignTokens.scaleLogicalPixel(this, DesignTokens.SPACE_4);
        int dropZoneTopGap = DesignTokens.scaleLogicalPixel(this, DesignTokens.SPACE_24);
        int linkTopGap = DesignTokens.scaleLogicalPixel(this, DesignTokens.SPACE_12);
        int stepsTopGap = DesignTokens.scaleLogicalPixel(this, DesignTokens.SPACE_12);

        int heroHeight = iconSize + titleTopGap + titleHeight +
            descriptionTopGap + descriptionHeight;
        int dropSectionHeight = dropZoneTopGap + dropZoneHeight;
        int footerHeight = linkTopGap + linkHeight + stepsTopGap + stepsHeight;
        int contentHeight = heroHeight + dropSectionHeight + footerHeight;
        int scrollableContentHeight = contentHeight + (outerPadding * 2);
        bool hasVerticalOverflow = scrollableContentHeight > ClientSize.Height;
        int verticalScrollbarWidth = hasVerticalOverflow
            ? SystemInformation.VerticalScrollBarWidth
            : 0;

        int viewportWidth = Math.Max(0, ClientSize.Width - verticalScrollbarWidth);
        int availableContentWidth = Math.Max(0, viewportWidth - (outerPadding * 2));
        int contentWidth = Math.Min(maximumContentWidth, availableContentWidth);
        AutoScrollMinSize = new Size(0, scrollableContentHeight);

        Point scrollOffset = AutoScrollPosition;
        int contentX = Math.Max(outerPadding, (viewportWidth - contentWidth) / 2);
        int contentY = Math.Max(outerPadding, (ClientSize.Height - contentHeight) / 2);
        mContentPanel.Bounds = new Rectangle(
            contentX + scrollOffset.X,
            contentY + scrollOffset.Y,
            contentWidth,
            contentHeight);

        int currentY = 0;
        int iconX = (contentWidth - iconSize) / 2;
        mHeroIconControl.Bounds = new Rectangle(iconX, currentY, iconSize, iconSize);

        currentY += iconSize + titleTopGap;
        mTitleLabel.Bounds = new Rectangle(0, currentY, contentWidth, titleHeight);

        currentY += titleHeight + descriptionTopGap;
        mDescriptionLabel.Bounds = new Rectangle(0, currentY, contentWidth, descriptionHeight);

        currentY += descriptionHeight + dropZoneTopGap;
        mDropZonePanel.Bounds = new Rectangle(0, currentY, contentWidth, dropZoneHeight);

        currentY += dropZoneHeight + linkTopGap;
        mExampleFormatLink.Bounds = new Rectangle(0, currentY, contentWidth, linkHeight);

        currentY += linkHeight + stepsTopGap;
        mStepLayout.Bounds = new Rectangle(0, currentY, contentWidth, stepsHeight);

        int primaryButtonHeight = DesignTokens.scaleLogicalPixel(this, DesignTokens.WELCOME_PRIMARY_BUTTON_MINIMUM_HEIGHT);
        mCsvSelectButton.MinimumSize = new Size(mCsvSelectButton.MinimumSize.Width, primaryButtonHeight);
    }
}
