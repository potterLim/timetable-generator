using System;
using System.Globalization;

namespace TimetableGenerator.CatalogJson;

public readonly record struct AdditionalInstructorCount
{
    public int Value { get; }

    public AdditionalInstructorCount(int value)
    {
        if (value < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(value),
                value,
                "Additional instructor counts cannot be negative.");
        }

        Value = value;
    }

    public override string ToString()
    {
        return Value.ToString(CultureInfo.InvariantCulture);
    }
}
