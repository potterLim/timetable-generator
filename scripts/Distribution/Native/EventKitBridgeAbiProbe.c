#include <dlfcn.h>
#include <stddef.h>
#include <stdint.h>
#include <stdio.h>
#include <string.h>

#define TG_EXPECTED_SCHEMA_VERSION (1U)

typedef enum tg_event_kit_probe_exit_code {
    TG_EVENT_KIT_PROBE_EXIT_CODE_SUCCESS = 0,
    TG_EVENT_KIT_PROBE_EXIT_CODE_ARGUMENT_INVALID = 10,
    TG_EVENT_KIT_PROBE_EXIT_CODE_LIBRARY_OPEN_FAILED = 11,
    TG_EVENT_KIT_PROBE_EXIT_CODE_SYMBOL_MISSING = 12,
    TG_EVENT_KIT_PROBE_EXIT_CODE_SCHEMA_UNSUPPORTED = 13,
    TG_EVENT_KIT_PROBE_EXIT_CODE_RESPONSE_MISSING = 14,
    TG_EVENT_KIT_PROBE_EXIT_CODE_RESPONSE_INVALID = 15
} tg_event_kit_probe_exit_code_t;

typedef uint32_t (*tg_schema_version_function_t)(void);
typedef char* (*tg_execute_function_t)(const uint8_t* const request_bytes_or_null, const size_t request_length);
typedef void (*tg_free_response_function_t)(char* const pa_response_or_null);

int main(const int argument_count, char* const argument_values[])
{
    if (argument_count != 2) {
        return TG_EVENT_KIT_PROBE_EXIT_CODE_ARGUMENT_INVALID;
    }

    void* const library_or_null = dlopen(argument_values[1], RTLD_LOCAL | RTLD_NOW);
    if (library_or_null == NULL) {
        fprintf(stderr, "%s\n", dlerror());
        return TG_EVENT_KIT_PROBE_EXIT_CODE_LIBRARY_OPEN_FAILED;
    }

    tg_schema_version_function_t const get_schema_version_or_null = (tg_schema_version_function_t)dlsym(library_or_null, "tg_eventkit_schema_version");
    tg_execute_function_t const execute_or_null = (tg_execute_function_t)dlsym(library_or_null, "tg_eventkit_execute");
    tg_free_response_function_t const free_response_or_null = (tg_free_response_function_t)dlsym(library_or_null, "tg_eventkit_free");
    if (get_schema_version_or_null == NULL || execute_or_null == NULL || free_response_or_null == NULL) {
        dlclose(library_or_null);
        return TG_EVENT_KIT_PROBE_EXIT_CODE_SYMBOL_MISSING;
    }
    if (get_schema_version_or_null() != TG_EXPECTED_SCHEMA_VERSION) {
        dlclose(library_or_null);
        return TG_EVENT_KIT_PROBE_EXIT_CODE_SCHEMA_UNSUPPORTED;
    }

    static const uint8_t INVALID_JSON[] = "not-json";
    char* pa_response_or_null = execute_or_null(INVALID_JSON, sizeof(INVALID_JSON) - 1U);
    if (pa_response_or_null == NULL) {
        dlclose(library_or_null);
        return TG_EVENT_KIT_PROBE_EXIT_CODE_RESPONSE_MISSING;
    }

    const int response_is_valid = strstr(pa_response_or_null, "invalid_request") != NULL;
    free_response_or_null(pa_response_or_null);
    pa_response_or_null = NULL;
    dlclose(library_or_null);
    if (response_is_valid != 0) {
        return TG_EVENT_KIT_PROBE_EXIT_CODE_SUCCESS;
    } else {
        return TG_EVENT_KIT_PROBE_EXIT_CODE_RESPONSE_INVALID;
    }
}
