using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

using TimetableGenerator.Desktop.Exporting.AppleCalendar;
using TimetableGenerator.Desktop.Exporting.Calendar;
using TimetableGenerator.Domain.Catalogs;
using TimetableGenerator.Domain.Planning;
using TimetableGenerator.Domain.Scheduling;

using Xunit;

namespace TimetableGenerator.Desktop.Tests.Exporting.AppleCalendar;

public abstract class EventKitAppleCalendarNativeBridgeTestFixture
{
    private protected static readonly PlanId PLAN_ID = new PlanId(Guid.Parse("71f3be04-d4c6-41d4-a269-792321e71423"));

    private protected static Dictionary<string, object?> createRegistrationBinding(
        AppleCalendarRegistration registration,
        string calendarIdentifier,
        string calendarItemIdentifier,
        string externalIdentifier)
    {
        AppleCalendarManagedEventRegistration managedEvent = Assert.Single(registration.Events);
        return new Dictionary<string, object?>
        {
            ["previousCalendarIdentifier"] = registration.CalendarIdentifier,
            ["calendarIdentifier"] = calendarIdentifier,
            ["calendarName"] = registration.CalendarName,
            ["sourceIdentifier"] = registration.SourceIdentifier,
            ["planId"] = registration.PlanId,
            ["events"] = new object[]
            {
                new
                {
                    sourceEventHash = managedEvent.SourceEventHash,
                    calendarItemIdentifier,
                    externalIdentifier,
                    fingerprint = managedEvent.Fingerprint,
                },
            },
        };
    }

    private protected static AppleCalendarPendingOperation createPendingReplacement(CalendarExportDocument document, AppleCalendarRegistration registration)
    {
        AppleCalendarRecurringEvent recurringEvent = Assert.Single(AppleCalendarRecurringEventProjector.Project(document));
        (long termStart, long termEnd) = EventKitAppleCalendarRequest.GetTermRange(document);
        return new AppleCalendarPendingOperation(
            Guid.NewGuid().ToString("D"),
            document.PlanId.ToString(),
            document.PlanId.ToString(),
            registration.CalendarIdentifier,
            registration.SourceIdentifier,
            document.CalendarName.Value,
            EventKitAppleCalendarRequest.NormalizeCalendarName(document.CalendarName.Value),
            termStart,
            termEnd,
            DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            new AppleCalendarPendingEvent[]
            {
                new AppleCalendarPendingEvent(recurringEvent.SourceEventHash, recurringEvent.Fingerprint),
            });
    }

    private protected static string createSuccessfulResponse(string requestJson, string calendarIdentifier, string sourceIdentifier, int deletedEventCount)
    {
        using (JsonDocument request = JsonDocument.Parse(requestJson))
        {
            string operation = request.RootElement.GetProperty("operation").GetString()!;
            string eventCollectionName;
            if (operation == "reconcile")
            {
                eventCollectionName = "desiredEvents";
            }
            else
            {
                eventCollectionName = "recurringEvents";
            }
            JsonElement requestedEvent = Assert.Single(request.RootElement.GetProperty(eventCollectionName).EnumerateArray());
            string sourceEventHash = requestedEvent.GetProperty("sourceEventHash").GetString()!;
            string fingerprint = requestedEvent.GetProperty("fingerprint").GetString()!;
            string calendarName = request.RootElement.GetProperty("destinationName").GetString()!;
            return JsonSerializer.Serialize(
                new
                {
                    schemaVersion = 1,
                    status = "ok",
                    diagnosticCode = "",
                    calendarIdentifier,
                    calendarName,
                    sourceIdentifier,
                    createdEventCount = 1,
                    deletedEventCount,
                    events = new[]
                    {
                        new
                        {
                            sourceEventHash,
                            calendarItemIdentifier = "new-item",
                            externalIdentifier = "new-external",
                            fingerprint,
                        },
                    },
                });
        }
    }

    private protected static string getOperation(string requestJson)
    {
        using (JsonDocument request = JsonDocument.Parse(requestJson))
        {
            return request.RootElement.GetProperty("operation").GetString()!;
        }
    }

    private protected static int getRegistrationCount(string requestJson)
    {
        using (JsonDocument request = JsonDocument.Parse(requestJson))
        {
            return request.RootElement.GetProperty("registrations").GetArrayLength();
        }
    }

    private protected static void assertLegacyMigrationRange(JsonElement root)
    {
        long termStart = root.GetProperty("termStartsAtUnixSeconds").GetInt64();
        long termEnd = root.GetProperty("termEndsAtUnixSeconds").GetInt64();
        (long expectedMigrationStart, long expectedMigrationEnd) = EventKitAppleCalendarRequest.getLegacyMigrationRange(termStart, termEnd);
        Assert.Equal(expectedMigrationStart, root.GetProperty("migrationStartsAtUnixSeconds").GetInt64());
        Assert.Equal(expectedMigrationEnd, root.GetProperty("migrationEndsAtUnixSeconds").GetInt64());
    }

    private protected static AppleCalendarRegistration createRegistration(CalendarExportDocument document, string calendarIdentifier, string sourceIdentifier)
    {
        AppleCalendarRecurringEvent recurringEvent = Assert.Single(AppleCalendarRecurringEventProjector.Project(document));
        (long termStart, long termEnd) = EventKitAppleCalendarRequest.GetTermRange(document);
        return new AppleCalendarRegistration(
            document.PlanId.ToString(),
            calendarIdentifier,
            document.CalendarName.Value,
            EventKitAppleCalendarRequest.NormalizeCalendarName(document.CalendarName.Value),
            sourceIdentifier,
            termStart,
            termEnd,
            new AppleCalendarManagedEventRegistration[]
            {
                new AppleCalendarManagedEventRegistration(recurringEvent.SourceEventHash, "old-item", "old-external", recurringEvent.Fingerprint),
            });
    }

    private protected static CalendarExportDocument createDocument()
    {
        return createDocument(new PlanName("2026-2학기 시간표"));
    }

    private protected static CalendarExportDocument createDocument(PlanName calendarName)
    {
        AcademicTermCalendarMetadata academicCalendar = new AcademicTermCalendarMetadata(
            AcademicTerm.Parse("2026-2"),
            new AcademicTermDateRange(new DateOnly(2026, 8, 31), new DateOnly(2026, 12, 18)),
            new CalendarTimeZoneId("Asia/Seoul"));
        RecurringCalendarEvent calendarEvent = new RecurringCalendarEvent(
            new CalendarEventUid("course:ITP30003:01"),
            new CalendarEventContent("컴퓨터 구조(01)", "OH 401", "담당: 이원형"),
            new DailyTimeRange(new ScheduleTime(11, 30), new ScheduleTime(12, 15)),
            new EDay[] { EDay.Monday, EDay.Thursday });
        return new CalendarExportDocument(PLAN_ID, calendarName, new InstitutionName("한동대학교"), academicCalendar, new RecurringCalendarEvent[] { calendarEvent });
    }

    private protected static CalendarExportDocument createDocumentWithDuplicateEventFingerprints()
    {
        CalendarExportDocument document = createDocument();
        RecurringCalendarEvent first = Assert.Single(document.Events);
        RecurringCalendarEvent duplicate = new RecurringCalendarEvent(
            new CalendarEventUid("personal:duplicate-event"),
            first.Content,
            first.TimeRange,
            first.Days);
        return new CalendarExportDocument(document.PlanId, document.CalendarName, document.InstitutionName, document.AcademicCalendar, new RecurringCalendarEvent[] { first, duplicate });
    }

    private protected sealed class RecordingEventKitCalendarCommand : IEventKitCalendarCommand
    {
        private readonly Func<string, CancellationToken, Task<string>> mResponseFactory;
        private readonly List<string> mRequests = new List<string>();

        public bool IsAvailable { get; set; } = true;

        public IReadOnlyList<string> Requests
        {
            get
            {
                return mRequests;
            }
        }

        public RecordingEventKitCalendarCommand(string response)
            : this((_, _) => Task.FromResult(response))
        {
        }

        public RecordingEventKitCalendarCommand(Func<string, string> responseFactory)
            : this((requestJson, _) => Task.FromResult(responseFactory(requestJson)))
        {
        }

        public RecordingEventKitCalendarCommand(Func<string, CancellationToken, Task<string>> responseFactory)
        {
            mResponseFactory = responseFactory;
        }

        public Task<string> ExecuteAsync(string requestJson, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            mRequests.Add(requestJson);
            return mResponseFactory(requestJson, cancellationToken);
        }
    }

    private protected sealed class FixedCalendarNameConflictResolver : ICalendarNameConflictResolver
    {
        private readonly ECalendarNameConflictResolution mResolution;
        private readonly List<CalendarNameConflict> mConflicts = new List<CalendarNameConflict>();

        public IReadOnlyList<CalendarNameConflict> Conflicts
        {
            get
            {
                return mConflicts;
            }
        }

        public FixedCalendarNameConflictResolver(ECalendarNameConflictResolution resolution)
        {
            mResolution = resolution;
        }

        public Task<ECalendarNameConflictResolution> ResolveAsync(CalendarNameConflict conflict, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(conflict);
            cancellationToken.ThrowIfCancellationRequested();
            mConflicts.Add(conflict);
            return Task.FromResult(mResolution);
        }
    }

    private protected sealed class RecordingRegistryStore : IAppleCalendarOwnershipRegistryStore
    {
        private readonly List<AppleCalendarOwnershipRegistryDocument> mSavedDocuments = new List<AppleCalendarOwnershipRegistryDocument>();

        public AppleCalendarOwnershipRegistryDocument Current { get; private set; }

        public int? FailureOnSaveAttemptOrNull { get; set; }

        public Exception? FailureOnLoadOrNull { get; set; }

        public IReadOnlyList<AppleCalendarOwnershipRegistryDocument> SavedDocuments
        {
            get
            {
                return mSavedDocuments;
            }
        }

        public RecordingRegistryStore(AppleCalendarOwnershipRegistryDocument initialDocument)
        {
            Current = initialDocument;
        }

        public AppleCalendarOwnershipRegistryDocument Load()
        {
            if (FailureOnLoadOrNull != null)
            {
                throw FailureOnLoadOrNull;
            }

            return Current;
        }

        public void Save(AppleCalendarOwnershipRegistryDocument document)
        {
            int saveAttempt = mSavedDocuments.Count + 1;
            if (FailureOnSaveAttemptOrNull == saveAttempt)
            {
                throw new System.IO.IOException("Controlled registry save failure.");
            }

            Current = document;
            mSavedDocuments.Add(document);
        }
    }
}
