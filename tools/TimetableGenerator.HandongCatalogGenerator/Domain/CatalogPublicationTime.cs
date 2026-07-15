using System;
using System.Globalization;

namespace TimetableGenerator.HandongCatalogGenerator.Domain;

internal readonly record struct CatalogPublicationTime
{
    private const string SERIALIZATION_FORMAT = "yyyy-MM-dd'T'HH:mm:ss'Z'";

    public DateTimeOffset Value { get; }

    public CatalogPublicationTime(DateTimeOffset value)
    {
        if (value.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException("The catalog publication time must use UTC.", nameof(value));
        }

        Value = value;
    }

    public static CatalogPublicationTime Parse(string value)
    {
        DateTimeOffset parsedValue;
        bool isParsed = DateTimeOffset.TryParseExact(
            value,
            SERIALIZATION_FORMAT,
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
            out parsedValue);
        if (isParsed == false)
        {
            throw new FormatException(
                "The publication time must use an RFC 3339 UTC value such as 2026-07-16T00:00:00Z.");
        }

        return new CatalogPublicationTime(parsedValue);
    }

    public override string ToString()
    {
        return Value.ToString(SERIALIZATION_FORMAT, CultureInfo.InvariantCulture);
    }
}
