#include <stddef.h>
#include <stdint.h>
#include <stdlib.h>
#include <string.h>

#ifndef TG_EVENT_KIT_TEST_SCHEMA_VERSION
#define TG_EVENT_KIT_TEST_SCHEMA_VERSION (1U)
#endif

#ifndef TG_EVENT_KIT_TEST_ABI_VERSION
#define TG_EVENT_KIT_TEST_ABI_VERSION (1U)
#endif

typedef int32_t (*tg_eventkit_is_cancelled_callback_t)(void* const p_context_or_null);

uint32_t tg_eventkit_abi_version(void)
{
    return TG_EVENT_KIT_TEST_ABI_VERSION;
}

uint32_t tg_eventkit_schema_version(void)
{
    return TG_EVENT_KIT_TEST_SCHEMA_VERSION;
}

char* tg_eventkit_execute(const uint8_t* const request_bytes_or_null, const size_t request_length)
{
    (void)request_bytes_or_null;
    (void)request_length;

    static const char RESPONSE_JSON[] = "{\"schemaVersion\":1,\"status\":\"invalid_request\"}";
    char* const pa_response_or_null = malloc(sizeof(RESPONSE_JSON));
    if (pa_response_or_null == NULL) {
        return NULL;
    }

    memcpy(pa_response_or_null, RESPONSE_JSON, sizeof(RESPONSE_JSON));
    return pa_response_or_null;
}

char* tg_eventkit_execute_cancellable(
    const uint8_t* const request_bytes_or_null,
    const size_t request_length,
    const tg_eventkit_is_cancelled_callback_t is_cancelled_or_null,
    void* const p_cancellation_context_or_null)
{
    if (is_cancelled_or_null != NULL && is_cancelled_or_null(p_cancellation_context_or_null) != 0) {
        static const char CANCELLED_RESPONSE_JSON[] = "{\"schemaVersion\":1,\"status\":\"operation_failed\",\"diagnosticCode\":\"eventkit_calendar_access_request_cancelled\"}";
        char* const pa_cancelled_response_or_null = malloc(sizeof(CANCELLED_RESPONSE_JSON));
        if (pa_cancelled_response_or_null == NULL) {
            return NULL;
        }

        memcpy(pa_cancelled_response_or_null, CANCELLED_RESPONSE_JSON, sizeof(CANCELLED_RESPONSE_JSON));
        return pa_cancelled_response_or_null;
    }

    return tg_eventkit_execute(request_bytes_or_null, request_length);
}

void tg_eventkit_free(char* const pa_response_or_null)
{
    free(pa_response_or_null);
}
