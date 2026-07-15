using System;

namespace TimetableGenerator.HandongCatalogGenerator.Publishing;

internal sealed class CatalogIndexFormatException : Exception
{
    public CatalogIndexFormatException(string message)
        : base(message)
    {
    }

    public CatalogIndexFormatException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
