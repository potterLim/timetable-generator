using System;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace TimetableGenerator.UI.Product;

internal static class ProductDrawing
{
    internal static GraphicsPath createRoundedRectanglePath(Rectangle bounds, int cornerRadius)
    {
        if (cornerRadius < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(cornerRadius));
        }

        GraphicsPath path = new GraphicsPath();
        if (bounds.Width <= 0 || bounds.Height <= 0)
        {
            return path;
        }

        int maximumRadius = Math.Min(bounds.Width, bounds.Height) / 2;
        int appliedRadius = Math.Min(cornerRadius, maximumRadius);
        if (appliedRadius == 0)
        {
            path.AddRectangle(bounds);
            return path;
        }

        int diameter = appliedRadius * 2;
        Rectangle topLeftArc = new Rectangle(bounds.Left, bounds.Top, diameter, diameter);
        Rectangle topRightArc = new Rectangle(bounds.Right - diameter, bounds.Top, diameter, diameter);
        Rectangle bottomRightArc = new Rectangle(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter);
        Rectangle bottomLeftArc = new Rectangle(bounds.Left, bounds.Bottom - diameter, diameter, diameter);

        path.AddArc(topLeftArc, 180.0f, 90.0f);
        path.AddArc(topRightArc, 270.0f, 90.0f);
        path.AddArc(bottomRightArc, 0.0f, 90.0f);
        path.AddArc(bottomLeftArc, 90.0f, 90.0f);
        path.CloseFigure();

        return path;
    }

    internal static Rectangle insetRectangle(Rectangle bounds, int inset)
    {
        if (inset < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(inset));
        }

        int insetWidth = Math.Max(0, bounds.Width - (inset * 2));
        int insetHeight = Math.Max(0, bounds.Height - (inset * 2));
        return new Rectangle(bounds.X + inset, bounds.Y + inset, insetWidth, insetHeight);
    }

    internal static Rectangle fitCenteredSquare(Rectangle bounds, int requestedSize)
    {
        if (requestedSize < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(requestedSize));
        }

        int squareSize = Math.Min(requestedSize, Math.Min(bounds.Width, bounds.Height));
        int squareX = bounds.X + ((bounds.Width - squareSize) / 2);
        int squareY = bounds.Y + ((bounds.Height - squareSize) / 2);
        return new Rectangle(squareX, squareY, squareSize, squareSize);
    }
}
