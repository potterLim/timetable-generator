using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace TimetableGenerator.UI.Product;

internal sealed class ProductButton : Button
{
    private bool mIsPointerOver;
    private bool mIsPressed;

    private EProductButtonVariant mVariant;

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    internal EProductButtonVariant Variant
    {
        get
        {
            return mVariant;
        }
        set
        {
            mVariant = value;
            Invalidate();
        }
    }

    private EAppIcon mAppIcon;

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    internal EAppIcon AppIcon
    {
        get
        {
            return mAppIcon;
        }
        set
        {
            mAppIcon = value;
            Invalidate();
            PerformLayout();
        }
    }

    internal ProductButton()
        : this(string.Empty, EAppIcon.None, EProductButtonVariant.Secondary)
    {
    }

    internal ProductButton(
        string text,
        EAppIcon appIcon,
        EProductButtonVariant variant)
    {
        if (text == null)
        {
            throw new ArgumentNullException(nameof(text));
        }

        mAppIcon = appIcon;
        mVariant = variant;

        Text = text;
        AccessibleName = text;
        AccessibleRole = AccessibleRole.PushButton;
        TabStop = true;

        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        Cursor = Cursors.Hand;
        FlatStyle = FlatStyle.Flat;
        FlatAppearance.BorderSize = 0;
        UseVisualStyleBackColor = false;

        SetStyle(ControlStyles.AllPaintingInWmPaint, true);
        SetStyle(ControlStyles.OptimizedDoubleBuffer, true);
        SetStyle(ControlStyles.ResizeRedraw, true);
        SetStyle(ControlStyles.SupportsTransparentBackColor, true);
        SetStyle(ControlStyles.UserPaint, true);

        BackColor = Color.Transparent;

        applyMinimumSize();
    }

    public override Size GetPreferredSize(Size proposedSize)
    {
        using (Font buttonFont = DesignTokens.createButtonFont(Font))
        {
            Size textSize = TextRenderer.MeasureText(
                Text,
                buttonFont,
                Size.Empty,
                TextFormatFlags.NoPadding | TextFormatFlags.SingleLine);

            int horizontalPadding = DesignTokens.scaleLogicalPixel(this, DesignTokens.BUTTON_HORIZONTAL_PADDING);
            int iconSize = DesignTokens.scaleLogicalPixel(this, DesignTokens.BUTTON_ICON_SIZE);
            int contentGap = DesignTokens.scaleLogicalPixel(this, DesignTokens.BUTTON_CONTENT_GAP);
            int preferredWidth = textSize.Width + (horizontalPadding * 2);

            if (mAppIcon != EAppIcon.None)
            {
                preferredWidth += iconSize;
                if (Text.Length > 0)
                {
                    preferredWidth += contentGap;
                }
            }

            int minimumHeight = DesignTokens.scaleLogicalPixel(this, DesignTokens.BUTTON_MINIMUM_HEIGHT);
            int preferredHeight = Math.Max(minimumHeight, textSize.Height + (DesignTokens.scaleLogicalPixel(this, DesignTokens.SPACE_8) * 2));
            return new Size(preferredWidth, preferredHeight);
        }
    }

    protected override void OnPaint(PaintEventArgs paintEventArgs)
    {
        Graphics graphics = paintEventArgs.Graphics;
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        graphics.Clear(findCanvasColor());

        Rectangle backgroundBounds = ProductDrawing.insetRectangle(ClientRectangle, 1);
        int cornerRadius = DesignTokens.scaleLogicalPixel(this, DesignTokens.CORNER_RADIUS_SMALL);

        Color backgroundColor = findBackgroundColor();
        Color borderColor = findBorderColor();
        Color foregroundColor = findForegroundColor();

        using (GraphicsPath backgroundPath =
            ProductDrawing.createRoundedRectanglePath(backgroundBounds, cornerRadius))
        {
            using (SolidBrush backgroundBrush = new SolidBrush(backgroundColor))
            {
                graphics.FillPath(backgroundBrush, backgroundPath);

                if (borderColor.A > 0)
                {
                    using (Pen borderPen = new Pen(
                        borderColor,
                        DesignTokens.scaleLogicalPixel(
                            this,
                            DesignTokens.BORDER_WIDTH)))
                    {
                        graphics.DrawPath(borderPen, backgroundPath);
                    }
                }
            }
        }

        drawContent(graphics, foregroundColor);

        if (Focused && ShowFocusCues)
        {
            drawFocusRing(graphics, cornerRadius);
        }
    }

    protected override void OnMouseEnter(EventArgs eventArgs)
    {
        base.OnMouseEnter(eventArgs);
        mIsPointerOver = true;
        Invalidate();
    }

    protected override void OnMouseLeave(EventArgs eventArgs)
    {
        base.OnMouseLeave(eventArgs);
        mIsPointerOver = false;
        mIsPressed = false;
        Invalidate();
    }

    protected override void OnMouseDown(MouseEventArgs mouseEventArgs)
    {
        base.OnMouseDown(mouseEventArgs);
        if (mouseEventArgs.Button == MouseButtons.Left)
        {
            mIsPressed = true;
            Invalidate();
        }
    }

    protected override void OnMouseUp(MouseEventArgs mouseEventArgs)
    {
        base.OnMouseUp(mouseEventArgs);
        mIsPressed = false;
        mIsPointerOver = ClientRectangle.Contains(mouseEventArgs.Location);
        Invalidate();
    }

    protected override void OnGotFocus(EventArgs eventArgs)
    {
        base.OnGotFocus(eventArgs);
        Invalidate();
    }

    protected override void OnLostFocus(EventArgs eventArgs)
    {
        base.OnLostFocus(eventArgs);
        mIsPressed = false;
        Invalidate();
    }

    protected override void OnEnabledChanged(EventArgs eventArgs)
    {
        base.OnEnabledChanged(eventArgs);
        Invalidate();
    }

    protected override void OnDpiChangedAfterParent(EventArgs eventArgs)
    {
        base.OnDpiChangedAfterParent(eventArgs);
        applyMinimumSize();
        PerformLayout();
        Invalidate();
    }

    private void applyMinimumSize()
    {
        int minimumHeight = DesignTokens.scaleLogicalPixel(this, DesignTokens.BUTTON_MINIMUM_HEIGHT);
        MinimumSize = new Size(MinimumSize.Width, minimumHeight);
    }

    private void drawContent(Graphics graphics, Color foregroundColor)
    {
        using (Font buttonFont = DesignTokens.createButtonFont(Font))
        {
            Size textSize = TextRenderer.MeasureText(
                graphics,
                Text,
                buttonFont,
                Size.Empty,
                TextFormatFlags.NoPadding | TextFormatFlags.SingleLine);

            int iconSize = DesignTokens.scaleLogicalPixel(this, DesignTokens.BUTTON_ICON_SIZE);
            int contentGap = DesignTokens.scaleLogicalPixel(this, DesignTokens.BUTTON_CONTENT_GAP);
            int contentWidth = textSize.Width;

            if (mAppIcon != EAppIcon.None)
            {
                contentWidth += iconSize;
                if (Text.Length > 0)
                {
                    contentWidth += contentGap;
                }
            }

            int contentX = (ClientSize.Width - contentWidth) / 2;
            int contentY = (ClientSize.Height - Math.Max(textSize.Height, iconSize)) / 2;

            if (mAppIcon != EAppIcon.None)
            {
                Rectangle iconBounds = new Rectangle(contentX, contentY, iconSize, iconSize);
                AppIconPainter.drawIcon(graphics, iconBounds, mAppIcon, foregroundColor);
                contentX += iconSize;
                if (Text.Length > 0)
                {
                    contentX += contentGap;
                }
            }

            Rectangle textBounds = new Rectangle(
                contentX,
                0,
                Math.Max(0, ClientSize.Width - contentX),
                ClientSize.Height);

            TextRenderer.DrawText(
                graphics,
                Text,
                buttonFont,
                textBounds,
                foregroundColor,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);
        }
    }

    private void drawFocusRing(Graphics graphics, int cornerRadius)
    {
        int focusInset = DesignTokens.scaleLogicalPixel(this, DesignTokens.SPACE_4);
        Rectangle focusBounds = ProductDrawing.insetRectangle(ClientRectangle, focusInset);
        int focusRadius = Math.Max(1, cornerRadius - 1);

        using (GraphicsPath focusPath =
            ProductDrawing.createRoundedRectanglePath(focusBounds, focusRadius))
        {
            using (Pen focusPen = new Pen(
                DesignTokens.ACCENT_COLOR,
                DesignTokens.scaleLogicalPixel(
                    this,
                    DesignTokens.FOCUS_RING_WIDTH)))
            {
                focusPen.DashStyle = DashStyle.Dot;
                graphics.DrawPath(focusPen, focusPath);
            }
        }
    }

    private Color findBackgroundColor()
    {
        if (Enabled == false)
        {
            if (mVariant == EProductButtonVariant.Quiet)
            {
                return Color.Transparent;
            }

            return DesignTokens.DISABLED_BACKGROUND_COLOR;
        }

        switch (mVariant)
        {
            case EProductButtonVariant.Primary:
                if (mIsPressed)
                {
                    return DesignTokens.ACCENT_PRESSED_COLOR;
                }

                if (mIsPointerOver)
                {
                    return DesignTokens.ACCENT_HOVER_COLOR;
                }

                return DesignTokens.ACCENT_COLOR;
            case EProductButtonVariant.Secondary:
                if (mIsPressed)
                {
                    return DesignTokens.QUIET_PRESSED_COLOR;
                }

                if (mIsPointerOver)
                {
                    return DesignTokens.QUIET_HOVER_COLOR;
                }

                return DesignTokens.SURFACE_COLOR;
            case EProductButtonVariant.Quiet:
                if (mIsPressed)
                {
                    return DesignTokens.QUIET_PRESSED_COLOR;
                }

                if (mIsPointerOver)
                {
                    return DesignTokens.QUIET_HOVER_COLOR;
                }

                return Color.Transparent;
            default:
                Debug.Fail("Unexpected product button variant: " + mVariant);
                return DesignTokens.SURFACE_COLOR;
        }
    }

    private Color findBorderColor()
    {
        if (Enabled == false)
        {
            if (mVariant == EProductButtonVariant.Quiet)
            {
                return Color.Transparent;
            }

            return DesignTokens.DISABLED_BORDER_COLOR;
        }

        if (mVariant == EProductButtonVariant.Secondary)
        {
            return DesignTokens.BORDER_COLOR;
        }

        return Color.Transparent;
    }

    private Color findForegroundColor()
    {
        if (Enabled == false)
        {
            return DesignTokens.DISABLED_TEXT_COLOR;
        }

        if (mVariant == EProductButtonVariant.Primary)
        {
            return Color.White;
        }

        return DesignTokens.TEXT_PRIMARY_COLOR;
    }

    private Color findCanvasColor()
    {
        Control? ancestorOrNull = Parent;
        while (ancestorOrNull != null)
        {
            if (ancestorOrNull.BackColor.A == byte.MaxValue)
            {
                return ancestorOrNull.BackColor;
            }

            ancestorOrNull = ancestorOrNull.Parent;
        }

        return DesignTokens.SURFACE_COLOR;
    }
}
