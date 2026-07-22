using Avalonia;
using Avalonia.Headless;

namespace TimetableGenerator.Desktop.Tests;

internal static class TestAppBuilder
{
    public static AppBuilder BuildAvaloniaApp()
    {
        AvaloniaHeadlessPlatformOptions platformOptions = new AvaloniaHeadlessPlatformOptions();
        platformOptions.UseHeadlessDrawing = false;

        return AppBuilder.Configure<App>().UseSkia().UseHeadless(platformOptions);
    }
}
