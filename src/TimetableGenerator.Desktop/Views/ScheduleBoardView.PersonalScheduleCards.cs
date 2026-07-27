using System;
using System.Collections.Generic;
using System.Windows.Input;

using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;

using FluentIcons.Avalonia;
using FluentIcons.Common;

using TimetableGenerator.Desktop.Presentation;
using TimetableGenerator.Desktop.Presentation.Models;
using TimetableGenerator.Domain.Planning;

namespace TimetableGenerator.Desktop.Views;

internal sealed partial class ScheduleBoardView
{
    private const int REGULAR_PERSONAL_SCHEDULE_MINIMUM_DURATION_MINUTES = 60;

    private const double PERSONAL_SCHEDULE_EDIT_BUTTON_SIZE = 36.0;

    private void configurePersonalScheduleCard(Button scheduleCard, PersonalScheduleEntry entry)
    {
        scheduleCard.Classes.Add("personal");
        ScheduleCardContent cardContent = new ScheduleCardContent(entry);
        if (entry.TimeRange.DurationMinutes < REGULAR_PERSONAL_SCHEDULE_MINIMUM_DURATION_MINUTES)
        {
            scheduleCard.Classes.Add("compact");
            scheduleCard.Content = createCompactScheduleCardContent(cardContent);
        }
        else
        {
            scheduleCard.Content = createScheduleCardContent(cardContent);
        }

        scheduleCard.Flyout = createPersonalScheduleEntryFlyout(scheduleCard, entry);
        AutomationProperties.SetAutomationId(scheduleCard, "PersonalScheduleCard:" + entry.ScheduleId + ":" + entry.Day + ":" + entry.TimeRange.Start);

        string accessibleName = createPersonalScheduleAccessibleName(entry);
        AutomationProperties.SetName(scheduleCard, accessibleName);
        AutomationProperties.SetHelpText(scheduleCard, "선택하면 개인 일정의 시간과 세부 정보를 엽니다.");
        ToolTip.SetTip(scheduleCard, entry.TitleWithSection + Environment.NewLine + "선택하여 개인 일정 상세 정보 보기");
        ToolTip.SetShowDelay(scheduleCard, 650);
    }

    private static string createPersonalScheduleAccessibleName(PersonalScheduleEntry entry)
    {
        List<string> details = new List<string>();
        details.Add("개인 일정");
        details.Add(entry.Title);
        details.Add(ScheduleBoardDayRange.FindFullDayDisplayName(entry.Day) + " " + entry.TimeRange);
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

    private Flyout createPersonalScheduleEntryFlyout(Button scheduleCard, PersonalScheduleEntry entry)
    {
        StackPanel details = createDetailsPanel();
        Grid heading = createPersonalScheduleFlyoutHeading();
        details.Children.Add(heading);
        details.Children.Add(createFlyoutTitle(entry.TitleWithSection));
        details.Children.Add(createFlyoutSeparator());
        details.Children.Add(createDetailRow("시간", ScheduleBoardDayRange.CreateFullDayTimeDisplayText(entry.Day, entry.TimeRange)));

        if (entry.HasLocation)
        {
            details.Children.Add(createDetailRow("장소", entry.LocationDisplayText));
        }

        if (entry.HasInstructor)
        {
            details.Children.Add(createDetailRow("담당", entry.InstructorDisplayText));
        }

        Flyout detailsFlyout = createDetailsFlyout(details, entry.TitleWithSection + " 개인 일정 상세 정보", entry.Day);
        Button editButton = createPersonalScheduleEditButton(scheduleCard, entry, detailsFlyout);
        Grid.SetColumn(editButton, 1);
        heading.Children.Add(editButton);
        return detailsFlyout;
    }

    private Grid createPersonalScheduleFlyoutHeading()
    {
        Grid heading = new Grid();
        heading.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(1.0, GridUnitType.Star)));
        heading.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));

        TextBlock identity = createFlyoutIdentity("개인 일정", "TextSecondaryBrush");
        identity.VerticalAlignment = VerticalAlignment.Center;
        heading.Children.Add(identity);
        return heading;
    }

    private Button createPersonalScheduleEditButton(
        Button scheduleCard,
        PersonalScheduleEntry entry,
        Flyout detailsFlyout)
    {
        FluentIcon editIcon = new FluentIcon();
        editIcon.Icon = Icon.Edit;
        editIcon.IconVariant = IconVariant.Regular;
        editIcon.FontSize = 16.0;

        Button editButton = new Button();
        editButton.Classes.Add("icon");
        editButton.Width = PERSONAL_SCHEDULE_EDIT_BUTTON_SIZE;
        editButton.Height = PERSONAL_SCHEDULE_EDIT_BUTTON_SIZE;
        editButton.MinWidth = PERSONAL_SCHEDULE_EDIT_BUTTON_SIZE;
        editButton.MinHeight = PERSONAL_SCHEDULE_EDIT_BUTTON_SIZE;
        editButton.Content = editIcon;
        editButton.Command = new DelegateCommand(
            () => beginPersonalScheduleEditing(
                scheduleCard,
                entry.ScheduleId,
                detailsFlyout),
            () => canEditPersonalSchedule(entry.ScheduleId));
        editButton.HorizontalAlignment = HorizontalAlignment.Right;
        editButton.VerticalAlignment = VerticalAlignment.Center;
        AutomationProperties.SetAutomationId(editButton, "EditPersonalScheduleButton:" + entry.ScheduleId);
        AutomationProperties.SetName(editButton, entry.TitleWithSection + " 개인 일정 수정");
        AutomationProperties.SetHelpText(editButton, "현재 개인 일정 정보가 채워진 수정 창을 엽니다.");
        ToolTip.SetTip(editButton, "개인 일정 수정");
        return editButton;
    }

    private bool canEditPersonalSchedule(PersonalScheduleId scheduleId)
    {
        ICommand? editCommandOrNull = EditPersonalScheduleCommand;
        return editCommandOrNull != null
            && editCommandOrNull.CanExecute(scheduleId);
    }

    private void beginPersonalScheduleEditing(
        Button scheduleCard,
        PersonalScheduleId scheduleId,
        Flyout detailsFlyout)
    {
        ICommand? editCommandOrNull = EditPersonalScheduleCommand;
        if (editCommandOrNull == null || editCommandOrNull.CanExecute(scheduleId) == false)
        {
            throw new InvalidOperationException("Personal schedule editing requires an executable command.");
        }

        detailsFlyout.Hide();
        scheduleCard.Focus();
        editCommandOrNull.Execute(scheduleId);
    }
}
