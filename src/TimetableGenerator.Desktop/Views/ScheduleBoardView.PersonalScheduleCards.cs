using System;
using System.Collections.Generic;

using Avalonia.Automation;
using Avalonia.Controls;

using TimetableGenerator.Desktop.Presentation.Models;

namespace TimetableGenerator.Desktop.Views;

internal sealed partial class ScheduleBoardView
{
    private const int REGULAR_PERSONAL_SCHEDULE_MINIMUM_DURATION_MINUTES = 60;

    private void configurePersonalScheduleCard(
        Button scheduleCard,
        PersonalScheduleEntry entry)
    {
        scheduleCard.Classes.Add("personal");
        ScheduleCardContent cardContent = new ScheduleCardContent(entry);
        if (entry.TimeRange.DurationMinutes
            < REGULAR_PERSONAL_SCHEDULE_MINIMUM_DURATION_MINUTES)
        {
            scheduleCard.Classes.Add("compact");
            scheduleCard.Content = createCompactScheduleCardContent(cardContent);
        }
        else
        {
            scheduleCard.Content = createScheduleCardContent(cardContent);
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

    private static string createPersonalScheduleAccessibleName(
        PersonalScheduleEntry entry)
    {
        List<string> details = new List<string>();
        details.Add("개인 일정");
        details.Add(entry.Title);
        details.Add(
            ScheduleBoardDayRange.FindFullDayDisplayName(entry.Day)
            + " "
            + entry.TimeRange);
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
            ScheduleBoardDayRange.FindFullDayDisplayName(entry.Day)
            + " · "
            + entry.TimeRange));
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
