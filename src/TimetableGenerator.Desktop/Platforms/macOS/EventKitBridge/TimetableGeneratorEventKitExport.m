#import <EventKit/EventKit.h>
#import <Foundation/Foundation.h>

#include <assert.h>

#import "TimetableGeneratorEventKitEventIdentity.h"
#import "TimetableGeneratorEventKitExport.h"
#import "TimetableGeneratorEventKitProtocol.h"
#import "TimetableGeneratorEventKitRegistration.h"

static BOOL tg_stage_event_removal(EKEventStore* const event_store, EKEvent* const event, NSError** const out_error)
{
    assert(event_store != NULL);
    assert(event != NULL);
    assert(out_error != NULL);

    EKSpan span;
    if (event.hasRecurrenceRules) {
        span = EKSpanFutureEvents;
    } else {
        span = EKSpanThisEvent;
    }
    return [event_store removeEvent:event span:span commit:NO error:out_error];
}

static EKEvent* tg_stage_recurring_event_or_null(
    EKEventStore* const event_store,
    EKCalendar* const calendar,
    NSDictionary* const event_request,
    NSError** const out_error)
{
    assert(event_store != NULL);
    assert(calendar != NULL);
    assert(event_request != NULL);
    assert(out_error != NULL);

    EKEvent* const event = [EKEvent eventWithEventStore:event_store];
    event.calendar = calendar;
    event.title = event_request[@"summary"];
    event.location = event_request[@"location"];
    event.notes = event_request[@"notes"];
    event.startDate = tg_get_date_from_unix_seconds([event_request[@"startsAtUnixSeconds"] longLongValue]);
    event.endDate = tg_get_date_from_unix_seconds([event_request[@"endsAtUnixSeconds"] longLongValue]);
    event.timeZone = [NSTimeZone timeZoneWithName:event_request[@"timeZoneIdentifier"]];
    event.URL = nil;

    NSMutableArray<EKRecurrenceDayOfWeek*>* const recurrence_weekdays = [NSMutableArray array];
    for (NSNumber* weekday in event_request[@"weekdays"]) {
        [recurrence_weekdays addObject:[EKRecurrenceDayOfWeek dayOfWeek:(EKWeekday)weekday.integerValue]];
    }
    EKRecurrenceEnd* const recurrence_end = [EKRecurrenceEnd recurrenceEndWithEndDate:tg_get_date_from_unix_seconds([event_request[@"recurrenceEndsAtUnixSeconds"] longLongValue])];
    EKRecurrenceRule* const recurrence_rule = [[EKRecurrenceRule alloc]
        initRecurrenceWithFrequency:EKRecurrenceFrequencyWeekly
                           interval:TG_WEEKLY_RECURRENCE_INTERVAL
                      daysOfTheWeek:recurrence_weekdays
                     daysOfTheMonth:nil
                    monthsOfTheYear:nil
                     weeksOfTheYear:nil
                      daysOfTheYear:nil
                       setPositions:nil
                                end:recurrence_end];
    [event addRecurrenceRule:recurrence_rule];

    if (![tg_get_fingerprint_for_event_or_null(event) isEqualToString:event_request[@"fingerprint"]]) {
        return nil;
    }
    const BOOL did_save_event = [event_store saveEvent:event span:EKSpanThisEvent commit:NO error:out_error];
    if (did_save_event) {
        return event;
    }

    return nil;
}

static NSDictionary* tg_create_calendar_event_result(
    EKCalendar* const calendar,
    NSArray<NSDictionary*>* const event_requests,
    NSArray<EKEvent*>* const events,
    const NSUInteger deleted_event_count)
{
    assert(calendar != NULL);
    assert(event_requests != NULL);
    assert(events != NULL);

    NSString* const calendar_identifier = tg_get_calendar_identifier(calendar);
    NSString* const source_identifier = tg_get_calendar_source_identifier(calendar);
    if (calendar_identifier.length == 0 || source_identifier.length == 0 || event_requests.count != events.count) {
        return tg_create_response(TG_STATUS_OPERATION_FAILED, @"eventkit_calendar_result_invalid");
    }

    NSMutableArray<NSDictionary*>* const event_responses = [NSMutableArray arrayWithCapacity:events.count];
    for (NSUInteger index = 0; index < events.count; ++index) {
        NSDictionary* const event_request = event_requests[index];
        EKEvent* const event = events[index];
        NSString* const calendar_item_identifier_or_null = event.calendarItemIdentifier;
        if (calendar_item_identifier_or_null.length == 0) {
            return tg_create_response(TG_STATUS_OPERATION_FAILED, @"eventkit_calendar_event_identifier_missing");
        }

        NSString* external_identifier = event.calendarItemExternalIdentifier;
        if (external_identifier == nil) {
            external_identifier = @"";
        }
        [event_responses addObject:@{
            @"sourceEventHash" : event_request[@"sourceEventHash"],
            @"calendarItemIdentifier" : calendar_item_identifier_or_null,
            @"externalIdentifier" : external_identifier,
            @"fingerprint" : event_request[@"fingerprint"]
        }];
    }

    NSString* calendar_name = calendar.title;
    if (calendar_name == nil) {
        calendar_name = @"";
    }
    return @{
        @"schemaVersion" : @(TG_SCHEMA_VERSION),
        @"status" : TG_STATUS_OK,
        @"diagnosticCode" : @"",
        @"calendarIdentifier" : calendar_identifier,
        @"calendarName" : calendar_name,
        @"sourceIdentifier" : source_identifier,
        @"createdEventCount" : @(event_responses.count),
        @"deletedEventCount" : @(deleted_event_count),
        @"events" : event_responses
    };
}

static NSDictionary* tg_create_response_by_adding_registration_binding(NSDictionary* const response, NSDictionary* const binding_or_null)
{
    assert(response != NULL);

    if (binding_or_null == nil) {
        return response;
    }

    NSMutableDictionary* const response_with_binding = [response mutableCopy];
    response_with_binding[@"registrationBindings"] = @[ binding_or_null ];
    return response_with_binding;
}

NSDictionary* tg_reconcile_export(NSDictionary* const request, EKEventStore* const event_store)
{
    assert(request != NULL);
    assert(event_store != NULL);

    const long long prepared_at_unix_seconds = tg_get_required_integer(request, @"preparedAtUnixSeconds");
    if (prepared_at_unix_seconds <= 0) {
        tg_throw_invalid_request(@"eventkit_request_prepared_at_invalid");
    }

    NSString* const mutation_kind = tg_get_required_string(request, @"mutationKind");
    if (![mutation_kind isEqualToString:@"create"] && ![mutation_kind isEqualToString:@"replace"]) {
        tg_throw_invalid_request(@"eventkit_request_mutation_kind_invalid");
    }

    NSString* const destination_name = tg_get_required_string(request, @"destinationName");
    NSString* const normalized_destination_name = tg_get_required_string(request, @"normalizedDestinationName");
    if (![tg_normalize_calendar_name(destination_name) isEqualToString:normalized_destination_name]) {
        tg_throw_invalid_request(@"eventkit_request_destination_name_invalid");
    }

    NSString* const plan_identifier = tg_get_required_plan_identifier(request, @"planId");
    NSString* registered_plan_identifier = tg_get_optional_string(request, @"registeredPlanId");
    if (registered_plan_identifier.length > 0) {
        if (!tg_is_nonempty_uuid(registered_plan_identifier)) {
            tg_throw_invalid_request(@"eventkit_request_registered_plan_id_invalid");
        }
        registered_plan_identifier = [[[NSUUID alloc] initWithUUIDString:registered_plan_identifier] UUIDString].lowercaseString;
    }

    NSString* const existing_calendar_identifier = tg_get_optional_string(request, @"existingCalendarIdentifier");
    NSString* const expected_source_identifier = tg_get_optional_string(request, @"expectedSourceIdentifier");
    const long long term_starts_at_unix_seconds = tg_get_required_integer(request, @"termStartsAtUnixSeconds");
    const long long term_ends_at_unix_seconds = tg_get_required_integer(request, @"termEndsAtUnixSeconds");
    const long long migration_starts_at_unix_seconds = tg_get_required_integer(request, @"migrationStartsAtUnixSeconds");
    const long long migration_ends_at_unix_seconds = tg_get_required_integer(request, @"migrationEndsAtUnixSeconds");
    tg_validate_legacy_migration_range(term_starts_at_unix_seconds, term_ends_at_unix_seconds, migration_starts_at_unix_seconds, migration_ends_at_unix_seconds);
    NSArray<NSDictionary*>* const desired_event_requests = tg_validate_desired_events(request);
    NSArray<NSDictionary*>* const managed_events = tg_validate_managed_events(request);
    const BOOL replacing = [mutation_kind isEqualToString:@"replace"];
    const BOOL registered_reconciliation = replacing && managed_events.count > 0 && [registered_plan_identifier isEqualToString:plan_identifier];
    EKCalendar* calendar;
    NSDictionary* registration_binding = nil;
    NSArray<EKEvent*>* rebound_registered_events = nil;

    if (replacing) {
        const BOOL legacy_reconciliation = managed_events.count == 0 && registered_plan_identifier.length == 0;
        if (existing_calendar_identifier.length == 0 || expected_source_identifier.length == 0 || (!registered_reconciliation && !legacy_reconciliation)) {
            tg_throw_invalid_request(@"eventkit_request_reconciliation_precondition_invalid");
        }

        calendar = tg_find_calendar_or_null(event_store, existing_calendar_identifier);
        if (calendar == nil) {
            if (!registered_reconciliation) {
                return tg_create_response(TG_STATUS_NOT_FOUND, @"eventkit_reconciliation_calendar_not_found");
            }

            NSDictionary* const registration = @{
                @"planId" : registered_plan_identifier,
                @"calendarIdentifier" : existing_calendar_identifier,
                @"calendarName" : destination_name,
                @"normalizedCalendarName" : normalized_destination_name,
                @"sourceIdentifier" : expected_source_identifier,
                @"termStartsAtUnixSeconds" : @(term_starts_at_unix_seconds),
                @"termEndsAtUnixSeconds" : @(term_ends_at_unix_seconds),
                @"managedEvents" : managed_events
            };
            NSUInteger rebound_candidate_count;
            NSDictionary* const rebound_registration = tg_resolve_rebound_registration_or_null(
                event_store,
                [event_store calendarsForEntityType:EKEntityTypeEvent],
                registration,
                &rebound_candidate_count);
            if (rebound_registration == nil) {
                calendar = tg_resolve_pending_committed_calendar_after_identifier_change_or_null(
                    event_store,
                    [event_store calendarsForEntityType:EKEntityTypeEvent],
                    normalized_destination_name,
                    expected_source_identifier,
                    desired_event_requests,
                    term_starts_at_unix_seconds,
                    term_ends_at_unix_seconds,
                    prepared_at_unix_seconds);
                if (calendar == nil) {
                    return tg_create_response(TG_STATUS_CALENDAR_CHANGED, @"eventkit_reconciliation_identifier_changed");
                }
            } else {
                calendar = rebound_registration[@"calendar"];
                registration_binding = rebound_registration[@"binding"];
                rebound_registered_events = rebound_registration[@"events"];
            }
        }
        NSString* calendar_title = calendar.title;
        if (calendar_title == nil) {
            calendar_title = @"";
        }
        const BOOL has_expected_name = [tg_normalize_calendar_name(calendar_title) isEqualToString:normalized_destination_name];
        const BOOL has_expected_source = [tg_get_calendar_source_identifier(calendar) isEqualToString:expected_source_identifier];
        if (!calendar.allowsContentModifications || !has_expected_name || !has_expected_source) {
            return tg_create_response(TG_STATUS_CALENDAR_CHANGED, @"eventkit_calendar_destination_changed");
        }
    } else {
        const BOOL has_existing_state = existing_calendar_identifier.length > 0 || expected_source_identifier.length > 0 || registered_plan_identifier.length > 0 || managed_events.count > 0;
        if (has_existing_state) {
            tg_throw_invalid_request(@"eventkit_request_reconciliation_precondition_invalid");
        }

        NSMutableArray<EKCalendar*>* const matching_calendars = [NSMutableArray array];
        for (EKCalendar* candidate in [event_store calendarsForEntityType:EKEntityTypeEvent]) {
            NSString* candidate_title = candidate.title;
            if (candidate_title == nil) {
                candidate_title = @"";
            }
            if ([tg_normalize_calendar_name(candidate_title) isEqualToString:normalized_destination_name]) {
                [matching_calendars addObject:candidate];
            }
        }
        if (matching_calendars.count == 0) {
            if (tg_does_any_calendar_contain_recent_desired_candidate(
                    event_store,
                    desired_event_requests,
                    term_starts_at_unix_seconds,
                    term_ends_at_unix_seconds,
                    prepared_at_unix_seconds)) {
                return tg_create_response(TG_STATUS_CALENDAR_CHANGED, @"eventkit_reconciliation_ambiguous");
            }
            return tg_create_response(TG_STATUS_NOT_FOUND, @"eventkit_reconciliation_not_found");
        }
        if (matching_calendars.count != 1) {
            return tg_create_response(TG_STATUS_CALENDAR_CHANGED, @"eventkit_calendar_destination_changed");
        }

        calendar = matching_calendars[0];
        if (!calendar.allowsContentModifications || tg_get_calendar_source_identifier(calendar).length == 0) {
            return tg_create_response(TG_STATUS_CALENDAR_CHANGED, @"eventkit_calendar_destination_changed");
        }
    }

    NSArray<EKEvent*>* const calendar_events = tg_get_events_in_calendar(event_store, calendar, term_starts_at_unix_seconds, term_ends_at_unix_seconds);
    NSDictionary<NSString*, NSDictionary<NSString*, EKEvent*>*>* const fingerprint_index = tg_index_events_by_fingerprint(calendar_events);
    NSArray<EKEvent*>* const desired_events = tg_resolve_unique_fingerprint_events_or_null(desired_event_requests, fingerprint_index);
    if (!replacing) {
        const BOOL has_recent_desired_events = desired_events != nil && tg_are_events_created_near(desired_events, prepared_at_unix_seconds);
        const BOOL contains_only_desired_series = desired_events != nil && tg_does_calendar_contain_only_resolved_series(calendar_events, desired_events);
        if (!has_recent_desired_events || !contains_only_desired_series) {
            return tg_create_response(TG_STATUS_CALENDAR_CHANGED, @"eventkit_reconciliation_ambiguous");
        }
        return tg_create_calendar_event_result(calendar, desired_event_requests, desired_events, 0);
    }

    if (!registered_reconciliation) {
        NSArray<EKEvent*>* const legacy_events = tg_get_managed_legacy_events(event_store, calendar, plan_identifier, migration_starts_at_unix_seconds, migration_ends_at_unix_seconds);
        const BOOL desired_state_is_complete = desired_events != nil && tg_are_events_created_near(desired_events, prepared_at_unix_seconds);
        if (desired_state_is_complete && legacy_events.count == 0) {
            return tg_create_calendar_event_result(calendar, desired_event_requests, desired_events, 0);
        }
        if (!desired_state_is_complete && legacy_events.count > 0) {
            NSSet<NSString*>* const legacy_identifiers = tg_get_event_identifier_set_or_null(legacy_events);
            const BOOL has_candidate_outside_legacy_events = legacy_identifiers != nil && tg_does_index_contain_candidate_outside_identifiers(desired_event_requests, fingerprint_index, legacy_identifiers);
            if (legacy_identifiers != nil && !has_candidate_outside_legacy_events) {
                return tg_create_response(TG_STATUS_NOT_FOUND, @"eventkit_reconciliation_not_found");
            }
        }
        return tg_create_response(TG_STATUS_CALENDAR_CHANGED, @"eventkit_reconciliation_ambiguous");
    }

    NSArray<EKEvent*>* registered_events = rebound_registered_events;
    if (registered_events == nil) {
        registered_events = tg_resolve_registered_events_or_null(event_store, calendar, managed_events, term_starts_at_unix_seconds, term_ends_at_unix_seconds);
    }
    if (registration_binding == nil && registered_events != nil && !tg_does_registration_match_resolved_events(managed_events, registered_events)) {
        NSDictionary* const registration = @{
            @"planId" : registered_plan_identifier,
            @"calendarIdentifier" : existing_calendar_identifier,
            @"calendarName" : destination_name,
            @"normalizedCalendarName" : normalized_destination_name,
            @"sourceIdentifier" : expected_source_identifier,
            @"termStartsAtUnixSeconds" : @(term_starts_at_unix_seconds),
            @"termEndsAtUnixSeconds" : @(term_ends_at_unix_seconds),
            @"managedEvents" : managed_events
        };
        registration_binding = tg_create_registration_binding_or_null(registration, calendar, registered_events);
        if (registration_binding == nil) {
            return tg_create_response(TG_STATUS_CALENDAR_CHANGED, @"eventkit_reconciliation_ambiguous");
        }
    }
    if (desired_events == nil || !tg_are_events_created_near(desired_events, prepared_at_unix_seconds)) {
        if (registered_events != nil) {
            NSSet<NSString*>* const registered_identifiers = tg_get_event_identifier_set_or_null(registered_events);
            const BOOL has_candidate_outside_registration = registered_identifiers != nil && tg_does_index_contain_candidate_outside_identifiers(desired_event_requests, fingerprint_index, registered_identifiers);
            if (registered_identifiers != nil && !has_candidate_outside_registration) {
                return tg_create_response_by_adding_registration_binding(tg_create_response(TG_STATUS_NOT_FOUND, @"eventkit_reconciliation_not_found"), registration_binding);
            }
        }
        return tg_create_response(TG_STATUS_CALENDAR_CHANGED, @"eventkit_reconciliation_ambiguous");
    }

    NSSet<NSString*>* const desired_identifiers = tg_get_event_identifier_set_or_null(desired_events);
    NSSet<NSString*>* const registered_candidate_identifiers = tg_get_registered_event_candidate_identifiers(event_store, calendar, managed_events);
    const BOOL has_candidate_outside_desired_events = desired_identifiers != nil && tg_does_index_contain_candidate_outside_identifiers(managed_events, fingerprint_index, desired_identifiers);
    if (desired_identifiers == nil || ![registered_candidate_identifiers isSubsetOfSet:desired_identifiers] || has_candidate_outside_desired_events) {
        return tg_create_response(TG_STATUS_CALENDAR_CHANGED, @"eventkit_reconciliation_ambiguous");
    }
    return tg_create_response_by_adding_registration_binding(tg_create_calendar_event_result(calendar, desired_event_requests, desired_events, 0), registration_binding);
}

NSDictionary* tg_apply_export(NSDictionary* const request, EKEventStore* const event_store)
{
    assert(request != NULL);
    assert(event_store != NULL);

    NSString* const mutation_kind = tg_get_required_string(request, @"mutationKind");
    if (![mutation_kind isEqualToString:@"create"] && ![mutation_kind isEqualToString:@"replace"]) {
        tg_throw_invalid_request(@"eventkit_request_mutation_kind_invalid");
    }

    NSString* const destination_name = tg_get_required_string(request, @"destinationName");
    NSString* const normalized_destination_name = tg_get_required_string(request, @"normalizedDestinationName");
    if (![tg_normalize_calendar_name(destination_name) isEqualToString:normalized_destination_name]) {
        tg_throw_invalid_request(@"eventkit_request_destination_name_invalid");
    }

    NSString* const plan_identifier = tg_get_required_plan_identifier(request, @"planId");
    NSString* registered_plan_identifier = tg_get_optional_string(request, @"registeredPlanId");
    if (registered_plan_identifier.length > 0) {
        if (!tg_is_nonempty_uuid(registered_plan_identifier)) {
            tg_throw_invalid_request(@"eventkit_request_registered_plan_id_invalid");
        }
        registered_plan_identifier = [[[NSUUID alloc] initWithUUIDString:registered_plan_identifier] UUIDString].lowercaseString;
    }

    NSString* const existing_calendar_identifier = tg_get_optional_string(request, @"existingCalendarIdentifier");
    NSString* const expected_source_identifier = tg_get_optional_string(request, @"expectedSourceIdentifier");
    const long long term_starts_at_unix_seconds = tg_get_required_integer(request, @"termStartsAtUnixSeconds");
    const long long term_ends_at_unix_seconds = tg_get_required_integer(request, @"termEndsAtUnixSeconds");
    const long long migration_starts_at_unix_seconds = tg_get_required_integer(request, @"migrationStartsAtUnixSeconds");
    const long long migration_ends_at_unix_seconds = tg_get_required_integer(request, @"migrationEndsAtUnixSeconds");
    tg_validate_legacy_migration_range(term_starts_at_unix_seconds, term_ends_at_unix_seconds, migration_starts_at_unix_seconds, migration_ends_at_unix_seconds);

    NSArray<NSDictionary*>* const recurring_events = tg_validate_recurring_events(request);
    NSArray<NSDictionary*>* const managed_events = tg_validate_managed_events(request);
    const BOOL replacing = [mutation_kind isEqualToString:@"replace"];
    EKCalendar* calendar;

    if (replacing) {
        if (existing_calendar_identifier.length == 0 || expected_source_identifier.length == 0) {
            tg_throw_invalid_request(@"eventkit_request_replacement_precondition_invalid");
        }

        calendar = tg_find_calendar_or_null(event_store, existing_calendar_identifier);
        NSString* calendar_title = calendar.title;
        if (calendar_title == nil) {
            calendar_title = @"";
        }
        const BOOL has_expected_name = [tg_normalize_calendar_name(calendar_title) isEqualToString:normalized_destination_name];
        const BOOL has_expected_source = [tg_get_calendar_source_identifier(calendar) isEqualToString:expected_source_identifier];
        if (calendar == nil || !calendar.allowsContentModifications || !has_expected_name || !has_expected_source) {
            return tg_create_response(TG_STATUS_CALENDAR_CHANGED, @"eventkit_calendar_destination_changed");
        }
    } else {
        const BOOL has_existing_state = existing_calendar_identifier.length > 0 || expected_source_identifier.length > 0 || registered_plan_identifier.length > 0 || managed_events.count > 0;
        if (has_existing_state) {
            tg_throw_invalid_request(@"eventkit_request_create_precondition_invalid");
        }

        for (EKCalendar* existing_calendar in [event_store calendarsForEntityType:EKEntityTypeEvent]) {
            NSString* existing_calendar_title = existing_calendar.title;
            if (existing_calendar_title == nil) {
                existing_calendar_title = @"";
            }
            if ([tg_normalize_calendar_name(existing_calendar_title) isEqualToString:normalized_destination_name]) {
                return tg_create_response(TG_STATUS_CALENDAR_CHANGED, @"eventkit_calendar_destination_changed");
            }
        }

        EKSource* const source = tg_find_source_for_new_calendar_or_null(event_store);
        if (source == nil) {
            return tg_create_response(TG_STATUS_OPERATION_FAILED, @"eventkit_calendar_source_unavailable");
        }

        calendar = [EKCalendar calendarForEntityType:EKEntityTypeEvent eventStore:event_store];
        calendar.title = destination_name;
        calendar.source = source;
        NSError* calendar_save_error = nil;
        if (![event_store saveCalendar:calendar commit:NO error:&calendar_save_error]) {
            [event_store reset];
            return tg_create_response(TG_STATUS_OPERATION_FAILED, @"eventkit_calendar_create_failed");
        }
    }

    NSArray<EKEvent*>* legacy_events;
    if (replacing) {
        legacy_events = tg_get_managed_legacy_events(event_store, calendar, plan_identifier, migration_starts_at_unix_seconds, migration_ends_at_unix_seconds);
    } else {
        legacy_events = @[];
    }
    const BOOL registered_ownership = registered_plan_identifier.length > 0 && [registered_plan_identifier isEqualToString:plan_identifier];
    const BOOL has_missing_registered_events = registered_ownership && managed_events.count == 0;
    const BOOL has_unregistered_managed_events = !registered_ownership && managed_events.count > 0;
    const BOOL has_unregistered_plan_identifier = registered_plan_identifier.length > 0 && !registered_ownership;
    if (replacing && (has_missing_registered_events || has_unregistered_managed_events || has_unregistered_plan_identifier)) {
        [event_store reset];
        return tg_create_response(TG_STATUS_CALENDAR_CHANGED, @"eventkit_calendar_ownership_changed");
    }
    if (replacing && !registered_ownership && legacy_events.count == 0) {
        [event_store reset];
        return tg_create_response(TG_STATUS_CALENDAR_CHANGED, @"eventkit_calendar_ownership_changed");
    }

    NSArray<EKEvent*>* registered_events;
    if (replacing) {
        registered_events = tg_resolve_registered_events_or_null(event_store, calendar, managed_events, term_starts_at_unix_seconds, term_ends_at_unix_seconds);
    } else {
        registered_events = @[];
    }
    if (replacing && registered_events == nil) {
        [event_store reset];
        return tg_create_response(TG_STATUS_CALENDAR_CHANGED, @"eventkit_calendar_managed_events_changed");
    }

    NSMutableDictionary<NSString*, EKEvent*>* const events_to_delete = [NSMutableDictionary dictionary];
    for (EKEvent* event in registered_events) {
        events_to_delete[event.calendarItemIdentifier] = event;
    }
    for (EKEvent* event in legacy_events) {
        events_to_delete[event.calendarItemIdentifier] = event;
    }

    NSError* mutation_error = nil;
    for (EKEvent* event in events_to_delete.allValues) {
        if (!tg_stage_event_removal(event_store, event, &mutation_error)) {
            [event_store reset];
            return tg_create_response(TG_STATUS_OPERATION_FAILED, @"eventkit_calendar_event_delete_failed");
        }
    }

    NSMutableArray<EKEvent*>* const staged_events = [NSMutableArray arrayWithCapacity:recurring_events.count];
    for (NSDictionary* event_request in recurring_events) {
        EKEvent* const event = tg_stage_recurring_event_or_null(event_store, calendar, event_request, &mutation_error);
        if (event == nil) {
            [event_store reset];
            return tg_create_response(TG_STATUS_OPERATION_FAILED, @"eventkit_calendar_event_create_failed");
        }
        [staged_events addObject:event];
    }

    if (![event_store commit:&mutation_error]) {
        [event_store reset];
        return tg_create_response(TG_STATUS_OPERATION_FAILED, @"eventkit_calendar_commit_failed");
    }

    return tg_create_calendar_event_result(calendar, recurring_events, staged_events, events_to_delete.count);
}
