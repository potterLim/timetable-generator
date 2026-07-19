using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using TimetableGenerator.Desktop.Integrations.GoogleCalendar;
using TimetableGenerator.Domain.Planning;
using Xunit;

namespace TimetableGenerator.Desktop.Tests.Integrations.GoogleCalendar;

public sealed class FileGoogleCalendarBindingStoreTests
{
    [Fact]
    public async Task CalendarBindingSurvivesStoreRecreationAsync()
    {
        string directoryPath = Path.Combine(
            Path.GetTempPath(),
            "TimetableGenerator.Desktop.Tests",
            Guid.NewGuid().ToString("N"));
        GoogleCalendarBindingFilePath path = new GoogleCalendarBindingFilePath(
            Path.Combine(directoryPath, "google-calendar-bindings-v1.json"));
        PlanId planId = PlanId.CreateNew();
        GoogleCalendarId calendarId = new GoogleCalendarId("calendar-id");

        try
        {
            using (FileGoogleCalendarBindingStore firstStore =
                new FileGoogleCalendarBindingStore(path))
            {
                await firstStore.SaveCalendarIdAsync(
                    planId,
                    calendarId,
                    CancellationToken.None);
            }

            using (FileGoogleCalendarBindingStore secondStore =
                new FileGoogleCalendarBindingStore(path))
            {
                GoogleCalendarId? restoredOrNull =
                    await secondStore.GetCalendarIdOrNullAsync(
                        planId,
                        CancellationToken.None);

                Assert.Equal(calendarId, restoredOrNull);
            }
        }
        finally
        {
            if (Directory.Exists(directoryPath))
            {
                Directory.Delete(directoryPath, true);
            }
        }
    }

    [Fact]
    public async Task MalformedCalendarIdIsReportedAsInvalidLocalDataAsync()
    {
        string directoryPath = Path.Combine(
            Path.GetTempPath(),
            "TimetableGenerator.Desktop.Tests",
            Guid.NewGuid().ToString("N"));
        GoogleCalendarBindingFilePath path = new GoogleCalendarBindingFilePath(
            Path.Combine(directoryPath, "google-calendar-bindings-v1.json"));
        PlanId planId = PlanId.CreateNew();

        try
        {
            Directory.CreateDirectory(directoryPath);
            File.WriteAllText(
                path.Value,
                "{\"schemaVersion\":1,\"bindings\":[{\"planId\":\""
                    + planId.Value.ToString("N")
                    + "\",\"calendarId\":\"   \"}]}",
                Encoding.UTF8);
            using (FileGoogleCalendarBindingStore store =
                new FileGoogleCalendarBindingStore(path))
            {
                await Assert.ThrowsAsync<InvalidDataException>(
                    async delegate
                    {
                        await store.GetCalendarIdOrNullAsync(
                            planId,
                            CancellationToken.None);
                    });
            }
        }
        finally
        {
            if (Directory.Exists(directoryPath))
            {
                Directory.Delete(directoryPath, true);
            }
        }
    }
}
