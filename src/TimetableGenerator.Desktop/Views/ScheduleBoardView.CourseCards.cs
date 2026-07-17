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

        string accessibleName = entry.Code + ", "
            + entry.CourseDetails.CreditsDisplayText + ", "
            + entry.Name + ", "
            + ScheduleBoardDayRange.FindFullDayDisplayName(entry.Day) + " "
            + entry.TimeRange + ", "
            + entry.InstructorDisplayText + ", "
            + entry.LocationDisplayText;
        AutomationProperties.SetName(scheduleCard, accessibleName);
        AutomationProperties.SetHelpText(
            scheduleCard,
            "선택하면 과목의 전체 시간, 교수, 강의실 정보를 엽니다.");
        ToolTip.SetTip(
            scheduleCard,
            entry.Name + Environment.NewLine + "선택하여 과목 상세 정보 보기");
    }

    private Grid createCourseEntryContent(CourseScheduleEntry entry)
    {
        Grid content = new Grid();
        content.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        content.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        content.RowDefinitions.Add(new RowDefinition(
            new GridLength(1.0, GridUnitType.Star)));
        content.RowDefinitions.Add(new RowDefinition(GridLength.Auto));

        Grid identity = new Grid();
        identity.ColumnDefinitions.Add(new ColumnDefinition(
            new GridLength(1.0, GridUnitType.Star)));
        identity.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));

        TextBlock code = createCardText(entry.Code, 10.5, FontWeight.SemiBold);
        code.Margin = new Thickness(0.0, 0.0, 6.0, 0.0);
        code.Foreground = findBrush("TextSecondaryBrush");
        identity.Children.Add(code);

        TextBlock credits = createCardText(
            entry.CourseDetails.CreditsDisplayText,
            9.5,
            FontWeight.SemiBold);
        credits.Foreground = findBrush("TextSecondaryBrush");
        Border creditsBadge = new Border();
        creditsBadge.Padding = new Thickness(5.0, 1.0);
        creditsBadge.CornerRadius = new CornerRadius(8.0);
        creditsBadge.Background = findBrush("ScheduleCardBadgeBackgroundBrush");
        creditsBadge.HorizontalAlignment = HorizontalAlignment.Right;
        creditsBadge.Child = credits;
        Grid.SetColumn(creditsBadge, 1);
        identity.Children.Add(creditsBadge);
        content.Children.Add(identity);

        TextBlock name = createCardText(entry.Name, 12.5, FontWeight.SemiBold);
        name.Margin = new Thickness(0.0, 4.0, 0.0, 0.0);
        name.TextWrapping = TextWrapping.NoWrap;
        name.TextTrimming = TextTrimming.CharacterEllipsis;
        Grid.SetRow(name, 1);
        content.Children.Add(name);

        Grid metadata = new Grid();
        metadata.ColumnDefinitions.Add(new ColumnDefinition(
            new GridLength(1.0, GridUnitType.Star)));
        metadata.ColumnDefinitions.Add(new ColumnDefinition(
            new GridLength(1.0, GridUnitType.Star)));
        metadata.ColumnSpacing = 8.0;
        metadata.Margin = new Thickness(0.0, 6.0, 0.0, 0.0);

        TextBlock instructor = createCardText(
            entry.InstructorDisplayText,
            9.5,
            FontWeight.Medium);
        instructor.Foreground = findBrush("TextSecondaryBrush");
        instructor.TextWrapping = TextWrapping.NoWrap;
        instructor.TextTrimming = TextTrimming.CharacterEllipsis;
        metadata.Children.Add(instructor);

        TextBlock location = createCardText(
            entry.LocationDisplayText,
            9.5,
            FontWeight.Normal);
        location.Foreground = findBrush("TextTertiaryBrush");
        location.TextAlignment = TextAlignment.Right;
        location.TextWrapping = TextWrapping.NoWrap;
        location.TextTrimming = TextTrimming.CharacterEllipsis;
        Grid.SetColumn(location, 1);
        metadata.Children.Add(location);

        Grid.SetRow(metadata, 3);
        content.Children.Add(metadata);
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
        string scheduleSummary =
            ScheduleBoardDayRange.FindFullDayDisplayName(entry.Day)
            + " · "
            + entry.TimeRange;
        details.Children.Add(createDetailRow("시간", scheduleSummary));
        details.Children.Add(createDetailRow("교수", entry.InstructorDisplayText));
        details.Children.Add(createDetailRow("장소", entry.LocationDisplayText));
        return createDetailsFlyout(details, entry.Name + " 과목 상세 정보");
    }
}
