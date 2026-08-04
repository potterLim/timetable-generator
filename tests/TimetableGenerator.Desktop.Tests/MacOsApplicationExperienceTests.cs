using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;

using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;

using TimetableGenerator.Desktop.Presentation.ViewModels;
using TimetableGenerator.Desktop.Product;
using TimetableGenerator.Desktop.Tests.Presentation.Appearance;
using TimetableGenerator.Desktop.Tests.Storage;
using TimetableGenerator.Desktop.Views;

using Xunit;

namespace TimetableGenerator.Desktop.Tests;

public sealed class MacOsApplicationExperienceTests
{
    private static readonly TimeSpan TEST_OPERATION_TIMEOUT = TimeSpan.FromSeconds(5.0);

    [AvaloniaFact]
    public void ApplicationDefinesProductIdentityAndBrandedAboutMenu()
    {
        App application = Assert.IsType<App>(Avalonia.Application.Current);
        NativeMenu applicationMenu = getRequiredNativeMenu(application);
        NativeMenuItem aboutMenuItem = getRequiredMenuItem(applicationMenu, 0);

        Assert.Equal("Timetable Generator", application.Name);
        Assert.Equal("About Timetable Generator…", aboutMenuItem.Header);
    }

    [AvaloniaFact]
    public void AboutWindowPresentsProductIdentityAndVersion()
    {
        AboutWindow aboutWindow = new AboutWindow();

        try
        {
            TextBlock productVersionText = findRequiredControl<TextBlock>(aboutWindow, "ProductVersionText");
            Button closeButton = findRequiredControl<Button>(aboutWindow, "CloseAboutButton");

            Assert.Equal("About Timetable Generator", aboutWindow.Title);
            Assert.Equal("About Timetable Generator", AutomationProperties.GetName(aboutWindow));
            Assert.StartsWith("Version 1.0.3", productVersionText.Text, StringComparison.Ordinal);
            Assert.Equal("정보 창 닫기", AutomationProperties.GetName(closeButton));
            Assert.False(aboutWindow.CanResize);
            Assert.False(aboutWindow.ShowInTaskbar);
        }
        finally
        {
            aboutWindow.Close();
        }
    }

    [AvaloniaFact]
    public void MainWindowDefinesNativeMacMenuHierarchyAndGestures()
    {
        PlannerWorkspaceViewModel workspace = PlannerWorkspaceTestFactory.CreateWorkspace();
        ProductShellViewModel shell = PlannerWorkspaceTestFactory.CreateShell(workspace);
        MainWindow hostWindow = new MainWindow(shell, ProductAppearanceTestFactory.CreateViewModel());

        try
        {
            NativeMenu nativeMenu = getRequiredNativeMenu(hostWindow);
            NativeMenu fileMenu = getRequiredSubmenu(nativeMenu, 0, "File");
            NativeMenu editMenu = getRequiredSubmenu(nativeMenu, 1, "Edit");
            NativeMenu windowMenu = getRequiredSubmenu(nativeMenu, 2, "Window");

            assertGesture(getRequiredMenuItem(fileMenu, 0), "Close Window", Key.W, KeyModifiers.Meta);
            assertGesture(getRequiredMenuItem(editMenu, 0), "Undo", Key.Z, KeyModifiers.Meta);
            assertGesture(getRequiredMenuItem(editMenu, 1), "Redo", Key.Z, KeyModifiers.Meta | KeyModifiers.Shift);
            assertGesture(getRequiredMenuItem(editMenu, 3), "Cut", Key.X, KeyModifiers.Meta);
            assertGesture(getRequiredMenuItem(editMenu, 4), "Copy", Key.C, KeyModifiers.Meta);
            assertGesture(getRequiredMenuItem(editMenu, 5), "Paste", Key.V, KeyModifiers.Meta);
            assertGesture(getRequiredMenuItem(editMenu, 6), "Select All", Key.A, KeyModifiers.Meta);
            assertGesture(getRequiredMenuItem(windowMenu, 0), "Minimize", Key.M, KeyModifiers.Meta);
            Assert.Equal("Zoom", getRequiredMenuItem(windowMenu, 1).Header);
            assertGesture(getRequiredMenuItem(windowMenu, 3), "Enter Full Screen", Key.F, KeyModifiers.Control | KeyModifiers.Meta);
            Assert.Equal("Bring All to Front", getRequiredMenuItem(windowMenu, 5).Header);
        }
        finally
        {
            hostWindow.Close();
        }
    }

    [AvaloniaFact]
    public void NativeEditAndWindowMenuCommandsActOnFocusedContentAndWindowState()
    {
        PlannerWorkspaceViewModel workspace = PlannerWorkspaceTestFactory.CreateWorkspace();
        ProductShellViewModel shell = PlannerWorkspaceTestFactory.CreateShell(workspace);
        MainWindow hostWindow = new MainWindow(shell, ProductAppearanceTestFactory.CreateViewModel());

        try
        {
            hostWindow.Show();
            Dispatcher.UIThread.RunJobs();

            NativeMenu nativeMenu = getRequiredNativeMenu(hostWindow);
            NativeMenu editMenu = getRequiredSubmenu(nativeMenu, 1, "Edit");
            NativeMenu windowMenu = getRequiredSubmenu(nativeMenu, 2, "Window");
            TextBox searchBox = hostWindow.GetVisualDescendants()
                .OfType<TextBox>()
                .Single(candidate => candidate.Name == "CourseSearchBox");
            searchBox.Text = "COMP";
            Assert.True(searchBox.Focus());
            searchBox.SelectAll();

            ICommand cutCommand = getRequiredCommand(getRequiredMenuItem(editMenu, 3));
            Assert.True(cutCommand.CanExecute(null));
            cutCommand.Execute(null);
            Assert.Equal(string.Empty, searchBox.Text);

            searchBox.Text = "MATH";
            searchBox.SelectionStart = searchBox.Text.Length;
            searchBox.SelectionEnd = searchBox.Text.Length;
            ICommand selectAllCommand = getRequiredCommand(getRequiredMenuItem(editMenu, 6));
            Assert.True(selectAllCommand.CanExecute(null));
            selectAllCommand.Execute(null);
            Assert.Equal(0, searchBox.SelectionStart);
            Assert.Equal(searchBox.Text.Length, searchBox.SelectionEnd);

            ICommand minimizeCommand = getRequiredCommand(getRequiredMenuItem(windowMenu, 0));
            Assert.True(minimizeCommand.CanExecute(null));
            minimizeCommand.Execute(null);
            Assert.Equal(WindowState.Minimized, hostWindow.WindowState);

            hostWindow.WindowState = WindowState.Normal;
            ICommand zoomCommand = getRequiredCommand(getRequiredMenuItem(windowMenu, 1));
            Assert.True(zoomCommand.CanExecute(null));
            zoomCommand.Execute(null);
            Assert.Equal(WindowState.Maximized, hostWindow.WindowState);
            zoomCommand.Execute(null);
            Assert.Equal(WindowState.Normal, hostWindow.WindowState);

            NativeMenuItem fullScreenMenuItem = getRequiredMenuItem(windowMenu, 3);
            ICommand fullScreenCommand = getRequiredCommand(fullScreenMenuItem);
            Assert.True(fullScreenCommand.CanExecute(null));
            fullScreenCommand.Execute(null);
            Assert.Equal(WindowState.FullScreen, hostWindow.WindowState);
            Assert.Equal("Exit Full Screen", fullScreenMenuItem.Header);
            fullScreenCommand.Execute(null);
            Assert.Equal(WindowState.Normal, hostWindow.WindowState);
            Assert.Equal("Enter Full Screen", fullScreenMenuItem.Header);

            zoomCommand.Execute(null);
            Assert.Equal(WindowState.Maximized, hostWindow.WindowState);
            hostWindow.WindowState = WindowState.FullScreen;
            fullScreenCommand.Execute(null);
            Assert.Equal(WindowState.Maximized, hostWindow.WindowState);
            zoomCommand.Execute(null);
            Assert.Equal(WindowState.Normal, hostWindow.WindowState);

            zoomCommand.Execute(null);
            Assert.Equal(WindowState.Maximized, hostWindow.WindowState);
            fullScreenCommand.Execute(null);
            Assert.Equal(WindowState.FullScreen, hostWindow.WindowState);
            fullScreenCommand.Execute(null);
            Assert.Equal(WindowState.Maximized, hostWindow.WindowState);
            zoomCommand.Execute(null);
            Assert.Equal(WindowState.Normal, hostWindow.WindowState);
        }
        finally
        {
            hostWindow.Close();
        }
    }

    [AvaloniaFact]
    public async Task CloseWindowMenuWaitsForPendingAutosaveAsync()
    {
        ControlledPlanningWorkspaceStore store = new ControlledPlanningWorkspaceStore();
        ControlledSaveAttempt saveAttempt = new ControlledSaveAttempt();
        ControlledSaveAttempt followupSaveAttempt = new ControlledSaveAttempt();
        followupSaveAttempt.CompleteSuccessfully();
        store.EnqueueSaveAttempt(saveAttempt);
        store.EnqueueSaveAttempt(followupSaveAttempt);
        PlannerWorkspaceViewModel workspace = PlannerWorkspaceTestFactory.CreateWorkspace(store);
        ProductShellViewModel shell = PlannerWorkspaceTestFactory.CreateShell(workspace);
        MainWindow hostWindow = new MainWindow(shell, ProductAppearanceTestFactory.CreateViewModel());
        TaskCompletionSource closedCompletionSource = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        bool saveAttemptCompleted = false;
        hostWindow.Closed += delegate
        {
            closedCompletionSource.TrySetResult();
        };

        try
        {
            await workspace.RecommendationRefreshTask.WaitAsync(TEST_OPERATION_TIMEOUT, TestContext.Current.CancellationToken);
            hostWindow.Show();
            Dispatcher.UIThread.RunJobs();
            workspace.BeginRenamePlanCommand.Execute(null);
            workspace.PlanNameDraft = "종료 저장 검증";
            workspace.ConfirmPlanNameCommand.Execute(null);
            await saveAttempt.WaitForStartAsync().WaitAsync(TEST_OPERATION_TIMEOUT, TestContext.Current.CancellationToken);

            NativeMenu nativeMenu = getRequiredNativeMenu(hostWindow);
            NativeMenu fileMenu = getRequiredSubmenu(nativeMenu, 0, "File");
            ICommand closeCommand = getRequiredCommand(getRequiredMenuItem(fileMenu, 0));
            closeCommand.Execute(null);

            Assert.True(hostWindow.IsVisible);
            Assert.True(shell.IsShutdownInProgress);

            saveAttempt.CompleteSuccessfully();
            saveAttemptCompleted = true;
            await waitForWindowCloseAsync(closedCompletionSource.Task);

            Assert.False(hostWindow.IsVisible);
        }
        finally
        {
            if (saveAttemptCompleted == false)
            {
                saveAttempt.CompleteSuccessfully();
            }

            if (closedCompletionSource.Task.IsCompleted == false)
            {
                Dispatcher.UIThread.RunJobs();
                hostWindow.Close();
            }
        }
    }

    private static NativeMenu getRequiredNativeMenu(AvaloniaObject owner)
    {
        NativeMenu? nativeMenuOrNull = NativeMenu.GetMenu(owner);
        Assert.NotNull(nativeMenuOrNull);
        if (nativeMenuOrNull == null)
        {
            throw new InvalidOperationException("The native menu was not attached.");
        }

        return nativeMenuOrNull;
    }

    private static async Task waitForWindowCloseAsync(Task closedTask)
    {
        DateTime timeoutAt = DateTime.UtcNow + TEST_OPERATION_TIMEOUT;
        while (closedTask.IsCompleted == false && DateTime.UtcNow < timeoutAt)
        {
            Dispatcher.UIThread.RunJobs();
            await Task.Delay(10, TestContext.Current.CancellationToken);
        }

        Dispatcher.UIThread.RunJobs();
        await closedTask.WaitAsync(TimeSpan.FromMilliseconds(100.0), TestContext.Current.CancellationToken);
    }

    private static NativeMenu getRequiredSubmenu(NativeMenu parentMenu, int index, string expectedHeader)
    {
        NativeMenuItem parentItem = getRequiredMenuItem(parentMenu, index);
        Assert.Equal(expectedHeader, parentItem.Header);
        Assert.NotNull(parentItem.Menu);
        if (parentItem.Menu == null)
        {
            throw new InvalidOperationException("The native submenu was not attached.");
        }

        return parentItem.Menu;
    }

    private static NativeMenuItem getRequiredMenuItem(NativeMenu menu, int index)
    {
        return Assert.IsType<NativeMenuItem>(menu.Items[index]);
    }

    private static ICommand getRequiredCommand(NativeMenuItem menuItem)
    {
        Assert.NotNull(menuItem.Command);
        if (menuItem.Command == null)
        {
            throw new InvalidOperationException("The native menu action has no command.");
        }

        return menuItem.Command;
    }

    private static void assertGesture(NativeMenuItem menuItem, string expectedHeader, Key expectedKey, KeyModifiers expectedModifiers)
    {
        Assert.Equal(expectedHeader, menuItem.Header);
        Assert.NotNull(menuItem.Command);
        Assert.NotNull(menuItem.Gesture);
        if (menuItem.Gesture == null)
        {
            throw new InvalidOperationException("The native menu action has no gesture.");
        }

        Assert.Equal(expectedKey, menuItem.Gesture.Key);
        Assert.Equal(expectedModifiers, menuItem.Gesture.KeyModifiers);
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
