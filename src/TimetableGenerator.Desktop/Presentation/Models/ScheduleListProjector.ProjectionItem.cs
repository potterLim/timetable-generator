using TimetableGenerator.Domain.Scheduling;

namespace TimetableGenerator.Desktop.Presentation.Models;

internal static partial class ScheduleListProjector
{
    private sealed class ScheduleListProjectionItem
    {
        public string Title { get; }

        public EDay Day { get; }

        public DailyTimeRange TimeRange { get; }

        public ScheduleListMetadata Metadata { get; }

        public ScheduleListSource Source { get; }

        public ScheduleListProjectionItem(
            string title,
            EDay day,
            DailyTimeRange timeRange,
            ScheduleListMetadata metadata,
            ScheduleListSource source)
        {
            Title = title;
            Day = day;
            TimeRange = timeRange;
            Metadata = metadata;
            Source = source;
        }
    }
}
