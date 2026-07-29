using System;
using System.Threading.Tasks;

using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;

using TimetableGenerator.Application.Scheduling;
using TimetableGenerator.Desktop.Presentation.Recommendations;
using TimetableGenerator.Desktop.Presentation.ViewModels;
using TimetableGenerator.Desktop.Views;

using Xunit;

namespace TimetableGenerator.Desktop.Tests;

public sealed class AdaptiveRecommendationPresentationTests
{
    private static readonly TimeSpan TEST_TIMEOUT = TimeSpan.FromSeconds(5.0);

    [AvaloniaFact]
    public async Task PartialResultsShowTheExhaustiveCalculationActionAsync()
    {
        RecommendationCalculationPolicy policy = new RecommendationCalculationPolicy(new ScheduleRecommendationLimit(1), TimeSpan.Zero);
        using (PlannerWorkspaceViewModel workspace = PlannerWorkspaceTestFactory.CreateWorkspace(policy))
        {
            await workspace.RecommendationRefreshTask.WaitAsync(TEST_TIMEOUT);
            ScheduleWorkspaceView workspaceView = new ScheduleWorkspaceView();
            workspaceView.DataContext = workspace;
            Window window = new Window
            {
                Width = 1_100.0,
                Height = 720.0,
                Content = workspaceView,
            };

            try
            {
                window.Show();
                Dispatcher.UIThread.RunJobs();

                Border? bannerOrNull = workspaceView.FindControl<Border>("AdditionalRecommendationsBanner");
                Button? calculateButtonOrNull = workspaceView.FindControl<Button>("CalculateAllRecommendationsButton");
                Assert.NotNull(bannerOrNull);
                Assert.NotNull(calculateButtonOrNull);
                if (bannerOrNull == null || calculateButtonOrNull == null)
                {
                    throw new InvalidOperationException(
                        "The additional recommendation controls were not found.");
                }

                Assert.True(bannerOrNull.IsVisible);
                Assert.Equal(new Thickness(0.0, 0.0, 0.0, 12.0), bannerOrNull.Margin);
                Assert.True(calculateButtonOrNull.IsVisible);
                Assert.Equal("전체 시간표 계산", calculateButtonOrNull.Content);
                Assert.Equal(
                    "전체 시간표 계산",
                    AutomationProperties.GetName(calculateButtonOrNull));
                Assert.Same(
                    workspace.CalculateAllRecommendationsCommand,
                    calculateButtonOrNull.Command);
            }
            finally
            {
                window.Close();
            }
        }
    }
}
