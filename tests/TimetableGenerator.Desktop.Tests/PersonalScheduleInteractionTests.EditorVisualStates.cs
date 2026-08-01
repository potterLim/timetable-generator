using System.Linq;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;

using TimetableGenerator.Desktop.Presentation.Models;
using TimetableGenerator.Desktop.Presentation.ViewModels;
using TimetableGenerator.Desktop.Views;
using TimetableGenerator.Domain.Scheduling;

using Xunit;

namespace TimetableGenerator.Desktop.Tests;

public sealed partial class PersonalScheduleInteractionTests
{
    [AvaloniaFact]
    public void DayOptionsUseCompleteVisualStatesAcrossThemes()
    {
        PlannerWorkspaceViewModel workspace = PlannerWorkspaceTestFactory.CreateWorkspace();
        ProductWorkspaceHostView host = new ProductWorkspaceHostView();
        host.DataContext = workspace;
        Window window = new Window();
        window.Width = 900.0;
        window.Height = 760.0;
        window.Content = host;

        try
        {
            window.Show();
            workspace.BeginAddPersonalScheduleCommand.Execute(null);
            Dispatcher.UIThread.RunJobs();
            PersonalScheduleDayOption mondayOption = workspace
                .PersonalScheduleDayOptions
                .Single(candidate => candidate.Day == EDay.Monday);
            PersonalScheduleDayOption tuesdayOption = workspace
                .PersonalScheduleDayOptions
                .Single(candidate => candidate.Day == EDay.Tuesday);
            ToggleButton mondayInput = host.GetVisualDescendants()
                .OfType<ToggleButton>()
                .Single(candidate => ReferenceEquals(
                    candidate.DataContext,
                    mondayOption));
            ToggleButton tuesdayInput = host.GetVisualDescendants()
                .OfType<ToggleButton>()
                .Single(candidate => ReferenceEquals(
                    candidate.DataContext,
                    tuesdayOption));
            Button closeEditorButton = host.GetVisualDescendants()
                .OfType<Button>()
                .Single(candidate => candidate.Name
                    == "ClosePersonalScheduleEditorButton");
            ThemeVariant[] themeVariants =
            {
                ThemeVariant.Light,
                ThemeVariant.Dark,
            };

            foreach (ThemeVariant themeVariant in themeVariants)
            {
                window.RequestedThemeVariant = themeVariant;
                mondayInput.IsEnabled = true;
                tuesdayInput.IsEnabled = true;
                mondayOption.IsSelected = false;
                tuesdayOption.IsSelected = true;
                movePointerOutsideDayOptions(window);
                Dispatcher.UIThread.RunJobs();

                assertDayOptionVisuals(
                    mondayInput,
                    themeVariant,
                    "ControlSurfaceBrush",
                    "ControlBorderBrush",
                    new Thickness(1.0));
                assertDayOptionVisuals(
                    tuesdayInput,
                    themeVariant,
                    "SelectionSurfaceBrush",
                    "SelectionIndicatorBrush",
                    new Thickness(1.0));

                Point mondayCenter = findControlCenter(window, mondayInput);
                window.MouseMove(mondayCenter, RawInputModifiers.None);
                Dispatcher.UIThread.RunJobs();
                assertDayOptionVisuals(
                    mondayInput,
                    themeVariant,
                    "HoverSurfaceBrush",
                    "ControlBorderBrush",
                    new Thickness(1.0));
                window.MouseDown(mondayCenter, MouseButton.Left, RawInputModifiers.None);
                Dispatcher.UIThread.RunJobs();
                assertDayOptionVisuals(
                    mondayInput,
                    themeVariant,
                    "PressedSurfaceBrush",
                    "ControlBorderBrush",
                    new Thickness(1.0));
                window.MouseUp(mondayCenter, MouseButton.Left, RawInputModifiers.None);

                mondayOption.IsSelected = false;
                movePointerOutsideDayOptions(window);
                Assert.True(closeEditorButton.Focus(NavigationMethod.Tab));
                Assert.True(mondayInput.Focus(NavigationMethod.Tab));
                Dispatcher.UIThread.RunJobs();
                assertDayOptionVisuals(
                    mondayInput,
                    themeVariant,
                    "ControlSurfaceBrush",
                    "ProductFocusStrokeBrush",
                    new Thickness(2.0));
                mondayInput.IsEnabled = false;
                window.MouseMove(mondayCenter, RawInputModifiers.None);
                Dispatcher.UIThread.RunJobs();
                Assert.False(mondayInput.IsEffectivelyEnabled);
                assertDayOptionVisuals(
                    mondayInput,
                    themeVariant,
                    "ControlSurfaceBrush",
                    "ControlBorderBrush",
                    new Thickness(1.0));
                assertDisabledContentOpacity(mondayInput);

                Point tuesdayCenter = findControlCenter(window, tuesdayInput);
                window.MouseMove(tuesdayCenter, RawInputModifiers.None);
                Dispatcher.UIThread.RunJobs();
                assertDayOptionVisuals(
                    tuesdayInput,
                    themeVariant,
                    "SelectionHoverSurfaceBrush",
                    "SelectionIndicatorBrush",
                    new Thickness(1.0));
                window.MouseDown(tuesdayCenter, MouseButton.Left, RawInputModifiers.None);
                Dispatcher.UIThread.RunJobs();
                assertDayOptionVisuals(
                    tuesdayInput,
                    themeVariant,
                    "SelectionPressedSurfaceBrush",
                    "SelectionIndicatorBrush",
                    new Thickness(1.0));
                window.MouseUp(tuesdayCenter, MouseButton.Left, RawInputModifiers.None);

                tuesdayOption.IsSelected = true;
                movePointerOutsideDayOptions(window);
                Assert.True(closeEditorButton.Focus(NavigationMethod.Tab));
                Assert.True(tuesdayInput.Focus(NavigationMethod.Tab));
                Dispatcher.UIThread.RunJobs();
                assertDayOptionVisuals(
                    tuesdayInput,
                    themeVariant,
                    "SelectionSurfaceBrush",
                    "ProductFocusStrokeBrush",
                    new Thickness(2.0));
                tuesdayInput.IsEnabled = false;
                window.MouseMove(tuesdayCenter, RawInputModifiers.None);
                Dispatcher.UIThread.RunJobs();
                Assert.False(tuesdayInput.IsEffectivelyEnabled);
                assertDayOptionVisuals(
                    tuesdayInput,
                    themeVariant,
                    "SelectionSurfaceBrush",
                    "SelectionIndicatorBrush",
                    new Thickness(1.0));
                assertDisabledContentOpacity(tuesdayInput);
            }
        }
        finally
        {
            window.Close();
            workspace.Dispose();
        }
    }
}
