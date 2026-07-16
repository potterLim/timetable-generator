using System;

using Avalonia;

namespace TimetableGenerator.Desktop.Presentation.Windowing;

internal readonly record struct WindowWorkingArea
{
    public PixelRect Bounds { get; }

    public DisplayScale Scale { get; }

    public WindowWorkingArea(PixelRect bounds, DisplayScale scale)
    {
        if (bounds.Width <= 0 || bounds.Height <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(bounds),
                bounds,
                "Window working area must have positive dimensions.");
        }

        Bounds = bounds;
        Scale = scale;
    }

    public WindowLogicalSize FindLogicalSize()
    {
        double logicalWidth = Bounds.Width / Scale.Value;
        double logicalHeight = Bounds.Height / Scale.Value;
        return new WindowLogicalSize(logicalWidth, logicalHeight);
    }
}
