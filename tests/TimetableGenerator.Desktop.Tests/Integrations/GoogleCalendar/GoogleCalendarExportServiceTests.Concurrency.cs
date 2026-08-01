using System;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using TimetableGenerator.Desktop.Exporting.Calendar;
using TimetableGenerator.Desktop.Integrations.GoogleCalendar;
using Xunit;

namespace TimetableGenerator.Desktop.Tests.Integrations.GoogleCalendar;

public sealed partial class GoogleCalendarExportServiceTests
{
    [Fact]
    public async Task ProcessLeaseIsAcquiredBeforeOAuthAcrossExporterInstancesAsync()
    {
        string directoryPath = Path.Combine(Path.GetTempPath(), "TimetableGenerator.Desktop.Tests", Guid.NewGuid().ToString("N"));
        GoogleCalendarExportLockFilePath lockFilePath = new GoogleCalendarExportLockFilePath(Path.Combine(directoryPath, "google-calendar-export.lock"));
        BlockingAccessTokenProvider firstAccessTokenProvider = new BlockingAccessTokenProvider();
        CountingAccessTokenProvider secondAccessTokenProvider = new CountingAccessTokenProvider();
        using (GoogleCalendarExportService firstExporter = new GoogleCalendarExportService(
            firstAccessTokenProvider,
            new GoogleCalendarApiClient(new HttpClient(new TimeoutHttpMessageHandler())),
            new FileGoogleCalendarExportLeaseProvider(lockFilePath),
            null))
        using (GoogleCalendarExportService secondExporter = new GoogleCalendarExportService(
            secondAccessTokenProvider,
            new GoogleCalendarApiClient(new HttpClient(new TimeoutHttpMessageHandler())),
            new FileGoogleCalendarExportLeaseProvider(lockFilePath),
            null))
        using (CancellationTokenSource firstCancellationSource = new CancellationTokenSource())
        {
            try
            {
                Task<GoogleCalendarExportResult> firstExportTask = firstExporter.ExportAsync(createPlan(), new RecordingConflictResolver(ECalendarNameConflictResolution.Cancel), firstCancellationSource.Token);
                await firstAccessTokenProvider.Started.WaitAsync(TimeSpan.FromSeconds(2.0), TestContext.Current.CancellationToken);

                Task<GoogleCalendarExportResult> secondExportTask = secondExporter.ExportAsync(createPlan(), new RecordingConflictResolver(ECalendarNameConflictResolution.Cancel), TestContext.Current.CancellationToken);
                await Task.Delay(TimeSpan.FromMilliseconds(250.0), TestContext.Current.CancellationToken);

                Assert.Equal(0, secondAccessTokenProvider.RequestCount);
                Assert.False(secondExportTask.IsCompleted);

                firstCancellationSource.Cancel();
                await Assert.ThrowsAnyAsync<OperationCanceledException>(
                    async delegate
                    {
                        await firstExportTask;
                    });

                GoogleCalendarExportResult secondResult = await secondExportTask.WaitAsync(TimeSpan.FromSeconds(2.0), TestContext.Current.CancellationToken);
                Assert.Equal(EGoogleCalendarExportStatus.NotConfigured, secondResult.Status);
                Assert.Equal(1, secondAccessTokenProvider.RequestCount);
            }
            finally
            {
                firstCancellationSource.Cancel();
            }
        }

        if (Directory.Exists(directoryPath))
        {
            Directory.Delete(directoryPath, true);
        }
    }

    [Fact]
    public async Task AuthorizationExceptionReleasesProcessLeaseForRetryAsync()
    {
        SequencedAccessTokenProvider accessTokenProvider = new SequencedAccessTokenProvider();
        TrackingExportLeaseProvider exportLeaseProvider = new TrackingExportLeaseProvider();
        using (GoogleCalendarExportService exporter = new GoogleCalendarExportService(
            accessTokenProvider,
            new GoogleCalendarApiClient(new HttpClient(new TimeoutHttpMessageHandler())),
            exportLeaseProvider,
            null))
        {
            GoogleCalendarExportResult firstResult = await exporter.ExportAsync(createPlan(), new RecordingConflictResolver(ECalendarNameConflictResolution.Cancel), CancellationToken.None);
            GoogleCalendarExportResult secondResult = await exporter.ExportAsync(createPlan(), new RecordingConflictResolver(ECalendarNameConflictResolution.Cancel), CancellationToken.None);

            Assert.Equal(EGoogleCalendarExportStatus.Failed, firstResult.Status);
            Assert.Equal("google_calendar_local_state_failed", firstResult.DiagnosticCodeOrNull);
            Assert.Equal(EGoogleCalendarExportStatus.NotConfigured, secondResult.Status);
            Assert.Equal(2, exportLeaseProvider.AcquireCount);
            Assert.Equal(2, exportLeaseProvider.ReleaseCount);
        }
    }

    [Fact]
    public async Task DisposeDuringActiveExportCancelsBeforeReleasingOwnedResourcesAsync()
    {
        BlockingAccessTokenProvider accessTokenProvider = new BlockingAccessTokenProvider();
        TrackingDisposable ownedResources = new TrackingDisposable();
        GoogleCalendarExportService exporter = new GoogleCalendarExportService(accessTokenProvider, new GoogleCalendarApiClient(new HttpClient(new TimeoutHttpMessageHandler())), ownedResources);
        Task<GoogleCalendarExportResult> exportTask = exporter.ExportAsync(createPlan(), new RecordingConflictResolver(ECalendarNameConflictResolution.Cancel), CancellationToken.None);
        await accessTokenProvider.Started.WaitAsync(TimeSpan.FromSeconds(2.0), TestContext.Current.CancellationToken);

        exporter.Dispose();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            async delegate
            {
                await exportTask;
            });
        Assert.Equal(1, ownedResources.DisposeCount);
        exporter.Dispose();
        Assert.Equal(1, ownedResources.DisposeCount);
    }
}
