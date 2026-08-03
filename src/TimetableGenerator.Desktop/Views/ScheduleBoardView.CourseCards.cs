using System;

using Avalonia.Automation;
using Avalonia.Controls;

using TimetableGenerator.Desktop.Presentation.Models;

namespace TimetableGenerator.Desktop.Views;

internal sealed partial class ScheduleBoardView
{
    private void configureCourseCard(Button scheduleCard, CourseScheduleEntry entry)
    {
        scheduleCard.Classes.Add(findAccentClass(entry.Accent));
        ScheduleCardContent cardContent = mIsPngExport ? ScheduleCardContent.CreateForPngExport(entry) : new ScheduleCardContent(entry);
        scheduleCard.Content = createScheduleCardContent(cardContent);
        scheduleCard.Flyout = createCourseEntryFlyout(entry);

        string accessibleName = entry.Code + ", " + entry.SectionDisplayText + ", " + entry.CourseDetails.CreditsDisplayText + ", " + entry.Name + ", " + ScheduleBoardDayRange.FindFullDayDisplayName(entry.Day) + " " + entry.TimeRange + ", " + entry.InstructorDisplayText + ", " + entry.LocationDisplayText;
        AutomationProperties.SetName(scheduleCard, accessibleName);
        AutomationProperties.SetHelpText(scheduleCard, "선택하면 과목의 전체 시간, 교수, 강의실 정보를 엽니다.");
        ToolTip.SetTip(scheduleCard, entry.Name + " · " + entry.SectionDisplayText + Environment.NewLine + "선택하여 과목 상세 정보 보기");
        ToolTip.SetShowDelay(scheduleCard, 650);
    }

    private Flyout createCourseEntryFlyout(CourseScheduleEntry entry)
    {
        StackPanel details = createDetailsPanel();
        TextBlock identity = createFlyoutIdentity(entry.Code + " · " + entry.SectionDisplayText + " · " + entry.CourseDetails.CreditsDisplayText, "AccentBrush");
        details.Children.Add(identity);
        details.Children.Add(createFlyoutTitle(entry.Name));
        details.Children.Add(createFlyoutSeparator());
        string scheduleSummary = ScheduleBoardDayRange.CreateFullDayTimeDisplayText(entry.Day, entry.TimeRange);
        details.Children.Add(createDetailRow("시간", scheduleSummary));
        details.Children.Add(createDetailRow("장소", entry.LocationDisplayText));
        details.Children.Add(createDetailRow("교수", entry.InstructorDisplayText));
        return createDetailsFlyout(details, entry.Name + " 과목 상세 정보", entry.Day);
    }
}
