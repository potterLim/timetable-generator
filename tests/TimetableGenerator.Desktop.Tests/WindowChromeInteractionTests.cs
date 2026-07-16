using System;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Chrome;
using Avalonia.Headless.XUnit;
using Avalonia.Input;

using TimetableGenerator.Desktop.Presentation.Windowing;
using TimetableGenerator.Desktop.Views;

using Xunit;

namespace TimetableGenerator.Desktop.Tests;

public sealed class WindowChromeInteractionTests
{
    [AvaloniaFact]
    public void ProductTitleBarKeepsHelpButtonInteractive()
    {
        ProductWorkspaceHostView workspaceHost =
            new ProductWorkspaceHostView();
        Window hostWindow = new Window();
        hostWindow.Content = workspaceHost;

        try
        {
            hostWindow.Show();

            Border titleBar = findRequiredControl<Border>(
                workspaceHost,
                "ProductTitleBar");
            Button helpButton = findRequiredControl<Button>(
                workspaceHost,
                "HelpButton");

            Assert.Equal(
                WindowDecorationsElementRole.TitleBar,
                WindowDecorationProperties.GetElementRole(titleBar));
            Assert.Equal(
                WindowDecorationsElementRole.User,
                WindowDecorationProperties.GetElementRole(helpButton));
            Assert.True(helpButton.IsHitTestVisible);
            Assert.True(helpButton.Focusable);
            Assert.True(helpButton.Focus());

            EWindowChromePlatform platform =
                WindowChromeLayoutPolicy.FindCurrentPlatform();
            WindowChromeInsets insets =
                WindowChromeLayoutPolicy.FindTitleBarInsets(platform);
            Assert.Equal(insets.Left, titleBar.Padding.Left);
            Assert.Equal(insets.Right, titleBar.Padding.Right);
        }
        finally
        {
            hostWindow.Close();
        }
    }

    private static TControl findRequiredControl<TControl>(
        Control root,
        string controlName)
        where TControl : Control
    {
        TControl? controlOrNull = root.FindControl<TControl>(controlName);
        Assert.NotNull(controlOrNull);
        if (controlOrNull == null)
        {
            throw new InvalidOperationException(
                "The required control could not be resolved: " + controlName);
        }

        return controlOrNull;
    }
}
