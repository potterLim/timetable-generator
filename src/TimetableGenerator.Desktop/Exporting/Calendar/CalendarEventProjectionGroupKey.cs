using TimetableGenerator.Domain.Scheduling;

namespace TimetableGenerator.Desktop.Exporting.Calendar;

internal readonly record struct CalendarEventProjectionGroupKey(
    CalendarEventSourceIdentity SourceIdentity,
    DailyTimeRange TimeRange);
