using System;

namespace TimetableGenerator.Desktop.Presentation.Windowing;

internal readonly record struct WindowLogicalSize
{
    public double Width { get; }

    public double Height { get; }

    public WindowLogicalSize(double width, double height)
    {
        validateLength(width, nameof(width));
        validateLength(height, nameof(height));

        Width = width;
        Height = height;
    }

    private static void validateLength(double length, string parameterName)
    {
        if (double.IsNaN(length) || double.IsInfinity(length) || length <= 0.0)
        {
            throw new ArgumentOutOfRangeException(
                parameterName,
                length,
                "Window size must be finite and positive.");
        }
    }
}
