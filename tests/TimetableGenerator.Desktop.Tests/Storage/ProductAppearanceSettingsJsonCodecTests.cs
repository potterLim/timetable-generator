using System;
using System.Text;

using TimetableGenerator.Desktop.Product.Appearance;
using TimetableGenerator.Desktop.Storage;

using Xunit;

namespace TimetableGenerator.Desktop.Tests.Storage;

public sealed class ProductAppearanceSettingsJsonCodecTests
{
    [Fact]
    public void SystemPreferenceRoundTripsWithStableSerializedValue()
    {
        assertPreferenceRoundTrip(EProductThemePreference.System, "system");
    }

    [Fact]
    public void LightPreferenceRoundTripsWithStableSerializedValue()
    {
        assertPreferenceRoundTrip(EProductThemePreference.Light, "light");
    }

    [Fact]
    public void DarkPreferenceRoundTripsWithStableSerializedValue()
    {
        assertPreferenceRoundTrip(EProductThemePreference.Dark, "dark");
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("{\"schemaVersion\":1}")]
    [InlineData("{\"schemaVersion\":2,\"themePreference\":\"system\"}")]
    [InlineData("{\"schemaVersion\":1,\"themePreference\":\"blue\"}")]
    [InlineData("{\"schemaVersion\":1,\"themePreference\":\"dark\",\"extra\":true}")]
    [InlineData("{\"schemaVersion\":1,\"schemaVersion\":1,\"themePreference\":\"dark\"}")]
    public void InvalidContractIsRejected(string json)
    {
        ProductAppearanceSettingsJsonCodec codec = new ProductAppearanceSettingsJsonCodec();

        Assert.Throws<ProductAppearanceSettingsException>(
            () => codec.Deserialize(Encoding.UTF8.GetBytes(json)));
    }

    private static void assertPreferenceRoundTrip(
        EProductThemePreference preference,
        string serializedValue)
    {
        ProductAppearanceSettingsJsonCodec codec = new ProductAppearanceSettingsJsonCodec();
        ProductAppearanceSettings settings = new ProductAppearanceSettings(preference);

        byte[] content = codec.Serialize(settings);
        ProductAppearanceSettings decodedSettings = codec.Deserialize(content);

        string json = Encoding.UTF8.GetString(content);
        Assert.Contains(
            "\"themePreference\": \"" + serializedValue + "\"",
            json,
            StringComparison.Ordinal);
        Assert.Equal(preference, decodedSettings.ThemePreference);
    }

}
