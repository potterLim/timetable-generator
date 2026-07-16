using System;
using System.IO;
using System.Text;

using TimetableGenerator.Desktop.Product.Appearance;
using TimetableGenerator.Desktop.Storage;

using Xunit;

namespace TimetableGenerator.Desktop.Tests.Storage;

public sealed class ProductAppearanceSettingsFileStoreTests
{
    [Fact]
    public void MissingSettingsUseSystemPreference()
    {
        string testDirectoryPath = createTestDirectoryPath();
        try
        {
            ProductAppearanceSettingsFileStore store = createStore(
                testDirectoryPath);

            ProductAppearanceSettings settings = store.LoadOrDefault();

            Assert.Equal(
                EProductThemePreference.System,
                settings.ThemePreference);
        }
        finally
        {
            tryDeleteDirectory(testDirectoryPath);
        }
    }

    [Fact]
    public void SavedPreferenceIsRestoredByANewStore()
    {
        string testDirectoryPath = createTestDirectoryPath();
        try
        {
            ProductAppearanceSettingsFileStore firstStore = createStore(
                testDirectoryPath);
            firstStore.Save(
                new ProductAppearanceSettings(EProductThemePreference.Light));
            ProductAppearanceSettingsFileStore secondStore = createStore(
                testDirectoryPath);

            ProductAppearanceSettings restoredSettings =
                secondStore.LoadOrDefault();

            Assert.Equal(
                EProductThemePreference.Light,
                restoredSettings.ThemePreference);
            Assert.Empty(
                Directory.GetFiles(testDirectoryPath, "*.tmp"));
        }
        finally
        {
            tryDeleteDirectory(testDirectoryPath);
        }
    }

    [Fact]
    public void CorruptSettingsRecoverToSystemPreference()
    {
        string testDirectoryPath = createTestDirectoryPath();
        try
        {
            string settingsFilePath = createSettingsFilePath(
                testDirectoryPath);
            Directory.CreateDirectory(testDirectoryPath);
            File.WriteAllText(
                settingsFilePath,
                "not-json",
                Encoding.UTF8);
            ProductAppearanceSettingsFileStore store = createStore(
                testDirectoryPath);

            ProductAppearanceSettings settings = store.LoadOrDefault();

            Assert.Equal(
                EProductThemePreference.System,
                settings.ThemePreference);
        }
        finally
        {
            tryDeleteDirectory(testDirectoryPath);
        }
    }

    private static ProductAppearanceSettingsFileStore createStore(
        string testDirectoryPath)
    {
        ProductAppearanceSettingsFilePath filePath =
            new ProductAppearanceSettingsFilePath(
                createSettingsFilePath(testDirectoryPath));
        return new ProductAppearanceSettingsFileStore(
            filePath,
            new ProductAppearanceSettingsJsonCodec());
    }

    private static string createSettingsFilePath(string testDirectoryPath)
    {
        return Path.Combine(testDirectoryPath, "appearance-v1.json");
    }

    private static string createTestDirectoryPath()
    {
        return Path.Combine(
            Path.GetTempPath(),
            "TimetableGenerator.Tests",
            Guid.NewGuid().ToString("N"));
    }

    private static void tryDeleteDirectory(string testDirectoryPath)
    {
        if (Directory.Exists(testDirectoryPath))
        {
            Directory.Delete(testDirectoryPath, true);
        }
    }
}
