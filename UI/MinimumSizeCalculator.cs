using System.Drawing;

namespace TimetableGenerator
{
    // Computes a minimum Form size so the schedule grid can be shown without scrollbars.
    public static class MinimumSizeCalculator
    {
        public static Size CalculateFormMinimumSize()
        {
            int minGridWidth = calculateGridMinimumWidth();
            int minGridHeight = calculateGridMinimumHeight();

            int minWidth = UiConstants.LEFT_PANEL_WIDTH + minGridWidth + UiConstants.FORM_BORDER_EXTRA_WIDTH;

            int minHeight = minGridHeight + UiConstants.FORM_BORDER_EXTRA_HEIGHT;

            if (minWidth < UiConstants.FORM_MIN_WIDTH)
            {
                minWidth = UiConstants.FORM_MIN_WIDTH;
            }

            if (minHeight < UiConstants.FORM_MIN_HEIGHT)
            {
                minHeight = UiConstants.FORM_MIN_HEIGHT;
            }

            return new Size(minWidth, minHeight);
        }

        private static int calculateGridMinimumWidth()
        {
            int dayCount = UiConstants.GRID_MAX_VISIBLE_DAY_COUNT;

            int timeWidth = UiConstants.TIME_COLUMN_WIDTH;
            int dayWidthTotal = UiConstants.GRID_MIN_DAY_COLUMN_WIDTH * dayCount;

            return timeWidth + dayWidthTotal + UiConstants.GRID_EXTRA_WIDTH;
        }

        private static int calculateGridMinimumHeight()
        {
            // Rows = header + default 8 periods. If you increase the maximum period, adjust this policy.
            int rows = UiConstants.GRID_MIN_VISIBLE_ROW_COUNT;
            return (UiConstants.GRID_ROW_HEIGHT * rows) + UiConstants.GRID_EXTRA_HEIGHT;
        }
    }
}
