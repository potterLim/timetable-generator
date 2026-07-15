using System;
using System.Collections.Generic;
using System.Diagnostics;
using TimetableGenerator.Presentation.Schedules;
using CoreDay = TimetableGenerator.Core.Domain.EDay;

namespace TimetableGenerator.UI.Product;

internal static class ScheduleSummaryTextFormatter
{
    internal static string formatSidebarSummary(ScheduleGridSummary summary)
    {
        if (summary == null)
        {
            throw new ArgumentNullException(nameof(summary));
        }

        string activeDaysText = formatActiveDays(summary.ActiveDays, "·");
        return summary.SelectedCourseCount + "개 과목 · " + activeDaysText;
    }

    internal static string formatWorkspaceSummary(ScheduleGridSummary summary)
    {
        if (summary == null)
        {
            throw new ArgumentNullException(nameof(summary));
        }

        string activeDaysText = formatActiveDays(summary.ActiveDays, ", ");
        return summary.SelectedCourseCount + "개 과목 · " +
            summary.ScheduledMeetingCount + "개 수업 · " + activeDaysText;
    }

    private static string formatActiveDays(
        IReadOnlyList<CoreDay> activeDays,
        string separator)
    {
        if (activeDays == null)
        {
            throw new ArgumentNullException(nameof(activeDays));
        }

        if (separator == null)
        {
            throw new ArgumentNullException(nameof(separator));
        }

        List<string> dayDisplayNames = new List<string>(activeDays.Count);
        foreach (CoreDay activeDay in activeDays)
        {
            dayDisplayNames.Add(getDayDisplayName(activeDay));
        }

        return string.Join(separator, dayDisplayNames);
    }

    private static string getDayDisplayName(CoreDay day)
    {
        switch (day)
        {
            case CoreDay.Monday:
                return "월";
            case CoreDay.Tuesday:
                return "화";
            case CoreDay.Wednesday:
                return "수";
            case CoreDay.Thursday:
                return "목";
            case CoreDay.Friday:
                return "금";
            case CoreDay.Saturday:
                return "토";
            case CoreDay.Sunday:
                return "일";
            case CoreDay.None:
            default:
                Debug.Fail("Unexpected active schedule day: " + day);
                throw new ArgumentOutOfRangeException(nameof(day));
        }
    }
}
