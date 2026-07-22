using System;
using System.Diagnostics;

using Avalonia.Controls;

namespace TimetableGenerator.Desktop.Presentation.Windowing;

internal static class WindowChromeLayoutPolicy
{
    private static readonly WindowChromeInsets WINDOWS_INSETS = new WindowChromeInsets(28.0, 0.0);

    private static readonly WindowChromeInsets MAC_OS_INSETS = new WindowChromeInsets(96.0, 22.0);

    private static readonly WindowChromeInsets OTHER_INSETS = new WindowChromeInsets(28.0, 22.0);

    public static EWindowChromePlatform FindCurrentPlatform()
    {
        if (OperatingSystem.IsWindows())
        {
            return EWindowChromePlatform.Windows;
        }

        if (OperatingSystem.IsMacOS())
        {
            return EWindowChromePlatform.MacOS;
        }

        return EWindowChromePlatform.Other;
    }

    public static WindowChromeInsets FindTitleBarInsets(EWindowChromePlatform platform)
    {
        switch (platform)
        {
            case EWindowChromePlatform.Windows:
                return WINDOWS_INSETS;
            case EWindowChromePlatform.MacOS:
                return MAC_OS_INSETS;
            case EWindowChromePlatform.Other:
                return OTHER_INSETS;
            default:
                Debug.Fail("Unexpected window chrome platform: " + platform);
                return OTHER_INSETS;
        }
    }

    public static WindowDecorations FindWindowDecorations(EWindowChromePlatform platform)
    {
        switch (platform)
        {
            case EWindowChromePlatform.Windows:
                return WindowDecorations.None;
            case EWindowChromePlatform.MacOS:
            case EWindowChromePlatform.Other:
                return WindowDecorations.Full;
            default:
                Debug.Fail("Unexpected window chrome platform: " + platform);
                return WindowDecorations.Full;
        }
    }
}
