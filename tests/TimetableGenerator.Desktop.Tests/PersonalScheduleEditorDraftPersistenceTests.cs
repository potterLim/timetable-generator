using System;
using System.Linq;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;

using TimetableGenerator.Desktop.Presentation.ViewModels;
using TimetableGenerator.Desktop.Views;
using TimetableGenerator.Domain.Planning;
using TimetableGenerator.Domain.Scheduling;

using Xunit;

namespace TimetableGenerator.Desktop.Tests;

public sealed class PersonalScheduleEditorDraftPersistenceTests
{
    [AvaloniaFact]
    public void SaveCommitsVisibleOptionalDetailsBeforeCreatingTheSchedule()
    {
        PlannerWorkspaceViewModel workspace = PlannerWorkspaceTestFactory.CreateWorkspace();
        ProductWorkspaceHostView host = new ProductWorkspaceHostView();
        host.DataContext = workspace;
        Window window = new Window();
        window.Width = 1_200.0;
        window.Height = 760.0;
        window.Content = host;

        try
        {
            window.Show();
            workspace.BeginAddPersonalScheduleCommand.Execute(null);
            workspace.PersonalScheduleTitleDraft = "개인 일정";
            workspace.PersonalScheduleDayOptions.Single(option => option.Day == EDay.Sunday).IsSelected = true;
            workspace.PersonalScheduleStartTimeOrNull = new ScheduleTime(20, 10);
            workspace.PersonalScheduleEndTimeOrNull = new ScheduleTime(21, 10);
            Dispatcher.UIThread.RunJobs();

            TextBox sectionInput = findRequiredControl<TextBox>(host, "PersonalScheduleSectionInput");
            TextBox instructorInput = findRequiredControl<TextBox>(host, "PersonalScheduleInstructorInput");
            TextBox locationInput = findRequiredControl<TextBox>(host, "PersonalScheduleLocationInput");
            bindExplicitDraft(sectionInput, workspace, nameof(workspace.PersonalScheduleSectionDraft));
            bindExplicitDraft(instructorInput, workspace, nameof(workspace.PersonalScheduleInstructorDraft));
            bindExplicitDraft(locationInput, workspace, nameof(workspace.PersonalScheduleLocationDraft));
            sectionInput.Text = "01";
            instructorInput.Text = "김교수";
            locationInput.Text = "NTH 101";

            Assert.Equal(string.Empty, workspace.PersonalScheduleSectionDraft);
            Assert.Equal(string.Empty, workspace.PersonalScheduleInstructorDraft);
            Assert.Equal(string.Empty, workspace.PersonalScheduleLocationDraft);

            Button saveButton = findRequiredControl<Button>(host, "SavePersonalScheduleButton");
            Point saveButtonCenter = getCenterInWindow(saveButton, window);
            window.MouseDown(saveButtonCenter, MouseButton.Left, RawInputModifiers.None);
            window.MouseUp(saveButtonCenter, MouseButton.Left, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();

            PersonalSchedule schedule = Assert.Single(workspace.ActivePlan.Plan.PersonalSchedules);
            Assert.Equal("01", schedule.Details.SectionOrNull?.Value);
            Assert.Equal("김교수", schedule.Details.InstructorOrNull?.Value);
            Assert.Equal("NTH 101", schedule.Details.LocationOrNull?.Value);
        }
        finally
        {
            window.Close();
            workspace.Dispose();
        }
    }

    private static void bindExplicitDraft(TextBox input, PlannerWorkspaceViewModel workspace, string propertyName)
    {
        input.Bind(TextBox.TextProperty, new Binding(propertyName)
        {
            Mode = BindingMode.TwoWay,
            Source = workspace,
            UpdateSourceTrigger = UpdateSourceTrigger.Explicit,
        });
    }

    private static TControl findRequiredControl<TControl>(Control root, string name)
        where TControl : Control
    {
        return root.GetVisualDescendants()
            .OfType<TControl>()
            .Single(candidate => candidate.Name == name);
    }

    private static Point getCenterInWindow(Control control, Window window)
    {
        Point? originOrNull = control.TranslatePoint(default(Point), window);
        if (originOrNull.HasValue == false)
        {
            throw new InvalidOperationException("The control is not attached to the test window.");
        }

        return originOrNull.Value + new Vector(control.Bounds.Width / 2.0, control.Bounds.Height / 2.0);
    }
}
