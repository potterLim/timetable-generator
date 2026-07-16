using System;

namespace TimetableGenerator.CatalogJson;

public sealed record EnglishInstitutionName
{
    public string Value { get; }

    public EnglishInstitutionName(string value)
    {
        if (value == null)
        {
            throw new ArgumentNullException(nameof(value));
        }

        string normalizedValue = value.Trim();
        if (normalizedValue.Length == 0)
        {
            throw new ArgumentException(
                "English institution names cannot be empty.",
                nameof(value));
        }

        Value = normalizedValue;
    }

    public override string ToString()
    {
        return Value;
    }
}
