using System.Threading;
using System.Threading.Tasks;

namespace TimetableGenerator.Desktop.Exporting.AppleCalendar;

internal sealed class NoOpAppleCalendarExportLeaseProvider
    : IAppleCalendarExportLeaseProvider
{
    public static NoOpAppleCalendarExportLeaseProvider Instance { get; } =
        new NoOpAppleCalendarExportLeaseProvider();

    private NoOpAppleCalendarExportLeaseProvider()
    {
    }

    public Task<IAppleCalendarExportLease> AcquireAsync(
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult<IAppleCalendarExportLease>(
            NoOpLease.Instance);
    }

    private sealed class NoOpLease : IAppleCalendarExportLease
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
