using System;
using System.Linq;
using System.Threading.Tasks;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;

using TimetableGenerator.Desktop.Presentation.Models;
using TimetableGenerator.Desktop.Presentation.ViewModels;
using TimetableGenerator.Desktop.Tests.Presentation.Appearance;
using TimetableGenerator.Desktop.Tests.Presentation.Catalog;
using TimetableGenerator.Desktop.Views;
using TimetableGenerator.Domain.Scheduling;

using Xunit;

namespace TimetableGenerator.Desktop.Tests;

public sealed class CompactModalAndScheduleListLayoutTests
{
    private const double COMPACT_HEIGHT = 640.0;
    private const double COMPACT_WIDTH = 695.0;
    private const double GEOMETRY_TOLERANCE = 0.5;
    private const double MINIMUM_PRODUCT_WINDOW_WIDTH = 900.0;

    [AvaloniaFact]
    public void CourseChoiceEditorKeepsActionsAndFocusReachableAtCompactSize()
    {
        PlannerWorkspaceViewModel workspace = PlannerWorkspaceTestFactory.CreateWorkspace(CatalogProjectionTestFixture.CreateDocumentWithScheduledAlternativeCourse());
        workspace.ActivePlan = workspace.Plans[1];
        workspace.SearchText = "프로그래밍";
        ProductWorkspaceHostView host = new ProductWorkspaceHostView();
        host.DataContext = workspace;
        Window window = createCompactWindow(host);

        try
        {
            window.Show();
            Dispatcher.UIThread.RunJobs();

            CourseSearchItem course = Assert.Single(workspace.VisibleCourses);
            Button invokingAction = host.GetVisualDescendants()
                .OfType<Button>()
                .Single(
                    candidate => ReferenceEquals(
                        candidate.Command,
                        workspace.AddCourseCommand)
                    && ReferenceEquals(candidate.CommandParameter, course));
            Assert.True(invokingAction.Focus());
            invokingAction.Command?.Execute(invokingAction.CommandParameter);
            Dispatcher.UIThread.RunJobs();

            Border dialog = findRequiredControl<Border>(host, "CourseChoiceEditorDialog");
            CourseChoiceEditorView editor = findRequiredControl<CourseChoiceEditorView>(
                host,
                "CourseChoiceEditor");
            ScrollViewer editorScrollViewer = editor.GetVisualDescendants()
                .OfType<ScrollViewer>()
                .Single(candidate => Grid.GetRow(candidate) == 2);
            Button cancelButton = editor.GetVisualDescendants()
                .OfType<Button>()
                .Single(candidate => candidate.Content as string == "취소");
            Button saveButton = findRequiredControl<Button>(editor, "SaveCourseChoiceButton");
            Border[] offeringRows = editor.GetVisualDescendants()
                .OfType<Border>()
                .Where(
                    candidate => candidate.Classes.Contains(
                        "course-choice-offering"))
                .ToArray();

            assertDialogFitsWindow(dialog, window);
            assertControlFitsDialog(cancelButton, dialog);
            assertControlFitsDialog(saveButton, dialog);
            Assert.True(cancelButton.IsEffectivelyVisible);
            Assert.True(saveButton.IsEffectivelyVisible);
            Assert.NotEmpty(offeringRows);
            Assert.DoesNotContain(
                editor.GetVisualDescendants().OfType<TextBlock>(),
                candidate => candidate.Text
                    == "선호는 먼저 추천하고, 가능은 충돌할 때 사용합니다.");
            foreach (Border offeringRow in offeringRows)
            {
                StackPanel information = offeringRow.GetVisualDescendants()
                    .OfType<StackPanel>()
                    .Single(
                        candidate => candidate.Classes.Contains(
                            "course-choice-offering-info"));
                StackPanel actions = offeringRow.GetVisualDescendants()
                    .OfType<StackPanel>()
                    .Single(
                        candidate => candidate.Classes.Contains(
                            "course-choice-offering-actions"));

                Assert.True(
                    offeringRow.Bounds.Height >= 56.0 - GEOMETRY_TOLERANCE);
                Assert.InRange(Math.Abs(information.Bounds.Height - 36.0), 0.0, GEOMETRY_TOLERANCE);
                Assert.InRange(Math.Abs(actions.Bounds.Height - 36.0), 0.0, GEOMETRY_TOLERANCE);
                assertVerticallyCentered(information, offeringRow, offeringRow);
                assertVerticallyCentered(actions, offeringRow, offeringRow);
            }

            Assert.True(
                editorScrollViewer.Extent.Width
                <= editorScrollViewer.Viewport.Width + GEOMETRY_TOLERANCE);

            window.KeyPress(Key.Escape, RawInputModifiers.None, PhysicalKey.Escape, string.Empty);
            Dispatcher.UIThread.RunJobs();

            Assert.False(workspace.IsCourseChoiceEditorVisible);
            Assert.True(invokingAction.IsKeyboardFocusWithin);
        }
        finally
        {
            window.Close();
            workspace.Dispose();
        }
    }

    [AvaloniaFact]
    public void PersonalScheduleEditorKeepsScrollableFieldsAndActionsReachableAtCompactSize()
    {
        PlannerWorkspaceViewModel workspace = PlannerWorkspaceTestFactory.CreateWorkspace();
        MainWindow window = new MainWindow(
            PlannerWorkspaceTestFactory.CreateShell(workspace),
            ProductAppearanceTestFactory.CreateViewModel());
        window.Width = MINIMUM_PRODUCT_WINDOW_WIDTH;
        window.Height = COMPACT_HEIGHT;

        try
        {
            window.Show();
            Dispatcher.UIThread.RunJobs();
            ProductWorkspaceHostView host = window.GetVisualDescendants().OfType<ProductWorkspaceHostView>().Single();

            Button invokingAction = host.GetVisualDescendants()
                .OfType<Button>()
                .Single(
                    candidate => candidate.Name
                        == "WorkspaceAddPersonalScheduleButton");
            Assert.True(invokingAction.Focus());
            invokingAction.Command?.Execute(null);
            Dispatcher.UIThread.RunJobs();

            Border dialog = findRequiredControl<Border>(host, "PersonalScheduleEditorDialog");
            PersonalScheduleEditorView editor =
                findRequiredControl<PersonalScheduleEditorView>(
                    host,
                    "PersonalScheduleEditor");
            ScrollViewer editorScrollViewer = editor.GetVisualDescendants()
                .OfType<ScrollViewer>()
                .Single(candidate => Grid.GetRow(candidate) == 2);
            TextBox nameInput = findRequiredControl<TextBox>(editor, "PersonalScheduleNameInput");
            TextBox locationInput = findRequiredControl<TextBox>(editor, "PersonalScheduleLocationInput");
            Button cancelButton = findRequiredControl<Button>(editor, "CancelPersonalScheduleEditButton");
            Button saveButton = findRequiredControl<Button>(editor, "SavePersonalScheduleButton");

            assertDialogFitsWindow(dialog, window);
            assertControlFitsDialog(cancelButton, dialog);
            assertControlFitsDialog(saveButton, dialog);
            Assert.True(nameInput.IsKeyboardFocusWithin);
            Assert.True(cancelButton.IsEffectivelyVisible);
            Assert.True(saveButton.IsEffectivelyVisible);
            Assert.True(editorScrollViewer.Extent.Height > editorScrollViewer.Viewport.Height);
            Assert.True(
                editorScrollViewer.Extent.Width
                <= editorScrollViewer.Viewport.Width + GEOMETRY_TOLERANCE);

            editorScrollViewer.ScrollToEnd();
            Dispatcher.UIThread.RunJobs();
            assertControlFitsViewport(locationInput, editorScrollViewer);

            window.KeyPress(Key.Escape, RawInputModifiers.None, PhysicalKey.Escape, string.Empty);
            Dispatcher.UIThread.RunJobs();

            Assert.False(workspace.IsPersonalScheduleOverlayVisible);
            Assert.True(invokingAction.IsKeyboardFocusWithin);
        }
        finally
        {
            window.Close();
            workspace.Dispose();
        }
    }

    [AvaloniaFact]
    public async Task SingleOccurrenceListRowsKeepTitleScheduleAndMetadataCenteredAsync()
    {
        PlannerWorkspaceViewModel workspace = PlannerWorkspaceTestFactory.CreateWorkspace();
        workspace.ActivePlan = workspace.Plans[1];
        addPersonalSchedule(
            workspace,
            "장소 없는 일정",
            EDay.Monday,
            new ScheduleTime(12, 0),
            new ScheduleTime(13, 0),
            null);
        addPersonalSchedule(
            workspace,
            "장소 있는 일정",
            EDay.Tuesday,
            new ScheduleTime(14, 0),
            new ScheduleTime(15, 0),
            "학생회관");
        await workspace.RecommendationRefreshTask;
        ScheduleWorkspaceView workspaceView = new ScheduleWorkspaceView();
        workspaceView.DataContext = workspace;
        Window window = new Window();
        window.Width = 900.0;
        window.Height = COMPACT_HEIGHT;
        window.Content = workspaceView;

        try
        {
            window.Show();
            Dispatcher.UIThread.RunJobs();
            workspaceView.ToggleSchedulePresentationCommand.Execute(null);
            Dispatcher.UIThread.RunJobs();

            ListBox list = findRequiredControl<ListBox>(workspaceView, "ScheduleListItems");
            ListBoxItem[] renderedGroups = list.GetVisualDescendants().OfType<ListBoxItem>().ToArray();
            Assert.Equal(2, renderedGroups.Length);

            foreach (ListBoxItem renderedGroup in renderedGroups)
            {
                ScheduleListGroup group = Assert.IsType<ScheduleListGroup>(renderedGroup.DataContext);
                ScheduleListOccurrence occurrence = Assert.Single(group.Occurrences);
                TextBlock title = renderedGroup.GetVisualDescendants()
                    .OfType<TextBlock>()
                    .Single(candidate => candidate.Text == group.TitleDisplayText);
                TextBlock schedule = renderedGroup.GetVisualDescendants()
                    .OfType<TextBlock>()
                    .Single(candidate => candidate.Text == occurrence.ScheduleDisplayText);
                Grid occurrenceRow = schedule.GetVisualAncestors()
                    .OfType<Grid>()
                    .First(candidate => candidate.ColumnDefinitions.Count == 2);

                assertVerticallyCentered(title, occurrenceRow, renderedGroup);
                assertVerticallyCentered(schedule, occurrenceRow, renderedGroup);

                TextBlock metadata = renderedGroup.GetVisualDescendants()
                    .OfType<TextBlock>()
                    .Single(
                        candidate => Grid.GetColumn(candidate) == 1
                            && candidate.Classes.Contains("caption"));
                Assert.Equal(occurrence.HasMetadata, metadata.IsVisible);
                if (occurrence.HasMetadata)
                {
                    Assert.Equal(occurrence.MetadataDisplayText, metadata.Text);
                    assertVerticallyCentered(metadata, occurrenceRow, renderedGroup);
                }
            }
        }
        finally
        {
            window.Close();
            workspace.Dispose();
        }
    }

    private static void addPersonalSchedule(
        PlannerWorkspaceViewModel workspace,
        string title,
        EDay day,
        ScheduleTime start,
        ScheduleTime end,
        string? locationOrNull)
    {
        workspace.BeginAddPersonalScheduleCommand.Execute(null);
        workspace.PersonalScheduleTitleDraft = title;
        workspace.PersonalScheduleDayOptions.Single(candidate => candidate.Day == day).IsSelected = true;
        workspace.PersonalScheduleStartTimeOrNull = start;
        workspace.PersonalScheduleEndTimeOrNull = end;
        if (locationOrNull == null)
        {
            workspace.PersonalScheduleLocationDraft = string.Empty;
        }
        else
        {
            workspace.PersonalScheduleLocationDraft = locationOrNull;
        }

        workspace.SavePersonalScheduleCommand.Execute(null);
    }

    private static void assertControlFitsDialog(Control control, Border dialog)
    {
        Point? positionOrNull = control.TranslatePoint(new Point(0.0, 0.0), dialog);
        Assert.NotNull(positionOrNull);
        if (positionOrNull == null)
        {
            throw new InvalidOperationException("The modal action was not attached to its dialog.");
        }

        Point position = positionOrNull.Value;
        Assert.True(position.X >= -GEOMETRY_TOLERANCE);
        Assert.True(position.Y >= -GEOMETRY_TOLERANCE);
        Assert.True(
            position.X + control.Bounds.Width
            <= dialog.Bounds.Width + GEOMETRY_TOLERANCE);
        Assert.True(
            position.Y + control.Bounds.Height
            <= dialog.Bounds.Height + GEOMETRY_TOLERANCE);
    }

    private static void assertControlFitsViewport(Control control, ScrollViewer scrollViewer)
    {
        Point? positionOrNull = control.TranslatePoint(new Point(0.0, 0.0), scrollViewer);
        Assert.NotNull(positionOrNull);
        if (positionOrNull == null)
        {
            throw new InvalidOperationException("The editor field was not attached to its scroll viewport.");
        }

        Point position = positionOrNull.Value;
        Assert.True(position.Y >= -GEOMETRY_TOLERANCE);
        Assert.True(
            position.Y + control.Bounds.Height
            <= scrollViewer.Viewport.Height + GEOMETRY_TOLERANCE);
    }

    private static void assertDialogFitsWindow(Border dialog, Window window)
    {
        Point? positionOrNull = dialog.TranslatePoint(new Point(0.0, 0.0), window);
        Assert.NotNull(positionOrNull);
        if (positionOrNull == null)
        {
            throw new InvalidOperationException("The modal dialog was not attached to its window.");
        }

        Point position = positionOrNull.Value;
        Assert.True(position.X >= -GEOMETRY_TOLERANCE);
        Assert.True(position.Y >= -GEOMETRY_TOLERANCE);
        Assert.True(
            position.X + dialog.Bounds.Width
            <= window.ClientSize.Width + GEOMETRY_TOLERANCE);
        Assert.True(
            position.Y + dialog.Bounds.Height
            <= window.ClientSize.Height + GEOMETRY_TOLERANCE);
    }

    private static void assertVerticallyCentered(Control content, Control row, Control coordinateSpace)
    {
        Point? contentPositionOrNull = content.TranslatePoint(new Point(0.0, 0.0), coordinateSpace);
        Point? rowPositionOrNull = row.TranslatePoint(new Point(0.0, 0.0), coordinateSpace);
        Assert.NotNull(contentPositionOrNull);
        Assert.NotNull(rowPositionOrNull);
        if (contentPositionOrNull == null || rowPositionOrNull == null)
        {
            throw new InvalidOperationException("The schedule list row geometry was not available.");
        }

        double contentCenter = contentPositionOrNull.Value.Y
            + (content.Bounds.Height / 2.0);
        double rowCenter = rowPositionOrNull.Value.Y + (row.Bounds.Height / 2.0);
        Assert.InRange(Math.Abs(contentCenter - rowCenter), 0.0, GEOMETRY_TOLERANCE);
    }

    private static Window createCompactWindow(Control content)
    {
        Window window = new Window();
        window.Width = COMPACT_WIDTH;
        window.Height = COMPACT_HEIGHT;
        window.Content = content;
        return window;
    }

    private static TControl findRequiredControl<TControl>(Control root, string name)
        where TControl : Control
    {
        TControl? controlOrNull = root.FindControl<TControl>(name);
        Assert.NotNull(controlOrNull);
        if (controlOrNull == null)
        {
            throw new InvalidOperationException("Missing control: " + name);
        }

        return controlOrNull;
    }
}
