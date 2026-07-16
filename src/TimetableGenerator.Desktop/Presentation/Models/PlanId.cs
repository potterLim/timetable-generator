using System;

namespace TimetableGenerator.Desktop.Presentation.Models;

internal readonly record struct PlanId
{
    public int Value { get; }

    public PlanId(int value)
    {
        if (value <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(value), value, "Plan IDs must be positive.");
        }

        Value = value;
    }
}
