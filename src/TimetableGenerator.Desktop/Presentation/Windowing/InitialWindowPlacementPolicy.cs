using System;
using System.Diagnostics;

using Avalonia;

namespace TimetableGenerator.Desktop.Presentation.Windowing;

internal static class InitialWindowPlacementPolicy
{
    private const double SAFE_EDGE_MARGIN = 24.0;
    private const double MINIMUM_VISIBLE_LOGICAL_LENGTH = 1.0;

    private static readonly WindowLogicalSize PREFERRED_SIZE =
        new WindowLogicalSize(1_440.0, 900.0);

    private static readonly WindowLogicalSize DESIGN_MINIMUM_SIZE =
        new WindowLogicalSize(900.0, 640.0);

    public static InitialWindowPlacement CreatePlacement(
        WindowWorkingArea workingArea)
    {
        WindowLogicalSize workingSize = workingArea.FindLogicalSize();

        double maximumHorizontalMargin = Math.Max(
            0.0,
            (workingSize.Width - MINIMUM_VISIBLE_LOGICAL_LENGTH) / 2.0);
        double horizontalMargin = Math.Min(
            SAFE_EDGE_MARGIN,
            maximumHorizontalMargin);
        double maximumVerticalMargin = Math.Max(
            0.0,
            (workingSize.Height - MINIMUM_VISIBLE_LOGICAL_LENGTH) / 2.0);
        double verticalMargin = Math.Min(
            SAFE_EDGE_MARGIN,
            maximumVerticalMargin);

        double availableWidth = workingSize.Width - (horizontalMargin * 2.0);
        double availableHeight = workingSize.Height - (verticalMargin * 2.0);
        WindowLogicalSize availableSize = new WindowLogicalSize(
            availableWidth,
            availableHeight);

        WindowLogicalSize effectiveMinimumSize = new WindowLogicalSize(
            Math.Min(DESIGN_MINIMUM_SIZE.Width, availableSize.Width),
            Math.Min(DESIGN_MINIMUM_SIZE.Height, availableSize.Height));
        WindowLogicalSize initialSize = new WindowLogicalSize(
            Math.Min(PREFERRED_SIZE.Width, availableSize.Width),
            Math.Min(PREFERRED_SIZE.Height, availableSize.Height));

        Debug.Assert(initialSize.Width >= effectiveMinimumSize.Width);
        Debug.Assert(initialSize.Height >= effectiveMinimumSize.Height);

        PixelPoint position = findCenteredPosition(workingArea, initialSize);
        return new InitialWindowPlacement(
            initialSize,
            effectiveMinimumSize,
            position);
    }

    private static PixelPoint findCenteredPosition(
        WindowWorkingArea workingArea,
        WindowLogicalSize initialSize)
    {
        PixelRect bounds = workingArea.Bounds;
        double initialPixelWidthValue = Math.Ceiling(
            initialSize.Width * workingArea.Scale.Value);
        int initialPixelWidth = (int)Math.Min(
            bounds.Width,
            initialPixelWidthValue);
        double initialPixelHeightValue = Math.Ceiling(
            initialSize.Height * workingArea.Scale.Value);
        int initialPixelHeight = (int)Math.Min(
            bounds.Height,
            initialPixelHeightValue);

        int horizontalSpace = bounds.Width - initialPixelWidth;
        int verticalSpace = bounds.Height - initialPixelHeight;
        int left = bounds.X + (horizontalSpace / 2);
        int top = bounds.Y + (verticalSpace / 2);
        return new PixelPoint(left, top);
    }
}
