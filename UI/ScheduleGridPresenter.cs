using System.Drawing;
using System.Windows.Forms;

namespace TimetableGenerator
{
    // Creates and styles the schedule grid.
    // This grid intentionally disables scrollbars and user resizing.
    // Minimum size is enforced at the Form level to keep all cells visible.
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
            grid.AlternatingRowsDefaultCellStyle.BackColor = UiConstants.ALT_ROW_COLOR;
            grid.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.None;

            return grid;
        }

        public void ApplySizingAndHeaderStyle(DataGridView grid)
        {
            if (grid == null)
            {
                return;
            }

            applyColumnSizing(grid);
            applyHeaderStyles(grid);
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

            int timeWidth = UiConstants.TIME_COLUMN_WIDTH;
            int remainingWidth = totalWidth - timeWidth;

            if (remainingWidth <= 0)
            {
                grid.Columns[0].Width = totalWidth;
                return;
            }

            int dayWidth = remainingWidth / dayColumnCount;

            // Day columns share remaining width equally; a minimum width is enforced.
            // Form minimum size should guarantee this condition in normal use.
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

            grid.Columns[0].Width = timeWidth + remainder;
        }

        private void applyHeaderStyles(DataGridView grid)
        {
            // Header row (row 0) and time column (col 0) are displayed in bold.
            foreach (DataGridViewRow row in grid.Rows)
            {
                if (row.Index == 0)
                {
                    row.DefaultCellStyle.Font = UiConstants.BOLD_FONT;
                }

                row.Cells[0].Style.Font = UiConstants.BOLD_FONT;
            }
        }
    }
}
