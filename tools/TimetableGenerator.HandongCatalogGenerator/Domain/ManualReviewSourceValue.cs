using System;

namespace TimetableGenerator.HandongCatalogGenerator.Domain;

internal sealed record ManualReviewSourceValue
{
    public string Value { get; }

    public ManualReviewSourceValue(string value)
    {
        if (value == null)
        {
            throw new ArgumentNullException(nameof(value));
        }

        if (value.Length == 0)
        {
            throw new ArgumentException("A manual-review source value cannot be empty.", nameof(value));
        }

        Value = value;
    }

    public override string ToString()
    {
        return Value;
    }
}
