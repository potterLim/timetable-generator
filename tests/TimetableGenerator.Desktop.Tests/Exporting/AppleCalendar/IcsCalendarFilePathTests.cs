using System;
using System.IO;

using TimetableGenerator.Desktop.Exporting.AppleCalendar;

using Xunit;

namespace TimetableGenerator.Desktop.Tests.Exporting.AppleCalendar;

public sealed class IcsCalendarFilePathTests
{
    [Fact]
    public void FullyQualifiedIcsPathIsAcceptedCaseInsensitively()
    {
        string sourcePath = Path.Combine(Path.GetTempPath(), "Schedule.ICS");

        IcsCalendarFilePath filePath = new IcsCalendarFilePath(sourcePath);

        Assert.Equal(sourcePath, filePath.Value);
    }

    [Fact]
    public void RelativePathIsRejected()
    {
        Assert.Throws<ArgumentException>(
            () => new IcsCalendarFilePath("schedule.ics"));
    }

    [Fact]
    public void NonIcsExtensionIsRejected()
    {
        string sourcePath = Path.Combine(Path.GetTempPath(), "schedule.txt");

        Assert.Throws<ArgumentException>(
            () => new IcsCalendarFilePath(sourcePath));
    }
}
