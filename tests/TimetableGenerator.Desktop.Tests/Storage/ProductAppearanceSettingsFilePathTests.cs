using System;
using System.IO;

using TimetableGenerator.Desktop.Storage;

using Xunit;

namespace TimetableGenerator.Desktop.Tests.Storage;

public sealed class ProductAppearanceSettingsFilePathTests
{
    [Fact]
    public void RelativePathIsRejected()
    {
        Assert.Throws<ArgumentException>(
            () => new ProductAppearanceSettingsFilePath(
                "appearance-v1.json"));
    }

    [Fact]
    public void DirectoryPathIsRejected()
    {
        string directoryPath = Path.Combine(Path.GetTempPath(), "Settings") + Path.DirectorySeparatorChar;

        Assert.Throws<ArgumentException>(
            () => new ProductAppearanceSettingsFilePath(directoryPath));
    }
}
