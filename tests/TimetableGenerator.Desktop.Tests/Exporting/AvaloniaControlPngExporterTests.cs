using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
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
    public async Task ExportControlRendersArrangedControlAsHighDensityPngAsync()
    {
        Border sourceControl = createArrangedControl();
        AvaloniaControlPngExporter exporter = new AvaloniaControlPngExporter(PngExportScale.PRODUCT_QUALITY);

        using (MemoryStream destinationStream = new MemoryStream())
        {
            await Task.Run(
                delegate
                {
                    return exporter.ExportControlAsync(sourceControl, destinationStream, CancellationToken.None);
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
    public async Task PngEncodingDoesNotBlockTheUiDispatcherAsync()
    {
        Border sourceControl = createArrangedControl();
        AvaloniaControlPngExporter exporter = new AvaloniaControlPngExporter(PngExportScale.PRODUCT_QUALITY);

        using (BlockingWriteStream destinationStream = new BlockingWriteStream())
        {
            Task exportTask = exporter.ExportControlAsync(sourceControl, destinationStream, TestContext.Current.CancellationToken);
            await destinationStream.WriteStartedTask.WaitAsync(TimeSpan.FromSeconds(5.0), TestContext.Current.CancellationToken);

            bool dispatcherCallbackRan = false;
            await Dispatcher.UIThread.InvokeAsync(
                delegate
                {
                    dispatcherCallbackRan = true;
                },
                DispatcherPriority.Input);

            Assert.True(dispatcherCallbackRan);
            destinationStream.Release();
            await exportTask;
            Assert.True(destinationStream.Length > PNG_SIGNATURE.Length);
        }
    }

    [AvaloniaFact]
    public async Task ExportControlFinalizesPendingChildLayoutAsync()
    {
        Color expectedChildColor = Color.FromRgb(17, 34, 51);
        Grid sourceControl = new Grid();
        sourceControl.Background = Brushes.White;
        sourceControl.Width = 120.0;
        sourceControl.Height = 80.0;
        Window window = new Window();
        window.Width = 180.0;
        window.Height = 140.0;
        window.Content = sourceControl;
        window.Show();
        Dispatcher.UIThread.RunJobs();

        try
        {
            Border lateChild = new Border();
            lateChild.Background = new SolidColorBrush(expectedChildColor);
            Dispatcher.UIThread.Post(
                delegate
                {
                    sourceControl.Children.Add(lateChild);
                },
                DispatcherPriority.Render);

            AvaloniaControlPngExporter exporter = new AvaloniaControlPngExporter(PngExportScale.Create(1.0));
            using (MemoryStream destinationStream = new MemoryStream())
            {
                await exporter.ExportControlAsync(sourceControl, destinationStream, TestContext.Current.CancellationToken);

                Assert.True(lateChild.IsArrangeValid);
                destinationStream.Position = 0L;
                using (Bitmap bitmap = new Bitmap(destinationStream))
                {
                    assertBitmapContainsColor(bitmap, expectedChildColor);
                }
            }
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public async Task ExportControlRejectsControlWithoutArrangedSizeAsync()
    {
        Border sourceControl = new Border();
        AvaloniaControlPngExporter exporter = new AvaloniaControlPngExporter(PngExportScale.PRODUCT_QUALITY);

        using (MemoryStream destinationStream = new MemoryStream())
        {
            InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(
                delegate
                {
                    return exporter.ExportControlAsync(sourceControl, destinationStream, CancellationToken.None);
                });

            Assert.Contains("positive arranged size", exception.Message, StringComparison.Ordinal);
            Assert.Empty(destinationStream.ToArray());
        }
    }

    [AvaloniaFact]
    public async Task ExportControlRejectsNullDestinationStreamAsync()
    {
        Border sourceControl = createArrangedControl();
        AvaloniaControlPngExporter exporter = new AvaloniaControlPngExporter(PngExportScale.PRODUCT_QUALITY);

        ArgumentNullException exception = await Assert.ThrowsAsync<ArgumentNullException>(
            delegate
            {
                return exporter.ExportControlAsync(sourceControl, null!, CancellationToken.None);
            });

        Assert.Equal("destinationStream", exception.ParamName);
    }

    [AvaloniaFact]
    public async Task ExportControlRejectsReadOnlyDestinationStreamAsync()
    {
        Border sourceControl = createArrangedControl();
        AvaloniaControlPngExporter exporter = new AvaloniaControlPngExporter(PngExportScale.PRODUCT_QUALITY);
        byte[] destinationBuffer = new byte[128];

        using (MemoryStream destinationStream = new MemoryStream(destinationBuffer, false))
        {
            ArgumentException exception = await Assert.ThrowsAsync<ArgumentException>(
                delegate
                {
                    return exporter.ExportControlAsync(sourceControl, destinationStream, CancellationToken.None);
                });

            Assert.Equal("destinationStream", exception.ParamName);
            Assert.Contains("writable", exception.Message, StringComparison.Ordinal);
        }
    }

    [AvaloniaFact]
    public async Task ExportControlHonorsCancellationBeforeRenderingAsync()
    {
        Border sourceControl = createArrangedControl();
        AvaloniaControlPngExporter exporter = new AvaloniaControlPngExporter(PngExportScale.PRODUCT_QUALITY);

        using (MemoryStream destinationStream = new MemoryStream())
        using (CancellationTokenSource cancellationSource = new CancellationTokenSource())
        {
            cancellationSource.Cancel();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(
                delegate
                {
                    return exporter.ExportControlAsync(sourceControl, destinationStream, cancellationSource.Token);
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

    private static void assertBitmapContainsColor(Bitmap bitmap, Color expectedColor)
    {
        using (WriteableBitmap pixelCopy = new WriteableBitmap(bitmap.PixelSize, new Vector(96.0, 96.0), PixelFormat.Bgra8888, AlphaFormat.Premul))
        using (ILockedFramebuffer framebuffer = pixelCopy.Lock())
        {
            bitmap.CopyPixels(framebuffer);
            for (int y = 0; y < bitmap.PixelSize.Height; ++y)
            {
                for (int x = 0; x < bitmap.PixelSize.Width; ++x)
                {
                    int pixelOffset = (y * framebuffer.RowBytes) + (x * 4);
                    byte blue = Marshal.ReadByte(framebuffer.Address, pixelOffset);
                    byte green = Marshal.ReadByte(framebuffer.Address, pixelOffset + 1);
                    byte red = Marshal.ReadByte(framebuffer.Address, pixelOffset + 2);
                    byte alpha = Marshal.ReadByte(framebuffer.Address, pixelOffset + 3);
                    if (blue == expectedColor.B
                        && green == expectedColor.G
                        && red == expectedColor.R
                        && alpha == expectedColor.A)
                    {
                        return;
                    }
                }
            }
        }

        Assert.Fail("The exported PNG did not contain the pending child layout.");
    }

    private sealed class BlockingWriteStream : Stream
    {
        private readonly MemoryStream mInnerStream = new MemoryStream();

        private readonly ManualResetEventSlim mReleaseEvent = new ManualResetEventSlim(false);

        private readonly TaskCompletionSource mWriteStartedSource = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        private int mHasBlocked;

        public override bool CanRead
        {
            get
            {
                return false;
            }
        }

        public override bool CanSeek
        {
            get
            {
                return true;
            }
        }

        public override bool CanWrite
        {
            get
            {
                return true;
            }
        }

        public override long Length
        {
            get
            {
                return mInnerStream.Length;
            }
        }

        public override long Position
        {
            get
            {
                return mInnerStream.Position;
            }
            set
            {
                mInnerStream.Position = value;
            }
        }

        public Task WriteStartedTask
        {
            get
            {
                return mWriteStartedSource.Task;
            }
        }

        public override void Flush()
        {
            mInnerStream.Flush();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            throw new NotSupportedException();
        }

        public void Release()
        {
            mReleaseEvent.Set();
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            return mInnerStream.Seek(offset, origin);
        }

        public override void SetLength(long value)
        {
            mInnerStream.SetLength(value);
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            waitForReleaseOnFirstWrite();
            mInnerStream.Write(buffer, offset, count);
        }

        public override void Write(ReadOnlySpan<byte> buffer)
        {
            waitForReleaseOnFirstWrite();
            mInnerStream.Write(buffer);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                mReleaseEvent.Set();
                mReleaseEvent.Dispose();
                mInnerStream.Dispose();
            }

            base.Dispose(disposing);
        }

        private void waitForReleaseOnFirstWrite()
        {
            if (Interlocked.Exchange(ref mHasBlocked, 1) != 0)
            {
                return;
            }

            mWriteStartedSource.TrySetResult();
            if (mReleaseEvent.Wait(TimeSpan.FromSeconds(5.0)) == false)
            {
                throw new TimeoutException("The PNG encoder did not release the test stream.");
            }
        }
    }
}
