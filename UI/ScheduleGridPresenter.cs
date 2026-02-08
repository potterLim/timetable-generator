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
                // Axis column (col 0): LightGray + Bold for all rows
                row.Cells[0].Style.BackColor = UiConstants.HEADER_AXIS_BACK_COLOR;
                row.Cells[0].Style.Font = UiConstants.BOLD_FONT;

                // Header row (row 0): LightGray + Bold for all columns
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
            if (e.RowIndex <= 0 || e.ColumnIndex <= 0)
            {
                return;
            }

            DataGridView grid = sender as DataGridView;
            if (grid == null)
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

            bool isSelected = (e.State & DataGridViewElementStates.Selected) == DataGridViewElementStates.Selected;

            Color backColor = isSelected ? e.CellStyle.SelectionBackColor : e.CellStyle.BackColor;
            using (SolidBrush backBrush = new SolidBrush(backColor))
            {
                e.Graphics.FillRectangle(backBrush, e.CellBounds);
            }

            e.Paint(e.ClipBounds, DataGridViewPaintParts.Border);

            Rectangle bounds = e.CellBounds;

            string courseLine = content.CourseLine ?? string.Empty;
            string classroomLine = content.GetClassroomLine();

            Font font = e.CellStyle.Font ?? UiConstants.DEFAULT_FONT;

            TextFormatFlags flags = TextFormatFlags.HorizontalCenter | TextFormatFlags.NoPadding | TextFormatFlags.EndEllipsis;

            Size courseSize = TextRenderer.MeasureText(e.Graphics, courseLine, font, new Size(bounds.Width, bounds.Height), flags);

            int gap = 2;

            int totalHeight = courseSize.Height;
            Size classroomSize = Size.Empty;

            bool hasClassroom = content.HasClassroom();
            if (hasClassroom)
            {
                classroomSize = TextRenderer.MeasureText(e.Graphics, classroomLine, font, new Size(bounds.Width, bounds.Height), flags);
                totalHeight = courseSize.Height + gap + classroomSize.Height;
            }

            int startY = bounds.Y + (bounds.Height - totalHeight) / 2;
            if (startY < bounds.Y)
            {
                startY = bounds.Y;
            }

            Rectangle courseRect = new Rectangle(bounds.X, startY, bounds.Width, courseSize.Height);
            TextRenderer.DrawText(e.Graphics, courseLine, font, courseRect, Color.Black, flags | TextFormatFlags.VerticalCenter);

            if (hasClassroom)
            {
                Rectangle classroomRect = new Rectangle(bounds.X, startY + courseSize.Height + gap, bounds.Width, classroomSize.Height);
                TextRenderer.DrawText(e.Graphics, classroomLine, font, classroomRect, UiConstants.CLASSROOM_FORE_COLOR, flags | TextFormatFlags.VerticalCenter);
            }
        }
    }
}
