using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

using TimetableGenerator.Desktop.Presentation.Models;

namespace TimetableGenerator.Desktop.Views;

internal sealed partial class ScheduleBoardView
{
    private const double SCHEDULE_CARD_TITLE_FONT_SIZE = 14.0;
    private const double SCHEDULE_CARD_TITLE_LINE_HEIGHT = 18.0;
    private const double SCHEDULE_CARD_LOCATION_FONT_SIZE = 11.5;
    private const double SCHEDULE_CARD_RESPONSIBLE_PERSON_FONT_SIZE = 10.5;
    private const double SCHEDULE_CARD_PRIMARY_GAP = 6.0;
    private const double SCHEDULE_CARD_SECONDARY_GAP = 2.0;

    private Grid createScheduleCardContent(ScheduleCardContent cardContent)
    {
        Grid content = new Grid();
        content.HorizontalAlignment = HorizontalAlignment.Stretch;
        content.VerticalAlignment = VerticalAlignment.Center;
        content.RowDefinitions.Add(new RowDefinition(GridLength.Auto));

        TextBlock title = createCardText(
            cardContent.Title,
            SCHEDULE_CARD_TITLE_FONT_SIZE,
            FontWeight.Bold);
        title.HorizontalAlignment = HorizontalAlignment.Stretch;
        title.LineHeight = SCHEDULE_CARD_TITLE_LINE_HEIGHT;
        title.MaxLines = 2;
        title.TextAlignment = TextAlignment.Center;
        title.TextWrapping = TextWrapping.Wrap;
        title.TextTrimming = TextTrimming.CharacterEllipsis;
        content.Children.Add(title);

        int nextRowIndex = 1;
        string? locationOrNull = cardContent.LocationOrNull;
        if (locationOrNull != null)
        {
            content.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            TextBlock location = createCardText(
                locationOrNull,
                SCHEDULE_CARD_LOCATION_FONT_SIZE,
                FontWeight.Medium);
            location.Margin = new Thickness(
                0.0,
                SCHEDULE_CARD_PRIMARY_GAP,
                0.0,
                0.0);
            location.HorizontalAlignment = HorizontalAlignment.Stretch;
            location.Foreground = findBrush("TextPrimaryBrush");
            location.TextAlignment = TextAlignment.Center;
            location.TextWrapping = TextWrapping.NoWrap;
            location.TextTrimming = TextTrimming.CharacterEllipsis;
            Grid.SetRow(location, nextRowIndex);
            content.Children.Add(location);
            ++nextRowIndex;
        }

        string? responsiblePersonOrNull = cardContent.ResponsiblePersonOrNull;
        if (responsiblePersonOrNull != null)
        {
            content.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            TextBlock responsiblePerson = createCardText(
                responsiblePersonOrNull,
                SCHEDULE_CARD_RESPONSIBLE_PERSON_FONT_SIZE,
                FontWeight.Normal);
            double responsiblePersonTopMargin = locationOrNull != null
                ? SCHEDULE_CARD_SECONDARY_GAP
                : SCHEDULE_CARD_PRIMARY_GAP;
            responsiblePerson.Margin = new Thickness(
                0.0,
                responsiblePersonTopMargin,
                0.0,
                0.0);
            responsiblePerson.HorizontalAlignment = HorizontalAlignment.Stretch;
            responsiblePerson.Foreground = findBrush("TextSecondaryBrush");
            responsiblePerson.TextAlignment = TextAlignment.Center;
            responsiblePerson.TextWrapping = TextWrapping.NoWrap;
            responsiblePerson.TextTrimming = TextTrimming.CharacterEllipsis;
            Grid.SetRow(responsiblePerson, nextRowIndex);
            content.Children.Add(responsiblePerson);
        }

        return content;
    }

    private static TextBlock createCompactScheduleCardContent(
        ScheduleCardContent cardContent)
    {
        TextBlock title = createCardText(
            cardContent.Title,
            SCHEDULE_CARD_TITLE_FONT_SIZE,
            FontWeight.Bold);
        title.HorizontalAlignment = HorizontalAlignment.Stretch;
        title.VerticalAlignment = VerticalAlignment.Center;
        title.LineHeight = SCHEDULE_CARD_TITLE_LINE_HEIGHT;
        title.MaxLines = 1;
        title.TextAlignment = TextAlignment.Center;
        title.TextWrapping = TextWrapping.NoWrap;
        title.TextTrimming = TextTrimming.CharacterEllipsis;
        return title;
    }
}
