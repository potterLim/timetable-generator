namespace TimetableGenerator.Desktop.Product.Appearance;

internal interface IProductAppearanceSettingsStore
{
    ProductAppearanceSettings LoadOrDefault();

    void Save(ProductAppearanceSettings settings);
}
