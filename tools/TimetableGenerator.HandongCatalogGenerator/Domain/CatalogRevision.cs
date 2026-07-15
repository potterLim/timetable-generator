using System;
using System.Globalization;

namespace TimetableGenerator.HandongCatalogGenerator.Domain;

internal readonly record struct CatalogRevision
{
    private const int MINIMUM_REVISION = 1;

    public int Value { get; }

    public string FileComponent
    {
        get
        {
            return "r" + Value.ToString("D4", CultureInfo.InvariantCulture);
        }
    }

    public CatalogRevision(int value)
    {
        if (value < MINIMUM_REVISION)
        {
            throw new ArgumentOutOfRangeException(
                nameof(value),
                value,
                "The catalog revision must be positive.");
        }

        Value = value;
    }

    public override string ToString()
    {
        return Value.ToString(CultureInfo.InvariantCulture);
    }
}
