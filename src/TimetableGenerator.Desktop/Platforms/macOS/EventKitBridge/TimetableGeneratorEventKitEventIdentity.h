#ifndef TIMETABLE_GENERATOR_EVENT_KIT_EVENT_IDENTITY_H
#define TIMETABLE_GENERATOR_EVENT_KIT_EVENT_IDENTITY_H

#import <EventKit/EventKit.h>
#import <Foundation/Foundation.h>

extern const NSInteger TG_WEEKLY_RECURRENCE_INTERVAL;

NSDate* tg_get_date_from_unix_seconds(const long long seconds);
NSString* tg_get_calendar_source_identifier(EKCalendar* const calendar_or_null);
NSString* tg_get_calendar_identifier(EKCalendar* const calendar_or_null);

NSArray<EKEvent*>* tg_get_events_in_calendar(
    EKEventStore* const event_store,
    EKCalendar* const calendar,
    const long long starts_at_unix_seconds,
    const long long ends_at_unix_seconds);

NSDictionary* tg_create_legacy_ownership_snapshot(
    EKEventStore* const event_store,
    EKCalendar* const calendar,
    NSString* const requested_plan_identifier,
    const long long starts_at_unix_seconds,
    const long long ends_at_unix_seconds);

NSString* tg_get_fingerprint_for_event_or_null(EKEvent* const event);
NSDictionary<NSString*, NSDictionary<NSString*, EKEvent*>*>* tg_index_events_by_fingerprint(NSArray<EKEvent*>* const events);
NSArray<EKEvent*>* tg_resolve_unique_fingerprint_events_or_null(NSArray<NSDictionary*>* const event_requests, NSDictionary<NSString*, NSDictionary<NSString*, EKEvent*>*>* const fingerprint_index);
NSSet<NSString*>* tg_get_event_identifier_set_or_null(NSArray<EKEvent*>* const events);
BOOL tg_does_calendar_contain_only_resolved_series(NSArray<EKEvent*>* const calendar_events, NSArray<EKEvent*>* const resolved_events);
BOOL tg_are_events_created_near(NSArray<EKEvent*>* const events, const long long prepared_at_unix_seconds);

NSArray<EKEvent*>* tg_get_managed_legacy_events(
    EKEventStore* const event_store,
    EKCalendar* const calendar,
    NSString* const plan_identifier,
    const long long starts_at_unix_seconds,
    const long long ends_at_unix_seconds);

NSSet<NSString*>* tg_get_registered_event_candidate_identifiers(EKEventStore* const event_store, EKCalendar* const calendar, NSArray<NSDictionary*>* const managed_events);
BOOL tg_does_index_contain_candidate_outside_identifiers(NSArray<NSDictionary*>* const event_requests, NSDictionary<NSString*, NSDictionary<NSString*, EKEvent*>*>* const fingerprint_index, NSSet<NSString*>* const allowed_identifiers);

BOOL tg_does_any_calendar_contain_recent_desired_candidate(
    EKEventStore* const event_store,
    NSArray<NSDictionary*>* const event_requests,
    const long long term_starts_at_unix_seconds,
    const long long term_ends_at_unix_seconds,
    const long long prepared_at_unix_seconds);

EKCalendar* tg_resolve_pending_committed_calendar_after_identifier_change_or_null(
    EKEventStore* const event_store,
    NSArray<EKCalendar*>* const calendars,
    NSString* const normalized_calendar_name,
    NSString* const source_identifier,
    NSArray<NSDictionary*>* const desired_event_requests,
    const long long term_starts_at_unix_seconds,
    const long long term_ends_at_unix_seconds,
    const long long prepared_at_unix_seconds);

#endif /* TIMETABLE_GENERATOR_EVENT_KIT_EVENT_IDENTITY_H */
