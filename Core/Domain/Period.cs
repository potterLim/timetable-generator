using System;
using System.Globalization;

namespace TimetableGenerator.Core.Domain;

public readonly record struct Period
{
    public int Value { get; }

    public bool IsValid
    {
        get
        {
            return Value > 0;
        }
    }

    public Period(int value)
    {
        if (value <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(value), "Periods must be greater than zero.");
        }

        Value = value;
    }

    public override string ToString()
    {
        return Value.ToString(CultureInfo.InvariantCulture);
    }
}
