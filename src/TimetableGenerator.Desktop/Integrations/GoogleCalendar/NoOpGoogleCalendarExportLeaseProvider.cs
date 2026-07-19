using System.Threading;
using System.Threading.Tasks;

namespace TimetableGenerator.Desktop.Integrations.GoogleCalendar;

internal sealed class NoOpGoogleCalendarExportLeaseProvider
    : IGoogleCalendarExportLeaseProvider
{
    public static NoOpGoogleCalendarExportLeaseProvider Instance { get; } =
        new NoOpGoogleCalendarExportLeaseProvider();

    private NoOpGoogleCalendarExportLeaseProvider()
    {
    }

    public Task<IGoogleCalendarExportLease> AcquireAsync(
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult<IGoogleCalendarExportLease>(NoOpLease.Instance);
    }

    private sealed class NoOpLease : IGoogleCalendarExportLease
    {
        public static NoOpLease Instance { get; } = new NoOpLease();

        private NoOpLease()
        {
        }

        public ValueTask DisposeAsync()
        {
            return ValueTask.CompletedTask;
        }
    }
}
