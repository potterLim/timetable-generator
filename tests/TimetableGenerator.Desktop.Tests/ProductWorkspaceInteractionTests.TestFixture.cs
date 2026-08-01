using System;
using System.Linq;

using Avalonia;
using Avalonia.Controls;
using Avalonia.VisualTree;

using FluentIcons.Avalonia;

using Xunit;

namespace TimetableGenerator.Desktop.Tests;

public sealed partial class ProductWorkspaceInteractionTests
{
    private const double MINIMUM_PRODUCT_HEIGHT = 640.0;

    private static readonly TimeSpan AUTOSAVE_INDICATOR_REVEAL_WAIT = TimeSpan.FromMilliseconds(750.0);

    private static Window createWindow(Control content, double width)
    {
        Window window = new Window();
        window.Width = width;
        window.Height = MINIMUM_PRODUCT_HEIGHT;
        window.Content = content;
        return window;
    }

    private static TControl findRequiredControl<TControl>(Control root, string controlName)
        where TControl : Control
    {
        TControl? controlOrNull = root.FindControl<TControl>(controlName);
        if (controlOrNull == null)
        {
            throw new InvalidOperationException("The required workspace control was not found: " + controlName);
        }

        return controlOrNull;
    }

    private static Point findRequiredPosition(Control control, Control relativeTo)
    {
        Point? positionOrNull = control.TranslatePoint(new Point(0.0, 0.0), relativeTo);
        if (positionOrNull == null)
        {
            throw new InvalidOperationException("The workspace control was not attached to the requested surface.");
        }

        return positionOrNull.Value;
    }

    private static void assertCentered(Control dialog, Control host)
    {
        Point? dialogPositionOrNull = dialog.TranslatePoint(new Point(0.0, 0.0), host);
        Assert.NotNull(dialogPositionOrNull);
        if (dialogPositionOrNull == null)
        {
            throw new InvalidOperationException("The plan dialog was not attached to the workspace.");
        }

        Point dialogPosition = dialogPositionOrNull.Value;
        double dialogCenterX = dialogPosition.X + (dialog.Bounds.Width / 2.0);
        double dialogCenterY = dialogPosition.Y + (dialog.Bounds.Height / 2.0);
        Assert.InRange(Math.Abs(dialogCenterX - (host.Bounds.Width / 2.0)), 0.0, 1.0);
        Assert.InRange(Math.Abs(dialogCenterY - (host.Bounds.Height / 2.0)), 0.0, 1.0);
    }

    private static void assertCompoundHeaderButtonAlignment(Button button)
    {
        FluentIcon[] icons = button.GetVisualDescendants().OfType<FluentIcon>().ToArray();
        TextBlock text = button.GetVisualDescendants().OfType<TextBlock>().Single();
        Point textPosition = findRequiredPosition(text, button);
        double textCenterY = textPosition.Y + (text.Bounds.Height / 2.0);

        Assert.NotEmpty(icons);
        Assert.InRange(button.Bounds.Height, 39.99, 40.01);
        foreach (FluentIcon icon in icons)
        {
            Point iconPosition = findRequiredPosition(icon, button);
            double iconCenterY = iconPosition.Y + (icon.Bounds.Height / 2.0);
            Assert.InRange(Math.Abs(iconCenterY - textCenterY), 0.0, 0.5);
        }
    }
}
