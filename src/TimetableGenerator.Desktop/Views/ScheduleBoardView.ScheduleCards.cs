using System;

using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Media;

using TimetableGenerator.Desktop.Presentation.Models;

namespace TimetableGenerator.Desktop.Views;

internal sealed partial class ScheduleBoardView
{
    private void addScheduleEntry(ScheduleEntry entry)
    {
        Button scheduleCard = new Button();
        scheduleCard.Classes.Add("schedule-card");
        scheduleCard.ZIndex = 2;

        CourseScheduleEntry? courseEntryOrNull = entry as CourseScheduleEntry;
        if (courseEntryOrNull != null)
        {
            configureCourseCard(scheduleCard, courseEntryOrNull);
        }
        else
        {
            PersonalScheduleEntry? personalEntryOrNull = entry as PersonalScheduleEntry;
            if (personalEntryOrNull == null)
            {
                throw new ArgumentOutOfRangeException(nameof(entry), entry, "Unknown schedule entry type.");
            }

            configurePersonalScheduleCard(scheduleCard, personalEntryOrNull);
        }

        int startRowOffset = mRenderedLayout.TimeAxis.FindStartingRowOffset(entry.TimeRange.Start);
        int endRowOffset = mRenderedLayout.TimeAxis.FindEndingRowOffset(entry.TimeRange.End);
        Grid.SetRow(scheduleCard, 1 + startRowOffset);
        Grid.SetRowSpan(scheduleCard, Math.Max(1, endRowOffset - startRowOffset));
        ScheduleBoardDay boardDay = mRenderedLayout.DayRange.FindDay(entry.Day);
        Grid.SetColumn(scheduleCard, boardDay.ColumnIndex);
        mBoardGrid.Children.Add(scheduleCard);
    }

    private static StackPanel createDetailsPanel()
    {
        StackPanel details = new StackPanel();
        details.MinWidth = 280.0;
        details.MaxWidth = 360.0;
        details.Spacing = 12.0;
        return details;
    }

    private TextBlock createFlyoutIdentity(string text, string brushKey)
    {
        TextBlock identity = new TextBlock();
        identity.Text = text;
        identity.FontSize = 12.0;
        identity.FontWeight = FontWeight.SemiBold;
        identity.Foreground = findBrush(brushKey);
        return identity;
    }

    private static TextBlock createFlyoutTitle(string text)
    {
        TextBlock title = new TextBlock();
        title.Text = text;
        title.FontSize = 18.0;
        title.FontWeight = FontWeight.SemiBold;
        title.TextWrapping = TextWrapping.Wrap;
        return title;
    }

    private Border createFlyoutSeparator()
    {
        Border separator = new Border();
        separator.Height = 1.0;
        separator.Background = findBrush("BorderBrush");
        return separator;
    }

    private static Flyout createDetailsFlyout(StackPanel details, string accessibleName)
    {
        Border detailsSurface = new Border();
        detailsSurface.Padding = new Thickness(6.0);
        detailsSurface.Child = details;
        AutomationProperties.SetName(detailsSurface, accessibleName);

        Flyout detailsFlyout = new Flyout();
        detailsFlyout.Content = detailsSurface;
        return detailsFlyout;
    }

    private static Grid createDetailRow(string label, string value)
    {
        Grid row = new Grid();
        row.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(52.0, GridUnitType.Pixel)));
        row.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(1.0, GridUnitType.Star)));

        TextBlock labelText = new TextBlock();
        labelText.Text = label;
        labelText.FontSize = 12.0;
        labelText.FontWeight = FontWeight.SemiBold;
        row.Children.Add(labelText);

        TextBlock valueText = new TextBlock();
        valueText.Text = value;
        valueText.FontSize = 12.0;
        valueText.TextWrapping = TextWrapping.Wrap;
        Grid.SetColumn(valueText, 1);
        row.Children.Add(valueText);
        return row;
    }

    private static TextBlock createCardText(string text, double fontSize, FontWeight fontWeight)
    {
        TextBlock textBlock = new TextBlock();
        textBlock.Text = text;
        textBlock.FontSize = fontSize;
        textBlock.FontWeight = fontWeight;
        return textBlock;
    }

    private static string findAccentClass(ECourseAccent accent)
    {
        switch (accent)
        {
            case ECourseAccent.Blue:
                return "blue";
            case ECourseAccent.Purple:
                return "purple";
            case ECourseAccent.Green:
                return "green";
            default:
                throw new ArgumentOutOfRangeException(nameof(accent), accent, "Unknown course accent.");
        }
    }
}
