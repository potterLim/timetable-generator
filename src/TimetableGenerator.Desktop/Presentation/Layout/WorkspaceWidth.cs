using System;

namespace TimetableGenerator.Desktop.Presentation.Layout;

internal readonly record struct WorkspaceWidth
{
    public double Value { get; }

    public WorkspaceWidth(double value)
    {
        if (double.IsNaN(value) || double.IsInfinity(value) || value < 0.0)
        {
            throw new ArgumentOutOfRangeException(nameof(value), value, "Workspace width must be finite and non-negative.");
        }

        Value = value;
    }
}
