using System;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Threading;

using Xunit;

namespace TimetableGenerator.Desktop.Tests;

public sealed class ProductTypographyTests
{
    private const string PRODUCT_FONT_RESOURCE_KEY = "ProductSystemFontFamily";
    private const string FLUENT_FONT_RESOURCE_KEY = "ContentControlThemeFontFamily";
    private const string PLATFORM_DEFAULT_FONT_NAME = "$Default";

    [AvaloniaFact]
    public void ProductTextUsesThePlatformSystemFontContract()
    {
        FontFamily productFontFamily = findRequiredFontFamily(
            PRODUCT_FONT_RESOURCE_KEY);
        FontFamily fluentFontFamily = findRequiredFontFamily(
            FLUENT_FONT_RESOURCE_KEY);
        TextBlock text = new TextBlock();
        text.Text = "시간표 Timetable";
        Button action = new Button();
        action.Content = "과목 추가";
        TextBox input = new TextBox();
        input.Text = "과목 검색";
        ComboBox selector = new ComboBox();
        selector.ItemsSource = new string[] { "개설 단위 전체" };
        Button popupAction = new Button();
        popupAction.Content = "계획 이름 바꾸기";
        Flyout actionFlyout = new Flyout();
        actionFlyout.Content = popupAction;

        StackPanel content = new StackPanel();
        content.Children.Add(text);
        content.Children.Add(action);
        content.Children.Add(input);
        content.Children.Add(selector);

        Window window = new Window();
        window.Content = content;

        try
        {
            window.Show();
            Dispatcher.UIThread.RunJobs();
            actionFlyout.ShowAt(action);
            Dispatcher.UIThread.RunJobs();

            Assert.Equal(PLATFORM_DEFAULT_FONT_NAME, productFontFamily.Name);
            Assert.Equal(productFontFamily, fluentFontFamily);
            Assert.Equal(productFontFamily, window.FontFamily);
            Assert.Equal(productFontFamily, text.FontFamily);
            Assert.Equal(productFontFamily, action.FontFamily);
            Assert.Equal(productFontFamily, input.FontFamily);
            Assert.Equal(productFontFamily, selector.FontFamily);
            Assert.True(actionFlyout.IsOpen);
            Assert.Equal(productFontFamily, popupAction.FontFamily);
        }
        finally
        {
            actionFlyout.Hide();
            window.Close();
        }
    }

    private static FontFamily findRequiredFontFamily(string resourceKey)
    {
        Avalonia.Application? applicationOrNull = Avalonia.Application.Current;
        Assert.NotNull(applicationOrNull);
        if (applicationOrNull == null)
        {
            throw new InvalidOperationException(
                "The Avalonia test application was not initialized.");
        }

        object? resourceOrNull;
        bool hasResource = applicationOrNull.TryGetResource(
            resourceKey,
            null,
            out resourceOrNull);
        Assert.True(hasResource);
        return Assert.IsType<FontFamily>(resourceOrNull);
    }
}
