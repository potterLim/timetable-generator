using System;

using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

using TimetableGenerator.Desktop.Presentation.Models;
using TimetableGenerator.Domain.Scheduling;

namespace TimetableGenerator.Desktop.Views;

internal sealed partial class ScheduleBoardView
{
    private void addScheduleEntry(ScheduleEntry entry)
    {
        Button scheduleCard = new Button();
        scheduleCard.Classes.Add("schedule-card");
        scheduleCard.Classes.Add(findAccentClass(entry.Accent));
        scheduleCard.Content = createScheduleEntryContent(entry);
        scheduleCard.Flyout = createScheduleEntryFlyout(entry);
        scheduleCard.ZIndex = 1;

        int periodIndex = entry.Period.Value - 1;
        string accessibleName = entry.Code + ", " + entry.Name + ", "
            + findDayName(entry.Day) + "요일 " + entry.Period.Value + "교시 "
            + PERIOD_TIME_RANGES[periodIndex] + ", "
            + entry.InstructorDisplayText + ", "
            + entry.LocationDisplayText;
        AutomationProperties.SetName(scheduleCard, accessibleName);
        AutomationProperties.SetHelpText(
            scheduleCard,
            "선택하면 과목의 전체 시간, 담당교원, 강의실 정보를 엽니다.");
        ToolTip.SetTip(scheduleCard, "과목 상세 정보 보기");

        Grid.SetRow(scheduleCard, entry.Period.Value);
        Grid.SetColumn(scheduleCard, findDayColumn(entry.Day));
        mBoardGrid.Children.Add(scheduleCard);
    }

    private static Grid createScheduleEntryContent(ScheduleEntry entry)
    {
        Grid content = new Grid();
        content.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        content.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        content.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        content.RowDefinitions.Add(new RowDefinition(GridLength.Auto));

        Grid identity = new Grid();
        identity.ColumnDefinitions.Add(new ColumnDefinition(
            new GridLength(1.0, GridUnitType.Star)));
        identity.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));

        TextBlock code = createCardText(entry.Code, 11.0, FontWeight.SemiBold);
        code.Margin = new Thickness(0.0, 0.0, 6.0, 0.0);
        identity.Children.Add(code);

        TextBlock credits = createCardText(
            entry.CourseDetails.CreditsDisplayText,
            10.5,
            FontWeight.Normal);
        credits.HorizontalAlignment = HorizontalAlignment.Right;
        Grid.SetColumn(credits, 1);
        identity.Children.Add(credits);
        content.Children.Add(identity);

        TextBlock name = createCardText(entry.Name, 12.0, FontWeight.SemiBold);
        name.Margin = new Thickness(0.0, 3.0, 0.0, 0.0);
        name.TextWrapping = TextWrapping.Wrap;
        Grid.SetRow(name, 1);
        content.Children.Add(name);

        TextBlock instructor = createCardText(
            entry.InstructorDisplayText,
            11.0,
            FontWeight.Normal);
        instructor.Margin = new Thickness(0.0, 3.0, 0.0, 0.0);
        instructor.TextWrapping = TextWrapping.Wrap;
        Grid.SetRow(instructor, 2);
        content.Children.Add(instructor);

        TextBlock location = createCardText(
            entry.LocationDisplayText,
            11.0,
            FontWeight.Normal);
        location.Margin = new Thickness(0.0, 2.0, 0.0, 0.0);
        location.TextWrapping = TextWrapping.Wrap;
        Grid.SetRow(location, 3);
        content.Children.Add(location);

        return content;
    }

    private Flyout createScheduleEntryFlyout(ScheduleEntry entry)
    {
        StackPanel details = new StackPanel();
        details.MinWidth = 280.0;
        details.MaxWidth = 360.0;
        details.Spacing = 12.0;

        TextBlock identity = new TextBlock();
        identity.Text = entry.Code + " · " + entry.CourseDetails.CreditsDisplayText;
        identity.FontSize = 12.0;
        identity.FontWeight = FontWeight.SemiBold;
        identity.Foreground = findBrush("AccentBrush");
        details.Children.Add(identity);

        TextBlock name = new TextBlock();
        name.Text = entry.Name;
        name.FontSize = 18.0;
        name.FontWeight = FontWeight.SemiBold;
        name.TextWrapping = TextWrapping.Wrap;
        details.Children.Add(name);

        Border separator = new Border();
        separator.Height = 1.0;
        separator.Background = findBrush("BorderBrush");
        details.Children.Add(separator);

        int periodIndex = entry.Period.Value - 1;
        string scheduleSummary = findDayName(entry.Day) + "요일 "
            + entry.Period.Value + "교시 · " + PERIOD_TIME_RANGES[periodIndex];
        details.Children.Add(createDetailRow("시간", scheduleSummary));
        details.Children.Add(createDetailRow("담당", entry.InstructorDisplayText));
        details.Children.Add(createDetailRow("장소", entry.LocationDisplayText));

        Border detailsSurface = new Border();
        detailsSurface.Padding = new Thickness(6.0);
        detailsSurface.Child = details;
        AutomationProperties.SetName(detailsSurface, entry.Name + " 과목 상세 정보");

        Flyout detailsFlyout = new Flyout();
        detailsFlyout.Content = detailsSurface;
        return detailsFlyout;
    }

    private static Grid createDetailRow(string label, string value)
    {
        Grid row = new Grid();
        row.ColumnDefinitions.Add(new ColumnDefinition(
            new GridLength(52.0, GridUnitType.Pixel)));
        row.ColumnDefinitions.Add(new ColumnDefinition(
            new GridLength(1.0, GridUnitType.Star)));

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

    private static TextBlock createCardText(
        string text,
        double fontSize,
        FontWeight fontWeight)
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
                throw new ArgumentOutOfRangeException(
                    nameof(accent),
                    accent,
                    "Unknown course accent.");
        }
    }

    private static int findDayColumn(EDay day)
    {
        switch (day)
        {
            case EDay.Monday:
                return 1;
            case EDay.Tuesday:
                return 2;
            case EDay.Wednesday:
                return 3;
            case EDay.Thursday:
                return 4;
            case EDay.Friday:
                return 5;
            default:
                throw new ArgumentOutOfRangeException(
                    nameof(day),
                    day,
                    "Unknown academic day.");
        }
    }

    private static string findDayName(EDay day)
    {
        switch (day)
        {
            case EDay.Monday:
                return "월";
            case EDay.Tuesday:
                return "화";
            case EDay.Wednesday:
                return "수";
            case EDay.Thursday:
                return "목";
            case EDay.Friday:
                return "금";
            default:
                throw new ArgumentOutOfRangeException(
                    nameof(day),
                    day,
                    "Unknown academic day.");
        }
    }
}
