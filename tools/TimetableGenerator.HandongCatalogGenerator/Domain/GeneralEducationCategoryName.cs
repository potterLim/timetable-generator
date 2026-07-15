using System;

namespace TimetableGenerator.HandongCatalogGenerator.Domain;

internal sealed record GeneralEducationCategoryName
{
    public string Value { get; }

    public GeneralEducationCategoryName(string value)
    {
        if (value == null)
        {
            throw new ArgumentNullException(nameof(value));
        }

        string normalizedValue = value.Trim();
        if (normalizedValue.Length == 0)
        {
            throw new ArgumentException("The general education category cannot be empty.", nameof(value));
        }

        Value = normalizedValue;
    }

    public override string ToString()
    {
        return Value;
    }
}
