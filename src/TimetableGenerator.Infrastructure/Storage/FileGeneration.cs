using System;
using System.Globalization;

namespace TimetableGenerator.Infrastructure.Storage;

internal readonly record struct FileGeneration
{
    private const long FIRST_GENERATION = 1L;

    public long Value { get; }

    public bool IsValid
    {
        get
        {
            return Value >= FIRST_GENERATION;
        }
    }

    public string FileComponent
    {
        get
        {
            return "g" + Value.ToString("D20", CultureInfo.InvariantCulture);
        }
    }

    public FileGeneration(long value)
    {
        if (value < FIRST_GENERATION)
        {
            throw new ArgumentOutOfRangeException(nameof(value), value, "File generations must be positive.");
        }

        Value = value;
    }

    public static bool TryParseFileComponent(string value, out FileGeneration generation)
    {
        generation = default(FileGeneration);
        if (value == null || value.Length != 21 || value[0] != 'g')
        {
            return false;
        }

        long parsedValue;
        bool isParsed = long.TryParse(
            value.AsSpan(1),
            NumberStyles.None,
            CultureInfo.InvariantCulture,
            out parsedValue);
        if (isParsed == false || parsedValue < FIRST_GENERATION)
        {
            return false;
        }

        generation = new FileGeneration(parsedValue);
        return true;
    }
}
