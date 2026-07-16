using Avalonia.Headless.XUnit;

using TimetableGenerator.Desktop.Presentation.Layout;
using TimetableGenerator.Desktop.Presentation.Models;
using TimetableGenerator.Desktop.Presentation.Sample;
using TimetableGenerator.Desktop.Presentation.ViewModels;

using Xunit;

namespace TimetableGenerator.Desktop.Tests;

public sealed class PlannerWorkspaceSmokeTests
{
    [AvaloniaFact]
    public void SearchAndCourseCommandsUpdateTheActivePlan()
    {
        PlannerWorkspaceViewModel workspace = PlannerSampleStateFactory.CreateWorkspace();
        int originalCourseCount = workspace.ActivePlan.ScheduledCourses.Count;

        workspace.SearchText = "파이썬";

        CourseSearchItem visibleCourse = Assert.Single(workspace.VisibleCourses);
        Assert.False(visibleCourse.IsAdded);

        workspace.AddCourseCommand.Execute(visibleCourse);

        Assert.True(visibleCourse.IsAdded);
        Assert.Equal(originalCourseCount + 1, workspace.ActivePlan.ScheduledCourses.Count);

        PlanCourseItem addedCourse = workspace.ActivePlan.ScheduledCourses[workspace.ActivePlan.ScheduledCourses.Count - 1];
        workspace.RemoveCourseCommand.Execute(addedCourse);

        Assert.False(visibleCourse.IsAdded);
        Assert.Equal(originalCourseCount, workspace.ActivePlan.ScheduledCourses.Count);
    }

    [AvaloniaFact]
    public void WorkspaceWidthSelectsResponsivePaneModes()
    {
        PlannerWorkspaceViewModel workspace = PlannerSampleStateFactory.CreateWorkspace();

        workspace.applyWorkspaceWidth(new WorkspaceWidth(1_200.0));

        Assert.Equal(EWorkspaceLayoutMode.Medium, workspace.LayoutMode);
        Assert.True(workspace.IsCoursePaneOpen);
        Assert.False(workspace.IsInspectorPaneOpen);
        Assert.True(workspace.IsInspectorPaneToggleVisible);

        workspace.applyWorkspaceWidth(new WorkspaceWidth(960.0));

        Assert.Equal(EWorkspaceLayoutMode.Compact, workspace.LayoutMode);
        Assert.False(workspace.IsCoursePaneOpen);
        Assert.False(workspace.IsInspectorPaneOpen);
        Assert.True(workspace.IsCoursePaneToggleVisible);
    }

    [AvaloniaFact]
    public void RecommendationNavigationWrapsAndChangesTheSampleSchedule()
    {
        PlannerWorkspaceViewModel workspace = PlannerSampleStateFactory.CreateWorkspace();
        ScheduleRecommendation firstRecommendation = workspace.ActiveRecommendation;

        workspace.NextRecommendationCommand.Execute(null);

        Assert.Equal("2 / 24", workspace.RecommendationSummary);
        Assert.NotSame(firstRecommendation, workspace.ActiveRecommendation);

        workspace.PreviousRecommendationCommand.Execute(null);

        Assert.Equal("1 / 24", workspace.RecommendationSummary);
        Assert.Same(firstRecommendation, workspace.ActiveRecommendation);
    }
}
