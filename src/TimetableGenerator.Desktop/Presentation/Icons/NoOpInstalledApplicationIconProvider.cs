using Avalonia;
using Avalonia.Media.Imaging;

namespace TimetableGenerator.Desktop.Presentation.Icons;

internal sealed class NoOpInstalledApplicationIconProvider
    : IInstalledApplicationIconProvider
{
    public static NoOpInstalledApplicationIconProvider Instance { get; } =
        new NoOpInstalledApplicationIconProvider();

    private NoOpInstalledApplicationIconProvider()
    {
    }

    public Bitmap? TryLoad(string bundleIdentifier, PixelSize pixelSize)
    {
        InstalledApplicationIconRequest.Validate(
            bundleIdentifier,
            pixelSize);
        return null;
    }
}
