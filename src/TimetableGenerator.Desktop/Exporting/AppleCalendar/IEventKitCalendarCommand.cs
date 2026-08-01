using System.Threading;
using System.Threading.Tasks;

namespace TimetableGenerator.Desktop.Exporting.AppleCalendar;

internal interface IEventKitCalendarCommand
{
    bool IsAvailable { get; }

    Task<string> ExecuteAsync(string requestJson, CancellationToken cancellationToken);
}
