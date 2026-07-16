using System;
using System.IO;
using System.Threading.Tasks;

using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Media.Imaging;
using Avalonia.Threading;

using TimetableGenerator.Desktop.Presentation.ViewModels;
using TimetableGenerator.Desktop.Product;
using TimetableGenerator.Desktop.Views;

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
        MainWindow window = new MainWindow(shell);
        window.Width = REFERENCE_WIDTH;
        window.Height = REFERENCE_HEIGHT;

        try
        {
            window.Show();
            Dispatcher.UIThread.RunJobs();

            WriteableBitmap? renderedFrameOrNull = window.CaptureRenderedFrame();
            Assert.NotNull(renderedFrameOrNull);
            if (renderedFrameOrNull == null)
            {
                throw new InvalidOperationException("The headless renderer did not produce a frame.");
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
            string outputFilePath = Path.Combine(
                outputDirectoryPath,
                "planning-workspace-1487x1058.png");
            renderedFrame.Save(outputFilePath, PngBitmapEncoderOptions.Default);

            Assert.True(File.Exists(outputFilePath));
            FileInfo outputFile = new FileInfo(outputFilePath);
            Assert.True(outputFile.Length > 0L);
            Assert.Equal(1_487, renderedFrame.PixelSize.Width);
            Assert.Equal(1_058, renderedFrame.PixelSize.Height);
        }
        finally
        {
            window.Close();
        }
    }
}
