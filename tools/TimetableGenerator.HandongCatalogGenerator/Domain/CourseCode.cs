using System;
using System.Text.RegularExpressions;

namespace TimetableGenerator.HandongCatalogGenerator.Domain;

internal sealed record CourseCode
{
    private static readonly Regex VALID_FORMAT = new Regex("^[A-Z]{3}[0-9]{5}$", RegexOptions.CultureInvariant);

    public string Value { get; }

    public CourseCode(string value)
    {
        if (value == null)
        {
            throw new ArgumentNullException(nameof(value));
        }

        string normalizedValue = value.Trim();
        if (VALID_FORMAT.IsMatch(normalizedValue) == false)
        {
            throw new ArgumentException(
                "Course codes must contain three uppercase letters and five digits.",
                nameof(value));
        }

        Value = normalizedValue;
    }

    public override string ToString()
    {
        return Value;
    }
}
