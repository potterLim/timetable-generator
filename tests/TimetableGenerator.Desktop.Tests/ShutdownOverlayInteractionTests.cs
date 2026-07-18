using System;

using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;

using TimetableGenerator.Desktop.Presentation.ViewModels;
using TimetableGenerator.Desktop.Product;
using TimetableGenerator.Desktop.Tests.Presentation.Appearance;
using TimetableGenerator.Desktop.Views;

using Xunit;

namespace TimetableGenerator.Desktop.Tests;

public sealed class ShutdownOverlayInteractionTests
{
    [AvaloniaFact]
    public void SaveFailureKeepsBackgroundLockedUntilEditingContinues()
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
            Dispatcher.UIThread.RunJobs();

            Grid productInteractionSurface = findRequiredControl<Grid>(
                hostWindow,
                "ProductInteractionSurface");
            Border shutdownOverlay = findRequiredControl<Border>(
                hostWindow,
                "ShutdownOverlay");
            Border shutdownDialog = findRequiredControl<Border>(
                hostWindow,
                "ShutdownDialog");
            Button appearanceButton = findRequiredControl<Button>(
                hostWindow,
                "AppearanceButton");
            Button continueEditingButton = findRequiredControl<Button>(
                hostWindow,
                "ContinueEditingButton");

            shell.beginShutdown();
            shell.showShutdownFailure(
                new InvalidOperationException("Expected save failure."));
            Dispatcher.UIThread.RunJobs();

            Assert.True(shutdownOverlay.IsVisible);
            Assert.False(productInteractionSurface.IsEnabled);
            Assert.False(appearanceButton.IsEnabled);
            Assert.Equal(
                KeyboardNavigationMode.Cycle,
                KeyboardNavigation.GetTabNavigation(shutdownDialog));
            Assert.True(continueEditingButton.IsKeyboardFocusWithin);

            AutomationPeer continueEditingPeer =
                ControlAutomationPeer.CreatePeerForElement(
                    continueEditingButton);
            IInvokeProvider continueEditingAction =
                Assert.IsAssignableFrom<IInvokeProvider>(continueEditingPeer);
            continueEditingAction.Invoke();
            Dispatcher.UIThread.RunJobs();

            Assert.False(shutdownOverlay.IsVisible);
            Assert.True(productInteractionSurface.IsEnabled);
            Assert.True(appearanceButton.IsEnabled);
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
