#import <CommonCrypto/CommonDigest.h>
#import <EventKit/EventKit.h>
#import <Foundation/Foundation.h>

#include <assert.h>
#include <limits.h>
#include <math.h>

#import "TimetableGeneratorEventKitEventIdentity.h"
#import "TimetableGeneratorEventKitProtocol.h"

#define TG_HEXADECIMAL_DIGITS_PER_BYTE (2U)
#define TG_NULL_TERMINATOR_BYTE_COUNT (1U)
#define TG_SHA256_HEXADECIMAL_LENGTH (CC_SHA256_DIGEST_LENGTH * TG_HEXADECIMAL_DIGITS_PER_BYTE)

static const NSTimeInterval TG_RECONCILIATION_CREATION_TOLERANCE_SECONDS = 10.0 * 60.0;
static const NSUInteger TG_BITS_PER_HEXADECIMAL_DIGIT = 4;
static const NSUInteger TG_CURRENT_MARKER_COMPONENT_COUNT = 2;
static const NSUInteger TG_CURRENT_MARKER_FINGERPRINT_INDEX = 1;
static const NSUInteger TG_CURRENT_MARKER_PLAN_IDENTIFIER_INDEX = 0;
static const NSUInteger TG_LOW_HEXADECIMAL_DIGIT_OFFSET = 1;
static const unsigned char TG_LOW_NIBBLE_MASK = 0x0F;

const NSInteger TG_WEEKLY_RECURRENCE_INTERVAL = 1;

static NSString* const TG_CURRENT_EVENT_MARKER_PREFIX = @"timetable-generator://managed-event/v2/";
static NSString* const TG_LEGACY_EVENT_MARKER_PREFIX = @"timetable-generator://managed-event/v1/";

static NSString* tg_get_current_marker_plan_identifier_or_null(NSURL* const url_or_null)
{
    NSString* const absolute_string = url_or_null.absoluteString;
    if (![absolute_string hasPrefix:TG_CURRENT_EVENT_MARKER_PREFIX]) {
        return nil;
    }

    NSString* const payload = [absolute_string substringFromIndex:TG_CURRENT_EVENT_MARKER_PREFIX.length];
    NSArray<NSString*>* const parts = [payload componentsSeparatedByString:@"/"];
    if (parts.count != TG_CURRENT_MARKER_COMPONENT_COUNT || !tg_is_nonempty_uuid(parts[TG_CURRENT_MARKER_PLAN_IDENTIFIER_INDEX]) || !tg_is_lowercase_sha256(parts[TG_CURRENT_MARKER_FINGERPRINT_INDEX])) {
        return nil;
    }

    return [[[NSUUID alloc] initWithUUIDString:parts[TG_CURRENT_MARKER_PLAN_IDENTIFIER_INDEX]] UUIDString].lowercaseString;
}

static BOOL tg_is_legacy_v1_marker(NSURL* const url_or_null)
{
    NSString* const absolute_string = url_or_null.absoluteString;
    if (![absolute_string hasPrefix:TG_LEGACY_EVENT_MARKER_PREFIX]) {
        return NO;
    }

    NSString* const payload = [absolute_string substringFromIndex:TG_LEGACY_EVENT_MARKER_PREFIX.length];
    return tg_is_lowercase_sha256(payload);
}

static BOOL tg_is_managed_legacy_url_for_plan(NSURL* const url_or_null, NSString* const plan_identifier)
{
    assert(plan_identifier != NULL);

    if (tg_is_legacy_v1_marker(url_or_null)) {
        return YES;
    }

    NSString* const marker_plan_identifier = tg_get_current_marker_plan_identifier_or_null(url_or_null);
    return marker_plan_identifier != nil && [marker_plan_identifier isEqualToString:plan_identifier];
}

static NSString* tg_get_base64_normalized_text(NSString* const value_or_null)
{
    NSData* const data = [tg_normalize_text(value_or_null ?: @"") dataUsingEncoding:NSUTF8StringEncoding];
    return [data base64EncodedStringWithOptions:0];
}

static NSString* tg_calculate_sha256(NSString* const value)
{
    assert(value != NULL);

    NSData* const data = [value dataUsingEncoding:NSUTF8StringEncoding];
    unsigned char digest[CC_SHA256_DIGEST_LENGTH];
    CC_SHA256(data.bytes, (CC_LONG)data.length, digest);

    static const char HEXADECIMAL_CHARACTERS[] = "0123456789abcdef";
    char hexadecimal_digest[TG_SHA256_HEXADECIMAL_LENGTH + TG_NULL_TERMINATOR_BYTE_COUNT];
    for (NSUInteger index = 0; index < CC_SHA256_DIGEST_LENGTH; ++index) {
        const NSUInteger high_digit_index = index * TG_HEXADECIMAL_DIGITS_PER_BYTE;
        const NSUInteger low_digit_index = high_digit_index + TG_LOW_HEXADECIMAL_DIGIT_OFFSET;
        hexadecimal_digest[high_digit_index] = HEXADECIMAL_CHARACTERS[(digest[index] >> TG_BITS_PER_HEXADECIMAL_DIGIT) & TG_LOW_NIBBLE_MASK];
        hexadecimal_digest[low_digit_index] = HEXADECIMAL_CHARACTERS[digest[index] & TG_LOW_NIBBLE_MASK];
    }
    hexadecimal_digest[TG_SHA256_HEXADECIMAL_LENGTH] = '\0';
    return [NSString stringWithUTF8String:hexadecimal_digest];
}

static NSString* tg_get_event_series_key_or_null(EKEvent* const event)
{
    assert(event != NULL);

    NSString* const external_identifier = event.calendarItemExternalIdentifier;
    if (external_identifier.length > 0) {
        return [@"external:" stringByAppendingString:external_identifier];
    }

    NSString* const calendar_item_identifier = event.calendarItemIdentifier;
    return calendar_item_identifier.length > 0 ? [@"calendar-item:" stringByAppendingString:calendar_item_identifier] : nil;
}

NSDate* tg_get_date_from_unix_seconds(const long long seconds)
{
    return [NSDate dateWithTimeIntervalSince1970:(NSTimeInterval)seconds];
}

NSString* tg_get_calendar_source_identifier(EKCalendar* const calendar_or_null)
{
    return calendar_or_null.source.sourceIdentifier ?: @"";
}

NSString* tg_get_calendar_identifier(EKCalendar* const calendar_or_null)
{
    return calendar_or_null.calendarIdentifier ?: @"";
}

NSArray<EKEvent*>* tg_get_events_in_calendar(
    EKEventStore* const event_store,
    EKCalendar* const calendar,
    const long long starts_at_unix_seconds,
    const long long ends_at_unix_seconds)
{
    assert(event_store != NULL);
    assert(calendar != NULL);

    if (ends_at_unix_seconds < starts_at_unix_seconds || ends_at_unix_seconds == LLONG_MAX) {
        tg_throw_invalid_request(@"eventkit_request_term_range_invalid");
    }

    NSDate* const start_date = tg_get_date_from_unix_seconds(starts_at_unix_seconds);
    NSDate* const end_date = tg_get_date_from_unix_seconds(ends_at_unix_seconds + TG_INCLUSIVE_RANGE_END_OFFSET_SECONDS);
    NSPredicate* const predicate = [event_store predicateForEventsWithStartDate:start_date endDate:end_date calendars:@[ calendar ]];
    return [event_store eventsMatchingPredicate:predicate];
}

NSDictionary* tg_create_legacy_ownership_snapshot(
    EKEventStore* const event_store,
    EKCalendar* const calendar,
    NSString* const requested_plan_identifier,
    const long long starts_at_unix_seconds,
    const long long ends_at_unix_seconds)
{
    assert(event_store != NULL);
    assert(calendar != NULL);
    assert(requested_plan_identifier != NULL);

    BOOL contains_v1_marker = NO;
    NSMutableSet<NSString*>* const v2_plan_identifiers = [NSMutableSet set];
    for (EKEvent* event in tg_get_events_in_calendar(event_store, calendar, starts_at_unix_seconds, ends_at_unix_seconds)) {
        if (tg_is_legacy_v1_marker(event.URL)) {
            contains_v1_marker = YES;
        }

        NSString* const plan_identifier = tg_get_current_marker_plan_identifier_or_null(event.URL);
        if (plan_identifier != nil) {
            [v2_plan_identifiers addObject:plan_identifier];
        }
    }

    if (v2_plan_identifiers.count > 1) {
        return @{
            @"managed" : @NO,
            @"planIdentifier" : @""
        };
    }

    NSString* plan_identifier = v2_plan_identifiers.anyObject;
    if (plan_identifier == nil && contains_v1_marker) {
        plan_identifier = requested_plan_identifier;
    }

    const BOOL managed = plan_identifier != nil || contains_v1_marker;
    return @{
        @"managed" : @(managed),
        @"planIdentifier" : plan_identifier ?: @""
    };
}

NSString* tg_get_fingerprint_for_event_or_null(EKEvent* const event)
{
    assert(event != NULL);

    if (event.startDate == nil || event.endDate == nil || event.timeZone.name.length == 0) {
        return nil;
    }

    NSArray<EKRecurrenceRule*>* const rules = event.recurrenceRules;
    if (rules.count != 1) {
        return nil;
    }

    EKRecurrenceRule* const rule = rules[0];
    if (rule.frequency != EKRecurrenceFrequencyWeekly || rule.interval != TG_WEEKLY_RECURRENCE_INTERVAL || rule.recurrenceEnd.endDate == nil) {
        return nil;
    }

    NSMutableArray<NSNumber*>* const weekdays = [NSMutableArray array];
    for (EKRecurrenceDayOfWeek* day in rule.daysOfTheWeek) {
        const NSInteger weekday = day.dayOfTheWeek;
        if (weekday < EKWeekdaySunday || weekday > EKWeekdaySaturday || [weekdays containsObject:@(weekday)]) {
            return nil;
        }
        [weekdays addObject:@(weekday)];
    }
    if (weekdays.count == 0) {
        return nil;
    }
    [weekdays sortUsingSelector:@selector(compare:)];

    NSMutableString* const canonical_value = [NSMutableString string];
    [canonical_value appendFormat:@"%@|", tg_get_base64_normalized_text(event.title ?: @"")];
    [canonical_value appendFormat:@"%@|", tg_get_base64_normalized_text(event.location ?: @"")];
    [canonical_value appendFormat:@"%@|", tg_get_base64_normalized_text(event.notes ?: @"")];
    [canonical_value appendFormat:@"%lld|", (long long)llround(event.startDate.timeIntervalSince1970)];
    [canonical_value appendFormat:@"%lld|", (long long)llround(event.endDate.timeIntervalSince1970)];
    [canonical_value appendFormat:@"%@|", tg_get_base64_normalized_text(event.timeZone.name)];
    [canonical_value appendFormat:@"%lld|", (long long)llround(rule.recurrenceEnd.endDate.timeIntervalSince1970)];
    for (NSUInteger index = 0; index < weekdays.count; ++index) {
        if (index > 0) {
            [canonical_value appendString:@","];
        }
        [canonical_value appendString:weekdays[index].stringValue];
    }

    return tg_calculate_sha256(canonical_value);
}

NSDictionary<NSString*, NSDictionary<NSString*, EKEvent*>*>* tg_index_events_by_fingerprint(NSArray<EKEvent*>* const events)
{
    assert(events != NULL);

    NSMutableDictionary<NSString*, NSMutableDictionary<NSString*, EKEvent*>*>* const mutable_index = [NSMutableDictionary dictionary];
    for (EKEvent* event in events) {
        NSString* const fingerprint = tg_get_fingerprint_for_event_or_null(event);
        NSString* const identifier = event.calendarItemIdentifier;
        if (fingerprint.length == 0 || identifier.length == 0) {
            continue;
        }

        NSMutableDictionary<NSString*, EKEvent*>* events_by_identifier = mutable_index[fingerprint];
        if (events_by_identifier == nil) {
            events_by_identifier = [NSMutableDictionary dictionary];
            mutable_index[fingerprint] = events_by_identifier;
        }
        events_by_identifier[identifier] = event;
    }

    NSMutableDictionary<NSString*, NSDictionary<NSString*, EKEvent*>*>* const index = [NSMutableDictionary dictionaryWithCapacity:mutable_index.count];
    for (NSString* fingerprint in mutable_index) {
        index[fingerprint] = [mutable_index[fingerprint] copy];
    }
    return [index copy];
}

NSArray<EKEvent*>* tg_resolve_unique_fingerprint_events_or_null(NSArray<NSDictionary*>* const event_requests, NSDictionary<NSString*, NSDictionary<NSString*, EKEvent*>*>* const fingerprint_index)
{
    assert(event_requests != NULL);
    assert(fingerprint_index != NULL);

    NSMutableSet<NSString*>* const fingerprints = [NSMutableSet setWithCapacity:event_requests.count];
    NSMutableSet<NSString*>* const resolved_identifiers = [NSMutableSet setWithCapacity:event_requests.count];
    NSMutableArray<EKEvent*>* const resolved_events = [NSMutableArray arrayWithCapacity:event_requests.count];
    for (NSDictionary* event_request in event_requests) {
        NSString* const fingerprint = event_request[@"fingerprint"];
        if ([fingerprints containsObject:fingerprint]) {
            return nil;
        }
        [fingerprints addObject:fingerprint];

        NSDictionary<NSString*, EKEvent*>* const events_by_identifier = fingerprint_index[fingerprint];
        if (events_by_identifier.count != 1) {
            return nil;
        }

        EKEvent* const event = events_by_identifier.allValues[0];
        NSString* const identifier = event.calendarItemIdentifier;
        if (identifier.length == 0 || [resolved_identifiers containsObject:identifier]) {
            return nil;
        }
        [resolved_identifiers addObject:identifier];
        [resolved_events addObject:event];
    }
    return resolved_events;
}

NSSet<NSString*>* tg_get_event_identifier_set_or_null(NSArray<EKEvent*>* const events)
{
    assert(events != NULL);

    NSMutableSet<NSString*>* const identifiers = [NSMutableSet setWithCapacity:events.count];
    for (EKEvent* event in events) {
        NSString* const identifier = event.calendarItemIdentifier;
        if (identifier.length == 0 || [identifiers containsObject:identifier]) {
            return nil;
        }
        [identifiers addObject:identifier];
    }
    return identifiers;
}

BOOL tg_does_calendar_contain_only_resolved_series(NSArray<EKEvent*>* const calendar_events, NSArray<EKEvent*>* const resolved_events)
{
    assert(calendar_events != NULL);
    assert(resolved_events != NULL);

    NSMutableSet<NSString*>* const resolved_series_keys = [NSMutableSet setWithCapacity:resolved_events.count];
    for (EKEvent* event in resolved_events) {
        NSString* const series_key = tg_get_event_series_key_or_null(event);
        if (series_key.length == 0 || [resolved_series_keys containsObject:series_key]) {
            return NO;
        }
        [resolved_series_keys addObject:series_key];
    }

    NSMutableSet<NSString*>* const calendar_series_keys = [NSMutableSet set];
    for (EKEvent* event in calendar_events) {
        NSString* const series_key = tg_get_event_series_key_or_null(event);
        if (series_key.length == 0 || ![resolved_series_keys containsObject:series_key]) {
            return NO;
        }
        [calendar_series_keys addObject:series_key];
    }
    return [calendar_series_keys isEqualToSet:resolved_series_keys];
}

BOOL tg_are_events_created_near(NSArray<EKEvent*>* const events, const long long prepared_at_unix_seconds)
{
    assert(events != NULL);

    for (EKEvent* event in events) {
        NSDate* const creation_date = event.creationDate;
        if (creation_date == nil || fabs(creation_date.timeIntervalSince1970 - (NSTimeInterval)prepared_at_unix_seconds) > TG_RECONCILIATION_CREATION_TOLERANCE_SECONDS) {
            return NO;
        }
    }
    return YES;
}

NSArray<EKEvent*>* tg_get_managed_legacy_events(
    EKEventStore* const event_store,
    EKCalendar* const calendar,
    NSString* const plan_identifier,
    const long long starts_at_unix_seconds,
    const long long ends_at_unix_seconds)
{
    assert(event_store != NULL);
    assert(calendar != NULL);
    assert(plan_identifier != NULL);

    NSMutableDictionary<NSString*, EKEvent*>* const events_by_identifier = [NSMutableDictionary dictionary];
    for (EKEvent* event in tg_get_events_in_calendar(event_store, calendar, starts_at_unix_seconds, ends_at_unix_seconds)) {
        if (!tg_is_managed_legacy_url_for_plan(event.URL, plan_identifier)) {
            continue;
        }

        NSString* const identifier = event.calendarItemIdentifier;
        if (identifier.length > 0) {
            events_by_identifier[identifier] = event;
        }
    }
    return events_by_identifier.allValues;
}

NSSet<NSString*>* tg_get_registered_event_candidate_identifiers(EKEventStore* const event_store, EKCalendar* const calendar, NSArray<NSDictionary*>* const managed_events)
{
    assert(event_store != NULL);
    assert(calendar != NULL);
    assert(managed_events != NULL);

    NSString* const calendar_identifier = tg_get_calendar_identifier(calendar);
    NSMutableSet<NSString*>* const candidate_identifiers = [NSMutableSet set];
    for (NSDictionary* registration in managed_events) {
        EKCalendarItem* const exact_item = [event_store calendarItemWithIdentifier:registration[@"calendarItemIdentifier"]];
        const BOOL exact_item_is_event = [exact_item isKindOfClass:[EKEvent class]];
        const BOOL exact_item_has_matching_calendar = [tg_get_calendar_identifier(exact_item.calendar) isEqualToString:calendar_identifier];
        const BOOL exact_item_has_identifier = exact_item.calendarItemIdentifier.length > 0;
        if (exact_item_is_event && exact_item_has_matching_calendar && exact_item_has_identifier) {
            [candidate_identifiers addObject:exact_item.calendarItemIdentifier];
        }

        NSString* const external_identifier = registration[@"externalIdentifier"];
        if (external_identifier.length == 0) {
            continue;
        }
        for (EKCalendarItem* candidate_item in [event_store calendarItemsWithExternalIdentifier:external_identifier]) {
            const BOOL is_event = [candidate_item isKindOfClass:[EKEvent class]];
            const BOOL has_matching_calendar = [tg_get_calendar_identifier(candidate_item.calendar) isEqualToString:calendar_identifier];
            const BOOL has_identifier = candidate_item.calendarItemIdentifier.length > 0;
            if (is_event && has_matching_calendar && has_identifier) {
                [candidate_identifiers addObject:candidate_item.calendarItemIdentifier];
            }
        }
    }
    return candidate_identifiers;
}

BOOL tg_does_index_contain_candidate_outside_identifiers(NSArray<NSDictionary*>* const event_requests, NSDictionary<NSString*, NSDictionary<NSString*, EKEvent*>*>* const fingerprint_index, NSSet<NSString*>* const allowed_identifiers)
{
    assert(event_requests != NULL);
    assert(fingerprint_index != NULL);
    assert(allowed_identifiers != NULL);

    for (NSDictionary* event_request in event_requests) {
        NSDictionary<NSString*, EKEvent*>* const events_by_identifier = fingerprint_index[event_request[@"fingerprint"]];
        for (NSString* identifier in events_by_identifier) {
            if (![allowed_identifiers containsObject:identifier]) {
                return YES;
            }
        }
    }
    return NO;
}

BOOL tg_does_any_calendar_contain_recent_desired_candidate(
    EKEventStore* const event_store,
    NSArray<NSDictionary*>* const event_requests,
    const long long term_starts_at_unix_seconds,
    const long long term_ends_at_unix_seconds,
    const long long prepared_at_unix_seconds)
{
    assert(event_store != NULL);
    assert(event_requests != NULL);

    for (EKCalendar* calendar in [event_store calendarsForEntityType:EKEntityTypeEvent]) {
        NSArray<EKEvent*>* const events = tg_get_events_in_calendar(event_store, calendar, term_starts_at_unix_seconds, term_ends_at_unix_seconds);
        NSDictionary<NSString*, NSDictionary<NSString*, EKEvent*>*>* const fingerprint_index = tg_index_events_by_fingerprint(events);
        for (NSDictionary* event_request in event_requests) {
            for (EKEvent* event in [fingerprint_index[event_request[@"fingerprint"]] allValues]) {
                NSDate* const creation_date = event.creationDate;
                if (creation_date == nil) {
                    continue;
                }

                const NSTimeInterval creation_delta_seconds = fabs(creation_date.timeIntervalSince1970 - (NSTimeInterval)prepared_at_unix_seconds);
                if (creation_delta_seconds <= TG_RECONCILIATION_CREATION_TOLERANCE_SECONDS) {
                    return YES;
                }
            }
        }
    }
    return NO;
}

EKCalendar* tg_resolve_pending_committed_calendar_after_identifier_change_or_null(
    EKEventStore* const event_store,
    NSArray<EKCalendar*>* const calendars,
    NSString* const normalized_calendar_name,
    NSString* const source_identifier,
    NSArray<NSDictionary*>* const desired_event_requests,
    const long long term_starts_at_unix_seconds,
    const long long term_ends_at_unix_seconds,
    const long long prepared_at_unix_seconds)
{
    assert(event_store != NULL);
    assert(calendars != NULL);
    assert(normalized_calendar_name != NULL);
    assert(source_identifier != NULL);
    assert(desired_event_requests != NULL);

    NSMutableArray<EKCalendar*>* const matches = [NSMutableArray array];
    for (EKCalendar* candidate in calendars) {
        const BOOL has_matching_source = [tg_get_calendar_source_identifier(candidate) isEqualToString:source_identifier];
        const BOOL has_matching_name = [tg_normalize_calendar_name(candidate.title ?: @"") isEqualToString:normalized_calendar_name];
        if (!candidate.allowsContentModifications || !has_matching_source || !has_matching_name) {
            continue;
        }

        NSArray<EKEvent*>* const events = tg_get_events_in_calendar(event_store, candidate, term_starts_at_unix_seconds, term_ends_at_unix_seconds);
        NSDictionary<NSString*, NSDictionary<NSString*, EKEvent*>*>* const fingerprint_index = tg_index_events_by_fingerprint(events);
        NSArray<EKEvent*>* const desired_events = tg_resolve_unique_fingerprint_events_or_null(desired_event_requests, fingerprint_index);
        if (desired_events != nil && tg_are_events_created_near(desired_events, prepared_at_unix_seconds)) {
            [matches addObject:candidate];
        }
    }
    return matches.count == 1 ? matches.firstObject : nil;
}
