using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using TimetableGenerator.Desktop.Integrations.GoogleCalendar;
using TimetableGenerator.Domain.Planning;

namespace TimetableGenerator.Desktop.Tests.Integrations.GoogleCalendar;

public sealed partial class GoogleCalendarExportServiceTests
{
    private sealed class PartialFailureReconciliationHttpMessageHandler : RecordingHttpMessageHandler
    {
        private readonly PlanId mPlanId;
        private readonly PlanId mOtherPlanId;
        private readonly HashSet<string> mDesiredEventIds;
        private readonly HashSet<string> mCurrentPlanEventIds;
        private readonly string mOtherPlanEventId;
        private readonly HttpMethod mFailureMethod;
        private int mSuccessfulFailureMethodMutationCount;
        private bool mFailureWasReturned;

        public bool OtherPlanEventRemains { get; private set; } = true;

        public bool UserEventRemains { get; private set; } = true;

        public bool ProtectedEventWasMutated { get; private set; }

        public bool HasConverged
        {
            get
            {
                if (mCurrentPlanEventIds.Count != mDesiredEventIds.Count)
                {
                    return false;
                }

                foreach (string desiredEventId in mDesiredEventIds)
                {
                    if (mCurrentPlanEventIds.Contains(desiredEventId) == false)
                    {
                        return false;
                    }
                }

                return true;
            }
        }

        public PartialFailureReconciliationHttpMessageHandler(GoogleCalendarExportPlan plan, string failureOperation)
        {
            mPlanId = plan.PlanId;
            mOtherPlanId = PlanId.CreateNew();
            mDesiredEventIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (GoogleCalendarExportEvent exportEvent in plan.Events)
            {
                mDesiredEventIds.Add(GoogleCalendarEventId.Create(plan.PlanId, exportEvent.SourceId).Value);
            }

            mCurrentPlanEventIds = new HashSet<string>(StringComparer.Ordinal);
            mOtherPlanEventId = GoogleCalendarEventId.Create(mOtherPlanId, new GoogleCalendarSourceEventId("protected-other-plan")).Value;
            switch (failureOperation)
            {
                case "create":
                    mFailureMethod = HttpMethod.Post;
                    break;
                case "update":
                    mFailureMethod = HttpMethod.Put;
                    mCurrentPlanEventIds.UnionWith(mDesiredEventIds);
                    break;
                case "delete":
                    mFailureMethod = HttpMethod.Delete;
                    mCurrentPlanEventIds.UnionWith(mDesiredEventIds);
                    mCurrentPlanEventIds.Add(GoogleCalendarEventId.Create(plan.PlanId, new GoogleCalendarSourceEventId("stale-first")).Value);
                    mCurrentPlanEventIds.Add(GoogleCalendarEventId.Create(plan.PlanId, new GoogleCalendarSourceEventId("stale-second")).Value);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(failureOperation), failureOperation, "Unknown partial-failure operation.");
            }
        }

        protected override HttpResponseMessage createResponse(RequestRecord request)
        {
            if (request.Method == HttpMethod.Get && request.Path.Contains("/events?", StringComparison.Ordinal))
            {
                return jsonResponse(createEventListJson());
            }

            if (isEventMutation(request) == false)
            {
                return jsonResponse("{}");
            }

            if (request.Method == mFailureMethod
                && mFailureWasReturned == false
                && mSuccessfulFailureMethodMutationCount == 1)
            {
                mFailureWasReturned = true;
                return new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
                {
                    Content = new StringContent("{}", Encoding.UTF8, "application/json"),
                };
            }

            applyMutation(request);
            if (request.Method == mFailureMethod && mFailureWasReturned == false)
            {
                mSuccessfulFailureMethodMutationCount++;
            }

            return jsonResponse("{}");
        }

        private bool isEventMutation(RequestRecord request)
        {
            return request.Path.Contains("/events", StringComparison.Ordinal)
                && (request.Method == HttpMethod.Post
                    || request.Method == HttpMethod.Put
                    || request.Method == HttpMethod.Delete);
        }

        private void applyMutation(RequestRecord request)
        {
            string eventId;
            if (request.Method == HttpMethod.Post)
            {
                using (JsonDocument document = JsonDocument.Parse(request.Body))
                {
                    eventId = document.RootElement.GetProperty("id").GetString()!;
                }

                mCurrentPlanEventIds.Add(eventId);
                return;
            }

            int finalPathSeparatorIndex = request.Path.LastIndexOf('/');
            eventId = request.Path[(finalPathSeparatorIndex + 1)..];
            if (string.Equals(eventId, mOtherPlanEventId, StringComparison.Ordinal))
            {
                ProtectedEventWasMutated = true;
                if (request.Method == HttpMethod.Delete)
                {
                    OtherPlanEventRemains = false;
                }

                return;
            }

            if (string.Equals(eventId, "manual-event", StringComparison.Ordinal))
            {
                ProtectedEventWasMutated = true;
                if (request.Method == HttpMethod.Delete)
                {
                    UserEventRemains = false;
                }

                return;
            }

            if (request.Method == HttpMethod.Delete)
            {
                mCurrentPlanEventIds.Remove(eventId);
            }
        }

        private string createEventListJson()
        {
            List<string> items = new List<string>();
            foreach (string eventId in mCurrentPlanEventIds)
            {
                items.Add(createManagedEventJson(eventId, mPlanId));
            }

            items.Add(createManagedEventJson(mOtherPlanEventId, mOtherPlanId));
            items.Add("{\"id\":\"manual-event\"}");
            return "{\"items\":[" + string.Join(',', items) + "]}";
        }

        private static string createManagedEventJson(string eventId, PlanId planId)
        {
            return "{\"id\":\"" + eventId + "\",\"extendedProperties\":{\"private\":{" + "\"timetableGeneratorManaged\":\"true\"," + "\"timetableGeneratorPlanId\":\"" + planId.Value.ToString("N") + "\"}}}";
        }
    }

    private sealed class ReconciliationHttpMessageHandler : RecordingHttpMessageHandler
    {
        private readonly PlanId mPlanId;

        private readonly GoogleCalendarEventId mDesiredId;

        private readonly GoogleCalendarEventId mStaleId;

        private readonly PlanId mOtherPlanId;

        private readonly GoogleCalendarEventId mOtherPlanEventId;

        public ReconciliationHttpMessageHandler(
            PlanId planId,
            GoogleCalendarEventId desiredId,
            GoogleCalendarEventId staleId,
            PlanId otherPlanId,
            GoogleCalendarEventId otherPlanEventId)
        {
            mPlanId = planId;
            mDesiredId = desiredId;
            mStaleId = staleId;
            mOtherPlanId = otherPlanId;
            mOtherPlanEventId = otherPlanEventId;
        }

        protected override HttpResponseMessage createResponse(RequestRecord request)
        {
            if (request.Method == HttpMethod.Get)
            {
                return jsonResponse("{\"items\":[{\"id\":\"" + mDesiredId.Value + "\",\"extendedProperties\":{\"private\":{" + "\"timetableGeneratorManaged\":\"true\"," + "\"timetableGeneratorPlanId\":\"" + mPlanId.Value.ToString("N") + "\"}}},{\"id\":\"" + mStaleId.Value + "\",\"extendedProperties\":{\"private\":{" + "\"timetableGeneratorManaged\":\"true\"," + "\"timetableGeneratorPlanId\":\"" + mPlanId.Value.ToString("N") + "\"}}},{\"id\":\"" + mOtherPlanEventId.Value + "\",\"extendedProperties\":{\"private\":{" + "\"timetableGeneratorManaged\":\"true\"," + "\"timetableGeneratorPlanId\":\"" + mOtherPlanId.Value.ToString("N") + "\"}}}," + "{\"id\":\"manual-event\"}]}");
            }

            return jsonResponse("{}");
        }
    }
}
