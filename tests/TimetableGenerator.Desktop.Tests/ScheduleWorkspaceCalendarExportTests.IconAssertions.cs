using System;
using System.Linq;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.VisualTree;

using Xunit;

namespace TimetableGenerator.Desktop.Tests;

public sealed partial class ScheduleWorkspaceCalendarExportTests
{
    private static void assertExportPngImageIconPresentation(MenuItem menuItem)
    {
        assertExportRasterLogoPresentation(menuItem, "ExportPngLogoSlot", "ExportPngLogoImage", 24.0, 24.0, null);
    }

    private static void assertExportAllPngMultipleImageIconPresentation(MenuItem menuItem)
    {
        assertExportMenuItemPresentation(menuItem);
        Grid iconSlot = Assert.IsType<Grid>(menuItem.Icon);
        Assert.Equal("ExportAllPngLogoSlot", iconSlot.Name);
        Assert.Equal(24.0, iconSlot.Width);
        Assert.Equal(24.0, iconSlot.Height);
        Assert.Equal(HorizontalAlignment.Center, iconSlot.HorizontalAlignment);
        Assert.Equal(VerticalAlignment.Center, iconSlot.VerticalAlignment);
        Assert.Contains("export-menu-logo-slot", iconSlot.Classes);

        Image backImage = findRequiredImage(iconSlot, "ExportAllPngBackImage");
        Image middleImage = findRequiredImage(iconSlot, "ExportAllPngMiddleImage");
        Image frontImage = findRequiredImage(iconSlot, "ExportAllPngFrontImage");
        assertStackedPngImagePresentation(backImage, 12.0, new Thickness(1.0, 2.0, 0.0, 0.0), -8.0);
        assertStackedPngImagePresentation(middleImage, 12.0, new Thickness(11.0, 2.0, 0.0, 0.0), 8.0);
        assertStackedPngImagePresentation(frontImage, 16.0, new Thickness(4.0, 8.0, 0.0, 0.0), null);
        Assert.Collection(
            iconSlot.Children,
            child => Assert.Same(backImage, child),
            child => Assert.Same(middleImage, child),
            child => Assert.Same(frontImage, child));
    }

    private static Image findRequiredImage(Grid iconSlot, string imageName)
    {
        Image? imageOrNull = iconSlot.FindControl<Image>(imageName);
        Assert.NotNull(imageOrNull);
        if (imageOrNull == null)
        {
            throw new InvalidOperationException("The stacked PNG export image was not found: " + imageName);
        }

        return imageOrNull;
    }

    private static void assertStackedPngImagePresentation(Image image, double size, Thickness margin, double? rotationAngleOrNull)
    {
        Assert.NotNull(image.Source);
        Assert.Equal(size, image.Width);
        Assert.Equal(size, image.Height);
        Assert.Equal(margin, image.Margin);
        Assert.Equal(HorizontalAlignment.Left, image.HorizontalAlignment);
        Assert.Equal(VerticalAlignment.Top, image.VerticalAlignment);
        Assert.Equal(Stretch.Uniform, image.Stretch);
        Assert.Contains("export-menu-logo", image.Classes);

        if (rotationAngleOrNull.HasValue)
        {
            Assert.Equal(RelativePoint.Center, image.RenderTransformOrigin);
            RotateTransform rotation = Assert.IsType<RotateTransform>(image.RenderTransform);
            Assert.Equal(rotationAngleOrNull.Value, rotation.Angle);
        }
        else
        {
            Assert.Null(image.RenderTransform);
        }
    }

    private static Grid assertExportRasterLogoPresentation(
        MenuItem menuItem,
        string slotName,
        string imageName,
        double imageWidth,
        double imageHeight,
        double? verticalTranslationOrNull)
    {
        assertExportMenuItemPresentation(menuItem);
        Grid logoSlot = Assert.IsType<Grid>(menuItem.Icon);
        Assert.Equal(slotName, logoSlot.Name);
        Assert.Equal(24.0, logoSlot.Width);
        Assert.Equal(24.0, logoSlot.Height);
        Assert.Equal(HorizontalAlignment.Center, logoSlot.HorizontalAlignment);
        Assert.Equal(VerticalAlignment.Center, logoSlot.VerticalAlignment);
        Assert.Contains("export-menu-logo-slot", logoSlot.Classes);

        Image? logoImageOrNull = logoSlot.FindControl<Image>(imageName);
        Assert.NotNull(logoImageOrNull);
        if (logoImageOrNull == null)
        {
            throw new InvalidOperationException("The export menu logo image was not found: " + imageName);
        }

        Assert.NotNull(logoImageOrNull.Source);
        Assert.Equal(imageWidth, logoImageOrNull.Width);
        Assert.Equal(imageHeight, logoImageOrNull.Height);
        Assert.Equal(Stretch.Uniform, logoImageOrNull.Stretch);
        Assert.Equal(HorizontalAlignment.Center, logoImageOrNull.HorizontalAlignment);
        Assert.Equal(VerticalAlignment.Center, logoImageOrNull.VerticalAlignment);
        Assert.Contains("export-menu-logo", logoImageOrNull.Classes);
        Assert.Same(logoImageOrNull, Assert.Single(logoSlot.Children));

        if (verticalTranslationOrNull.HasValue)
        {
            TranslateTransform translation = Assert.IsType<TranslateTransform>(logoImageOrNull.RenderTransform);
            Assert.Equal(verticalTranslationOrNull.Value, translation.Y);
        }
        else
        {
            Assert.Null(logoImageOrNull.RenderTransform);
        }

        return logoSlot;
    }

    private static void assertAppleCalendarIconPresentation(MenuItem menuItem)
    {
        Assert.Contains("export-menu-item", menuItem.Classes);
        if (menuItem.IsVisible)
        {
            assertExportMenuItemPresentation(menuItem);
        }

        Grid logoSlot = Assert.IsType<Grid>(menuItem.Icon);
        Assert.Equal("ExportAppleCalendarIconSlot", logoSlot.Name);
        Assert.Equal(24.0, logoSlot.Width);
        Assert.Equal(24.0, logoSlot.Height);
        Assert.Equal(HorizontalAlignment.Center, logoSlot.HorizontalAlignment);
        Assert.Equal(VerticalAlignment.Center, logoSlot.VerticalAlignment);
        Assert.Contains("export-menu-logo-slot", logoSlot.Classes);

        Image? iconImageOrNull = logoSlot.FindControl<Image>("ExportAppleCalendarIconImage");
        Assert.NotNull(iconImageOrNull);
        if (iconImageOrNull == null)
        {
            throw new InvalidOperationException("The Apple Calendar icon image was not found.");
        }

        Assert.NotNull(iconImageOrNull.Source);
        Assert.True(iconImageOrNull.IsVisible);
        Assert.Equal(24.0, iconImageOrNull.Width);
        Assert.Equal(24.0, iconImageOrNull.Height);
        Assert.Equal(Stretch.Uniform, iconImageOrNull.Stretch);
        Assert.Equal(HorizontalAlignment.Center, iconImageOrNull.HorizontalAlignment);
        Assert.Equal(VerticalAlignment.Center, iconImageOrNull.VerticalAlignment);
        Assert.Contains("export-menu-logo", iconImageOrNull.Classes);
        Assert.Null(iconImageOrNull.RenderTransform);
        Assert.Same(iconImageOrNull, Assert.Single(logoSlot.Children));
    }

    private static void assertExportMenuItemPresentation(MenuItem menuItem)
    {
        Assert.Contains("export-menu-item", menuItem.Classes);

        ContentControl iconPresenter = menuItem.GetVisualDescendants().OfType<ContentControl>().Single(control => string.Equals(control.Name, "PART_IconPresenter", StringComparison.Ordinal));
        Assert.Equal(24.0, iconPresenter.Width);
        Assert.Equal(24.0, iconPresenter.Height);
        Assert.Equal(HorizontalAlignment.Center, iconPresenter.HorizontalAlignment);
        Assert.Equal(VerticalAlignment.Center, iconPresenter.VerticalAlignment);
        Assert.Equal(HorizontalAlignment.Center, iconPresenter.HorizontalContentAlignment);
        Assert.Equal(VerticalAlignment.Center, iconPresenter.VerticalContentAlignment);

        Point? iconOriginOrNull = iconPresenter.TranslatePoint(new Point(0.0, 0.0), menuItem);
        Assert.NotNull(iconOriginOrNull);
        if (iconOriginOrNull == null)
        {
            throw new InvalidOperationException("The export menu icon geometry could not be resolved.");
        }

        double menuItemCenterY = menuItem.Bounds.Height / 2.0;
        double iconCenterY = iconOriginOrNull.Value.Y + (iconPresenter.Bounds.Height / 2.0);
        double iconCenterDelta = iconCenterY - menuItemCenterY;
        Assert.True(Math.Abs(iconCenterDelta) <= MAXIMUM_CENTER_DELTA_DIP, "Export menu icon center delta=" + iconCenterDelta + ", item height=" + menuItem.Bounds.Height + ", icon top=" + iconOriginOrNull.Value.Y + ", icon height=" + iconPresenter.Bounds.Height + ".");

        string headerText = Assert.IsType<string>(menuItem.Header);
        TextBlock header = menuItem.GetVisualDescendants().OfType<TextBlock>().Single(candidate => candidate.Text == headerText);
        Point? headerOriginOrNull = header.TranslatePoint(new Point(0.0, 0.0), menuItem);
        Assert.NotNull(headerOriginOrNull);
        if (headerOriginOrNull == null)
        {
            throw new InvalidOperationException("The export menu header geometry could not be resolved.");
        }

        double headerCenterY = headerOriginOrNull.Value.Y + (header.Bounds.Height / 2.0);
        double headerCenterDelta = headerCenterY - menuItemCenterY;
        Assert.True(Math.Abs(headerCenterDelta) <= MAXIMUM_CENTER_DELTA_DIP, "Export menu header center delta=" + headerCenterDelta + ", item height=" + menuItem.Bounds.Height + ", header top=" + headerOriginOrNull.Value.Y + ", header height=" + header.Bounds.Height + ".");
    }

    private static ThemeVariant[] getProductThemeVariants()
    {
        return new ThemeVariant[]
        {
            ThemeVariant.Light,
            ThemeVariant.Dark,
        };
    }
}
