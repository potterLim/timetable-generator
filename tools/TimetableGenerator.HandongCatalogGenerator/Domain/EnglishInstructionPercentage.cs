using System;
using System.Globalization;

namespace TimetableGenerator.HandongCatalogGenerator.Domain;

internal readonly record struct EnglishInstructionPercentage
{
    private const int MINIMUM_PERCENTAGE = 0;
    private const int MAXIMUM_PERCENTAGE = 100;

    public int Value { get; }

    public EnglishInstructionPercentage(int value)
    {
        if (value < MINIMUM_PERCENTAGE || value > MAXIMUM_PERCENTAGE)
        {
            throw new ArgumentOutOfRangeException(nameof(value), value, "English instruction percentage must be between 0 and 100.");
        }

        Value = value;
    }

    public override string ToString()
    {
        return Value.ToString(CultureInfo.InvariantCulture);
    }
}
