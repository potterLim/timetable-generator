using System;
using System.Globalization;

namespace TimetableGenerator.HandongCatalogGenerator.Domain;

internal readonly record struct CourseCredits
{
    private const decimal CREDIT_INCREMENT = 0.5m;

    public decimal Value { get; }

    public CourseCredits(decimal value)
    {
        if (value < 0m || value % CREDIT_INCREMENT != 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(value), value, "Course credits must be nonnegative and use 0.5-credit increments.");
        }

        Value = value;
    }

    public override string ToString()
    {
        return Value.ToString(CultureInfo.InvariantCulture);
    }
}
