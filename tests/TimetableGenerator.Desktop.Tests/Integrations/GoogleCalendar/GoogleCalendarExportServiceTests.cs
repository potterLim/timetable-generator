using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using TimetableGenerator.Desktop.Exporting.Calendar;
using TimetableGenerator.Desktop.Integrations.GoogleCalendar;
using TimetableGenerator.Domain.Planning;
using TimetableGenerator.Domain.Scheduling;
using Xunit;

namespace TimetableGenerator.Desktop.Tests.Integrations.GoogleCalendar;

public sealed class GoogleCalendarExportServiceTests
{
    [Fact]
    public async Task AvailableRequestedNameCreatesANewCalendarWithoutPromptingAsync()
    {
        CalendarExportHttpMessageHandler handler =
            new CalendarExportHttpMessageHandler("{\"items\":[]}");
        RecordingConflictResolver resolver = new RecordingConflictResolver(
            ECalendarNameConflictResolution.Cancel);

        GoogleCalendarExportResult result = await exportAsync(handler, resolver);

        Assert.Equal(EGoogleCalendarExportStatus.Success, result.Status);
        Assert.Equal("created-calendar", result.CalendarIdOrNull?.Value);
        Assert.Equal("2026-2학기 시간표", result.CalendarNameOrNull?.Value);
        Assert.Equal(0, resolver.CallCount);
        Assert.Contains(
            handler.Requests,
            request => request.Method == HttpMethod.Post
                && request.Path.EndsWith("/calendars", StringComparison.Ordinal)
                && hasCalendarSummary(request, "2026-2학기 시간표"));
    }

    [Fact]
    public async Task NameConflictCanCreateTheNextAvailableNameAsync()
    {
        string listJson = createCalendarListJson(
            createCalendarJson(
                "existing",
                "2026-2학기 시간표",
                false,
                null));
        CalendarExportHttpMessageHandler handler =
            new CalendarExportHttpMessageHandler(listJson, listJson);
        RecordingConflictResolver resolver = new RecordingConflictResolver(
            ECalendarNameConflictResolution.CreateWithAvailableName);

        GoogleCalendarExportResult result = await exportAsync(handler, resolver);

        Assert.Equal(EGoogleCalendarExportStatus.Success, result.Status);
        Assert.Equal("2026-2학기 시간표 (2)", result.CalendarNameOrNull?.Value);
        Assert.Equal(1, resolver.CallCount);
        Assert.False(resolver.ConflictOrNull?.CanReplace);
        Assert.Equal(
            "2026-2학기 시간표 (2)",
            resolver.ConflictOrNull?.NextAvailableName.Value);
        Assert.Contains(
            handler.Requests,
            request => request.Method == HttpMethod.Post
                && request.Path.EndsWith("/calendars", StringComparison.Ordinal)
                && hasCalendarSummary(request, "2026-2학기 시간표 (2)"));
    }

    [Fact]
    public async Task ApplicationManagedNonPrimaryCalendarCanBeReplacedAsync()
    {
        GoogleCalendarExportPlan plan = createPlan();
        string marker = GoogleCalendarApiClient.createPlanMarker(
            new PlanId(Guid.Parse("5c113dab-0fe8-4c86-a69f-ef657e21314b")));
        string listJson = createCalendarListJson(
            createCalendarJson(
                "managed-calendar",
                plan.CalendarName.Value,
                false,
                marker));
        GoogleCalendarEventId staleEventId = GoogleCalendarEventId.Create(
            new PlanId(Guid.Parse("5c113dab-0fe8-4c86-a69f-ef657e21314b")),
            new GoogleCalendarSourceEventId("stale-event"));
        string eventListJson = "{\"items\":["
            + "{\"id\":\"" + staleEventId.Value + "\","
            + "\"extendedProperties\":{\"private\":{"
            + "\"timetableGeneratorManaged\":\"true\"}}},"
            + "{\"id\":\"manual-event\"}]}";
        CalendarExportHttpMessageHandler handler =
            new CalendarExportHttpMessageHandler(listJson, listJson)
            {
                EventListJson = eventListJson,
            };
        RecordingConflictResolver resolver = new RecordingConflictResolver(
            ECalendarNameConflictResolution.ReplaceExisting);

        GoogleCalendarExportResult result = await exportAsync(handler, resolver, plan);

        Assert.Equal(EGoogleCalendarExportStatus.Success, result.Status);
        Assert.Equal("managed-calendar", result.CalendarIdOrNull?.Value);
        Assert.True(resolver.ConflictOrNull?.CanReplace);
        Assert.Contains(
            handler.Requests,
            request => request.Method == HttpMethod.Put
                && request.Path.EndsWith(
                    "/calendars/managed-calendar",
                    StringComparison.Ordinal));
        Assert.Single(
            handler.Requests,
            request => request.Method == HttpMethod.Delete);
        Assert.Contains(
            handler.Requests,
            request => request.Method == HttpMethod.Delete
                && request.Path.EndsWith(staleEventId.Value, StringComparison.Ordinal));
        Assert.DoesNotContain(
            handler.Requests,
            request => request.Method == HttpMethod.Delete
                && request.Path.EndsWith("manual-event", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("owner", true)]
    [InlineData("writer", true)]
    [InlineData("writerWithoutPrivateAccess", true)]
    [InlineData("reader", false)]
    [InlineData("freeBusyReader", false)]
    [InlineData("futureRole", false)]
    [InlineData(null, false)]
    public async Task ReplacementRequiresAWriteCapableAccessRoleAsync(
        string? accessRoleOrNull,
        bool expectedCanReplace)
    {
        GoogleCalendarExportPlan plan = createPlan();
        string listJson = createCalendarListJson(
            createCalendarJson(
                "managed-calendar",
                plan.CalendarName.Value,
                false,
                GoogleCalendarApiClient.createPlanMarker(plan.PlanId),
                accessRoleOrNull));
        CalendarExportHttpMessageHandler handler =
            new CalendarExportHttpMessageHandler(listJson);
        RecordingConflictResolver resolver = new RecordingConflictResolver(
            ECalendarNameConflictResolution.Cancel);

        GoogleCalendarExportResult result = await exportAsync(
            handler,
            resolver,
            plan);

        Assert.Equal(EGoogleCalendarExportStatus.Cancelled, result.Status);
        Assert.Equal(expectedCanReplace, resolver.ConflictOrNull?.CanReplace);
        Assert.DoesNotContain(
            handler.Requests,
            request => request.Method == HttpMethod.Put
                || request.Method == HttpMethod.Post
                || request.Method == HttpMethod.Delete);
    }

    [Theory]
    [InlineData(true, true)]
    [InlineData(false, false)]
    public async Task PrimaryOrUnmanagedCalendarCannotBeReplacedAsync(
        bool isPrimary,
        bool hasApplicationMarker)
    {
        GoogleCalendarExportPlan plan = createPlan();
        string? markerOrNull = hasApplicationMarker
            ? GoogleCalendarApiClient.createPlanMarker(plan.PlanId)
            : null;
        string listJson = createCalendarListJson(
            createCalendarJson(
                "protected-calendar",
                plan.CalendarName.Value,
                isPrimary,
                markerOrNull));
        CalendarExportHttpMessageHandler handler =
            new CalendarExportHttpMessageHandler(listJson);
        RecordingConflictResolver resolver = new RecordingConflictResolver(
            ECalendarNameConflictResolution.ReplaceExisting);

        GoogleCalendarExportResult result = await exportAsync(handler, resolver, plan);

        Assert.Equal(EGoogleCalendarExportStatus.Failed, result.Status);
        Assert.False(resolver.ConflictOrNull?.CanReplace);
        Assert.DoesNotContain(
            handler.Requests,
            request => request.Method == HttpMethod.Put
                || request.Method == HttpMethod.Post
                || request.Method == HttpMethod.Delete);
    }

    [Fact]
    public async Task CancellingNameConflictDoesNotMutateGoogleCalendarAsync()
    {
        GoogleCalendarExportPlan plan = createPlan();
        string listJson = createCalendarListJson(
            createCalendarJson(
                "existing",
                plan.CalendarName.Value,
                false,
                GoogleCalendarApiClient.createPlanMarker(plan.PlanId)));
        CalendarExportHttpMessageHandler handler =
            new CalendarExportHttpMessageHandler(listJson);
        RecordingConflictResolver resolver = new RecordingConflictResolver(
            ECalendarNameConflictResolution.Cancel);

        GoogleCalendarExportResult result = await exportAsync(handler, resolver, plan);

        Assert.Equal(EGoogleCalendarExportStatus.Cancelled, result.Status);
        Assert.DoesNotContain(
            handler.Requests,
            request => request.Method == HttpMethod.Put
                || request.Method == HttpMethod.Post
                || request.Method == HttpMethod.Delete);
    }

    [Fact]
    public async Task ReplacementRevalidatesOwnershipBeforeMutationAsync()
    {
        GoogleCalendarExportPlan plan = createPlan();
        string firstListJson = createCalendarListJson(
            createCalendarJson(
                "managed-calendar",
                plan.CalendarName.Value,
                false,
                GoogleCalendarApiClient.createPlanMarker(plan.PlanId)));
        string secondListJson = createCalendarListJson(
            createCalendarJson(
                "managed-calendar",
                plan.CalendarName.Value,
                false,
                null));
        CalendarExportHttpMessageHandler handler =
            new CalendarExportHttpMessageHandler(firstListJson, secondListJson);
        RecordingConflictResolver resolver = new RecordingConflictResolver(
            ECalendarNameConflictResolution.ReplaceExisting);

        GoogleCalendarExportResult result = await exportAsync(handler, resolver, plan);

        Assert.Equal(EGoogleCalendarExportStatus.Failed, result.Status);
        Assert.DoesNotContain(
            handler.Requests,
            request => request.Method == HttpMethod.Put
                || request.Method == HttpMethod.Post
                || request.Method == HttpMethod.Delete);
    }

    [Fact]
    public async Task ReplacementRevalidatesTheConfirmedCalendarIdBeforeMutationAsync()
    {
        GoogleCalendarExportPlan plan = createPlan();
        string marker = GoogleCalendarApiClient.createPlanMarker(plan.PlanId);
        string firstListJson = createCalendarListJson(
            createCalendarJson(
                "confirmed-calendar",
                plan.CalendarName.Value,
                false,
                marker));
        string secondListJson = createCalendarListJson(
            createCalendarJson(
                "different-calendar",
                plan.CalendarName.Value,
                false,
                marker));
        CalendarExportHttpMessageHandler handler =
            new CalendarExportHttpMessageHandler(firstListJson, secondListJson);
        RecordingConflictResolver resolver = new RecordingConflictResolver(
            ECalendarNameConflictResolution.ReplaceExisting);

        GoogleCalendarExportResult result = await exportAsync(handler, resolver, plan);

        Assert.Equal(EGoogleCalendarExportStatus.Failed, result.Status);
        Assert.True(resolver.ConflictOrNull?.CanReplace);
        Assert.DoesNotContain(
            handler.Requests,
            request => request.Method == HttpMethod.Put
                || request.Method == HttpMethod.Post
                || request.Method == HttpMethod.Delete);
    }

    [Fact]
    public async Task ReplacementRevalidatesTheConfirmedCalendarNameBeforeMutationAsync()
    {
        GoogleCalendarExportPlan plan = createPlan();
        string marker = GoogleCalendarApiClient.createPlanMarker(plan.PlanId);
        string firstListJson = createCalendarListJson(
            createCalendarJson(
                "confirmed-calendar",
                plan.CalendarName.Value,
                false,
                marker));
        string secondListJson = createCalendarListJson(
            createCalendarJson(
                "confirmed-calendar",
                "이름이 바뀐 시간표",
                false,
                marker));
        CalendarExportHttpMessageHandler handler =
            new CalendarExportHttpMessageHandler(firstListJson, secondListJson);
        RecordingConflictResolver resolver = new RecordingConflictResolver(
            ECalendarNameConflictResolution.ReplaceExisting);

        GoogleCalendarExportResult result = await exportAsync(handler, resolver, plan);

        Assert.Equal(EGoogleCalendarExportStatus.Failed, result.Status);
        Assert.DoesNotContain(
            handler.Requests,
            request => request.Method == HttpMethod.Put
                || request.Method == HttpMethod.Post
                || request.Method == HttpMethod.Delete);
    }

    [Fact]
    public async Task ReplacementRevalidatesTheManagementMarkerBeforeMutationAsync()
    {
        GoogleCalendarExportPlan plan = createPlan();
        string firstListJson = createCalendarListJson(
            createCalendarJson(
                "confirmed-calendar",
                plan.CalendarName.Value,
                false,
                GoogleCalendarApiClient.createPlanMarker(plan.PlanId)));
        string secondListJson = createCalendarListJson(
            createCalendarJson(
                "confirmed-calendar",
                plan.CalendarName.Value,
                false,
                GoogleCalendarApiClient.createPlanMarker(PlanId.CreateNew())));
        CalendarExportHttpMessageHandler handler =
            new CalendarExportHttpMessageHandler(firstListJson, secondListJson);
        RecordingConflictResolver resolver = new RecordingConflictResolver(
            ECalendarNameConflictResolution.ReplaceExisting);

        GoogleCalendarExportResult result = await exportAsync(handler, resolver, plan);

        Assert.Equal(EGoogleCalendarExportStatus.Failed, result.Status);
        Assert.DoesNotContain(
            handler.Requests,
            request => request.Method == HttpMethod.Put
                || request.Method == HttpMethod.Post
                || request.Method == HttpMethod.Delete);
    }

    [Fact]
    public async Task ReplacementRevalidatesWriteAccessBeforeMutationAsync()
    {
        GoogleCalendarExportPlan plan = createPlan();
        string marker = GoogleCalendarApiClient.createPlanMarker(plan.PlanId);
        string firstListJson = createCalendarListJson(
            createCalendarJson(
                "confirmed-calendar",
                plan.CalendarName.Value,
                false,
                marker,
                "owner"));
        string secondListJson = createCalendarListJson(
            createCalendarJson(
                "confirmed-calendar",
                plan.CalendarName.Value,
                false,
                marker,
                "reader"));
        CalendarExportHttpMessageHandler handler =
            new CalendarExportHttpMessageHandler(firstListJson, secondListJson);
        RecordingConflictResolver resolver = new RecordingConflictResolver(
            ECalendarNameConflictResolution.ReplaceExisting);

        GoogleCalendarExportResult result = await exportAsync(handler, resolver, plan);

        Assert.Equal(EGoogleCalendarExportStatus.Failed, result.Status);
        Assert.DoesNotContain(
            handler.Requests,
            request => request.Method == HttpMethod.Put
                || request.Method == HttpMethod.Post
                || request.Method == HttpMethod.Delete);
    }

    [Fact]
    public async Task OccupiedSuggestedNameIsConfirmedAgainBeforeCreationAsync()
    {
        GoogleCalendarExportPlan plan = createPlan();
        string requestedCalendar = createCalendarJson(
            "requested-calendar",
            plan.CalendarName.Value,
            false,
            null);
        string firstListJson = createCalendarListJson(requestedCalendar);
        string secondListJson = createCalendarListJson(
            requestedCalendar,
            createCalendarJson(
                "first-copy",
                plan.CalendarName.Value + " (2)",
                false,
                null));
        CalendarExportHttpMessageHandler handler =
            new CalendarExportHttpMessageHandler(
                firstListJson,
                secondListJson,
                secondListJson);
        RecordingConflictResolver resolver = new RecordingConflictResolver(
            ECalendarNameConflictResolution.CreateWithAvailableName);

        GoogleCalendarExportResult result = await exportAsync(handler, resolver, plan);

        Assert.Equal(EGoogleCalendarExportStatus.Success, result.Status);
        Assert.Equal("2026-2학기 시간표 (3)", result.CalendarNameOrNull?.Value);
        Assert.Equal(2, resolver.CallCount);
        Assert.Equal(2, resolver.Conflicts.Count);
        Assert.Equal(
            "2026-2학기 시간표 (2)",
            resolver.Conflicts[0].NextAvailableName.Value);
        Assert.Equal(
            "2026-2학기 시간표 (3)",
            resolver.Conflicts[1].NextAvailableName.Value);
        Assert.Contains(
            handler.Requests,
            request => request.Method == HttpMethod.Post
                && hasCalendarSummary(request, "2026-2학기 시간표 (3)"));
    }

    [Fact]
    public async Task ChangedConflictDoesNotSilentlyRestoreTheRequestedNameAsync()
    {
        GoogleCalendarExportPlan plan = createPlan();
        string requestedCalendar = createCalendarJson(
            "requested-calendar",
            plan.CalendarName.Value,
            false,
            null);
        string occupiedCopy = createCalendarJson(
            "first-copy",
            plan.CalendarName.Value + " (2)",
            false,
            null);
        CalendarExportHttpMessageHandler handler =
            new CalendarExportHttpMessageHandler(
                createCalendarListJson(requestedCalendar),
                createCalendarListJson(occupiedCopy));
        RecordingConflictResolver resolver = new RecordingConflictResolver(
            ECalendarNameConflictResolution.CreateWithAvailableName);

        GoogleCalendarExportResult result = await exportAsync(handler, resolver, plan);

        Assert.Equal(EGoogleCalendarExportStatus.Failed, result.Status);
        Assert.Equal(1, resolver.CallCount);
        Assert.DoesNotContain(
            handler.Requests,
            request => request.Method == HttpMethod.Put
                || request.Method == HttpMethod.Post
                || request.Method == HttpMethod.Delete);
    }

    [Fact]
    public async Task ReconfirmedSuggestedNameCanBeCancelledWithoutMutationAsync()
    {
        GoogleCalendarExportPlan plan = createPlan();
        string requestedCalendar = createCalendarJson(
            "requested-calendar",
            plan.CalendarName.Value,
            false,
            null);
        string secondListJson = createCalendarListJson(
            requestedCalendar,
            createCalendarJson(
                "first-copy",
                plan.CalendarName.Value + " (2)",
                false,
                null));
        CalendarExportHttpMessageHandler handler =
            new CalendarExportHttpMessageHandler(
                createCalendarListJson(requestedCalendar),
                secondListJson);
        SequencedConflictResolver resolver = new SequencedConflictResolver(
            ECalendarNameConflictResolution.CreateWithAvailableName,
            ECalendarNameConflictResolution.Cancel);

        GoogleCalendarExportResult result = await exportAsync(handler, resolver, plan);

        Assert.Equal(EGoogleCalendarExportStatus.Cancelled, result.Status);
        Assert.Equal(2, resolver.CallCount);
        Assert.DoesNotContain(
            handler.Requests,
            request => request.Method == HttpMethod.Put
                || request.Method == HttpMethod.Post
                || request.Method == HttpMethod.Delete);
    }

    [Fact]
    public async Task RepeatedSuggestedNameRacesStopWithoutMutationAsync()
    {
        GoogleCalendarExportPlan plan = createPlan();
        string requestedCalendar = createCalendarJson(
            "requested-calendar",
            plan.CalendarName.Value,
            false,
            null);
        string copyTwo = createCalendarJson(
            "copy-two",
            plan.CalendarName.Value + " (2)",
            false,
            null);
        string copyThree = createCalendarJson(
            "copy-three",
            plan.CalendarName.Value + " (3)",
            false,
            null);
        string copyFour = createCalendarJson(
            "copy-four",
            plan.CalendarName.Value + " (4)",
            false,
            null);
        CalendarExportHttpMessageHandler handler =
            new CalendarExportHttpMessageHandler(
                createCalendarListJson(requestedCalendar),
                createCalendarListJson(requestedCalendar, copyTwo),
                createCalendarListJson(requestedCalendar, copyTwo, copyThree),
                createCalendarListJson(
                    requestedCalendar,
                    copyTwo,
                    copyThree,
                    copyFour));
        RecordingConflictResolver resolver = new RecordingConflictResolver(
            ECalendarNameConflictResolution.CreateWithAvailableName);

        GoogleCalendarExportResult result = await exportAsync(handler, resolver, plan);

        Assert.Equal(EGoogleCalendarExportStatus.Failed, result.Status);
        Assert.Equal(3, resolver.CallCount);
        Assert.DoesNotContain(
            handler.Requests,
            request => request.Method == HttpMethod.Put
                || request.Method == HttpMethod.Post
                || request.Method == HttpMethod.Delete);
    }

    [Fact]
    public async Task ReconciliationDeletesManagedEventsFromPreviousExportsOnlyAsync()
    {
        GoogleCalendarExportPlan plan = createPlan();
        GoogleCalendarEventId desiredId = GoogleCalendarEventId.Create(
            plan.PlanId,
            plan.Events[0].SourceId);
        GoogleCalendarEventId staleId = GoogleCalendarEventId.Create(
            new PlanId(Guid.NewGuid()),
            new GoogleCalendarSourceEventId("stale"));
        ReconciliationHttpMessageHandler handler =
            new ReconciliationHttpMessageHandler(desiredId, staleId);
        GoogleCalendarApiClient apiClient = new GoogleCalendarApiClient(
            new HttpClient(handler));

        GoogleCalendarReconciliationResult result = await apiClient.ReconcileEventsAsync(
            new GoogleAccessToken("access-secret"),
            new GoogleCalendarId("calendar-id"),
            plan,
            CancellationToken.None);

        Assert.Equal(0, result.CreatedEventCount);
        Assert.Equal(1, result.UpdatedEventCount);
        Assert.Equal(1, result.DeletedEventCount);
        Assert.Contains(
            handler.Requests,
            request => request.Path.Contains(
                "privateExtendedProperty=timetableGeneratorManaged%3Dtrue",
                StringComparison.Ordinal));
        Assert.DoesNotContain(
            handler.Requests,
            request => request.Path.Contains(
                "privateExtendedProperty=timetableGeneratorPlanId%3D",
                StringComparison.Ordinal));
        Assert.Single(
            handler.Requests,
            request => request.Method == HttpMethod.Delete);
        Assert.Contains(
            handler.Requests,
            request => request.Method == HttpMethod.Delete
                && request.Path.EndsWith(staleId.Value, StringComparison.Ordinal));
    }

    [Fact]
    public async Task CalendarListUsesVisibleOverrideAndSkipsDeletedEntriesAsync()
    {
        string json = "{\"items\":["
            + "{\"id\":\"deleted\",\"summary\":\"Deleted\",\"deleted\":true},"
            + "{\"id\":\"visible\",\"summary\":\"Original\","
            + "\"summaryOverride\":\"Visible\",\"primary\":false,"
            + "\"accessRole\":\"owner\","
            + "\"description\":\"TimetableGenerator-Plan:"
            + Guid.NewGuid().ToString("N") + "\"}]}";
        GoogleCalendarApiClient apiClient = new GoogleCalendarApiClient(
            new HttpClient(new FixedResponseHttpMessageHandler(HttpStatusCode.OK, json)));

        IReadOnlyList<GoogleCalendarDescriptor> calendars =
            await apiClient.ListCalendarsAsync(
                new GoogleAccessToken("access-secret"),
                CancellationToken.None);

        GoogleCalendarDescriptor calendar = Assert.Single(calendars);
        Assert.Equal("visible", calendar.CalendarId.Value);
        Assert.Equal("Visible", calendar.DisplayName);
        Assert.Equal(EGoogleCalendarAccessRole.Owner, calendar.AccessRole);
        Assert.NotNull(calendar.ManagedPlanIdOrNull);
        Assert.True(calendar.CanReplace);
    }

    [Fact]
    public async Task ApiTimeoutIsReportedAsNetworkFailureAsync()
    {
        using (GoogleCalendarExportService exporter = new GoogleCalendarExportService(
            new FixedAccessTokenProvider(),
            new GoogleCalendarApiClient(new HttpClient(new TimeoutHttpMessageHandler()))))
        {
            GoogleCalendarExportResult result = await exporter.ExportAsync(
                createPlan(),
                new RecordingConflictResolver(ECalendarNameConflictResolution.Cancel),
                CancellationToken.None);

            Assert.Equal(EGoogleCalendarExportStatus.NetworkFailed, result.Status);
            Assert.Equal("google_calendar_timeout", result.DiagnosticCodeOrNull);
        }
    }

    [Fact]
    public async Task ForbiddenRateLimitIsNotMisreportedAsAccessDeniedAsync()
    {
        GoogleCalendarExportResult result = await exportWithResponseAsync(
            HttpStatusCode.Forbidden,
            "{\"error\":{\"errors\":[{\"reason\":\"rateLimitExceeded\"}]}}");

        Assert.Equal(EGoogleCalendarExportStatus.NetworkFailed, result.Status);
    }

    [Fact]
    public async Task OrdinaryForbiddenResponseRemainsAccessDeniedAsync()
    {
        GoogleCalendarExportResult result = await exportWithResponseAsync(
            HttpStatusCode.Forbidden,
            "{\"error\":{\"errors\":[{\"reason\":\"forbidden\"}]}}");

        Assert.Equal(EGoogleCalendarExportStatus.AccessDenied, result.Status);
    }

    [Fact]
    public async Task RepeatedCalendarListPageTokenIsRejectedAsync()
    {
        RepeatingCalendarPageHttpMessageHandler handler =
            new RepeatingCalendarPageHttpMessageHandler();
        GoogleCalendarApiClient apiClient = new GoogleCalendarApiClient(
            new HttpClient(handler));

        GoogleCalendarApiException exception = await Assert.ThrowsAsync<
            GoogleCalendarApiException>(
            async delegate
            {
                await apiClient.ListCalendarsAsync(
                    new GoogleAccessToken("access-secret"),
                    CancellationToken.None);
            });

        Assert.Equal("calendar_list_invalid_pagination", exception.DiagnosticCode);
        Assert.Equal(2, handler.RequestCount);
    }

    [Fact]
    public async Task OversizedCalendarListResponseIsRejectedAsync()
    {
        GoogleCalendarApiClient apiClient = new GoogleCalendarApiClient(
            new HttpClient(
                new FixedResponseHttpMessageHandler(
                    HttpStatusCode.OK,
                    new string('x', 5_000_000))));

        GoogleCalendarApiException exception = await Assert.ThrowsAsync<
            GoogleCalendarApiException>(
            async delegate
            {
                await apiClient.ListCalendarsAsync(
                    new GoogleAccessToken("access-secret"),
                    CancellationToken.None);
            });

        Assert.Equal("google_calendar_response_too_large", exception.DiagnosticCode);
    }

    [Fact]
    public async Task DisposeDuringActiveExportCancelsBeforeReleasingOwnedResourcesAsync()
    {
        BlockingAccessTokenProvider accessTokenProvider =
            new BlockingAccessTokenProvider();
        TrackingDisposable ownedResources = new TrackingDisposable();
        GoogleCalendarExportService exporter = new GoogleCalendarExportService(
            accessTokenProvider,
            new GoogleCalendarApiClient(new HttpClient(new TimeoutHttpMessageHandler())),
            ownedResources);
        Task<GoogleCalendarExportResult> exportTask = exporter.ExportAsync(
            createPlan(),
            new RecordingConflictResolver(ECalendarNameConflictResolution.Cancel),
            CancellationToken.None);
        await accessTokenProvider.Started.WaitAsync(
            TimeSpan.FromSeconds(2.0),
            TestContext.Current.CancellationToken);

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

    [Fact]
    public void ExportResultRejectsInvalidStatusAndNegativeCounts()
    {
        Assert.Throws<ArgumentException>(
            delegate
            {
                GoogleCalendarExportResult.Fail(
                    EGoogleCalendarExportStatus.None,
                    "invalid");
            });
        Assert.Throws<ArgumentOutOfRangeException>(
            delegate
            {
                new GoogleCalendarReconciliationResult(-1, 0, 0);
            });
    }

    [Fact]
    public void CalendarDescriptorRejectsInvalidStronglyTypedState()
    {
        Assert.Throws<ArgumentException>(
            delegate
            {
                new GoogleCalendarDescriptor(
                    new GoogleCalendarId("calendar-id"),
                    "시간표",
                    false,
                    default(PlanId),
                    EGoogleCalendarAccessRole.Owner);
            });
        Assert.Throws<ArgumentOutOfRangeException>(
            delegate
            {
                new GoogleCalendarDescriptor(
                    new GoogleCalendarId("calendar-id"),
                    "시간표",
                    false,
                    null,
                    (EGoogleCalendarAccessRole)99);
            });
    }

    private static async Task<GoogleCalendarExportResult> exportAsync(
        CalendarExportHttpMessageHandler handler,
        ICalendarNameConflictResolver resolver,
        GoogleCalendarExportPlan? planOrNull = null)
    {
        using (GoogleCalendarExportService exporter = new GoogleCalendarExportService(
            new FixedAccessTokenProvider(),
            new GoogleCalendarApiClient(new HttpClient(handler))))
        {
            return await exporter.ExportAsync(
                planOrNull ?? createPlan(),
                resolver,
                CancellationToken.None);
        }
    }

    private static async Task<GoogleCalendarExportResult> exportWithResponseAsync(
        HttpStatusCode statusCode,
        string body)
    {
        using (GoogleCalendarExportService exporter = new GoogleCalendarExportService(
            new FixedAccessTokenProvider(),
            new GoogleCalendarApiClient(
                new HttpClient(
                    new FixedResponseHttpMessageHandler(statusCode, body)))))
        {
            return await exporter.ExportAsync(
                createPlan(),
                new RecordingConflictResolver(ECalendarNameConflictResolution.Cancel),
                CancellationToken.None);
        }
    }

    private static GoogleCalendarExportPlan createPlan()
    {
        PlanId planId = new PlanId(
            Guid.Parse("71f3be04-d4c6-41d4-a269-792321e71423"));
        GoogleCalendarExportEvent exportEvent = new GoogleCalendarExportEvent(
            new GoogleCalendarSourceEventId("course:ITP30003"),
            new CalendarEventContent("컴퓨터 구조(01)", "OH 401", "담당: 이원형"),
            new GoogleCalendarRecurrenceDateRange(
                new DateOnly(2026, 8, 31),
                new DateOnly(2026, 12, 20)),
            new DailyTimeRange(
                new ScheduleTime(11, 30),
                new ScheduleTime(12, 15)),
            new EDay[] { EDay.Monday, EDay.Thursday });
        return new GoogleCalendarExportPlan(
            planId,
            new PlanName("2026-2학기 시간표"),
            new CalendarTimeZoneId("Asia/Seoul"),
            new GoogleCalendarExportEvent[] { exportEvent });
    }

    private static string createCalendarListJson(params string[] items)
    {
        return "{\"items\":[" + string.Join(',', items) + "]}";
    }

    private static bool hasCalendarSummary(
        RequestRecord request,
        string expectedSummary)
    {
        using (JsonDocument document = JsonDocument.Parse(request.Body))
        {
            JsonElement summary;
            return document.RootElement.TryGetProperty("summary", out summary)
                && summary.ValueKind == JsonValueKind.String
                && string.Equals(
                    summary.GetString(),
                    expectedSummary,
                    StringComparison.Ordinal);
        }
    }

    private static string createCalendarJson(
        string id,
        string name,
        bool isPrimary,
        string? descriptionOrNull)
    {
        return createCalendarJson(
            id,
            name,
            isPrimary,
            descriptionOrNull,
            "owner");
    }

    private static string createCalendarJson(
        string id,
        string name,
        bool isPrimary,
        string? descriptionOrNull,
        string? accessRoleOrNull)
    {
        string description = descriptionOrNull == null
            ? string.Empty
            : ",\"description\":\"" + descriptionOrNull + "\"";
        string accessRole = accessRoleOrNull == null
            ? string.Empty
            : ",\"accessRole\":\"" + accessRoleOrNull + "\"";
        return "{\"id\":\"" + id + "\",\"summary\":\"" + name
            + "\",\"primary\":" + (isPrimary ? "true" : "false")
            + description + accessRole + "}";
    }

    private sealed class FixedAccessTokenProvider : IGoogleAccessTokenProvider
    {
        public Task<GoogleOAuthAuthorizationResult> AuthorizeAsync(
            CancellationToken cancellationToken)
        {
            return Task.FromResult(
                GoogleOAuthAuthorizationResult.Complete(
                    new GoogleAccessToken("access-secret")));
        }
    }

    private sealed class RecordingConflictResolver : ICalendarNameConflictResolver
    {
        private readonly ECalendarNameConflictResolution mResolution;

        private readonly List<CalendarNameConflict> mConflicts =
            new List<CalendarNameConflict>();

        public int CallCount { get; private set; }

        public CalendarNameConflict? ConflictOrNull { get; private set; }

        public IReadOnlyList<CalendarNameConflict> Conflicts
        {
            get
            {
                return mConflicts;
            }
        }

        public RecordingConflictResolver(ECalendarNameConflictResolution resolution)
        {
            mResolution = resolution;
        }

        public Task<ECalendarNameConflictResolution> ResolveAsync(
            CalendarNameConflict conflict,
            CancellationToken cancellationToken)
        {
            CallCount++;
            ConflictOrNull = conflict;
            mConflicts.Add(conflict);
            return Task.FromResult(mResolution);
        }
    }

    private sealed class SequencedConflictResolver : ICalendarNameConflictResolver
    {
        private readonly Queue<ECalendarNameConflictResolution> mResolutions;

        public int CallCount { get; private set; }

        public SequencedConflictResolver(
            params ECalendarNameConflictResolution[] resolutions)
        {
            if (resolutions == null || resolutions.Length == 0)
            {
                throw new ArgumentException(
                    "At least one conflict resolution is required.",
                    nameof(resolutions));
            }

            mResolutions = new Queue<ECalendarNameConflictResolution>(
                resolutions);
        }

        public Task<ECalendarNameConflictResolution> ResolveAsync(
            CalendarNameConflict conflict,
            CancellationToken cancellationToken)
        {
            if (conflict == null)
            {
                throw new ArgumentNullException(nameof(conflict));
            }

            cancellationToken.ThrowIfCancellationRequested();
            if (mResolutions.Count == 0)
            {
                throw new InvalidOperationException(
                    "No recorded conflict resolution remains.");
            }

            CallCount++;
            return Task.FromResult(mResolutions.Dequeue());
        }
    }

    private sealed class BlockingAccessTokenProvider : IGoogleAccessTokenProvider
    {
        private readonly TaskCompletionSource mStartedSource =
            new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task Started
        {
            get
            {
                return mStartedSource.Task;
            }
        }

        public async Task<GoogleOAuthAuthorizationResult> AuthorizeAsync(
            CancellationToken cancellationToken)
        {
            mStartedSource.TrySetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            throw new InvalidOperationException("The cancellation wait unexpectedly completed.");
        }
    }

    private sealed class TrackingDisposable : IDisposable
    {
        public int DisposeCount { get; private set; }

        public void Dispose()
        {
            DisposeCount++;
        }
    }

    private sealed class TimeoutHttpMessageHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            return Task.FromException<HttpResponseMessage>(
                new TaskCanceledException("Simulated HTTP timeout."));
        }
    }

    private sealed class FixedResponseHttpMessageHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode mStatusCode;
        private readonly string mBody;

        public FixedResponseHttpMessageHandler(
            HttpStatusCode statusCode,
            string body)
        {
            mStatusCode = statusCode;
            mBody = body;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(
                new HttpResponseMessage(mStatusCode)
                {
                    Content = new StringContent(mBody, Encoding.UTF8, "application/json"),
                });
        }
    }

    private sealed class RepeatingCalendarPageHttpMessageHandler : HttpMessageHandler
    {
        public int RequestCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RequestCount++;
            return Task.FromResult(
                new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(
                        "{\"items\":[],\"nextPageToken\":\"repeated\"}",
                        Encoding.UTF8,
                        "application/json"),
                });
        }
    }

    private sealed class CalendarExportHttpMessageHandler : RecordingHttpMessageHandler
    {
        private readonly Queue<string> mCalendarLists;
        private string mLastCalendarList;

        public string EventListJson { get; init; } = "{\"items\":[]}";

        public CalendarExportHttpMessageHandler(params string[] calendarLists)
        {
            if (calendarLists == null || calendarLists.Length == 0)
            {
                throw new ArgumentException(
                    "At least one calendar-list response is required.",
                    nameof(calendarLists));
            }

            mCalendarLists = new Queue<string>(calendarLists);
            mLastCalendarList = calendarLists[^1];
        }

        protected override HttpResponseMessage createResponse(RequestRecord request)
        {
            if (request.Method == HttpMethod.Get
                && request.Path.Contains(
                    "/users/me/calendarList?",
                    StringComparison.Ordinal))
            {
                if (mCalendarLists.Count > 0)
                {
                    mLastCalendarList = mCalendarLists.Dequeue();
                }

                return jsonResponse(mLastCalendarList);
            }

            if (request.Method == HttpMethod.Post
                && request.Path.EndsWith("/calendars", StringComparison.Ordinal))
            {
                return jsonResponse("{\"id\":\"created-calendar\"}");
            }

            if (request.Method == HttpMethod.Get
                && request.Path.Contains("/events?", StringComparison.Ordinal))
            {
                return jsonResponse(EventListJson);
            }

            return jsonResponse("{}");
        }
    }

    private sealed class ReconciliationHttpMessageHandler : RecordingHttpMessageHandler
    {
        private readonly GoogleCalendarEventId mDesiredId;
        private readonly GoogleCalendarEventId mStaleId;

        public ReconciliationHttpMessageHandler(
            GoogleCalendarEventId desiredId,
            GoogleCalendarEventId staleId)
        {
            mDesiredId = desiredId;
            mStaleId = staleId;
        }

        protected override HttpResponseMessage createResponse(RequestRecord request)
        {
            if (request.Method == HttpMethod.Get)
            {
                return jsonResponse(
                    "{\"items\":[{\"id\":\""
                        + mDesiredId.Value
                        + "\",\"extendedProperties\":{\"private\":{"
                        + "\"timetableGeneratorManaged\":\"true\"}}},{\"id\":\""
                        + mStaleId.Value
                        + "\",\"extendedProperties\":{\"private\":{"
                        + "\"timetableGeneratorManaged\":\"true\"}}},"
                        + "{\"id\":\"manual-event\"}]}");
            }

            return jsonResponse("{}");
        }
    }

    private abstract class RecordingHttpMessageHandler : HttpMessageHandler
    {
        private readonly List<RequestRecord> mRequests = new List<RequestRecord>();

        public IReadOnlyList<RequestRecord> Requests
        {
            get
            {
                return mRequests;
            }
        }

        protected sealed override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            string body = request.Content == null
                ? string.Empty
                : await request.Content.ReadAsStringAsync(cancellationToken);
            string path = request.RequestUri == null
                ? string.Empty
                : request.RequestUri.PathAndQuery;
            RequestRecord record = new RequestRecord(request.Method, path, body);
            mRequests.Add(record);
            return createResponse(record);
        }

        protected abstract HttpResponseMessage createResponse(RequestRecord request);

        protected static HttpResponseMessage jsonResponse(string json)
        {
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json"),
            };
        }
    }

    private sealed record RequestRecord(
        HttpMethod Method,
        string Path,
        string Body);
}
