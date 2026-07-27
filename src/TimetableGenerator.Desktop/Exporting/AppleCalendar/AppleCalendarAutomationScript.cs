namespace TimetableGenerator.Desktop.Exporting.AppleCalendar;

internal static class AppleCalendarAutomationScript
{
    public const string SOURCE = """
        ObjC.import("Foundation");

        function readRequest(path) {
            const data = $.NSData.dataWithContentsOfFile(path);
            if (!data) {
                throw new Error("request_file_unreadable");
            }

            const text = $.NSString.alloc.initWithDataEncoding(
                data,
                $.NSUTF8StringEncoding);
            if (!text) {
                throw new Error("request_file_not_utf8");
            }

            return JSON.parse(ObjC.unwrap(text));
        }

        function canonicalName(value) {
            return String(value).trim().normalize("NFC").toUpperCase();
        }

        function calendarName(calendar) {
            return String(calendar.name());
        }

        function calendarDescription(calendar) {
            const value = calendar.description();
            return value === null || value === undefined ? "" : String(value);
        }

        function calendarIsWritable(calendar) {
            try {
                return Boolean(calendar.writable());
            } catch (_) {
                return false;
            }
        }

        function validPlanIdOrNull(value) {
            const normalizedValue = String(value).toLowerCase();
            const planIdPattern = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/;
            return planIdPattern.test(normalizedValue)
                    && normalizedValue !== "00000000-0000-0000-0000-000000000000"
                ? normalizedValue
                : null;
        }

        function legacyCalendarManagedPlanId(calendar, markerPrefix) {
            const description = calendarDescription(calendar);
            if (description.indexOf(markerPrefix) !== 0) {
                return null;
            }

            const planId = description.substring(markerPrefix.length);
            return validPlanIdOrNull(planId);
        }

        function currentEventManagedPlanId(url, markerPrefix) {
            if (url.indexOf(markerPrefix) !== 0) {
                return null;
            }

            const markerPayload = url.substring(markerPrefix.length);
            const separatorIndex = markerPayload.indexOf("/");
            if (separatorIndex <= 0
                || /^[0-9a-f]{64}$/.test(
                    markerPayload.substring(separatorIndex + 1)) === false) {
                return null;
            }

            return validPlanIdOrNull(
                markerPayload.substring(0, separatorIndex));
        }

        function calendarManagedPlanId(calendar, request) {
            if (canonicalName(calendarName(calendar))
                !== request.normalizedDestinationName) {
                return null;
            }

            const legacyPlanId = legacyCalendarManagedPlanId(
                calendar,
                request.ownershipMarkerPrefix);
            let eventPlanId = null;
            const events = calendar.events();
            for (let index = 0; index < events.length; index += 1) {
                const candidatePlanId = currentEventManagedPlanId(
                    eventUrl(events[index]),
                    request.eventOwnershipMarkerPrefix);
                if (candidatePlanId === null) {
                    continue;
                }

                if (eventPlanId !== null
                    && eventPlanId !== candidatePlanId) {
                    return null;
                }

                eventPlanId = candidatePlanId;
            }

            if (legacyPlanId !== null
                && eventPlanId !== null
                && legacyPlanId !== eventPlanId) {
                return null;
            }

            return eventPlanId === null
                ? legacyPlanId
                : eventPlanId;
        }

        function calendarIsManagedByPlan(calendar, request) {
            return calendarManagedPlanId(calendar, request)
                === request.planId;
        }

        function managedCalendarId(calendar, request) {
            const planId = calendarManagedPlanId(calendar, request);
            return managedCalendarIdForPlanId(calendar, planId);
        }

        function managedCalendarIdForPlanId(calendar, planId) {
            return planId === null
                ? null
                : "managed:"
                    + planId
                    + ":"
                    + encodeURIComponent(canonicalName(calendarName(calendar)));
        }

        function calendarSnapshotId(calendar, index, managedPlanId) {
            const managedId = managedCalendarIdForPlanId(
                calendar,
                managedPlanId);
            return managedId === null
                ? "external:" + String(index)
                : managedId;
        }

        function createCalendarSnapshot(calendars, request) {
            const ids = [];
            const managedPlanIds = [];
            for (let index = 0; index < calendars.length; index += 1) {
                const managedPlanId = calendarManagedPlanId(
                    calendars[index],
                    request);
                managedPlanIds.push(managedPlanId);
                ids.push(calendarSnapshotId(
                    calendars[index],
                    index,
                    managedPlanId));
            }

            const uniqueIds = [];
            for (let index = 0; index < ids.length; index += 1) {
                let duplicateCount = 0;
                for (let candidateIndex = 0; candidateIndex < ids.length; candidateIndex += 1) {
                    if (ids[candidateIndex] === ids[index]) {
                        duplicateCount += 1;
                    }
                }

                uniqueIds.push(duplicateCount === 1
                    ? ids[index]
                    : "ambiguous:"
                        + String(index)
                        + ":"
                        + ids[index]);
            }

            return {
                ids: uniqueIds,
                managedPlanIds: managedPlanIds,
            };
        }

        function findManagedCalendarById(calendars, id, request) {
            let match = null;
            for (let index = 0; index < calendars.length; index += 1) {
                const calendar = calendars[index];
                if (managedCalendarId(calendar, request) !== id) {
                    continue;
                }

                if (match !== null) {
                    return null;
                }

                match = calendar;
            }

            return match;
        }

        function findCalendarsByName(calendars, normalizedName) {
            const matches = [];
            for (let index = 0; index < calendars.length; index += 1) {
                if (canonicalName(calendarName(calendars[index])) === normalizedName) {
                    matches.push(calendars[index]);
                }
            }

            return matches;
        }

        function calendarTargetIsValid(
            calendars,
            target,
            expectedCalendarId,
            request) {
            if (target === null) {
                return false;
            }

            const matchingCalendars = findCalendarsByName(
                calendars,
                request.normalizedDestinationName);
            return matchingCalendars.length === 1
                && managedCalendarId(
                    matchingCalendars[0],
                    request) === expectedCalendarId
                && managedCalendarId(
                    target,
                    request) === expectedCalendarId
                && calendarIsManagedByPlan(
                    target,
                    request)
                && calendarIsWritable(target);
        }

        function replacementTargetIsValid(calendars, target, request) {
            return calendarTargetIsValid(
                calendars,
                target,
                request.existingCalendarId,
                request);
        }

        function eventUrl(event) {
            try {
                const value = event.url();
                return value === null || value === undefined
                    ? ""
                    : String(value);
            } catch (_) {
                return "";
            }
        }

        function eventUrlOrThrow(event) {
            const value = event.url();
            return value === null || value === undefined
                ? ""
                : String(value);
        }

        function legacyEventUrlIsManaged(url, markerPrefix) {
            return url.indexOf(markerPrefix) === 0
                && /^[0-9a-f]{64}$/.test(
                    url.substring(markerPrefix.length));
        }

        function eventUrlIsManaged(url, request) {
            return currentEventManagedPlanId(
                    url,
                    request.eventOwnershipMarkerPrefix) !== null
                || legacyEventUrlIsManaged(
                    url,
                    request.legacyEventOwnershipMarkerPrefix);
        }

        function createOperationEventUrl(request) {
            const firstPart = String(
                ObjC.unwrap($.NSUUID.UUID.UUIDString))
                .replace(/-/g, "")
                .toLowerCase();
            const secondPart = String(
                ObjC.unwrap($.NSUUID.UUID.UUIDString))
                .replace(/-/g, "")
                .toLowerCase();
            return request.eventOwnershipMarkerPrefix
                + request.planId
                + "/"
                + firstPart
                + secondPart;
        }

        function createManagedEventIndex(calendar, request) {
            const entries = [];
            const countsByUrl = Object.create(null);
            const uniqueEventsByUrl = Object.create(null);
            const events = calendar.events();
            for (let index = 0; index < events.length; index += 1) {
                const event = events[index];
                const url = eventUrlOrThrow(event);
                if (eventUrlIsManaged(url, request) === false) {
                    continue;
                }

                entries.push({
                    event: event,
                    url: url,
                });
                const nextCount = (countsByUrl[url] || 0) + 1;
                countsByUrl[url] = nextCount;
                if (nextCount === 1) {
                    uniqueEventsByUrl[url] = event;
                } else {
                    uniqueEventsByUrl[url] = null;
                }
            }

            return {
                entries: entries,
                countsByUrl: countsByUrl,
                uniqueEventsByUrl: uniqueEventsByUrl,
            };
        }

        function managedEventCount(index, url) {
            return index.countsByUrl[url] || 0;
        }

        function uniqueManagedEvent(index, url) {
            return managedEventCount(index, url) === 1
                ? index.uniqueEventsByUrl[url]
                : null;
        }

        function managedEventIndexMatchesExpectedUrls(
            index,
            expectedUrls) {
            if (index.entries.length !== expectedUrls.length) {
                return false;
            }

            const expectedCounts = Object.create(null);
            for (let urlIndex = 0; urlIndex < expectedUrls.length; urlIndex += 1) {
                const url = expectedUrls[urlIndex];
                expectedCounts[url] = (expectedCounts[url] || 0) + 1;
            }

            for (const url in expectedCounts) {
                if (managedEventCount(index, url)
                    !== expectedCounts[url]) {
                    return false;
                }
            }

            return true;
        }

        function findValidatedCalendarSnapshot(
            calendarApplication,
            expectedCalendarId,
            request) {
            const calendars = calendarApplication.calendars();
            const matchingCalendars = findCalendarsByName(
                calendars,
                request.normalizedDestinationName);
            if (matchingCalendars.length !== 1) {
                return null;
            }

            const calendar = matchingCalendars[0];
            if (calendarTargetIsValid(
                    calendars,
                    calendar,
                    expectedCalendarId,
                    request) === false) {
                return null;
            }

            return {
                calendar: calendar,
                eventIndex: createManagedEventIndex(
                    calendar,
                    request),
            };
        }

        function findOperationEventProof(
            calendarApplication,
            operationUrl,
            expectedCalendarId,
            request) {
            const snapshot = findValidatedCalendarSnapshot(
                calendarApplication,
                expectedCalendarId,
                request);
            if (snapshot === null) {
                return null;
            }

            const event = uniqueManagedEvent(
                snapshot.eventIndex,
                operationUrl);
            return event === null
                ? null
                : {
                    calendar: snapshot.calendar,
                    event: event,
                    eventIndex: snapshot.eventIndex,
                };
        }

        function createOperationCanaryEvent(
            calendarApplication,
            calendar,
            request) {
            const operationUrl = createOperationEventUrl(
                request);
            createEvent(
                calendarApplication,
                calendar,
                {
                    summary: "Timetable Generator operation marker",
                    location: "",
                    description: "Temporary managed operation marker.",
                    startsAt: "2001-01-01T00:00:00Z",
                    endsAt: "2001-01-01T00:01:00Z",
                    ownershipUrl: operationUrl,
                });
            return operationUrl;
        }

        function deleteOperationCanaryAndConfirm(
            calendarApplication,
            operationUrl,
            expectedCalendarId,
            request,
            expectedRemainingUrls) {
            const proof = findOperationEventProof(
                calendarApplication,
                operationUrl,
                expectedCalendarId,
                request);
            if (proof === null) {
                return false;
            }

            try {
                proof.event.delete();
            } catch (_) {
                return false;
            }

            const finalSnapshot = findValidatedCalendarSnapshot(
                calendarApplication,
                expectedCalendarId,
                request);
            return finalSnapshot !== null
                && managedEventIndexMatchesExpectedUrls(
                    finalSnapshot.eventIndex,
                    expectedRemainingUrls);
        }

        function publishCalendarDescriptionAndConfirm(
            calendarApplication,
            expectedCalendarId,
            request,
            expectedEventUrls) {
            const snapshot = findValidatedCalendarSnapshot(
                calendarApplication,
                expectedCalendarId,
                request);
            if (snapshot === null) {
                return false;
            }

            try {
                snapshot.calendar.description = request.calendarDescription;
            } catch (_) {
                return false;
            }

            const confirmedSnapshot = findValidatedCalendarSnapshot(
                calendarApplication,
                expectedCalendarId,
                request);
            return confirmedSnapshot !== null
                && calendarDescription(
                    confirmedSnapshot.calendar)
                    === request.calendarDescription
                && managedEventIndexMatchesExpectedUrls(
                    confirmedSnapshot.eventIndex,
                    expectedEventUrls);
        }

        function createReplacementEvents(
            calendarApplication,
            calendar,
            events,
            request) {
            const mappings = [];
            for (let index = 0; index < events.length; index += 1) {
                const eventData = events[index];
                const operationUrl = createOperationEventUrl(
                    request);
                createEvent(
                    calendarApplication,
                    calendar,
                    {
                        summary: eventData.summary,
                        location: eventData.location,
                        description: eventData.description,
                        startsAt: eventData.startsAt,
                        endsAt: eventData.endsAt,
                        ownershipUrl: operationUrl,
                    });
                mappings.push({
                    operationUrl: operationUrl,
                    finalUrl: eventData.ownershipUrl,
                });
            }

            return mappings;
        }

        function replacementEventsAreCurrent(eventIndex, mappings) {
            for (let mappingIndex = 0; mappingIndex < mappings.length; mappingIndex += 1) {
                if (uniqueManagedEvent(
                        eventIndex,
                        mappings[mappingIndex].operationUrl) === null) {
                    return false;
                }
            }

            return true;
        }

        function createReplacementUrlSet(mappings) {
            const urls = Object.create(null);
            for (let index = 0; index < mappings.length; index += 1) {
                urls[mappings[index].operationUrl] = true;
            }

            return urls;
        }

        function findPreviousManagedEvents(
            index,
            canaryUrl,
            replacementMappings) {
            const replacementUrls = createReplacementUrlSet(replacementMappings);
            return index.entries
                .filter(function (event) {
                    return event.url !== canaryUrl
                        && replacementUrls[event.url] !== true;
                })
                .map(function (entry) {
                    return entry.event;
                });
        }

        function restoreReplacementEventUrls(
            proof,
            mappings) {
            for (let index = 0; index < mappings.length; index += 1) {
                const mapping = mappings[index];
                const event = uniqueManagedEvent(
                    proof.eventIndex,
                    mapping.operationUrl);
                if (event === null
                    || eventUrl(event) !== mapping.operationUrl) {
                    return false;
                }

                try {
                    event.url = mapping.finalUrl;
                } catch (_) {
                    return false;
                }

                if (eventUrl(event) !== mapping.finalUrl) {
                    return false;
                }
            }

            return true;
        }

        function createEvent(calendarApplication, calendar, eventData) {
            const event = calendarApplication.Event({
                summary: eventData.summary,
                location: eventData.location,
                description: eventData.description,
                startDate: new Date(eventData.startsAt),
                endDate: new Date(eventData.endsAt),
                url: eventData.ownershipUrl,
            });
            calendar.events.push(event);
            return event;
        }

        function deleteItems(items) {
            let deletedCount = 0;
            for (let index = items.length - 1; index >= 0; index -= 1) {
                items[index].delete();
                deletedCount += 1;
            }

            return deletedCount;
        }

        function createAllEvents(calendarApplication, calendar, events) {
            const createdEvents = [];
            for (let index = 0; index < events.length; index += 1) {
                createdEvents.push(createEvent(
                    calendarApplication,
                    calendar,
                    events[index]));
            }

            return createdEvents;
        }

        function listCalendars(calendarApplication, request) {
            const calendars = calendarApplication.calendars();
            const snapshot = createCalendarSnapshot(
                calendars,
                request);
            const result = [];
            for (let index = 0; index < calendars.length; index += 1) {
                const calendar = calendars[index];
                result.push({
                    id: snapshot.ids[index],
                    name: calendarName(calendar),
                    description: calendarDescription(calendar),
                    managedPlanId: snapshot.managedPlanIds[index],
                    writable: calendarIsWritable(calendar),
                });
            }

            return {
                status: "ok",
                calendars: result,
            };
        }

        function ensureExportHasEvents(request) {
            if (Array.isArray(request.events) === false
                || request.events.length === 0) {
                throw new Error(
                    "apple_calendar_export_requires_events");
            }
        }

        function createCalendar(calendarApplication, request) {
            ensureExportHasEvents(request);
            const calendars = calendarApplication.calendars();
            if (findCalendarsByName(
                    calendars,
                    request.normalizedDestinationName).length !== 0) {
                return {
                    status: "calendar_changed",
                    diagnosticCode: "apple_calendar_destination_name_changed",
                };
            }

            const calendar = calendarApplication.Calendar({
                name: request.destinationName,
                description: request.ownershipDescription,
            }).make();
            let createdCalendarId;
            try {
                createdCalendarId = managedCalendarId(
                    calendar,
                    request);
                if (createdCalendarId === null
                    || canonicalName(calendarName(calendar))
                        !== request.normalizedDestinationName) {
                    throw new Error(
                        "apple_calendar_creation_target_invalid");
                }
            } catch (error) {
                // Without a canary-backed fresh rebind, deleting this lazy
                // object specifier could target an unrelated calendar.
                throw error;
            }

            const operationUrl = createOperationCanaryEvent(
                calendarApplication,
                calendar,
                request);
            let operationProof = findOperationEventProof(
                calendarApplication,
                operationUrl,
                createdCalendarId,
                request);
            if (operationProof === null) {
                throw new Error(
                    "apple_calendar_creation_target_changed");
            }

            const createdEvents = createAllEvents(
                calendarApplication,
                operationProof.calendar,
                request.events);
            operationProof = findOperationEventProof(
                calendarApplication,
                operationUrl,
                createdCalendarId,
                request);
            if (operationProof === null) {
                throw new Error(
                    "apple_calendar_creation_target_changed");
            }

            const expectedCreatedEventUrls = [];
            for (let index = 0; index < request.events.length; index += 1) {
                expectedCreatedEventUrls.push(
                    request.events[index].ownershipUrl);
            }

            const expectedManagedEventUrls = [operationUrl].concat(expectedCreatedEventUrls);
            if (managedEventIndexMatchesExpectedUrls(
                    operationProof.eventIndex,
                    expectedManagedEventUrls) === false) {
                throw new Error(
                    "apple_calendar_creation_events_changed");
            }

            if (deleteOperationCanaryAndConfirm(
                    calendarApplication,
                    operationUrl,
                    createdCalendarId,
                    request,
                    expectedCreatedEventUrls) === false) {
                throw new Error(
                    "apple_calendar_creation_canary_cleanup_failed");
            }

            if (publishCalendarDescriptionAndConfirm(
                    calendarApplication,
                    createdCalendarId,
                    request,
                    expectedCreatedEventUrls) === false) {
                throw new Error(
                    "apple_calendar_creation_description_commit_failed");
            }

            return {
                status: "ok",
                calendarId: createdCalendarId,
                calendarName: request.destinationName,
                createdEventCount: createdEvents.length,
                deletedEventCount: 0,
            };
        }

        function replaceCalendar(calendarApplication, request) {
            ensureExportHasEvents(request);
            const calendars = calendarApplication.calendars();
            const target = findManagedCalendarById(
                calendars,
                request.existingCalendarId,
                request);
            if (replacementTargetIsValid(
                    calendars,
                    target,
                    request) === false) {
                return {
                    status: "calendar_changed",
                    diagnosticCode: "apple_calendar_replacement_precondition_changed",
                };
            }

            const operationUrl = createOperationCanaryEvent(
                calendarApplication,
                target,
                request);
            let operationProof = findOperationEventProof(
                calendarApplication,
                operationUrl,
                request.existingCalendarId,
                request);
            if (operationProof === null) {
                throw new Error(
                    "apple_calendar_replacement_target_changed");
            }

            const replacementMappings = createReplacementEvents(
                calendarApplication,
                operationProof.calendar,
                request.events,
                request);
            operationProof = findOperationEventProof(
                calendarApplication,
                operationUrl,
                request.existingCalendarId,
                request);
            if (operationProof === null) {
                throw new Error(
                    "apple_calendar_replacement_target_changed");
            }

            if (replacementEventsAreCurrent(
                    operationProof.eventIndex,
                    replacementMappings) === false) {
                throw new Error(
                    "apple_calendar_replacement_events_changed");
            }

            const previousEvents = findPreviousManagedEvents(
                operationProof.eventIndex,
                operationUrl,
                replacementMappings);
            let deletedCount;
            try {
                deletedCount = deleteItems(previousEvents);
            } catch (error) {
                // Old-event deletion is the commit point. Retaining the new
                // managed events and canary avoids data loss and lets a later
                // explicit replacement reconcile.
                throw error;
            }

            operationProof = findOperationEventProof(
                calendarApplication,
                operationUrl,
                request.existingCalendarId,
                request);
            if (operationProof === null) {
                throw new Error(
                    "apple_calendar_replacement_commit_unverified");
            }

            const expectedTemporaryUrls = [operationUrl];
            for (let index = 0; index < replacementMappings.length; index += 1) {
                expectedTemporaryUrls.push(
                    replacementMappings[index].operationUrl);
            }

            if (managedEventIndexMatchesExpectedUrls(
                    operationProof.eventIndex,
                    expectedTemporaryUrls) === false) {
                throw new Error(
                    "apple_calendar_replacement_old_events_remain");
            }

            if (restoreReplacementEventUrls(
                    operationProof,
                    replacementMappings) === false) {
                throw new Error(
                    "apple_calendar_replacement_event_commit_failed");
            }

            operationProof = findOperationEventProof(
                calendarApplication,
                operationUrl,
                request.existingCalendarId,
                request);
            const expectedReplacementUrls = [];
            for (let index = 0; index < replacementMappings.length; index += 1) {
                expectedReplacementUrls.push(
                    replacementMappings[index].finalUrl);
            }

            if (operationProof === null
                || managedEventIndexMatchesExpectedUrls(
                    operationProof.eventIndex,
                    [operationUrl].concat(
                        expectedReplacementUrls)) === false) {
                throw new Error(
                    "apple_calendar_replacement_event_commit_unverified");
            }

            if (deleteOperationCanaryAndConfirm(
                    calendarApplication,
                    operationUrl,
                    request.existingCalendarId,
                    request,
                    expectedReplacementUrls) === false) {
                throw new Error(
                    "apple_calendar_replacement_canary_cleanup_failed");
            }

            if (publishCalendarDescriptionAndConfirm(
                    calendarApplication,
                    request.existingCalendarId,
                    request,
                    expectedReplacementUrls) === false) {
                throw new Error(
                    "apple_calendar_replacement_description_commit_failed");
            }

            return {
                status: "ok",
                calendarId: request.existingCalendarId,
                calendarName: request.destinationName,
                createdEventCount: replacementMappings.length,
                deletedEventCount: deletedCount,
            };
        }

        function applyExport(calendarApplication, request) {
            if (request.mutationKind === "create") {
                return createCalendar(calendarApplication, request);
            }

            if (request.mutationKind === "replace") {
                return replaceCalendar(calendarApplication, request);
            }

            throw new Error("unsupported_mutation_kind");
        }

        function errorNumber(error) {
            if (error === null || error === undefined) {
                return 0;
            }

            const numericValue = Number(error.number);
            return Number.isFinite(numericValue) ? numericValue : 0;
        }

        function isAccessDenied(error) {
            const number = errorNumber(error);
            if (number === -1743 || number === -10004) {
                return true;
            }

            const message = String(error && error.message || "").toLowerCase();
            return message.indexOf("not authorized") >= 0
                || message.indexOf("not permitted") >= 0
                || message.indexOf("automation permission") >= 0;
        }

        function failureResponse(error) {
            if (isAccessDenied(error)) {
                return {
                    status: "access_denied",
                    diagnosticCode: "apple_calendar_automation_access_denied",
                };
            }

            return {
                status: "operation_failed",
                diagnosticCode: "apple_calendar_automation_operation_failed",
            };
        }

        function run(arguments) {
            try {
                if (arguments.length !== 2) {
                    throw new Error("invalid_argument_count");
                }

                const operation = arguments[0];
                const request = readRequest(arguments[1]);
                const calendarApplication = Application("Calendar");
                let response;
                if (operation === "list") {
                    response = listCalendars(calendarApplication, request);
                } else if (operation === "apply") {
                    response = applyExport(calendarApplication, request);
                } else {
                    throw new Error("unsupported_operation");
                }

                return JSON.stringify(response);
            } catch (error) {
                return JSON.stringify(failureResponse(error));
            }
        }
        """;
}
