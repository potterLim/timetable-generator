using System;
using System.ComponentModel;

using Avalonia.Controls;

using TimetableGenerator.Desktop.Presentation.ViewModels;

namespace TimetableGenerator.Desktop.Views;

internal sealed partial class MainWindow
{
    private Button? mAppearanceButtonOrNull;

    private PlannerWorkspaceViewModel? mInteractionWorkspaceOrNull;

    private void initializeWorkspaceInteraction()
    {
        mAppearanceButtonOrNull = this.FindControl<Button>("AppearanceButton");
        if (mAppearanceButtonOrNull == null)
        {
            throw new InvalidOperationException(
                "The appearance button could not be resolved.");
        }

        connectWorkspaceInteraction();
    }

    private void connectWorkspaceInteraction()
    {
        PlannerWorkspaceViewModel? workspaceOrNull =
            mProductShellViewModel.WorkspaceOrNull;
        if (ReferenceEquals(mInteractionWorkspaceOrNull, workspaceOrNull))
        {
            updateAppearanceInteraction();
            return;
        }

        disconnectWorkspaceInteraction();
        mInteractionWorkspaceOrNull = workspaceOrNull;
        if (mInteractionWorkspaceOrNull != null)
        {
            mInteractionWorkspaceOrNull.PropertyChanged +=
                onInteractionWorkspacePropertyChanged;
        }

        updateAppearanceInteraction();
    }

    private void disconnectWorkspaceInteraction()
    {
        if (mInteractionWorkspaceOrNull == null)
        {
            return;
        }

        mInteractionWorkspaceOrNull.PropertyChanged -=
            onInteractionWorkspacePropertyChanged;
        mInteractionWorkspaceOrNull = null;
    }

    private void onInteractionWorkspacePropertyChanged(
        object? senderOrNull,
        PropertyChangedEventArgs eventArgs)
    {
        if (eventArgs.PropertyName
            == nameof(PlannerWorkspaceViewModel.IsWorkspaceInteractionEnabled))
        {
            updateAppearanceInteraction();
        }
    }

    private void updateAppearanceInteraction()
    {
        if (mAppearanceButtonOrNull == null)
        {
            return;
        }

        bool isWorkspaceInteractionEnabled = true;
        if (mInteractionWorkspaceOrNull != null)
        {
            isWorkspaceInteractionEnabled =
                mInteractionWorkspaceOrNull.IsWorkspaceInteractionEnabled;
        }

        bool isAppearanceInteractionEnabled =
            mProductShellViewModel.IsProductInteractionEnabled
                && isWorkspaceInteractionEnabled;
        mAppearanceButtonOrNull.IsEnabled = isAppearanceInteractionEnabled;
        if (isAppearanceInteractionEnabled == false)
        {
            mAppearanceButtonOrNull.Flyout?.Hide();
        }
    }

    private void disposeWorkspaceInteraction()
    {
        disconnectWorkspaceInteraction();
        mAppearanceButtonOrNull = null;
    }
}
