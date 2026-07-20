using System;
using System.Linq;

using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using Avalonia.VisualTree;

using TimetableGenerator.Desktop.Presentation.Models;
using TimetableGenerator.Desktop.Presentation.ViewModels;

namespace TimetableGenerator.Desktop.Views;

internal sealed partial class CourseBrowserView : UserControl
{
    private Flyout? mOpenCourseSelectionFlyoutOrNull;

    public CourseBrowserView()
    {
        AvaloniaXamlLoader.Load(this);
    }

    private void onCourseSelectionFlyoutOpened(
        object? senderOrNull,
        EventArgs eventArgs)
    {
        Flyout? selectionFlyoutOrNull = senderOrNull as Flyout;
        if (selectionFlyoutOrNull == null)
        {
            throw new ArgumentException(
                "Course selection flyout events require a flyout sender.",
                nameof(senderOrNull));
        }

        mOpenCourseSelectionFlyoutOrNull = selectionFlyoutOrNull;
        Dispatcher.UIThread.Post(
            focusFirstCourseSelectionOption,
            DispatcherPriority.Input);
    }

    private void onCourseSelectionFlyoutClosed(
        object? senderOrNull,
        EventArgs eventArgs)
    {
        if (ReferenceEquals(mOpenCourseSelectionFlyoutOrNull, senderOrNull))
        {
            mOpenCourseSelectionFlyoutOrNull = null;
        }
    }

    private void onCourseSelectionOptionClick(
        object? senderOrNull,
        RoutedEventArgs eventArgs)
    {
        Button? optionButtonOrNull = senderOrNull as Button;
        CourseSelectionOption? selectionOptionOrNull =
            optionButtonOrNull?.DataContext as CourseSelectionOption;
        if (selectionOptionOrNull == null)
        {
            throw new ArgumentException(
                "Course selection actions require a selection option.",
                nameof(senderOrNull));
        }

        PlannerWorkspaceViewModel? workspaceOrNull =
            DataContext as PlannerWorkspaceViewModel;
        if (workspaceOrNull == null)
        {
            throw new InvalidOperationException(
                "The course browser requires a planner workspace.");
        }

        mOpenCourseSelectionFlyoutOrNull?.Hide();
        workspaceOrNull.AddCourseSelectionOptionCommand.Execute(
            selectionOptionOrNull);
    }

    private void focusFirstCourseSelectionOption()
    {
        Flyout? selectionFlyoutOrNull = mOpenCourseSelectionFlyoutOrNull;
        Control? contentOrNull = selectionFlyoutOrNull?.Content as Control;
        if (contentOrNull == null)
        {
            return;
        }

        Button? firstOptionOrNull = contentOrNull
            .GetVisualDescendants()
            .OfType<Button>()
            .FirstOrDefault(
                candidate => candidate.Classes.Contains(
                    "course-selection-option"));
        firstOptionOrNull?.Focus();
    }
}
