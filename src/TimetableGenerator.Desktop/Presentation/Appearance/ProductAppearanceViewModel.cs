using System;
using System.Threading.Tasks;

using TimetableGenerator.Desktop.Product.Appearance;

namespace TimetableGenerator.Desktop.Presentation.Appearance;

internal sealed partial class ProductAppearanceViewModel : ObservableObject
{
    private readonly IProductThemeVariantService mThemeVariantService;

    private EProductThemePreference mThemePreference;

    public EProductThemePreference ThemePreference
    {
        get
        {
            return mThemePreference;
        }
    }

    public bool IsSystemThemeSelected
    {
        get
        {
            return mThemePreference == EProductThemePreference.System;
        }

        set
        {
            if (value)
            {
                selectTheme(EProductThemePreference.System);
            }
        }
    }

    public bool IsLightThemeSelected
    {
        get
        {
            return mThemePreference == EProductThemePreference.Light;
        }

        set
        {
            if (value)
            {
                selectTheme(EProductThemePreference.Light);
            }
        }
    }

    public bool IsDarkThemeSelected
    {
        get
        {
            return mThemePreference == EProductThemePreference.Dark;
        }

        set
        {
            if (value)
            {
                selectTheme(EProductThemePreference.Dark);
            }
        }
    }

    public ProductAppearanceViewModel(
        IProductAppearanceSettingsStore settingsStore,
        IProductThemeVariantService themeVariantService)
    {
        if (settingsStore == null)
        {
            throw new ArgumentNullException(nameof(settingsStore));
        }

        if (themeVariantService == null)
        {
            throw new ArgumentNullException(nameof(themeVariantService));
        }

        mSettingsStore = settingsStore;
        mThemeVariantService = themeVariantService;
        mPersistenceSync = new object();
        mRetryPersistenceCommand = new DelegateCommand(retryPersistence);
        mPersistenceTask = Task.CompletedTask;
        ProductAppearanceSettings settings = mSettingsStore.LoadOrDefault();
        mThemePreference = settings.ThemePreference;
        mPersistenceFailureMessage = string.Empty;
        mThemeVariantService.ApplyThemePreference(mThemePreference);
    }

    private void selectTheme(EProductThemePreference themePreference)
    {
        bool isPreferenceChanged = mThemePreference != themePreference;
        if (isPreferenceChanged)
        {
            mThemeVariantService.ApplyThemePreference(themePreference);
            mThemePreference = themePreference;
            raiseThemeSelectionPropertiesChanged();
        }

        schedulePersistence(themePreference);
    }

    private void raiseThemeSelectionPropertiesChanged()
    {
        raisePropertyChanged(nameof(ThemePreference));
        raisePropertyChanged(nameof(IsSystemThemeSelected));
        raisePropertyChanged(nameof(IsLightThemeSelected));
        raisePropertyChanged(nameof(IsDarkThemeSelected));
    }
}
