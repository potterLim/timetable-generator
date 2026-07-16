using System;
using System.ComponentModel;
using System.Linq;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using Avalonia.VisualTree;

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
        KeyDown += onKeyDown;
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

    private void onKeyDown(object? senderOrNull, KeyEventArgs eventArgs)
    {
        bool isFindShortcut = eventArgs.Key == Key.F
            && (eventArgs.KeyModifiers.HasFlag(KeyModifiers.Control)
                || eventArgs.KeyModifiers.HasFlag(KeyModifiers.Meta));
        if (isFindShortcut == false)
        {
            return;
        }

        if (mWorkspaceOrNull != null)
        {
            mWorkspaceOrNull.IsCoursePaneOpen = true;
        }

        Dispatcher.UIThread.Post(focusCourseSearchBox, DispatcherPriority.Input);
        eventArgs.Handled = true;
    }

    private void focusCourseSearchBox()
    {
        TextBox? searchBoxOrNull = this.GetVisualDescendants()
            .OfType<TextBox>()
            .FirstOrDefault(
                static candidate => candidate.Name == "CourseSearchBox");
        searchBoxOrNull?.Focus();
    }

    private void onDetachedFromVisualTree(
        object? senderOrNull,
        VisualTreeAttachmentEventArgs eventArgs)
    {
        DataContextChanged -= onDataContextChanged;
        DetachedFromVisualTree -= onDetachedFromVisualTree;
        KeyDown -= onKeyDown;
        if (mWorkspaceOrNull != null)
        {
            mWorkspaceOrNull.PropertyChanged -= onWorkspacePropertyChanged;
            mWorkspaceOrNull = null;
        }
    }
}
