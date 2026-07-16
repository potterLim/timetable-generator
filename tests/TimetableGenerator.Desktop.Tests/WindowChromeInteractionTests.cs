using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Chrome;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using TimetableGenerator.Desktop.Presentation.ViewModels;
using TimetableGenerator.Desktop.Presentation.Windowing;
using TimetableGenerator.Desktop.Product;
using TimetableGenerator.Desktop.Tests.Presentation.Appearance;
using TimetableGenerator.Desktop.Views;
using Xunit;

namespace TimetableGenerator.Desktop.Tests;

public sealed class WindowChromeInteractionTests
{
    [AvaloniaFact]
    public void ProductTitleBarKeepsNativeCaptionControlsClear()
    {
        PlannerWorkspaceViewModel workspace =
            PlannerWorkspaceTestFactory.CreateWorkspace();
        ProductShellViewModel shell =
            PlannerWorkspaceTestFactory.CreateShell(workspace);
        MainWindow hostWindow = new MainWindow(
            shell,
            ProductAppearanceTestFactory.CreateViewModel());

        try
        {
            hostWindow.Show();

            Border titleBar = findRequiredControl<Border>(
                hostWindow,
                "ProductTitleBar");
            Button appearanceButton = findRequiredControl<Button>(
                hostWindow,
                "AppearanceButton");

            Assert.Equal(
                WindowDecorationsElementRole.TitleBar,
                WindowDecorationProperties.GetElementRole(titleBar));
            Assert.Equal(
                WindowDecorationsElementRole.User,
                WindowDecorationProperties.GetElementRole(appearanceButton));
            Assert.True(appearanceButton.IsHitTestVisible);
            Assert.True(appearanceButton.Focusable);
            Assert.NotNull(appearanceButton.Flyout);

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
