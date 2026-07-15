using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace TimetableGenerator.UI.Product;

internal static class AppIconPainter
{
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

        if (appIcon == EAppIcon.None || bounds.Width <= 2 || bounds.Height <= 2)
        {
            return;
        }

        GraphicsState graphicsState = graphics.Save();
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;

        float strokeWidth = Math.Max(1.0f, Math.Min(bounds.Width, bounds.Height) / 12.0f);
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
        Rectangle outerBounds = ProductDrawing.insetRectangle(bounds, 2);
        int cornerRadius = Math.Max(2, outerBounds.Width / 8);

        using (GraphicsPath outerPath = ProductDrawing.createRoundedRectanglePath(outerBounds, cornerRadius))
        {
            graphics.DrawPath(iconPen, outerPath);
        }

        float headerY = outerBounds.Top + (outerBounds.Height * 0.30f);
        graphics.DrawLine(iconPen, outerBounds.Left, headerY, outerBounds.Right, headerY);

        float firstColumnX = outerBounds.Left + (outerBounds.Width / 3.0f);
        float secondColumnX = outerBounds.Left + ((outerBounds.Width * 2.0f) / 3.0f);
        graphics.DrawLine(iconPen, firstColumnX, headerY, firstColumnX, outerBounds.Bottom);
        graphics.DrawLine(iconPen, secondColumnX, headerY, secondColumnX, outerBounds.Bottom);

        float firstRowY = headerY + ((outerBounds.Bottom - headerY) / 2.0f);
        graphics.DrawLine(iconPen, outerBounds.Left, firstRowY, outerBounds.Right, firstRowY);

        float bindingTop = outerBounds.Top - (iconPen.Width / 2.0f);
        float bindingBottom = outerBounds.Top + (outerBounds.Height * 0.15f);
        graphics.DrawLine(iconPen, outerBounds.Left + (outerBounds.Width * 0.28f), bindingTop, outerBounds.Left + (outerBounds.Width * 0.28f), bindingBottom);
        graphics.DrawLine(iconPen, outerBounds.Left + (outerBounds.Width * 0.72f), bindingTop, outerBounds.Left + (outerBounds.Width * 0.72f), bindingBottom);
    }

    private static void drawFileIcon(Graphics graphics, Rectangle bounds, Pen iconPen)
    {
        Rectangle outerBounds = ProductDrawing.insetRectangle(bounds, 2);
        float foldSize = outerBounds.Width * 0.32f;

        using (GraphicsPath filePath = new GraphicsPath())
        {
            filePath.AddLine(outerBounds.Left, outerBounds.Top, outerBounds.Right - foldSize, outerBounds.Top);
            filePath.AddLine(outerBounds.Right - foldSize, outerBounds.Top, outerBounds.Right, outerBounds.Top + foldSize);
            filePath.AddLine(outerBounds.Right, outerBounds.Top + foldSize, outerBounds.Right, outerBounds.Bottom);
            filePath.AddLine(outerBounds.Right, outerBounds.Bottom, outerBounds.Left, outerBounds.Bottom);
            filePath.CloseFigure();
            graphics.DrawPath(iconPen, filePath);
        }

        graphics.DrawLine(
            iconPen,
            outerBounds.Right - foldSize,
            outerBounds.Top,
            outerBounds.Right - foldSize,
            outerBounds.Top + foldSize);
        graphics.DrawLine(
            iconPen,
            outerBounds.Right - foldSize,
            outerBounds.Top + foldSize,
            outerBounds.Right,
            outerBounds.Top + foldSize);
    }

    private static void drawFolderOpenIcon(Graphics graphics, Rectangle bounds, Pen iconPen)
    {
        Rectangle outerBounds = ProductDrawing.insetRectangle(bounds, 2);
        float tabRightX = outerBounds.Left + (outerBounds.Width * 0.42f);
        float tabBottomY = outerBounds.Top + (outerBounds.Height * 0.28f);
        float folderBackY = outerBounds.Top + (outerBounds.Height * 0.40f);

        using (GraphicsPath folderBackPath = new GraphicsPath())
        {
            folderBackPath.AddLine(outerBounds.Left, folderBackY, outerBounds.Left, tabBottomY);
            folderBackPath.AddLine(outerBounds.Left, tabBottomY, tabRightX, tabBottomY);
            folderBackPath.AddLine(tabRightX, tabBottomY, tabRightX + (outerBounds.Width * 0.10f), folderBackY);
            folderBackPath.AddLine(tabRightX + (outerBounds.Width * 0.10f), folderBackY, outerBounds.Right, folderBackY);
            graphics.DrawPath(iconPen, folderBackPath);
        }

        PointF[] flapPoints = new PointF[]
        {
            new PointF(outerBounds.Left, folderBackY),
            new PointF(outerBounds.Right, folderBackY),
            new PointF(outerBounds.Right - (outerBounds.Width * 0.14f), outerBounds.Bottom),
            new PointF(outerBounds.Left + (outerBounds.Width * 0.10f), outerBounds.Bottom),
        };

        graphics.DrawPolygon(iconPen, flapPoints);
    }

    private static void drawImageExportIcon(Graphics graphics, Rectangle bounds, Pen iconPen)
    {
        Rectangle outerBounds = ProductDrawing.insetRectangle(bounds, 2);
        int cornerRadius = Math.Max(2, outerBounds.Width / 9);

        using (GraphicsPath outerPath = ProductDrawing.createRoundedRectanglePath(outerBounds, cornerRadius))
        {
            graphics.DrawPath(iconPen, outerPath);
        }

        float circleDiameter = outerBounds.Width * 0.14f;
        RectangleF circleBounds = new RectangleF(
            outerBounds.Left + (outerBounds.Width * 0.18f),
            outerBounds.Top + (outerBounds.Height * 0.18f),
            circleDiameter,
            circleDiameter);
        graphics.DrawEllipse(iconPen, circleBounds);

        PointF[] mountainPoints = new PointF[]
        {
            new PointF(outerBounds.Left + (outerBounds.Width * 0.12f), outerBounds.Bottom - (outerBounds.Height * 0.18f)),
            new PointF(outerBounds.Left + (outerBounds.Width * 0.40f), outerBounds.Top + (outerBounds.Height * 0.50f)),
            new PointF(outerBounds.Left + (outerBounds.Width * 0.57f), outerBounds.Bottom - (outerBounds.Height * 0.30f)),
            new PointF(outerBounds.Left + (outerBounds.Width * 0.70f), outerBounds.Top + (outerBounds.Height * 0.56f)),
            new PointF(outerBounds.Right - (outerBounds.Width * 0.10f), outerBounds.Bottom - (outerBounds.Height * 0.18f)),
        };
        graphics.DrawLines(iconPen, mountainPoints);
    }

    private static void drawDirectionIcon(
        Graphics graphics,
        Rectangle bounds,
        Pen iconPen,
        EAppIcon directionIcon)
    {
        float centerY = bounds.Top + (bounds.Height / 2.0f);
        float leftX = bounds.Left + (bounds.Width * 0.18f);
        float rightX = bounds.Right - (bounds.Width * 0.18f);
        float arrowOffset = bounds.Height * 0.24f;

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
        Rectangle circleBounds = ProductDrawing.insetRectangle(bounds, 2);
        graphics.DrawEllipse(iconPen, circleBounds);

        PointF[] checkPoints = new PointF[]
        {
            new PointF(circleBounds.Left + (circleBounds.Width * 0.25f), circleBounds.Top + (circleBounds.Height * 0.52f)),
            new PointF(circleBounds.Left + (circleBounds.Width * 0.44f), circleBounds.Top + (circleBounds.Height * 0.70f)),
            new PointF(circleBounds.Left + (circleBounds.Width * 0.76f), circleBounds.Top + (circleBounds.Height * 0.34f)),
        };
        graphics.DrawLines(iconPen, checkPoints);
    }

    private static void drawWarningIcon(Graphics graphics, Rectangle bounds, Pen iconPen)
    {
        Rectangle outerBounds = ProductDrawing.insetRectangle(bounds, 2);
        PointF[] trianglePoints = new PointF[]
        {
            new PointF(outerBounds.Left + (outerBounds.Width / 2.0f), outerBounds.Top),
            new PointF(outerBounds.Right, outerBounds.Bottom),
            new PointF(outerBounds.Left, outerBounds.Bottom),
        };
        graphics.DrawPolygon(iconPen, trianglePoints);

        float centerX = outerBounds.Left + (outerBounds.Width / 2.0f);
        graphics.DrawLine(
            iconPen,
            centerX,
            outerBounds.Top + (outerBounds.Height * 0.32f),
            centerX,
            outerBounds.Top + (outerBounds.Height * 0.62f));
        graphics.DrawEllipse(
            iconPen,
            centerX - (iconPen.Width / 2.0f),
            outerBounds.Top + (outerBounds.Height * 0.76f),
            iconPen.Width,
            iconPen.Width);
    }

    private static void drawBusyIcon(Graphics graphics, Rectangle bounds, Pen iconPen)
    {
        Rectangle outerBounds = ProductDrawing.insetRectangle(bounds, 2);
        graphics.DrawArc(iconPen, outerBounds, -55.0f, 285.0f);

        float dotDiameter = Math.Max(2.0f, iconPen.Width * 1.5f);
        float dotX = outerBounds.Right - dotDiameter;
        float dotY = outerBounds.Top + (outerBounds.Height * 0.28f);
        using (SolidBrush dotBrush = new SolidBrush(iconPen.Color))
        {
            graphics.FillEllipse(dotBrush, dotX, dotY, dotDiameter, dotDiameter);
        }
    }
}
