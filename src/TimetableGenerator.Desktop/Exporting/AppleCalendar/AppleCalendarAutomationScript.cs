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

        function calendarId(calendar) {
            return String(calendar.id());
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

        function calendarIsManaged(calendar, markerPrefix) {
            return calendarDescription(calendar).indexOf(markerPrefix) === 0;
        }

        function findCalendarById(calendars, id) {
            for (let index = 0; index < calendars.length; index += 1) {
                if (calendarId(calendars[index]) === id) {
                    return calendars[index];
                }
            }

            return null;
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

        function createEvent(calendarApplication, calendar, eventData) {
            const event = calendarApplication.Event({
                summary: eventData.summary,
                location: eventData.location,
                description: eventData.description,
                startDate: new Date(eventData.startsAt),
                endDate: new Date(eventData.endsAt),
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
            try {
                for (let index = 0; index < events.length; index += 1) {
                    createdEvents.push(createEvent(
                        calendarApplication,
                        calendar,
                        events[index]));
                }
            } catch (error) {
                try {
                    deleteItems(createdEvents);
                } catch (_) {
                }

                throw error;
            }

            return createdEvents;
        }

        function listCalendars(calendarApplication, request) {
            const calendars = calendarApplication.calendars();
            const result = [];
            for (let index = 0; index < calendars.length; index += 1) {
                const calendar = calendars[index];
                result.push({
                    id: calendarId(calendar),
                    name: calendarName(calendar),
                    description: calendarDescription(calendar),
                    writable: calendarIsWritable(calendar),
                });
            }

            return {
                status: "ok",
                calendars: result,
            };
        }

        function createCalendar(calendarApplication, request) {
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
            let createdEvents;
            try {
                createdEvents = createAllEvents(
                    calendarApplication,
                    calendar,
                    request.events);
            } catch (error) {
                try {
                    calendar.delete();
                } catch (_) {
                }

                throw error;
            }

            return {
                status: "ok",
                calendarId: calendarId(calendar),
                calendarName: calendarName(calendar),
                createdEventCount: createdEvents.length,
                deletedEventCount: 0,
            };
        }

        function replaceCalendar(calendarApplication, request) {
            const calendars = calendarApplication.calendars();
            const target = findCalendarById(
                calendars,
                request.existingCalendarId);
            const matchingCalendars = findCalendarsByName(
                calendars,
                request.normalizedDestinationName);
            if (target === null
                || matchingCalendars.length !== 1
                || calendarId(matchingCalendars[0]) !== request.existingCalendarId
                || calendarIsManaged(
                    target,
                    request.ownershipMarkerPrefix) === false
                || calendarIsWritable(target) === false) {
                return {
                    status: "calendar_changed",
                    diagnosticCode: "apple_calendar_replacement_precondition_changed",
                };
            }

            const previousEvents = target.events();
            const createdEvents = createAllEvents(
                calendarApplication,
                target,
                request.events);
            let deletedCount;
            try {
                deletedCount = deleteItems(previousEvents);
            } catch (error) {
                try {
                    deleteItems(createdEvents);
                } catch (_) {
                }

                throw error;
            }

            return {
                status: "ok",
                calendarId: calendarId(target),
                calendarName: calendarName(target),
                createdEventCount: createdEvents.length,
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
