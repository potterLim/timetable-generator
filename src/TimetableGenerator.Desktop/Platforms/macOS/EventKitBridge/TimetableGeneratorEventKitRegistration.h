#ifndef TIMETABLE_GENERATOR_EVENT_KIT_REGISTRATION_H
#define TIMETABLE_GENERATOR_EVENT_KIT_REGISTRATION_H

#import <EventKit/EventKit.h>
#import <Foundation/Foundation.h>

NSDictionary* tg_list_calendars(NSDictionary* const request, EKEventStore* const event_store);
NSDictionary* tg_create_registration_binding_or_null(NSDictionary* const registration, EKCalendar* const calendar, NSArray<EKEvent*>* const resolved_events);
BOOL tg_does_registration_match_resolved_events(NSArray<NSDictionary*>* const managed_events, NSArray<EKEvent*>* const resolved_events);

NSDictionary* tg_resolve_rebound_registration_or_null(
    EKEventStore* const event_store,
    NSArray<EKCalendar*>* const calendars,
    NSDictionary* const registration,
    NSUInteger* const out_candidate_count);

EKCalendar* tg_find_calendar_or_null(EKEventStore* const event_store, NSString* const identifier);
EKSource* tg_find_source_for_new_calendar_or_null(EKEventStore* const event_store);

NSArray<EKEvent*>* tg_resolve_registered_events_or_null(
    EKEventStore* const event_store,
    EKCalendar* const calendar,
    NSArray<NSDictionary*>* const managed_events,
    const long long term_starts_at_unix_seconds,
    const long long term_ends_at_unix_seconds);

#endif /* TIMETABLE_GENERATOR_EVENT_KIT_REGISTRATION_H */
