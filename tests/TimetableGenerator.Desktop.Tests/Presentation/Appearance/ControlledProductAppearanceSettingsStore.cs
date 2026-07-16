using System;
using System.Collections.Generic;

using TimetableGenerator.Desktop.Product.Appearance;

namespace TimetableGenerator.Desktop.Tests.Presentation.Appearance;

internal sealed class ControlledProductAppearanceSettingsStore
    : IProductAppearanceSettingsStore
{
    private readonly List<ProductAppearanceSettings> mSavedSettings;

    private readonly List<int> mSaveThreadIds;

    private readonly ProductAppearanceSettings mLoadedSettings;

    private ProductAppearanceSettingsException? mSaveFailureOrNull;

    public IReadOnlyList<ProductAppearanceSettings> SavedSettings
    {
        get
        {
            return mSavedSettings.AsReadOnly();
        }
    }

    public IReadOnlyList<int> SaveThreadIds
    {
        get
        {
            return mSaveThreadIds.AsReadOnly();
        }
    }

    public ControlledProductAppearanceSettingsStore(
        ProductAppearanceSettings loadedSettings)
    {
        if (loadedSettings == null)
        {
            throw new ArgumentNullException(nameof(loadedSettings));
        }

        mLoadedSettings = loadedSettings;
        mSavedSettings = new List<ProductAppearanceSettings>();
        mSaveThreadIds = new List<int>();
    }

    public ProductAppearanceSettings LoadOrDefault()
    {
        return mLoadedSettings;
    }

    public void Save(ProductAppearanceSettings settings)
    {
        mSaveThreadIds.Add(Environment.CurrentManagedThreadId);
        if (mSaveFailureOrNull != null)
        {
            throw mSaveFailureOrNull;
        }

        mSavedSettings.Add(settings);
    }

    public void FailSaves(ProductAppearanceSettingsException failure)
    {
        if (failure == null)
        {
            throw new ArgumentNullException(nameof(failure));
        }

        mSaveFailureOrNull = failure;
    }

    public void AllowSaves()
    {
        mSaveFailureOrNull = null;
    }
}
