using System;

namespace TimetableGenerator.Desktop.Presentation.Windowing;

internal readonly record struct DisplayScale
{
    public double Value { get; }

    public DisplayScale(double value)
    {
        if (double.IsNaN(value) || double.IsInfinity(value) || value <= 0.0)
        {
            throw new ArgumentOutOfRangeException(nameof(value), value, "Display scale must be finite and positive.");
        }

        Value = value;
    }
}
