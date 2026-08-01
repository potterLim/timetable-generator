using System;
using System.Threading;
using System.Threading.Tasks;

using TimetableGenerator.Desktop.Exporting.AppleCalendar;
using TimetableGenerator.Desktop.Exporting.Calendar;

using Xunit;

namespace TimetableGenerator.Desktop.Tests.Exporting.AppleCalendar;

public sealed class AppleCalendarExportServiceLeaseAndPermissionTests
    : AppleCalendarExportServiceTestFixture
{
    [Fact]
    public async Task NativeAccessDenialReturnsTypedFailureAsync()
    {
        RecordingAppleCalendarNativeBridge nativeBridge = new RecordingAppleCalendarNativeBridge();
        nativeBridge.FailureOnNextMutationOrNull = new AppleCalendarNativeBridgeException(EAppleCalendarNativeFailureKind.AccessDenied, "apple_calendar_access_denied");

        AppleCalendarExportResult result = await new AppleCalendarExportService(nativeBridge).ExportAsync(createDocument(), new RecordingCalendarNameConflictResolver(ECalendarNameConflictResolution.Cancel), TestContext.Current.CancellationToken);

        Assert.Equal(EAppleCalendarExportStatus.AccessDenied, result.Status);
        Assert.Equal("apple_calendar_access_denied", result.DiagnosticCodeOrNull);
    }

    [Fact]
    public async Task ProcessLeaseCoversSnapshotAndMutationThenReleasesAsync()
    {
        RecordingAppleCalendarExportLeaseProvider leaseProvider = new RecordingAppleCalendarExportLeaseProvider();
        LeaseObservingAppleCalendarNativeBridge nativeBridge = new LeaseObservingAppleCalendarNativeBridge(leaseProvider);
        AppleCalendarExportService exporter = new AppleCalendarExportService(nativeBridge, leaseProvider);

        AppleCalendarExportResult result = await exporter.ExportAsync(createDocument(), new RecordingCalendarNameConflictResolver(ECalendarNameConflictResolution.Cancel), TestContext.Current.CancellationToken);

        Assert.Equal(EAppleCalendarExportStatus.Success, result.Status);
        Assert.True(nativeBridge.SnapshotObservedLease);
        Assert.True(nativeBridge.MutationObservedLease);
        Assert.Equal(1, leaseProvider.AcquireCount);
        Assert.Equal(0, leaseProvider.ActiveLeaseCount);
    }

    [Fact]
    public async Task NativeExceptionReleasesProcessLeaseAsync()
    {
        RecordingAppleCalendarExportLeaseProvider leaseProvider = new RecordingAppleCalendarExportLeaseProvider();
        RecordingAppleCalendarNativeBridge nativeBridge = new RecordingAppleCalendarNativeBridge();
        nativeBridge.FailureOnNextMutationOrNull = new AppleCalendarNativeBridgeException(EAppleCalendarNativeFailureKind.AccessDenied, "apple_calendar_access_denied");
        AppleCalendarExportService exporter = new AppleCalendarExportService(nativeBridge, leaseProvider);

        AppleCalendarExportResult result = await exporter.ExportAsync(createDocument(), new RecordingCalendarNameConflictResolver(ECalendarNameConflictResolution.Cancel), TestContext.Current.CancellationToken);

        Assert.Equal(EAppleCalendarExportStatus.AccessDenied, result.Status);
        Assert.Equal(1, leaseProvider.AcquireCount);
        Assert.Equal(0, leaseProvider.ActiveLeaseCount);
    }

    [Fact]
    public async Task CancellationReleasesProcessLeaseWithoutMutationAsync()
    {
        RecordingAppleCalendarExportLeaseProvider leaseProvider = new RecordingAppleCalendarExportLeaseProvider();
        ControlledPermissionAppleCalendarNativeBridge nativeBridge = new ControlledPermissionAppleCalendarNativeBridge(1);
        AppleCalendarExportService exporter = new AppleCalendarExportService(nativeBridge, leaseProvider);

        using (CancellationTokenSource cancellationSource = new CancellationTokenSource())
        {
            Task<AppleCalendarExportResult> exportTask = exporter.ExportAsync(createDocument(), new RecordingCalendarNameConflictResolver(ECalendarNameConflictResolution.Cancel), cancellationSource.Token);
            await nativeBridge.WaitForSnapshotRequestAsync(0, TestContext.Current.CancellationToken);

            Assert.Equal(1, leaseProvider.ActiveLeaseCount);
            cancellationSource.Cancel();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(
                async delegate
                {
                    await exportTask;
                });
        }

        Assert.Equal(1, leaseProvider.AcquireCount);
        Assert.Equal(0, leaseProvider.ActiveLeaseCount);
        Assert.Equal(0, nativeBridge.ApplyExportRequestCount);
    }

    [Fact]
    public async Task PendingPermissionContinuesTheSameExportAfterApprovalAsync()
    {
        ControlledPermissionAppleCalendarNativeBridge nativeBridge = new ControlledPermissionAppleCalendarNativeBridge(1);
        AppleCalendarExportService exporter = new AppleCalendarExportService(nativeBridge);

        Task<AppleCalendarExportResult> exportTask = exporter.ExportAsync(createDocument(), new RecordingCalendarNameConflictResolver(ECalendarNameConflictResolution.Cancel), TestContext.Current.CancellationToken);
        await nativeBridge.WaitForSnapshotRequestAsync(0, TestContext.Current.CancellationToken);

        await Task.Delay(TimeSpan.FromMilliseconds(50.0), TestContext.Current.CancellationToken);

        Assert.False(exportTask.IsCompleted);
        Assert.Equal(1, nativeBridge.CalendarSnapshotRequestCount);
        Assert.Equal(0, nativeBridge.ApplyExportRequestCount);

        nativeBridge.AllowSnapshot(0);
        AppleCalendarExportResult result = await exportTask;

        Assert.Equal(EAppleCalendarExportStatus.Success, result.Status);
        Assert.Equal(1, nativeBridge.CalendarSnapshotRequestCount);
        Assert.Equal(1, nativeBridge.ApplyExportRequestCount);
    }

    [Fact]
    public async Task DeniedPermissionDoesNotMutateAndAUserRetryCanSucceedAsync()
    {
        ControlledPermissionAppleCalendarNativeBridge nativeBridge = new ControlledPermissionAppleCalendarNativeBridge(2);
        AppleCalendarExportService exporter = new AppleCalendarExportService(nativeBridge);

        Task<AppleCalendarExportResult> deniedExportTask = exporter.ExportAsync(createDocument(), new RecordingCalendarNameConflictResolver(ECalendarNameConflictResolution.Cancel), TestContext.Current.CancellationToken);
        await nativeBridge.WaitForSnapshotRequestAsync(0, TestContext.Current.CancellationToken);
        nativeBridge.DenySnapshot(0);

        AppleCalendarExportResult deniedResult = await deniedExportTask;

        Assert.Equal(EAppleCalendarExportStatus.AccessDenied, deniedResult.Status);
        Assert.Equal(0, nativeBridge.ApplyExportRequestCount);

        Task<AppleCalendarExportResult> retryTask = exporter.ExportAsync(createDocument(), new RecordingCalendarNameConflictResolver(ECalendarNameConflictResolution.Cancel), TestContext.Current.CancellationToken);
        await nativeBridge.WaitForSnapshotRequestAsync(1, TestContext.Current.CancellationToken);
        nativeBridge.AllowSnapshot(1);

        AppleCalendarExportResult retryResult = await retryTask;

        Assert.Equal(EAppleCalendarExportStatus.Success, retryResult.Status);
        Assert.Equal(2, nativeBridge.CalendarSnapshotRequestCount);
        Assert.Equal(1, nativeBridge.ApplyExportRequestCount);
    }

    [Fact]
    public async Task PendingPermissionHonorsCancellationWithoutMutationAsync()
    {
        ControlledPermissionAppleCalendarNativeBridge nativeBridge = new ControlledPermissionAppleCalendarNativeBridge(1);
        AppleCalendarExportService exporter = new AppleCalendarExportService(nativeBridge);
        using (CancellationTokenSource cancellationSource = new CancellationTokenSource())
        {
            Task<AppleCalendarExportResult> exportTask = exporter.ExportAsync(createDocument(), new RecordingCalendarNameConflictResolver(ECalendarNameConflictResolution.Cancel), cancellationSource.Token);
            await nativeBridge.WaitForSnapshotRequestAsync(0, TestContext.Current.CancellationToken);

            cancellationSource.Cancel();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(
                async delegate
                {
                    await exportTask;
                });
        }

        Assert.Equal(1, nativeBridge.CalendarSnapshotRequestCount);
        Assert.Equal(0, nativeBridge.ApplyExportRequestCount);
    }

}
