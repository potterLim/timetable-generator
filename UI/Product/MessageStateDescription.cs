using System;

namespace TimetableGenerator.UI.Product;

internal sealed record MessageStateDescription
{
    internal string Value { get; }

    internal MessageStateDescription(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException(
                "Message state descriptions cannot be empty.",
                nameof(value));
        }

        Value = value.Trim();
    }
}
