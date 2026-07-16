using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia.Threading;

namespace TimetableGenerator.Desktop.Exporting;

public sealed class AvaloniaControlPngExporter : IControlPngExporter
{
    private const double STANDARD_DPI = 96.0;

    private readonly PngExportScale mExportScale;

    public AvaloniaControlPngExporter(PngExportScale exportScale)
    {
        ArgumentNullException.ThrowIfNull(exportScale);

        mExportScale = exportScale;
    }

    public Task ExportControlAsync(
        Control sourceControl,
        Stream destinationStream,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(sourceControl);
        ArgumentNullException.ThrowIfNull(destinationStream);

        if (destinationStream.CanWrite == false)
        {
            throw new ArgumentException(
                "The PNG destination stream must be writable.",
                nameof(destinationStream));
        }

        cancellationToken.ThrowIfCancellationRequested();

        return exportOnUiThreadAsync(sourceControl, destinationStream, cancellationToken);
    }

    private async Task exportOnUiThreadAsync(
        Control sourceControl,
        Stream destinationStream,
        CancellationToken cancellationToken)
    {
        await Dispatcher.UIThread.InvokeAsync(
            delegate
            {
                cancellationToken.ThrowIfCancellationRequested();

                PixelSize pixelSize = calculatePixelSize(sourceControl.Bounds.Size);
                double exportDpi = STANDARD_DPI * mExportScale.Multiplier;
                Vector dpi = new Vector(exportDpi, exportDpi);

                using (RenderTargetBitmap renderedBitmap = new RenderTargetBitmap(pixelSize, dpi))
                {
                    renderedBitmap.Render(sourceControl);
                    cancellationToken.ThrowIfCancellationRequested();
                    renderedBitmap.Save(destinationStream, PngBitmapEncoderOptions.Default);
                }
            },
            DispatcherPriority.Render,
            cancellationToken);
    }

    private PixelSize calculatePixelSize(Size controlSize)
    {
        if (double.IsFinite(controlSize.Width) == false ||
            double.IsFinite(controlSize.Height) == false ||
            controlSize.Width <= 0.0 ||
            controlSize.Height <= 0.0)
        {
            throw new InvalidOperationException(
                "The source control must have a positive arranged size before PNG export.");
        }

        double scaledWidth = Math.Ceiling(controlSize.Width * mExportScale.Multiplier);
        double scaledHeight = Math.Ceiling(controlSize.Height * mExportScale.Multiplier);
        if (scaledWidth > int.MaxValue || scaledHeight > int.MaxValue)
        {
            throw new InvalidOperationException(
                "The arranged control is too large to export as a PNG image.");
        }

        return new PixelSize((int)scaledWidth, (int)scaledHeight);
    }
}
