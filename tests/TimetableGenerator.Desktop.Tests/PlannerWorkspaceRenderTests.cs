using System;
using System.IO;

using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Media.Imaging;
using Avalonia.Threading;

using TimetableGenerator.Desktop.Presentation.Sample;
using TimetableGenerator.Desktop.Presentation.ViewModels;
using TimetableGenerator.Desktop.Views;

using Xunit;

namespace TimetableGenerator.Desktop.Tests;

public sealed class PlannerWorkspaceRenderTests
{
    private const double REFERENCE_WIDTH = 1_440.0;
    private const double REFERENCE_HEIGHT = 900.0;

    [AvaloniaFact]
    public void ReferenceWorkspaceRendersToPng()
    {
        PlannerWorkspaceViewModel workspace = PlannerSampleStateFactory.CreateWorkspace();
        MainWindow window = new MainWindow(workspace);
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
                "planning-workspace-1440x900.png");
            renderedFrame.Save(outputFilePath, PngBitmapEncoderOptions.Default);

            Assert.True(File.Exists(outputFilePath));
            FileInfo outputFile = new FileInfo(outputFilePath);
            Assert.True(outputFile.Length > 0L);
            Assert.Equal(1_440, renderedFrame.PixelSize.Width);
            Assert.Equal(900, renderedFrame.PixelSize.Height);
        }
        finally
        {
            window.Close();
        }
    }
}
