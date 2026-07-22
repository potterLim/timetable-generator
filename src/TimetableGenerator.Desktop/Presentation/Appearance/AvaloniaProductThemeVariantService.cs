using System;

using TimetableGenerator.Desktop.Product.Appearance;

namespace TimetableGenerator.Desktop.Presentation.Appearance;

internal sealed class AvaloniaProductThemeVariantService
    : IProductThemeVariantService
{
    private readonly Avalonia.Application mApplication;

    public AvaloniaProductThemeVariantService(Avalonia.Application application)
    {
        if (application == null)
        {
            throw new ArgumentNullException(nameof(application));
        }

        mApplication = application;
    }

    public void ApplyThemePreference(EProductThemePreference themePreference)
    {
        mApplication.RequestedThemeVariant = ProductThemeVariantPolicy.FindThemeVariant(themePreference);
    }
}
