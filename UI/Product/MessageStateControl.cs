using System;
using System.Drawing;
using System.Windows.Forms;

namespace TimetableGenerator.UI.Product;

internal sealed class MessageStateControl : UserControl
{
    private const int CONTENT_MAXIMUM_WIDTH = 680;
    private const int ICON_SIZE = 64;
    private const int TITLE_HEIGHT = 48;
    private const int DESCRIPTION_HEIGHT = 56;
    private const int DETAIL_HEIGHT = 112;
    private const int ACTION_HEIGHT = 48;

    private readonly Panel mContentPanel;
    private readonly AppIconControl mIconControl;
    private readonly Label mTitleLabel;
    private readonly Label mDescriptionLabel;
    private readonly Label mDetailLabel;
    private readonly ProductButton mPrimaryActionButton;
    private readonly Font mTitleFont;
    private readonly Font mDescriptionFont;
    private readonly Font mDetailFont;

    internal event EventHandler? PrimaryActionRequested;

    internal MessageStateControl()
    {
        AutoScaleDimensions = new SizeF(DesignTokens.BASE_DPI, DesignTokens.BASE_DPI);
        AutoScaleMode = AutoScaleMode.Dpi;
        AutoScroll = true;
        BackColor = DesignTokens.WINDOW_BACKGROUND_COLOR;
        AccessibleRole = AccessibleRole.Grouping;
        TabStop = false;

        mTitleFont = DesignTokens.createWelcomeTitleFont(Font);
        mDescriptionFont = DesignTokens.createWelcomeDescriptionFont(Font);
        mDetailFont = DesignTokens.createBodyFont(Font);

        mContentPanel = new Panel();
        mContentPanel.BackColor = Color.Transparent;
        mContentPanel.TabStop = false;

        mIconControl = new AppIconControl(
            EAppIcon.Calendar,
            DesignTokens.ACCENT_COLOR,
            "안내");
        mTitleLabel = createLabel(mTitleFont, DesignTokens.TEXT_PRIMARY_COLOR);
        mDescriptionLabel = createLabel(mDescriptionFont, DesignTokens.TEXT_SECONDARY_COLOR);
        mDetailLabel = createLabel(mDetailFont, DesignTokens.TEXT_SECONDARY_COLOR);
        mPrimaryActionButton = new ProductButton(
            "다른 CSV 선택",
            EAppIcon.FolderOpen,
            EProductButtonVariant.Primary);
        mPrimaryActionButton.Anchor = AnchorStyles.None;
        mPrimaryActionButton.Click += onPrimaryActionButtonClick;

        mContentPanel.Controls.Add(mIconControl);
        mContentPanel.Controls.Add(mTitleLabel);
        mContentPanel.Controls.Add(mDescriptionLabel);
        mContentPanel.Controls.Add(mDetailLabel);
        mContentPanel.Controls.Add(mPrimaryActionButton);
        Controls.Add(mContentPanel);

        showContent(new MessageStateContent(
            EMessageStateKind.Empty,
            new MessageStateTitle("표시할 시간표가 없습니다"),
            new MessageStateDescription("다른 CSV 파일을 선택해 다시 시도해 주세요."),
            new MessageStateDetail(string.Empty),
            new MessageStateActionText("CSV 파일 선택")));
        layoutContent();
    }

    internal void showContent(MessageStateContent content)
    {
        if (content == null)
        {
            throw new ArgumentNullException(nameof(content));
        }

        mTitleLabel.Text = content.Title.Value;
        mDescriptionLabel.Text = content.Description.Value;
        mDetailLabel.Text = content.Detail.Value;
        mDetailLabel.Visible = content.Detail.Value.Length > 0;
        mPrimaryActionButton.Text = content.PrimaryActionText.Value;
        mPrimaryActionButton.AccessibleName = content.PrimaryActionText.Value;

        if (content.Kind == EMessageStateKind.Error)
        {
            mIconControl.showIcon(EAppIcon.Warning, DesignTokens.ERROR_COLOR);
            mIconControl.AccessibleName = "오류";
        }
        else
        {
            mIconControl.showIcon(EAppIcon.Calendar, DesignTokens.ACCENT_COLOR);
            mIconControl.AccessibleName = "빈 시간표";
        }

        AccessibleName = content.Title.Value;
        AccessibleDescription = content.Description.Value;
        layoutContent();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            mTitleFont.Dispose();
            mDescriptionFont.Dispose();
            mDetailFont.Dispose();
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

    private Label createLabel(Font font, Color foregroundColor)
    {
        Label label = new Label();
        label.Font = font;
        label.ForeColor = foregroundColor;
        label.BackColor = Color.Transparent;
        label.TextAlign = ContentAlignment.MiddleCenter;
        label.AutoEllipsis = true;
        label.AccessibleRole = AccessibleRole.StaticText;
        label.TabStop = false;
        return label;
    }

    private void onPrimaryActionButtonClick(object? senderOrNull, EventArgs eventArgs)
    {
        if (PrimaryActionRequested != null)
        {
            PrimaryActionRequested(this, EventArgs.Empty);
        }
    }

    private void layoutContent()
    {
        // WinForms can raise SizeChanged while DPI autoscaling is still constructing this control.
        if (mContentPanel == null)
        {
            return;
        }

        int outerPadding = DesignTokens.scaleLogicalPixel(this, DesignTokens.SPACE_32);
        int maximumContentWidth = DesignTokens.scaleLogicalPixel(this, CONTENT_MAXIMUM_WIDTH);
        int availableContentWidth = Math.Max(0, ClientSize.Width - (outerPadding * 2));
        int contentWidth = Math.Min(maximumContentWidth, availableContentWidth);

        int iconSize = DesignTokens.scaleLogicalPixel(this, ICON_SIZE);
        int titleHeight = DesignTokens.scaleLogicalPixel(this, TITLE_HEIGHT);
        int descriptionHeight = DesignTokens.scaleLogicalPixel(this, DESCRIPTION_HEIGHT);
        int detailHeight = mDetailLabel.Visible
            ? DesignTokens.scaleLogicalPixel(this, DETAIL_HEIGHT)
            : 0;
        int actionHeight = DesignTokens.scaleLogicalPixel(this, ACTION_HEIGHT);
        int gap = DesignTokens.scaleLogicalPixel(this, DesignTokens.SPACE_16);
        int detailGap = mDetailLabel.Visible ? gap : 0;
        int messageHeight = iconSize + gap + titleHeight + descriptionHeight;
        int detailSectionHeight = detailGap + detailHeight;
        int actionSectionHeight = gap + actionHeight;
        int contentHeight = messageHeight + detailSectionHeight + actionSectionHeight;
        int scrollableContentHeight = contentHeight + (outerPadding * 2);
        bool hasVerticalOverflow = scrollableContentHeight > ClientSize.Height;
        int verticalScrollbarWidth = hasVerticalOverflow
            ? SystemInformation.VerticalScrollBarWidth
            : 0;
        int viewportWidth = Math.Max(0, ClientSize.Width - verticalScrollbarWidth);
        availableContentWidth = Math.Max(0, viewportWidth - (outerPadding * 2));
        contentWidth = Math.Min(maximumContentWidth, availableContentWidth);
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
        mIconControl.Bounds = new Rectangle((contentWidth - iconSize) / 2, currentY, iconSize, iconSize);

        currentY += iconSize + gap;
        mTitleLabel.Bounds = new Rectangle(0, currentY, contentWidth, titleHeight);

        currentY += titleHeight;
        mDescriptionLabel.Bounds = new Rectangle(0, currentY, contentWidth, descriptionHeight);

        currentY += descriptionHeight + detailGap;
        mDetailLabel.Bounds = new Rectangle(0, currentY, contentWidth, detailHeight);

        currentY += detailHeight + gap;
        mPrimaryActionButton.MinimumSize = new Size(
            mPrimaryActionButton.MinimumSize.Width,
            actionHeight);
        Size preferredButtonSize = mPrimaryActionButton.GetPreferredSize(Size.Empty);
        int buttonWidth = Math.Min(contentWidth, preferredButtonSize.Width);
        mPrimaryActionButton.Bounds = new Rectangle(
            (contentWidth - buttonWidth) / 2,
            currentY,
            buttonWidth,
            actionHeight);
    }
}
