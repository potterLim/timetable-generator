#import <CommonCrypto/CommonDigest.h>
#import <EventKit/EventKit.h>
#import <Foundation/Foundation.h>

#include <assert.h>
#include <limits.h>
#include <math.h>

#import "TimetableGeneratorEventKitProtocol.h"

#define TG_HEXADECIMAL_DIGITS_PER_BYTE (2U)
#define TG_SHA256_HEXADECIMAL_LENGTH (CC_SHA256_DIGEST_LENGTH * TG_HEXADECIMAL_DIGITS_PER_BYTE)

static const int64_t TG_LEGACY_MIGRATION_PADDING_SECONDS = 366LL * 24LL * 60LL * 60LL;

const uint32_t TG_SCHEMA_VERSION = 1;
const int64_t TG_INCLUSIVE_RANGE_END_OFFSET_SECONDS = 1;

NSString* const TG_INVALID_REQUEST_EXCEPTION = @"TimetableGeneratorEventKitInvalidRequest";

NSString* const TG_STATUS_OK = @"ok";
NSString* const TG_STATUS_ACCESS_DENIED = @"access_denied";
NSString* const TG_STATUS_CALENDAR_CHANGED = @"calendar_changed";
NSString* const TG_STATUS_INVALID_REQUEST = @"invalid_request";
NSString* const TG_STATUS_NOT_FOUND = @"not_found";
NSString* const TG_STATUS_OPERATION_FAILED = @"operation_failed";

static BOOL tg_is_boolean_number(NSNumber* const number)
{
    assert(number != NULL);

    return CFGetTypeID((__bridge CFTypeRef)number) == CFBooleanGetTypeID();
}

static NSArray* tg_get_required_array(NSDictionary* const dictionary, NSString* const key)
{
    assert(dictionary != NULL);
    assert(key != NULL);

    const id value = dictionary[key];
    if (![value isKindOfClass:[NSArray class]]) {
        tg_throw_invalid_request(@"eventkit_request_array_invalid");
    }

    return value;
}

static NSArray* tg_get_optional_array(NSDictionary* const dictionary, NSString* const key)
{
    assert(dictionary != NULL);
    assert(key != NULL);

    const id value = dictionary[key];
    if (value == nil) {
        return @[];
    }

    if (![value isKindOfClass:[NSArray class]]) {
        tg_throw_invalid_request(@"eventkit_request_array_invalid");
    }

    return value;
}

static NSString* tg_get_required_hash(NSDictionary* const dictionary, NSString* const key)
{
    assert(dictionary != NULL);
    assert(key != NULL);

    NSString* const value = tg_get_required_string(dictionary, key);
    if (!tg_is_lowercase_sha256(value)) {
        tg_throw_invalid_request(@"eventkit_request_hash_invalid");
    }

    return value;
}

NSDictionary* tg_create_response(NSString* const status, NSString* const diagnostic_code)
{
    assert(status != NULL);
    assert(diagnostic_code != NULL);

    return @{
        @"schemaVersion" : @(TG_SCHEMA_VERSION),
        @"status" : status,
        @"diagnosticCode" : diagnostic_code
    };
}

void tg_throw_invalid_request(NSString* const diagnostic_code)
{
    assert(diagnostic_code != NULL);

    @throw [NSException exceptionWithName:TG_INVALID_REQUEST_EXCEPTION reason:diagnostic_code userInfo:nil];
}

NSString* tg_get_required_string(NSDictionary* const dictionary, NSString* const key)
{
    assert(dictionary != NULL);
    assert(key != NULL);

    const id value = dictionary[key];
    if (![value isKindOfClass:[NSString class]] || [(NSString*)value length] == 0) {
        tg_throw_invalid_request(@"eventkit_request_string_invalid");
    }

    return value;
}

NSString* tg_get_optional_string(NSDictionary* const dictionary, NSString* const key)
{
    assert(dictionary != NULL);
    assert(key != NULL);

    const id value = dictionary[key];
    if (value == nil || value == [NSNull null]) {
        return @"";
    }

    if (![value isKindOfClass:[NSString class]]) {
        tg_throw_invalid_request(@"eventkit_request_string_invalid");
    }

    return value;
}

long long tg_get_required_integer(NSDictionary* const dictionary, NSString* const key)
{
    assert(dictionary != NULL);
    assert(key != NULL);

    const id value = dictionary[key];
    if (![value isKindOfClass:[NSNumber class]] || tg_is_boolean_number(value)) {
        tg_throw_invalid_request(@"eventkit_request_integer_invalid");
    }

    const double double_value = [value doubleValue];
    const long long integer_value = [value longLongValue];
    if (!isfinite(double_value) || double_value != (double)integer_value) {
        tg_throw_invalid_request(@"eventkit_request_integer_invalid");
    }

    return integer_value;
}

NSString* tg_normalize_text(NSString* const value)
{
    assert(value != NULL);

    NSString* const trimmed = [value stringByTrimmingCharactersInSet:[NSCharacterSet whitespaceAndNewlineCharacterSet]];
    return [trimmed precomposedStringWithCanonicalMapping];
}

NSString* tg_normalize_calendar_name(NSString* const value)
{
    assert(value != NULL);

    NSMutableString* const canonical_value = [tg_normalize_text(value) mutableCopy];
    for (NSUInteger index = 0; index < canonical_value.length; ++index) {
        const unichar character = [canonical_value characterAtIndex:index];
        if (character >= 'a' && character <= 'z') {
            const unichar uppercase_character = character - ('a' - 'A');
            [canonical_value replaceCharactersInRange:NSMakeRange(index, 1) withString:[NSString stringWithCharacters:&uppercase_character length:1]];
        }
    }
    return canonical_value;
}

BOOL tg_is_lowercase_sha256(NSString* const value)
{
    assert(value != NULL);

    if (value.length != TG_SHA256_HEXADECIMAL_LENGTH) {
        return NO;
    }

    static NSCharacterSet* s_invalid_characters;
    static dispatch_once_t s_once_token;
    dispatch_once(&s_once_token, ^{
        s_invalid_characters = [[NSCharacterSet characterSetWithCharactersInString:@"0123456789abcdef"] invertedSet];
    });
    return [value rangeOfCharacterFromSet:s_invalid_characters].location == NSNotFound;
}

BOOL tg_is_nonempty_uuid(NSString* const value)
{
    assert(value != NULL);

    NSUUID* const uuid = [[NSUUID alloc] initWithUUIDString:value];
    if (uuid == nil) {
        return NO;
    }

    static NSUUID* s_empty_uuid;
    static dispatch_once_t s_once_token;
    dispatch_once(&s_once_token, ^{
        s_empty_uuid = [[NSUUID alloc] initWithUUIDString:@"00000000-0000-0000-0000-000000000000"];
    });
    return ![uuid isEqual:s_empty_uuid];
}

NSString* tg_get_required_plan_identifier(NSDictionary* const dictionary, NSString* const key)
{
    assert(dictionary != NULL);
    assert(key != NULL);

    NSString* const value = tg_get_required_string(dictionary, key);
    if (!tg_is_nonempty_uuid(value)) {
        tg_throw_invalid_request(@"eventkit_request_plan_id_invalid");
    }

    return [[[NSUUID alloc] initWithUUIDString:value] UUIDString].lowercaseString;
}

void tg_validate_legacy_migration_range(
    const long long term_starts_at_unix_seconds,
    const long long term_ends_at_unix_seconds,
    const long long migration_starts_at_unix_seconds,
    const long long migration_ends_at_unix_seconds)
{
    if (term_ends_at_unix_seconds < term_starts_at_unix_seconds || term_ends_at_unix_seconds == LLONG_MAX) {
        tg_throw_invalid_request(@"eventkit_request_term_range_invalid");
    }

    long long expected_migration_start;
    if (term_starts_at_unix_seconds < LLONG_MIN + TG_LEGACY_MIGRATION_PADDING_SECONDS) {
        expected_migration_start = LLONG_MIN;
    } else {
        expected_migration_start = term_starts_at_unix_seconds - TG_LEGACY_MIGRATION_PADDING_SECONDS;
    }
    const long long maximum_inclusive_end = LLONG_MAX - TG_INCLUSIVE_RANGE_END_OFFSET_SECONDS;
    long long expected_migration_end;
    if (term_ends_at_unix_seconds > maximum_inclusive_end - TG_LEGACY_MIGRATION_PADDING_SECONDS) {
        expected_migration_end = maximum_inclusive_end;
    } else {
        expected_migration_end = term_ends_at_unix_seconds + TG_LEGACY_MIGRATION_PADDING_SECONDS;
    }
    if (migration_starts_at_unix_seconds != expected_migration_start || migration_ends_at_unix_seconds != expected_migration_end) {
        tg_throw_invalid_request(@"eventkit_request_migration_range_invalid");
    }
}

NSArray<NSDictionary*>* tg_validate_recurring_events(NSDictionary* const request)
{
    assert(request != NULL);

    NSArray* const event_requests = tg_get_required_array(request, @"recurringEvents");
    if (event_requests.count == 0) {
        tg_throw_invalid_request(@"eventkit_request_events_empty");
    }

    NSMutableSet<NSString*>* const source_event_hashes = [NSMutableSet set];
    NSMutableSet<NSString*>* const fingerprints = [NSMutableSet set];
    NSMutableArray<NSDictionary*>* const validated_events = [NSMutableArray arrayWithCapacity:event_requests.count];
    for (id item in event_requests) {
        if (![item isKindOfClass:[NSDictionary class]]) {
            tg_throw_invalid_request(@"eventkit_request_event_invalid");
        }

        NSDictionary* const event = item;
        NSString* const source_event_hash = tg_get_required_hash(event, @"sourceEventHash");
        NSString* const fingerprint = tg_get_required_hash(event, @"fingerprint");
        NSString* const summary = tg_get_required_string(event, @"summary");
        NSString* const location = tg_get_optional_string(event, @"location");
        NSString* const notes = tg_get_optional_string(event, @"notes");
        const long long starts_at_unix_seconds = tg_get_required_integer(event, @"startsAtUnixSeconds");
        const long long ends_at_unix_seconds = tg_get_required_integer(event, @"endsAtUnixSeconds");
        const long long recurrence_ends_at_unix_seconds = tg_get_required_integer(event, @"recurrenceEndsAtUnixSeconds");
        NSString* const time_zone_identifier = tg_get_required_string(event, @"timeZoneIdentifier");
        NSArray* const weekday_values = tg_get_required_array(event, @"weekdays");

        const BOOL has_duplicate_source_event_hash = [source_event_hashes containsObject:source_event_hash];
        const BOOL has_invalid_time_range = ends_at_unix_seconds <= starts_at_unix_seconds || recurrence_ends_at_unix_seconds < starts_at_unix_seconds;
        const BOOL has_invalid_time_zone = [NSTimeZone timeZoneWithName:time_zone_identifier] == nil;
        const BOOL has_no_weekday = weekday_values.count == 0;
        if (has_duplicate_source_event_hash || has_invalid_time_range || has_invalid_time_zone || has_no_weekday) {
            tg_throw_invalid_request(@"eventkit_request_event_invalid");
        }
        if ([fingerprints containsObject:fingerprint]) {
            tg_throw_invalid_request(@"eventkit_request_event_fingerprint_duplicate");
        }
        [source_event_hashes addObject:source_event_hash];
        [fingerprints addObject:fingerprint];

        NSMutableSet<NSNumber*>* const unique_weekdays = [NSMutableSet set];
        NSMutableArray<NSNumber*>* const weekdays = [NSMutableArray arrayWithCapacity:weekday_values.count];
        for (id weekday_value in weekday_values) {
            if (![weekday_value isKindOfClass:[NSNumber class]] || tg_is_boolean_number(weekday_value)) {
                tg_throw_invalid_request(@"eventkit_request_weekday_invalid");
            }

            const NSInteger weekday = [weekday_value integerValue];
            if ([weekday_value doubleValue] != (double)weekday || weekday < EKWeekdaySunday || weekday > EKWeekdaySaturday || [unique_weekdays containsObject:@(weekday)]) {
                tg_throw_invalid_request(@"eventkit_request_weekday_invalid");
            }
            [unique_weekdays addObject:@(weekday)];
            [weekdays addObject:@(weekday)];
        }
        [weekdays sortUsingSelector:@selector(compare:)];

        [validated_events addObject:@{
            @"sourceEventHash" : source_event_hash,
            @"fingerprint" : fingerprint,
            @"summary" : summary,
            @"location" : location,
            @"notes" : notes,
            @"startsAtUnixSeconds" : @(starts_at_unix_seconds),
            @"endsAtUnixSeconds" : @(ends_at_unix_seconds),
            @"recurrenceEndsAtUnixSeconds" : @(recurrence_ends_at_unix_seconds),
            @"timeZoneIdentifier" : time_zone_identifier,
            @"weekdays" : weekdays
        }];
    }

    return validated_events;
}

NSArray<NSDictionary*>* tg_validate_desired_events(NSDictionary* const request)
{
    assert(request != NULL);

    NSArray* const event_requests = tg_get_required_array(request, @"desiredEvents");
    if (event_requests.count == 0) {
        tg_throw_invalid_request(@"eventkit_request_events_empty");
    }

    NSMutableSet<NSString*>* const source_event_hashes = [NSMutableSet setWithCapacity:event_requests.count];
    NSMutableSet<NSString*>* const fingerprints = [NSMutableSet setWithCapacity:event_requests.count];
    NSMutableArray<NSDictionary*>* const validated_events = [NSMutableArray arrayWithCapacity:event_requests.count];
    for (id item in event_requests) {
        if (![item isKindOfClass:[NSDictionary class]]) {
            tg_throw_invalid_request(@"eventkit_request_event_invalid");
        }

        NSDictionary* const event = item;
        NSString* const source_event_hash = tg_get_required_hash(event, @"sourceEventHash");
        NSString* const fingerprint = tg_get_required_hash(event, @"fingerprint");
        if ([source_event_hashes containsObject:source_event_hash]) {
            tg_throw_invalid_request(@"eventkit_request_event_invalid");
        }
        if ([fingerprints containsObject:fingerprint]) {
            tg_throw_invalid_request(@"eventkit_request_event_fingerprint_duplicate");
        }
        [source_event_hashes addObject:source_event_hash];
        [fingerprints addObject:fingerprint];
        [validated_events addObject:@{
            @"sourceEventHash" : source_event_hash,
            @"fingerprint" : fingerprint
        }];
    }
    return validated_events;
}

NSArray<NSDictionary*>* tg_validate_managed_events(NSDictionary* const request)
{
    assert(request != NULL);

    NSMutableSet<NSString*>* const source_event_hashes = [NSMutableSet set];
    NSMutableSet<NSString*>* const calendar_item_identifiers = [NSMutableSet set];
    NSMutableArray<NSDictionary*>* const validated_events = [NSMutableArray array];
    for (id item in tg_get_optional_array(request, @"managedEvents")) {
        if (![item isKindOfClass:[NSDictionary class]]) {
            tg_throw_invalid_request(@"eventkit_request_managed_event_invalid");
        }

        NSDictionary* const event = item;
        NSString* const source_event_hash = tg_get_required_hash(event, @"sourceEventHash");
        NSString* const calendar_item_identifier = tg_get_required_string(event, @"calendarItemIdentifier");
        NSString* const external_identifier = tg_get_optional_string(event, @"externalIdentifier");
        NSString* const fingerprint = tg_get_required_hash(event, @"fingerprint");
        if ([source_event_hashes containsObject:source_event_hash] || [calendar_item_identifiers containsObject:calendar_item_identifier]) {
            tg_throw_invalid_request(@"eventkit_request_managed_event_duplicate");
        }
        [source_event_hashes addObject:source_event_hash];
        [calendar_item_identifiers addObject:calendar_item_identifier];

        [validated_events addObject:@{
            @"sourceEventHash" : source_event_hash,
            @"calendarItemIdentifier" : calendar_item_identifier,
            @"externalIdentifier" : external_identifier,
            @"fingerprint" : fingerprint
        }];
    }

    return validated_events;
}

NSArray<NSDictionary*>* tg_validate_list_registrations(NSDictionary* const request)
{
    assert(request != NULL);

    NSMutableSet<NSString*>* const calendar_identifiers = [NSMutableSet set];
    NSMutableArray<NSDictionary*>* const validated_registrations = [NSMutableArray array];
    for (id item in tg_get_optional_array(request, @"registrations")) {
        if (![item isKindOfClass:[NSDictionary class]]) {
            tg_throw_invalid_request(@"eventkit_request_registration_invalid");
        }

        NSDictionary* const registration = item;
        NSString* const plan_identifier = tg_get_required_plan_identifier(registration, @"planId");
        NSString* const calendar_identifier = tg_get_required_string(registration, @"calendarIdentifier");
        NSString* const calendar_name = tg_get_required_string(registration, @"calendarName");
        NSString* const normalized_calendar_name = tg_get_required_string(registration, @"normalizedCalendarName");
        NSString* const source_identifier = tg_get_required_string(registration, @"sourceIdentifier");
        const long long term_starts_at_unix_seconds = tg_get_required_integer(registration, @"termStartsAtUnixSeconds");
        const long long term_ends_at_unix_seconds = tg_get_required_integer(registration, @"termEndsAtUnixSeconds");
        NSArray<NSDictionary*>* const managed_events = tg_validate_managed_events(registration);
        const BOOL has_duplicate_calendar_identifier = [calendar_identifiers containsObject:calendar_identifier];
        const BOOL has_mismatched_normalized_name = ![tg_normalize_calendar_name(calendar_name) isEqualToString:normalized_calendar_name];
        const BOOL has_invalid_term_range = term_ends_at_unix_seconds < term_starts_at_unix_seconds;
        const BOOL has_no_managed_event = managed_events.count == 0;
        if (has_duplicate_calendar_identifier || has_mismatched_normalized_name || has_invalid_term_range || has_no_managed_event) {
            tg_throw_invalid_request(@"eventkit_request_registration_invalid");
        }
        [calendar_identifiers addObject:calendar_identifier];

        [validated_registrations addObject:@{
            @"planId" : plan_identifier,
            @"calendarIdentifier" : calendar_identifier,
            @"calendarName" : calendar_name,
            @"normalizedCalendarName" : normalized_calendar_name,
            @"sourceIdentifier" : source_identifier,
            @"termStartsAtUnixSeconds" : @(term_starts_at_unix_seconds),
            @"termEndsAtUnixSeconds" : @(term_ends_at_unix_seconds),
            @"managedEvents" : managed_events
        }];
    }
    return validated_registrations;
}
