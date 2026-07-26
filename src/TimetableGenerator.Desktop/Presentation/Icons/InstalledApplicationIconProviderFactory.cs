using System;

using TimetableGenerator.Desktop.Platforms.MacOS;

namespace TimetableGenerator.Desktop.Presentation.Icons;

internal static class InstalledApplicationIconProviderFactory
{
    public static IInstalledApplicationIconProvider CreateDefault()
    {
        if (OperatingSystem.IsMacOS())
        {
            return MacOSInstalledApplicationIconProvider.Instance;
        }

        return NoOpInstalledApplicationIconProvider.Instance;
    }
}
