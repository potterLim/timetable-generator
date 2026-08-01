using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using TimetableGenerator.Desktop.Integrations.GoogleCalendar;
using TimetableGenerator.Domain.Planning;
using Xunit;

namespace TimetableGenerator.Desktop.Tests.Integrations.GoogleCalendar;

public sealed partial class GoogleCalendarExportServiceTests
{
    [Fact]
    public async Task ReconciliationDeletesOnlyStaleEventsFromTheCurrentPlanAsync()
    {
        GoogleCalendarExportPlan plan = createPlan();
        GoogleCalendarEventId desiredId = GoogleCalendarEventId.Create(plan.PlanId, plan.Events[0].SourceId);
        GoogleCalendarEventId staleId = GoogleCalendarEventId.Create(plan.PlanId, new GoogleCalendarSourceEventId("stale"));
        PlanId otherPlanId = PlanId.CreateNew();
        GoogleCalendarEventId otherPlanEventId = GoogleCalendarEventId.Create(otherPlanId, new GoogleCalendarSourceEventId("other-plan"));
        ReconciliationHttpMessageHandler handler = new ReconciliationHttpMessageHandler(
            plan.PlanId,
            desiredId,
            staleId,
            otherPlanId,
            otherPlanEventId);
        GoogleCalendarApiClient apiClient = new GoogleCalendarApiClient(new HttpClient(handler));

        GoogleCalendarReconciliationResult result = await apiClient.ReconcileEventsAsync(new GoogleAccessToken("access-secret"), new GoogleCalendarId("calendar-id"), plan, CancellationToken.None);

        Assert.Equal(0, result.CreatedEventCount);
        Assert.Equal(1, result.UpdatedEventCount);
        Assert.Equal(1, result.DeletedEventCount);
        Assert.Contains(
            handler.Requests,
            request => request.Path.Contains("privateExtendedProperty=timetableGeneratorManaged%3Dtrue", StringComparison.Ordinal));
        Assert.Contains(
            handler.Requests,
            request => request.Path.Contains("privateExtendedProperty=timetableGeneratorPlanId%3D" + plan.PlanId.Value.ToString("N"), StringComparison.Ordinal));
        Assert.Single(handler.Requests, request => request.Method == HttpMethod.Delete);
        Assert.Contains(
            handler.Requests,
            request => request.Method == HttpMethod.Delete
                && request.Path.EndsWith(staleId.Value, StringComparison.Ordinal));
        Assert.DoesNotContain(
            handler.Requests,
            request => request.Method == HttpMethod.Delete
                && request.Path.EndsWith(otherPlanEventId.Value, StringComparison.Ordinal));
        Assert.DoesNotContain(
            handler.Requests,
            request => request.Method == HttpMethod.Delete
                && request.Path.EndsWith("manual-event", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("create", "event_create_failed", 1, 1, 0)]
    [InlineData("update", "event_update_failed", 0, 2, 0)]
    [InlineData("delete", "event_delete_failed", 0, 2, 1)]
    public async Task PartialEventMutationFailureConvergesOnRetryWithoutTouchingProtectedEventsAsync(
        string failureOperation,
        string expectedDiagnosticCode,
        int expectedCreatedCount,
        int expectedUpdatedCount,
        int expectedDeletedCount)
    {
        GoogleCalendarExportPlan plan = createPlanWithTwoEvents();
        PartialFailureReconciliationHttpMessageHandler handler = new PartialFailureReconciliationHttpMessageHandler(plan, failureOperation);
        GoogleCalendarApiClient apiClient = new GoogleCalendarApiClient(new HttpClient(handler));
        GoogleAccessToken accessToken = new GoogleAccessToken("access-secret");
        GoogleCalendarId calendarId = new GoogleCalendarId("calendar-id");

        GoogleCalendarApiException firstFailure = await Assert.ThrowsAsync<GoogleCalendarApiException>(
            async delegate
            {
                await apiClient.ReconcileEventsAsync(accessToken, calendarId, plan, CancellationToken.None);
            });
        GoogleCalendarReconciliationResult retryResult = await apiClient.ReconcileEventsAsync(accessToken, calendarId, plan, CancellationToken.None);

        Assert.Equal(expectedDiagnosticCode, firstFailure.DiagnosticCode);
        Assert.Equal(expectedCreatedCount, retryResult.CreatedEventCount);
        Assert.Equal(expectedUpdatedCount, retryResult.UpdatedEventCount);
        Assert.Equal(expectedDeletedCount, retryResult.DeletedEventCount);
        Assert.True(handler.HasConverged);
        Assert.True(handler.OtherPlanEventRemains);
        Assert.True(handler.UserEventRemains);
        Assert.False(handler.ProtectedEventWasMutated);
    }
}
