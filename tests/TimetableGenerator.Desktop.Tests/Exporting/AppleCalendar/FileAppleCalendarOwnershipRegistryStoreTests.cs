using System;
using System.IO;

using TimetableGenerator.Desktop.Exporting.AppleCalendar;

using Xunit;

namespace TimetableGenerator.Desktop.Tests.Exporting.AppleCalendar;

public sealed class FileAppleCalendarOwnershipRegistryStoreTests
{
    private const string PLAN_ID = "71f3be04-d4c6-41d4-a269-792321e71423";

    [Fact]
    public void MissingRegistryLoadsAsEmpty()
    {
        using (TemporaryDirectory directory = new TemporaryDirectory())
        {
            FileAppleCalendarOwnershipRegistryStore store = createStore(directory.Path);

            AppleCalendarOwnershipRegistryDocument document = store.Load();

            Assert.Equal(AppleCalendarOwnershipRegistryDocument.CURRENT_SCHEMA_VERSION, document.SchemaVersion);
            Assert.Empty(document.Calendars);
            Assert.Null(document.PendingOperation);
        }
    }

    [Fact]
    public void RegistryRoundTripsPendingAndCompletedOwnershipWithoutEventContent()
    {
        using (TemporaryDirectory directory = new TemporaryDirectory())
        {
            string registryPath = Path.Combine(directory.Path, "apple-calendar-ownership.json");
            FileAppleCalendarOwnershipRegistryStore store = new FileAppleCalendarOwnershipRegistryStore(new AppleCalendarOwnershipRegistryFilePath(registryPath));
            AppleCalendarRegistration registration = createRegistration("calendar-1", "event-1");
            AppleCalendarPendingOperation pendingOperation = new AppleCalendarPendingOperation(
                "8e7bfa52-c13c-4f40-8f8c-27f388c3aa19",
                PLAN_ID,
                PLAN_ID,
                "calendar-1",
                "source-1",
                "2026-2학기 시간표",
                "2026-2학기 시간표",
                1_777_824_000,
                1_799_625_599,
                1_777_824_000,
                new AppleCalendarPendingEvent[]
                {
                    new AppleCalendarPendingEvent(new string('a', 64), new string('b', 64)),
                });
            AppleCalendarOwnershipRegistryDocument document = new AppleCalendarOwnershipRegistryDocument(
                AppleCalendarOwnershipRegistryDocument.CURRENT_SCHEMA_VERSION,
                new AppleCalendarRegistration[] { registration },
                pendingOperation);

            store.Save(document);
            AppleCalendarOwnershipRegistryDocument loaded = store.Load();

            AppleCalendarRegistration loadedRegistration = Assert.Single(loaded.Calendars);
            Assert.Equal(PLAN_ID, loadedRegistration.PlanId);
            Assert.Equal("calendar-1", loadedRegistration.CalendarIdentifier);
            AppleCalendarManagedEventRegistration loadedEvent = Assert.Single(loadedRegistration.Events);
            Assert.Equal("event-1", loadedEvent.CalendarItemIdentifier);
            Assert.NotNull(loaded.PendingOperation);
            Assert.Equal(pendingOperation.OperationId, loaded.PendingOperation!.OperationId);
            Assert.Equal("source-1", loaded.PendingOperation.ExpectedSourceIdentifierOrNull);
            string json = File.ReadAllText(registryPath);
            Assert.DoesNotContain("전자기학", json, StringComparison.Ordinal);
            Assert.DoesNotContain("NTH 311", json, StringComparison.Ordinal);
            Assert.DoesNotContain("managed-event", json, StringComparison.Ordinal);
            if (OperatingSystem.IsMacOS() || OperatingSystem.IsLinux() || OperatingSystem.IsFreeBSD())
            {
                UnixFileMode mode = File.GetUnixFileMode(registryPath);
                Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite, mode & (UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.GroupRead | UnixFileMode.GroupWrite | UnixFileMode.OtherRead | UnixFileMode.OtherWrite));
            }
        }
    }

    [Fact]
    public void CompletingOperationReplacesOnlyTheMatchingCalendarAndClearsPending()
    {
        AppleCalendarRegistration first = createRegistration("calendar-1", "event-old");
        AppleCalendarRegistration second = createRegistration("calendar-2", "event-other");
        AppleCalendarPendingOperation pendingOperation = new AppleCalendarPendingOperation(
            "8e7bfa52-c13c-4f40-8f8c-27f388c3aa19",
            PLAN_ID,
            PLAN_ID,
            "calendar-1",
            "source-1",
            "2026-2학기 시간표",
            "2026-2학기 시간표",
            1,
            2,
            1,
            new AppleCalendarPendingEvent[]
            {
                new AppleCalendarPendingEvent(new string('a', 64), new string('b', 64)),
            });
        AppleCalendarOwnershipRegistryDocument document = new AppleCalendarOwnershipRegistryDocument(
            AppleCalendarOwnershipRegistryDocument.CURRENT_SCHEMA_VERSION,
            new AppleCalendarRegistration[] { second, first },
            pendingOperation);

        AppleCalendarOwnershipRegistryDocument completed = document.CompleteOperation(createRegistration("calendar-1", "event-new"));

        Assert.Null(completed.PendingOperation);
        Assert.Collection(
            completed.Calendars,
            calendar =>
            {
                Assert.Equal("calendar-1", calendar.CalendarIdentifier);
                Assert.Equal("event-new", Assert.Single(calendar.Events).CalendarItemIdentifier);
            },
            calendar =>
            {
                Assert.Equal("calendar-2", calendar.CalendarIdentifier);
                Assert.Equal("event-other", Assert.Single(calendar.Events).CalendarItemIdentifier);
            });
    }

    [Theory]
    [InlineData("{")]
    [InlineData("{\"schemaVersion\":2,\"calendars\":[],\"pendingOperation\":null}")]
    [InlineData("{\"schemaVersion\":1,\"calendars\":[],\"pendingOperation\":null,\"unexpected\":true}")]
    public void InvalidRegistryFailsClosed(string json)
    {
        using (TemporaryDirectory directory = new TemporaryDirectory())
        {
            string registryPath = Path.Combine(directory.Path, "apple-calendar-ownership.json");
            File.WriteAllText(registryPath, json);
            FileAppleCalendarOwnershipRegistryStore store = new FileAppleCalendarOwnershipRegistryStore(new AppleCalendarOwnershipRegistryFilePath(registryPath));

            Assert.Throws<AppleCalendarOwnershipRegistryException>(() => store.Load());
        }
    }

    private static FileAppleCalendarOwnershipRegistryStore createStore(string directoryPath)
    {
        return new FileAppleCalendarOwnershipRegistryStore(new AppleCalendarOwnershipRegistryFilePath(Path.Combine(directoryPath, "apple-calendar-ownership.json")));
    }

    private static AppleCalendarRegistration createRegistration(string calendarIdentifier, string eventIdentifier)
    {
        return new AppleCalendarRegistration(
            PLAN_ID,
            calendarIdentifier,
            "2026-2학기 시간표",
            "2026-2학기 시간표",
            "source-1",
            1_777_824_000,
            1_799_625_599,
            new AppleCalendarManagedEventRegistration[]
            {
                new AppleCalendarManagedEventRegistration(new string('a', 64), eventIdentifier, "external-" + eventIdentifier, new string('b', 64)),
            });
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        public string Path { get; }

        public TemporaryDirectory()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "timetable-generator-apple-registry-tests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);
        }

        public void Dispose()
        {
            Directory.Delete(Path, true);
        }
    }
}
