using System;

namespace TimetableGenerator.UI.Product;

internal sealed record MessageStateDetail
{
    internal string Value { get; }

    internal MessageStateDetail(string value)
    {
        if (value == null)
        {
            throw new ArgumentNullException(nameof(value));
        }

        Value = value.Trim();
    }
}
