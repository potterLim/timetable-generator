using System;
using System.Drawing;
using System.Windows.Forms;

namespace TimetableGenerator.UI.Product;

internal static class DesignTokens
{
    internal const int BASE_DPI = 96;

    internal const int SPACE_4 = 4;
    internal const int SPACE_8 = 8;
    internal const int SPACE_12 = 12;
    internal const int SPACE_16 = 16;
    internal const int SPACE_20 = 20;
    internal const int SPACE_24 = 24;
    internal const int SPACE_32 = 32;
    internal const int SPACE_40 = 40;
    internal const int SPACE_48 = 48;

    internal const int APP_HEADER_HEIGHT = 72;
    internal const int APP_STATUS_HEIGHT = 40;
    internal const int SIDEBAR_WIDTH = 240;
    internal const int SIDEBAR_HEADER_HEIGHT = 72;
    internal const int SIDEBAR_ITEM_HEIGHT = 72;

    internal const int BUTTON_MINIMUM_HEIGHT = 38;
    internal const int BUTTON_HORIZONTAL_PADDING = 16;
    internal const int BUTTON_ICON_SIZE = 18;
    internal const int BUTTON_CONTENT_GAP = 8;
    internal const int HEADER_BUTTON_MINIMUM_WIDTH = 112;
    internal const int WELCOME_PRIMARY_BUTTON_MINIMUM_HEIGHT = 48;

    internal const int APP_LOGO_ICON_SIZE = 40;
    internal const int WELCOME_ICON_SIZE = 72;
    internal const int SIDEBAR_ICON_SIZE = 20;
    internal const int STATUS_ICON_SIZE = 18;

    internal const int CORNER_RADIUS_SMALL = 6;
    internal const int CORNER_RADIUS_MEDIUM = 8;
    internal const int BORDER_WIDTH = 1;
    internal const int FOCUS_RING_WIDTH = 2;

    internal const int WELCOME_CONTENT_MAXIMUM_WIDTH = 720;
    internal const int WELCOME_DROP_ZONE_HEIGHT = 168;

    internal const float BODY_FONT_SIZE_POINTS = 10.0f;
    internal const float CAPTION_FONT_SIZE_POINTS = 9.0f;
    internal const float BUTTON_FONT_SIZE_POINTS = 10.0f;
    internal const float SECTION_TITLE_FONT_SIZE_POINTS = 11.0f;
    internal const float APP_TITLE_FONT_SIZE_POINTS = 20.0f;
    internal const float WELCOME_TITLE_FONT_SIZE_POINTS = 24.0f;
    internal const float WELCOME_DESCRIPTION_FONT_SIZE_POINTS = 11.0f;
    internal const float SIDEBAR_ITEM_TITLE_FONT_SIZE_POINTS = 11.0f;
    internal const float SIDEBAR_ITEM_SUMMARY_FONT_SIZE_POINTS = 9.5f;
    internal const float STATUS_FONT_SIZE_POINTS = 9.5f;

    internal static readonly Color WINDOW_BACKGROUND_COLOR = Color.FromArgb(255, 255, 255);
    internal static readonly Color SIDEBAR_BACKGROUND_COLOR = Color.FromArgb(250, 250, 250);
    internal static readonly Color SURFACE_COLOR = Color.FromArgb(255, 255, 255);
    internal static readonly Color SUBTLE_SURFACE_COLOR = Color.FromArgb(247, 247, 247);

    internal static readonly Color BORDER_COLOR = Color.FromArgb(210, 210, 210);
    internal static readonly Color SUBTLE_BORDER_COLOR = Color.FromArgb(230, 230, 230);

    internal static readonly Color TEXT_PRIMARY_COLOR = Color.FromArgb(26, 26, 26);
    internal static readonly Color TEXT_SECONDARY_COLOR = Color.FromArgb(97, 97, 97);
    internal static readonly Color TEXT_TERTIARY_COLOR = Color.FromArgb(117, 117, 117);

    internal static readonly Color ACCENT_COLOR = Color.FromArgb(15, 108, 189);
    internal static readonly Color ACCENT_HOVER_COLOR = Color.FromArgb(17, 94, 163);
    internal static readonly Color ACCENT_PRESSED_COLOR = Color.FromArgb(12, 59, 94);
    internal static readonly Color ACCENT_TINT_COLOR = Color.FromArgb(239, 246, 252);
    internal static readonly Color ACCENT_BORDER_COLOR = Color.FromArgb(138, 188, 236);

    internal static readonly Color QUIET_HOVER_COLOR = Color.FromArgb(245, 245, 245);
    internal static readonly Color QUIET_PRESSED_COLOR = Color.FromArgb(232, 232, 232);
    internal static readonly Color DISABLED_BACKGROUND_COLOR = Color.FromArgb(243, 243, 243);
    internal static readonly Color DISABLED_BORDER_COLOR = Color.FromArgb(224, 224, 224);
    internal static readonly Color DISABLED_TEXT_COLOR = Color.FromArgb(148, 148, 148);

    internal static readonly Color SUCCESS_COLOR = Color.FromArgb(16, 124, 16);
    internal static readonly Color ERROR_COLOR = Color.FromArgb(196, 43, 28);

    internal static readonly Color COURSE_BLUE_BACKGROUND_COLOR = Color.FromArgb(238, 246, 255);
    internal static readonly Color COURSE_BLUE_BORDER_COLOR = Color.FromArgb(116, 169, 245);
    internal static readonly Color COURSE_BLUE_TEXT_COLOR = Color.FromArgb(15, 108, 189);
    internal static readonly Color COURSE_PURPLE_BACKGROUND_COLOR = Color.FromArgb(247, 242, 252);
    internal static readonly Color COURSE_PURPLE_BORDER_COLOR = Color.FromArgb(183, 160, 229);
    internal static readonly Color COURSE_PURPLE_TEXT_COLOR = Color.FromArgb(116, 77, 169);
    internal static readonly Color COURSE_GREEN_BACKGROUND_COLOR = Color.FromArgb(237, 248, 244);
    internal static readonly Color COURSE_GREEN_BORDER_COLOR = Color.FromArgb(124, 199, 170);
    internal static readonly Color COURSE_GREEN_TEXT_COLOR = Color.FromArgb(16, 124, 92);

    internal static int scaleLogicalPixel(Control control, int logicalPixel)
    {
        if (control == null)
        {
            throw new ArgumentNullException(nameof(control));
        }

        if (logicalPixel < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(logicalPixel));
        }

        if (logicalPixel == 0)
        {
            return 0;
        }

        float scaleFactor = (float)control.DeviceDpi / BASE_DPI;
        int scaledPixel = (int)Math.Round(logicalPixel * scaleFactor, MidpointRounding.AwayFromZero);
        return Math.Max(1, scaledPixel);
    }

    internal static Font createBodyFont(Font baseFont)
    {
        return createFont(baseFont, BODY_FONT_SIZE_POINTS, FontStyle.Regular);
    }

    internal static Font createCaptionFont(Font baseFont)
    {
        return createFont(baseFont, CAPTION_FONT_SIZE_POINTS, FontStyle.Regular);
    }

    internal static Font createButtonFont(Font baseFont)
    {
        return createFont(baseFont, BUTTON_FONT_SIZE_POINTS, FontStyle.Regular);
    }

    internal static Font createSectionTitleFont(Font baseFont)
    {
        return createFont(baseFont, SECTION_TITLE_FONT_SIZE_POINTS, FontStyle.Bold);
    }

    internal static Font createAppTitleFont(Font baseFont)
    {
        return createFont(baseFont, APP_TITLE_FONT_SIZE_POINTS, FontStyle.Bold);
    }

    internal static Font createWelcomeTitleFont(Font baseFont)
    {
        return createFont(baseFont, WELCOME_TITLE_FONT_SIZE_POINTS, FontStyle.Bold);
    }

    internal static Font createWelcomeDescriptionFont(Font baseFont)
    {
        return createFont(baseFont, WELCOME_DESCRIPTION_FONT_SIZE_POINTS, FontStyle.Regular);
    }

    internal static Font createSidebarItemTitleFont(Font baseFont)
    {
        return createFont(baseFont, SIDEBAR_ITEM_TITLE_FONT_SIZE_POINTS, FontStyle.Bold);
    }

    internal static Font createSidebarItemSummaryFont(Font baseFont)
    {
        return createFont(baseFont, SIDEBAR_ITEM_SUMMARY_FONT_SIZE_POINTS, FontStyle.Regular);
    }

    internal static Font createStatusFont(Font baseFont)
    {
        return createFont(baseFont, STATUS_FONT_SIZE_POINTS, FontStyle.Regular);
    }

    private static Font createFont(Font baseFont, float sizeInPoints, FontStyle fontStyle)
    {
        if (baseFont == null)
        {
            throw new ArgumentNullException(nameof(baseFont));
        }

        return new Font(baseFont.FontFamily, sizeInPoints, fontStyle, GraphicsUnit.Point);
    }
}
