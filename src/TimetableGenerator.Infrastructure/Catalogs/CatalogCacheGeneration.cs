using System;
using System.Globalization;

namespace TimetableGenerator.Infrastructure.Catalogs;

internal readonly record struct CatalogCacheGeneration
{
    private const long FIRST_GENERATION = 1L;

    public long Value { get; }

    public string FileComponent
    {
        get
        {
            return "g" + Value.ToString("D20", CultureInfo.InvariantCulture);
        }
    }

    public CatalogCacheGeneration(long value)
    {
        if (value < FIRST_GENERATION)
        {
            throw new ArgumentOutOfRangeException(
                nameof(value),
                value,
                "Catalog cache generations must be positive.");
        }

        Value = value;
    }

    public CatalogCacheGeneration GetNext()
    {
        if (Value == long.MaxValue)
        {
            throw new InvalidOperationException("The catalog cache generation range is exhausted.");
        }

        return new CatalogCacheGeneration(Value + 1L);
    }

    public static bool TryParseFileComponent(
        string value,
        out CatalogCacheGeneration generation)
    {
        generation = default(CatalogCacheGeneration);
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

        generation = new CatalogCacheGeneration(parsedValue);
        return true;
    }
}
