using TimetableGenerator.Desktop.Presentation.Windowing;

using Xunit;

namespace TimetableGenerator.Desktop.Tests.Presentation.Windowing;

public sealed class WindowChromeLayoutPolicyTests
{
    [Fact]
    public void WindowsInsetsReserveNativeCaptionButtons()
    {
        WindowChromeInsets insets = WindowChromeLayoutPolicy.FindTitleBarInsets(
            EWindowChromePlatform.Windows);

        Assert.Equal(28.0, insets.Left);
        Assert.Equal(160.0, insets.Right);
    }

    [Fact]
    public void MacOSInsetsReserveTrafficLights()
    {
        WindowChromeInsets insets = WindowChromeLayoutPolicy.FindTitleBarInsets(
            EWindowChromePlatform.MacOS);

        Assert.Equal(96.0, insets.Left);
        Assert.Equal(22.0, insets.Right);
    }

    [Fact]
    public void OtherPlatformInsetsPreserveStandardContentMargins()
    {
        WindowChromeInsets insets = WindowChromeLayoutPolicy.FindTitleBarInsets(
            EWindowChromePlatform.Other);

        Assert.Equal(28.0, insets.Left);
        Assert.Equal(22.0, insets.Right);
    }
}
