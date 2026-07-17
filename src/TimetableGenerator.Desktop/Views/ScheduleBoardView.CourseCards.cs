using System;

using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

using TimetableGenerator.Desktop.Presentation.Models;

namespace TimetableGenerator.Desktop.Views;

internal sealed partial class ScheduleBoardView
{
    private void configureCourseCard(
        Button scheduleCard,
        CourseScheduleEntry entry)
    {
        scheduleCard.Classes.Add(findAccentClass(entry.Accent));
        scheduleCard.Content = createCourseEntryContent(entry);
        scheduleCard.Flyout = createCourseEntryFlyout(entry);

        string accessibleName = entry.Code + ", " + entry.Name + ", "
            + findDayName(entry.Day) + "요일 " + entry.Period.Value + "교시 "
            + entry.TimeRange + ", "
            + entry.InstructorDisplayText + ", "
            + entry.LocationDisplayText;
        AutomationProperties.SetName(scheduleCard, accessibleName);
        AutomationProperties.SetHelpText(
            scheduleCard,
            "선택하면 과목의 전체 시간, 담당교원, 강의실 정보를 엽니다.");
        ToolTip.SetTip(
            scheduleCard,
            entry.Name + Environment.NewLine + "선택하여 과목 상세 정보 보기");
    }

    private static Grid createCourseEntryContent(CourseScheduleEntry entry)
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

        TextBlock code = createCardText(entry.Code, 10.5, FontWeight.SemiBold);
        code.Margin = new Thickness(0.0, 0.0, 6.0, 0.0);
        identity.Children.Add(code);

        TextBlock credits = createCardText(
            entry.CourseDetails.CreditsDisplayText,
            10.0,
            FontWeight.Normal);
        credits.HorizontalAlignment = HorizontalAlignment.Right;
        Grid.SetColumn(credits, 1);
        identity.Children.Add(credits);
        content.Children.Add(identity);

        TextBlock name = createCardText(entry.Name, 12.0, FontWeight.SemiBold);
        name.Margin = new Thickness(0.0, 2.0, 0.0, 0.0);
        name.TextWrapping = TextWrapping.NoWrap;
        name.TextTrimming = TextTrimming.CharacterEllipsis;
        Grid.SetRow(name, 1);
        content.Children.Add(name);

        TextBlock instructor = createCardText(
            entry.InstructorDisplayText,
            9.5,
            FontWeight.Normal);
        instructor.Margin = new Thickness(0.0, 2.0, 0.0, 0.0);
        instructor.TextWrapping = TextWrapping.NoWrap;
        instructor.TextTrimming = TextTrimming.CharacterEllipsis;
        Grid.SetRow(instructor, 2);
        content.Children.Add(instructor);

        TextBlock location = createCardText(
            entry.LocationDisplayText,
            9.5,
            FontWeight.Normal);
        location.Margin = new Thickness(0.0, 2.0, 0.0, 0.0);
        location.TextWrapping = TextWrapping.NoWrap;
        location.TextTrimming = TextTrimming.CharacterEllipsis;
        Grid.SetRow(location, 3);
        content.Children.Add(location);
        return content;
    }

    private Flyout createCourseEntryFlyout(CourseScheduleEntry entry)
    {
        StackPanel details = createDetailsPanel();
        TextBlock identity = createFlyoutIdentity(
            entry.Code + " · " + entry.CourseDetails.CreditsDisplayText,
            "AccentBrush");
        details.Children.Add(identity);
        details.Children.Add(createFlyoutTitle(entry.Name));
        details.Children.Add(createFlyoutSeparator());
        string scheduleSummary = findDayName(entry.Day) + "요일 "
            + entry.Period.Value + "교시 · " + entry.TimeRange;
        details.Children.Add(createDetailRow("시간", scheduleSummary));
        details.Children.Add(createDetailRow("담당", entry.InstructorDisplayText));
        details.Children.Add(createDetailRow("장소", entry.LocationDisplayText));
        return createDetailsFlyout(details, entry.Name + " 과목 상세 정보");
    }
}
