using System.Threading;
using System.Threading.Tasks;

namespace TimetableGenerator.Desktop.Exporting.AppleCalendar;

internal interface IAppleCalendarAutomationCommand
{
    bool IsAvailable { get; }

    Task<string> ExecuteAsync(
        EAppleCalendarAutomationOperation operation,
        string requestJson,
        CancellationToken cancellationToken);
}
