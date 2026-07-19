using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

using Avalonia.Controls;
using Avalonia.Controls.Chrome;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media.Imaging;
using Avalonia.Styling;
using Avalonia.Threading;

using TimetableGenerator.CatalogJson;
using TimetableGenerator.Desktop.Presentation.Models;
using TimetableGenerator.Desktop.Presentation.ViewModels;
using TimetableGenerator.Desktop.Presentation.Windowing;
using TimetableGenerator.Desktop.Product;
using TimetableGenerator.Desktop.Tests.Presentation.Appearance;
using TimetableGenerator.Desktop.Tests.Presentation.Catalog;
using TimetableGenerator.Desktop.Views;
using TimetableGenerator.Domain.Scheduling;

using Xunit;

namespace TimetableGenerator.Desktop.Tests;

public sealed class PlannerWorkspaceRenderTests
{
    private const double REFERENCE_WIDTH = 1_487.0;
    private const double REFERENCE_HEIGHT = 1_058.0;

    [AvaloniaFact]
    public async Task ReferenceWorkspaceRendersToPngAsync()
    {
        PlannerWorkspaceViewModel workspace = PlannerWorkspaceTestFactory.CreateWorkspace();
        await workspace.RecommendationRefreshTask;
        ProductShellViewModel shell = PlannerWorkspaceTestFactory.CreateShell(workspace);
        MainWindow window = new MainWindow(
            shell,
            ProductAppearanceTestFactory.CreateViewModel());
        window.Width = REFERENCE_WIDTH;
        window.Height = REFERENCE_HEIGHT;
        Assert.True(window.CanResize);
        EWindowChromePlatform windowChromePlatform =
            WindowChromeLayoutPolicy.FindCurrentPlatform();
        Assert.Equal(
            WindowChromeLayoutPolicy.FindWindowDecorations(
                windowChromePlatform),
            window.WindowDecorations);
        Assert.True(window.ExtendClientAreaToDecorationsHint);
        Assert.Equal(56.0, window.ExtendClientAreaTitleBarHeightHint);
        Assert.True(window.ShowInTaskbar);
        Border? productTitleBarOrNull =
            window.FindControl<Border>("ProductTitleBar");
        Assert.NotNull(productTitleBarOrNull);
        if (productTitleBarOrNull == null)
        {
            throw new InvalidOperationException(
                "The product title bar could not be resolved.");
        }

        Assert.Equal(
            WindowDecorationsElementRole.TitleBar,
            WindowDecorationProperties.GetElementRole(productTitleBarOrNull));

        try
        {
            window.Show();
            Dispatcher.UIThread.RunJobs();
            saveRenderedFrame(window, "planning-workspace-1487x1058.png");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public async Task PlanDeleteConfirmationRendersInLightAndDarkThemesAsync()
    {
        PlannerWorkspaceViewModel workspace = PlannerWorkspaceTestFactory.CreateWorkspace();
        await workspace.RecommendationRefreshTask;
        ProductShellViewModel shell = PlannerWorkspaceTestFactory.CreateShell(workspace);
        MainWindow window = new MainWindow(
            shell,
            ProductAppearanceTestFactory.CreateViewModel());
        window.Width = REFERENCE_WIDTH;
        window.Height = REFERENCE_HEIGHT;

        try
        {
            window.RequestedThemeVariant = ThemeVariant.Light;
            window.Show();
            workspace.Plans[1].CloseCommand.Execute(null);
            Dispatcher.UIThread.RunJobs();

            saveRenderedFrame(
                window,
                "plan-delete-confirmation-light-1487x1058.png");

            window.RequestedThemeVariant = ThemeVariant.Dark;
            Dispatcher.UIThread.RunJobs();
            saveRenderedFrame(
                window,
                "plan-delete-confirmation-dark-1487x1058.png");
        }
        finally
        {
            window.Close();
            workspace.Dispose();
        }
    }

    [AvaloniaFact]
    public async Task PlanClearConfirmationRendersInLightAndDarkThemesAsync()
    {
        PlannerWorkspaceViewModel workspace = PlannerWorkspaceTestFactory.CreateWorkspace();
        await workspace.RecommendationRefreshTask;
        ProductShellViewModel shell = PlannerWorkspaceTestFactory.CreateShell(workspace);
        MainWindow window = new MainWindow(
            shell,
            ProductAppearanceTestFactory.CreateViewModel());
        window.Width = REFERENCE_WIDTH;
        window.Height = REFERENCE_HEIGHT;

        try
        {
            window.RequestedThemeVariant = ThemeVariant.Light;
            window.Show();
            Assert.True(workspace.BeginClearActivePlanCommand.CanExecute(null));
            workspace.BeginClearActivePlanCommand.Execute(null);
            Dispatcher.UIThread.RunJobs();

            saveRenderedFrame(
                window,
                "plan-clear-confirmation-light-1487x1058.png");

            window.RequestedThemeVariant = ThemeVariant.Dark;
            Dispatcher.UIThread.RunJobs();
            saveRenderedFrame(
                window,
                "plan-clear-confirmation-dark-1487x1058.png");
        }
        finally
        {
            window.Close();
            workspace.Dispose();
        }
    }

    [AvaloniaFact]
    public async Task EmptyWorkspaceRendersInLightAndDarkThemesAsync()
    {
        PlannerWorkspaceViewModel workspace = PlannerWorkspaceTestFactory.CreateWorkspace();
        await workspace.RecommendationRefreshTask;
        ProductShellViewModel shell = PlannerWorkspaceTestFactory.CreateShell(workspace);
        MainWindow window = new MainWindow(
            shell,
            ProductAppearanceTestFactory.CreateViewModel());
        window.Width = REFERENCE_WIDTH;
        window.Height = REFERENCE_HEIGHT;

        try
        {
            window.RequestedThemeVariant = ThemeVariant.Light;
            window.Show();
            while (workspace.Plans.Count > 0)
            {
                workspace.Plans[0].CloseCommand.Execute(null);
                workspace.ConfirmDeletePlanCommand.Execute(null);
            }

            Dispatcher.UIThread.RunJobs();
            Assert.True(workspace.IsWorkspaceEmpty);
            saveRenderedFrame(
                window,
                "empty-workspace-light-1487x1058.png");

            window.RequestedThemeVariant = ThemeVariant.Dark;
            Dispatcher.UIThread.RunJobs();
            saveRenderedFrame(
                window,
                "empty-workspace-dark-1487x1058.png");
        }
        finally
        {
            window.Close();
            workspace.Dispose();
        }
    }

    [AvaloniaFact]
    public void CourseChoiceEditorRendersInLightAndDarkThemes()
    {
        CourseCatalogDocument document = CatalogProjectionTestFixture
            .CreateDocumentWithScheduledAlternativeCourse();
        PlannerWorkspaceViewModel workspace =
            PlannerWorkspaceTestFactory.CreateWorkspace(document);
        workspace.ActivePlan = workspace.Plans[1];
        ProductShellViewModel shell = PlannerWorkspaceTestFactory.CreateShell(
            workspace);
        MainWindow window = new MainWindow(
            shell,
            ProductAppearanceTestFactory.CreateViewModel());
        window.Width = REFERENCE_WIDTH;
        window.Height = REFERENCE_HEIGHT;

        try
        {
            window.RequestedThemeVariant = ThemeVariant.Light;
            window.Show();
            Dispatcher.UIThread.RunJobs();

            workspace.SearchText = "프로그래밍";
            CourseSearchItem programming = Assert.Single(
                workspace.VisibleCourses);
            workspace.AddCourseCommand.Execute(programming);
            CourseChoiceDraftCourseItem programmingDraft = Assert.Single(
                workspace.CourseChoiceDraftCourses);
            programmingDraft.Offerings[0].SelectPreferredCommand.Execute(null);
            workspace.AlternativeCourseSearchText = "세미나";
            CourseChoiceAlternativeSearchItem seminar = Assert.Single(
                workspace.AlternativeCourseSearchResults);
            workspace.AddAlternativeCourseCommand.Execute(seminar);
            CourseChoiceDraftCourseItem seminarDraft = workspace
                .CourseChoiceDraftCourses
                .Single(candidate => candidate.Name == "세미나 3");
            seminarDraft.Offerings[0].SelectAcceptableCommand.Execute(null);
            Dispatcher.UIThread.RunJobs();

            Assert.True(workspace.IsCourseChoiceEditorVisible);
            Assert.Equal("수강 선택 설정", workspace.CourseChoiceEditorTitle);
            saveRenderedFrame(
                window,
                "course-choice-editor-light-1487x1058.png");

            window.RequestedThemeVariant = ThemeVariant.Dark;
            Dispatcher.UIThread.RunJobs();
            saveRenderedFrame(
                window,
                "course-choice-editor-dark-1487x1058.png");
        }
        finally
        {
            window.Close();
            workspace.Dispose();
        }
    }

    [AvaloniaFact]
    public void PersonalScheduleEditorRendersInLightAndDarkThemes()
    {
        PlannerWorkspaceViewModel workspace =
            PlannerWorkspaceTestFactory.CreateWorkspace();
        ProductShellViewModel shell = PlannerWorkspaceTestFactory.CreateShell(
            workspace);
        MainWindow window = new MainWindow(
            shell,
            ProductAppearanceTestFactory.CreateViewModel());
        window.Width = REFERENCE_WIDTH;
        window.Height = REFERENCE_HEIGHT;

        try
        {
            window.RequestedThemeVariant = ThemeVariant.Light;
            window.Show();
            Dispatcher.UIThread.RunJobs();

            workspace.BeginAddPersonalScheduleCommand.Execute(null);
            workspace.PersonalScheduleTitleDraft = "연구실 정기 미팅";
            workspace.PersonalScheduleDayOptions
                .Single(option => option.Day == EDay.Tuesday)
                .IsSelected = true;
            workspace.PersonalScheduleDayOptions
                .Single(option => option.Day == EDay.Thursday)
                .IsSelected = true;
            workspace.PersonalScheduleStartTimeOrNull = new ScheduleTime(18, 0);
            workspace.PersonalScheduleEndTimeOrNull = new ScheduleTime(19, 30);
            workspace.PersonalScheduleSectionDraft = "A";
            workspace.PersonalScheduleInstructorDraft = "김 교수";
            workspace.PersonalScheduleLocationDraft = "느헤미야홀 101호";
            Dispatcher.UIThread.RunJobs();

            saveRenderedFrame(
                window,
                "personal-schedule-editor-light-1487x1058.png");

            window.RequestedThemeVariant = ThemeVariant.Dark;
            Dispatcher.UIThread.RunJobs();
            saveRenderedFrame(
                window,
                "personal-schedule-editor-dark-1487x1058.png");
        }
        finally
        {
            window.Close();
            workspace.Dispose();
        }
    }

    [AvaloniaFact]
    public void PersonalScheduleDeleteConfirmationRendersInLightAndDarkThemes()
    {
        PlannerWorkspaceViewModel workspace =
            PlannerWorkspaceTestFactory.CreateWorkspace();
        workspace.BeginAddPersonalScheduleCommand.Execute(null);
        workspace.PersonalScheduleTitleDraft = "연구실 정기 미팅";
        workspace.PersonalScheduleDayOptions
            .Single(option => option.Day == EDay.Tuesday)
            .IsSelected = true;
        workspace.PersonalScheduleStartTimeOrNull = new ScheduleTime(18, 0);
        workspace.PersonalScheduleEndTimeOrNull = new ScheduleTime(19, 0);
        workspace.SavePersonalScheduleCommand.Execute(null);
        PersonalScheduleItem personalSchedule = Assert.Single(
            workspace.ActivePlan.PersonalSchedules);
        workspace.BeginDeletePersonalScheduleCommand.Execute(personalSchedule);
        ProductShellViewModel shell = PlannerWorkspaceTestFactory.CreateShell(
            workspace);
        MainWindow window = new MainWindow(
            shell,
            ProductAppearanceTestFactory.CreateViewModel());
        window.Width = REFERENCE_WIDTH;
        window.Height = REFERENCE_HEIGHT;

        try
        {
            window.RequestedThemeVariant = ThemeVariant.Light;
            window.Show();
            Dispatcher.UIThread.RunJobs();
            saveRenderedFrame(
                window,
                "personal-schedule-delete-light-1487x1058.png");

            window.RequestedThemeVariant = ThemeVariant.Dark;
            Dispatcher.UIThread.RunJobs();
            saveRenderedFrame(
                window,
                "personal-schedule-delete-dark-1487x1058.png");
        }
        finally
        {
            window.Close();
            workspace.Dispose();
        }
    }

    private static void saveRenderedFrame(MainWindow window, string fileName)
    {
        WriteableBitmap? renderedFrameOrNull = window.CaptureRenderedFrame();
        Assert.NotNull(renderedFrameOrNull);
        if (renderedFrameOrNull == null)
        {
            throw new InvalidOperationException(
                "The headless renderer did not produce a frame.");
        }

        WriteableBitmap renderedFrame = renderedFrameOrNull;
        string outputDirectoryPath = Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "TestResults");
        outputDirectoryPath = Path.GetFullPath(outputDirectoryPath);
        Directory.CreateDirectory(outputDirectoryPath);
        string outputFilePath = Path.Combine(outputDirectoryPath, fileName);
        renderedFrame.Save(outputFilePath, PngBitmapEncoderOptions.Default);

        Assert.True(File.Exists(outputFilePath));
        FileInfo outputFile = new FileInfo(outputFilePath);
        Assert.True(outputFile.Length > 0L);
        Assert.Equal(1_487, renderedFrame.PixelSize.Width);
        Assert.Equal(1_058, renderedFrame.PixelSize.Height);
    }
}
