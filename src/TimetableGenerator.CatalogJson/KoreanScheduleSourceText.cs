using System;

namespace TimetableGenerator.CatalogJson;

public sealed record KoreanScheduleSourceText
{
    public string Value { get; }

    public KoreanScheduleSourceText(string value)
    {
        if (value == null)
        {
            throw new ArgumentNullException(nameof(value));
        }

        string normalizedValue = value.Trim();
        if (normalizedValue.Length == 0)
        {
            throw new ArgumentException("Korean schedule source text cannot be empty.", nameof(value));
        }

        Value = normalizedValue;
    }

    public override string ToString()
    {
        return Value;
    }
}
