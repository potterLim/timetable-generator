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
        scheduleCard.ZIndex = 2;

        CourseScheduleEntry? courseEntryOrNull = entry as CourseScheduleEntry;
        if (courseEntryOrNull != null)
        {
            configureCourseCard(scheduleCard, courseEntryOrNull);
        }
        else
        {
            PersonalScheduleEntry? personalEntryOrNull =
                entry as PersonalScheduleEntry;
            if (personalEntryOrNull == null)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(entry),
                    entry,
                    "Unknown schedule entry type.");
            }

            configurePersonalScheduleCard(scheduleCard, personalEntryOrNull);
        }

        int startRowOffset = getRowOffset(
            entry.TimeRange.Start.MinutesFromMidnight);
        int endRowOffset = getRowOffsetCeiling(
            entry.TimeRange.End.MinutesFromMidnight);
        Grid.SetRow(scheduleCard, 1 + startRowOffset);
        Grid.SetRowSpan(scheduleCard, Math.Max(1, endRowOffset - startRowOffset));
        Grid.SetColumn(scheduleCard, findDayColumn(entry.Day));
        mBoardGrid.Children.Add(scheduleCard);
    }

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

    private void configurePersonalScheduleCard(
        Button scheduleCard,
        PersonalScheduleEntry entry)
    {
        scheduleCard.Classes.Add("personal");
        scheduleCard.Content = createPersonalScheduleEntryContent(entry);
        scheduleCard.Flyout = createPersonalScheduleEntryFlyout(entry);
        AutomationProperties.SetAutomationId(
            scheduleCard,
            "PersonalScheduleCard:" + entry.ScheduleId);

        string accessibleName = "개인 일정, " + entry.Title + ", "
            + findDayName(entry.Day) + "요일 " + entry.TimeRange;
        AutomationProperties.SetName(scheduleCard, accessibleName);
        AutomationProperties.SetHelpText(
            scheduleCard,
            "선택하면 개인 일정의 시간과 세부 정보를 엽니다.");
        ToolTip.SetTip(
            scheduleCard,
            entry.Title + Environment.NewLine + "선택하여 일정 상세 정보 보기");
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

    private static Grid createPersonalScheduleEntryContent(
        PersonalScheduleEntry entry)
    {
        Grid content = new Grid();
        content.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        content.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        content.RowDefinitions.Add(new RowDefinition(GridLength.Auto));

        TextBlock eyebrow = createCardText(
            "개인 일정",
            10.0,
            FontWeight.SemiBold);
        content.Children.Add(eyebrow);

        TextBlock title = createCardText(entry.Title, 12.0, FontWeight.SemiBold);
        title.Margin = new Thickness(0.0, 2.0, 0.0, 0.0);
        title.TextWrapping = TextWrapping.NoWrap;
        title.TextTrimming = TextTrimming.CharacterEllipsis;
        Grid.SetRow(title, 1);
        content.Children.Add(title);

        TextBlock time = createCardText(
            entry.TimeRange.ToString(),
            10.0,
            FontWeight.Normal);
        time.Margin = new Thickness(0.0, 2.0, 0.0, 0.0);
        Grid.SetRow(time, 2);
        content.Children.Add(time);
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

    private Flyout createPersonalScheduleEntryFlyout(PersonalScheduleEntry entry)
    {
        StackPanel details = createDetailsPanel();
        details.Children.Add(createFlyoutIdentity(
            "개인 일정",
            "PersonalScheduleBorderBrush"));
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

    private static Flyout createDetailsFlyout(
        StackPanel details,
        string accessibleName)
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
