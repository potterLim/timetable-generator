using System;
using System.Drawing;
using System.Windows.Forms;

namespace TimetableGenerator.UI.Product;

internal sealed class AppLogoControl : Control
{
    private const string APP_TITLE = "시간표 생성기";

    internal AppLogoControl()
    {
        BackColor = DesignTokens.SURFACE_COLOR;
        AccessibleName = APP_TITLE;
        AccessibleRole = AccessibleRole.StaticText;
        TabStop = false;

        SetStyle(ControlStyles.AllPaintingInWmPaint, true);
        SetStyle(ControlStyles.OptimizedDoubleBuffer, true);
        SetStyle(ControlStyles.ResizeRedraw, true);
        SetStyle(ControlStyles.UserPaint, true);

        Size = GetPreferredSize(Size.Empty);
    }

    public override Size GetPreferredSize(Size proposedSize)
    {
        using (Font titleFont = DesignTokens.createAppTitleFont(Font))
        {
            Size titleSize = TextRenderer.MeasureText(
                APP_TITLE,
                titleFont,
                Size.Empty,
                TextFormatFlags.NoPadding | TextFormatFlags.SingleLine);

            int iconSize = DesignTokens.scaleLogicalPixel(this, DesignTokens.APP_LOGO_ICON_SIZE);
            int contentGap = DesignTokens.scaleLogicalPixel(this, DesignTokens.SPACE_16);
            int preferredWidth = iconSize + contentGap + titleSize.Width;
            int preferredHeight = Math.Max(iconSize, titleSize.Height);
            return new Size(preferredWidth, preferredHeight);
        }
    }

    protected override void OnPaint(PaintEventArgs paintEventArgs)
    {
        base.OnPaint(paintEventArgs);

        int iconSize = DesignTokens.scaleLogicalPixel(this, DesignTokens.APP_LOGO_ICON_SIZE);
        int iconY = (ClientSize.Height - iconSize) / 2;
        Rectangle iconBounds = new Rectangle(0, iconY, iconSize, iconSize);
        AppIconPainter.drawIcon(
            paintEventArgs.Graphics,
            iconBounds,
            EAppIcon.Calendar,
            DesignTokens.ACCENT_COLOR);

        int contentGap = DesignTokens.scaleLogicalPixel(this, DesignTokens.SPACE_16);
        int titleX = iconBounds.Right + contentGap;
        Rectangle titleBounds = new Rectangle(
            titleX,
            0,
            Math.Max(0, ClientSize.Width - titleX),
            ClientSize.Height);

        using (Font titleFont = DesignTokens.createAppTitleFont(Font))
        {
            TextRenderer.DrawText(
                paintEventArgs.Graphics,
                APP_TITLE,
                titleFont,
                titleBounds,
                DesignTokens.TEXT_PRIMARY_COLOR,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);
        }
    }
}
