using System;
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
    private const string PRODUCT_FONT_FAMILY_NAME = "Pretendard";

    [AvaloniaFact]
    public void AppliesBundledPretendardAcrossProductText()
    {
        FontFamily productFontFamily = findRequiredFontFamily(PRODUCT_FONT_RESOURCE_KEY);
        FontFamily fluentFontFamily = findRequiredFontFamily(FLUENT_FONT_RESOURCE_KEY);
        TextBlock text = new TextBlock();
        text.Text = "시간표 Timetable";
        Button action = new Button();
        action.Content = "과목 추가";
        TextBox input = new TextBox();
        input.Text = "과목 검색";
        ComboBox selector = new ComboBox();
        selector.ItemsSource = new string[] { "개설 단위 전체" };
        Button popupAction = new Button();
        popupAction.Content = "시간표 이름 바꾸기";
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

            Assert.Equal(PRODUCT_FONT_FAMILY_NAME, productFontFamily.Name);
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

    [AvaloniaFact]
    public void ResolvesBundledPretendardProductWeights()
    {
        FontFamily productFontFamily = findRequiredFontFamily(PRODUCT_FONT_RESOURCE_KEY);
        FontWeight[] productFontWeights = new FontWeight[]
        {
            FontWeight.Normal,
            FontWeight.Medium,
            FontWeight.SemiBold,
            FontWeight.Bold,
        };

        foreach (FontWeight productFontWeight in productFontWeights)
        {
            Typeface productTypeface = new Typeface(productFontFamily, FontStyle.Normal, productFontWeight);
            GlyphTypeface? resolvedTypefaceOrNull;
            bool hasResolvedTypeface = FontManager.Current.TryGetGlyphTypeface(productTypeface, out resolvedTypefaceOrNull);

            Assert.True(hasResolvedTypeface);
            Assert.NotNull(resolvedTypefaceOrNull);
            Assert.Equal(PRODUCT_FONT_FAMILY_NAME, resolvedTypefaceOrNull.TypographicFamilyName);
            Assert.Equal(productFontWeight, resolvedTypefaceOrNull.Weight);
        }
    }

    private static FontFamily findRequiredFontFamily(string resourceKey)
    {
        Avalonia.Application? applicationOrNull = Avalonia.Application.Current;
        Assert.NotNull(applicationOrNull);
        if (applicationOrNull == null)
        {
            throw new InvalidOperationException("The Avalonia test application was not initialized.");
        }

        object? resourceOrNull;
        bool hasResource = applicationOrNull.TryGetResource(resourceKey, null, out resourceOrNull);
        Assert.True(hasResource);
        return Assert.IsType<FontFamily>(resourceOrNull);
    }
}
