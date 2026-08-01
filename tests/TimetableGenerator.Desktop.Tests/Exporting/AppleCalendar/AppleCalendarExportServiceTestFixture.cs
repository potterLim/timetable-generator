using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using TimetableGenerator.Desktop.Exporting.AppleCalendar;
using TimetableGenerator.Desktop.Exporting.Calendar;
using TimetableGenerator.Domain.Catalogs;
using TimetableGenerator.Domain.Planning;
using TimetableGenerator.Domain.Scheduling;

namespace TimetableGenerator.Desktop.Tests.Exporting.AppleCalendar;

public abstract class AppleCalendarExportServiceTestFixture
{
    private protected static AppleCalendarDescriptor createCalendar(AppleCalendarId calendarId, string name, EAppleCalendarOwnership ownership, EAppleCalendarContentAccess contentAccess)
    {
        return new AppleCalendarDescriptor(calendarId, name, "source-1", ownership == EAppleCalendarOwnership.ApplicationManaged ? createDocument().PlanId : null, contentAccess);
    }

    private protected static CalendarExportDocument createDocument()
    {
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
        return createDocument(new RecurringCalendarEvent[] { calendarEvent });
    }

    private protected static CalendarExportDocument createDocument(IReadOnlyList<RecurringCalendarEvent> events)
    {
        AcademicTermCalendarMetadata academicCalendar = AcademicTermCalendarMetadataRegistry.findByTerm(AcademicTerm.Parse("2026-2"), new CalendarTimeZoneId("Asia/Seoul"));
        return new CalendarExportDocument(
            new PlanId(
                Guid.Parse("71f3be04-d4c6-41d4-a269-792321e71423")),
            new PlanName("2026-2학기 시간표"),
            new InstitutionName("한동대학교"),
            academicCalendar,
            events);
    }

    private protected sealed class RecordingCalendarNameConflictResolver
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

        public Task<ECalendarNameConflictResolution> ResolveAsync(CalendarNameConflict conflict, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            mConflicts.Add(conflict);
            return Task.FromResult(mResolution);
        }
    }

    private protected sealed class RecordingAppleCalendarExportProgress : IProgress<AppleCalendarExportProgress>
    {
        private readonly List<EAppleCalendarExportProgressStage> mStages = new List<EAppleCalendarExportProgressStage>();

        public IReadOnlyList<EAppleCalendarExportProgressStage> Stages
        {
            get
            {
                return mStages;
            }
        }

        public void Report(AppleCalendarExportProgress value)
        {
            mStages.Add(value.Stage);
        }
    }

    private protected sealed class RecordingAppleCalendarExportLeaseProvider
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

        public Task<IAppleCalendarExportLease> AcquireAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Interlocked.Increment(ref mAcquireCount);
            Interlocked.Increment(ref mActiveLeaseCount);
            return Task.FromResult<IAppleCalendarExportLease>(new RecordingLease(this));
        }

        private sealed class RecordingLease
            : IAppleCalendarExportLease
        {
            private readonly RecordingAppleCalendarExportLeaseProvider
                mProvider;

            private int mWasDisposed;

            public RecordingLease(RecordingAppleCalendarExportLeaseProvider provider)
            {
                mProvider = provider;
            }

            public ValueTask DisposeAsync()
            {
                if (Interlocked.Exchange(ref mWasDisposed, 1) == 0)
                {
                    Interlocked.Decrement(ref mProvider.mActiveLeaseCount);
                }

                return ValueTask.CompletedTask;
            }
        }
    }

    private protected sealed class LeaseObservingAppleCalendarNativeBridge
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

        public LeaseObservingAppleCalendarNativeBridge(RecordingAppleCalendarExportLeaseProvider leaseProvider)
        {
            mLeaseProvider = leaseProvider;
        }

        public Task ReconcilePendingOperationAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<AppleCalendarDescriptor>> GetCalendarsAsync(CalendarExportDocument document, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(document);
            cancellationToken.ThrowIfCancellationRequested();
            SnapshotObservedLease = mLeaseProvider.ActiveLeaseCount == 1;
            return Task.FromResult<IReadOnlyList<AppleCalendarDescriptor>>(Array.Empty<AppleCalendarDescriptor>());
        }

        public Task<AppleCalendarNativeExportResult> ApplyExportAsync(AppleCalendarExportMutation mutation, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            MutationObservedLease = mLeaseProvider.ActiveLeaseCount == 1;
            return Task.FromResult(new AppleCalendarNativeExportResult(new AppleCalendarId("lease-observed-calendar"), mutation.DestinationName, mutation.Document.Events.Count, 0));
        }
    }

    private protected sealed class RecordingAppleCalendarNativeBridge
        : IAppleCalendarNativeBridge
    {
        private readonly List<AppleCalendarDescriptor> mCalendars;
        private readonly List<AppleCalendarExportMutation> mAppliedMutations = new List<AppleCalendarExportMutation>();
        private readonly List<PlanName> mRequestedDestinationNames = new List<PlanName>();

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

        public int PendingReconciliationRequestCount { get; private set; }

        public IReadOnlyList<AppleCalendarExportMutation> AppliedMutations
        {
            get
            {
                return mAppliedMutations;
            }
        }

        public IReadOnlyList<PlanName> RequestedDestinationNames
        {
            get
            {
                return mRequestedDestinationNames;
            }
        }

        public RecordingAppleCalendarNativeBridge(params AppleCalendarDescriptor[] calendars)
        {
            mCalendars = new List<AppleCalendarDescriptor>(calendars);
        }

        public Task ReconcilePendingOperationAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            PendingReconciliationRequestCount++;
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<AppleCalendarDescriptor>> GetCalendarsAsync(CalendarExportDocument document, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(document);
            cancellationToken.ThrowIfCancellationRequested();
            mRequestedDestinationNames.Add(document.CalendarName);
            CalendarSnapshotRequestCount++;
            IReadOnlyList<AppleCalendarDescriptor> snapshot = new List<AppleCalendarDescriptor>(mCalendars).AsReadOnly();
            return Task.FromResult(snapshot);
        }

        public Task<AppleCalendarNativeExportResult> ApplyExportAsync(AppleCalendarExportMutation mutation, CancellationToken cancellationToken)
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

                return Task.FromException<AppleCalendarNativeExportResult>(new AppleCalendarNativeBridgeException(EAppleCalendarNativeFailureKind.CalendarChanged, "eventkit_calendar_destination_changed"));
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
            return Task.FromResult(new AppleCalendarNativeExportResult(calendarId, mutation.DestinationName, mutation.Document.Events.Count, deletedEventCount));
        }
    }

    private protected sealed class ControlledPermissionAppleCalendarNativeBridge
        : IAppleCalendarNativeBridge
    {
        private readonly TaskCompletionSource<IReadOnlyList<AppleCalendarDescriptor>>[] mSnapshotSources;

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

        public ControlledPermissionAppleCalendarNativeBridge(int snapshotCount)
        {
            if (snapshotCount <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(snapshotCount));
            }

            mSnapshotSources = new TaskCompletionSource<IReadOnlyList<AppleCalendarDescriptor>>[snapshotCount];
            mSnapshotRequestSources = new TaskCompletionSource[snapshotCount];
            for (int index = 0; index < snapshotCount; ++index)
            {
                mSnapshotSources[index] = new TaskCompletionSource<IReadOnlyList<AppleCalendarDescriptor>>(TaskCreationOptions.RunContinuationsAsynchronously);
                mSnapshotRequestSources[index] = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            }
        }

        public Task ReconcilePendingOperationAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.CompletedTask;
        }

        public async Task<IReadOnlyList<AppleCalendarDescriptor>> GetCalendarsAsync(CalendarExportDocument document, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(document);
            int requestIndex = CalendarSnapshotRequestCount;
            if (requestIndex >= mSnapshotSources.Length)
            {
                throw new InvalidOperationException("No controlled Apple Calendar snapshot remains.");
            }

            CalendarSnapshotRequestCount++;
            mSnapshotRequestSources[requestIndex].TrySetResult();
            return await mSnapshotSources[requestIndex].Task.WaitAsync(cancellationToken);
        }

        public Task<AppleCalendarNativeExportResult> ApplyExportAsync(AppleCalendarExportMutation mutation, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ApplyExportRequestCount++;
            return Task.FromResult(new AppleCalendarNativeExportResult(new AppleCalendarId("controlled-calendar"), mutation.DestinationName, mutation.Document.Events.Count, 0));
        }

        public Task WaitForSnapshotRequestAsync(int requestIndex, CancellationToken cancellationToken)
        {
            return mSnapshotRequestSources[requestIndex].Task.WaitAsync(cancellationToken);
        }

        public void AllowSnapshot(int requestIndex)
        {
            mSnapshotSources[requestIndex].TrySetResult(Array.Empty<AppleCalendarDescriptor>());
        }

        public void DenySnapshot(int requestIndex)
        {
            mSnapshotSources[requestIndex].TrySetException(new AppleCalendarNativeBridgeException(EAppleCalendarNativeFailureKind.AccessDenied, "apple_calendar_automation_access_denied"));
        }
    }
}
