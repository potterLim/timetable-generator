using System;
using System.Collections.Generic;
using System.Linq;

using TimetableGenerator.Domain.Planning;

namespace TimetableGenerator.Desktop.Exporting.AppleCalendar;

internal sealed partial class EventKitAppleCalendarNativeBridge
{
    private static IReadOnlyList<AppleCalendarDescriptor> createCalendarDescriptors(EventKitAppleCalendarResponse response, AppleCalendarOwnershipRegistryDocument registry)
    {
        if (response.Calendars == null)
        {
            throw invalidResponse();
        }

        Dictionary<string, AppleCalendarRegistration> registrations = registry.Calendars.ToDictionary(registration => registration.CalendarIdentifier, StringComparer.Ordinal);
        List<AppleCalendarDescriptor> descriptors = new List<AppleCalendarDescriptor>(response.Calendars.Count);
        HashSet<string> identifiers = new HashSet<string>(StringComparer.Ordinal);
        try
        {
            foreach (EventKitAppleCalendarDescriptorResponse? calendarOrNull in response.Calendars)
            {
                if (calendarOrNull == null
                    || string.IsNullOrWhiteSpace(calendarOrNull.Identifier)
                    || string.IsNullOrWhiteSpace(calendarOrNull.Name)
                    || string.IsNullOrWhiteSpace(calendarOrNull.SourceIdentifier)
                    || identifiers.Add(calendarOrNull.Identifier) == false)
                {
                    throw invalidResponse();
                }

                PlanId? registeredPlanIdOrNull = parseOptionalPlanId(calendarOrNull.RegisteredPlanId);
                PlanId? legacyPlanIdOrNull = parseOptionalPlanId(calendarOrNull.LegacyPlanId);
                if ((calendarOrNull.LegacyManaged && legacyPlanIdOrNull == null)
                    || (calendarOrNull.LegacyManaged == false && legacyPlanIdOrNull != null)
                    || (registeredPlanIdOrNull != null && legacyPlanIdOrNull != null && registeredPlanIdOrNull != legacyPlanIdOrNull))
                {
                    throw invalidResponse();
                }

                AppleCalendarRegistration? registrationOrNull;
                registrations.TryGetValue(calendarOrNull.Identifier, out registrationOrNull);
                if ((registrationOrNull == null) != (registeredPlanIdOrNull == null)
                    || (registrationOrNull != null && registrationOrNull.GetPlanId() != registeredPlanIdOrNull)
                    || (registrationOrNull != null && string.Equals(registrationOrNull.SourceIdentifier, calendarOrNull.SourceIdentifier, StringComparison.Ordinal) == false))
                {
                    throw invalidResponse();
                }

                PlanId? managedPlanIdOrNull = registeredPlanIdOrNull;
                if (managedPlanIdOrNull == null)
                {
                    managedPlanIdOrNull = legacyPlanIdOrNull;
                }

                EAppleCalendarContentAccess contentAccess = calendarOrNull.Writable ? EAppleCalendarContentAccess.Writable : EAppleCalendarContentAccess.ReadOnly;
                descriptors.Add(new AppleCalendarDescriptor(new AppleCalendarId(calendarOrNull.Identifier), calendarOrNull.Name, calendarOrNull.SourceIdentifier, managedPlanIdOrNull, contentAccess));
            }
        }
        catch (AppleCalendarNativeBridgeException)
        {
            throw;
        }
        catch (Exception exception) when (exception is ArgumentException || exception is FormatException || exception is OverflowException)
        {
            throw invalidResponse(exception);
        }

        return descriptors.AsReadOnly();
    }

    private AppleCalendarOwnershipRegistryDocument applyRegistrationBindings(
        EventKitAppleCalendarResponse response,
        AppleCalendarOwnershipRegistryDocument registry)
    {
        IReadOnlyList<EventKitAppleCalendarRegistrationBindingResponse> bindings = response.RegistrationBindings == null ? Array.Empty<EventKitAppleCalendarRegistrationBindingResponse>() : response.RegistrationBindings;
        if (bindings.Count == 0 && response.Calendars == null)
        {
            return registry;
        }

        bool successful = string.Equals(response.Status, "ok", StringComparison.Ordinal);
        bool notFound = string.Equals(response.Status, "not_found", StringComparison.Ordinal);
        if (response.SchemaVersion != EventKitAppleCalendarRequest.CURRENT_SCHEMA_VERSION
            || (response.Calendars != null && successful == false)
            || (bindings.Count > 0 && successful == false && notFound == false))
        {
            throw invalidResponse();
        }

        HashSet<string> previousCalendarIdentifiers = new HashSet<string>(StringComparer.Ordinal);
        HashSet<string> currentCalendarIdentifiers = new HashSet<string>(StringComparer.Ordinal);
        AppleCalendarOwnershipRegistryDocument reboundRegistry = registry;
        bool removedMissingRegistration = false;
        try
        {
            foreach (EventKitAppleCalendarRegistrationBindingResponse? bindingOrNull in bindings)
            {
                if (bindingOrNull == null
                    || string.IsNullOrWhiteSpace(bindingOrNull.PreviousCalendarIdentifier)
                    || string.IsNullOrWhiteSpace(bindingOrNull.CalendarIdentifier)
                    || string.IsNullOrWhiteSpace(bindingOrNull.CalendarName)
                    || string.IsNullOrWhiteSpace(bindingOrNull.SourceIdentifier)
                    || string.IsNullOrWhiteSpace(bindingOrNull.PlanId)
                    || bindingOrNull.Events == null
                    || previousCalendarIdentifiers.Add(bindingOrNull.PreviousCalendarIdentifier) == false
                    || currentCalendarIdentifiers.Add(bindingOrNull.CalendarIdentifier) == false)
                {
                    throw invalidResponse();
                }

                AppleCalendarRegistration? previousRegistrationOrNull = reboundRegistry.Calendars.SingleOrDefault(registration => string.Equals(registration.CalendarIdentifier, bindingOrNull.PreviousCalendarIdentifier, StringComparison.Ordinal));
                if (previousRegistrationOrNull == null)
                {
                    throw invalidResponse();
                }

                AppleCalendarRegistration previousRegistration = previousRegistrationOrNull;
                if (string.Equals(previousRegistration.PlanId, bindingOrNull.PlanId, StringComparison.Ordinal) == false
                    || string.Equals(previousRegistration.SourceIdentifier, bindingOrNull.SourceIdentifier, StringComparison.Ordinal) == false
                    || string.Equals(previousRegistration.NormalizedCalendarName, EventKitAppleCalendarRequest.NormalizeCalendarName(bindingOrNull.CalendarName), StringComparison.Ordinal) == false
                    || bindingOrNull.Events.Count != previousRegistration.Events.Count)
                {
                    throw invalidResponse();
                }

                validateRegistrationBindingCalendarResponse(response.Calendars, bindingOrNull);
                IReadOnlyList<AppleCalendarManagedEventRegistration> reboundEvents = validateRegistrationBindingEvents(bindingOrNull.Events, previousRegistration.Events);
                AppleCalendarRegistration reboundRegistration = new AppleCalendarRegistration(
                    previousRegistration.PlanId,
                    bindingOrNull.CalendarIdentifier,
                    bindingOrNull.CalendarName,
                    EventKitAppleCalendarRequest.NormalizeCalendarName(bindingOrNull.CalendarName),
                    bindingOrNull.SourceIdentifier,
                    previousRegistration.TermStartsAtUnixSeconds,
                    previousRegistration.TermEndsAtUnixSeconds,
                    reboundEvents);
                reboundRegistry = reboundRegistry.RebindCalendar(bindingOrNull.PreviousCalendarIdentifier, reboundRegistration);
            }

            if (response.Calendars != null)
            {
                reboundRegistry = removeMissingRegistrationsFromSnapshot(response.Calendars, reboundRegistry, out removedMissingRegistration);
                _ = createCalendarDescriptors(response, reboundRegistry);
            }
        }
        catch (AppleCalendarNativeBridgeException)
        {
            throw;
        }
        catch (Exception exception) when (exception is ArgumentException || exception is InvalidOperationException || exception is AppleCalendarOwnershipRegistryException)
        {
            throw invalidResponse(exception);
        }

        if (bindings.Count > 0 || removedMissingRegistration)
        {
            string diagnosticCode = bindings.Count > 0 ? "apple_calendar_registry_rebind_failed" : "apple_calendar_registry_cleanup_failed";
            saveRegistry(reboundRegistry, diagnosticCode);
        }
        return reboundRegistry;
    }

    private static AppleCalendarOwnershipRegistryDocument removeMissingRegistrationsFromSnapshot(
        IReadOnlyList<EventKitAppleCalendarDescriptorResponse> calendars,
        AppleCalendarOwnershipRegistryDocument registry,
        out bool removedRegistration)
    {
        Dictionary<string, EventKitAppleCalendarDescriptorResponse> calendarsByIdentifier = new Dictionary<string, EventKitAppleCalendarDescriptorResponse>(StringComparer.Ordinal);
        foreach (EventKitAppleCalendarDescriptorResponse? calendarOrNull in calendars)
        {
            if (calendarOrNull == null
                || string.IsNullOrWhiteSpace(calendarOrNull.Identifier)
                || string.IsNullOrWhiteSpace(calendarOrNull.Name)
                || string.IsNullOrWhiteSpace(calendarOrNull.SourceIdentifier)
                || calendarsByIdentifier.TryAdd(calendarOrNull.Identifier, calendarOrNull) == false)
            {
                throw invalidResponse();
            }
        }

        removedRegistration = false;
        AppleCalendarOwnershipRegistryDocument cleanedRegistry = registry;
        foreach (AppleCalendarRegistration registration in registry.Calendars)
        {
            if (calendarsByIdentifier.ContainsKey(registration.CalendarIdentifier))
            {
                continue;
            }

            bool hasRebindingCandidate = calendarsByIdentifier.Values.Any(
                calendar => string.Equals(calendar.SourceIdentifier, registration.SourceIdentifier, StringComparison.Ordinal)
                    && string.Equals(EventKitAppleCalendarRequest.NormalizeCalendarName(calendar.Name!), registration.NormalizedCalendarName, StringComparison.Ordinal));
            if (hasRebindingCandidate)
            {
                throw invalidResponse();
            }

            cleanedRegistry = cleanedRegistry.RemoveMissingCalendar(registration.CalendarIdentifier);
            removedRegistration = true;
        }

        return cleanedRegistry;
    }

    private static void validateRegistrationBindingCalendarResponse(
        IReadOnlyList<EventKitAppleCalendarDescriptorResponse>? calendarsOrNull,
        EventKitAppleCalendarRegistrationBindingResponse binding)
    {
        if (calendarsOrNull == null)
        {
            return;
        }

        EventKitAppleCalendarDescriptorResponse? reboundCalendarOrNull = null;
        bool calendarIdentifierChanged = string.Equals(binding.PreviousCalendarIdentifier, binding.CalendarIdentifier, StringComparison.Ordinal) == false;
        foreach (EventKitAppleCalendarDescriptorResponse? calendarOrNull in calendarsOrNull)
        {
            if (calendarIdentifierChanged
                && calendarOrNull != null
                && string.Equals(calendarOrNull.Identifier, binding.PreviousCalendarIdentifier, StringComparison.Ordinal))
            {
                throw invalidResponse();
            }

            if (calendarOrNull != null && string.Equals(calendarOrNull.Identifier, binding.CalendarIdentifier, StringComparison.Ordinal))
            {
                if (reboundCalendarOrNull != null)
                {
                    throw invalidResponse();
                }
                reboundCalendarOrNull = calendarOrNull;
            }
        }

        if (reboundCalendarOrNull == null
            || reboundCalendarOrNull.Writable == false
            || string.Equals(reboundCalendarOrNull.Name, binding.CalendarName, StringComparison.Ordinal) == false
            || string.Equals(reboundCalendarOrNull.SourceIdentifier, binding.SourceIdentifier, StringComparison.Ordinal) == false
            || string.Equals(reboundCalendarOrNull.RegisteredPlanId, binding.PlanId, StringComparison.Ordinal) == false)
        {
            throw invalidResponse();
        }
    }

    private static IReadOnlyList<AppleCalendarManagedEventRegistration> validateRegistrationBindingEvents(
        IReadOnlyList<EventKitAppleCalendarEventResponse> bindingEvents,
        IReadOnlyList<AppleCalendarManagedEventRegistration> previousEvents)
    {
        Dictionary<string, AppleCalendarManagedEventRegistration> previousEventsBySourceHash = previousEvents.ToDictionary(managedEvent => managedEvent.SourceEventHash, StringComparer.Ordinal);
        HashSet<string> calendarItemIdentifiers = new HashSet<string>(StringComparer.Ordinal);
        List<AppleCalendarManagedEventRegistration> reboundEvents = new List<AppleCalendarManagedEventRegistration>(bindingEvents.Count);
        foreach (EventKitAppleCalendarEventResponse? eventOrNull in bindingEvents)
        {
            if (eventOrNull == null
                || string.IsNullOrWhiteSpace(eventOrNull.SourceEventHash)
                || string.IsNullOrWhiteSpace(eventOrNull.CalendarItemIdentifier)
                || string.IsNullOrWhiteSpace(eventOrNull.Fingerprint)
                || calendarItemIdentifiers.Add(eventOrNull.CalendarItemIdentifier) == false)
            {
                throw invalidResponse();
            }

            AppleCalendarManagedEventRegistration? previousEventOrNull;
            if (previousEventsBySourceHash.Remove(eventOrNull.SourceEventHash, out previousEventOrNull) == false
                || string.Equals(previousEventOrNull.Fingerprint, eventOrNull.Fingerprint, StringComparison.Ordinal) == false)
            {
                throw invalidResponse();
            }

            reboundEvents.Add(new AppleCalendarManagedEventRegistration(eventOrNull.SourceEventHash, eventOrNull.CalendarItemIdentifier, eventOrNull.ExternalIdentifier, eventOrNull.Fingerprint));
        }

        if (previousEventsBySourceHash.Count != 0)
        {
            throw invalidResponse();
        }

        return reboundEvents.AsReadOnly();
    }
}
