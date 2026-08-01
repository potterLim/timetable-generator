#ifndef TIMETABLE_GENERATOR_EVENT_KIT_EXPORT_H
#define TIMETABLE_GENERATOR_EVENT_KIT_EXPORT_H

#import <EventKit/EventKit.h>
#import <Foundation/Foundation.h>

NSDictionary* tg_reconcile_export(NSDictionary* const request, EKEventStore* const event_store);
NSDictionary* tg_apply_export(NSDictionary* const request, EKEventStore* const event_store);

#endif /* TIMETABLE_GENERATOR_EVENT_KIT_EXPORT_H */
