using System;
using System.Collections.Generic;

using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Controls.Chrome;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;

using FluentIcons.Avalonia;
using FluentIcons.Common;

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
    public void AppearanceSettingsStayUnavailableWhileWorkspaceModalIsVisible()
    {
        PlannerWorkspaceViewModel workspace = PlannerWorkspaceTestFactory.CreateWorkspace();
        ProductShellViewModel shell = PlannerWorkspaceTestFactory.CreateShell(workspace);
        MainWindow hostWindow = new MainWindow(shell, ProductAppearanceTestFactory.CreateViewModel());

        try
        {
            hostWindow.Show();
            Dispatcher.UIThread.RunJobs();
            Button appearanceButton = findRequiredControl<Button>(hostWindow, "AppearanceButton");

            Assert.True(appearanceButton.IsEnabled);

            workspace.BeginAddPersonalScheduleCommand.Execute(null);
            Dispatcher.UIThread.RunJobs();

            Assert.True(workspace.IsPersonalScheduleEditorVisible);
            Assert.False(appearanceButton.IsEnabled);

            workspace.CancelPersonalScheduleEditCommand.Execute(null);
            Dispatcher.UIThread.RunJobs();

            Assert.False(workspace.IsPersonalScheduleEditorVisible);
            Assert.True(appearanceButton.IsEnabled);
        }
        finally
        {
            hostWindow.Close();
        }
    }

    [AvaloniaFact]
    public void ProductTitleBarUsesOneAccessibleCaptionControlSystem()
    {
        PlannerWorkspaceViewModel workspace = PlannerWorkspaceTestFactory.CreateWorkspace();
        ProductShellViewModel shell = PlannerWorkspaceTestFactory.CreateShell(workspace);
        MainWindow hostWindow = new MainWindow(shell, ProductAppearanceTestFactory.CreateViewModel());

        try
        {
            hostWindow.Show();

            Border titleBar = findRequiredControl<Border>(hostWindow, "ProductTitleBar");
            Button appearanceButton = findRequiredControl<Button>(hostWindow, "AppearanceButton");
            StackPanel captionButtons = findRequiredControl<StackPanel>(hostWindow, "ProductCaptionButtons");
            Button minimizeButton = findRequiredControl<Button>(hostWindow, "WindowMinimizeButton");
            Button maximizeRestoreButton = findRequiredControl<Button>(hostWindow, "WindowMaximizeRestoreButton");
            Button closeButton = findRequiredControl<Button>(hostWindow, "WindowCloseButton");
            FluentIcon maximizeRestoreIcon = findRequiredControl<FluentIcon>(hostWindow, "WindowMaximizeRestoreIcon");

            Assert.Equal(
                WindowDecorationsElementRole.TitleBar,
                WindowDecorationProperties.GetElementRole(titleBar));
            Assert.Equal(
                WindowDecorationsElementRole.User,
                WindowDecorationProperties.GetElementRole(appearanceButton));
            Assert.True(appearanceButton.IsHitTestVisible);
            Assert.True(appearanceButton.Focusable);
            Assert.NotNull(appearanceButton.Flyout);

            EWindowChromePlatform platform = WindowChromeLayoutPolicy.FindCurrentPlatform();
            WindowDecorations expectedDecorations = WindowChromeLayoutPolicy.FindWindowDecorations(platform);
            WindowChromeInsets insets = WindowChromeLayoutPolicy.FindTitleBarInsets(platform);
            Assert.Equal(expectedDecorations, hostWindow.WindowDecorations);
            Assert.Equal(insets.Left, titleBar.Padding.Left);
            Assert.Equal(insets.Right, titleBar.Padding.Right);
            Assert.Equal(platform == EWindowChromePlatform.Windows, captionButtons.IsVisible);

            assertCaptionButton(
                minimizeButton,
                "WindowMinimizeButton",
                "최소화",
                WindowDecorationsElementRole.MinimizeButton);
            assertCaptionButton(
                maximizeRestoreButton,
                "WindowMaximizeRestoreButton",
                "최대화",
                WindowDecorationsElementRole.MaximizeButton);
            assertCaptionButton(
                closeButton,
                "WindowCloseButton",
                "닫기",
                WindowDecorationsElementRole.CloseButton);
            (string Name, WindowDecorationsElementRole Role)[] resizeGrips =
            {
                ("WindowResizeNorth", WindowDecorationsElementRole.ResizeN),
                ("WindowResizeSouth", WindowDecorationsElementRole.ResizeS),
                ("WindowResizeEast", WindowDecorationsElementRole.ResizeE),
                ("WindowResizeWest", WindowDecorationsElementRole.ResizeW),
                ("WindowResizeNorthEast", WindowDecorationsElementRole.ResizeNE),
                ("WindowResizeNorthWest", WindowDecorationsElementRole.ResizeNW),
                ("WindowResizeSouthEast", WindowDecorationsElementRole.ResizeSE),
                ("WindowResizeSouthWest", WindowDecorationsElementRole.ResizeSW),
            };
            foreach ((string name, WindowDecorationsElementRole role)
                in resizeGrips)
            {
                Border resizeGrip = findRequiredControl<Border>(hostWindow, name);
                Assert.Equal(platform == EWindowChromePlatform.Windows, resizeGrip.IsVisible);
                Assert.Equal(AccessibilityView.Raw, AutomationProperties.GetAccessibilityView(resizeGrip));
                Assert.Equal(role, WindowDecorationProperties.GetElementRole(resizeGrip));
            }

            if (platform == EWindowChromePlatform.Windows)
            {
                AutomationPeer maximizeRestorePeer = ControlAutomationPeer.CreatePeerForElement(maximizeRestoreButton);
                List<AutomationPropertyChangedEventArgs> propertyChanges = new List<AutomationPropertyChangedEventArgs>();
                maximizeRestorePeer.PropertyChanged += (object? senderOrNull, AutomationPropertyChangedEventArgs eventArgs) => propertyChanges.Add(eventArgs);

                maximizeRestoreButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Assert.Equal(WindowState.Maximized, hostWindow.WindowState);
                Assert.Equal("복원", AutomationProperties.GetName(maximizeRestoreButton));
                Assert.Equal("복원", maximizeRestorePeer.GetName());
                Assert.Equal("복원", maximizeRestorePeer.GetHelpText());
                Assert.Equal(Icon.SquareMultiple, maximizeRestoreIcon.Icon);
                assertAutomationPropertyChange(
                    propertyChanges,
                    AutomationElementIdentifiers.NameProperty,
                    "최대화",
                    "복원");
                assertAutomationPropertyChange(
                    propertyChanges,
                    AutomationElementIdentifiers.HelpTextProperty,
                    "최대화",
                    "복원");

                propertyChanges.Clear();
                maximizeRestoreButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Assert.Equal(WindowState.Normal, hostWindow.WindowState);
                Assert.Equal("최대화", AutomationProperties.GetName(maximizeRestoreButton));
                Assert.Equal("최대화", maximizeRestorePeer.GetName());
                Assert.Equal("최대화", maximizeRestorePeer.GetHelpText());
                Assert.Equal(Icon.Square, maximizeRestoreIcon.Icon);
                assertAutomationPropertyChange(
                    propertyChanges,
                    AutomationElementIdentifiers.NameProperty,
                    "복원",
                    "최대화");
                assertAutomationPropertyChange(
                    propertyChanges,
                    AutomationElementIdentifiers.HelpTextProperty,
                    "복원",
                    "최대화");

                minimizeButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Assert.Equal(WindowState.Minimized, hostWindow.WindowState);
                hostWindow.WindowState = WindowState.Normal;
            }
        }
        finally
        {
            hostWindow.Close();
        }
    }

    private static void assertCaptionButton(
        Button button,
        string expectedAutomationId,
        string expectedName,
        WindowDecorationsElementRole expectedRole)
    {
        Assert.Equal(expectedAutomationId, AutomationProperties.GetAutomationId(button));
        Assert.Equal(expectedName, AutomationProperties.GetName(button));
        Assert.Equal(expectedRole, WindowDecorationProperties.GetElementRole(button));
        AutomationPeer peer = ControlAutomationPeer.CreatePeerForElement(button);
        Assert.IsType<ButtonAutomationPeer>(peer);
        Assert.IsAssignableFrom<IInvokeProvider>(peer);
        Assert.Equal(expectedName, peer.GetName());
    }

    private static void assertAutomationPropertyChange(
        IEnumerable<AutomationPropertyChangedEventArgs> propertyChanges,
        AutomationProperty property,
        string expectedOldValue,
        string expectedNewValue)
    {
        AutomationPropertyChangedEventArgs propertyChange = Assert.Single(
            propertyChanges,
            candidate => candidate.Property == property);
        Assert.Equal(expectedOldValue, Assert.IsType<string>(propertyChange.OldValue));
        Assert.Equal(expectedNewValue, Assert.IsType<string>(propertyChange.NewValue));
    }

    private static TControl findRequiredControl<TControl>(Control root, string controlName)
        where TControl : Control
    {
        TControl? controlOrNull = root.FindControl<TControl>(controlName);
        Assert.NotNull(controlOrNull);
        if (controlOrNull == null)
        {
            throw new InvalidOperationException("The required control could not be resolved: " + controlName);
        }

        return controlOrNull;
    }
}
