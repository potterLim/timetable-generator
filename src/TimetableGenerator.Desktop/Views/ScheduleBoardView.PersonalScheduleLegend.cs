using System.Collections.Generic;

using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Media;

using TimetableGenerator.Desktop.Presentation.Models;
using TimetableGenerator.Domain.Planning;

namespace TimetableGenerator.Desktop.Views;

internal sealed partial class ScheduleBoardView
{
    private void rebuildPersonalScheduleLegend(
        ScheduleRecommendation? recommendationOrNull)
    {
        mPersonalScheduleLegendEntries.Children.Clear();
        if (recommendationOrNull == null)
        {
            mPersonalScheduleLegendSurface.IsVisible = false;
            return;
        }

        HashSet<PersonalScheduleId> renderedScheduleIds =
            new HashSet<PersonalScheduleId>();
        foreach (ScheduleEntry entry in recommendationOrNull.Entries)
        {
            PersonalScheduleEntry? personalEntryOrNull =
                entry as PersonalScheduleEntry;
            if (personalEntryOrNull == null
                || renderedScheduleIds.Add(personalEntryOrNull.ScheduleId) == false)
            {
                continue;
            }

            mPersonalScheduleLegendEntries.Children.Add(
                createPersonalScheduleLegendRow(personalEntryOrNull.Schedule));
        }

        mPersonalScheduleLegendSurface.IsVisible =
            mPersonalScheduleLegendEntries.Children.Count > 0;
    }

    private Border createPersonalScheduleLegendRow(PersonalSchedule schedule)
    {
        PersonalScheduleItem item = new PersonalScheduleItem(schedule);
        StackPanel content = new StackPanel();
        content.Spacing = 3.0;

        TextBlock title = new TextBlock();
        title.Text = item.Title;
        title.FontSize = 12.0;
        title.FontWeight = FontWeight.SemiBold;
        content.Children.Add(title);

        string summary = item.TimeSummary;
        if (item.HasDetails)
        {
            summary += " · " + item.DetailsSummary;
        }

        TextBlock details = new TextBlock();
        details.Text = summary;
        details.FontSize = 11.0;
        details.Foreground = findBrush("TextSecondaryBrush");
        details.TextWrapping = TextWrapping.Wrap;
        content.Children.Add(details);

        Border row = new Border();
        row.Child = content;
        AutomationProperties.SetName(
            row,
            item.Title + " 개인 일정 내보내기 세부 정보");
        return row;
    }
}
