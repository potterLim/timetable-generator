using System;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;

namespace TimetableGenerator.UI.Product;

internal sealed class AppStatusControl : Control
{
    private EAppStatusKind mStatusKind;
    private string mMessage;

    internal AppStatusControl()
    {
        mMessage = string.Empty;

        BackColor = DesignTokens.SURFACE_COLOR;
        AccessibleName = "애플리케이션 상태";
        AccessibleRole = AccessibleRole.StatusBar;
        TabStop = false;

        SetStyle(ControlStyles.AllPaintingInWmPaint, true);
        SetStyle(ControlStyles.OptimizedDoubleBuffer, true);
        SetStyle(ControlStyles.ResizeRedraw, true);
        SetStyle(ControlStyles.UserPaint, true);

        showStatus(EAppStatusKind.Neutral, "CSV 파일을 불러오면 시작할 수 있습니다");
        applyMetrics();
    }

    internal void showStatus(EAppStatusKind statusKind, string message)
    {
        if (message == null)
        {
            throw new ArgumentNullException(nameof(message));
        }

        mStatusKind = statusKind;
        mMessage = message.Trim();
        AccessibleName = "상태: " + mMessage;
        AccessibilityNotifyClients(AccessibleEvents.NameChange, -1);
        Invalidate();
    }

    internal void clearStatus()
    {
        showStatus(EAppStatusKind.Neutral, string.Empty);
    }

    protected override void OnPaint(PaintEventArgs paintEventArgs)
    {
        base.OnPaint(paintEventArgs);

        using (Pen borderPen = new Pen(DesignTokens.SUBTLE_BORDER_COLOR, DesignTokens.scaleLogicalPixel(this, DesignTokens.BORDER_WIDTH)))
        {
            paintEventArgs.Graphics.DrawLine(borderPen, 0, 0, ClientSize.Width, 0);
        }

        int horizontalPadding = DesignTokens.scaleLogicalPixel(this, DesignTokens.SPACE_20);
        int contentX = horizontalPadding;
        EAppIcon statusIcon = findStatusIcon();
        Color statusColor = findStatusColor();

        if (statusIcon != EAppIcon.None)
        {
            int iconSize = DesignTokens.scaleLogicalPixel(this, DesignTokens.STATUS_ICON_SIZE);
            int iconY = (ClientSize.Height - iconSize) / 2;
            Rectangle iconBounds = new Rectangle(contentX, iconY, iconSize, iconSize);
            AppIconPainter.drawIcon(paintEventArgs.Graphics, iconBounds, statusIcon, statusColor);
            contentX = iconBounds.Right + DesignTokens.scaleLogicalPixel(this, DesignTokens.SPACE_8);
        }

        Rectangle textBounds = new Rectangle(
            contentX,
            0,
            Math.Max(0, ClientSize.Width - contentX - horizontalPadding),
            ClientSize.Height);

        using (Font statusFont = DesignTokens.createStatusFont(Font))
        {
            TextRenderer.DrawText(
                paintEventArgs.Graphics,
                mMessage,
                statusFont,
                textBounds,
                findMessageColor(),
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);
        }
    }

    protected override void OnDpiChangedAfterParent(EventArgs eventArgs)
    {
        base.OnDpiChangedAfterParent(eventArgs);
        applyMetrics();
    }

    private void applyMetrics()
    {
        Height = DesignTokens.scaleLogicalPixel(this, DesignTokens.APP_STATUS_HEIGHT);
        MinimumSize = new Size(0, Height);
    }

    private EAppIcon findStatusIcon()
    {
        switch (mStatusKind)
        {
            case EAppStatusKind.Neutral:
                return EAppIcon.None;
            case EAppStatusKind.Busy:
                return EAppIcon.Busy;
            case EAppStatusKind.Success:
                return EAppIcon.Success;
            case EAppStatusKind.Error:
                return EAppIcon.Warning;
            default:
                Debug.Fail("Unexpected app status kind: " + mStatusKind);
                return EAppIcon.None;
        }
    }

    private Color findStatusColor()
    {
        switch (mStatusKind)
        {
            case EAppStatusKind.Neutral:
                return DesignTokens.TEXT_SECONDARY_COLOR;
            case EAppStatusKind.Busy:
                return DesignTokens.ACCENT_COLOR;
            case EAppStatusKind.Success:
                return DesignTokens.SUCCESS_COLOR;
            case EAppStatusKind.Error:
                return DesignTokens.ERROR_COLOR;
            default:
                Debug.Fail("Unexpected app status kind: " + mStatusKind);
                return DesignTokens.TEXT_SECONDARY_COLOR;
        }
    }

    private Color findMessageColor()
    {
        if (mStatusKind == EAppStatusKind.Error)
        {
            return DesignTokens.ERROR_COLOR;
        }

        return DesignTokens.TEXT_SECONDARY_COLOR;
    }
}
