using System;
using System.Drawing;
using System.Windows.Forms;
using TimetableGenerator.Infrastructure.Exporting;

namespace TimetableGenerator.UI.Product;

internal sealed class ScheduleExportDialog : Form
{
    private const int PREFERRED_CLIENT_WIDTH = 640;
    private const int PREFERRED_CLIENT_HEIGHT = 430;
    private const int MINIMUM_WINDOW_WIDTH = 520;
    private const int MINIMUM_WINDOW_HEIGHT = 360;

    private readonly ScheduleExportDialogContext mContext;
    private readonly RadioButton mCurrentScheduleRadioButton;
    private readonly RadioButton mAllSchedulesRadioButton;
    private readonly TextBox mDestinationTextBox;
    private readonly Font mTitleFont;
    private readonly Font mBodyFont;
    private ScheduleExportDirectoryPath mSelectedDirectory;
    private ScheduleExportChoice? mChoiceOrNull;

    internal ScheduleExportDialog(ScheduleExportDialogContext context)
    {
        if (context == null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        mContext = context;
        mSelectedDirectory = context.InitialDirectory;
        mTitleFont = DesignTokens.createSectionTitleFont(Font);
        mBodyFont = DesignTokens.createBodyFont(Font);

        AutoScaleDimensions = new SizeF(DesignTokens.BASE_DPI, DesignTokens.BASE_DPI);
        AutoScaleMode = AutoScaleMode.Dpi;
        BackColor = DesignTokens.WINDOW_BACKGROUND_COLOR;
        ClientSize = new Size(PREFERRED_CLIENT_WIDTH, PREFERRED_CLIENT_HEIGHT);
        FormBorderStyle = FormBorderStyle.Sizable;
        MaximizeBox = false;
        MinimumSize = new Size(MINIMUM_WINDOW_WIDTH, MINIMUM_WINDOW_HEIGHT);
        MinimizeBox = false;
        SizeGripStyle = SizeGripStyle.Show;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterParent;
        Text = "PNG 내보내기";

        mCurrentScheduleRadioButton = createScopeRadioButton(
            "현재 시간표 " + context.SelectedScheduleNumber + "번만 내보내기",
            "화면에 표시된 시간표 한 개만 저장합니다.");
        mAllSchedulesRadioButton = createScopeRadioButton(
            "모든 시간표 " + context.TotalScheduleCount + "개 내보내기",
            "생성된 모든 시간표를 각각의 PNG 파일로 저장합니다.");
        if (context.InitialScope == EScheduleExportScope.AllSchedules)
        {
            mAllSchedulesRadioButton.Checked = true;
        }
        else
        {
            mCurrentScheduleRadioButton.Checked = true;
        }
        mDestinationTextBox = createDestinationTextBox();

        TableLayoutPanel layout = createLayout();
        layout.Controls.Add(createTitleLabel(), 0, 0);
        layout.Controls.Add(createDescriptionLabel(), 0, 1);
        layout.Controls.Add(createScopePanel(), 0, 2);
        layout.Controls.Add(createDestinationPanel(), 0, 3);
        layout.Controls.Add(createFooter(), 0, 4);
        Panel scrollHost = createScrollHost();
        scrollHost.Controls.Add(layout);
        Controls.Add(scrollHost);
    }

    internal ScheduleExportChoice getChoice()
    {
        if (mChoiceOrNull == null)
        {
            throw new InvalidOperationException("The export dialog has not produced a choice.");
        }

        return mChoiceOrNull;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            mTitleFont.Dispose();
            mBodyFont.Dispose();
        }

        base.Dispose(disposing);
    }

    protected override void OnLoad(EventArgs eventArgs)
    {
        base.OnLoad(eventArgs);
        ClientSize = ProductDialogSizing.findInitialClientSize(
            this,
            new Size(PREFERRED_CLIENT_WIDTH, PREFERRED_CLIENT_HEIGHT));
    }

    private TableLayoutPanel createLayout()
    {
        TableLayoutPanel layout = new TableLayoutPanel();
        layout.ColumnCount = 1;
        layout.RowCount = 5;
        layout.Dock = DockStyle.Top;
        layout.AutoSize = true;
        layout.AutoSizeMode = AutoSizeMode.GrowAndShrink;
        layout.Padding = new Padding(DesignTokens.SPACE_32, DesignTokens.SPACE_24, DesignTokens.SPACE_32, DesignTokens.SPACE_24);
        layout.BackColor = DesignTokens.WINDOW_BACKGROUND_COLOR;
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100.0f));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 38.0f));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 42.0f));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 132.0f));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 88.0f));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 48.0f));
        return layout;
    }

    private Panel createScrollHost()
    {
        Panel scrollHost = new Panel();
        scrollHost.Dock = DockStyle.Fill;
        scrollHost.AutoScroll = true;
        scrollHost.BackColor = DesignTokens.WINDOW_BACKGROUND_COLOR;
        scrollHost.TabStop = false;
        return scrollHost;
    }

    private Label createTitleLabel()
    {
        Label titleLabel = new Label();
        titleLabel.Dock = DockStyle.Fill;
        titleLabel.Text = "시간표를 PNG로 내보내기";
        titleLabel.Font = mTitleFont;
        titleLabel.ForeColor = DesignTokens.TEXT_PRIMARY_COLOR;
        titleLabel.TextAlign = ContentAlignment.MiddleLeft;
        titleLabel.AccessibleRole = AccessibleRole.StaticText;
        titleLabel.TabStop = false;
        return titleLabel;
    }

    private Label createDescriptionLabel()
    {
        Label descriptionLabel = new Label();
        descriptionLabel.Dock = DockStyle.Fill;
        descriptionLabel.Text = mContext.SourceFileName.Value +
            "에서 만든 시간표의 저장 범위와 위치를 선택하세요.";
        descriptionLabel.Font = mBodyFont;
        descriptionLabel.ForeColor = DesignTokens.TEXT_SECONDARY_COLOR;
        descriptionLabel.TextAlign = ContentAlignment.MiddleLeft;
        descriptionLabel.AutoEllipsis = true;
        descriptionLabel.AccessibleRole = AccessibleRole.StaticText;
        descriptionLabel.TabStop = false;
        return descriptionLabel;
    }

    private Control createScopePanel()
    {
        TableLayoutPanel scopePanel = new TableLayoutPanel();
        scopePanel.ColumnCount = 1;
        scopePanel.RowCount = 2;
        scopePanel.Dock = DockStyle.Fill;
        scopePanel.Margin = new Padding(0, DesignTokens.SPACE_8, 0, DesignTokens.SPACE_8);
        scopePanel.Padding = new Padding(DesignTokens.SPACE_16, DesignTokens.SPACE_8, DesignTokens.SPACE_16, DesignTokens.SPACE_8);
        scopePanel.BackColor = DesignTokens.SUBTLE_SURFACE_COLOR;
        scopePanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100.0f));
        scopePanel.RowStyles.Add(new RowStyle(SizeType.Percent, 50.0f));
        scopePanel.RowStyles.Add(new RowStyle(SizeType.Percent, 50.0f));
        scopePanel.Controls.Add(mCurrentScheduleRadioButton, 0, 0);
        scopePanel.Controls.Add(mAllSchedulesRadioButton, 0, 1);
        return scopePanel;
    }

    private RadioButton createScopeRadioButton(string title, string description)
    {
        RadioButton radioButton = new RadioButton();
        radioButton.Dock = DockStyle.Fill;
        radioButton.Text = title;
        radioButton.Font = mBodyFont;
        radioButton.ForeColor = DesignTokens.TEXT_PRIMARY_COLOR;
        radioButton.BackColor = Color.Transparent;
        radioButton.AutoEllipsis = true;
        radioButton.AccessibleName = title;
        radioButton.AccessibleDescription = description;
        radioButton.TabStop = true;
        radioButton.UseVisualStyleBackColor = true;
        return radioButton;
    }

    private Control createDestinationPanel()
    {
        TableLayoutPanel destinationPanel = new TableLayoutPanel();
        destinationPanel.ColumnCount = 2;
        destinationPanel.RowCount = 2;
        destinationPanel.Dock = DockStyle.Fill;
        destinationPanel.Margin = Padding.Empty;
        destinationPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100.0f));
        destinationPanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        destinationPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 32.0f));
        destinationPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 44.0f));

        Label destinationLabel = new Label();
        destinationLabel.Dock = DockStyle.Fill;
        destinationLabel.Text = "저장 위치";
        destinationLabel.Font = mBodyFont;
        destinationLabel.ForeColor = DesignTokens.TEXT_PRIMARY_COLOR;
        destinationLabel.TextAlign = ContentAlignment.BottomLeft;
        destinationLabel.AccessibleRole = AccessibleRole.StaticText;
        destinationLabel.TabStop = false;

        ProductButton browseButton = new ProductButton(
            "찾아보기",
            EAppIcon.FolderOpen,
            EProductButtonVariant.Secondary);
        browseButton.Margin = new Padding(DesignTokens.SPACE_8, DesignTokens.SPACE_4, 0, 0);
        browseButton.AccessibleDescription = "PNG를 저장할 폴더를 선택합니다.";
        browseButton.Click += onBrowseButtonClick;

        destinationPanel.Controls.Add(destinationLabel, 0, 0);
        destinationPanel.SetColumnSpan(destinationLabel, 2);
        destinationPanel.Controls.Add(mDestinationTextBox, 0, 1);
        destinationPanel.Controls.Add(browseButton, 1, 1);
        return destinationPanel;
    }

    private TextBox createDestinationTextBox()
    {
        TextBox destinationTextBox = new TextBox();
        destinationTextBox.Dock = DockStyle.Fill;
        destinationTextBox.Margin = new Padding(0, DesignTokens.SPACE_8, 0, 0);
        destinationTextBox.ReadOnly = true;
        destinationTextBox.Font = mBodyFont;
        destinationTextBox.BackColor = DesignTokens.SURFACE_COLOR;
        destinationTextBox.ForeColor = DesignTokens.TEXT_PRIMARY_COLOR;
        destinationTextBox.Text = mSelectedDirectory.Value;
        destinationTextBox.AccessibleName = "PNG 저장 위치";
        destinationTextBox.TabStop = true;
        return destinationTextBox;
    }

    private Control createFooter()
    {
        FlowLayoutPanel footer = new FlowLayoutPanel();
        footer.Dock = DockStyle.Fill;
        footer.FlowDirection = FlowDirection.RightToLeft;
        footer.WrapContents = false;
        footer.Margin = Padding.Empty;
        footer.Padding = Padding.Empty;

        ProductButton exportButton = new ProductButton(
            "내보내기",
            EAppIcon.ImageExport,
            EProductButtonVariant.Primary);
        exportButton.Margin = new Padding(DesignTokens.SPACE_8, DesignTokens.SPACE_4, 0, DesignTokens.SPACE_4);
        exportButton.Click += onExportButtonClick;

        ProductButton cancelButton = new ProductButton(
            "취소",
            EAppIcon.None,
            EProductButtonVariant.Secondary);
        cancelButton.DialogResult = DialogResult.Cancel;
        cancelButton.Margin = new Padding(DesignTokens.SPACE_8, DesignTokens.SPACE_4, 0, DesignTokens.SPACE_4);

        footer.Controls.Add(exportButton);
        footer.Controls.Add(cancelButton);
        AcceptButton = exportButton;
        CancelButton = cancelButton;
        return footer;
    }

    private void onBrowseButtonClick(object? senderOrNull, EventArgs eventArgs)
    {
        using (FolderBrowserDialog folderBrowserDialog = new FolderBrowserDialog())
        {
            folderBrowserDialog.Description = "시간표 PNG를 저장할 폴더를 선택하세요.";
            folderBrowserDialog.SelectedPath = mSelectedDirectory.Value;
            folderBrowserDialog.ShowNewFolderButton = true;
            folderBrowserDialog.UseDescriptionForTitle = true;

            if (folderBrowserDialog.ShowDialog(this) != DialogResult.OK)
            {
                return;
            }

            mSelectedDirectory = new ScheduleExportDirectoryPath(folderBrowserDialog.SelectedPath);
            mDestinationTextBox.Text = mSelectedDirectory.Value;
        }
    }

    private void onExportButtonClick(object? senderOrNull, EventArgs eventArgs)
    {
        EScheduleExportScope scope = mAllSchedulesRadioButton.Checked
            ? EScheduleExportScope.AllSchedules
            : EScheduleExportScope.CurrentSchedule;
        mChoiceOrNull = new ScheduleExportChoice(scope, mSelectedDirectory);
        DialogResult = DialogResult.OK;
        Close();
    }
}
