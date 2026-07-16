using System;
using System.Globalization;

namespace TimetableGenerator.Domain.Catalogs;

public readonly record struct CatalogRevision
{
    private const int MINIMUM_REVISION = 1;

    public int Value { get; }

    public bool IsValid
    {
        get
        {
            return Value >= MINIMUM_REVISION;
        }
    }

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
                "Catalog revisions must be positive.");
        }

        Value = value;
    }

    public override string ToString()
    {
        return Value.ToString(CultureInfo.InvariantCulture);
    }
}
