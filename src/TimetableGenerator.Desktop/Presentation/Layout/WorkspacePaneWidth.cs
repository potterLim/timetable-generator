using System;

namespace TimetableGenerator.Desktop.Presentation.Layout;

internal readonly record struct WorkspacePaneWidth
{
    public double Value { get; }

    public WorkspacePaneWidth(double value)
    {
        if (double.IsNaN(value) || double.IsInfinity(value) || value <= 0.0)
        {
            throw new ArgumentOutOfRangeException(nameof(value), value, "Workspace pane width must be finite and positive.");
        }

        Value = value;
    }
}
