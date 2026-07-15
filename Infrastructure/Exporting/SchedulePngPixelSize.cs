using System;

namespace TimetableGenerator.Infrastructure.Exporting;

public readonly record struct SchedulePngPixelSize
{
    public int Width { get; }

    public int Height { get; }

    public bool IsValid
    {
        get
        {
            return Width > 0 && Height > 0;
        }
    }

    internal SchedulePngPixelSize(int width, int height)
    {
        if (width <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(width));
        }

        if (height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(height));
        }

        Width = width;
        Height = height;
    }
}
