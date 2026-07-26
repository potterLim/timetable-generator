using System;

using Avalonia;

namespace TimetableGenerator.Desktop.Presentation.Icons;

internal static class InstalledApplicationIconRequest
{
    public static void Validate(
        string bundleIdentifier,
        PixelSize pixelSize)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(bundleIdentifier);
        if (pixelSize.Width <= 0 || pixelSize.Height <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(pixelSize),
                "Installed application icon dimensions must be positive.");
        }
    }
}
