using System;
using System.IO;
using System.Threading.Tasks;

using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Media.Imaging;
using Avalonia.Styling;
using Avalonia.Threading;

using TimetableGenerator.Desktop.Presentation.ViewModels;
using TimetableGenerator.Desktop.Product;
using TimetableGenerator.Desktop.Tests.Presentation.Appearance;
using TimetableGenerator.Desktop.Tests.Presentation.Recommendations;
using TimetableGenerator.Desktop.Views;

using Xunit;

namespace TimetableGenerator.Desktop.Tests;

public sealed class AdaptiveRecommendationRenderTests
{
    private const double REFERENCE_WIDTH = 1_487.0;

    private const double REFERENCE_HEIGHT = 1_058.0;

    private static readonly TimeSpan TEST_TIMEOUT = TimeSpan.FromSeconds(5.0);

    [AvaloniaFact]
    public async Task AdaptiveRecommendationStatesRenderToPngAsync()
    {
        ControlledExhaustiveScheduleRecommendationProvider recommendationProvider;
        PlannerWorkspaceViewModel workspace = AdaptiveRecommendationVisualTestFixture.CreateWorkspace(out recommendationProvider);
        await workspace.RecommendationRefreshTask.WaitAsync(TEST_TIMEOUT);

        ProductShellViewModel shell = PlannerWorkspaceTestFactory.CreateShell(workspace);
        MainWindow window = new MainWindow(shell, ProductAppearanceTestFactory.CreateViewModel());
        window.Width = REFERENCE_WIDTH;
        window.Height = REFERENCE_HEIGHT;

        try
        {
            window.RequestedThemeVariant = ThemeVariant.Light;
            window.Show();
            Dispatcher.UIThread.RunJobs();

            assertPartialRecommendationState(workspace);
            saveRenderedFrame(window, "adaptive-recommendations-partial-light-1487x1058.png");

            window.RequestedThemeVariant = ThemeVariant.Dark;
            Dispatcher.UIThread.RunJobs();
            saveRenderedFrame(window, "adaptive-recommendations-partial-dark-1487x1058.png");

            window.RequestedThemeVariant = ThemeVariant.Light;
            Dispatcher.UIThread.RunJobs();
            workspace.CalculateAllRecommendationsCommand.Execute(null);
            await recommendationProvider.ExhaustiveCallStarted.WaitAsync(TEST_TIMEOUT);
            Dispatcher.UIThread.RunJobs();

            Assert.True(workspace.IsCalculatingAllRecommendations);
            Assert.Equal("1 / 24+", workspace.RecommendationSummary);
            Assert.Equal("모든 가능한 시간표를 계산하고 있습니다", workspace.AdditionalRecommendationTitle);
            saveRenderedFrame(window, "adaptive-recommendations-calculating-light-1487x1058.png");
        }
        finally
        {
            window.Close();
            workspace.Dispose();
            await workspace.RecommendationRefreshTask.WaitAsync(TEST_TIMEOUT);
        }
    }

    private static void assertPartialRecommendationState(PlannerWorkspaceViewModel workspace)
    {
        Assert.Equal("1 / 24+", workspace.RecommendationSummary);
        Assert.True(workspace.HasAdditionalRecommendations);
        Assert.True(workspace.CanCalculateAllRecommendations);
        Assert.False(workspace.IsCalculatingAllRecommendations);
        Assert.Equal("전체 시간표 계산", workspace.CalculateAllRecommendationsActionText);
    }

    private static void saveRenderedFrame(MainWindow window, string fileName)
    {
        WriteableBitmap? renderedFrameOrNull = window.CaptureRenderedFrame();
        Assert.NotNull(renderedFrameOrNull);
        if (renderedFrameOrNull == null)
        {
            throw new InvalidOperationException("The headless renderer did not produce a frame.");
        }

        string outputDirectoryPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "TestResults"));
        Directory.CreateDirectory(outputDirectoryPath);
        string outputFilePath = Path.Combine(outputDirectoryPath, fileName);
        renderedFrameOrNull.Save(outputFilePath, PngBitmapEncoderOptions.Default);

        Assert.True(File.Exists(outputFilePath));
        Assert.True(new FileInfo(outputFilePath).Length > 0L);
        Assert.Equal(1_487, renderedFrameOrNull.PixelSize.Width);
        Assert.Equal(1_058, renderedFrameOrNull.PixelSize.Height);
    }
}
