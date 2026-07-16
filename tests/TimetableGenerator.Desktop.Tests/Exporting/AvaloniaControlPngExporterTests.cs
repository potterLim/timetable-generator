using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;

using TimetableGenerator.Desktop.Exporting;

using Xunit;

namespace TimetableGenerator.Desktop.Tests.Exporting;

public sealed class AvaloniaControlPngExporterTests
{
    private static readonly byte[] PNG_SIGNATURE =
    {
        0x89,
        0x50,
        0x4E,
        0x47,
        0x0D,
        0x0A,
        0x1A,
        0x0A,
    };

    [AvaloniaFact]
    public async Task ExportControlAsyncRendersArrangedControlAsHighDensityPng()
    {
        Border sourceControl = createArrangedControl();
        AvaloniaControlPngExporter exporter = new AvaloniaControlPngExporter(
            PngExportScale.PRODUCT_QUALITY);

        using (MemoryStream destinationStream = new MemoryStream())
        {
            await Task.Run(
                delegate
                {
                    return exporter.ExportControlAsync(
                        sourceControl,
                        destinationStream,
                        CancellationToken.None);
                });

            byte[] pngBytes = destinationStream.ToArray();
            Assert.True(pngBytes.Length > PNG_SIGNATURE.Length);
            Assert.Equal(PNG_SIGNATURE, pngBytes[..PNG_SIGNATURE.Length]);

            destinationStream.Position = 0L;
            using (Bitmap exportedBitmap = new Bitmap(destinationStream))
            {
                Assert.Equal(new PixelSize(240, 160), exportedBitmap.PixelSize);
            }
        }
    }

    [AvaloniaFact]
    public async Task ExportControlAsyncRejectsControlWithoutArrangedSize()
    {
        Border sourceControl = new Border();
        AvaloniaControlPngExporter exporter = new AvaloniaControlPngExporter(
            PngExportScale.PRODUCT_QUALITY);

        using (MemoryStream destinationStream = new MemoryStream())
        {
            InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(
                delegate
                {
                    return exporter.ExportControlAsync(
                        sourceControl,
                        destinationStream,
                        CancellationToken.None);
                });

            Assert.Contains("positive arranged size", exception.Message, StringComparison.Ordinal);
            Assert.Empty(destinationStream.ToArray());
        }
    }

    [AvaloniaFact]
    public async Task ExportControlAsyncRejectsNullDestinationStream()
    {
        Border sourceControl = createArrangedControl();
        AvaloniaControlPngExporter exporter = new AvaloniaControlPngExporter(
            PngExportScale.PRODUCT_QUALITY);

        ArgumentNullException exception = await Assert.ThrowsAsync<ArgumentNullException>(
            delegate
            {
                return exporter.ExportControlAsync(
                    sourceControl,
                    null!,
                    CancellationToken.None);
            });

        Assert.Equal("destinationStream", exception.ParamName);
    }

    [AvaloniaFact]
    public async Task ExportControlAsyncRejectsReadOnlyDestinationStream()
    {
        Border sourceControl = createArrangedControl();
        AvaloniaControlPngExporter exporter = new AvaloniaControlPngExporter(
            PngExportScale.PRODUCT_QUALITY);
        byte[] destinationBuffer = new byte[128];

        using (MemoryStream destinationStream = new MemoryStream(destinationBuffer, false))
        {
            ArgumentException exception = await Assert.ThrowsAsync<ArgumentException>(
                delegate
                {
                    return exporter.ExportControlAsync(
                        sourceControl,
                        destinationStream,
                        CancellationToken.None);
                });

            Assert.Equal("destinationStream", exception.ParamName);
            Assert.Contains("writable", exception.Message, StringComparison.Ordinal);
        }
    }

    [AvaloniaFact]
    public async Task ExportControlAsyncHonorsCancellationBeforeRendering()
    {
        Border sourceControl = createArrangedControl();
        AvaloniaControlPngExporter exporter = new AvaloniaControlPngExporter(
            PngExportScale.PRODUCT_QUALITY);

        using (MemoryStream destinationStream = new MemoryStream())
        using (CancellationTokenSource cancellationSource = new CancellationTokenSource())
        {
            cancellationSource.Cancel();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(
                delegate
                {
                    return exporter.ExportControlAsync(
                        sourceControl,
                        destinationStream,
                        cancellationSource.Token);
                });

            Assert.Empty(destinationStream.ToArray());
        }
    }

    [AvaloniaFact]
    public void CreateRejectsInvalidPngExportScale()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            delegate
            {
                PngExportScale.Create(0.0);
            });
        Assert.Throws<ArgumentOutOfRangeException>(
            delegate
            {
                PngExportScale.Create(double.NaN);
            });
        Assert.Throws<ArgumentOutOfRangeException>(
            delegate
            {
                PngExportScale.Create(4.1);
            });
    }

    private static Border createArrangedControl()
    {
        Border sourceControl = new Border();
        sourceControl.Background = Brushes.White;
        sourceControl.BorderBrush = Brushes.Blue;
        sourceControl.BorderThickness = new Thickness(2.0);
        sourceControl.Measure(new Size(120.0, 80.0));
        sourceControl.Arrange(new Rect(0.0, 0.0, 120.0, 80.0));
        Dispatcher.UIThread.RunJobs();
        return sourceControl;
    }
}
