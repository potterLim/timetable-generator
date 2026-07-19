using System.Threading;
using System.Threading.Tasks;
using TimetableGenerator.Domain.Planning;

namespace TimetableGenerator.Desktop.Integrations.GoogleCalendar;

internal interface IGoogleCalendarBindingStore
{
    Task<GoogleCalendarId?> GetCalendarIdOrNullAsync(
        PlanId planId,
        CancellationToken cancellationToken);

    Task SaveCalendarIdAsync(
        PlanId planId,
        GoogleCalendarId calendarId,
        CancellationToken cancellationToken);

    Task DeleteCalendarIdAsync(
        PlanId planId,
        CancellationToken cancellationToken);
}
