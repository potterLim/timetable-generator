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
        RecordingAppleCalendarNativeBridge nativeBridge =
            new RecordingAppleCalendarNativeBridge();
        RecordingCalendarNameConflictResolver conflictResolver =
            new RecordingCalendarNameConflictResolver(
                ECalendarNameConflictResolution.Cancel);
        AppleCalendarExportService exporter =
            new AppleCalendarExportService(nativeBridge);

        AppleCalendarExportResult result = await exporter.ExportAsync(
            createDocument(),
            conflictResolver,
            TestContext.Current.CancellationToken);

        Assert.Equal(EAppleCalendarExportStatus.Success, result.Status);
        Assert.Equal("2026-2학기 시간표", result.CalendarNameOrNull?.Value);
        Assert.Equal(1, result.CreatedEventCount);
        Assert.Equal(0, result.DeletedEventCount);
        Assert.Empty(conflictResolver.Conflicts);
        AppleCalendarExportMutation mutation =
            Assert.Single(nativeBridge.AppliedMutations);
        Assert.Equal(EAppleCalendarExportMutationKind.CreateNew, mutation.Kind);
        Assert.Equal("2026-2학기 시간표", mutation.DestinationName.Value);
        Assert.Null(mutation.ExistingCalendarIdOrNull);
    }

    [Fact]
    public async Task ManagedWritableNameCollisionCanReplaceExistingCalendarAsync()
    {
        AppleCalendarId existingCalendarId =
            new AppleCalendarId("existing-calendar");
        RecordingAppleCalendarNativeBridge nativeBridge =
            new RecordingAppleCalendarNativeBridge(
                createCalendar(
                    existingCalendarId,
                    "2026-2학기 시간표",
                    true,
                    true));
        RecordingCalendarNameConflictResolver conflictResolver =
            new RecordingCalendarNameConflictResolver(
                ECalendarNameConflictResolution.ReplaceExisting);

        AppleCalendarExportResult result =
            await new AppleCalendarExportService(nativeBridge).ExportAsync(
                createDocument(),
                conflictResolver,
                TestContext.Current.CancellationToken);

        Assert.Equal(EAppleCalendarExportStatus.Success, result.Status);
        Assert.Equal(existingCalendarId, result.CalendarIdOrNull);
        Assert.Equal(1, result.DeletedEventCount);
        CalendarNameConflict conflict =
            Assert.Single(conflictResolver.Conflicts);
        Assert.True(conflict.CanReplace);
        Assert.Equal("2026-2학기 시간표 (2)", conflict.NextAvailableName.Value);
        AppleCalendarExportMutation mutation =
            Assert.Single(nativeBridge.AppliedMutations);
        Assert.Equal(
            EAppleCalendarExportMutationKind.ReplaceExisting,
            mutation.Kind);
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
                    false,
                    true),
                createCalendar(
                    new AppleCalendarId("existing-copy"),
                    "2026-2학기 시간표 (2)",
                    true,
                    true));
        RecordingCalendarNameConflictResolver conflictResolver =
            new RecordingCalendarNameConflictResolver(
                ECalendarNameConflictResolution.CreateWithAvailableName);

        AppleCalendarExportResult result =
            await new AppleCalendarExportService(nativeBridge).ExportAsync(
                createDocument(),
                conflictResolver,
                TestContext.Current.CancellationToken);

        Assert.Equal(EAppleCalendarExportStatus.Success, result.Status);
        Assert.Equal("2026-2학기 시간표 (3)", result.CalendarNameOrNull?.Value);
        CalendarNameConflict conflict =
            Assert.Single(conflictResolver.Conflicts);
        Assert.False(conflict.CanReplace);
        Assert.Equal("2026-2학기 시간표 (3)", conflict.NextAvailableName.Value);
        AppleCalendarExportMutation mutation =
            Assert.Single(nativeBridge.AppliedMutations);
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
                    true,
                    true));
        RecordingCalendarNameConflictResolver conflictResolver =
            new RecordingCalendarNameConflictResolver(
                ECalendarNameConflictResolution.Cancel);

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
                    true,
                    true));
        nativeBridge.FailNextMutationForDestinationChange = true;
        nativeBridge.CalendarAddedAfterDestinationChange = createCalendar(
            new AppleCalendarId("racing-copy"),
            "2026-2학기 시간표 (2)",
            false,
            true);
        RecordingCalendarNameConflictResolver conflictResolver =
            new RecordingCalendarNameConflictResolver(
                ECalendarNameConflictResolution.CreateWithAvailableName);

        AppleCalendarExportResult result =
            await new AppleCalendarExportService(nativeBridge).ExportAsync(
                createDocument(),
                conflictResolver,
                TestContext.Current.CancellationToken);

        Assert.Equal(EAppleCalendarExportStatus.Success, result.Status);
        Assert.Equal("2026-2학기 시간표 (3)", result.CalendarNameOrNull?.Value);
        Assert.Equal(2, conflictResolver.Conflicts.Count);
        Assert.Equal(2, nativeBridge.AppliedMutations.Count);
        Assert.Equal(
            "2026-2학기 시간표 (2)",
            nativeBridge.AppliedMutations[0].DestinationName.Value);
        Assert.Equal(
            "2026-2학기 시간표 (3)",
            nativeBridge.AppliedMutations[1].DestinationName.Value);
    }

    [Fact]
    public async Task UnsupportedNativeBridgeReturnsUnavailableWithoutPromptAsync()
    {
        RecordingAppleCalendarNativeBridge nativeBridge =
            new RecordingAppleCalendarNativeBridge();
        nativeBridge.IsAvailable = false;
        RecordingCalendarNameConflictResolver conflictResolver =
            new RecordingCalendarNameConflictResolver(
                ECalendarNameConflictResolution.Cancel);

        AppleCalendarExportResult result =
            await new AppleCalendarExportService(nativeBridge).ExportAsync(
                createDocument(),
                conflictResolver,
                TestContext.Current.CancellationToken);

        Assert.Equal(EAppleCalendarExportStatus.Unavailable, result.Status);
        Assert.Equal(
            "apple_calendar_native_bridge_unavailable",
            result.DiagnosticCodeOrNull);
        Assert.Equal(0, nativeBridge.CalendarSnapshotRequestCount);
        Assert.Empty(conflictResolver.Conflicts);
    }

    [Fact]
    public async Task NativeAccessDenialReturnsTypedFailureAsync()
    {
        RecordingAppleCalendarNativeBridge nativeBridge =
            new RecordingAppleCalendarNativeBridge();
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
    public async Task ResolverCannotReplaceUnmanagedCalendarAsync()
    {
        RecordingAppleCalendarNativeBridge nativeBridge =
            new RecordingAppleCalendarNativeBridge(
                createCalendar(
                    new AppleCalendarId("personal-calendar"),
                    "2026-2학기 시간표",
                    false,
                    true));

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => new AppleCalendarExportService(nativeBridge).ExportAsync(
                createDocument(),
                new RecordingCalendarNameConflictResolver(
                    ECalendarNameConflictResolution.ReplaceExisting),
                TestContext.Current.CancellationToken));

        Assert.Empty(nativeBridge.AppliedMutations);
    }

    private static AppleCalendarDescriptor createCalendar(
        AppleCalendarId calendarId,
        string name,
        bool isManagedByApplication,
        bool allowsContentModification)
    {
        return new AppleCalendarDescriptor(
            calendarId,
            name,
            isManagedByApplication,
            allowsContentModification);
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
        private readonly List<CalendarNameConflict> mConflicts =
            new List<CalendarNameConflict>();

        public IReadOnlyList<CalendarNameConflict> Conflicts
        {
            get
            {
                return mConflicts;
            }
        }

        public RecordingCalendarNameConflictResolver(
            ECalendarNameConflictResolution resolution)
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

    private sealed class RecordingAppleCalendarNativeBridge
        : IAppleCalendarNativeBridge
    {
        private readonly List<AppleCalendarDescriptor> mCalendars;
        private readonly List<AppleCalendarExportMutation> mAppliedMutations =
            new List<AppleCalendarExportMutation>();

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

        public RecordingAppleCalendarNativeBridge(
            params AppleCalendarDescriptor[] calendars)
        {
            mCalendars = new List<AppleCalendarDescriptor>(calendars);
        }

        public Task<IReadOnlyList<AppleCalendarDescriptor>> GetCalendarsAsync(
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            CalendarSnapshotRequestCount++;
            IReadOnlyList<AppleCalendarDescriptor> snapshot =
                new List<AppleCalendarDescriptor>(mCalendars).AsReadOnly();
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
                AppleCalendarNativeBridgeException exception =
                    FailureOnNextMutationOrNull;
                FailureOnNextMutationOrNull = null;
                return Task.FromException<AppleCalendarNativeExportResult>(
                    exception);
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

            AppleCalendarId calendarId =
                mutation.ExistingCalendarIdOrNull
                ?? new AppleCalendarId(
                    "created-calendar-" + mAppliedMutations.Count);
            int deletedEventCount =
                mutation.Kind == EAppleCalendarExportMutationKind.ReplaceExisting
                    ? 1
                    : 0;
            return Task.FromResult(
                new AppleCalendarNativeExportResult(
                    calendarId,
                    mutation.DestinationName,
                    mutation.Document.Events.Count,
                    deletedEventCount));
        }
    }
}
