#include <stddef.h>
#include <stdint.h>
#include <stdlib.h>
#include <string.h>

#ifndef TG_EVENT_KIT_TEST_SCHEMA_VERSION
#define TG_EVENT_KIT_TEST_SCHEMA_VERSION (1U)
#endif

uint32_t tg_eventkit_schema_version(void)
{
    return TG_EVENT_KIT_TEST_SCHEMA_VERSION;
}

char* tg_eventkit_execute(const uint8_t* const request_bytes_or_null, const size_t request_length)
{
    (void)request_bytes_or_null;
    (void)request_length;

    static const char RESPONSE_JSON[] = "{\"schemaVersion\":1,\"status\":\"invalid_request\"}";
    char* pa_response_or_null = malloc(sizeof(RESPONSE_JSON));
    if (pa_response_or_null != NULL) {
        memcpy(pa_response_or_null, RESPONSE_JSON, sizeof(RESPONSE_JSON));
    }
    return pa_response_or_null;
}

void tg_eventkit_free(char* const pa_response_or_null)
{
    free(pa_response_or_null);
}
