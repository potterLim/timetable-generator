using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

using TimetableGenerator.Desktop.Exporting.AppleCalendar;

using Xunit;

namespace TimetableGenerator.Desktop.Tests.Exporting.AppleCalendar;

public sealed class FileAppleCalendarExportLeaseProviderTests
{
    [Fact]
    public async Task SeparateProvidersSerializeOnTheSameProcessWideFileAsync()
    {
        string directoryPath = createDirectoryPath();
        FileAppleCalendarExportLeaseProvider firstProvider =
            createProvider(directoryPath);
        FileAppleCalendarExportLeaseProvider secondProvider =
            createProvider(directoryPath);

        try
        {
            IAppleCalendarExportLease firstLease =
                await firstProvider.AcquireAsync(
                    TestContext.Current.CancellationToken);
            Task<IAppleCalendarExportLease> secondLeaseTask;
            await using (firstLease)
            {
                secondLeaseTask = secondProvider.AcquireAsync(
                    TestContext.Current.CancellationToken);
                await Task.Delay(
                    TimeSpan.FromMilliseconds(200.0),
                    TestContext.Current.CancellationToken);
                Assert.False(secondLeaseTask.IsCompleted);
            }

            IAppleCalendarExportLease secondLease =
                await secondLeaseTask.WaitAsync(
                    TimeSpan.FromSeconds(2.0),
                    TestContext.Current.CancellationToken);
            await using (secondLease)
            {
            }
        }
        finally
        {
            deleteDirectory(directoryPath);
        }
    }

    [Fact]
    public async Task CancelledWaitLeavesTheFileAvailableAfterOwnerReleasesAsync()
    {
        string directoryPath = createDirectoryPath();
        FileAppleCalendarExportLeaseProvider ownerProvider =
            createProvider(directoryPath);
        FileAppleCalendarExportLeaseProvider waitingProvider =
            createProvider(directoryPath);

        try
        {
            IAppleCalendarExportLease ownerLease =
                await ownerProvider.AcquireAsync(
                    TestContext.Current.CancellationToken);
            await using (ownerLease)
            {
                using (CancellationTokenSource cancellationSource =
                    new CancellationTokenSource())
                {
                    Task<IAppleCalendarExportLease> waitingLeaseTask =
                        waitingProvider.AcquireAsync(
                            cancellationSource.Token);
                    await Task.Delay(
                        TimeSpan.FromMilliseconds(200.0),
                        TestContext.Current.CancellationToken);
                    cancellationSource.Cancel();

                    await Assert.ThrowsAnyAsync<OperationCanceledException>(
                        async delegate
                        {
                            await waitingLeaseTask;
                        });
                }
            }

            IAppleCalendarExportLease nextLease =
                await waitingProvider.AcquireAsync(
                    TestContext.Current.CancellationToken);
            await using (nextLease)
            {
            }
        }
        finally
        {
            deleteDirectory(directoryPath);
        }
    }

    private static FileAppleCalendarExportLeaseProvider createProvider(
        string directoryPath)
    {
        return new FileAppleCalendarExportLeaseProvider(
            new AppleCalendarExportLockFilePath(
                Path.Combine(
                    directoryPath,
                    "apple-calendar-export.lock")));
    }

    private static string createDirectoryPath()
    {
        return Path.Combine(
            Path.GetTempPath(),
            "TimetableGenerator.Desktop.Tests",
            Guid.NewGuid().ToString("N"));
    }

    private static void deleteDirectory(string directoryPath)
    {
        if (Directory.Exists(directoryPath))
        {
            Directory.Delete(directoryPath, true);
        }
    }
}
