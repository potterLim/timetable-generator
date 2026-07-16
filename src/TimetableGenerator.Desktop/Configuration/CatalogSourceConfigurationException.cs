using System;

namespace TimetableGenerator.Desktop.Configuration;

internal sealed class CatalogSourceConfigurationException : Exception
{
    public CatalogSourceConfigurationException(string message)
        : base(message)
    {
    }

    public CatalogSourceConfigurationException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
