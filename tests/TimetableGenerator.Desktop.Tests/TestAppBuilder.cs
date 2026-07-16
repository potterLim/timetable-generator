using Avalonia;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;

using TimetableGenerator.Desktop;

namespace TimetableGenerator.Desktop.Tests;

internal static class TestAppBuilder
{
    public static AppBuilder BuildAvaloniaApp()
    {
        AvaloniaHeadlessPlatformOptions platformOptions = new AvaloniaHeadlessPlatformOptions();
        platformOptions.UseHeadlessDrawing = false;

        return AppBuilder.Configure<App>()
            .UseSkia()
            .UseHeadless(platformOptions);
    }
}
