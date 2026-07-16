using System;

namespace TimetableGenerator.Desktop.Presentation.Models;

internal readonly record struct CreditCount
{
    public int Value { get; }

    public CreditCount(int value)
    {
        if (value < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(value), value, "Credit counts cannot be negative.");
        }

        Value = value;
    }
}
