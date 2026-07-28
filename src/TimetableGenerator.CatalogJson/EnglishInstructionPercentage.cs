using System;
using System.Globalization;

namespace TimetableGenerator.CatalogJson;

public readonly record struct EnglishInstructionPercentage
{
    private const decimal MINIMUM_PERCENTAGE = 0m;
    private const decimal MAXIMUM_PERCENTAGE = 100m;

    public decimal Value { get; }

    public EnglishInstructionPercentage(decimal value)
    {
        if (value < MINIMUM_PERCENTAGE || value > MAXIMUM_PERCENTAGE)
        {
            throw new ArgumentOutOfRangeException(nameof(value), value, "English instruction percentages must be between 0 and 100.");
        }

        Value = value;
    }

    public override string ToString()
    {
        return Value.ToString(CultureInfo.InvariantCulture);
    }
}
