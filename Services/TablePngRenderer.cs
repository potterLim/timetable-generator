using System;
using System.Data;
using System.Drawing;
using System.Drawing.Imaging;

namespace TimetableGenerator
{
    public static class TablePngRenderer
    {
        public static void RenderToPng(DataTable table, string filePath)
        {
            if (table == null)
            {
                throw new ArgumentNullException(nameof(table));
            }

            int columnCount = table.Columns.Count;
            int rowCount = table.Rows.Count;

            if (columnCount <= 0)
            {
                throw new InvalidOperationException("Table has no columns.");
            }

            int timeWidth = UiConstants.TIME_COLUMN_WIDTH;

            int dayColumnCount = columnCount - 1;
            if (dayColumnCount < 0)
            {
                dayColumnCount = 0;
            }

            int dayWidth = UiConstants.GRID_MIN_DAY_COLUMN_WIDTH;
            int headerHeight = UiConstants.GRID_ROW_HEIGHT;
            int rowHeight = UiConstants.GRID_ROW_HEIGHT;

            int width = timeWidth + (dayWidth * dayColumnCount) + 2;
            int height = headerHeight + (rowHeight * rowCount) + 2;

            using (Bitmap bitmap = new Bitmap(width, height))
            using (Graphics g = Graphics.FromImage(bitmap))
            using (Pen gridPen = new Pen(Color.Black, 1))
            {
                g.Clear(UiConstants.GRID_BACKGROUND_COLOR);
                g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

                drawHeader(g, table, timeWidth, dayWidth, headerHeight, gridPen);
                drawBody(g, table, timeWidth, dayWidth, headerHeight, rowHeight, gridPen);

                bitmap.Save(filePath, ImageFormat.Png);
            }
        }

        private static void drawHeader(Graphics g, DataTable table, int timeWidth, int dayWidth, int headerHeight, Pen gridPen)
        {
            int x = 1;
            int y = 1;

            // time header
            drawCell(g, x, y, timeWidth, headerHeight, table.Columns[0].ColumnName, UiConstants.BOLD_FONT, UiConstants.GRID_BACKGROUND_COLOR, gridPen);

            x += timeWidth;

            for (int i = 1; i < table.Columns.Count; ++i)
            {
                drawCell(g, x, y, dayWidth, headerHeight, table.Columns[i].ColumnName, UiConstants.BOLD_FONT, UiConstants.GRID_BACKGROUND_COLOR, gridPen);
                x += dayWidth;
            }
        }

        private static void drawBody(Graphics g, DataTable table, int timeWidth, int dayWidth, int headerHeight, int rowHeight, Pen gridPen)
        {
            for (int row = 0; row < table.Rows.Count; ++row)
            {
                int x = 1;
                int y = 1 + headerHeight + (row * rowHeight);

                Color bg = (row % 2 == 1) ? UiConstants.ALT_ROW_COLOR : UiConstants.GRID_BACKGROUND_COLOR;

                drawCell(g, x, y, timeWidth, rowHeight, safeToString(table.Rows[row][0]), UiConstants.BOLD_FONT, bg, gridPen);
                x += timeWidth;

                for (int col = 1; col < table.Columns.Count; ++col)
                {
                    drawCell(g, x, y, dayWidth, rowHeight, safeToString(table.Rows[row][col]), UiConstants.DEFAULT_FONT, bg, gridPen);
                    x += dayWidth;
                }
            }
        }

        private static void drawCell(Graphics g, int x, int y, int w, int h, string text, Font font, Color bg, Pen gridPen)
        {
            using (SolidBrush bgBrush = new SolidBrush(bg))
            using (SolidBrush textBrush = new SolidBrush(Color.Black))
            {
                g.FillRectangle(bgBrush, x, y, w, h);
                g.DrawRectangle(gridPen, x, y, w, h);

                Rectangle rect = new Rectangle(x, y, w, h);

                StringFormat format = new StringFormat();
                format.Alignment = StringAlignment.Center;
                format.LineAlignment = StringAlignment.Center;
                format.Trimming = StringTrimming.EllipsisCharacter;
                format.FormatFlags = StringFormatFlags.LineLimit;

                g.DrawString(text ?? string.Empty, font, textBrush, rect, format);
            }
        }

        private static string safeToString(object value)
        {
            if (value == null)
            {
                return string.Empty;
            }

            return value.ToString() ?? string.Empty;
        }
    }
}
