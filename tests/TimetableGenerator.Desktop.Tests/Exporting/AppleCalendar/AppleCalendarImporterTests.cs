using System;
using System.IO;
using System.Threading.Tasks;

using TimetableGenerator.Desktop.Exporting.AppleCalendar;

using Xunit;

namespace TimetableGenerator.Desktop.Tests.Exporting.AppleCalendar;

public sealed class AppleCalendarImporterTests
{
    [Fact]
    public async Task SupportedImporterOpensExistingIcsFileAsync()
    {
        string temporaryFilePath = Path.Combine(
            Path.GetTempPath(),
            Guid.NewGuid().ToString("N") + ".ics");

        try
        {
            await File.WriteAllTextAsync(
                temporaryFilePath,
                "BEGIN:VCALENDAR",
                TestContext.Current.CancellationToken);

            RecordingAppleCalendarOpenCommand openCommand =
                new RecordingAppleCalendarOpenCommand();
            AppleCalendarImporter importer = new AppleCalendarImporter(
                EAppleCalendarRuntimePlatform.MacOS,
                openCommand);
            IcsCalendarFilePath calendarFilePath =
                new IcsCalendarFilePath(temporaryFilePath);

            await importer.OpenImportAsync(
                calendarFilePath,
                TestContext.Current.CancellationToken);

            Assert.True(importer.IsAvailable);
            Assert.Same(
                calendarFilePath,
                openCommand.OpenedCalendarFilePathOrNull);
        }
        finally
        {
            File.Delete(temporaryFilePath);
        }
    }

    [Fact]
    public async Task UnsupportedImporterRejectsImportBeforeOpeningFileAsync()
    {
        RecordingAppleCalendarOpenCommand openCommand =
            new RecordingAppleCalendarOpenCommand();
        AppleCalendarImporter importer = new AppleCalendarImporter(
            EAppleCalendarRuntimePlatform.Unsupported,
            openCommand);
        IcsCalendarFilePath calendarFilePath = new IcsCalendarFilePath(
            Path.Combine(Path.GetTempPath(), "missing.ics"));

        await Assert.ThrowsAsync<PlatformNotSupportedException>(
            () => importer.OpenImportAsync(
                calendarFilePath,
                TestContext.Current.CancellationToken));

        Assert.False(importer.IsAvailable);
        Assert.Null(openCommand.OpenedCalendarFilePathOrNull);
    }

    [Fact]
    public async Task MissingIcsFileIsRejectedBeforeOpeningCalendarAsync()
    {
        RecordingAppleCalendarOpenCommand openCommand =
            new RecordingAppleCalendarOpenCommand();
        AppleCalendarImporter importer = new AppleCalendarImporter(
            EAppleCalendarRuntimePlatform.MacOS,
            openCommand);
        IcsCalendarFilePath calendarFilePath = new IcsCalendarFilePath(
            Path.Combine(
                Path.GetTempPath(),
                Guid.NewGuid().ToString("N") + ".ics"));

        await Assert.ThrowsAsync<FileNotFoundException>(
            () => importer.OpenImportAsync(
                calendarFilePath,
                TestContext.Current.CancellationToken));

        Assert.Null(openCommand.OpenedCalendarFilePathOrNull);
    }
}
