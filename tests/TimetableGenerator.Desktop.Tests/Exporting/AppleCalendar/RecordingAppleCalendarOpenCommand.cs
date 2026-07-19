using System.Threading;
using System.Threading.Tasks;

using TimetableGenerator.Desktop.Exporting.AppleCalendar;

namespace TimetableGenerator.Desktop.Tests.Exporting.AppleCalendar;

internal sealed class RecordingAppleCalendarOpenCommand : IAppleCalendarOpenCommand
{
    public IcsCalendarFilePath? OpenedCalendarFilePathOrNull { get; private set; }

    public Task RunAsync(
        IcsCalendarFilePath calendarFilePath,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        OpenedCalendarFilePathOrNull = calendarFilePath;
        return Task.CompletedTask;
    }
}
