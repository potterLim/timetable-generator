using System;
using System.Linq;

using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Primitives.PopupPositioning;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;

using TimetableGenerator.Desktop.Presentation.Models;
using TimetableGenerator.Desktop.Presentation.ViewModels;
using TimetableGenerator.Domain.Catalogs;
using TimetableGenerator.Domain.Planning;
using TimetableGenerator.Domain.Scheduling;

using Xunit;

namespace TimetableGenerator.Desktop.Tests;

public sealed partial class PersonalScheduleInteractionTests
{
    private static PersonalSchedule createPersonalSchedule()
    {
        WeeklyTimeRange timeRange = new WeeklyTimeRange(EDay.Wednesday, new DailyTimeRange(new ScheduleTime(12, 20), new ScheduleTime(13, 20)));
        PersonalScheduleDetails details = new PersonalScheduleDetails(null, null, new PersonalScheduleLocation("학생회관"));
        return new PersonalSchedule(PersonalScheduleId.CreateNew(), new PersonalScheduleTitle("점심 약속"), new WeeklyTimeRange[] { timeRange }, details);
    }

    private static void addPersonalSchedule(
        PlannerWorkspaceViewModel workspace,
        string title,
        EDay day,
        ScheduleTime start,
        ScheduleTime end)
    {
        workspace.BeginAddPersonalScheduleCommand.Execute(null);
        workspace.PersonalScheduleTitleDraft = title;
        selectPersonalScheduleDay(workspace, day);
        workspace.PersonalScheduleStartTimeOrNull = start;
        workspace.PersonalScheduleEndTimeOrNull = end;
        workspace.SavePersonalScheduleCommand.Execute(null);
    }

    private static void selectPersonalScheduleDay(PlannerWorkspaceViewModel workspace, EDay day)
    {
        PersonalScheduleDayOption? matchingOptionOrNull = workspace.PersonalScheduleDayOptions.FirstOrDefault(option => option.Day == day);
        if (matchingOptionOrNull == null)
        {
            throw new ArgumentOutOfRangeException(nameof(day), day, "The personal schedule day option was not found.");
        }

        matchingOptionOrNull.IsSelected = true;
    }

    private static void assertDayOptionVisuals(
        ToggleButton option,
        ThemeVariant themeVariant,
        string backgroundResourceKey,
        string borderResourceKey,
        Thickness borderThickness)
    {
        assertBrushUsesResource(option.Background, backgroundResourceKey, themeVariant);
        assertBrushUsesResource(option.BorderBrush, borderResourceKey, themeVariant);
        Assert.Equal(borderThickness, option.BorderThickness);
    }

    private static void assertDisabledContentOpacity(ToggleButton option)
    {
        ContentPresenter presenter = option.GetVisualDescendants()
            .OfType<ContentPresenter>()
            .Single(candidate => candidate.Name == "PART_ContentPresenter");
        Assert.Equal(0.45, presenter.Opacity);
    }

    private static void assertBrushUsesResource(IBrush? actualBrushOrNull, string resourceKey, ThemeVariant themeVariant)
    {
        Avalonia.Application? applicationOrNull = Avalonia.Application.Current;
        Assert.NotNull(applicationOrNull);
        if (applicationOrNull == null)
        {
            throw new InvalidOperationException("The Avalonia test application was not initialized.");
        }

        object? resourceOrNull;
        bool hasResource = applicationOrNull.TryGetResource(resourceKey, themeVariant, out resourceOrNull);
        Assert.True(hasResource, "Missing brush resource: " + resourceKey);
        SolidColorBrush actualBrush = Assert.IsType<SolidColorBrush>(actualBrushOrNull);
        SolidColorBrush expectedBrush = Assert.IsType<SolidColorBrush>(resourceOrNull);
        Assert.Equal(expectedBrush.Color, actualBrush.Color);
    }

    private static Point findControlCenter(Window window, Control control)
    {
        Point? originOrNull = control.TranslatePoint(new Point(0.0, 0.0), window);
        Assert.NotNull(originOrNull);
        if (originOrNull == null)
        {
            throw new InvalidOperationException("The personal schedule control position could not be resolved.");
        }

        return originOrNull.Value + new Vector(control.Bounds.Width / 2.0, control.Bounds.Height / 2.0);
    }

    private static void movePointerOutsideDayOptions(Window window)
    {
        window.MouseMove(new Point(1.0, 1.0), RawInputModifiers.None);
        Dispatcher.UIThread.RunJobs();
    }

    private static TControl findRequiredControl<TControl>(Control root, string name)
        where TControl : Control
    {
        TControl? controlOrNull = root.FindControl<TControl>(name);
        Assert.NotNull(controlOrNull);
        if (controlOrNull == null)
        {
            throw new InvalidOperationException("Required control not found: " + name);
        }

        return controlOrNull;
    }

    private static void assertDetailsFlyoutFitsWindow(Button scheduleCard, PlacementMode expectedPlacement, Window window)
    {
        Flyout detailsFlyout = Assert.IsType<Flyout>(scheduleCard.Flyout);
        Assert.Equal(expectedPlacement, detailsFlyout.Placement);
        Assert.Equal(PopupPositionerConstraintAdjustment.All, detailsFlyout.PlacementConstraintAdjustment);
        detailsFlyout.ShowAt(scheduleCard);
        Dispatcher.UIThread.RunJobs();

        Control detailsContent = Assert.IsAssignableFrom<Control>(detailsFlyout.Content);
        PixelPoint windowOrigin = window.PointToScreen(default);
        PixelPoint contentOrigin = detailsContent.PointToScreen(default);
        TopLevel detailsTopLevel = Assert.IsAssignableFrom<TopLevel>(TopLevel.GetTopLevel(detailsContent));

        double windowRight = windowOrigin.X + (window.ClientSize.Width * window.RenderScaling);
        double contentRight = contentOrigin.X + (detailsContent.Bounds.Width * detailsTopLevel.RenderScaling);
        Assert.InRange((double)contentOrigin.X, windowOrigin.X, windowRight);
        Assert.InRange(contentRight, windowOrigin.X, windowRight);

        detailsFlyout.Hide();
        Dispatcher.UIThread.RunJobs();
    }

    private static string getTextOrEmpty(TextBlock textBlock)
    {
        if (textBlock.Text == null)
        {
            return string.Empty;
        }
        else
        {
            return textBlock.Text;
        }
    }

    private static bool hasAutomationIdPrefix(Control control, string automationIdPrefix)
    {
        string? automationIdOrNull = AutomationProperties.GetAutomationId(control);
        return automationIdOrNull != null && automationIdOrNull.StartsWith(automationIdPrefix, StringComparison.Ordinal);
    }

    private static ScheduleBoardPresentation createScheduleBoardPresentation(ScheduleRecommendation schedule)
    {
        return new ScheduleBoardPresentation(schedule, new PlanName("테스트 계획"), new InstitutionName("한동대학교"), AcademicTerm.Parse("2026-2"));
    }
}
