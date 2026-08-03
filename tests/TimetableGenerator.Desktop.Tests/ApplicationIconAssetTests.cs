using System;
using System.Collections.Generic;
using System.IO;

using Xunit;

namespace TimetableGenerator.Desktop.Tests;

public sealed class ApplicationIconAssetTests
{
    private const int PNG_MINIMUM_SIDE_LENGTH = 1024;
    private const byte PNG_TRUECOLOR_WITH_ALPHA = 6;

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

    private static readonly int[] REQUIRED_WINDOWS_ICON_SIZES =
    {
        16,
        20,
        24,
        32,
        40,
        48,
        64,
        128,
        256,
    };

    [Fact]
    public void AppIconMasterIsSquareHighResolutionPngWithAlpha()
    {
        string iconPath = Path.Combine(AppContext.BaseDirectory, "ProductAssets", "AppIcon.png");
        byte[] bytes = File.ReadAllBytes(iconPath);

        Assert.True(bytes.Length >= 26);
        Assert.Equal(PNG_SIGNATURE, bytes[..PNG_SIGNATURE.Length]);

        int width = readBigEndianInt32(bytes, 16);
        int height = readBigEndianInt32(bytes, 20);
        Assert.Equal(width, height);
        Assert.True(width >= PNG_MINIMUM_SIDE_LENGTH);
        Assert.Equal(PNG_TRUECOLOR_WITH_ALPHA, bytes[25]);
    }

    [Fact]
    public void WindowsIconContainsEveryRequiredDesktopSize()
    {
        string iconPath = Path.Combine(AppContext.BaseDirectory, "ProductAssets", "AppIcon.ico");
        byte[] bytes = File.ReadAllBytes(iconPath);

        Assert.True(bytes.Length >= 6);
        Assert.Equal(0, readLittleEndianUInt16(bytes, 0));
        Assert.Equal(1, readLittleEndianUInt16(bytes, 2));

        int imageCount = readLittleEndianUInt16(bytes, 4);
        Assert.True(imageCount >= REQUIRED_WINDOWS_ICON_SIZES.Length);
        Assert.True(bytes.Length >= 6 + (16 * imageCount));

        HashSet<int> sizes = new HashSet<int>();
        for (int imageIndex = 0; imageIndex < imageCount; ++imageIndex)
        {
            int entryOffset = 6 + (16 * imageIndex);
            int width;
            if (bytes[entryOffset] == 0)
            {
                width = 256;
            }
            else
            {
                width = bytes[entryOffset];
            }

            int height;
            if (bytes[entryOffset + 1] == 0)
            {
                height = 256;
            }
            else
            {
                height = bytes[entryOffset + 1];
            }
            Assert.Equal(width, height);

            uint payloadLength = readLittleEndianUInt32(bytes, entryOffset + 8);
            uint payloadOffset = readLittleEndianUInt32(bytes, entryOffset + 12);
            Assert.True(payloadLength > 0);
            Assert.True(payloadOffset + payloadLength <= bytes.Length);
            sizes.Add(width);
        }

        foreach (int requiredSize in REQUIRED_WINDOWS_ICON_SIZES)
        {
            Assert.Contains(requiredSize, sizes);
        }
    }

    private static int readBigEndianInt32(byte[] bytes, int offset)
    {
        return (bytes[offset] << 24) | (bytes[offset + 1] << 16) | (bytes[offset + 2] << 8) | bytes[offset + 3];
    }

    private static ushort readLittleEndianUInt16(byte[] bytes, int offset)
    {
        return (ushort)(bytes[offset] | (bytes[offset + 1] << 8));
    }

    private static uint readLittleEndianUInt32(byte[] bytes, int offset)
    {
        return (uint)(bytes[offset] | (bytes[offset + 1] << 8) | (bytes[offset + 2] << 16) | (bytes[offset + 3] << 24));
    }
}
