using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

using TimetableGenerator.Desktop.Exporting.AppleCalendar;
using TimetableGenerator.Desktop.Exporting.Calendar;
using TimetableGenerator.Domain.Catalogs;
using TimetableGenerator.Domain.Planning;

using Xunit;

namespace TimetableGenerator.Desktop.Tests.Exporting.Calendar;

public sealed class IcsCalendarFileStoreTests
{
    [Fact]
    public void ExportDirectoryRequiresAFullyQualifiedPath()
    {
        Assert.Throws<ArgumentException>(
            () => new CalendarExportDirectoryPath("relative-path"));
    }

    [Fact]
    public async Task SaveUsesStablePlanIdentityAndCurrentCalendarNameAsync()
    {
        string testDirectory = createTestDirectory();
        PlanId planId = new PlanId(
            new Guid("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee"));
        IcsCalendarFileStore store = new IcsCalendarFileStore(
            new CalendarExportDirectoryPath(testDirectory));

        try
        {
            IcsCalendarFilePath firstPath = await store.SaveAsync(
                createDocument(planId, new PlanName("처음 이름")),
                new CalendarExportTimestamp(DateTimeOffset.UnixEpoch),
                CancellationToken.None);
            IcsCalendarFilePath renamedPath = await store.SaveAsync(
                createDocument(planId, new PlanName("바뀐 이름")),
                new CalendarExportTimestamp(DateTimeOffset.UnixEpoch),
                CancellationToken.None);

            Assert.Equal(firstPath.Value, renamedPath.Value);
            Assert.Equal(
                planId.Value.ToString("N") + ".ics",
                Path.GetFileName(renamedPath.Value));
            byte[] content = await File.ReadAllBytesAsync(
                renamedPath.Value,
                CancellationToken.None);
            Assert.False(hasUtf8ByteOrderMark(content));
            string serializedCalendar = File.ReadAllText(renamedPath.Value);
            Assert.Contains(
                "X-WR-CALNAME:바뀐 이름\r\n",
                serializedCalendar);
            Assert.DoesNotContain("처음 이름", serializedCalendar);
            Assert.Empty(Directory.GetFiles(testDirectory, "*.tmp"));
        }
        finally
        {
            deleteTestDirectory(testDirectory);
        }
    }

    [Fact]
    public async Task CancelledSaveDoesNotCreateAFileAsync()
    {
        string testDirectory = createTestDirectory();
        IcsCalendarFileStore store = new IcsCalendarFileStore(
            new CalendarExportDirectoryPath(testDirectory));
        CancellationToken cancellationToken = new CancellationToken(true);

        try
        {
            await Assert.ThrowsAsync<OperationCanceledException>(
                () => store.SaveAsync(
                    createDocument(
                        PlanId.CreateNew(),
                        new PlanName("취소된 시간표")),
                    new CalendarExportTimestamp(DateTimeOffset.UnixEpoch),
                    cancellationToken));
            Assert.Empty(Directory.GetFiles(testDirectory));
        }
        finally
        {
            deleteTestDirectory(testDirectory);
        }
    }

    private static CalendarExportDocument createDocument(
        PlanId planId,
        PlanName planName)
    {
        return new CalendarExportDocument(
            planId,
            planName,
            AcademicTermCalendarMetadataRegistry.findByTerm(
                AcademicTerm.Parse("2026-2"),
                new CalendarTimeZoneId("Asia/Seoul")),
            new List<RecurringCalendarEvent>());
    }

    private static string createTestDirectory()
    {
        string testDirectory = Path.Combine(
            Path.GetTempPath(),
            "TimetableGenerator.Tests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(testDirectory);
        return testDirectory;
    }

    private static bool hasUtf8ByteOrderMark(byte[] content)
    {
        return content.Length >= 3
            && content[0] == 0xEF
            && content[1] == 0xBB
            && content[2] == 0xBF;
    }

    private static void deleteTestDirectory(string testDirectory)
    {
        if (Directory.Exists(testDirectory))
        {
            Directory.Delete(testDirectory, true);
        }
    }
}
