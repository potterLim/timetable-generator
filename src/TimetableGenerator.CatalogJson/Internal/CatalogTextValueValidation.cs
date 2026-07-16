using System;

namespace TimetableGenerator.CatalogJson.Internal;

internal static class CatalogTextValueValidation
{
    internal static void requireNonBlank(string value, string description)
    {
        if (value == null)
        {
            throw new ArgumentNullException(nameof(value));
        }

        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException(description + " cannot be empty.", nameof(value));
        }
    }
}
