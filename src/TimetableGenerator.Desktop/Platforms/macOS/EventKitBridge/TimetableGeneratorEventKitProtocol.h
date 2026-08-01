#ifndef TIMETABLE_GENERATOR_EVENT_KIT_PROTOCOL_H
#define TIMETABLE_GENERATOR_EVENT_KIT_PROTOCOL_H

#import <Foundation/Foundation.h>

#include <stdint.h>

extern const uint32_t TG_SCHEMA_VERSION;
extern const int64_t TG_INCLUSIVE_RANGE_END_OFFSET_SECONDS;

extern NSString* const TG_INVALID_REQUEST_EXCEPTION;

extern NSString* const TG_STATUS_OK;
extern NSString* const TG_STATUS_ACCESS_DENIED;
extern NSString* const TG_STATUS_CALENDAR_CHANGED;
extern NSString* const TG_STATUS_INVALID_REQUEST;
extern NSString* const TG_STATUS_NOT_FOUND;
extern NSString* const TG_STATUS_OPERATION_FAILED;

NSDictionary* tg_create_response(NSString* const status, NSString* const diagnostic_code);
void tg_throw_invalid_request(NSString* const diagnostic_code);

NSString* tg_get_required_string(NSDictionary* const dictionary, NSString* const key);
NSString* tg_get_optional_string(NSDictionary* const dictionary, NSString* const key);
long long tg_get_required_integer(NSDictionary* const dictionary, NSString* const key);
NSString* tg_normalize_text(NSString* const value);
NSString* tg_normalize_calendar_name(NSString* const value);
BOOL tg_is_lowercase_sha256(NSString* const value);
BOOL tg_is_nonempty_uuid(NSString* const value);
NSString* tg_get_required_plan_identifier(NSDictionary* const dictionary, NSString* const key);

void tg_validate_legacy_migration_range(
    const long long term_starts_at_unix_seconds,
    const long long term_ends_at_unix_seconds,
    const long long migration_starts_at_unix_seconds,
    const long long migration_ends_at_unix_seconds);

NSArray<NSDictionary*>* tg_validate_recurring_events(NSDictionary* const request);
NSArray<NSDictionary*>* tg_validate_desired_events(NSDictionary* const request);
NSArray<NSDictionary*>* tg_validate_managed_events(NSDictionary* const request);
NSArray<NSDictionary*>* tg_validate_list_registrations(NSDictionary* const request);

#endif /* TIMETABLE_GENERATOR_EVENT_KIT_PROTOCOL_H */
