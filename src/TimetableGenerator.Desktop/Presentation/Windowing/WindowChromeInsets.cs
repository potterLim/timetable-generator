using System;

namespace TimetableGenerator.Desktop.Presentation.Windowing;

internal readonly record struct WindowChromeInsets
{
    public double Left { get; }

    public double Right { get; }

    public WindowChromeInsets(double left, double right)
    {
        validateInset(left, nameof(left));
        validateInset(right, nameof(right));

        Left = left;
        Right = right;
    }

    private static void validateInset(double inset, string parameterName)
    {
        if (double.IsNaN(inset)
            || double.IsInfinity(inset)
            || inset < 0.0)
        {
            throw new ArgumentOutOfRangeException(
                parameterName,
                inset,
                "Window chrome inset must be finite and non-negative.");
        }
    }
}
