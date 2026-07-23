using System;
using System.Collections.Generic;

using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input;

using TimetableGenerator.Desktop.Presentation;

namespace TimetableGenerator.Desktop.Views;

internal sealed partial class MainWindow
{
    private readonly List<DelegateCommand> mNativeMenuCommands = new List<DelegateCommand>();

    private NativeMenu? mEditNativeMenuOrNull;

    private NativeMenu? mWindowNativeMenuOrNull;

    private NativeMenuItem? mFullScreenNativeMenuItemOrNull;

    private WindowState mWindowStateBeforeFullScreen = WindowState.Normal;

    private void initializeNativeMenu()
    {
        NativeMenu fileMenu = new NativeMenu();
        fileMenu.Add(
            createNativeMenuAction(
                "Close Window",
                new KeyGesture(Key.W, KeyModifiers.Meta),
                Close));

        NativeMenu editMenu = new NativeMenu();
        editMenu.Add(
            createNativeMenuAction(
                "Undo",
                new KeyGesture(Key.Z, KeyModifiers.Meta),
                undoFocusedText,
                canUndoFocusedText));
        editMenu.Add(
            createNativeMenuAction(
                "Redo",
                new KeyGesture(Key.Z, KeyModifiers.Meta | KeyModifiers.Shift),
                redoFocusedText,
                canRedoFocusedText));
        editMenu.Add(new NativeMenuItemSeparator());
        editMenu.Add(
            createNativeMenuAction(
                "Cut",
                new KeyGesture(Key.X, KeyModifiers.Meta),
                cutFocusedText,
                canCutFocusedText));
        editMenu.Add(
            createNativeMenuAction(
                "Copy",
                new KeyGesture(Key.C, KeyModifiers.Meta),
                copyFocusedText,
                canCopyFocusedText));
        editMenu.Add(
            createNativeMenuAction(
                "Paste",
                new KeyGesture(Key.V, KeyModifiers.Meta),
                pasteFocusedText,
                canPasteFocusedText));
        editMenu.Add(
            createNativeMenuAction(
                "Select All",
                new KeyGesture(Key.A, KeyModifiers.Meta),
                selectAllFocusedText,
                canSelectAllFocusedText));

        NativeMenu windowMenu = new NativeMenu();
        windowMenu.Add(
            createNativeMenuAction(
                "Minimize",
                new KeyGesture(Key.M, KeyModifiers.Meta),
                minimizeWindow,
                canMinimizeWindow));
        windowMenu.Add(
            createNativeMenuAction(
                "Zoom",
                null,
                toggleWindowZoom,
                canZoomWindow));
        windowMenu.Add(new NativeMenuItemSeparator());
        NativeMenuItem fullScreenMenuItem = createNativeMenuAction(
            "Enter Full Screen",
            new KeyGesture(Key.F, KeyModifiers.Control | KeyModifiers.Meta),
            toggleFullScreen,
            canToggleFullScreen);
        windowMenu.Add(fullScreenMenuItem);
        windowMenu.Add(new NativeMenuItemSeparator());
        windowMenu.Add(
            createNativeMenuAction(
                "Bring All to Front",
                null,
                bringAllWindowsToFront));

        NativeMenu nativeMenu = new NativeMenu();
        NativeMenuItem fileMenuItem = new NativeMenuItem("File");
        fileMenuItem.Menu = fileMenu;
        nativeMenu.Add(fileMenuItem);
        NativeMenuItem editMenuItem = new NativeMenuItem("Edit");
        editMenuItem.Menu = editMenu;
        nativeMenu.Add(editMenuItem);
        NativeMenuItem windowMenuItem = new NativeMenuItem("Window");
        windowMenuItem.Menu = windowMenu;
        nativeMenu.Add(windowMenuItem);

        mEditNativeMenuOrNull = editMenu;
        mWindowNativeMenuOrNull = windowMenu;
        mFullScreenNativeMenuItemOrNull = fullScreenMenuItem;
        editMenu.NeedsUpdate += onNativeMenuNeedsUpdate;
        windowMenu.NeedsUpdate += onNativeMenuNeedsUpdate;
        NativeMenu.SetMenu(this, nativeMenu);
        synchronizeNativeMenuCommandState();
    }

    private void disposeNativeMenu()
    {
        if (mEditNativeMenuOrNull != null)
        {
            mEditNativeMenuOrNull.NeedsUpdate -= onNativeMenuNeedsUpdate;
            mEditNativeMenuOrNull = null;
        }

        if (mWindowNativeMenuOrNull != null)
        {
            mWindowNativeMenuOrNull.NeedsUpdate -= onNativeMenuNeedsUpdate;
            mWindowNativeMenuOrNull = null;
        }

        mFullScreenNativeMenuItemOrNull = null;
        NativeMenu.SetMenu(this, null);
        mNativeMenuCommands.Clear();
    }

    private NativeMenuItem createNativeMenuAction(
        string header,
        KeyGesture? gestureOrNull,
        Action execute,
        Func<bool>? canExecuteOrNull = null)
    {
        DelegateCommand command = new DelegateCommand(execute, canExecuteOrNull);
        mNativeMenuCommands.Add(command);
        NativeMenuItem menuItem = new NativeMenuItem(header);
        menuItem.Command = command;
        menuItem.Gesture = gestureOrNull;
        return menuItem;
    }

    private void onNativeMenuNeedsUpdate(object? senderOrNull, EventArgs eventArgs)
    {
        synchronizeNativeMenuCommandState();
    }

    private void synchronizeNativeMenuCommandState()
    {
        if (mFullScreenNativeMenuItemOrNull != null)
        {
            mFullScreenNativeMenuItemOrNull.Header = WindowState == WindowState.FullScreen
                ? "Exit Full Screen"
                : "Enter Full Screen";
        }

        foreach (DelegateCommand command in mNativeMenuCommands)
        {
            command.NotifyCanExecuteChanged();
        }
    }

    private TextBox? findFocusedTextBoxOrNull()
    {
        return FocusManager?.GetFocusedElement() as TextBox;
    }

    private SelectableTextBlock? findFocusedSelectableTextBlockOrNull()
    {
        return FocusManager?.GetFocusedElement() as SelectableTextBlock;
    }

    private void undoFocusedText()
    {
        findFocusedTextBoxOrNull()?.Undo();
    }

    private bool canUndoFocusedText()
    {
        return findFocusedTextBoxOrNull()?.CanUndo == true;
    }

    private void redoFocusedText()
    {
        findFocusedTextBoxOrNull()?.Redo();
    }

    private bool canRedoFocusedText()
    {
        return findFocusedTextBoxOrNull()?.CanRedo == true;
    }

    private void cutFocusedText()
    {
        findFocusedTextBoxOrNull()?.Cut();
    }

    private bool canCutFocusedText()
    {
        return findFocusedTextBoxOrNull()?.CanCut == true;
    }

    private void copyFocusedText()
    {
        TextBox? textBoxOrNull = findFocusedTextBoxOrNull();
        if (textBoxOrNull != null)
        {
            textBoxOrNull.Copy();
            return;
        }

        findFocusedSelectableTextBlockOrNull()?.Copy();
    }

    private bool canCopyFocusedText()
    {
        TextBox? textBoxOrNull = findFocusedTextBoxOrNull();
        if (textBoxOrNull != null)
        {
            return textBoxOrNull.CanCopy;
        }

        return findFocusedSelectableTextBlockOrNull()?.CanCopy == true;
    }

    private void pasteFocusedText()
    {
        findFocusedTextBoxOrNull()?.Paste();
    }

    private bool canPasteFocusedText()
    {
        return findFocusedTextBoxOrNull()?.CanPaste == true;
    }

    private void selectAllFocusedText()
    {
        TextBox? textBoxOrNull = findFocusedTextBoxOrNull();
        if (textBoxOrNull != null)
        {
            textBoxOrNull.SelectAll();
            return;
        }

        findFocusedSelectableTextBlockOrNull()?.SelectAll();
    }

    private bool canSelectAllFocusedText()
    {
        return findFocusedTextBoxOrNull() != null
            || findFocusedSelectableTextBlockOrNull() != null;
    }

    private void minimizeWindow()
    {
        WindowState = WindowState.Minimized;
        synchronizeNativeMenuCommandState();
    }

    private bool canMinimizeWindow()
    {
        return CanMinimize && WindowState != WindowState.Minimized;
    }

    private void toggleWindowZoom()
    {
        WindowState = WindowState == WindowState.Maximized
            ? WindowState.Normal
            : WindowState.Maximized;
        synchronizeNativeMenuCommandState();
    }

    private bool canZoomWindow()
    {
        return CanMaximize && WindowState != WindowState.FullScreen;
    }

    private void recordWindowStateBeforeFullScreen(
        WindowState previousWindowState,
        WindowState currentWindowState)
    {
        if (previousWindowState != WindowState.FullScreen
            && currentWindowState == WindowState.FullScreen)
        {
            mWindowStateBeforeFullScreen = previousWindowState;
        }
    }

    private void toggleFullScreen()
    {
        if (WindowState == WindowState.FullScreen)
        {
            WindowState = mWindowStateBeforeFullScreen;
        }
        else
        {
            mWindowStateBeforeFullScreen = WindowState;
            WindowState = WindowState.FullScreen;
        }

        synchronizeNativeMenuCommandState();
    }

    private bool canToggleFullScreen()
    {
        return CanResize;
    }

    private void bringAllWindowsToFront()
    {
        IClassicDesktopStyleApplicationLifetime? desktopLifetimeOrNull =
            Avalonia.Application.Current?.ApplicationLifetime
                as IClassicDesktopStyleApplicationLifetime;
        if (desktopLifetimeOrNull != null)
        {
            foreach (Window window in desktopLifetimeOrNull.Windows)
            {
                if (window.IsVisible)
                {
                    window.Activate();
                }
            }
        }

        Activate();
    }
}
