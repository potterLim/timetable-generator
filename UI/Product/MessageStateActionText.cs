using System;

namespace TimetableGenerator.UI.Product;

internal sealed record MessageStateActionText
{
    internal string Value { get; }

    internal MessageStateActionText(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException(
                "Message state action text cannot be empty.",
                nameof(value));
        }

        Value = value.Trim();
    }
}
