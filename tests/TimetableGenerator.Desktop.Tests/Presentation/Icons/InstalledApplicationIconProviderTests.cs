using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

using Avalonia;
using Avalonia.Headless.XUnit;
using Avalonia.Media.Imaging;
using Avalonia.Platform;

using TimetableGenerator.Desktop.Platforms.MacOS;
using TimetableGenerator.Desktop.Presentation.Icons;

using Xunit;

namespace TimetableGenerator.Desktop.Tests.Presentation.Icons;

public sealed class InstalledApplicationIconProviderTests
{
    private const string APPLE_CALENDAR_BUNDLE_IDENTIFIER =
        "com.apple.iCal";

    private static readonly PixelSize MENU_ICON_PIXEL_SIZE =
        new PixelSize(48, 48);

    [Fact]
    public void FactorySelectsTheCurrentPlatformProvider()
    {
        IInstalledApplicationIconProvider provider =
            InstalledApplicationIconProviderFactory.CreateDefault();

        if (OperatingSystem.IsMacOS())
        {
            Assert.Same(
                MacOSInstalledApplicationIconProvider.Instance,
                provider);
        }
        else
        {
            Assert.Same(
                NoOpInstalledApplicationIconProvider.Instance,
                provider);
        }
    }

    [Fact]
    public void NoOpProviderReturnsNoIcon()
    {
        Bitmap? iconOrNull =
            NoOpInstalledApplicationIconProvider.Instance.TryLoad(
                APPLE_CALENDAR_BUNDLE_IDENTIFIER,
                MENU_ICON_PIXEL_SIZE);

        Assert.Null(iconOrNull);
    }

    [AvaloniaFact]
    public void MacOSProviderLoadsTheInstalledAppleCalendarIcon()
    {
        if (OperatingSystem.IsMacOS() == false)
        {
            return;
        }

        IInstalledApplicationIconProvider provider =
            InstalledApplicationIconProviderFactory.CreateDefault();
        Bitmap? iconOrNull = provider.TryLoad(
            APPLE_CALENDAR_BUNDLE_IDENTIFIER,
            MENU_ICON_PIXEL_SIZE);
        Assert.NotNull(iconOrNull);
        if (iconOrNull == null)
        {
            throw new InvalidOperationException(
                "The installed Apple Calendar icon was unavailable.");
        }

        using (Bitmap icon = iconOrNull)
        {
            Assert.Equal(MENU_ICON_PIXEL_SIZE, icon.PixelSize);
            assertContainsNontrivialPixels(icon);
        }
    }

    private static void assertContainsNontrivialPixels(Bitmap bitmap)
    {
        using (WriteableBitmap pixelCopy = new WriteableBitmap(
            bitmap.PixelSize,
            new Vector(96.0, 96.0),
            PixelFormat.Bgra8888,
            AlphaFormat.Premul))
        using (ILockedFramebuffer framebuffer = pixelCopy.Lock())
        {
            bitmap.CopyPixels(framebuffer);
            int visiblePixelCount = 0;
            bool containsTransparentOrTranslucentPixel = false;
            HashSet<uint> visibleColors = new HashSet<uint>();
            for (int y = 0; y < bitmap.PixelSize.Height; ++y)
            {
                for (int x = 0; x < bitmap.PixelSize.Width; ++x)
                {
                    int pixelOffset =
                        (y * framebuffer.RowBytes) + (x * 4);
                    byte blue = Marshal.ReadByte(
                        framebuffer.Address,
                        pixelOffset);
                    byte green = Marshal.ReadByte(
                        framebuffer.Address,
                        pixelOffset + 1);
                    byte red = Marshal.ReadByte(
                        framebuffer.Address,
                        pixelOffset + 2);
                    byte alpha = Marshal.ReadByte(
                        framebuffer.Address,
                        pixelOffset + 3);
                    if (alpha == byte.MaxValue)
                    {
                        visiblePixelCount++;
                    }
                    else
                    {
                        containsTransparentOrTranslucentPixel = true;
                    }

                    if (alpha > 0)
                    {
                        visibleColors.Add(
                            ((uint)alpha << 24)
                            | ((uint)red << 16)
                            | ((uint)green << 8)
                            | blue);
                    }
                }
            }

            int pixelCount =
                bitmap.PixelSize.Width * bitmap.PixelSize.Height;
            Assert.True(visiblePixelCount > pixelCount / 4);
            Assert.True(containsTransparentOrTranslucentPixel);
            Assert.True(visibleColors.Count >= 8);
        }
    }
}
