using System;
using System.Globalization;

namespace TimetableGenerator.Infrastructure.Exporting;

public readonly record struct ScheduleExportNumber
{
    public int Value { get; }

    public bool IsValid
    {
        get
        {
            return Value > 0;
        }
    }

    public ScheduleExportNumber(int value)
    {
        if (value <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(value),
                "Schedule export numbers must be greater than zero.");
        }

        Value = value;
    }

    public override string ToString()
    {
        return Value.ToString(CultureInfo.InvariantCulture);
    }
}
