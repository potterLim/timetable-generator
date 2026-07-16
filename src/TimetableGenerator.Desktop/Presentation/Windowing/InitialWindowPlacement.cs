using System;

using Avalonia;

namespace TimetableGenerator.Desktop.Presentation.Windowing;

internal readonly record struct InitialWindowPlacement
{
    public WindowLogicalSize InitialSize { get; }

    public WindowLogicalSize EffectiveMinimumSize { get; }

    public PixelPoint Position { get; }

    public InitialWindowPlacement(
        WindowLogicalSize initialSize,
        WindowLogicalSize effectiveMinimumSize,
        PixelPoint position)
    {
        if (initialSize.Width < effectiveMinimumSize.Width
            || initialSize.Height < effectiveMinimumSize.Height)
        {
            throw new ArgumentOutOfRangeException(
                nameof(effectiveMinimumSize),
                effectiveMinimumSize,
                "Effective minimum size cannot exceed the initial size.");
        }

        InitialSize = initialSize;
        EffectiveMinimumSize = effectiveMinimumSize;
        Position = position;
    }
}
