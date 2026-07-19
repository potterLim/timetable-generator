using System.Threading;
using System.Threading.Tasks;

namespace TimetableGenerator.Desktop.Exporting.AppleCalendar;

internal interface IAppleCalendarOpenCommand
{
    Task RunAsync(
        IcsCalendarFilePath calendarFilePath,
        CancellationToken cancellationToken);
}
