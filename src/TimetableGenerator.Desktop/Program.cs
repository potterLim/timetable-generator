using System;

using Avalonia;

namespace TimetableGenerator.Desktop;

internal static class Program
{
    [STAThread]
    public static void Main(string[] arguments)
    {
        BuildAvaloniaApp().StartWithClassicDesktopLifetime(arguments);
    }

    public static AppBuilder BuildAvaloniaApp()
    {
        return AppBuilder.Configure<App>().UsePlatformDetect().LogToTrace();
    }
}
