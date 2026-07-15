using System;
using System.Collections.Generic;
using System.Drawing;

namespace TimetableGenerator.Infrastructure.Exporting;

internal sealed class SchedulePngRenderResources : IDisposable
{
    private const string FONT_FAMILY_NAME = "Segoe UI";
    private const float TITLE_FONT_SIZE_PIXELS = 33.0f;
    private const float SUBTITLE_FONT_SIZE_PIXELS = 17.0f;
    private const float COLUMN_HEADER_FONT_SIZE_PIXELS = 19.0f;
    private const float PERIOD_LABEL_FONT_SIZE_PIXELS = 18.0f;
    private const float TIME_RANGE_FONT_SIZE_PIXELS = 14.0f;
    private const float COURSE_NAME_FONT_SIZE_PIXELS = 18.0f;
    private const float CLASSROOM_FONT_SIZE_PIXELS = 14.0f;
    private const float FOOTER_FONT_SIZE_PIXELS = 13.0f;

    private readonly List<IDisposable> mOwnedResources;
    private bool mIsDisposed;

    internal Font TitleFont { get; }
    internal Font SubtitleFont { get; }
    internal Font ColumnHeaderFont { get; }
    internal Font PeriodLabelFont { get; }
    internal Font TimeRangeFont { get; }
    internal Font CourseNameFont { get; }
    internal Font ClassroomFont { get; }
    internal Font FooterFont { get; }
    internal StringFormat CenteredTextFormat { get; }
    internal StringFormat LeftAlignedTextFormat { get; }
    internal StringFormat CourseTextFormat { get; }

    internal SchedulePngRenderResources()
    {
        mOwnedResources = new List<IDisposable>();

        try
        {
            TitleFont = ownResource(new Font(
                FONT_FAMILY_NAME,
                TITLE_FONT_SIZE_PIXELS,
                FontStyle.Bold,
                GraphicsUnit.Pixel));
            SubtitleFont = ownResource(new Font(
                FONT_FAMILY_NAME,
                SUBTITLE_FONT_SIZE_PIXELS,
                FontStyle.Regular,
                GraphicsUnit.Pixel));
            ColumnHeaderFont = ownResource(new Font(
                FONT_FAMILY_NAME,
                COLUMN_HEADER_FONT_SIZE_PIXELS,
                FontStyle.Bold,
                GraphicsUnit.Pixel));
            PeriodLabelFont = ownResource(new Font(
                FONT_FAMILY_NAME,
                PERIOD_LABEL_FONT_SIZE_PIXELS,
                FontStyle.Bold,
                GraphicsUnit.Pixel));
            TimeRangeFont = ownResource(new Font(
                FONT_FAMILY_NAME,
                TIME_RANGE_FONT_SIZE_PIXELS,
                FontStyle.Regular,
                GraphicsUnit.Pixel));
            CourseNameFont = ownResource(new Font(
                FONT_FAMILY_NAME,
                COURSE_NAME_FONT_SIZE_PIXELS,
                FontStyle.Bold,
                GraphicsUnit.Pixel));
            ClassroomFont = ownResource(new Font(
                FONT_FAMILY_NAME,
                CLASSROOM_FONT_SIZE_PIXELS,
                FontStyle.Regular,
                GraphicsUnit.Pixel));
            FooterFont = ownResource(new Font(
                FONT_FAMILY_NAME,
                FOOTER_FONT_SIZE_PIXELS,
                FontStyle.Regular,
                GraphicsUnit.Pixel));
            CenteredTextFormat = ownResource(createCenteredTextFormat());
            LeftAlignedTextFormat = ownResource(createLeftAlignedTextFormat());
            CourseTextFormat = ownResource(createCourseTextFormat());
        }
        catch
        {
            disposeOwnedResources();
            throw;
        }
    }

    public void Dispose()
    {
        if (mIsDisposed)
        {
            return;
        }

        disposeOwnedResources();
        mIsDisposed = true;
    }

    private TResource ownResource<TResource>(TResource resource)
        where TResource : IDisposable
    {
        mOwnedResources.Add(resource);
        return resource;
    }

    private static StringFormat createCenteredTextFormat()
    {
        StringFormat stringFormat = new StringFormat(StringFormat.GenericTypographic);
        stringFormat.Alignment = StringAlignment.Center;
        stringFormat.LineAlignment = StringAlignment.Center;
        stringFormat.Trimming = StringTrimming.EllipsisCharacter;
        stringFormat.FormatFlags = StringFormatFlags.NoWrap;
        return stringFormat;
    }

    private static StringFormat createLeftAlignedTextFormat()
    {
        StringFormat stringFormat = new StringFormat(StringFormat.GenericTypographic);
        stringFormat.Alignment = StringAlignment.Near;
        stringFormat.LineAlignment = StringAlignment.Center;
        stringFormat.Trimming = StringTrimming.EllipsisCharacter;
        stringFormat.FormatFlags = StringFormatFlags.NoWrap;
        return stringFormat;
    }

    private static StringFormat createCourseTextFormat()
    {
        StringFormat stringFormat = new StringFormat(StringFormat.GenericTypographic);
        stringFormat.Alignment = StringAlignment.Near;
        stringFormat.LineAlignment = StringAlignment.Near;
        stringFormat.Trimming = StringTrimming.EllipsisWord;
        stringFormat.FormatFlags = StringFormatFlags.LineLimit;
        return stringFormat;
    }

    private void disposeOwnedResources()
    {
        for (int resourceIndex = mOwnedResources.Count - 1; resourceIndex >= 0; --resourceIndex)
        {
            mOwnedResources[resourceIndex].Dispose();
        }

        mOwnedResources.Clear();
    }
}
