using System;

namespace TimetableGenerator.Domain.Catalogs;

public sealed record InstitutionId
{
    public string Value { get; }

    public InstitutionId(string value)
    {
        if (value == null)
        {
            throw new ArgumentNullException(nameof(value));
        }

        string normalizedValue = value.Trim();
        if (normalizedValue.Length == 0)
        {
            throw new ArgumentException("Institution IDs cannot be empty.", nameof(value));
        }

        Value = normalizedValue;
    }

    public override string ToString()
    {
        return Value;
    }
}
