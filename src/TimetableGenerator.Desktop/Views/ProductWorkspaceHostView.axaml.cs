using System;
using System.ComponentModel;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;

using TimetableGenerator.Desktop.Presentation.ViewModels;

namespace TimetableGenerator.Desktop.Views;

internal sealed partial class ProductWorkspaceHostView : UserControl
{
    private PlannerWorkspaceViewModel? mWorkspaceOrNull;

    public ProductWorkspaceHostView()
    {
        AvaloniaXamlLoader.Load(this);
        DataContextChanged += onDataContextChanged;
        DetachedFromVisualTree += onDetachedFromVisualTree;
    }

    private void onDataContextChanged(object? senderOrNull, EventArgs eventArgs)
    {
        if (mWorkspaceOrNull != null)
        {
            mWorkspaceOrNull.PropertyChanged -= onWorkspacePropertyChanged;
        }

        mWorkspaceOrNull = DataContext as PlannerWorkspaceViewModel;
        if (mWorkspaceOrNull != null)
        {
            mWorkspaceOrNull.PropertyChanged += onWorkspacePropertyChanged;
            focusPlanEditingControlWhenRequired();
        }
    }

    private void onWorkspacePropertyChanged(
        object? senderOrNull,
        PropertyChangedEventArgs eventArgs)
    {
        if (eventArgs.PropertyName == nameof(PlannerWorkspaceViewModel.IsRenamingPlan))
        {
            focusPlanEditingControlWhenRequired();
        }
        else if (eventArgs.PropertyName
            == nameof(PlannerWorkspaceViewModel.IsDeletePlanConfirmationVisible))
        {
            focusPlanEditingControlWhenRequired();
        }
    }

    private void focusPlanEditingControlWhenRequired()
    {
        if (mWorkspaceOrNull == null
            || mWorkspaceOrNull.IsPlanEditingOverlayVisible == false)
        {
            return;
        }

        Dispatcher.UIThread.Post(focusPlanEditingControl, DispatcherPriority.Input);
    }

    private void focusPlanEditingControl()
    {
        if (mWorkspaceOrNull == null)
        {
            return;
        }

        if (mWorkspaceOrNull.IsRenamingPlan)
        {
            TextBox? editorOrNull = this.FindControl<TextBox>("PlanNameEditor");
            if (editorOrNull != null)
            {
                editorOrNull.Focus();
                editorOrNull.SelectAll();
            }

            return;
        }

        if (mWorkspaceOrNull.IsDeletePlanConfirmationVisible)
        {
            Button? cancelButtonOrNull = this.FindControl<Button>(
                "CancelDeletePlanButton");
            if (cancelButtonOrNull != null)
            {
                cancelButtonOrNull.Focus();
            }
        }
    }

    private void onDetachedFromVisualTree(
        object? senderOrNull,
        VisualTreeAttachmentEventArgs eventArgs)
    {
        DataContextChanged -= onDataContextChanged;
        DetachedFromVisualTree -= onDetachedFromVisualTree;
        if (mWorkspaceOrNull != null)
        {
            mWorkspaceOrNull.PropertyChanged -= onWorkspacePropertyChanged;
            mWorkspaceOrNull = null;
        }
    }
}
