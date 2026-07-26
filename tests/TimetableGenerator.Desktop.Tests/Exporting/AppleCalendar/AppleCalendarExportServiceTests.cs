using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using TimetableGenerator.Desktop.Exporting.AppleCalendar;
using TimetableGenerator.Desktop.Exporting.Calendar;
using TimetableGenerator.Domain.Catalogs;
using TimetableGenerator.Domain.Planning;
using TimetableGenerator.Domain.Scheduling;

using Xunit;

namespace TimetableGenerator.Desktop.Tests.Exporting.AppleCalendar;

public sealed class AppleCalendarExportServiceTests
{
    [Fact]
    public async Task AvailableNameCreatesCalendarWithoutPromptAsync()
    {
        RecordingAppleCalendarNativeBridge nativeBridge = new RecordingAppleCalendarNativeBridge();
        RecordingCalendarNameConflictResolver conflictResolver = new RecordingCalendarNameConflictResolver(ECalendarNameConflictResolution.Cancel);
        AppleCalendarExportService exporter = new AppleCalendarExportService(nativeBridge);

        AppleCalendarExportResult result = await exporter.ExportAsync(
            createDocument(),
            conflictResolver,
            TestContext.Current.CancellationToken);

        Assert.Equal(EAppleCalendarExportStatus.Success, result.Status);
        Assert.Equal("2026-2학기 시간표", result.CalendarNameOrNull?.Value);
        Assert.Equal(1, result.CreatedEventCount);
        Assert.Equal(0, result.DeletedEventCount);
        Assert.Empty(conflictResolver.Conflicts);
        AppleCalendarExportMutation mutation = Assert.Single(nativeBridge.AppliedMutations);
        Assert.Equal(EAppleCalendarExportMutationKind.CreateNew, mutation.Kind);
        Assert.Equal("2026-2학기 시간표", mutation.DestinationName.Value);
        Assert.Null(mutation.ExistingCalendarIdOrNull);
    }

    [Fact]
    public async Task ManagedWritableNameCollisionCanReplaceExistingCalendarAsync()
    {
        AppleCalendarId existingCalendarId = new AppleCalendarId("existing-calendar");
        RecordingAppleCalendarNativeBridge nativeBridge =
            new RecordingAppleCalendarNativeBridge(
                createCalendar(
                    existingCalendarId,
                    "2026-2학기 시간표",
                    EAppleCalendarOwnership.ApplicationManaged,
                    EAppleCalendarContentAccess.Writable));
        RecordingCalendarNameConflictResolver conflictResolver = new RecordingCalendarNameConflictResolver(ECalendarNameConflictResolution.ReplaceExisting);

        AppleCalendarExportResult result =
            await new AppleCalendarExportService(nativeBridge).ExportAsync(
                createDocument(),
                conflictResolver,
                TestContext.Current.CancellationToken);

        Assert.Equal(EAppleCalendarExportStatus.Success, result.Status);
        Assert.Equal(existingCalendarId, result.CalendarIdOrNull);
        Assert.Equal(1, result.DeletedEventCount);
        CalendarNameConflict conflict = Assert.Single(conflictResolver.Conflicts);
        Assert.True(conflict.CanReplace);
        Assert.Equal("2026-2학기 시간표 (2)", conflict.NextAvailableName.Value);
        AppleCalendarExportMutation mutation = Assert.Single(nativeBridge.AppliedMutations);
        Assert.Equal(EAppleCalendarExportMutationKind.ReplaceExisting, mutation.Kind);
        Assert.Equal(existingCalendarId, mutation.ExistingCalendarIdOrNull);
    }

    [Fact]
    public async Task UnmanagedCollisionCreatesFirstAvailableNumberedCalendarAsync()
    {
        RecordingAppleCalendarNativeBridge nativeBridge =
            new RecordingAppleCalendarNativeBridge(
                createCalendar(
                    new AppleCalendarId("personal-calendar"),
                    "2026-2학기 시간표",
                    EAppleCalendarOwnership.External,
                    EAppleCalendarContentAccess.Writable),
                createCalendar(
                    new AppleCalendarId("existing-copy"),
                    "2026-2학기 시간표 (2)",
                    EAppleCalendarOwnership.ApplicationManaged,
                    EAppleCalendarContentAccess.Writable));
        RecordingCalendarNameConflictResolver conflictResolver = new RecordingCalendarNameConflictResolver(ECalendarNameConflictResolution.CreateWithAvailableName);

        AppleCalendarExportResult result =
            await new AppleCalendarExportService(nativeBridge).ExportAsync(
                createDocument(),
                conflictResolver,
                TestContext.Current.CancellationToken);

        Assert.Equal(EAppleCalendarExportStatus.Success, result.Status);
        Assert.Equal("2026-2학기 시간표 (3)", result.CalendarNameOrNull?.Value);
        CalendarNameConflict conflict = Assert.Single(conflictResolver.Conflicts);
        Assert.False(conflict.CanReplace);
        Assert.Equal("2026-2학기 시간표 (3)", conflict.NextAvailableName.Value);
        AppleCalendarExportMutation mutation = Assert.Single(nativeBridge.AppliedMutations);
        Assert.Equal(EAppleCalendarExportMutationKind.CreateNew, mutation.Kind);
        Assert.Equal("2026-2학기 시간표 (3)", mutation.DestinationName.Value);
    }

    [Fact]
    public async Task CancelledConflictDoesNotMutateCalendarAsync()
    {
        RecordingAppleCalendarNativeBridge nativeBridge =
            new RecordingAppleCalendarNativeBridge(
                createCalendar(
                    new AppleCalendarId("existing-calendar"),
                    "2026-2학기 시간표",
                    EAppleCalendarOwnership.ApplicationManaged,
                    EAppleCalendarContentAccess.Writable));
        RecordingCalendarNameConflictResolver conflictResolver = new RecordingCalendarNameConflictResolver(ECalendarNameConflictResolution.Cancel);

        AppleCalendarExportResult result =
            await new AppleCalendarExportService(nativeBridge).ExportAsync(
                createDocument(),
                conflictResolver,
                TestContext.Current.CancellationToken);

        Assert.Equal(EAppleCalendarExportStatus.Cancelled, result.Status);
        Assert.Null(result.DiagnosticCodeOrNull);
        Assert.Empty(nativeBridge.AppliedMutations);
    }

    [Fact]
    public async Task DestinationRaceReloadsNamesBeforeRetryingAsync()
    {
        RecordingAppleCalendarNativeBridge nativeBridge =
            new RecordingAppleCalendarNativeBridge(
                createCalendar(
                    new AppleCalendarId("existing-calendar"),
                    "2026-2학기 시간표",
                    EAppleCalendarOwnership.ApplicationManaged,
                    EAppleCalendarContentAccess.Writable));
        nativeBridge.FailNextMutationForDestinationChange = true;
        nativeBridge.CalendarAddedAfterDestinationChange = createCalendar(
            new AppleCalendarId("racing-copy"),
            "2026-2학기 시간표 (2)",
            EAppleCalendarOwnership.External,
            EAppleCalendarContentAccess.Writable);
        RecordingCalendarNameConflictResolver conflictResolver = new RecordingCalendarNameConflictResolver(ECalendarNameConflictResolution.CreateWithAvailableName);

        AppleCalendarExportResult result =
            await new AppleCalendarExportService(nativeBridge).ExportAsync(
                createDocument(),
                conflictResolver,
                TestContext.Current.CancellationToken);

        Assert.Equal(EAppleCalendarExportStatus.Success, result.Status);
        Assert.Equal("2026-2학기 시간표 (3)", result.CalendarNameOrNull?.Value);
        Assert.Equal(2, conflictResolver.Conflicts.Count);
        Assert.Equal(2, nativeBridge.AppliedMutations.Count);
        Assert.Equal("2026-2학기 시간표 (2)", nativeBridge.AppliedMutations[0].DestinationName.Value);
        Assert.Equal("2026-2학기 시간표 (3)", nativeBridge.AppliedMutations[1].DestinationName.Value);
    }

    [Fact]
    public async Task UnsupportedNativeBridgeReturnsUnavailableWithoutPromptAsync()
    {
        RecordingAppleCalendarNativeBridge nativeBridge = new RecordingAppleCalendarNativeBridge();
        nativeBridge.IsAvailable = false;
        RecordingCalendarNameConflictResolver conflictResolver = new RecordingCalendarNameConflictResolver(ECalendarNameConflictResolution.Cancel);

        AppleCalendarExportResult result =
            await new AppleCalendarExportService(nativeBridge).ExportAsync(
                createDocument(),
                conflictResolver,
                TestContext.Current.CancellationToken);

        Assert.Equal(EAppleCalendarExportStatus.Unavailable, result.Status);
        Assert.Equal("apple_calendar_native_bridge_unavailable", result.DiagnosticCodeOrNull);
        Assert.Equal(0, nativeBridge.CalendarSnapshotRequestCount);
        Assert.Empty(conflictResolver.Conflicts);
    }

    [Fact]
    public async Task NativeAccessDenialReturnsTypedFailureAsync()
    {
        RecordingAppleCalendarNativeBridge nativeBridge = new RecordingAppleCalendarNativeBridge();
        nativeBridge.FailureOnNextMutationOrNull =
            new AppleCalendarNativeBridgeException(
                EAppleCalendarNativeFailureKind.AccessDenied,
                "apple_calendar_access_denied");

        AppleCalendarExportResult result =
            await new AppleCalendarExportService(nativeBridge).ExportAsync(
                createDocument(),
                new RecordingCalendarNameConflictResolver(
                    ECalendarNameConflictResolution.Cancel),
                TestContext.Current.CancellationToken);

        Assert.Equal(EAppleCalendarExportStatus.AccessDenied, result.Status);
        Assert.Equal("apple_calendar_access_denied", result.DiagnosticCodeOrNull);
    }

    [Fact]
    public async Task ProcessLeaseCoversSnapshotAndMutationThenReleasesAsync()
    {
        RecordingAppleCalendarExportLeaseProvider leaseProvider =
            new RecordingAppleCalendarExportLeaseProvider();
        LeaseObservingAppleCalendarNativeBridge nativeBridge =
            new LeaseObservingAppleCalendarNativeBridge(leaseProvider);
        AppleCalendarExportService exporter =
            new AppleCalendarExportService(
                nativeBridge,
                leaseProvider);

        AppleCalendarExportResult result = await exporter.ExportAsync(
            createDocument(),
            new RecordingCalendarNameConflictResolver(
                ECalendarNameConflictResolution.Cancel),
            TestContext.Current.CancellationToken);

        Assert.Equal(EAppleCalendarExportStatus.Success, result.Status);
        Assert.True(nativeBridge.SnapshotObservedLease);
        Assert.True(nativeBridge.MutationObservedLease);
        Assert.Equal(1, leaseProvider.AcquireCount);
        Assert.Equal(0, leaseProvider.ActiveLeaseCount);
    }

    [Fact]
    public async Task NativeExceptionReleasesProcessLeaseAsync()
    {
        RecordingAppleCalendarExportLeaseProvider leaseProvider =
            new RecordingAppleCalendarExportLeaseProvider();
        RecordingAppleCalendarNativeBridge nativeBridge =
            new RecordingAppleCalendarNativeBridge();
        nativeBridge.FailureOnNextMutationOrNull =
            new AppleCalendarNativeBridgeException(
                EAppleCalendarNativeFailureKind.AccessDenied,
                "apple_calendar_access_denied");
        AppleCalendarExportService exporter =
            new AppleCalendarExportService(
                nativeBridge,
                leaseProvider);

        AppleCalendarExportResult result = await exporter.ExportAsync(
            createDocument(),
            new RecordingCalendarNameConflictResolver(
                ECalendarNameConflictResolution.Cancel),
            TestContext.Current.CancellationToken);

        Assert.Equal(
            EAppleCalendarExportStatus.AccessDenied,
            result.Status);
        Assert.Equal(1, leaseProvider.AcquireCount);
        Assert.Equal(0, leaseProvider.ActiveLeaseCount);
    }

    [Fact]
    public async Task CancellationReleasesProcessLeaseWithoutMutationAsync()
    {
        RecordingAppleCalendarExportLeaseProvider leaseProvider =
            new RecordingAppleCalendarExportLeaseProvider();
        ControlledPermissionAppleCalendarNativeBridge nativeBridge =
            new ControlledPermissionAppleCalendarNativeBridge(1);
        AppleCalendarExportService exporter =
            new AppleCalendarExportService(
                nativeBridge,
                leaseProvider);

        using (CancellationTokenSource cancellationSource =
            new CancellationTokenSource())
        {
            Task<AppleCalendarExportResult> exportTask =
                exporter.ExportAsync(
                    createDocument(),
                    new RecordingCalendarNameConflictResolver(
                        ECalendarNameConflictResolution.Cancel),
                    cancellationSource.Token);
            await nativeBridge.WaitForSnapshotRequestAsync(
                0,
                TestContext.Current.CancellationToken);

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
        ControlledPermissionAppleCalendarNativeBridge nativeBridge =
            new ControlledPermissionAppleCalendarNativeBridge(1);
        AppleCalendarExportService exporter =
            new AppleCalendarExportService(nativeBridge);

        Task<AppleCalendarExportResult> exportTask = exporter.ExportAsync(
            createDocument(),
            new RecordingCalendarNameConflictResolver(
                ECalendarNameConflictResolution.Cancel),
            TestContext.Current.CancellationToken);
        await nativeBridge.WaitForSnapshotRequestAsync(
            0,
            TestContext.Current.CancellationToken);

        await Task.Delay(
            TimeSpan.FromMilliseconds(50.0),
            TestContext.Current.CancellationToken);

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
        ControlledPermissionAppleCalendarNativeBridge nativeBridge =
            new ControlledPermissionAppleCalendarNativeBridge(2);
        AppleCalendarExportService exporter =
            new AppleCalendarExportService(nativeBridge);

        Task<AppleCalendarExportResult> deniedExportTask =
            exporter.ExportAsync(
                createDocument(),
                new RecordingCalendarNameConflictResolver(
                    ECalendarNameConflictResolution.Cancel),
                TestContext.Current.CancellationToken);
        await nativeBridge.WaitForSnapshotRequestAsync(
            0,
            TestContext.Current.CancellationToken);
        nativeBridge.DenySnapshot(0);

        AppleCalendarExportResult deniedResult =
            await deniedExportTask;

        Assert.Equal(
            EAppleCalendarExportStatus.AccessDenied,
            deniedResult.Status);
        Assert.Equal(0, nativeBridge.ApplyExportRequestCount);

        Task<AppleCalendarExportResult> retryTask = exporter.ExportAsync(
            createDocument(),
            new RecordingCalendarNameConflictResolver(
                ECalendarNameConflictResolution.Cancel),
            TestContext.Current.CancellationToken);
        await nativeBridge.WaitForSnapshotRequestAsync(
            1,
            TestContext.Current.CancellationToken);
        nativeBridge.AllowSnapshot(1);

        AppleCalendarExportResult retryResult = await retryTask;

        Assert.Equal(
            EAppleCalendarExportStatus.Success,
            retryResult.Status);
        Assert.Equal(2, nativeBridge.CalendarSnapshotRequestCount);
        Assert.Equal(1, nativeBridge.ApplyExportRequestCount);
    }

    [Fact]
    public async Task PendingPermissionHonorsCancellationWithoutMutationAsync()
    {
        ControlledPermissionAppleCalendarNativeBridge nativeBridge =
            new ControlledPermissionAppleCalendarNativeBridge(1);
        AppleCalendarExportService exporter =
            new AppleCalendarExportService(nativeBridge);
        using (CancellationTokenSource cancellationSource =
            new CancellationTokenSource())
        {
            Task<AppleCalendarExportResult> exportTask =
                exporter.ExportAsync(
                    createDocument(),
                    new RecordingCalendarNameConflictResolver(
                        ECalendarNameConflictResolution.Cancel),
                    cancellationSource.Token);
            await nativeBridge.WaitForSnapshotRequestAsync(
                0,
                TestContext.Current.CancellationToken);

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

    [Fact]
    public async Task ResolverCannotReplaceUnmanagedCalendarAsync()
    {
        RecordingAppleCalendarNativeBridge nativeBridge =
            new RecordingAppleCalendarNativeBridge(
                createCalendar(
                    new AppleCalendarId("personal-calendar"),
                    "2026-2학기 시간표",
                    EAppleCalendarOwnership.External,
                    EAppleCalendarContentAccess.Writable));

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => new AppleCalendarExportService(nativeBridge).ExportAsync(
                createDocument(),
                new RecordingCalendarNameConflictResolver(
                    ECalendarNameConflictResolution.ReplaceExisting),
                TestContext.Current.CancellationToken));

        Assert.Empty(nativeBridge.AppliedMutations);
    }

    [Fact]
    public void CalendarOwnershipMarkerRequiresAnExactNonEmptyPlanId()
    {
        PlanId planId = new PlanId(
            Guid.Parse("71f3be04-d4c6-41d4-a269-792321e71423"));
        string marker = AppleCalendarOwnershipMarker.CreateForPlan(planId);

        Assert.True(
            AppleCalendarOwnershipMarker.IsApplicationManaged(marker));
        Assert.False(
            AppleCalendarOwnershipMarker.IsApplicationManaged(
                AppleCalendarOwnershipMarker.PREFIX));
        Assert.False(
            AppleCalendarOwnershipMarker.IsApplicationManaged(
                AppleCalendarOwnershipMarker.PREFIX + "not-a-plan-id"));
        Assert.False(
            AppleCalendarOwnershipMarker.IsApplicationManaged(
                marker + "/unexpected"));
        Assert.False(
            AppleCalendarOwnershipMarker.IsApplicationManaged(
                AppleCalendarOwnershipMarker.PREFIX
                    + "00000000-0000-0000-0000-000000000000"));
    }

    private static AppleCalendarDescriptor createCalendar(
        AppleCalendarId calendarId,
        string name,
        EAppleCalendarOwnership ownership,
        EAppleCalendarContentAccess contentAccess)
    {
        return new AppleCalendarDescriptor(
            calendarId,
            name,
            ownership,
            contentAccess);
    }

    private static CalendarExportDocument createDocument()
    {
        AcademicTermCalendarMetadata academicCalendar =
            AcademicTermCalendarMetadataRegistry.findByTerm(
                AcademicTerm.Parse("2026-2"),
                new CalendarTimeZoneId("Asia/Seoul"));
        RecurringCalendarEvent calendarEvent = new RecurringCalendarEvent(
            new CalendarEventUid("course:ITP30003:01"),
            new CalendarEventContent(
                "컴퓨터 구조(01)",
                "OH 401",
                "담당: 이원형"),
            new DailyTimeRange(
                new ScheduleTime(11, 30),
                new ScheduleTime(12, 15)),
            new EDay[] { EDay.Monday, EDay.Thursday });
        return new CalendarExportDocument(
            new PlanId(
                Guid.Parse("71f3be04-d4c6-41d4-a269-792321e71423")),
            new PlanName("2026-2학기 시간표"),
            academicCalendar,
            new RecurringCalendarEvent[] { calendarEvent });
    }

    private sealed class RecordingCalendarNameConflictResolver
        : ICalendarNameConflictResolver
    {
        private readonly ECalendarNameConflictResolution mResolution;
        private readonly List<CalendarNameConflict> mConflicts = new List<CalendarNameConflict>();

        public IReadOnlyList<CalendarNameConflict> Conflicts
        {
            get
            {
                return mConflicts;
            }
        }

        public RecordingCalendarNameConflictResolver(ECalendarNameConflictResolution resolution)
        {
            mResolution = resolution;
        }

        public Task<ECalendarNameConflictResolution> ResolveAsync(
            CalendarNameConflict conflict,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            mConflicts.Add(conflict);
            return Task.FromResult(mResolution);
        }
    }

    private sealed class RecordingAppleCalendarExportLeaseProvider
        : IAppleCalendarExportLeaseProvider
    {
        private int mAcquireCount;
        private int mActiveLeaseCount;

        public int AcquireCount
        {
            get
            {
                return Volatile.Read(ref mAcquireCount);
            }
        }

        public int ActiveLeaseCount
        {
            get
            {
                return Volatile.Read(ref mActiveLeaseCount);
            }
        }

        public Task<IAppleCalendarExportLease> AcquireAsync(
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Interlocked.Increment(ref mAcquireCount);
            Interlocked.Increment(ref mActiveLeaseCount);
            return Task.FromResult<IAppleCalendarExportLease>(
                new RecordingLease(this));
        }

        private sealed class RecordingLease
            : IAppleCalendarExportLease
        {
            private readonly RecordingAppleCalendarExportLeaseProvider
                mProvider;

            private int mWasDisposed;

            public RecordingLease(
                RecordingAppleCalendarExportLeaseProvider provider)
            {
                mProvider = provider;
            }

            public ValueTask DisposeAsync()
            {
                if (Interlocked.Exchange(ref mWasDisposed, 1) == 0)
                {
                    Interlocked.Decrement(
                        ref mProvider.mActiveLeaseCount);
                }

                return ValueTask.CompletedTask;
            }
        }
    }

    private sealed class LeaseObservingAppleCalendarNativeBridge
        : IAppleCalendarNativeBridge
    {
        private readonly RecordingAppleCalendarExportLeaseProvider
            mLeaseProvider;

        public bool IsAvailable
        {
            get
            {
                return true;
            }
        }

        public bool SnapshotObservedLease { get; private set; }

        public bool MutationObservedLease { get; private set; }

        public LeaseObservingAppleCalendarNativeBridge(
            RecordingAppleCalendarExportLeaseProvider leaseProvider)
        {
            mLeaseProvider = leaseProvider;
        }

        public Task<IReadOnlyList<AppleCalendarDescriptor>>
            GetCalendarsAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            SnapshotObservedLease =
                mLeaseProvider.ActiveLeaseCount == 1;
            return Task.FromResult<IReadOnlyList<
                AppleCalendarDescriptor>>(
                    Array.Empty<AppleCalendarDescriptor>());
        }

        public Task<AppleCalendarNativeExportResult> ApplyExportAsync(
            AppleCalendarExportMutation mutation,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            MutationObservedLease =
                mLeaseProvider.ActiveLeaseCount == 1;
            return Task.FromResult(
                new AppleCalendarNativeExportResult(
                    new AppleCalendarId("lease-observed-calendar"),
                    mutation.DestinationName,
                    mutation.Document.Events.Count,
                    0));
        }
    }

    private sealed class RecordingAppleCalendarNativeBridge
        : IAppleCalendarNativeBridge
    {
        private readonly List<AppleCalendarDescriptor> mCalendars;
        private readonly List<AppleCalendarExportMutation> mAppliedMutations = new List<AppleCalendarExportMutation>();

        public bool IsAvailable { get; set; } = true;

        public bool FailNextMutationForDestinationChange { get; set; }

        public AppleCalendarDescriptor? CalendarAddedAfterDestinationChange
        {
            get;
            set;
        }

        public AppleCalendarNativeBridgeException? FailureOnNextMutationOrNull
        {
            get;
            set;
        }

        public int CalendarSnapshotRequestCount { get; private set; }

        public IReadOnlyList<AppleCalendarExportMutation> AppliedMutations
        {
            get
            {
                return mAppliedMutations;
            }
        }

        public RecordingAppleCalendarNativeBridge(params AppleCalendarDescriptor[] calendars)
        {
            mCalendars = new List<AppleCalendarDescriptor>(calendars);
        }

        public Task<IReadOnlyList<AppleCalendarDescriptor>> GetCalendarsAsync(
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            CalendarSnapshotRequestCount++;
            IReadOnlyList<AppleCalendarDescriptor> snapshot = new List<AppleCalendarDescriptor>(mCalendars).AsReadOnly();
            return Task.FromResult(snapshot);
        }

        public Task<AppleCalendarNativeExportResult> ApplyExportAsync(
            AppleCalendarExportMutation mutation,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            mAppliedMutations.Add(mutation);

            if (FailureOnNextMutationOrNull != null)
            {
                AppleCalendarNativeBridgeException exception = FailureOnNextMutationOrNull;
                FailureOnNextMutationOrNull = null;
                return Task.FromException<AppleCalendarNativeExportResult>(exception);
            }

            if (FailNextMutationForDestinationChange)
            {
                FailNextMutationForDestinationChange = false;
                if (CalendarAddedAfterDestinationChange != null)
                {
                    mCalendars.Add(CalendarAddedAfterDestinationChange);
                }

                return Task.FromException<AppleCalendarNativeExportResult>(
                    new AppleCalendarNativeBridgeException(
                        EAppleCalendarNativeFailureKind.CalendarChanged,
                        "apple_calendar_destination_changed"));
            }

            AppleCalendarId? existingCalendarIdOrNull = mutation.ExistingCalendarIdOrNull;
            AppleCalendarId calendarId;
            if (existingCalendarIdOrNull == null)
            {
                calendarId = new AppleCalendarId("created-calendar-" + mAppliedMutations.Count);
            }
            else
            {
                calendarId = existingCalendarIdOrNull;
            }

            int deletedEventCount = mutation.Kind == EAppleCalendarExportMutationKind.ReplaceExisting ? 1 : 0;
            return Task.FromResult(
                new AppleCalendarNativeExportResult(
                    calendarId,
                    mutation.DestinationName,
                    mutation.Document.Events.Count,
                    deletedEventCount));
        }
    }

    private sealed class ControlledPermissionAppleCalendarNativeBridge
        : IAppleCalendarNativeBridge
    {
        private readonly TaskCompletionSource<
            IReadOnlyList<AppleCalendarDescriptor>>[] mSnapshotSources;

        private readonly TaskCompletionSource[] mSnapshotRequestSources;

        public bool IsAvailable
        {
            get
            {
                return true;
            }
        }

        public int CalendarSnapshotRequestCount { get; private set; }

        public int ApplyExportRequestCount { get; private set; }

        public ControlledPermissionAppleCalendarNativeBridge(
            int snapshotCount)
        {
            if (snapshotCount <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(snapshotCount));
            }

            mSnapshotSources = new TaskCompletionSource<
                IReadOnlyList<AppleCalendarDescriptor>>[snapshotCount];
            mSnapshotRequestSources =
                new TaskCompletionSource[snapshotCount];
            for (int index = 0; index < snapshotCount; ++index)
            {
                mSnapshotSources[index] = new TaskCompletionSource<
                    IReadOnlyList<AppleCalendarDescriptor>>(
                    TaskCreationOptions.RunContinuationsAsynchronously);
                mSnapshotRequestSources[index] =
                    new TaskCompletionSource(
                        TaskCreationOptions.RunContinuationsAsynchronously);
            }
        }

        public async Task<IReadOnlyList<AppleCalendarDescriptor>>
            GetCalendarsAsync(CancellationToken cancellationToken)
        {
            int requestIndex = CalendarSnapshotRequestCount;
            if (requestIndex >= mSnapshotSources.Length)
            {
                throw new InvalidOperationException(
                    "No controlled Apple Calendar snapshot remains.");
            }

            CalendarSnapshotRequestCount++;
            mSnapshotRequestSources[requestIndex].TrySetResult();
            return await mSnapshotSources[requestIndex].Task
                .WaitAsync(cancellationToken);
        }

        public Task<AppleCalendarNativeExportResult> ApplyExportAsync(
            AppleCalendarExportMutation mutation,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ApplyExportRequestCount++;
            return Task.FromResult(
                new AppleCalendarNativeExportResult(
                    new AppleCalendarId("controlled-calendar"),
                    mutation.DestinationName,
                    mutation.Document.Events.Count,
                    0));
        }

        public Task WaitForSnapshotRequestAsync(
            int requestIndex,
            CancellationToken cancellationToken)
        {
            return mSnapshotRequestSources[requestIndex].Task
                .WaitAsync(cancellationToken);
        }

        public void AllowSnapshot(int requestIndex)
        {
            mSnapshotSources[requestIndex].TrySetResult(
                Array.Empty<AppleCalendarDescriptor>());
        }

        public void DenySnapshot(int requestIndex)
        {
            mSnapshotSources[requestIndex].TrySetException(
                new AppleCalendarNativeBridgeException(
                    EAppleCalendarNativeFailureKind.AccessDenied,
                    "apple_calendar_automation_access_denied"));
        }
    }
}
