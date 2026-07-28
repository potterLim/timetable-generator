using System;

using Avalonia.Styling;

using TimetableGenerator.Desktop.Presentation.Appearance;
using TimetableGenerator.Desktop.Product.Appearance;

using Xunit;

namespace TimetableGenerator.Desktop.Tests.Presentation.Appearance;

public sealed class ProductThemeVariantPolicyTests
{
    [Fact]
    public void SystemPreferenceMapsToDefaultVariant()
    {
        Assert.Same(ThemeVariant.Default, ProductThemeVariantPolicy.FindThemeVariant(EProductThemePreference.System));
    }

    [Fact]
    public void LightPreferenceMapsToLightVariant()
    {
        Assert.Same(ThemeVariant.Light, ProductThemeVariantPolicy.FindThemeVariant(EProductThemePreference.Light));
    }

    [Fact]
    public void DarkPreferenceMapsToDarkVariant()
    {
        Assert.Same(ThemeVariant.Dark, ProductThemeVariantPolicy.FindThemeVariant(EProductThemePreference.Dark));
    }

    [Fact]
    public void UndefinedPreferenceIsRejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => ProductThemeVariantPolicy.FindThemeVariant(
                (EProductThemePreference)int.MaxValue));
    }
}
