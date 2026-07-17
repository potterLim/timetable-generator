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
    private const double COURSE_CARD_TITLE_FONT_SIZE = 13.0;
    private const double COURSE_CARD_TITLE_LINE_HEIGHT = 16.5;
    private const double COURSE_CARD_LOCATION_FONT_SIZE = 11.5;
    private const double COURSE_CARD_INSTRUCTOR_FONT_SIZE = 10.5;
    private const double COURSE_CARD_PRIMARY_GAP = 4.0;
    private const double COURSE_CARD_SECONDARY_GAP = 2.0;

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
        content.HorizontalAlignment = HorizontalAlignment.Stretch;
        content.VerticalAlignment = VerticalAlignment.Center;
        content.RowDefinitions.Add(new RowDefinition(GridLength.Auto));

        TextBlock name = createCardText(
            entry.Name,
            COURSE_CARD_TITLE_FONT_SIZE,
            FontWeight.SemiBold);
        name.HorizontalAlignment = HorizontalAlignment.Stretch;
        name.LineHeight = COURSE_CARD_TITLE_LINE_HEIGHT;
        name.MaxLines = 2;
        name.TextAlignment = TextAlignment.Center;
        name.TextWrapping = TextWrapping.Wrap;
        name.TextTrimming = TextTrimming.CharacterEllipsis;
        content.Children.Add(name);

        int nextRowIndex = 1;
        if (entry.HasAssignedLocation)
        {
            content.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            TextBlock location = createCardText(
                entry.LocationDisplayText,
                COURSE_CARD_LOCATION_FONT_SIZE,
                FontWeight.Medium);
            location.Margin = new Thickness(
                0.0,
                COURSE_CARD_PRIMARY_GAP,
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

        if (entry.HasConfirmedInstructor)
        {
            content.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            TextBlock instructor = createCardText(
                entry.InstructorDisplayText,
                COURSE_CARD_INSTRUCTOR_FONT_SIZE,
                FontWeight.Normal);
            double instructorTopMargin = entry.HasAssignedLocation
                ? COURSE_CARD_SECONDARY_GAP
                : COURSE_CARD_PRIMARY_GAP;
            instructor.Margin = new Thickness(0.0, instructorTopMargin, 0.0, 0.0);
            instructor.HorizontalAlignment = HorizontalAlignment.Stretch;
            instructor.Foreground = findBrush("TextSecondaryBrush");
            instructor.TextAlignment = TextAlignment.Center;
            instructor.TextWrapping = TextWrapping.NoWrap;
            instructor.TextTrimming = TextTrimming.CharacterEllipsis;
            Grid.SetRow(instructor, nextRowIndex);
            content.Children.Add(instructor);
        }

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
