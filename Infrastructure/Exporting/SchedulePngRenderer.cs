using System;
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
    private const string FONT_FAMILY_NAME = "Segoe UI";

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
        Color.FromArgb(255, 242, 231),
        Color.FromArgb(255, 235, 240),
        Color.FromArgb(232, 246, 249),
    };
    private static readonly Color[] COURSE_CARD_ACCENT_COLORS = new Color[]
    {
        Color.FromArgb(15, 108, 189),
        Color.FromArgb(16, 124, 97),
        Color.FromArgb(105, 76, 180),
        Color.FromArgb(196, 92, 28),
        Color.FromArgb(190, 55, 88),
        Color.FromArgb(25, 123, 140),
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
        using (Font titleFont = new Font(FONT_FAMILY_NAME, 33.0f, FontStyle.Bold, GraphicsUnit.Pixel))
        using (Font subtitleFont = new Font(FONT_FAMILY_NAME, 17.0f, FontStyle.Regular, GraphicsUnit.Pixel))
        using (Font headerFont = new Font(FONT_FAMILY_NAME, 19.0f, FontStyle.Bold, GraphicsUnit.Pixel))
        using (Font periodFont = new Font(FONT_FAMILY_NAME, 18.0f, FontStyle.Bold, GraphicsUnit.Pixel))
        using (Font timeFont = new Font(FONT_FAMILY_NAME, 14.0f, FontStyle.Regular, GraphicsUnit.Pixel))
        using (Font courseFont = new Font(FONT_FAMILY_NAME, 18.0f, FontStyle.Bold, GraphicsUnit.Pixel))
        using (Font classroomFont = new Font(FONT_FAMILY_NAME, 14.0f, FontStyle.Regular, GraphicsUnit.Pixel))
        using (Font footerFont = new Font(FONT_FAMILY_NAME, 13.0f, FontStyle.Regular, GraphicsUnit.Pixel))
        using (StringFormat centeredFormat = createCenteredStringFormat())
        using (StringFormat leftAlignedFormat = createLeftAlignedStringFormat())
        using (StringFormat courseFormat = createCourseStringFormat())
        {
            drawTitle(graphics, scheduleGrid, titleFont, subtitleFont, leftAlignedFormat);

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
                        headerFont,
                        centeredFormat);
                    drawPeriodRows(
                        graphics,
                        scheduleGrid,
                        tableBounds,
                        periodFont,
                        timeFont,
                        courseFont,
                        classroomFont,
                        centeredFormat,
                        courseFormat,
                        cancellationToken);
                }
                finally
                {
                    graphics.Restore(tableClipState);
                }
            }

            drawTableBorder(graphics, tableBounds);
            drawFooter(graphics, pixelSize, tableBounds, footerFont, leftAlignedFormat);
        }
    }

    private static StringFormat createCenteredStringFormat()
    {
        StringFormat stringFormat = new StringFormat(StringFormat.GenericTypographic);
        stringFormat.Alignment = StringAlignment.Center;
        stringFormat.LineAlignment = StringAlignment.Center;
        stringFormat.Trimming = StringTrimming.EllipsisCharacter;
        stringFormat.FormatFlags = StringFormatFlags.NoWrap;
        return stringFormat;
    }

    private static StringFormat createLeftAlignedStringFormat()
    {
        StringFormat stringFormat = new StringFormat(StringFormat.GenericTypographic);
        stringFormat.Alignment = StringAlignment.Near;
        stringFormat.LineAlignment = StringAlignment.Center;
        stringFormat.Trimming = StringTrimming.EllipsisCharacter;
        stringFormat.FormatFlags = StringFormatFlags.NoWrap;
        return stringFormat;
    }

    private static StringFormat createCourseStringFormat()
    {
        StringFormat stringFormat = new StringFormat(StringFormat.GenericTypographic);
        stringFormat.Alignment = StringAlignment.Near;
        stringFormat.LineAlignment = StringAlignment.Near;
        stringFormat.Trimming = StringTrimming.EllipsisWord;
        stringFormat.FormatFlags = StringFormatFlags.LineLimit;
        return stringFormat;
    }

    private static void drawTitle(
        Graphics graphics,
        ScheduleGridViewModel scheduleGrid,
        Font titleFont,
        Font subtitleFont,
        StringFormat leftAlignedFormat)
    {
        float titleTop = PAGE_MARGIN_PIXELS + 8.0f;
        RectangleF titleBounds = new RectangleF(
            PAGE_MARGIN_PIXELS,
            titleTop,
            CANVAS_WIDTH_PIXELS - (PAGE_MARGIN_PIXELS * 2),
            58.0f);
        RectangleF subtitleBounds = new RectangleF(
            PAGE_MARGIN_PIXELS,
            titleTop + 62.0f,
            CANVAS_WIDTH_PIXELS - (PAGE_MARGIN_PIXELS * 2),
            34.0f);

        using (SolidBrush titleBrush = new SolidBrush(TITLE_COLOR))
        using (SolidBrush subtitleBrush = new SolidBrush(SECONDARY_TEXT_COLOR))
        using (SolidBrush accentBrush = new SolidBrush(ACCENT_COLOR))
        {
            graphics.FillRectangle(
                accentBrush,
                PAGE_MARGIN_PIXELS,
                titleTop - 1.0f,
                8.0f,
                57.0f);
            graphics.DrawString("시간표", titleFont, titleBrush, titleBounds, leftAlignedFormat);

            string summaryText = buildSummaryText(scheduleGrid);
            graphics.DrawString(
                summaryText,
                subtitleFont,
                subtitleBrush,
                subtitleBounds,
                leftAlignedFormat);
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
        using (SolidBrush surfaceBrush = new SolidBrush(SURFACE_COLOR))
        {
            graphics.FillPath(surfaceBrush, tablePath);
        }
    }

    private static void drawTableBorder(Graphics graphics, RectangleF tableBounds)
    {
        using (GraphicsPath tablePath = createRoundedRectanglePath(
            tableBounds,
            TABLE_CORNER_RADIUS_PIXELS))
        using (Pen borderPen = new Pen(GRID_LINE_COLOR, 1.0f))
        {
            graphics.DrawPath(borderPen, tablePath);
        }
    }

    private static void drawColumnHeaders(
        Graphics graphics,
        ScheduleGridViewModel scheduleGrid,
        RectangleF tableBounds,
        Font headerFont,
        StringFormat centeredFormat)
    {
        RectangleF headerBounds = new RectangleF(
            tableBounds.X,
            tableBounds.Y,
            tableBounds.Width,
            COLUMN_HEADER_HEIGHT_PIXELS);
        using (GraphicsPath headerPath = createTopRoundedRectanglePath(
            headerBounds,
            TABLE_CORNER_RADIUS_PIXELS))
        using (SolidBrush headerBackgroundBrush = new SolidBrush(HEADER_BACKGROUND_COLOR))
        using (SolidBrush headerTextBrush = new SolidBrush(TITLE_COLOR))
        using (Pen gridPen = new Pen(GRID_LINE_COLOR, 1.0f))
        {
            graphics.FillPath(headerBackgroundBrush, headerPath);

            RectangleF timeHeaderBounds = new RectangleF(
                tableBounds.X,
                tableBounds.Y,
                TIME_COLUMN_WIDTH_PIXELS,
                COLUMN_HEADER_HEIGHT_PIXELS);
            graphics.DrawString("교시", headerFont, headerTextBrush, timeHeaderBounds, centeredFormat);

            float dayColumnWidth = calculateDayColumnWidth(scheduleGrid, tableBounds);
            for (int columnIndex = 0; columnIndex < scheduleGrid.DayColumns.Count; ++columnIndex)
            {
                float columnLeft = tableBounds.X
                    + TIME_COLUMN_WIDTH_PIXELS
                    + (columnIndex * dayColumnWidth);
                RectangleF dayHeaderBounds = new RectangleF(
                    columnLeft,
                    tableBounds.Y,
                    dayColumnWidth,
                    COLUMN_HEADER_HEIGHT_PIXELS);
                string dayHeaderText = scheduleGrid.DayColumns[columnIndex].DisplayName + "요일";
                graphics.DrawString(
                    dayHeaderText,
                    headerFont,
                    headerTextBrush,
                    dayHeaderBounds,
                    centeredFormat);
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
        Font periodFont,
        Font timeFont,
        Font courseFont,
        Font classroomFont,
        StringFormat centeredFormat,
        StringFormat courseFormat,
        CancellationToken cancellationToken)
    {
        float dayColumnWidth = calculateDayColumnWidth(scheduleGrid, tableBounds);
        using (Pen gridPen = new Pen(GRID_LINE_COLOR, 1.0f))
        using (SolidBrush alternateRowBrush = new SolidBrush(ALTERNATE_ROW_COLOR))
        using (SolidBrush primaryTextBrush = new SolidBrush(TITLE_COLOR))
        using (SolidBrush secondaryTextBrush = new SolidBrush(SECONDARY_TEXT_COLOR))
        {
            for (int rowIndex = 0; rowIndex < scheduleGrid.PeriodRows.Count; ++rowIndex)
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
                    periodFont,
                    timeFont,
                    primaryTextBrush,
                    secondaryTextBrush,
                    centeredFormat);

                for (int columnIndex = 0; columnIndex < periodRow.Cells.Count; ++columnIndex)
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
                        courseFont,
                        classroomFont,
                        courseFormat);
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

    private static void drawPeriodAxis(
        Graphics graphics,
        SchedulePeriodRowViewModel periodRow,
        RectangleF rowBounds,
        Font periodFont,
        Font timeFont,
        Brush primaryTextBrush,
        Brush secondaryTextBrush,
        StringFormat centeredFormat)
    {
        RectangleF periodBounds = new RectangleF(
            rowBounds.X,
            rowBounds.Y + 18.0f,
            TIME_COLUMN_WIDTH_PIXELS,
            35.0f);
        RectangleF timeBounds = new RectangleF(
            rowBounds.X,
            rowBounds.Y + 55.0f,
            TIME_COLUMN_WIDTH_PIXELS,
            34.0f);

        string periodText = periodRow.Period.Value.ToString(CultureInfo.InvariantCulture) + "교시";
        string timeText = periodRow.TimeRange.StartTime.ToString("HH:mm", CultureInfo.InvariantCulture)
            + " – "
            + periodRow.TimeRange.EndTime.ToString("HH:mm", CultureInfo.InvariantCulture);

        graphics.DrawString(periodText, periodFont, primaryTextBrush, periodBounds, centeredFormat);
        graphics.DrawString(timeText, timeFont, secondaryTextBrush, timeBounds, centeredFormat);
    }

    private static void drawScheduleCell(
        Graphics graphics,
        ScheduleCellViewModel scheduleCell,
        RectangleF cellBounds,
        Font courseFont,
        Font classroomFont,
        StringFormat courseFormat)
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
        using (SolidBrush cardBackgroundBrush = new SolidBrush(cardBackgroundColor))
        using (SolidBrush cardAccentBrush = new SolidBrush(cardAccentColor))
        using (SolidBrush courseTextBrush = new SolidBrush(TITLE_COLOR))
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
            float contentWidth = cardBounds.Width - (COURSE_CARD_CONTENT_INSET_PIXELS * 2);
            float classroomHeight = scheduleCell.HasClassroom ? 28.0f : 0.0f;
            RectangleF courseBounds = new RectangleF(
                contentLeft,
                cardBounds.Y + 16.0f,
                contentWidth,
                cardBounds.Height - classroomHeight - 26.0f);
            graphics.DrawString(
                scheduleCell.CourseDisplayName,
                courseFont,
                courseTextBrush,
                courseBounds,
                courseFormat);

            if (scheduleCell.HasClassroom)
            {
                RectangleF classroomBounds = new RectangleF(
                    contentLeft,
                    cardBounds.Bottom - 39.0f,
                    contentWidth,
                    24.0f);
                graphics.DrawString(
                    scheduleCell.GetClassroomDisplayText(),
                    classroomFont,
                    classroomTextBrush,
                    classroomBounds,
                    courseFormat);
            }
        }
    }

    private static int getCoursePaletteIndex(ScheduleCellViewModel scheduleCell)
    {
        int choiceGroupIndex = scheduleCell.GetCourseOffering().ChoiceGroupId.Value - 1;
        return choiceGroupIndex % COURSE_CARD_BACKGROUND_COLORS.Length;
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
        Font footerFont,
        StringFormat leftAlignedFormat)
    {
        RectangleF footerBounds = new RectangleF(
            PAGE_MARGIN_PIXELS,
            tableBounds.Bottom + 18.0f,
            pixelSize.Width - (PAGE_MARGIN_PIXELS * 2),
            30.0f);
        using (SolidBrush footerTextBrush = new SolidBrush(MUTED_TEXT_COLOR))
        {
            graphics.DrawString(
                "Timetable Generator  ·  PNG 내보내기",
                footerFont,
                footerTextBrush,
                footerBounds,
                leftAlignedFormat);
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
