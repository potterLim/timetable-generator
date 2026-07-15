using System;

namespace TimetableGenerator.Infrastructure.Csv;

public readonly record struct DiagnosticCountLimit
{
    public int Value { get; }

    public bool IsValid
    {
        get
        {
            return Value > 0;
        }
    }

    public DiagnosticCountLimit(int value)
    {
        if (value <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(value), "Diagnostic count limits must be greater than zero.");
        }

        Value = value;
    }
}
