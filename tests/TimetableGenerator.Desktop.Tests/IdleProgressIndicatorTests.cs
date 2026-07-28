using System.Linq;
using System.Threading.Tasks;

using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;

using TimetableGenerator.Desktop.Presentation.ViewModels;
using TimetableGenerator.Desktop.Product;
using TimetableGenerator.Desktop.Tests.Presentation.Appearance;
using TimetableGenerator.Desktop.Views;

using Xunit;

namespace TimetableGenerator.Desktop.Tests;

public sealed class IdleProgressIndicatorTests
{
    [AvaloniaFact]
    public async Task IdleWorkspaceStopsEveryIndeterminateProgressAnimationAsync()
    {
        PlannerWorkspaceViewModel workspace = PlannerWorkspaceTestFactory.CreateWorkspace();
        await workspace.RecommendationRefreshTask;
        ProductShellViewModel shell = PlannerWorkspaceTestFactory.CreateShell(workspace);
        MainWindow window = new MainWindow(shell, ProductAppearanceTestFactory.CreateViewModel());

        try
        {
            window.Show();
            Dispatcher.UIThread.RunJobs();

            ProgressBar[] progressIndicators = window.GetVisualDescendants().OfType<ProgressBar>().ToArray();
            Assert.Equal(5, progressIndicators.Length);
            Assert.All(
                progressIndicators,
                progressIndicator => Assert.False(progressIndicator.IsIndeterminate));
        }
        finally
        {
            window.Close();
            workspace.Dispose();
        }
    }
}
