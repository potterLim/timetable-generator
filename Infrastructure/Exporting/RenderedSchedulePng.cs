using System;
using System.IO;

namespace TimetableGenerator.Infrastructure.Exporting;

public sealed class RenderedSchedulePng
{
    private readonly byte[] mContent;

    public SchedulePngPixelSize PixelSize { get; }

    public int ContentLength
    {
        get
        {
            return mContent.Length;
        }
    }

    internal RenderedSchedulePng(byte[] content, SchedulePngPixelSize pixelSize)
    {
        if (content == null)
        {
            throw new ArgumentNullException(nameof(content));
        }

        if (content.Length == 0)
        {
            throw new ArgumentException("Rendered PNG content cannot be empty.", nameof(content));
        }

        if (pixelSize.IsValid == false)
        {
            throw new ArgumentException("A valid PNG pixel size is required.", nameof(pixelSize));
        }

        mContent = (byte[])content.Clone();
        PixelSize = pixelSize;
    }

    public byte[] GetContentCopy()
    {
        return (byte[])mContent.Clone();
    }

    internal void writeTo(Stream destination)
    {
        if (destination == null)
        {
            throw new ArgumentNullException(nameof(destination));
        }

        destination.Write(mContent, 0, mContent.Length);
    }
}
