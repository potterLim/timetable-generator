using System;
using System.Threading.Tasks;

using TimetableGenerator.Desktop.Exporting.AppleCalendar;
using TimetableGenerator.Desktop.Exporting.Calendar;
using TimetableGenerator.Domain.Planning;

using Xunit;

namespace TimetableGenerator.Desktop.Tests.Exporting.AppleCalendar;

public sealed class AppleCalendarExportServiceConflictFlowTests
    : AppleCalendarExportServiceTestFixture
{
    [Fact]
    public async Task AvailableNameCreatesCalendarWithoutPromptAsync()
    {
        RecordingAppleCalendarNativeBridge nativeBridge = new RecordingAppleCalendarNativeBridge();
        RecordingCalendarNameConflictResolver conflictResolver = new RecordingCalendarNameConflictResolver(ECalendarNameConflictResolution.Cancel);
        AppleCalendarExportService exporter = new AppleCalendarExportService(nativeBridge);

        AppleCalendarExportResult result = await exporter.ExportAsync(createDocument(), conflictResolver, TestContext.Current.CancellationToken);

        Assert.Equal(EAppleCalendarExportStatus.Success, result.Status);
        Assert.Equal("2026-2학기 시간표", result.CalendarNameOrNull?.Value);
        Assert.Equal(1, result.CreatedEventCount);
        Assert.Equal(0, result.DeletedEventCount);
        Assert.Empty(conflictResolver.Conflicts);
        AppleCalendarExportMutation mutation = Assert.Single(nativeBridge.AppliedMutations);
        Assert.Equal(EAppleCalendarExportMutationKind.CreateNew, mutation.Kind);
        Assert.Equal("2026-2학기 시간표", mutation.DestinationName.Value);
        Assert.Null(mutation.ExistingCalendarIdOrNull);
        Assert.Equal("2026-2학기 시간표", Assert.Single(nativeBridge.RequestedDestinationNames).Value);
    }

    [Fact]
    public async Task SuccessfulExportReportsNativeWorkPhasesInOrderAsync()
    {
        RecordingAppleCalendarNativeBridge nativeBridge = new RecordingAppleCalendarNativeBridge();
        RecordingAppleCalendarExportProgress progress = new RecordingAppleCalendarExportProgress();

        AppleCalendarExportResult result = await new AppleCalendarExportService(nativeBridge).ExportAsync(
            createDocument(),
            new RecordingCalendarNameConflictResolver(ECalendarNameConflictResolution.Cancel),
            TestContext.Current.CancellationToken,
            progress);

        Assert.Equal(EAppleCalendarExportStatus.Success, result.Status);
        Assert.Equal(
            new EAppleCalendarExportProgressStage[]
            {
                EAppleCalendarExportProgressStage.CheckingCalendar,
                EAppleCalendarExportProgressStage.SavingEvents,
                EAppleCalendarExportProgressStage.Finalizing,
            },
            progress.Stages);
    }

    [Fact]
    public async Task ReconcilesPendingOperationBeforeExportingCurrentDocumentAsync()
    {
        RecordingAppleCalendarNativeBridge nativeBridge = new RecordingAppleCalendarNativeBridge();
        RecordingAppleCalendarExportProgress progress = new RecordingAppleCalendarExportProgress();

        AppleCalendarExportResult result = await new AppleCalendarExportService(nativeBridge).ExportAsync(
            createDocument(),
            new RecordingCalendarNameConflictResolver(ECalendarNameConflictResolution.Cancel),
            TestContext.Current.CancellationToken,
            progress);

        Assert.Equal(EAppleCalendarExportStatus.Success, result.Status);
        Assert.Equal("created-calendar-1", result.CalendarIdOrNull?.Value);
        Assert.Equal(1, nativeBridge.PendingReconciliationRequestCount);
        Assert.Equal(1, nativeBridge.CalendarSnapshotRequestCount);
        Assert.Single(nativeBridge.AppliedMutations);
        Assert.Equal(
            new EAppleCalendarExportProgressStage[]
            {
                EAppleCalendarExportProgressStage.CheckingCalendar,
                EAppleCalendarExportProgressStage.SavingEvents,
                EAppleCalendarExportProgressStage.Finalizing,
            },
            progress.Stages);
    }

    [Fact]
    public async Task ManagedWritableNameCollisionCanReplaceExistingCalendarAsync()
    {
        AppleCalendarId existingCalendarId = new AppleCalendarId("existing-calendar");
        RecordingAppleCalendarNativeBridge nativeBridge = new RecordingAppleCalendarNativeBridge(createCalendar(existingCalendarId, "2026-2학기 시간표", EAppleCalendarOwnership.ApplicationManaged, EAppleCalendarContentAccess.Writable));
        RecordingCalendarNameConflictResolver conflictResolver = new RecordingCalendarNameConflictResolver(ECalendarNameConflictResolution.ReplaceExisting);

        AppleCalendarExportResult result = await new AppleCalendarExportService(nativeBridge).ExportAsync(createDocument(), conflictResolver, TestContext.Current.CancellationToken);

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
    public async Task MultipleMatchingManagedCalendarsCannotBeReplacedAsync()
    {
        RecordingAppleCalendarNativeBridge nativeBridge = new RecordingAppleCalendarNativeBridge(createCalendar(new AppleCalendarId("managed-calendar-one"), "2026-2학기 시간표", EAppleCalendarOwnership.ApplicationManaged, EAppleCalendarContentAccess.Writable), createCalendar(new AppleCalendarId("managed-calendar-two"), "2026-2학기 시간표", EAppleCalendarOwnership.ApplicationManaged, EAppleCalendarContentAccess.Writable));
        RecordingCalendarNameConflictResolver conflictResolver = new RecordingCalendarNameConflictResolver(ECalendarNameConflictResolution.ReplaceExisting);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => new AppleCalendarExportService(nativeBridge).ExportAsync(createDocument(), conflictResolver, TestContext.Current.CancellationToken));

        Assert.False(Assert.Single(conflictResolver.Conflicts).CanReplace);
        Assert.Empty(nativeBridge.AppliedMutations);
    }

    [Fact]
    public async Task ManagedReadOnlyCalendarCannotBeReplacedAsync()
    {
        RecordingAppleCalendarNativeBridge nativeBridge = new RecordingAppleCalendarNativeBridge(createCalendar(new AppleCalendarId("managed-read-only-calendar"), "2026-2학기 시간표", EAppleCalendarOwnership.ApplicationManaged, EAppleCalendarContentAccess.ReadOnly));
        RecordingCalendarNameConflictResolver conflictResolver = new RecordingCalendarNameConflictResolver(ECalendarNameConflictResolution.ReplaceExisting);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => new AppleCalendarExportService(nativeBridge).ExportAsync(createDocument(), conflictResolver, TestContext.Current.CancellationToken));

        Assert.False(Assert.Single(conflictResolver.Conflicts).CanReplace);
        Assert.Empty(nativeBridge.AppliedMutations);
    }

    [Fact]
    public async Task CalendarManagedByDifferentPlanCanBeReplacedWithoutChangingItsOwnershipPlanAsync()
    {
        PlanId existingCalendarOwnerPlanId = PlanId.CreateNew();
        AppleCalendarDescriptor existingCalendar = new AppleCalendarDescriptor(new AppleCalendarId("different-plan-calendar"), "2026-2학기 시간표", "source-1", existingCalendarOwnerPlanId, EAppleCalendarContentAccess.Writable);
        RecordingAppleCalendarNativeBridge nativeBridge = new RecordingAppleCalendarNativeBridge(existingCalendar);
        RecordingCalendarNameConflictResolver conflictResolver = new RecordingCalendarNameConflictResolver(ECalendarNameConflictResolution.ReplaceExisting);

        AppleCalendarExportResult result = await new AppleCalendarExportService(nativeBridge).ExportAsync(createDocument(), conflictResolver, TestContext.Current.CancellationToken);

        Assert.Equal(EAppleCalendarExportStatus.Success, result.Status);
        Assert.True(Assert.Single(conflictResolver.Conflicts).CanReplace);
        AppleCalendarExportMutation mutation = Assert.Single(nativeBridge.AppliedMutations);
        Assert.Equal(existingCalendarOwnerPlanId, mutation.CalendarOwnershipPlanId);
        Assert.NotEqual(mutation.Document.PlanId, mutation.CalendarOwnershipPlanId);
    }

    [Fact]
    public async Task DocumentWithoutEventsIsRejectedBeforeCalendarAccessAsync()
    {
        RecordingAppleCalendarNativeBridge nativeBridge = new RecordingAppleCalendarNativeBridge();
        AppleCalendarExportService exporter = new AppleCalendarExportService(nativeBridge);
        RecordingCalendarNameConflictResolver conflictResolver = new RecordingCalendarNameConflictResolver(ECalendarNameConflictResolution.Cancel);

        AppleCalendarExportResult result = await exporter.ExportAsync(createDocument(Array.Empty<RecurringCalendarEvent>()), conflictResolver, TestContext.Current.CancellationToken);

        Assert.Equal(EAppleCalendarExportStatus.Failed, result.Status);
        Assert.Equal("apple_calendar_export_requires_events", result.DiagnosticCodeOrNull);
        Assert.Equal(0, nativeBridge.CalendarSnapshotRequestCount);
        Assert.Empty(nativeBridge.AppliedMutations);
        Assert.Empty(conflictResolver.Conflicts);
    }

    [Fact]
    public async Task UnmanagedCollisionCreatesFirstAvailableNumberedCalendarAsync()
    {
        RecordingAppleCalendarNativeBridge nativeBridge = new RecordingAppleCalendarNativeBridge(createCalendar(new AppleCalendarId("personal-calendar"), "2026-2학기 시간표", EAppleCalendarOwnership.External, EAppleCalendarContentAccess.Writable), createCalendar(new AppleCalendarId("existing-copy"), "2026-2학기 시간표 (2)", EAppleCalendarOwnership.ApplicationManaged, EAppleCalendarContentAccess.Writable));
        RecordingCalendarNameConflictResolver conflictResolver = new RecordingCalendarNameConflictResolver(ECalendarNameConflictResolution.CreateWithAvailableName);

        AppleCalendarExportResult result = await new AppleCalendarExportService(nativeBridge).ExportAsync(createDocument(), conflictResolver, TestContext.Current.CancellationToken);

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
        RecordingAppleCalendarNativeBridge nativeBridge = new RecordingAppleCalendarNativeBridge(createCalendar(new AppleCalendarId("existing-calendar"), "2026-2학기 시간표", EAppleCalendarOwnership.ApplicationManaged, EAppleCalendarContentAccess.Writable));
        RecordingCalendarNameConflictResolver conflictResolver = new RecordingCalendarNameConflictResolver(ECalendarNameConflictResolution.Cancel);

        AppleCalendarExportResult result = await new AppleCalendarExportService(nativeBridge).ExportAsync(createDocument(), conflictResolver, TestContext.Current.CancellationToken);

        Assert.Equal(EAppleCalendarExportStatus.Cancelled, result.Status);
        Assert.Null(result.DiagnosticCodeOrNull);
        Assert.Empty(nativeBridge.AppliedMutations);
    }

    [Fact]
    public async Task DestinationRaceReloadsNamesBeforeRetryingAsync()
    {
        RecordingAppleCalendarNativeBridge nativeBridge = new RecordingAppleCalendarNativeBridge(createCalendar(new AppleCalendarId("existing-calendar"), "2026-2학기 시간표", EAppleCalendarOwnership.ApplicationManaged, EAppleCalendarContentAccess.Writable));
        nativeBridge.FailNextMutationForDestinationChange = true;
        nativeBridge.CalendarAddedAfterDestinationChange = createCalendar(new AppleCalendarId("racing-copy"), "2026-2학기 시간표 (2)", EAppleCalendarOwnership.External, EAppleCalendarContentAccess.Writable);
        RecordingCalendarNameConflictResolver conflictResolver = new RecordingCalendarNameConflictResolver(ECalendarNameConflictResolution.CreateWithAvailableName);

        AppleCalendarExportResult result = await new AppleCalendarExportService(nativeBridge).ExportAsync(createDocument(), conflictResolver, TestContext.Current.CancellationToken);

        Assert.Equal(EAppleCalendarExportStatus.Success, result.Status);
        Assert.Equal("2026-2학기 시간표 (3)", result.CalendarNameOrNull?.Value);
        Assert.Equal(2, conflictResolver.Conflicts.Count);
        Assert.Equal(2, nativeBridge.AppliedMutations.Count);
        Assert.Equal(1, nativeBridge.PendingReconciliationRequestCount);
        Assert.Equal("2026-2학기 시간표 (2)", nativeBridge.AppliedMutations[0].DestinationName.Value);
        Assert.Equal("2026-2학기 시간표 (3)", nativeBridge.AppliedMutations[1].DestinationName.Value);
        Assert.Equal(2, nativeBridge.RequestedDestinationNames.Count);
        Assert.All(
            nativeBridge.RequestedDestinationNames,
            requestedName => Assert.Equal(
                "2026-2학기 시간표",
                requestedName.Value));
    }

    [Fact]
    public async Task DestinationRejectedAfterAnIncompleteSnapshotOpensTheSafeConflictFlowAsync()
    {
        RecordingAppleCalendarNativeBridge nativeBridge = new RecordingAppleCalendarNativeBridge
        {
            FailNextMutationForDestinationChange = true,
        };
        RecordingCalendarNameConflictResolver conflictResolver = new RecordingCalendarNameConflictResolver(ECalendarNameConflictResolution.CreateWithAvailableName);

        AppleCalendarExportResult result = await new AppleCalendarExportService(nativeBridge).ExportAsync(createDocument(), conflictResolver, TestContext.Current.CancellationToken);

        Assert.Equal(EAppleCalendarExportStatus.Success, result.Status);
        Assert.Equal("2026-2학기 시간표 (2)", result.CalendarNameOrNull?.Value);
        CalendarNameConflict conflict = Assert.Single(conflictResolver.Conflicts);
        Assert.False(conflict.CanReplace);
        Assert.Equal("2026-2학기 시간표 (2)", conflict.NextAvailableName.Value);
        Assert.Collection(
            nativeBridge.AppliedMutations,
            mutation => Assert.Equal("2026-2학기 시간표", mutation.DestinationName.Value),
            mutation => Assert.Equal("2026-2학기 시간표 (2)", mutation.DestinationName.Value));
    }

    [Fact]
    public async Task UnsupportedNativeBridgeReturnsUnavailableWithoutPromptAsync()
    {
        RecordingAppleCalendarNativeBridge nativeBridge = new RecordingAppleCalendarNativeBridge();
        nativeBridge.IsAvailable = false;
        RecordingCalendarNameConflictResolver conflictResolver = new RecordingCalendarNameConflictResolver(ECalendarNameConflictResolution.Cancel);

        AppleCalendarExportResult result = await new AppleCalendarExportService(nativeBridge).ExportAsync(createDocument(), conflictResolver, TestContext.Current.CancellationToken);

        Assert.Equal(EAppleCalendarExportStatus.Unavailable, result.Status);
        Assert.Equal("apple_calendar_native_bridge_unavailable", result.DiagnosticCodeOrNull);
        Assert.Equal(0, nativeBridge.CalendarSnapshotRequestCount);
        Assert.Empty(conflictResolver.Conflicts);
    }


    [Fact]
    public async Task ResolverCannotReplaceUnmanagedCalendarAsync()
    {
        RecordingAppleCalendarNativeBridge nativeBridge = new RecordingAppleCalendarNativeBridge(createCalendar(new AppleCalendarId("personal-calendar"), "2026-2학기 시간표", EAppleCalendarOwnership.External, EAppleCalendarContentAccess.Writable));

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => new AppleCalendarExportService(nativeBridge).ExportAsync(
                createDocument(),
                new RecordingCalendarNameConflictResolver(ECalendarNameConflictResolution.ReplaceExisting), TestContext.Current.CancellationToken));

        Assert.Empty(nativeBridge.AppliedMutations);
    }

}
