using System;
using System.Drawing;
using System.Windows.Forms;

namespace TimetableGenerator.UI.Product;

internal static class ProductDialogSizing
{
    private const int WORKING_AREA_MARGIN = 24;

    internal static Size findInitialClientSize(
        Form dialog,
        Size preferredLogicalSize)
    {
        if (dialog == null)
        {
            throw new ArgumentNullException(nameof(dialog));
        }

        if (preferredLogicalSize.Width <= 0 || preferredLogicalSize.Height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(preferredLogicalSize));
        }

        Control screenReference = dialog.Owner == null ? dialog : dialog.Owner;
        Rectangle workingArea = Screen.FromControl(screenReference).WorkingArea;
        int workingAreaMargin = DesignTokens.scaleLogicalPixel(
            dialog,
            WORKING_AREA_MARGIN);
        int maximumWindowWidth = Math.Max(
            1,
            workingArea.Width - (workingAreaMargin * 2));
        int maximumWindowHeight = Math.Max(
            1,
            workingArea.Height - (workingAreaMargin * 2));
        int preferredClientWidth = DesignTokens.scaleLogicalPixel(
            dialog,
            preferredLogicalSize.Width);
        int preferredClientHeight = DesignTokens.scaleLogicalPixel(
            dialog,
            preferredLogicalSize.Height);
        int nonClientWidth = Math.Max(
            0,
            dialog.Size.Width - dialog.ClientSize.Width);
        int nonClientHeight = Math.Max(
            0,
            dialog.Size.Height - dialog.ClientSize.Height);
        int maximumClientWidth = Math.Max(
            1,
            maximumWindowWidth - nonClientWidth);
        int maximumClientHeight = Math.Max(
            1,
            maximumWindowHeight - nonClientHeight);
        return new Size(
            Math.Min(preferredClientWidth, maximumClientWidth),
            Math.Min(preferredClientHeight, maximumClientHeight));
    }
}
