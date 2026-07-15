using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace TimetableGenerator.UI.Product;

internal sealed class ExampleFormatDialog : Form
{
    private const int PREFERRED_CLIENT_WIDTH = 720;
    private const int PREFERRED_CLIENT_HEIGHT = 510;
    private const int MINIMUM_WINDOW_WIDTH = 600;
    private const int MINIMUM_WINDOW_HEIGHT = 420;
    private const float CODE_FONT_SIZE = 10.0f;
    private const float TITLE_ROW_HEIGHT = 38.0f;
    private const float DESCRIPTION_ROW_HEIGHT = 64.0f;
    private const float EXAMPLE_ROW_HEIGHT = 180.0f;
    private const float TIP_ROW_HEIGHT = 76.0f;
    private const float FOOTER_ROW_HEIGHT = 48.0f;

    private const string CSV_EXAMPLE =
        "CourseId,Section,Name,TimeSlots,Classroom\r\n"
        + "1,01,알고리즘,월요일1교시/수요일1교시,공학관 301\r\n"
        + "1,02,알고리즘,화요일1교시/목요일1교시,공학관 302\r\n"
        + "2,01,데이터베이스,금요일2교시,미래관 204";

    private readonly Font mTitleFont;
    private readonly Font mBodyFont;
    private readonly Font mCodeFont;
    private readonly Label mCopyStatusLabel;

    internal ExampleFormatDialog()
    {
        AutoScaleDimensions = new SizeF(DesignTokens.BASE_DPI, DesignTokens.BASE_DPI);
        AutoScaleMode = AutoScaleMode.Dpi;
        BackColor = DesignTokens.WINDOW_BACKGROUND_COLOR;
        ClientSize = new Size(PREFERRED_CLIENT_WIDTH, PREFERRED_CLIENT_HEIGHT);
        FormBorderStyle = FormBorderStyle.Sizable;
        MaximizeBox = false;
        MinimumSize = new Size(MINIMUM_WINDOW_WIDTH, MINIMUM_WINDOW_HEIGHT);
        MinimizeBox = false;
        SizeGripStyle = SizeGripStyle.Show;
        ShowIcon = true;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterParent;
        Text = "CSV 작성 형식";

        mTitleFont = DesignTokens.createSectionTitleFont(Font);
        mBodyFont = DesignTokens.createBodyFont(Font);
        mCodeFont = new Font(
            "Cascadia Mono",
            CODE_FONT_SIZE,
            FontStyle.Regular,
            GraphicsUnit.Point);
        mCopyStatusLabel = createCopyStatusLabel();

        TableLayoutPanel layout = createLayout();
        layout.Controls.Add(createTitleLabel(), 0, 0);
        layout.Controls.Add(createDescriptionLabel(), 0, 1);
        layout.Controls.Add(createExampleTextBox(), 0, 2);
        layout.Controls.Add(createTipLabel(), 0, 3);
        layout.Controls.Add(createFooter(), 0, 4);
        Panel scrollHost = createScrollHost();
        scrollHost.Controls.Add(layout);
        Controls.Add(scrollHost);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            mTitleFont.Dispose();
            mBodyFont.Dispose();
            mCodeFont.Dispose();
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
        layout.Padding = new Padding(
            DesignTokens.SPACE_32,
            DesignTokens.SPACE_24,
            DesignTokens.SPACE_32,
            DesignTokens.SPACE_24);
        layout.BackColor = DesignTokens.WINDOW_BACKGROUND_COLOR;
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100.0f));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, TITLE_ROW_HEIGHT));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, DESCRIPTION_ROW_HEIGHT));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, EXAMPLE_ROW_HEIGHT));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, TIP_ROW_HEIGHT));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, FOOTER_ROW_HEIGHT));
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
        titleLabel.Text = "CSV 파일은 이렇게 작성해 주세요";
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
        descriptionLabel.Text = "같은 CourseId의 행은 서로 선택 가능한 분반입니다. 시간은 ‘요일+교시’로 쓰고, 여러 시간은 /로 구분합니다.";
        descriptionLabel.Font = mBodyFont;
        descriptionLabel.ForeColor = DesignTokens.TEXT_SECONDARY_COLOR;
        descriptionLabel.TextAlign = ContentAlignment.MiddleLeft;
        descriptionLabel.AccessibleRole = AccessibleRole.StaticText;
        descriptionLabel.TabStop = false;
        return descriptionLabel;
    }

    private TextBox createExampleTextBox()
    {
        TextBox exampleTextBox = new TextBox();
        exampleTextBox.Dock = DockStyle.Fill;
        exampleTextBox.Margin = new Padding(0, DesignTokens.SPACE_8, 0, DesignTokens.SPACE_8);
        exampleTextBox.Multiline = true;
        exampleTextBox.ReadOnly = true;
        exampleTextBox.ScrollBars = ScrollBars.Both;
        exampleTextBox.WordWrap = false;
        exampleTextBox.Text = CSV_EXAMPLE;
        exampleTextBox.Font = mCodeFont;
        exampleTextBox.BackColor = DesignTokens.SUBTLE_SURFACE_COLOR;
        exampleTextBox.ForeColor = DesignTokens.TEXT_PRIMARY_COLOR;
        exampleTextBox.AccessibleName = "CSV 예제";
        exampleTextBox.AccessibleDescription = "복사 가능한 CSV 형식 예제입니다.";
        exampleTextBox.TabStop = true;
        return exampleTextBox;
    }

    private Label createTipLabel()
    {
        Label tipLabel = new Label();
        tipLabel.Dock = DockStyle.Fill;
        tipLabel.Text = "• 헤더는 정확히 유지하세요. Classroom 열은 생략할 수 있습니다.\r\n• UTF-8 CSV로 저장하고, 시간은 ‘월요일1교시/수요일1교시’처럼 입력하세요.";
        tipLabel.Font = mBodyFont;
        tipLabel.ForeColor = DesignTokens.TEXT_SECONDARY_COLOR;
        tipLabel.TextAlign = ContentAlignment.MiddleLeft;
        tipLabel.AccessibleRole = AccessibleRole.StaticText;
        tipLabel.TabStop = false;
        return tipLabel;
    }

    private Control createFooter()
    {
        TableLayoutPanel footer = new TableLayoutPanel();
        footer.ColumnCount = 3;
        footer.RowCount = 1;
        footer.Dock = DockStyle.Fill;
        footer.Margin = Padding.Empty;
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100.0f));
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        footer.RowStyles.Add(new RowStyle(SizeType.Percent, 100.0f));

        ProductButton copyButton = new ProductButton(
            "예제 복사",
            EAppIcon.File,
            EProductButtonVariant.Secondary);
        copyButton.AccessibleDescription = "CSV 예제를 클립보드에 복사합니다.";
        copyButton.Margin = new Padding(DesignTokens.SPACE_8, DesignTokens.SPACE_4, 0, DesignTokens.SPACE_4);
        copyButton.Click += onCopyButtonClick;

        ProductButton closeButton = new ProductButton(
            "닫기",
            EAppIcon.None,
            EProductButtonVariant.Primary);
        closeButton.DialogResult = DialogResult.Cancel;
        closeButton.Margin = new Padding(DesignTokens.SPACE_8, DesignTokens.SPACE_4, 0, DesignTokens.SPACE_4);

        footer.Controls.Add(mCopyStatusLabel, 0, 0);
        footer.Controls.Add(copyButton, 1, 0);
        footer.Controls.Add(closeButton, 2, 0);
        CancelButton = closeButton;
        return footer;
    }

    private Label createCopyStatusLabel()
    {
        Label copyStatusLabel = new Label();
        copyStatusLabel.Dock = DockStyle.Fill;
        copyStatusLabel.Font = mBodyFont;
        copyStatusLabel.ForeColor = DesignTokens.SUCCESS_COLOR;
        copyStatusLabel.TextAlign = ContentAlignment.MiddleLeft;
        copyStatusLabel.AccessibleRole = AccessibleRole.StaticText;
        copyStatusLabel.TabStop = false;
        return copyStatusLabel;
    }

    private void onCopyButtonClick(object? senderOrNull, EventArgs eventArgs)
    {
        try
        {
            Clipboard.SetText(CSV_EXAMPLE);
            mCopyStatusLabel.Text = "클립보드에 복사했습니다.";
            mCopyStatusLabel.ForeColor = DesignTokens.SUCCESS_COLOR;
        }
        catch (ExternalException)
        {
            mCopyStatusLabel.Text = "클립보드를 사용할 수 없습니다.";
            mCopyStatusLabel.ForeColor = DesignTokens.ERROR_COLOR;
        }

        mCopyStatusLabel.AccessibleName = mCopyStatusLabel.Text;
    }
}
