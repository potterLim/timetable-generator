using System;
using System.Collections.Generic;

using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

using TimetableGenerator.Desktop.Presentation.Models;

namespace TimetableGenerator.Desktop.Views;

internal sealed partial class ScheduleBoardView
{
    private const int REGULAR_PERSONAL_SCHEDULE_MINIMUM_DURATION_MINUTES = 45;

    private void configurePersonalScheduleCard(
        Button scheduleCard,
        PersonalScheduleEntry entry)
    {
        scheduleCard.Classes.Add("personal");
        if (entry.TimeRange.DurationMinutes
            < REGULAR_PERSONAL_SCHEDULE_MINIMUM_DURATION_MINUTES)
        {
            scheduleCard.Classes.Add("compact");
            scheduleCard.Content = createCompactPersonalScheduleEntryContent(entry);
        }
        else
        {
            scheduleCard.Content = createPersonalScheduleEntryContent(entry);
        }

        scheduleCard.Flyout = createPersonalScheduleEntryFlyout(entry);
        AutomationProperties.SetAutomationId(
            scheduleCard,
            "PersonalScheduleCard:"
            + entry.ScheduleId
            + ":"
            + entry.Day
            + ":"
            + entry.TimeRange.Start);

        string accessibleName = createPersonalScheduleAccessibleName(entry);
        AutomationProperties.SetName(scheduleCard, accessibleName);
        AutomationProperties.SetHelpText(
            scheduleCard,
            "선택하면 개인 일정의 시간과 세부 정보를 엽니다.");
        ToolTip.SetTip(
            scheduleCard,
            accessibleName + Environment.NewLine + "선택하여 일정 상세 정보 보기");
    }

    private static Grid createPersonalScheduleEntryContent(
        PersonalScheduleEntry entry)
    {
        Grid content = new Grid();
        content.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        content.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        content.RowDefinitions.Add(new RowDefinition(GridLength.Auto));

        Grid identity = new Grid();
        identity.ColumnDefinitions.Add(new ColumnDefinition(
            new GridLength(1.0, GridUnitType.Star)));
        identity.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
        TextBlock eyebrow = createCardText(
            "개인 일정",
            9.5,
            FontWeight.SemiBold);
        identity.Children.Add(eyebrow);

        if (entry.HasSection)
        {
            TextBlock section = createCardText(
                entry.SectionDisplayText,
                9.5,
                FontWeight.Normal);
            section.HorizontalAlignment = HorizontalAlignment.Right;
            Grid.SetColumn(section, 1);
            identity.Children.Add(section);
        }

        content.Children.Add(identity);

        TextBlock title = createCardText(entry.Title, 11.5, FontWeight.SemiBold);
        title.Margin = new Thickness(0.0, 1.0, 0.0, 0.0);
        title.TextWrapping = TextWrapping.NoWrap;
        title.TextTrimming = TextTrimming.CharacterEllipsis;
        Grid.SetRow(title, 1);
        content.Children.Add(title);

        TextBlock metadata = createCardText(
            createPersonalScheduleCardMetadata(entry),
            9.0,
            FontWeight.Normal);
        metadata.Margin = new Thickness(0.0, 1.0, 0.0, 0.0);
        metadata.TextWrapping = TextWrapping.NoWrap;
        metadata.TextTrimming = TextTrimming.CharacterEllipsis;
        Grid.SetRow(metadata, 2);
        content.Children.Add(metadata);
        return content;
    }

    private static TextBlock createCompactPersonalScheduleEntryContent(
        PersonalScheduleEntry entry)
    {
        TextBlock title = createCardText(entry.Title, 10.5, FontWeight.SemiBold);
        title.VerticalAlignment = VerticalAlignment.Center;
        title.TextWrapping = TextWrapping.NoWrap;
        title.TextTrimming = TextTrimming.CharacterEllipsis;
        return title;
    }

    private static string createPersonalScheduleCardMetadata(
        PersonalScheduleEntry entry)
    {
        List<string> metadata = new List<string>();
        metadata.Add(entry.TimeRange.ToString());
        if (entry.HasInstructor)
        {
            metadata.Add(entry.InstructorDisplayText);
        }

        if (entry.HasLocation)
        {
            metadata.Add(entry.LocationDisplayText);
        }

        return string.Join(" · ", metadata);
    }

    private static string createPersonalScheduleAccessibleName(
        PersonalScheduleEntry entry)
    {
        List<string> details = new List<string>();
        details.Add("개인 일정");
        details.Add(entry.Title);
        details.Add(findDayName(entry.Day) + "요일 " + entry.TimeRange);
        if (entry.HasSection)
        {
            details.Add("분반 " + entry.SectionDisplayText);
        }

        if (entry.HasInstructor)
        {
            details.Add("담당 " + entry.InstructorDisplayText);
        }

        if (entry.HasLocation)
        {
            details.Add("장소 " + entry.LocationDisplayText);
        }

        return string.Join(", ", details);
    }

    private Flyout createPersonalScheduleEntryFlyout(PersonalScheduleEntry entry)
    {
        StackPanel details = createDetailsPanel();
        details.Children.Add(createFlyoutIdentity(
            "개인 일정",
            "TextSecondaryBrush"));
        details.Children.Add(createFlyoutTitle(entry.Title));
        details.Children.Add(createFlyoutSeparator());
        details.Children.Add(createDetailRow(
            "시간",
            findDayName(entry.Day) + "요일 · " + entry.TimeRange));
        if (entry.HasSection)
        {
            details.Children.Add(createDetailRow("분반", entry.SectionDisplayText));
        }

        if (entry.HasInstructor)
        {
            details.Children.Add(createDetailRow("담당", entry.InstructorDisplayText));
        }

        if (entry.HasLocation)
        {
            details.Children.Add(createDetailRow("장소", entry.LocationDisplayText));
        }

        return createDetailsFlyout(details, entry.Title + " 개인 일정 상세 정보");
    }
}
