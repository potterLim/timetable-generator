using System;

namespace TimetableGenerator.Infrastructure.Csv;

public sealed record CsvInputFileName
{
    public string Value { get; }

    internal CsvInputFileName(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException(
                "CSV input file names cannot be empty.",
                nameof(value));
        }

        Value = value;
    }

    public override string ToString()
    {
        return Value;
    }
}
