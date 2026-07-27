using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

using TimetableGenerator.Domain.Planning;

namespace TimetableGenerator.Desktop.Exporting.AppleCalendar;

internal sealed class JxaAppleCalendarNativeBridge
    : IAppleCalendarNativeBridge
{
    private static readonly JsonSerializerOptions sJsonOptions = createJsonOptions();

    private readonly IAppleCalendarAutomationCommand mAutomationCommand;

    public bool IsAvailable
    {
        get
        {
            return mAutomationCommand.IsAvailable;
        }
    }

    public JxaAppleCalendarNativeBridge()
        : this(new ProcessAppleCalendarAutomationCommand())
    {
    }

    internal JxaAppleCalendarNativeBridge(
        IAppleCalendarAutomationCommand automationCommand)
    {
        if (automationCommand == null)
        {
            throw new ArgumentNullException(nameof(automationCommand));
        }

        mAutomationCommand = automationCommand;
    }

    public async Task<IReadOnlyList<AppleCalendarDescriptor>> GetCalendarsAsync(
        PlanName requestedDestinationName,
        CancellationToken cancellationToken)
    {
        if (requestedDestinationName == null)
        {
            throw new ArgumentNullException(nameof(requestedDestinationName));
        }

        ensureAvailable();
        AppleCalendarAutomationRequest request = AppleCalendarAutomationRequest.CreateListRequest(requestedDestinationName);
        AppleCalendarAutomationResponse response = await executeAsync(
                EAppleCalendarAutomationOperation.ListCalendars,
                request,
                cancellationToken)
            .ConfigureAwait(false);
        ensureSuccess(response);
        if (response.Calendars == null)
        {
            throw createOperationFailure(
                "apple_calendar_automation_calendars_missing",
                null);
        }

        List<AppleCalendarDescriptor> calendars = new List<AppleCalendarDescriptor>(response.Calendars.Count);
        foreach (AppleCalendarAutomationCalendarResponse calendarResponse
            in response.Calendars)
        {
            calendars.Add(createDescriptor(calendarResponse));
        }

        return calendars.AsReadOnly();
    }

    public async Task<AppleCalendarNativeExportResult> ApplyExportAsync(
        AppleCalendarExportMutation mutation,
        CancellationToken cancellationToken)
    {
        if (mutation == null)
        {
            throw new ArgumentNullException(nameof(mutation));
        }

        ensureAvailable();
        AppleCalendarAutomationRequest request = AppleCalendarAutomationRequest.CreateMutationRequest(mutation);
        AppleCalendarAutomationResponse response = await executeAsync(
                EAppleCalendarAutomationOperation.ApplyExport,
                request,
                cancellationToken)
            .ConfigureAwait(false);
        ensureSuccess(response);
        if (string.IsNullOrWhiteSpace(response.CalendarId)
            || string.IsNullOrWhiteSpace(response.CalendarName))
        {
            throw createOperationFailure(
                "apple_calendar_automation_export_result_missing",
                null);
        }

        if (response.CreatedEventCount != request.Events.Count
            || response.DeletedEventCount < 0
            || (mutation.Kind == EAppleCalendarExportMutationKind.CreateNew
                && response.DeletedEventCount != 0))
        {
            throw createOperationFailure(
                "apple_calendar_automation_event_counts_invalid",
                null);
        }

        try
        {
            return new AppleCalendarNativeExportResult(
                new AppleCalendarId(response.CalendarId),
                new PlanName(response.CalendarName),
                response.CreatedEventCount,
                response.DeletedEventCount);
        }
        catch (ArgumentException exception)
        {
            throw createOperationFailure(
                "apple_calendar_automation_export_result_invalid",
                exception);
        }
    }

    private async Task<AppleCalendarAutomationResponse> executeAsync(
        EAppleCalendarAutomationOperation operation,
        AppleCalendarAutomationRequest request,
        CancellationToken cancellationToken)
    {
        string requestJson = JsonSerializer.Serialize(request, sJsonOptions);
        string responseJson = await mAutomationCommand.ExecuteAsync(
                operation,
                requestJson,
                cancellationToken)
            .ConfigureAwait(false);
        try
        {
            AppleCalendarAutomationResponse? responseOrNull = JsonSerializer.Deserialize<AppleCalendarAutomationResponse>(responseJson, sJsonOptions);
            if (responseOrNull == null)
            {
                throw new JsonException(
                    "Apple Calendar automation returned a null response.");
            }

            return responseOrNull;
        }
        catch (JsonException exception)
        {
            throw createOperationFailure(
                "apple_calendar_automation_response_invalid",
                exception);
        }
    }

    private void ensureAvailable()
    {
        if (IsAvailable == false)
        {
            throw new AppleCalendarNativeBridgeException(
                EAppleCalendarNativeFailureKind.Unavailable,
                "apple_calendar_automation_unavailable");
        }
    }

    private static AppleCalendarDescriptor createDescriptor(
        AppleCalendarAutomationCalendarResponse calendarResponse)
    {
        if (calendarResponse == null)
        {
            throw createOperationFailure(
                "apple_calendar_automation_calendar_invalid",
                null);
        }

        try
        {
            string calendarId = calendarResponse.Id == null ? string.Empty : calendarResponse.Id;
            string calendarName = calendarResponse.Name == null ? string.Empty : calendarResponse.Name;
            PlanId? managedPlanIdOrNull = tryParsePlanIdOrNull(calendarResponse.ManagedPlanId);
            EAppleCalendarContentAccess contentAccess = calendarResponse.Writable
                ? EAppleCalendarContentAccess.Writable
                : EAppleCalendarContentAccess.ReadOnly;
            return new AppleCalendarDescriptor(
                new AppleCalendarId(calendarId),
                calendarName,
                managedPlanIdOrNull,
                contentAccess);
        }
        catch (ArgumentException exception)
        {
            throw createOperationFailure(
                "apple_calendar_automation_calendar_invalid",
                exception);
        }
    }

    private static PlanId? tryParsePlanIdOrNull(string? planIdOrNull)
    {
        if (planIdOrNull == null)
        {
            return null;
        }

        Guid planIdValue;
        if (Guid.TryParseExact(
                planIdOrNull,
                "D",
                out planIdValue) == false
            || planIdValue == Guid.Empty)
        {
            throw createOperationFailure(
                "apple_calendar_automation_calendar_invalid",
                null);
        }

        return new PlanId(planIdValue);
    }

    private static void ensureSuccess(
        AppleCalendarAutomationResponse response)
    {
        switch (response.Status)
        {
            case "ok":
                return;
            case "access_denied":
                throw new AppleCalendarNativeBridgeException(
                    EAppleCalendarNativeFailureKind.AccessDenied,
                    "apple_calendar_automation_access_denied");
            case "calendar_changed":
                throw new AppleCalendarNativeBridgeException(
                    EAppleCalendarNativeFailureKind.CalendarChanged,
                    "apple_calendar_destination_changed");
            case "operation_failed":
                throw createOperationFailure(
                    "apple_calendar_automation_operation_failed",
                    null);
            default:
                throw createOperationFailure(
                    "apple_calendar_automation_status_invalid",
                    null);
        }
    }

    private static JsonSerializerOptions createJsonOptions()
    {
        return new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = false,
        };
    }

    private static AppleCalendarNativeBridgeException createOperationFailure(
        string diagnosticCode,
        Exception? innerExceptionOrNull)
    {
        return new AppleCalendarNativeBridgeException(
            EAppleCalendarNativeFailureKind.OperationFailed,
            diagnosticCode,
            innerExceptionOrNull);
    }
}
