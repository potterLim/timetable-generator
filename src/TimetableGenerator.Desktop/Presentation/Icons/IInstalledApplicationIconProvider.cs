using Avalonia;
using Avalonia.Media.Imaging;

namespace TimetableGenerator.Desktop.Presentation.Icons;

internal interface IInstalledApplicationIconProvider
{
    /// <summary>
    /// Returns a newly owned bitmap when the installed application icon can
    /// be loaded. The caller must dispose the returned bitmap.
    /// </summary>
    Bitmap? TryLoad(string bundleIdentifier, PixelSize pixelSize);
}
