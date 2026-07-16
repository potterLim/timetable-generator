using System;

namespace TimetableGenerator.Desktop.Product.Appearance;

internal sealed class ProductAppearanceSettingsException : Exception
{
    public ProductAppearanceSettingsException(string message)
        : base(message)
    {
    }

    public ProductAppearanceSettingsException(
        string message,
        Exception innerException)
        : base(message, innerException)
    {
    }
}
