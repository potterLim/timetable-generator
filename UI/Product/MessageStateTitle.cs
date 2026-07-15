using System;

namespace TimetableGenerator.UI.Product;

internal sealed record MessageStateTitle
{
    internal string Value { get; }

    internal MessageStateTitle(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Message state titles cannot be empty.", nameof(value));
        }

        Value = value.Trim();
    }
}
