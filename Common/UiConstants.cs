using System.Drawing;

namespace TimetableGenerator
{
    // UI sizing constants are designed to keep the schedule grid visible without scrollbars.
    // If you change widths/heights, also review MinimumSizeCalculator and ScheduleGridPresenter.
    public static class UiConstants
    {
        public const string FONT_FAMILY_NAME = "Segoe UI";

        public const int FORM_WIDTH = 1200;
        public const int FORM_HEIGHT = 700;

        // Minimum form size policy (scrollbar-less grid)
        public const int FORM_MIN_WIDTH = FORM_WIDTH;
        public const int FORM_MIN_HEIGHT = FORM_HEIGHT;

        // WinForms non-client area size varies by OS/theme/DPI.
        // These values are a practical approximation used only for minimum-size policy.
        public const int FORM_BORDER_EXTRA_WIDTH = 16;
        public const int FORM_BORDER_EXTRA_HEIGHT = 39;

        // Left panel (must not shrink)
        public const int LEFT_PANEL_WIDTH = 150;
        public const int LEFT_PANEL_PADDING = 10;

        public const int LIST_ITEM_HEIGHT = 24;
        public const int LIST_HEIGHT = 600;

        public const int LEFT_GAP_HEIGHT = 2;
        public const int LEFT_ACTION_ROW_HEIGHT = 42;

        public const int LEFT_BUTTON_HEIGHT = 32;
        public const int LEFT_BUTTON_WIDTH = 130;

        // Grid sizing policy:
        // - No scrollbars (ScrollBars.None)
        // - Minimum day column width is enforced by form minimum size.
        public const int GRID_ROW_HEIGHT = 50;

        // Time column width is fixed to 70 (no extra constants)
        public const int TIME_COLUMN_WIDTH = 70;

        // Minimum day column width policy
        public const int GRID_MIN_DAY_COLUMN_WIDTH = 120;

        // Policy helpers (minimum-size calculation)
        public const int GRID_MAX_VISIBLE_DAY_COUNT = 7;      // Mon~Fri + Sat + Sun
        public const int GRID_MIN_VISIBLE_ROW_COUNT = 9;      // Header row + 8 periods (default)
        public const int GRID_EXTRA_WIDTH = 2;                // small safety margin
        public const int GRID_EXTRA_HEIGHT = 2;               // small safety margin

        // Offscreen position for temporary rendering during PNG export.
        // Must be far enough to avoid flashing on screen.
        public const int OFFSCREEN_X = -2000;
        public const int OFFSCREEN_Y = -2000;

        public static readonly Font DEFAULT_FONT = new Font(FONT_FAMILY_NAME, 10.0f);
        public static readonly Font BOLD_FONT = new Font(FONT_FAMILY_NAME, 10.0f, FontStyle.Bold);

        public static readonly Color MAIN_BACKGROUND_COLOR = Color.WhiteSmoke;
        public static readonly Color GRID_BACKGROUND_COLOR = Color.White;
        public static readonly Color ALT_ROW_COLOR = Color.FromArgb(240, 240, 240);

        public static readonly Color SELECTION_BACK_COLOR = Color.LightBlue;
        public static readonly Color SELECTION_FORE_COLOR = Color.Black;
    }
}
