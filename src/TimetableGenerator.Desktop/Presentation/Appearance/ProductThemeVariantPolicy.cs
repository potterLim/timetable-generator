using System;

using Avalonia.Styling;

using TimetableGenerator.Desktop.Product.Appearance;

namespace TimetableGenerator.Desktop.Presentation.Appearance;

internal static class ProductThemeVariantPolicy
{
    public static ThemeVariant FindThemeVariant(EProductThemePreference themePreference)
    {
        switch (themePreference)
        {
            case EProductThemePreference.System:
                return ThemeVariant.Default;
            case EProductThemePreference.Light:
                return ThemeVariant.Light;
            case EProductThemePreference.Dark:
                return ThemeVariant.Dark;
            default:
                throw new ArgumentOutOfRangeException(
                    nameof(themePreference),
                    themePreference,
                    "Unknown product theme preference.");
        }
    }
}
