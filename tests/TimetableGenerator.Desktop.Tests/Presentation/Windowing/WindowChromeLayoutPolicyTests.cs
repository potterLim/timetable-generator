using Avalonia.Controls;

using TimetableGenerator.Desktop.Presentation.Windowing;

using Xunit;

namespace TimetableGenerator.Desktop.Tests.Presentation.Windowing;

public sealed class WindowChromeLayoutPolicyTests
{
    [Fact]
    public void WindowsInsetsKeepEmbeddedCaptionControlsFlushRight()
    {
        WindowChromeInsets insets = WindowChromeLayoutPolicy.FindTitleBarInsets(EWindowChromePlatform.Windows);

        Assert.Equal(28.0, insets.Left);
        Assert.Equal(0.0, insets.Right);
    }

    [Fact]
    public void MacOSInsetsReserveTrafficLights()
    {
        WindowChromeInsets insets = WindowChromeLayoutPolicy.FindTitleBarInsets(EWindowChromePlatform.MacOS);

        Assert.Equal(96.0, insets.Left);
        Assert.Equal(22.0, insets.Right);
    }

    [Fact]
    public void OtherPlatformInsetsPreserveStandardContentMargins()
    {
        WindowChromeInsets insets = WindowChromeLayoutPolicy.FindTitleBarInsets(EWindowChromePlatform.Other);

        Assert.Equal(28.0, insets.Left);
        Assert.Equal(22.0, insets.Right);
    }

    [Fact]
    public void WindowsUsesEmbeddedCaptionControlsWithoutANativeTitleBar()
    {
        WindowDecorations decorations = WindowChromeLayoutPolicy.FindWindowDecorations(EWindowChromePlatform.Windows);

        Assert.Equal(WindowDecorations.None, decorations);
    }

    [Fact]
    public void MacOSKeepsPlatformCaptionControls()
    {
        WindowDecorations decorations = WindowChromeLayoutPolicy.FindWindowDecorations(EWindowChromePlatform.MacOS);

        Assert.Equal(WindowDecorations.Full, decorations);
    }

    [Fact]
    public void OtherPlatformsKeepManagedCaptionControls()
    {
        WindowDecorations decorations = WindowChromeLayoutPolicy.FindWindowDecorations(EWindowChromePlatform.Other);

        Assert.Equal(WindowDecorations.Full, decorations);
    }
}
