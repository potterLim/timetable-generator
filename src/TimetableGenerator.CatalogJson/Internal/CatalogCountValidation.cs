using System;

namespace TimetableGenerator.CatalogJson.Internal;

internal static class CatalogCountValidation
{
    internal static void requireNonNegative(int value, string description)
    {
        if (value < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(value), value, description + " cannot be negative.");
        }
    }

    internal static void requirePositive(int value, string description)
    {
        if (value <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(value), value, description + " must be positive.");
        }
    }
}
