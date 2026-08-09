#import <EventKit/EventKit.h>
#import <Foundation/Foundation.h>

#include <assert.h>
#include <stdlib.h>
#include <string.h>

#import "TimetableGeneratorEventKitBridge.h"
#import "TimetableGeneratorEventKitExport.h"
#import "TimetableGeneratorEventKitProtocol.h"
#import "TimetableGeneratorEventKitRegistration.h"

#define TG_NULL_TERMINATOR_BYTE_COUNT (1U)

static const uint32_t TG_ABI_VERSION = 1U;
static const size_t TG_MAXIMUM_REQUEST_BYTE_COUNT = 8 * 1024 * 1024;
static const int64_t TG_CALENDAR_ACCESS_WAIT_SLICE_NANOSECONDS = 100LL * NSEC_PER_MSEC;
static const NSUInteger TG_CALENDAR_ACCESS_WAIT_SLICE_COUNT = 60U * 10U;

typedef NS_ENUM(NSInteger, tg_calendar_access_result_t) {
    TG_CALENDAR_ACCESS_RESULT_GRANTED,
    TG_CALENDAR_ACCESS_RESULT_DENIED,
    TG_CALENDAR_ACCESS_RESULT_CANCELLED,
    TG_CALENDAR_ACCESS_RESULT_TIMED_OUT,
    TG_CALENDAR_ACCESS_RESULT_FAILED
};

static char* tg_copy_utf8_string_malloc(NSString* const value)
{
    assert(value != NULL);

    NSData* const data = [value dataUsingEncoding:NSUTF8StringEncoding];
    if (data == nil || data.length >= SIZE_MAX) {
        return NULL;
    }

    char* const pa_utf8_string = malloc(data.length + TG_NULL_TERMINATOR_BYTE_COUNT);
    if (pa_utf8_string == NULL) {
        return NULL;
    }

    if (data.length > 0) {
        memcpy(pa_utf8_string, data.bytes, data.length);
    }
    pa_utf8_string[data.length] = '\0';
    return pa_utf8_string;
}

static char* tg_copy_json_response_malloc(NSDictionary* const response)
{
    assert(response != NULL);

    NSError* serialization_error = nil;
    NSData* const response_data = [NSJSONSerialization dataWithJSONObject:response options:0 error:&serialization_error];
    if (response_data == nil || serialization_error != nil) {
        NSString* const fallback_response = [NSString stringWithFormat:@"{\"schemaVersion\":%u,\"status\":\"operation_failed\",\"diagnosticCode\":\"eventkit_response_serialization_failed\"}", (unsigned int)TG_SCHEMA_VERSION];
        return tg_copy_utf8_string_malloc(fallback_response);
    }

    if (response_data.length >= SIZE_MAX) {
        return NULL;
    }

    char* const pa_json_response = malloc(response_data.length + TG_NULL_TERMINATOR_BYTE_COUNT);
    if (pa_json_response == NULL) {
        return NULL;
    }

    if (response_data.length > 0) {
        memcpy(pa_json_response, response_data.bytes, response_data.length);
    }
    pa_json_response[response_data.length] = '\0';
    return pa_json_response;
}

static BOOL tg_is_cancellation_requested(
    const tg_eventkit_is_cancelled_callback_t is_cancelled_or_null,
    void* const p_cancellation_context_or_null)
{
    if (is_cancelled_or_null == NULL) {
        return NO;
    }

    return is_cancelled_or_null(p_cancellation_context_or_null) != 0;
}

static tg_calendar_access_result_t tg_request_calendar_access(
    EKEventStore* const event_store,
    const tg_eventkit_is_cancelled_callback_t is_cancelled_or_null,
    void* const p_cancellation_context_or_null)
{
    assert(event_store != NULL);

    if (tg_is_cancellation_requested(is_cancelled_or_null, p_cancellation_context_or_null)) {
        return TG_CALENDAR_ACCESS_RESULT_CANCELLED;
    }

    const EKAuthorizationStatus status = [EKEventStore authorizationStatusForEntityType:EKEntityTypeEvent];
    if (status == EKAuthorizationStatusFullAccess) {
        return TG_CALENDAR_ACCESS_RESULT_GRANTED;
    }

    if (status != EKAuthorizationStatusNotDetermined) {
        return TG_CALENDAR_ACCESS_RESULT_DENIED;
    }

    const dispatch_semaphore_t completion_semaphore = dispatch_semaphore_create(0);
    __block BOOL granted = NO;
    __block NSError* request_error = nil;
    [event_store requestFullAccessToEventsWithCompletion:^(const BOOL request_granted, NSError* const error_or_null) {
        granted = request_granted;
        request_error = error_or_null;
        dispatch_semaphore_signal(completion_semaphore);
    }];
    for (NSUInteger wait_slice_index = 0; wait_slice_index < TG_CALENDAR_ACCESS_WAIT_SLICE_COUNT; ++wait_slice_index) {
        const long wait_result = dispatch_semaphore_wait(completion_semaphore, dispatch_time(DISPATCH_TIME_NOW, TG_CALENDAR_ACCESS_WAIT_SLICE_NANOSECONDS));
        if (wait_result == 0) {
            if (granted) {
                return TG_CALENDAR_ACCESS_RESULT_GRANTED;
            }

            if (request_error == nil) {
                return TG_CALENDAR_ACCESS_RESULT_DENIED;
            }

            return TG_CALENDAR_ACCESS_RESULT_FAILED;
        }

        if (tg_is_cancellation_requested(is_cancelled_or_null, p_cancellation_context_or_null)) {
            return TG_CALENDAR_ACCESS_RESULT_CANCELLED;
        }
    }

    return TG_CALENDAR_ACCESS_RESULT_TIMED_OUT;
}

static NSDictionary* tg_get_calendar_access_failure(const tg_calendar_access_result_t result)
{
    if (result == TG_CALENDAR_ACCESS_RESULT_DENIED) {
        return tg_create_response(TG_STATUS_ACCESS_DENIED, @"eventkit_calendar_access_denied");
    }
    if (result == TG_CALENDAR_ACCESS_RESULT_CANCELLED) {
        return tg_create_response(TG_STATUS_OPERATION_FAILED, @"eventkit_calendar_access_request_cancelled");
    }
    if (result == TG_CALENDAR_ACCESS_RESULT_TIMED_OUT) {
        return tg_create_response(TG_STATUS_OPERATION_FAILED, @"eventkit_calendar_access_request_timed_out");
    }

    return tg_create_response(TG_STATUS_OPERATION_FAILED, @"eventkit_calendar_access_request_failed");
}

static NSDictionary* tg_execute_request(
    NSDictionary* const request,
    const tg_eventkit_is_cancelled_callback_t is_cancelled_or_null,
    void* const p_cancellation_context_or_null)
{
    assert(request != NULL);

    const long long schema_version = tg_get_required_integer(request, @"schemaVersion");
    if (schema_version != TG_SCHEMA_VERSION) {
        tg_throw_invalid_request(@"eventkit_request_schema_version_unsupported");
    }

    NSString* const operation = tg_get_required_string(request, @"operation");
    if (![operation isEqualToString:@"list"] && ![operation isEqualToString:@"apply"] && ![operation isEqualToString:@"reconcile"]) {
        tg_throw_invalid_request(@"eventkit_request_operation_unsupported");
    }

    EKEventStore* const event_store = [[EKEventStore alloc] init];
    const tg_calendar_access_result_t access_result = tg_request_calendar_access(event_store, is_cancelled_or_null, p_cancellation_context_or_null);
    if (access_result != TG_CALENDAR_ACCESS_RESULT_GRANTED) {
        return tg_get_calendar_access_failure(access_result);
    }

    if ([operation isEqualToString:@"list"]) {
        return tg_list_calendars(request, event_store);
    }
    if ([operation isEqualToString:@"apply"]) {
        return tg_apply_export(request, event_store);
    }
    if ([operation isEqualToString:@"reconcile"]) {
        return tg_reconcile_export(request, event_store);
    }

    tg_throw_invalid_request(@"eventkit_request_operation_unsupported");
}

uint32_t tg_eventkit_schema_version(void)
{
    return TG_SCHEMA_VERSION;
}

uint32_t tg_eventkit_abi_version(void)
{
    return TG_ABI_VERSION;
}

char* tg_eventkit_execute_cancellable(
    const uint8_t* const request_bytes_or_null,
    const size_t request_length,
    const tg_eventkit_is_cancelled_callback_t is_cancelled_or_null,
    void* const p_cancellation_context_or_null)
{
    @autoreleasepool {
        if (request_bytes_or_null == NULL || request_length == 0 || request_length > TG_MAXIMUM_REQUEST_BYTE_COUNT) {
            return tg_copy_json_response_malloc(tg_create_response(TG_STATUS_INVALID_REQUEST, @"eventkit_request_size_invalid"));
        }

        @try {
            NSData* const request_data = [NSData dataWithBytes:request_bytes_or_null length:request_length];
            NSError* parse_error = nil;
            const id request_object = [NSJSONSerialization JSONObjectWithData:request_data options:0 error:&parse_error];
            if (![request_object isKindOfClass:[NSDictionary class]] || parse_error != nil) {
                return tg_copy_json_response_malloc(tg_create_response(TG_STATUS_INVALID_REQUEST, @"eventkit_request_json_invalid"));
            }

            return tg_copy_json_response_malloc(tg_execute_request(request_object, is_cancelled_or_null, p_cancellation_context_or_null));
        } @catch (NSException* exception) {
            if ([exception.name isEqualToString:TG_INVALID_REQUEST_EXCEPTION]) {
                NSString* const diagnostic_code_or_null = exception.reason;
                if (diagnostic_code_or_null == nil) {
                    return tg_copy_json_response_malloc(tg_create_response(TG_STATUS_INVALID_REQUEST, @"eventkit_request_invalid"));
                }

                return tg_copy_json_response_malloc(tg_create_response(TG_STATUS_INVALID_REQUEST, diagnostic_code_or_null));
            }
            return tg_copy_json_response_malloc(tg_create_response(TG_STATUS_OPERATION_FAILED, @"eventkit_native_exception"));
        }
    }
}

char* tg_eventkit_execute(const uint8_t* const request_bytes_or_null, const size_t request_length)
{
    return tg_eventkit_execute_cancellable(request_bytes_or_null, request_length, NULL, NULL);
}

void tg_eventkit_free(char* const pa_response_or_null)
{
    free(pa_response_or_null);
}
