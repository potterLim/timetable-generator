#import <EventKit/EventKit.h>
#import <Foundation/Foundation.h>

#include <assert.h>

#import "TimetableGeneratorEventKitEventIdentity.h"
#import "TimetableGeneratorEventKitProtocol.h"
#import "TimetableGeneratorEventKitRegistration.h"

static NSDictionary* tg_resolve_list_registration_state(NSDictionary* const request, EKEventStore* const event_store, NSArray<EKCalendar*>* const calendars);

static NSArray<EKEvent*>* tg_resolve_registered_events_after_identifier_change_or_null(
    EKEventStore* const event_store,
    EKCalendar* const calendar,
    NSArray<NSDictionary*>* const managed_events,
    const long long term_starts_at_unix_seconds,
    const long long term_ends_at_unix_seconds)
{
    assert(event_store != NULL);
    assert(calendar != NULL);
    assert(managed_events != NULL);

    NSString* const calendar_identifier = tg_get_calendar_identifier(calendar);
    NSArray<EKEvent*>* const calendar_events = tg_get_events_in_calendar(event_store, calendar, term_starts_at_unix_seconds, term_ends_at_unix_seconds);
    NSDictionary<NSString*, NSDictionary<NSString*, EKEvent*>*>* const fingerprint_index = tg_index_events_by_fingerprint(calendar_events);
    NSMutableSet<NSString*>* const resolved_identifiers = [NSMutableSet setWithCapacity:managed_events.count];
    NSMutableArray<EKEvent*>* const resolved_events = [NSMutableArray arrayWithCapacity:managed_events.count];
    for (NSDictionary* registration in managed_events) {
        EKEvent* resolved_event = nil;
        NSString* const external_identifier = registration[@"externalIdentifier"];
        if (external_identifier.length > 0) {
            NSMutableDictionary<NSString*, EKEvent*>* const external_matches = [NSMutableDictionary dictionary];
            for (EKCalendarItem* candidate_item in [event_store calendarItemsWithExternalIdentifier:external_identifier]) {
                if (![candidate_item isKindOfClass:[EKEvent class]] || ![tg_get_calendar_identifier(candidate_item.calendar) isEqualToString:calendar_identifier]) {
                    continue;
                }

                EKEvent* const candidate_event = (EKEvent*)candidate_item;
                NSString* const candidate_identifier = candidate_event.calendarItemIdentifier;
                if (candidate_identifier.length > 0 && [tg_get_fingerprint_for_event_or_null(candidate_event) isEqualToString:registration[@"fingerprint"]]) {
                    external_matches[candidate_identifier] = candidate_event;
                }
            }
            if (external_matches.count > 1) {
                return nil;
            }
            resolved_event = external_matches.allValues.firstObject;
        }

        if (resolved_event == nil) {
            NSDictionary<NSString*, EKEvent*>* const fingerprint_matches = fingerprint_index[registration[@"fingerprint"]];
            if (fingerprint_matches.count != 1) {
                return nil;
            }
            resolved_event = fingerprint_matches.allValues.firstObject;
        }

        NSString* const resolved_identifier = resolved_event.calendarItemIdentifier;
        if (resolved_identifier.length == 0 || [resolved_identifiers containsObject:resolved_identifier]) {
            return nil;
        }
        [resolved_identifiers addObject:resolved_identifier];
        [resolved_events addObject:resolved_event];
    }
    return resolved_events;
}

static NSDictionary* tg_resolve_list_registration_state(NSDictionary* const request, EKEventStore* const event_store, NSArray<EKCalendar*>* const calendars)
{
    assert(request != NULL);
    assert(event_store != NULL);
    assert(calendars != NULL);

    NSArray<NSDictionary*>* const registrations = tg_validate_list_registrations(request);
    NSString* const requested_normalized_name = tg_normalize_calendar_name(tg_get_required_string(request, @"requestedName"));
    NSMutableDictionary<NSString*, EKCalendar*>* const current_calendars_by_identifier = [NSMutableDictionary dictionaryWithCapacity:calendars.count];
    for (EKCalendar* calendar in calendars) {
        NSString* const identifier = tg_get_calendar_identifier(calendar);
        if (identifier.length > 0) {
            current_calendars_by_identifier[identifier] = calendar;
        }
    }

    NSMutableDictionary<NSString*, NSString*>* const plan_identifiers_by_calendar_identifier = [NSMutableDictionary dictionary];
    NSMutableArray<NSDictionary*>* const bindings = [NSMutableArray array];
    NSMutableArray<NSDictionary*>* const proposed_rebindings = [NSMutableArray array];
    NSMutableDictionary<NSString*, NSNumber*>* const proposal_counts = [NSMutableDictionary dictionary];
    NSMutableSet<NSString*>* const requested_registration_identifiers = [NSMutableSet set];
    for (NSDictionary* registration in registrations) {
        NSString* const previous_calendar_identifier = registration[@"calendarIdentifier"];
        const BOOL matches_requested_name = [registration[@"normalizedCalendarName"] isEqualToString:requested_normalized_name];
        if (matches_requested_name) {
            [requested_registration_identifiers addObject:previous_calendar_identifier];
        }

        EKCalendar* const current_calendar = current_calendars_by_identifier[previous_calendar_identifier];
        if (current_calendar != nil) {
            plan_identifiers_by_calendar_identifier[previous_calendar_identifier] = registration[@"planId"];
            NSArray<EKEvent*>* const resolved_events = tg_resolve_registered_events_or_null(
                event_store,
                current_calendar,
                registration[@"managedEvents"],
                [registration[@"termStartsAtUnixSeconds"] longLongValue],
                [registration[@"termEndsAtUnixSeconds"] longLongValue]);
            if (resolved_events == nil) {
                if (matches_requested_name) {
                    return @{
                        @"diagnosticCode" : @"eventkit_calendar_registration_ambiguous"
                    };
                }
                continue;
            }

            if (!tg_does_registration_match_resolved_events(registration[@"managedEvents"], resolved_events)) {
                NSDictionary* const binding = tg_create_registration_binding_or_null(registration, current_calendar, resolved_events);
                if (binding == nil) {
                    return @{
                        @"diagnosticCode" : @"eventkit_calendar_registration_ambiguous"
                    };
                }
                [bindings addObject:binding];
            }
            continue;
        }

        NSUInteger candidate_count = 0;
        NSDictionary* const rebound_registration = tg_resolve_rebound_registration_or_null(event_store, calendars, registration, &candidate_count);
        if (rebound_registration == nil) {
            if (matches_requested_name && candidate_count > 0) {
                return @{
                    @"diagnosticCode" : @"eventkit_calendar_registration_ambiguous"
                };
            }
            continue;
        }

        NSDictionary* const binding = rebound_registration[@"binding"];
        NSString* const current_calendar_identifier = binding[@"calendarIdentifier"];
        proposal_counts[current_calendar_identifier] = @([proposal_counts[current_calendar_identifier] unsignedIntegerValue] + 1);
        [proposed_rebindings addObject:binding];
    }

    [proposed_rebindings sortUsingComparator:^NSComparisonResult(NSDictionary* const left, NSDictionary* const right) {
        return [left[@"previousCalendarIdentifier"] compare:right[@"previousCalendarIdentifier"]];
    }];
    for (NSDictionary* binding in proposed_rebindings) {
        NSString* const current_calendar_identifier = binding[@"calendarIdentifier"];
        if ([proposal_counts[current_calendar_identifier] unsignedIntegerValue] != 1 || plan_identifiers_by_calendar_identifier[current_calendar_identifier] != nil) {
            if ([requested_registration_identifiers containsObject:binding[@"previousCalendarIdentifier"]]) {
                return @{
                    @"diagnosticCode" : @"eventkit_calendar_registration_ambiguous"
                };
            }
            continue;
        }

        plan_identifiers_by_calendar_identifier[current_calendar_identifier] = binding[@"planId"];
        [bindings addObject:binding];
    }

    return @{
        @"planIdentifiersByCalendarIdentifier" : plan_identifiers_by_calendar_identifier,
        @"bindings" : bindings,
        @"diagnosticCode" : @""
    };
}

static NSArray<EKEvent*>* tg_resolve_registered_events_by_stored_identifiers_or_null(EKEventStore* const event_store, EKCalendar* const calendar, NSArray<NSDictionary*>* const managed_events)
{
    assert(event_store != NULL);
    assert(calendar != NULL);
    assert(managed_events != NULL);

    NSString* const calendar_identifier = tg_get_calendar_identifier(calendar);
    NSMutableSet<NSString*>* const resolved_identifiers = [NSMutableSet set];
    NSMutableArray<EKEvent*>* const resolved_events = [NSMutableArray arrayWithCapacity:managed_events.count];
    for (NSDictionary* registration in managed_events) {
        NSString* const registered_identifier = registration[@"calendarItemIdentifier"];
        EKCalendarItem* const exact_item = [event_store calendarItemWithIdentifier:registered_identifier];
        EKEvent* resolved_event = nil;
        if (exact_item != nil) {
            if (![exact_item isKindOfClass:[EKEvent class]] || ![tg_get_calendar_identifier(exact_item.calendar) isEqualToString:calendar_identifier]) {
                return nil;
            }
            resolved_event = (EKEvent*)exact_item;
        } else {
            NSString* const external_identifier = registration[@"externalIdentifier"];
            if (external_identifier.length == 0) {
                return nil;
            }

            NSMutableDictionary<NSString*, EKEvent*>* const matching_events = [NSMutableDictionary dictionary];
            for (EKCalendarItem* candidate_item in [event_store calendarItemsWithExternalIdentifier:external_identifier]) {
                if (![candidate_item isKindOfClass:[EKEvent class]] || ![tg_get_calendar_identifier(candidate_item.calendar) isEqualToString:calendar_identifier]) {
                    continue;
                }

                EKEvent* const candidate_event = (EKEvent*)candidate_item;
                NSString* const candidate_identifier = candidate_event.calendarItemIdentifier;
                NSString* const candidate_fingerprint = tg_get_fingerprint_for_event_or_null(candidate_event);
                if (candidate_identifier.length > 0 && [candidate_fingerprint isEqualToString:registration[@"fingerprint"]]) {
                    matching_events[candidate_identifier] = candidate_event;
                }
            }
            if (matching_events.count != 1) {
                return nil;
            }
            resolved_event = matching_events.allValues[0];
        }

        NSString* const resolved_identifier = resolved_event.calendarItemIdentifier;
        if (resolved_identifier.length == 0 || [resolved_identifiers containsObject:resolved_identifier]) {
            return nil;
        }
        [resolved_identifiers addObject:resolved_identifier];
        [resolved_events addObject:resolved_event];
    }

    return resolved_events;
}

NSDictionary* tg_list_calendars(NSDictionary* const request, EKEventStore* const event_store)
{
    assert(request != NULL);
    assert(event_store != NULL);

    NSString* const requested_name = tg_get_required_string(request, @"requestedName");
    NSString* const requested_normalized_name = tg_normalize_calendar_name(requested_name);
    NSString* const requested_plan_identifier = tg_get_required_plan_identifier(request, @"planId");
    const long long term_starts_at_unix_seconds = tg_get_required_integer(request, @"termStartsAtUnixSeconds");
    const long long term_ends_at_unix_seconds = tg_get_required_integer(request, @"termEndsAtUnixSeconds");
    const long long migration_starts_at_unix_seconds = tg_get_required_integer(request, @"migrationStartsAtUnixSeconds");
    const long long migration_ends_at_unix_seconds = tg_get_required_integer(request, @"migrationEndsAtUnixSeconds");
    tg_validate_legacy_migration_range(term_starts_at_unix_seconds, term_ends_at_unix_seconds, migration_starts_at_unix_seconds, migration_ends_at_unix_seconds);

    NSArray<EKCalendar*>* const calendars = [event_store calendarsForEntityType:EKEntityTypeEvent];
    NSDictionary* const registration_state = tg_resolve_list_registration_state(request, event_store, calendars);
    NSString* const registration_diagnostic_code = registration_state[@"diagnosticCode"];
    if (registration_diagnostic_code.length > 0) {
        return tg_create_response(TG_STATUS_CALENDAR_CHANGED, registration_diagnostic_code);
    }
    NSDictionary<NSString*, NSString*>* const registrations = registration_state[@"planIdentifiersByCalendarIdentifier"];
    NSMutableArray<NSDictionary*>* const calendar_responses = [NSMutableArray array];
    for (EKCalendar* calendar in calendars) {
        NSString* const calendar_identifier = tg_get_calendar_identifier(calendar);
        NSString* const source_identifier = tg_get_calendar_source_identifier(calendar);
        NSString* const calendar_name = calendar.title ?: @"";
        if (calendar_identifier.length == 0 || source_identifier.length == 0 || calendar_name.length == 0) {
            continue;
        }

        const BOOL matching_name = [tg_normalize_calendar_name(calendar_name) isEqualToString:requested_normalized_name];
        NSDictionary* legacy_snapshot = @{
            @"managed" : @NO,
            @"planIdentifier" : @""
        };
        if (matching_name) {
            legacy_snapshot = tg_create_legacy_ownership_snapshot(
                event_store,
                calendar,
                requested_plan_identifier,
                migration_starts_at_unix_seconds,
                migration_ends_at_unix_seconds);
        }

        [calendar_responses addObject:@{
            @"identifier" : calendar_identifier,
            @"name" : calendar_name,
            @"sourceIdentifier" : source_identifier,
            @"writable" : @(calendar.allowsContentModifications),
            @"registeredPlanId" : registrations[calendar_identifier] ?: @"",
            @"legacyPlanId" : legacy_snapshot[@"planIdentifier"],
            @"legacyManaged" : legacy_snapshot[@"managed"]
        }];
    }

    [calendar_responses sortUsingComparator:^NSComparisonResult(NSDictionary* const left, NSDictionary* const right) {
        const NSComparisonResult name_comparison = [tg_normalize_calendar_name(left[@"name"]) compare:tg_normalize_calendar_name(right[@"name"])];
        if (name_comparison != NSOrderedSame) {
            return name_comparison;
        }
        return [left[@"identifier"] compare:right[@"identifier"]];
    }];

    return @{
        @"schemaVersion" : @(TG_SCHEMA_VERSION),
        @"status" : TG_STATUS_OK,
        @"diagnosticCode" : @"",
        @"calendars" : calendar_responses,
        @"registrationBindings" : registration_state[@"bindings"]
    };
}

NSDictionary* tg_create_registration_binding_or_null(NSDictionary* const registration, EKCalendar* const calendar, NSArray<EKEvent*>* const resolved_events)
{
    assert(registration != NULL);
    assert(calendar != NULL);
    assert(resolved_events != NULL);

    NSArray<NSDictionary*>* const managed_events = registration[@"managedEvents"];
    if (managed_events.count != resolved_events.count) {
        return nil;
    }

    NSMutableArray<NSDictionary*>* const event_bindings = [NSMutableArray arrayWithCapacity:managed_events.count];
    for (NSUInteger index = 0; index < managed_events.count; ++index) {
        NSDictionary* const managed_event = managed_events[index];
        EKEvent* const resolved_event = resolved_events[index];
        NSString* const calendar_item_identifier = resolved_event.calendarItemIdentifier;
        if (calendar_item_identifier.length == 0) {
            return nil;
        }

        [event_bindings addObject:@{
            @"sourceEventHash" : managed_event[@"sourceEventHash"],
            @"calendarItemIdentifier" : calendar_item_identifier,
            @"externalIdentifier" : resolved_event.calendarItemExternalIdentifier ?: @"",
            @"fingerprint" : managed_event[@"fingerprint"]
        }];
    }

    return @{
        @"previousCalendarIdentifier" : registration[@"calendarIdentifier"],
        @"calendarIdentifier" : tg_get_calendar_identifier(calendar),
        @"calendarName" : calendar.title ?: @"",
        @"sourceIdentifier" : tg_get_calendar_source_identifier(calendar),
        @"planId" : registration[@"planId"],
        @"events" : event_bindings
    };
}

BOOL tg_does_registration_match_resolved_events(NSArray<NSDictionary*>* const managed_events, NSArray<EKEvent*>* const resolved_events)
{
    assert(managed_events != NULL);
    assert(resolved_events != NULL);

    if (managed_events.count != resolved_events.count) {
        return NO;
    }

    for (NSUInteger index = 0; index < managed_events.count; ++index) {
        NSDictionary* const managed_event = managed_events[index];
        EKEvent* const resolved_event = resolved_events[index];
        NSString* const resolved_identifier = resolved_event.calendarItemIdentifier ?: @"";
        NSString* const resolved_external_identifier = resolved_event.calendarItemExternalIdentifier ?: @"";
        const BOOL has_matching_identifier = [managed_event[@"calendarItemIdentifier"] isEqualToString:resolved_identifier];
        const BOOL has_matching_external_identifier = [managed_event[@"externalIdentifier"] isEqualToString:resolved_external_identifier];
        if (!has_matching_identifier || !has_matching_external_identifier) {
            return NO;
        }
    }
    return YES;
}

NSDictionary* tg_resolve_rebound_registration_or_null(
    EKEventStore* const event_store,
    NSArray<EKCalendar*>* const calendars,
    NSDictionary* const registration,
    NSUInteger* const out_candidate_count)
{
    assert(event_store != NULL);
    assert(calendars != NULL);
    assert(registration != NULL);
    assert(out_candidate_count != NULL);

    NSUInteger candidate_count = 0;
    NSMutableArray<NSDictionary*>* const matches = [NSMutableArray array];
    for (EKCalendar* candidate in calendars) {
        const BOOL has_matching_source = [tg_get_calendar_source_identifier(candidate) isEqualToString:registration[@"sourceIdentifier"]];
        const BOOL has_matching_name = [tg_normalize_calendar_name(candidate.title ?: @"") isEqualToString:registration[@"normalizedCalendarName"]];
        if (!has_matching_source || !has_matching_name) {
            continue;
        }
        ++candidate_count;
        if (!candidate.allowsContentModifications) {
            continue;
        }

        NSArray<EKEvent*>* const resolved_events = tg_resolve_registered_events_after_identifier_change_or_null(
            event_store,
            candidate,
            registration[@"managedEvents"],
            [registration[@"termStartsAtUnixSeconds"] longLongValue],
            [registration[@"termEndsAtUnixSeconds"] longLongValue]);
        if (resolved_events == nil) {
            continue;
        }

        NSDictionary* const binding = tg_create_registration_binding_or_null(registration, candidate, resolved_events);
        if (binding != nil) {
            [matches addObject:@{
                @"calendar" : candidate,
                @"events" : resolved_events,
                @"binding" : binding
            }];
        }
    }
    *out_candidate_count = candidate_count;
    return matches.count == 1 ? matches.firstObject : nil;
}

EKCalendar* tg_find_calendar_or_null(EKEventStore* const event_store, NSString* const identifier)
{
    assert(event_store != NULL);
    assert(identifier != NULL);

    if (identifier.length == 0) {
        return nil;
    }
    return [event_store calendarWithIdentifier:identifier];
}

EKSource* tg_find_source_for_new_calendar_or_null(EKEventStore* const event_store)
{
    assert(event_store != NULL);

    EKSource* const default_source = event_store.defaultCalendarForNewEvents.source;
    if (default_source != nil && default_source.sourceIdentifier.length > 0) {
        return default_source;
    }

    for (EKCalendar* calendar in [event_store calendarsForEntityType:EKEntityTypeEvent]) {
        if (calendar.allowsContentModifications && calendar.source.sourceIdentifier.length > 0) {
            return calendar.source;
        }
    }
    return nil;
}

NSArray<EKEvent*>* tg_resolve_registered_events_or_null(
    EKEventStore* const event_store,
    EKCalendar* const calendar,
    NSArray<NSDictionary*>* const managed_events,
    const long long term_starts_at_unix_seconds,
    const long long term_ends_at_unix_seconds)
{
    assert(event_store != NULL);
    assert(calendar != NULL);
    assert(managed_events != NULL);

    NSArray<EKEvent*>* const resolved_events = tg_resolve_registered_events_by_stored_identifiers_or_null(event_store, calendar, managed_events);
    if (resolved_events != nil) {
        return resolved_events;
    }

    return tg_resolve_registered_events_after_identifier_change_or_null(
        event_store,
        calendar,
        managed_events,
        term_starts_at_unix_seconds,
        term_ends_at_unix_seconds);
}
