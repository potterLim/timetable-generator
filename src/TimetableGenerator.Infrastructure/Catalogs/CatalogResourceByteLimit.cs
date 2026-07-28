using System;
using System.Globalization;

namespace TimetableGenerator.Infrastructure.Catalogs;

public readonly record struct CatalogResourceByteLimit
{
    private const long MAXIMUM_SUPPORTED_BYTES = int.MaxValue - 4_096L;

    public long Bytes { get; }

    public bool IsValid
    {
        get
        {
            return Bytes > 0L && Bytes <= MAXIMUM_SUPPORTED_BYTES;
        }
    }

    public CatalogResourceByteLimit(long bytes)
    {
        if (bytes <= 0L || bytes > MAXIMUM_SUPPORTED_BYTES)
        {
            throw new ArgumentOutOfRangeException(nameof(bytes), bytes, "Catalog resource limits must fit in a supported in-memory document.");
        }

        Bytes = bytes;
    }

    public override string ToString()
    {
        return Bytes.ToString(CultureInfo.InvariantCulture);
    }
}
