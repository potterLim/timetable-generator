using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;
using TimetableGenerator.Presentation.Schedules;
using CoreDay = TimetableGenerator.Core.Domain.EDay;

namespace TimetableGenerator.Infrastructure.Exporting;

public sealed class SchedulePngRenderer
{
    private const int CANVAS_WIDTH_PIXELS = 1_800;
    private const int PAGE_MARGIN_PIXELS = 72;
    private const int TITLE_AREA_HEIGHT_PIXELS = 168;
    private const int COLUMN_HEADER_HEIGHT_PIXELS = 84;
    private const int PERIOD_ROW_HEIGHT_PIXELS = 128;
    private const int TIME_COLUMN_WIDTH_PIXELS = 210;
    private const int FOOTER_HEIGHT_PIXELS = 64;
    private const int COURSE_CARD_INSET_PIXELS = 7;
    private const int COURSE_CARD_CORNER_RADIUS_PIXELS = 14;
    private const int COURSE_CARD_ACCENT_WIDTH_PIXELS = 6;
    private const int COURSE_CARD_CONTENT_INSET_PIXELS = 18;
    private const int TABLE_CORNER_RADIUS_PIXELS = 18;
    private const int PNG_DPI = 144;
    private const float GRID_LINE_WIDTH_PIXELS = 1.0f;
    private const float TITLE_TOP_INSET_PIXELS = 8.0f;
    private const float TITLE_TEXT_HEIGHT_PIXELS = 58.0f;
    private const float SUBTITLE_TOP_OFFSET_PIXELS = 62.0f;
    private const float SUBTITLE_TEXT_HEIGHT_PIXELS = 34.0f;
    private const float TITLE_ACCENT_TOP_OFFSET_PIXELS = -1.0f;
    private const float TITLE_ACCENT_WIDTH_PIXELS = 8.0f;
    private const float TITLE_ACCENT_HEIGHT_PIXELS = 57.0f;
    private const float PERIOD_LABEL_TOP_INSET_PIXELS = 18.0f;
    private const float PERIOD_LABEL_HEIGHT_PIXELS = 35.0f;
    private const float TIME_RANGE_TOP_INSET_PIXELS = 55.0f;
    private const float TIME_RANGE_HEIGHT_PIXELS = 34.0f;
    private const float COURSE_NAME_TOP_INSET_PIXELS = 16.0f;
    private const float COURSE_NAME_BOTTOM_INSET_PIXELS = 26.0f;
    private const float CLASSROOM_RESERVED_HEIGHT_PIXELS = 28.0f;
    private const float CLASSROOM_BOTTOM_INSET_PIXELS = 39.0f;
    private const float CLASSROOM_TEXT_HEIGHT_PIXELS = 24.0f;
    private const float FOOTER_TOP_INSET_PIXELS = 18.0f;
    private const float FOOTER_TEXT_HEIGHT_PIXELS = 30.0f;

    private static readonly Color PAGE_BACKGROUND_COLOR = Color.FromArgb(247, 249, 252);
    private static readonly Color SURFACE_COLOR = Color.White;
    private static readonly Color TITLE_COLOR = Color.FromArgb(28, 37, 51);
    private static readonly Color SECONDARY_TEXT_COLOR = Color.FromArgb(91, 103, 120);
    private static readonly Color MUTED_TEXT_COLOR = Color.FromArgb(119, 130, 145);
    private static readonly Color ACCENT_COLOR = Color.FromArgb(15, 108, 189);
    private static readonly Color HEADER_BACKGROUND_COLOR = Color.FromArgb(243, 247, 251);
    private static readonly Color ALTERNATE_ROW_COLOR = Color.FromArgb(250, 251, 253);
    private static readonly Color GRID_LINE_COLOR = Color.FromArgb(222, 228, 236);
    private static readonly Color[] COURSE_CARD_BACKGROUND_COLORS = new Color[]
    {
        Color.FromArgb(231, 243, 255),
        Color.FromArgb(232, 247, 242),
        Color.FromArgb(242, 237, 255),
    };
    private static readonly Color[] COURSE_CARD_ACCENT_COLORS = new Color[]
    {
        Color.FromArgb(15, 108, 189),
        Color.FromArgb(16, 124, 97),
        Color.FromArgb(105, 76, 180),
    };

    public RenderedSchedulePng Render(ScheduleGridViewModel scheduleGrid)
    {
        return Render(scheduleGrid, CancellationToken.None);
    }

    public RenderedSchedulePng Render(
        ScheduleGridViewModel scheduleGrid,
        CancellationToken cancellationToken)
    {
        if (scheduleGrid == null)
        {
            throw new ArgumentNullException(nameof(scheduleGrid));
        }

        cancellationToken.ThrowIfCancellationRequested();

        int canvasHeightPixels = calculateCanvasHeightPixels(scheduleGrid);
        SchedulePngPixelSize pixelSize = new SchedulePngPixelSize(
            CANVAS_WIDTH_PIXELS,
            canvasHeightPixels);

        using (Bitmap bitmap = new Bitmap(
            pixelSize.Width,
            pixelSize.Height,
            PixelFormat.Format32bppPArgb))
        {
            bitmap.SetResolution(PNG_DPI, PNG_DPI);
            using (Graphics graphics = Graphics.FromImage(bitmap))
            {
                configureGraphics(graphics);
                drawSchedule(graphics, scheduleGrid, pixelSize, cancellationToken);
            }

            cancellationToken.ThrowIfCancellationRequested();

            using (MemoryStream pngStream = new MemoryStream())
            {
                bitmap.Save(pngStream, ImageFormat.Png);
                return new RenderedSchedulePng(pngStream.ToArray(), pixelSize);
            }
        }
    }

    private static int calculateCanvasHeightPixels(ScheduleGridViewModel scheduleGrid)
    {
        int periodRowsHeightPixels = scheduleGrid.PeriodRows.Count * PERIOD_ROW_HEIGHT_PIXELS;
        return (PAGE_MARGIN_PIXELS * 2)
            + TITLE_AREA_HEIGHT_PIXELS
            + COLUMN_HEADER_HEIGHT_PIXELS
            + periodRowsHeightPixels
            + FOOTER_HEIGHT_PIXELS;
    }

    private static void configureGraphics(Graphics graphics)
    {
        graphics.Clear(PAGE_BACKGROUND_COLOR);
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        graphics.CompositingQuality = CompositingQuality.HighQuality;
        graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
        graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
        graphics.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
    }

    private static void drawSchedule(
        Graphics graphics,
        ScheduleGridViewModel scheduleGrid,
        SchedulePngPixelSize pixelSize,
        CancellationToken cancellationToken)
    {
        using (SchedulePngRenderResources resources = new SchedulePngRenderResources())
        {
            drawTitle(graphics, scheduleGrid, resources);

            RectangleF tableBounds = getTableBounds(scheduleGrid);
            drawTableSurface(graphics, tableBounds);
            using (GraphicsPath tableClipPath = createRoundedRectanglePath(
                tableBounds,
                TABLE_CORNER_RADIUS_PIXELS))
            {
                GraphicsState tableClipState = graphics.Save();
                try
                {
                    graphics.SetClip(tableClipPath, CombineMode.Intersect);
                    drawColumnHeaders(
                        graphics,
                        scheduleGrid,
                        tableBounds,
                        resources);
                    drawPeriodRows(
                        graphics,
                        scheduleGrid,
                        tableBounds,
                        resources,
                        cancellationToken);
                }
                finally
                {
                    graphics.Restore(tableClipState);
                }
            }

            drawTableBorder(graphics, tableBounds);
            drawFooter(graphics, pixelSize, tableBounds, resources);
        }
    }

    private static void drawTitle(
        Graphics graphics,
        ScheduleGridViewModel scheduleGrid,
        SchedulePngRenderResources resources)
    {
        float titleTop = PAGE_MARGIN_PIXELS + TITLE_TOP_INSET_PIXELS;
        RectangleF titleBounds = new RectangleF(
            PAGE_MARGIN_PIXELS,
            titleTop,
            CANVAS_WIDTH_PIXELS - (PAGE_MARGIN_PIXELS * 2),
            TITLE_TEXT_HEIGHT_PIXELS);
        RectangleF subtitleBounds = new RectangleF(
            PAGE_MARGIN_PIXELS,
            titleTop + SUBTITLE_TOP_OFFSET_PIXELS,
            CANVAS_WIDTH_PIXELS - (PAGE_MARGIN_PIXELS * 2),
            SUBTITLE_TEXT_HEIGHT_PIXELS);

        using (SolidBrush titleBrush = new SolidBrush(TITLE_COLOR))
        {
            using (SolidBrush subtitleBrush = new SolidBrush(SECONDARY_TEXT_COLOR))
            {
                using (SolidBrush accentBrush = new SolidBrush(ACCENT_COLOR))
                {
                    graphics.FillRectangle(
                        accentBrush,
                        PAGE_MARGIN_PIXELS,
                        titleTop + TITLE_ACCENT_TOP_OFFSET_PIXELS,
                        TITLE_ACCENT_WIDTH_PIXELS,
                        TITLE_ACCENT_HEIGHT_PIXELS);
                    graphics.DrawString(
                        "시간표",
                        resources.TitleFont,
                        titleBrush,
                        titleBounds,
                        resources.LeftAlignedTextFormat);

                    string summaryText = buildSummaryText(scheduleGrid);
                    graphics.DrawString(
                        summaryText,
                        resources.SubtitleFont,
                        subtitleBrush,
                        subtitleBounds,
                        resources.LeftAlignedTextFormat);
                }
            }
        }
    }

    private static string buildSummaryText(ScheduleGridViewModel scheduleGrid)
    {
        StringBuilder summaryBuilder = new StringBuilder();
        summaryBuilder.Append(scheduleGrid.Summary.SelectedCourseCount.ToString(CultureInfo.InvariantCulture));
        summaryBuilder.Append("개 과목  ·  ");
        summaryBuilder.Append(scheduleGrid.Summary.ScheduledMeetingCount.ToString(CultureInfo.InvariantCulture));
        summaryBuilder.Append("회 수업  ·  ");

        for (int dayIndex = 0; dayIndex < scheduleGrid.Summary.ActiveDays.Count; ++dayIndex)
        {
            if (dayIndex > 0)
            {
                summaryBuilder.Append(", ");
            }

            summaryBuilder.Append(getDayDisplayName(scheduleGrid.Summary.ActiveDays[dayIndex]));
        }

        return summaryBuilder.ToString();
    }

    private static string getDayDisplayName(CoreDay day)
    {
        switch (day)
        {
            case CoreDay.Monday:
                return "월요일";
            case CoreDay.Tuesday:
                return "화요일";
            case CoreDay.Wednesday:
                return "수요일";
            case CoreDay.Thursday:
                return "목요일";
            case CoreDay.Friday:
                return "금요일";
            case CoreDay.Saturday:
                return "토요일";
            case CoreDay.Sunday:
                return "일요일";
            case CoreDay.None:
            default:
                Debug.Fail("Unexpected schedule day: " + day);
                throw new ArgumentOutOfRangeException(nameof(day));
        }
    }

    private static RectangleF getTableBounds(ScheduleGridViewModel scheduleGrid)
    {
        float tableTop = PAGE_MARGIN_PIXELS + TITLE_AREA_HEIGHT_PIXELS;
        float tableHeight = COLUMN_HEADER_HEIGHT_PIXELS
            + (scheduleGrid.PeriodRows.Count * PERIOD_ROW_HEIGHT_PIXELS);
        return new RectangleF(
            PAGE_MARGIN_PIXELS,
            tableTop,
            CANVAS_WIDTH_PIXELS - (PAGE_MARGIN_PIXELS * 2),
            tableHeight);
    }

    private static void drawTableSurface(Graphics graphics, RectangleF tableBounds)
    {
        using (GraphicsPath tablePath = createRoundedRectanglePath(
            tableBounds,
            TABLE_CORNER_RADIUS_PIXELS))
        {
            using (SolidBrush surfaceBrush = new SolidBrush(SURFACE_COLOR))
            {
                graphics.FillPath(surfaceBrush, tablePath);
            }
        }
    }

    private static void drawTableBorder(Graphics graphics, RectangleF tableBounds)
    {
        using (GraphicsPath tablePath = createRoundedRectanglePath(
            tableBounds,
            TABLE_CORNER_RADIUS_PIXELS))
        {
            using (Pen borderPen = new Pen(GRID_LINE_COLOR, GRID_LINE_WIDTH_PIXELS))
            {
                graphics.DrawPath(borderPen, tablePath);
            }
        }
    }

    private static void drawColumnHeaders(
        Graphics graphics,
        ScheduleGridViewModel scheduleGrid,
        RectangleF tableBounds,
        SchedulePngRenderResources resources)
    {
        RectangleF headerBounds = new RectangleF(
            tableBounds.X,
            tableBounds.Y,
            tableBounds.Width,
            COLUMN_HEADER_HEIGHT_PIXELS);
        using (GraphicsPath headerPath = createTopRoundedRectanglePath(
            headerBounds,
            TABLE_CORNER_RADIUS_PIXELS))
        {
            using (SolidBrush headerBackgroundBrush = new SolidBrush(HEADER_BACKGROUND_COLOR))
            {
                using (SolidBrush headerTextBrush = new SolidBrush(TITLE_COLOR))
                {
                    using (Pen gridPen = new Pen(GRID_LINE_COLOR, GRID_LINE_WIDTH_PIXELS))
                    {
                        graphics.FillPath(headerBackgroundBrush, headerPath);

                        RectangleF timeHeaderBounds = new RectangleF(
                            tableBounds.X,
                            tableBounds.Y,
                            TIME_COLUMN_WIDTH_PIXELS,
                            COLUMN_HEADER_HEIGHT_PIXELS);
                        graphics.DrawString(
                            "교시",
                            resources.ColumnHeaderFont,
                            headerTextBrush,
                            timeHeaderBounds,
                            resources.CenteredTextFormat);

                        float dayColumnWidth = calculateDayColumnWidth(scheduleGrid, tableBounds);
                        for (int columnIndex = 0;
                            columnIndex < scheduleGrid.DayColumns.Count;
                            ++columnIndex)
                        {
                            float columnLeft = tableBounds.X
                                + TIME_COLUMN_WIDTH_PIXELS
                                + (columnIndex * dayColumnWidth);
                            RectangleF dayHeaderBounds = new RectangleF(
                                columnLeft,
                                tableBounds.Y,
                                dayColumnWidth,
                                COLUMN_HEADER_HEIGHT_PIXELS);
                            string dayHeaderText = scheduleGrid.DayColumns[columnIndex].DisplayName
                                + "요일";
                            graphics.DrawString(
                                dayHeaderText,
                                resources.ColumnHeaderFont,
                                headerTextBrush,
                                dayHeaderBounds,
                                resources.CenteredTextFormat);
                            graphics.DrawLine(
                                gridPen,
                                columnLeft,
                                tableBounds.Y,
                                columnLeft,
                                tableBounds.Bottom);
                        }

                        graphics.DrawLine(
                            gridPen,
                            tableBounds.X,
                            tableBounds.Y + COLUMN_HEADER_HEIGHT_PIXELS,
                            tableBounds.Right,
                            tableBounds.Y + COLUMN_HEADER_HEIGHT_PIXELS);
                    }
                }
            }
        }
    }

    private static float calculateDayColumnWidth(
        ScheduleGridViewModel scheduleGrid,
        RectangleF tableBounds)
    {
        float dayColumnsWidth = tableBounds.Width - TIME_COLUMN_WIDTH_PIXELS;
        return dayColumnsWidth / scheduleGrid.DayColumns.Count;
    }

    private static void drawPeriodRows(
        Graphics graphics,
        ScheduleGridViewModel scheduleGrid,
        RectangleF tableBounds,
        SchedulePngRenderResources resources,
        CancellationToken cancellationToken)
    {
        float dayColumnWidth = calculateDayColumnWidth(scheduleGrid, tableBounds);
        using (Pen gridPen = new Pen(GRID_LINE_COLOR, GRID_LINE_WIDTH_PIXELS))
        {
            using (SolidBrush alternateRowBrush = new SolidBrush(ALTERNATE_ROW_COLOR))
            {
                using (SolidBrush primaryTextBrush = new SolidBrush(TITLE_COLOR))
                {
                    using (SolidBrush secondaryTextBrush = new SolidBrush(SECONDARY_TEXT_COLOR))
                    {
                        for (int rowIndex = 0;
                            rowIndex < scheduleGrid.PeriodRows.Count;
                            ++rowIndex)
                        {
                            cancellationToken.ThrowIfCancellationRequested();

                            SchedulePeriodRowViewModel periodRow = scheduleGrid.PeriodRows[rowIndex];
                            float rowTop = tableBounds.Y
                                + COLUMN_HEADER_HEIGHT_PIXELS
                                + (rowIndex * PERIOD_ROW_HEIGHT_PIXELS);
                            RectangleF rowBounds = new RectangleF(
                                tableBounds.X,
                                rowTop,
                                tableBounds.Width,
                                PERIOD_ROW_HEIGHT_PIXELS);

                            if (rowIndex % 2 == 1)
                            {
                                graphics.FillRectangle(alternateRowBrush, rowBounds);
                            }

                            drawPeriodAxis(
                                graphics,
                                periodRow,
                                rowBounds,
                                resources,
                                primaryTextBrush,
                                secondaryTextBrush);

                            for (int columnIndex = 0;
                                columnIndex < periodRow.Cells.Count;
                                ++columnIndex)
                            {
                                cancellationToken.ThrowIfCancellationRequested();

                                float cellLeft = tableBounds.X
                                    + TIME_COLUMN_WIDTH_PIXELS
                                    + (columnIndex * dayColumnWidth);
                                RectangleF cellBounds = new RectangleF(
                                    cellLeft,
                                    rowTop,
                                    dayColumnWidth,
                                    PERIOD_ROW_HEIGHT_PIXELS);
                                drawScheduleCell(
                                    graphics,
                                    periodRow.Cells[columnIndex],
                                    cellBounds,
                                    resources);
                            }

                            if (rowIndex < scheduleGrid.PeriodRows.Count - 1)
                            {
                                graphics.DrawLine(
                                    gridPen,
                                    tableBounds.X,
                                    rowBounds.Bottom,
                                    tableBounds.Right,
                                    rowBounds.Bottom);
                            }
                        }
                    }
                }
            }
        }
    }

    private static void drawPeriodAxis(
        Graphics graphics,
        SchedulePeriodRowViewModel periodRow,
        RectangleF rowBounds,
        SchedulePngRenderResources resources,
        Brush primaryTextBrush,
        Brush secondaryTextBrush)
    {
        RectangleF periodBounds = new RectangleF(
            rowBounds.X,
            rowBounds.Y + PERIOD_LABEL_TOP_INSET_PIXELS,
            TIME_COLUMN_WIDTH_PIXELS,
            PERIOD_LABEL_HEIGHT_PIXELS);
        RectangleF timeBounds = new RectangleF(
            rowBounds.X,
            rowBounds.Y + TIME_RANGE_TOP_INSET_PIXELS,
            TIME_COLUMN_WIDTH_PIXELS,
            TIME_RANGE_HEIGHT_PIXELS);

        string periodText = periodRow.Period.Value.ToString(CultureInfo.InvariantCulture) + "교시";
        string timeText = periodRow.TimeRange.StartTime.ToString("HH:mm", CultureInfo.InvariantCulture)
            + " – "
            + periodRow.TimeRange.EndTime.ToString("HH:mm", CultureInfo.InvariantCulture);

        graphics.DrawString(
            periodText,
            resources.PeriodLabelFont,
            primaryTextBrush,
            periodBounds,
            resources.CenteredTextFormat);
        graphics.DrawString(
            timeText,
            resources.TimeRangeFont,
            secondaryTextBrush,
            timeBounds,
            resources.CenteredTextFormat);
    }

    private static void drawScheduleCell(
        Graphics graphics,
        ScheduleCellViewModel scheduleCell,
        RectangleF cellBounds,
        SchedulePngRenderResources resources)
    {
        if (scheduleCell.HasCourseOffering == false)
        {
            return;
        }

        int paletteIndex = getCoursePaletteIndex(scheduleCell);
        Color cardBackgroundColor = COURSE_CARD_BACKGROUND_COLORS[paletteIndex];
        Color cardAccentColor = COURSE_CARD_ACCENT_COLORS[paletteIndex];
        RectangleF cardBounds = insetRectangle(cellBounds, COURSE_CARD_INSET_PIXELS);

        using (GraphicsPath cardPath = createRoundedRectanglePath(
            cardBounds,
            COURSE_CARD_CORNER_RADIUS_PIXELS))
        {
            using (SolidBrush cardBackgroundBrush = new SolidBrush(cardBackgroundColor))
            {
                using (SolidBrush cardAccentBrush = new SolidBrush(cardAccentColor))
                {
                    using (SolidBrush courseTextBrush = new SolidBrush(TITLE_COLOR))
                    {
                        using (SolidBrush classroomTextBrush = new SolidBrush(SECONDARY_TEXT_COLOR))
                        {
                            graphics.FillPath(cardBackgroundBrush, cardPath);
                            RectangleF accentBounds = new RectangleF(
                                cardBounds.X,
                                cardBounds.Y,
                                COURSE_CARD_ACCENT_WIDTH_PIXELS,
                                cardBounds.Height);
                            GraphicsState cardClipState = graphics.Save();
                            try
                            {
                                graphics.SetClip(cardPath, CombineMode.Intersect);
                                graphics.FillRectangle(cardAccentBrush, accentBounds);
                            }
                            finally
                            {
                                graphics.Restore(cardClipState);
                            }

                            float contentLeft = cardBounds.X + COURSE_CARD_CONTENT_INSET_PIXELS;
                            float contentWidth = cardBounds.Width
                                - (COURSE_CARD_CONTENT_INSET_PIXELS * 2);
                            float classroomReservedHeight = scheduleCell.HasClassroom
                                ? CLASSROOM_RESERVED_HEIGHT_PIXELS
                                : 0.0f;
                            RectangleF courseBounds = new RectangleF(
                                contentLeft,
                                cardBounds.Y + COURSE_NAME_TOP_INSET_PIXELS,
                                contentWidth,
                                cardBounds.Height
                                    - classroomReservedHeight
                                    - COURSE_NAME_BOTTOM_INSET_PIXELS);
                            graphics.DrawString(
                                scheduleCell.CourseDisplayName,
                                resources.CourseNameFont,
                                courseTextBrush,
                                courseBounds,
                                resources.CourseTextFormat);

                            if (scheduleCell.HasClassroom)
                            {
                                RectangleF classroomBounds = new RectangleF(
                                    contentLeft,
                                    cardBounds.Bottom - CLASSROOM_BOTTOM_INSET_PIXELS,
                                    contentWidth,
                                    CLASSROOM_TEXT_HEIGHT_PIXELS);
                                graphics.DrawString(
                                    scheduleCell.GetClassroomDisplayText(),
                                    resources.ClassroomFont,
                                    classroomTextBrush,
                                    classroomBounds,
                                    resources.CourseTextFormat);
                            }
                        }
                    }
                }
            }
        }
    }

    private static int getCoursePaletteIndex(ScheduleCellViewModel scheduleCell)
    {
        EScheduleCourseColor courseColor = ScheduleCourseColorPolicy.findColor(
            scheduleCell.GetCourseOffering().ChoiceGroupId);
        switch (courseColor)
        {
            case EScheduleCourseColor.Blue:
                return 0;
            case EScheduleCourseColor.Green:
                return 1;
            case EScheduleCourseColor.Purple:
                return 2;
            default:
                Debug.Fail("Unexpected schedule course color: " + courseColor);
                throw new ArgumentOutOfRangeException(nameof(courseColor));
        }
    }

    private static RectangleF insetRectangle(RectangleF bounds, float inset)
    {
        return new RectangleF(
            bounds.X + inset,
            bounds.Y + inset,
            bounds.Width - (inset * 2.0f),
            bounds.Height - (inset * 2.0f));
    }

    private static void drawFooter(
        Graphics graphics,
        SchedulePngPixelSize pixelSize,
        RectangleF tableBounds,
        SchedulePngRenderResources resources)
    {
        RectangleF footerBounds = new RectangleF(
            PAGE_MARGIN_PIXELS,
            tableBounds.Bottom + FOOTER_TOP_INSET_PIXELS,
            pixelSize.Width - (PAGE_MARGIN_PIXELS * 2),
            FOOTER_TEXT_HEIGHT_PIXELS);
        using (SolidBrush footerTextBrush = new SolidBrush(MUTED_TEXT_COLOR))
        {
            graphics.DrawString(
                "Timetable Generator  ·  PNG 내보내기",
                resources.FooterFont,
                footerTextBrush,
                footerBounds,
                resources.LeftAlignedTextFormat);
        }
    }

    private static GraphicsPath createTopRoundedRectanglePath(RectangleF bounds, float radius)
    {
        float diameter = radius * 2.0f;
        GraphicsPath path = new GraphicsPath();
        path.AddArc(bounds.Left, bounds.Top, diameter, diameter, 180.0f, 90.0f);
        path.AddArc(bounds.Right - diameter, bounds.Top, diameter, diameter, 270.0f, 90.0f);
        path.AddLine(bounds.Right, bounds.Bottom, bounds.Left, bounds.Bottom);
        path.CloseFigure();
        return path;
    }

    private static GraphicsPath createRoundedRectanglePath(RectangleF bounds, float radius)
    {
        float diameter = radius * 2.0f;
        GraphicsPath path = new GraphicsPath();
        path.AddArc(bounds.Left, bounds.Top, diameter, diameter, 180.0f, 90.0f);
        path.AddArc(bounds.Right - diameter, bounds.Top, diameter, diameter, 270.0f, 90.0f);
        path.AddArc(
            bounds.Right - diameter,
            bounds.Bottom - diameter,
            diameter,
            diameter,
            0.0f,
            90.0f);
        path.AddArc(bounds.Left, bounds.Bottom - diameter, diameter, diameter, 90.0f, 90.0f);
        path.CloseFigure();
        return path;
    }
}
