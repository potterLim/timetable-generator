using System;
using System.Drawing;
using System.Windows.Forms;

namespace TimetableGenerator.UI.Product;

internal sealed class AppIconControl : Control
{
    private EAppIcon mAppIcon;
    private Color mIconColor;

    internal AppIconControl(
        EAppIcon appIcon,
        Color iconColor,
        string accessibleName)
    {
        if (accessibleName == null)
        {
            throw new ArgumentNullException(nameof(accessibleName));
        }

        mAppIcon = appIcon;
        mIconColor = iconColor;

        AccessibleName = accessibleName;
        AccessibleRole = AccessibleRole.Graphic;
        TabStop = false;

        SetStyle(ControlStyles.AllPaintingInWmPaint, true);
        SetStyle(ControlStyles.OptimizedDoubleBuffer, true);
        SetStyle(ControlStyles.ResizeRedraw, true);
        SetStyle(ControlStyles.SupportsTransparentBackColor, true);
        SetStyle(ControlStyles.UserPaint, true);
    }

    internal void showIcon(EAppIcon appIcon, Color iconColor)
    {
        mAppIcon = appIcon;
        mIconColor = iconColor;
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs paintEventArgs)
    {
        base.OnPaint(paintEventArgs);
        AppIconPainter.drawIcon(paintEventArgs.Graphics, ClientRectangle, mAppIcon, mIconColor);
    }
}
