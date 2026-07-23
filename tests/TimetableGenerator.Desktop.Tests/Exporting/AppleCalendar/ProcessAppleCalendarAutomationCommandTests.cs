using System;
using System.IO;

using TimetableGenerator.Desktop.Exporting.AppleCalendar;

using Xunit;

namespace TimetableGenerator.Desktop.Tests.Exporting.AppleCalendar;

public sealed class ProcessAppleCalendarAutomationCommandTests
{
    [Fact]
    public void PrivateRequestOptionsCreateAWriteOnlyExclusiveFile()
    {
        FileStreamOptions options =
            ProcessAppleCalendarAutomationCommand.createPrivateRequestFileOptions();

        Assert.Equal(FileMode.CreateNew, options.Mode);
        Assert.Equal(FileAccess.Write, options.Access);
        Assert.Equal(FileShare.None, options.Share);
        Assert.True(options.Options.HasFlag(FileOptions.Asynchronous));
        Assert.True(options.Options.HasFlag(FileOptions.WriteThrough));

        if (OperatingSystem.IsWindows())
        {
            Assert.Null(options.UnixCreateMode);
            return;
        }

        Assert.Equal(
            UnixFileMode.UserRead | UnixFileMode.UserWrite,
            options.UnixCreateMode);
    }

    [Fact]
    public void PrivateRequestFileStartsOwnerOnlyWhileCreationStreamIsOpen()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        string requestPath = Path.Combine(
            Path.GetTempPath(),
            "timetable-generator-apple-calendar-test-"
                + Guid.NewGuid().ToString("N")
                + ".json");
        try
        {
            FileStreamOptions options =
                ProcessAppleCalendarAutomationCommand.createPrivateRequestFileOptions();
            using (FileStream requestStream = new FileStream(requestPath, options))
            {
                UnixFileMode createdMode = File.GetUnixFileMode(requestPath);

                Assert.Equal(
                    UnixFileMode.UserRead | UnixFileMode.UserWrite,
                    createdMode);
                Assert.True(requestStream.CanWrite);
                Assert.False(requestStream.CanRead);
            }
        }
        finally
        {
            if (File.Exists(requestPath))
            {
                File.Delete(requestPath);
            }
        }
    }
}
