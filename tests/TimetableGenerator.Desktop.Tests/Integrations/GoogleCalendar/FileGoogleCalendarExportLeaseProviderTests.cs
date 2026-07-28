using System;
using System.IO;
using System.Threading.Tasks;
using TimetableGenerator.Desktop.Integrations.GoogleCalendar;
using Xunit;

namespace TimetableGenerator.Desktop.Tests.Integrations.GoogleCalendar;

public sealed class FileGoogleCalendarExportLeaseProviderTests
{
    [Fact]
    public async Task SecondProviderWaitsUntilFirstProcessLeaseIsReleasedAsync()
    {
        string directoryPath = Path.Combine(Path.GetTempPath(), "TimetableGenerator.Desktop.Tests", Guid.NewGuid().ToString("N"));
        GoogleCalendarExportLockFilePath path = new GoogleCalendarExportLockFilePath(Path.Combine(directoryPath, "google-calendar-export.lock"));
        FileGoogleCalendarExportLeaseProvider firstProvider = new FileGoogleCalendarExportLeaseProvider(path);
        FileGoogleCalendarExportLeaseProvider secondProvider = new FileGoogleCalendarExportLeaseProvider(path);

        try
        {
            IGoogleCalendarExportLease firstLease = await firstProvider.AcquireAsync(TestContext.Current.CancellationToken);
            Task<IGoogleCalendarExportLease> secondLeaseTask;
            await using (firstLease)
            {
                secondLeaseTask = secondProvider.AcquireAsync(TestContext.Current.CancellationToken);
                await Task.Delay(TimeSpan.FromMilliseconds(200.0), TestContext.Current.CancellationToken);
                Assert.False(secondLeaseTask.IsCompleted);
            }

            IGoogleCalendarExportLease secondLease = await secondLeaseTask.WaitAsync(TimeSpan.FromSeconds(2.0), TestContext.Current.CancellationToken);
            await using (secondLease)
            {
            }
        }
        finally
        {
            if (Directory.Exists(directoryPath))
            {
                Directory.Delete(directoryPath, true);
            }
        }
    }
}
