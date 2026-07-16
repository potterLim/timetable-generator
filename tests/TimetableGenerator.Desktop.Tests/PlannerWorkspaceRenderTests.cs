using System;
using System.IO;
using System.Threading.Tasks;

using Avalonia.Controls;
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
        Assert.True(window.CanResize);
        Assert.Equal(WindowDecorations.Full, window.WindowDecorations);
        Assert.False(window.ExtendClientAreaToDecorationsHint);
        Assert.True(window.ShowInTaskbar);

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
    public async Task PlanCloseDialogRendersToPngAsync()
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
            workspace.Plans[1].CloseCommand.Execute(null);
            Dispatcher.UIThread.RunJobs();

            saveRenderedFrame(window, "plan-close-dialog-1487x1058.png");
        }
        finally
        {
            window.Close();
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
