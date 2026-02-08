using System.Drawing;
using System.Windows.Forms;

namespace TimetableGenerator
{
    // Creates and styles the schedule grid.
    // This grid disables scrollbars and user resizing; the Form enforces minimum size
    // so the entire grid remains visible.
    public sealed class ScheduleGridPresenter
    {
        public DataGridView CreateGrid()
        {
            DataGridView grid = new DataGridView();
            grid.Dock = DockStyle.Fill;
            grid.ReadOnly = true;
            grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None;

            grid.BorderStyle = BorderStyle.FixedSingle;
            grid.BackgroundColor = UiConstants.GRID_BACKGROUND_COLOR;

            grid.AllowUserToAddRows = false;
            grid.AllowUserToResizeRows = false;
            grid.AllowUserToResizeColumns = false;

            grid.RowHeadersVisible = false;
            grid.ColumnHeadersVisible = false;

            grid.RowTemplate.Height = UiConstants.GRID_ROW_HEIGHT;

            grid.MultiSelect = false;
            grid.SelectionMode = DataGridViewSelectionMode.CellSelect;
            grid.ScrollBars = ScrollBars.None;

            DataGridViewCellStyle style = new DataGridViewCellStyle();
            style.Font = UiConstants.DEFAULT_FONT;
            style.Alignment = DataGridViewContentAlignment.MiddleCenter;
            style.BackColor = UiConstants.GRID_BACKGROUND_COLOR;
            style.ForeColor = Color.Black;
            style.SelectionBackColor = UiConstants.SELECTION_BACK_COLOR;
            style.SelectionForeColor = UiConstants.SELECTION_FORE_COLOR;
            style.WrapMode = DataGridViewTriState.True;

            grid.DefaultCellStyle = style;

            // Stripe policy (start from row 1 as White, then alternate):
            // DataGridView alternation starts at row index 0, so invert the defaults.
            grid.RowsDefaultCellStyle.BackColor = UiConstants.ALT_ROW_COLOR;
            grid.AlternatingRowsDefaultCellStyle.BackColor = UiConstants.GRID_BACKGROUND_COLOR;

            grid.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.None;
            grid.CellPainting += onGridCellPainting;

            return grid;
        }

        public void ApplySizingAndHeaderStyle(DataGridView grid)
        {
            if (grid == null)
            {
                return;
            }

            applyColumnSizing(grid);
            applyHeaderAndAxisStyle(grid);
        }

        private void applyColumnSizing(DataGridView grid)
        {
            if (grid.Columns == null || grid.Columns.Count <= 0)
            {
                return;
            }

            int totalWidth = grid.ClientSize.Width;
            if (totalWidth <= 0)
            {
                return;
            }

            int dayColumnCount = grid.Columns.Count - 1;
            if (dayColumnCount <= 0)
            {
                grid.Columns[0].Width = totalWidth;
                return;
            }

            int remainingWidth = totalWidth - UiConstants.TIME_COLUMN_WIDTH;
            if (remainingWidth <= 0)
            {
                grid.Columns[0].Width = totalWidth;
                return;
            }

            int dayWidth = remainingWidth / dayColumnCount;
            if (dayWidth < UiConstants.GRID_MIN_DAY_COLUMN_WIDTH)
            {
                dayWidth = UiConstants.GRID_MIN_DAY_COLUMN_WIDTH;
            }

            for (int i = 1; i < grid.Columns.Count; ++i)
            {
                grid.Columns[i].Width = dayWidth;
            }

            int usedByDays = dayWidth * dayColumnCount;
            int remainder = remainingWidth - usedByDays;

            grid.Columns[0].Width = UiConstants.TIME_COLUMN_WIDTH + remainder;
        }

        private void applyHeaderAndAxisStyle(DataGridView grid)
        {
            if (grid.Rows == null || grid.Rows.Count <= 0 || grid.Columns == null || grid.Columns.Count <= 0)
            {
                return;
            }

            int columnCount = grid.Columns.Count;

            foreach (DataGridViewRow row in grid.Rows)
            {
                // Axis column (col 0): header/axis background for all rows
                row.Cells[0].Style.BackColor = UiConstants.HEADER_AXIS_BACK_COLOR;
                row.Cells[0].Style.Font = UiConstants.BOLD_FONT;

                // Header row (row 0): header/axis background for all columns
                if (row.Index == 0)
                {
                    for (int c = 0; c < columnCount; ++c)
                    {
                        row.Cells[c].Style.BackColor = UiConstants.HEADER_AXIS_BACK_COLOR;
                        row.Cells[c].Style.Font = UiConstants.BOLD_FONT;
                    }
                }
            }
        }

        private void onGridCellPainting(object sender, DataGridViewCellPaintingEventArgs e)
        {
            if (e.RowIndex < 0 || e.ColumnIndex < 0)
            {
                return;
            }

            DataGridView grid = sender as DataGridView;
            if (grid == null)
            {
                return;
            }

            bool isSelected = (e.State & DataGridViewElementStates.Selected) == DataGridViewElementStates.Selected;
            Color backColor = isSelected ? e.CellStyle.SelectionBackColor : e.CellStyle.BackColor;

            if (e.ColumnIndex == 0 && e.RowIndex > 0)
            {
                e.Handled = true;

                using (SolidBrush backBrush = new SolidBrush(backColor))
                {
                    e.Graphics.FillRectangle(backBrush, e.CellBounds);
                }

                e.Paint(e.ClipBounds, DataGridViewPaintParts.Border);

                int period = e.RowIndex;

                string periodLine = period + "교시";
                string timeLine = buildPeriodTimeLine(period);

                Rectangle bounds = e.CellBounds;

                TextFormatFlags flags = TextFormatFlags.HorizontalCenter| TextFormatFlags.NoPadding| TextFormatFlags.EndEllipsis;

                Font periodFont = UiConstants.BOLD_FONT;
                Font timeFont = UiConstants.AXIS_TIME_FONT;

                Size periodSize = TextRenderer.MeasureText(e.Graphics, periodLine, periodFont, new Size(bounds.Width, bounds.Height), flags);
                Size timeSize = TextRenderer.MeasureText(e.Graphics, timeLine, timeFont, new Size(bounds.Width, bounds.Height), flags);

                int gap = 2;
                int totalHeight = periodSize.Height + gap + timeSize.Height;

                int startY = bounds.Y + (bounds.Height - totalHeight) / 2;
                if (startY < bounds.Y)
                {
                    startY = bounds.Y;
                }

                Rectangle periodRect = new Rectangle(bounds.X, startY, bounds.Width, periodSize.Height);
                TextRenderer.DrawText(e.Graphics, periodLine, periodFont, periodRect, Color.Black, flags | TextFormatFlags.VerticalCenter);

                Rectangle timeRect = new Rectangle(bounds.X, startY + periodSize.Height + gap, bounds.Width, timeSize.Height);
                TextRenderer.DrawText(e.Graphics, timeLine, timeFont, timeRect, Color.Black, flags | TextFormatFlags.VerticalCenter);

                return;
            }

            // Only schedule cells (excluding header row/axis column)
            if (e.RowIndex <= 0 || e.ColumnIndex <= 0)
            {
                return;
            }

            object value = grid.Rows[e.RowIndex].Cells[e.ColumnIndex].Value;
            ScheduleCellContent content = value as ScheduleCellContent;
            if (content == null)
            {
                return;
            }

            e.Handled = true;

            using (SolidBrush backBrush = new SolidBrush(backColor))
            {
                e.Graphics.FillRectangle(backBrush, e.CellBounds);
            }

            e.Paint(e.ClipBounds, DataGridViewPaintParts.Border);

            Rectangle bounds2 = e.CellBounds;

            string courseLine = content.CourseLine ?? string.Empty;
            string classroomLine = content.GetClassroomLine();

            Font font = e.CellStyle.Font ?? UiConstants.DEFAULT_FONT;

            TextFormatFlags flags2 = TextFormatFlags.HorizontalCenter| TextFormatFlags.NoPadding| TextFormatFlags.EndEllipsis;

            Size courseSize = TextRenderer.MeasureText(e.Graphics, courseLine, font, new Size(bounds2.Width, bounds2.Height), flags2);

            int gap2 = 2;

            int totalHeight2 = courseSize.Height;
            Size classroomSize = Size.Empty;

            bool hasClassroom = content.HasClassroom();
            if (hasClassroom)
            {
                classroomSize = TextRenderer.MeasureText(e.Graphics, classroomLine, font, new Size(bounds2.Width, bounds2.Height), flags2);
                totalHeight2 = courseSize.Height + gap2 + classroomSize.Height;
            }

            int startY2 = bounds2.Y + (bounds2.Height - totalHeight2) / 2;
            if (startY2 < bounds2.Y)
            {
                startY2 = bounds2.Y;
            }

            Rectangle courseRect = new Rectangle(bounds2.X, startY2, bounds2.Width, courseSize.Height);
            TextRenderer.DrawText(e.Graphics, courseLine, font, courseRect, Color.Black, flags2 | TextFormatFlags.VerticalCenter);

            if (hasClassroom)
            {
                Rectangle classroomRect = new Rectangle(bounds2.X, startY2 + courseSize.Height + gap2, bounds2.Width, classroomSize.Height);
                TextRenderer.DrawText(e.Graphics, classroomLine, font, classroomRect, UiConstants.CLASSROOM_FORE_COLOR, flags2 | TextFormatFlags.VerticalCenter);
            }
        }

        private static string buildPeriodTimeLine(int period)
        {
            int start = UiConstants.PERIOD1_START_MINUTES + ((period - 1) * UiConstants.PERIOD_BLOCK_MINUTES);
            int end = start + UiConstants.PERIOD_DURATION_MINUTES;

            return "(" + formatTime(start) + "~" + formatTime(end) + ")";
        }

        private static string formatTime(int totalMinutes)
        {
            int hour = totalMinutes / 60;
            int minute = totalMinutes % 60;

            return hour.ToString("D2") + ":" + minute.ToString("D2");
        }
    }
}
