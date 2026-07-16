using System;

namespace TimetableGenerator.Desktop.Product.Appearance;

internal sealed record ProductAppearanceSettings
{
    public EProductThemePreference ThemePreference { get; }

    public ProductAppearanceSettings(
        EProductThemePreference themePreference)
    {
        switch (themePreference)
        {
            case EProductThemePreference.System:
            case EProductThemePreference.Light:
            case EProductThemePreference.Dark:
                ThemePreference = themePreference;
                break;
            default:
                throw new ArgumentOutOfRangeException(
                    nameof(themePreference),
                    themePreference,
                    "Unknown product theme preference.");
        }
    }

    public static ProductAppearanceSettings CreateDefault()
    {
        return new ProductAppearanceSettings(EProductThemePreference.System);
    }
}
