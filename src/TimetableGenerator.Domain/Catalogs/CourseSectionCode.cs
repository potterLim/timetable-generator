using System;
using System.Text.RegularExpressions;

namespace TimetableGenerator.Domain.Catalogs;

public sealed record CourseSectionCode
{
    private static readonly Regex VALID_FORMAT = new Regex(
        "^[0-9]{2}$",
        RegexOptions.CultureInvariant);

    public string Value { get; }

    public CourseSectionCode(string value)
    {
        if (value == null)
        {
            throw new ArgumentNullException(nameof(value));
        }

        string normalizedValue = value.Trim();
        if (VALID_FORMAT.IsMatch(normalizedValue) == false)
        {
            throw new ArgumentException(
                "Course section codes must contain exactly two digits.",
                nameof(value));
        }

        Value = normalizedValue;
    }

    public override string ToString()
    {
        return Value;
    }
}
