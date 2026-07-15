using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace TimetableGenerator.UI.Product;

internal static class AppIconPainter
{
    private const int MINIMUM_ICON_DIMENSION_PIXELS = 2;
    private const int ICON_CONTENT_INSET_PIXELS = 2;
    private const float MINIMUM_STROKE_WIDTH_PIXELS = 1.0f;
    private const float STROKE_WIDTH_DIVISOR = 12.0f;

    private const int CALENDAR_MINIMUM_CORNER_RADIUS_PIXELS = 2;
    private const int CALENDAR_CORNER_RADIUS_DIVISOR = 8;
    private const float CALENDAR_HEADER_HEIGHT_RATIO = 0.30f;
    private const float CALENDAR_COLUMN_COUNT = 3.0f;
    private const float CALENDAR_SECOND_COLUMN_POSITION = 2.0f;
    private const float CALENDAR_ROW_COUNT = 2.0f;
    private const float CALENDAR_BINDING_STROKE_DIVISOR = 2.0f;
    private const float CALENDAR_BINDING_HEIGHT_RATIO = 0.15f;
    private const float CALENDAR_LEFT_BINDING_X_RATIO = 0.28f;
    private const float CALENDAR_RIGHT_BINDING_X_RATIO = 0.72f;

    private const float FILE_FOLD_SIZE_RATIO = 0.32f;

    private const float FOLDER_TAB_RIGHT_X_RATIO = 0.42f;
    private const float FOLDER_TAB_BOTTOM_Y_RATIO = 0.28f;
    private const float FOLDER_BACK_Y_RATIO = 0.40f;
    private const float FOLDER_TAB_SLOPE_WIDTH_RATIO = 0.10f;
    private const float FOLDER_RIGHT_FLAP_INSET_RATIO = 0.14f;
    private const float FOLDER_LEFT_FLAP_INSET_RATIO = 0.10f;

    private const int IMAGE_MINIMUM_CORNER_RADIUS_PIXELS = 2;
    private const int IMAGE_CORNER_RADIUS_DIVISOR = 9;
    private const float IMAGE_SUN_DIAMETER_RATIO = 0.14f;
    private const float IMAGE_SUN_X_RATIO = 0.18f;
    private const float IMAGE_SUN_Y_RATIO = 0.18f;
    private const float IMAGE_MOUNTAIN_START_X_RATIO = 0.12f;
    private const float IMAGE_MOUNTAIN_EDGE_Y_INSET_RATIO = 0.18f;
    private const float IMAGE_MOUNTAIN_FIRST_PEAK_X_RATIO = 0.40f;
    private const float IMAGE_MOUNTAIN_FIRST_PEAK_Y_RATIO = 0.50f;
    private const float IMAGE_MOUNTAIN_VALLEY_X_RATIO = 0.57f;
    private const float IMAGE_MOUNTAIN_VALLEY_Y_INSET_RATIO = 0.30f;
    private const float IMAGE_MOUNTAIN_SECOND_PEAK_X_RATIO = 0.70f;
    private const float IMAGE_MOUNTAIN_SECOND_PEAK_Y_RATIO = 0.56f;
    private const float IMAGE_MOUNTAIN_END_X_INSET_RATIO = 0.10f;

    private const float DIRECTION_CENTER_DIVISOR = 2.0f;
    private const float DIRECTION_HORIZONTAL_INSET_RATIO = 0.18f;
    private const float DIRECTION_ARROW_OFFSET_RATIO = 0.24f;

    private const float SUCCESS_CHECK_START_X_RATIO = 0.25f;
    private const float SUCCESS_CHECK_START_Y_RATIO = 0.52f;
    private const float SUCCESS_CHECK_MIDDLE_X_RATIO = 0.44f;
    private const float SUCCESS_CHECK_MIDDLE_Y_RATIO = 0.70f;
    private const float SUCCESS_CHECK_END_X_RATIO = 0.76f;
    private const float SUCCESS_CHECK_END_Y_RATIO = 0.34f;

    private const float WARNING_CENTER_DIVISOR = 2.0f;
    private const float WARNING_STEM_TOP_Y_RATIO = 0.32f;
    private const float WARNING_STEM_BOTTOM_Y_RATIO = 0.62f;
    private const float WARNING_DOT_CENTER_Y_RATIO = 0.76f;
    private const float WARNING_DOT_RADIUS_DIVISOR = 2.0f;

    private const float BUSY_ARC_START_ANGLE_DEGREES = -55.0f;
    private const float BUSY_ARC_SWEEP_ANGLE_DEGREES = 285.0f;
    private const float BUSY_MINIMUM_DOT_DIAMETER_PIXELS = 2.0f;
    private const float BUSY_DOT_STROKE_MULTIPLIER = 1.5f;
    private const float BUSY_DOT_Y_RATIO = 0.28f;

    internal static void drawIcon(
        Graphics graphics,
        Rectangle bounds,
        EAppIcon appIcon,
        Color iconColor)
    {
        if (graphics == null)
        {
            throw new ArgumentNullException(nameof(graphics));
        }

        if (appIcon == EAppIcon.None
            || bounds.Width <= MINIMUM_ICON_DIMENSION_PIXELS
            || bounds.Height <= MINIMUM_ICON_DIMENSION_PIXELS)
        {
            return;
        }

        GraphicsState graphicsState = graphics.Save();
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;

        float strokeWidth = Math.Max(
            MINIMUM_STROKE_WIDTH_PIXELS,
            Math.Min(bounds.Width, bounds.Height) / STROKE_WIDTH_DIVISOR);
        using (Pen iconPen = new Pen(iconColor, strokeWidth))
        {
            iconPen.StartCap = LineCap.Round;
            iconPen.EndCap = LineCap.Round;
            iconPen.LineJoin = LineJoin.Round;

            switch (appIcon)
            {
                case EAppIcon.Calendar:
                    drawCalendarIcon(graphics, bounds, iconPen);
                    break;
                case EAppIcon.File:
                    drawFileIcon(graphics, bounds, iconPen);
                    break;
                case EAppIcon.FolderOpen:
                    drawFolderOpenIcon(graphics, bounds, iconPen);
                    break;
                case EAppIcon.ImageExport:
                    drawImageExportIcon(graphics, bounds, iconPen);
                    break;
                case EAppIcon.Previous:
                    drawDirectionIcon(graphics, bounds, iconPen, EAppIcon.Previous);
                    break;
                case EAppIcon.Next:
                    drawDirectionIcon(graphics, bounds, iconPen, EAppIcon.Next);
                    break;
                case EAppIcon.Success:
                    drawSuccessIcon(graphics, bounds, iconPen);
                    break;
                case EAppIcon.Warning:
                    drawWarningIcon(graphics, bounds, iconPen);
                    break;
                case EAppIcon.Busy:
                    drawBusyIcon(graphics, bounds, iconPen);
                    break;
                case EAppIcon.None:
                    break;
                default:
                    Debug.Fail("Unexpected app icon: " + appIcon);
                    break;
            }
        }

        graphics.Restore(graphicsState);
    }

    private static void drawCalendarIcon(Graphics graphics, Rectangle bounds, Pen iconPen)
    {
        Rectangle outerBounds = ProductDrawing.insetRectangle(bounds, ICON_CONTENT_INSET_PIXELS);
        int cornerRadius = Math.Max(
            CALENDAR_MINIMUM_CORNER_RADIUS_PIXELS,
            outerBounds.Width / CALENDAR_CORNER_RADIUS_DIVISOR);

        using (GraphicsPath outerPath = ProductDrawing.createRoundedRectanglePath(outerBounds, cornerRadius))
        {
            graphics.DrawPath(iconPen, outerPath);
        }

        float headerY = outerBounds.Top + (outerBounds.Height * CALENDAR_HEADER_HEIGHT_RATIO);
        graphics.DrawLine(iconPen, outerBounds.Left, headerY, outerBounds.Right, headerY);

        float firstColumnX = outerBounds.Left + (outerBounds.Width / CALENDAR_COLUMN_COUNT);
        float secondColumnX = outerBounds.Left
            + ((outerBounds.Width * CALENDAR_SECOND_COLUMN_POSITION) / CALENDAR_COLUMN_COUNT);
        graphics.DrawLine(iconPen, firstColumnX, headerY, firstColumnX, outerBounds.Bottom);
        graphics.DrawLine(iconPen, secondColumnX, headerY, secondColumnX, outerBounds.Bottom);

        float firstRowY = headerY + ((outerBounds.Bottom - headerY) / CALENDAR_ROW_COUNT);
        graphics.DrawLine(iconPen, outerBounds.Left, firstRowY, outerBounds.Right, firstRowY);

        float bindingTop = outerBounds.Top - (iconPen.Width / CALENDAR_BINDING_STROKE_DIVISOR);
        float bindingBottom = outerBounds.Top + (outerBounds.Height * CALENDAR_BINDING_HEIGHT_RATIO);
        float leftBindingX = outerBounds.Left + (outerBounds.Width * CALENDAR_LEFT_BINDING_X_RATIO);
        float rightBindingX = outerBounds.Left + (outerBounds.Width * CALENDAR_RIGHT_BINDING_X_RATIO);
        graphics.DrawLine(iconPen, leftBindingX, bindingTop, leftBindingX, bindingBottom);
        graphics.DrawLine(iconPen, rightBindingX, bindingTop, rightBindingX, bindingBottom);
    }

    private static void drawFileIcon(Graphics graphics, Rectangle bounds, Pen iconPen)
    {
        Rectangle outerBounds = ProductDrawing.insetRectangle(bounds, ICON_CONTENT_INSET_PIXELS);
        float foldSize = outerBounds.Width * FILE_FOLD_SIZE_RATIO;
        float foldLeftX = outerBounds.Right - foldSize;
        float foldBottomY = outerBounds.Top + foldSize;

        using (GraphicsPath filePath = new GraphicsPath())
        {
            filePath.AddLine(outerBounds.Left, outerBounds.Top, foldLeftX, outerBounds.Top);
            filePath.AddLine(foldLeftX, outerBounds.Top, outerBounds.Right, foldBottomY);
            filePath.AddLine(outerBounds.Right, foldBottomY, outerBounds.Right, outerBounds.Bottom);
            filePath.AddLine(outerBounds.Right, outerBounds.Bottom, outerBounds.Left, outerBounds.Bottom);
            filePath.CloseFigure();
            graphics.DrawPath(iconPen, filePath);
        }

        graphics.DrawLine(
            iconPen,
            foldLeftX,
            outerBounds.Top,
            foldLeftX,
            foldBottomY);
        graphics.DrawLine(
            iconPen,
            foldLeftX,
            foldBottomY,
            outerBounds.Right,
            foldBottomY);
    }

    private static void drawFolderOpenIcon(Graphics graphics, Rectangle bounds, Pen iconPen)
    {
        Rectangle outerBounds = ProductDrawing.insetRectangle(bounds, ICON_CONTENT_INSET_PIXELS);
        float tabRightX = outerBounds.Left + (outerBounds.Width * FOLDER_TAB_RIGHT_X_RATIO);
        float tabBottomY = outerBounds.Top + (outerBounds.Height * FOLDER_TAB_BOTTOM_Y_RATIO);
        float folderBackY = outerBounds.Top + (outerBounds.Height * FOLDER_BACK_Y_RATIO);
        float tabSlopeRightX = tabRightX + (outerBounds.Width * FOLDER_TAB_SLOPE_WIDTH_RATIO);

        using (GraphicsPath folderBackPath = new GraphicsPath())
        {
            folderBackPath.AddLine(outerBounds.Left, folderBackY, outerBounds.Left, tabBottomY);
            folderBackPath.AddLine(outerBounds.Left, tabBottomY, tabRightX, tabBottomY);
            folderBackPath.AddLine(tabRightX, tabBottomY, tabSlopeRightX, folderBackY);
            folderBackPath.AddLine(tabSlopeRightX, folderBackY, outerBounds.Right, folderBackY);
            graphics.DrawPath(iconPen, folderBackPath);
        }

        float flapRightX = outerBounds.Right - (outerBounds.Width * FOLDER_RIGHT_FLAP_INSET_RATIO);
        float flapLeftX = outerBounds.Left + (outerBounds.Width * FOLDER_LEFT_FLAP_INSET_RATIO);
        PointF[] flapPoints = new PointF[]
        {
            new PointF(outerBounds.Left, folderBackY),
            new PointF(outerBounds.Right, folderBackY),
            new PointF(flapRightX, outerBounds.Bottom),
            new PointF(flapLeftX, outerBounds.Bottom),
        };

        graphics.DrawPolygon(iconPen, flapPoints);
    }

    private static void drawImageExportIcon(Graphics graphics, Rectangle bounds, Pen iconPen)
    {
        Rectangle outerBounds = ProductDrawing.insetRectangle(bounds, ICON_CONTENT_INSET_PIXELS);
        int cornerRadius = Math.Max(
            IMAGE_MINIMUM_CORNER_RADIUS_PIXELS,
            outerBounds.Width / IMAGE_CORNER_RADIUS_DIVISOR);

        using (GraphicsPath outerPath = ProductDrawing.createRoundedRectanglePath(outerBounds, cornerRadius))
        {
            graphics.DrawPath(iconPen, outerPath);
        }

        float circleDiameter = outerBounds.Width * IMAGE_SUN_DIAMETER_RATIO;
        RectangleF circleBounds = new RectangleF(
            outerBounds.Left + (outerBounds.Width * IMAGE_SUN_X_RATIO),
            outerBounds.Top + (outerBounds.Height * IMAGE_SUN_Y_RATIO),
            circleDiameter,
            circleDiameter);
        graphics.DrawEllipse(iconPen, circleBounds);

        float mountainEdgeY = outerBounds.Bottom
            - (outerBounds.Height * IMAGE_MOUNTAIN_EDGE_Y_INSET_RATIO);
        PointF[] mountainPoints = new PointF[]
        {
            new PointF(
                outerBounds.Left + (outerBounds.Width * IMAGE_MOUNTAIN_START_X_RATIO),
                mountainEdgeY),
            new PointF(
                outerBounds.Left + (outerBounds.Width * IMAGE_MOUNTAIN_FIRST_PEAK_X_RATIO),
                outerBounds.Top + (outerBounds.Height * IMAGE_MOUNTAIN_FIRST_PEAK_Y_RATIO)),
            new PointF(
                outerBounds.Left + (outerBounds.Width * IMAGE_MOUNTAIN_VALLEY_X_RATIO),
                outerBounds.Bottom - (outerBounds.Height * IMAGE_MOUNTAIN_VALLEY_Y_INSET_RATIO)),
            new PointF(
                outerBounds.Left + (outerBounds.Width * IMAGE_MOUNTAIN_SECOND_PEAK_X_RATIO),
                outerBounds.Top + (outerBounds.Height * IMAGE_MOUNTAIN_SECOND_PEAK_Y_RATIO)),
            new PointF(
                outerBounds.Right - (outerBounds.Width * IMAGE_MOUNTAIN_END_X_INSET_RATIO),
                mountainEdgeY),
        };
        graphics.DrawLines(iconPen, mountainPoints);
    }

    private static void drawDirectionIcon(
        Graphics graphics,
        Rectangle bounds,
        Pen iconPen,
        EAppIcon directionIcon)
    {
        float centerY = bounds.Top + (bounds.Height / DIRECTION_CENTER_DIVISOR);
        float leftX = bounds.Left + (bounds.Width * DIRECTION_HORIZONTAL_INSET_RATIO);
        float rightX = bounds.Right - (bounds.Width * DIRECTION_HORIZONTAL_INSET_RATIO);
        float arrowOffset = bounds.Height * DIRECTION_ARROW_OFFSET_RATIO;

        if (directionIcon == EAppIcon.Previous)
        {
            graphics.DrawLine(iconPen, rightX, centerY, leftX, centerY);
            graphics.DrawLine(iconPen, leftX, centerY, leftX + arrowOffset, centerY - arrowOffset);
            graphics.DrawLine(iconPen, leftX, centerY, leftX + arrowOffset, centerY + arrowOffset);
            return;
        }

        graphics.DrawLine(iconPen, leftX, centerY, rightX, centerY);
        graphics.DrawLine(iconPen, rightX, centerY, rightX - arrowOffset, centerY - arrowOffset);
        graphics.DrawLine(iconPen, rightX, centerY, rightX - arrowOffset, centerY + arrowOffset);
    }

    private static void drawSuccessIcon(Graphics graphics, Rectangle bounds, Pen iconPen)
    {
        Rectangle circleBounds = ProductDrawing.insetRectangle(bounds, ICON_CONTENT_INSET_PIXELS);
        graphics.DrawEllipse(iconPen, circleBounds);

        PointF[] checkPoints = new PointF[]
        {
            new PointF(
                circleBounds.Left + (circleBounds.Width * SUCCESS_CHECK_START_X_RATIO),
                circleBounds.Top + (circleBounds.Height * SUCCESS_CHECK_START_Y_RATIO)),
            new PointF(
                circleBounds.Left + (circleBounds.Width * SUCCESS_CHECK_MIDDLE_X_RATIO),
                circleBounds.Top + (circleBounds.Height * SUCCESS_CHECK_MIDDLE_Y_RATIO)),
            new PointF(
                circleBounds.Left + (circleBounds.Width * SUCCESS_CHECK_END_X_RATIO),
                circleBounds.Top + (circleBounds.Height * SUCCESS_CHECK_END_Y_RATIO)),
        };
        graphics.DrawLines(iconPen, checkPoints);
    }

    private static void drawWarningIcon(Graphics graphics, Rectangle bounds, Pen iconPen)
    {
        Rectangle outerBounds = ProductDrawing.insetRectangle(bounds, ICON_CONTENT_INSET_PIXELS);
        float centerX = outerBounds.Left + (outerBounds.Width / WARNING_CENTER_DIVISOR);
        PointF[] trianglePoints = new PointF[]
        {
            new PointF(centerX, outerBounds.Top),
            new PointF(outerBounds.Right, outerBounds.Bottom),
            new PointF(outerBounds.Left, outerBounds.Bottom),
        };
        graphics.DrawPolygon(iconPen, trianglePoints);

        graphics.DrawLine(
            iconPen,
            centerX,
            outerBounds.Top + (outerBounds.Height * WARNING_STEM_TOP_Y_RATIO),
            centerX,
            outerBounds.Top + (outerBounds.Height * WARNING_STEM_BOTTOM_Y_RATIO));
        graphics.DrawEllipse(
            iconPen,
            centerX - (iconPen.Width / WARNING_DOT_RADIUS_DIVISOR),
            outerBounds.Top + (outerBounds.Height * WARNING_DOT_CENTER_Y_RATIO),
            iconPen.Width,
            iconPen.Width);
    }

    private static void drawBusyIcon(Graphics graphics, Rectangle bounds, Pen iconPen)
    {
        Rectangle outerBounds = ProductDrawing.insetRectangle(bounds, ICON_CONTENT_INSET_PIXELS);
        graphics.DrawArc(
            iconPen,
            outerBounds,
            BUSY_ARC_START_ANGLE_DEGREES,
            BUSY_ARC_SWEEP_ANGLE_DEGREES);

        float dotDiameter = Math.Max(
            BUSY_MINIMUM_DOT_DIAMETER_PIXELS,
            iconPen.Width * BUSY_DOT_STROKE_MULTIPLIER);
        float dotX = outerBounds.Right - dotDiameter;
        float dotY = outerBounds.Top + (outerBounds.Height * BUSY_DOT_Y_RATIO);
        using (SolidBrush dotBrush = new SolidBrush(iconPen.Color))
        {
            graphics.FillEllipse(dotBrush, dotX, dotY, dotDiameter, dotDiameter);
        }
    }
}
