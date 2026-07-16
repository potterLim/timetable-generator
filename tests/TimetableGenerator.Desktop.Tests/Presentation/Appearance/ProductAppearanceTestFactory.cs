using TimetableGenerator.Desktop.Presentation.Appearance;
using TimetableGenerator.Desktop.Product.Appearance;

namespace TimetableGenerator.Desktop.Tests.Presentation.Appearance;

internal static class ProductAppearanceTestFactory
{
    public static ProductAppearanceViewModel CreateViewModel()
    {
        ControlledProductAppearanceSettingsStore settingsStore =
            new ControlledProductAppearanceSettingsStore(
                ProductAppearanceSettings.CreateDefault());
        return new ProductAppearanceViewModel(
            settingsStore,
            new RecordingProductThemeVariantService());
    }
}
