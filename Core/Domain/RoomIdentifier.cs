using System;

namespace TimetableGenerator.Core.Domain;

public sealed record RoomIdentifier
{
    public string Value { get; }

    public RoomIdentifier(string value)
    {
        if (value == null)
        {
            throw new ArgumentNullException(nameof(value));
        }

        string normalizedValue = value.Trim();
        if (normalizedValue.Length == 0)
        {
            throw new ArgumentException("Room identifiers cannot be empty.", nameof(value));
        }

        Value = normalizedValue;
    }

    public override string ToString()
    {
        return Value;
    }
}
