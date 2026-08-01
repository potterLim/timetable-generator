#ifndef TIMETABLE_GENERATOR_EVENT_KIT_BRIDGE_H
#define TIMETABLE_GENERATOR_EVENT_KIT_BRIDGE_H

#include <stddef.h>
#include <stdint.h>

#if defined(__cplusplus)
extern "C" {
#endif

#if defined(__GNUC__)
#define TG_EVENT_KIT_EXPORT __attribute__((visibility("default")))
#else
#define TG_EVENT_KIT_EXPORT
#endif /* __GNUC__ */

TG_EVENT_KIT_EXPORT uint32_t tg_eventkit_schema_version(void);

/*
 * Executes one UTF-8 JSON request and returns a malloc-owned, NUL-terminated
 * UTF-8 JSON response. The caller must release every non-NULL response with
 * tg_eventkit_free. The response must not be released with a managed allocator.
 */
TG_EVENT_KIT_EXPORT char* tg_eventkit_execute(const uint8_t* const request_bytes_or_null, const size_t request_length);

TG_EVENT_KIT_EXPORT void tg_eventkit_free(char* const pa_response_or_null);

#if defined(__cplusplus)
}
#endif /* __cplusplus */

#endif /* TIMETABLE_GENERATOR_EVENT_KIT_BRIDGE_H */
