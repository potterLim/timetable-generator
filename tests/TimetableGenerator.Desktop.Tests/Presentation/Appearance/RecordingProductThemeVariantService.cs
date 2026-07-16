using System.Collections.Generic;

using TimetableGenerator.Desktop.Presentation.Appearance;
using TimetableGenerator.Desktop.Product.Appearance;

namespace TimetableGenerator.Desktop.Tests.Presentation.Appearance;

internal sealed class RecordingProductThemeVariantService
    : IProductThemeVariantService
{
    private readonly List<EProductThemePreference> mAppliedPreferences;

    public IReadOnlyList<EProductThemePreference> AppliedPreferences
    {
        get
        {
            return mAppliedPreferences.AsReadOnly();
        }
    }

    public RecordingProductThemeVariantService()
    {
        mAppliedPreferences = new List<EProductThemePreference>();
    }

    public void ApplyThemePreference(
        EProductThemePreference themePreference)
    {
        mAppliedPreferences.Add(themePreference);
    }
}
