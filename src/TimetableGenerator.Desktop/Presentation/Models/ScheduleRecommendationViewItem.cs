using System;

using TimetableGenerator.Domain.Planning;

namespace TimetableGenerator.Desktop.Presentation.Models;

internal sealed class ScheduleRecommendationViewItem
{
    public ScheduleRecommendation Schedule { get; }

    public ScheduleRecommendationBookmark? BookmarkOrNull { get; }

    public ScheduleRecommendationViewItem(
        ScheduleRecommendation schedule,
        ScheduleRecommendationBookmark? bookmarkOrNull)
    {
        if (schedule == null)
        {
            throw new ArgumentNullException(nameof(schedule));
        }

        Schedule = schedule;
        BookmarkOrNull = bookmarkOrNull;
    }
}
