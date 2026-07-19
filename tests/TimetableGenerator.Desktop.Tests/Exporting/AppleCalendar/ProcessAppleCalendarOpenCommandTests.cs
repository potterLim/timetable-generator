using System.Diagnostics;
using System.IO;

using TimetableGenerator.Desktop.Exporting.AppleCalendar;

using Xunit;

namespace TimetableGenerator.Desktop.Tests.Exporting.AppleCalendar;

public sealed class ProcessAppleCalendarOpenCommandTests
{
    [Fact]
    public void LaunchRequestTargetsAppleCalendarWithoutShellParsing()
    {
        string sourcePath = Path.Combine(
            Path.GetTempPath(),
            "2026-2학기 시간표 with spaces.ics");
        IcsCalendarFilePath calendarFilePath = new IcsCalendarFilePath(sourcePath);

        ProcessStartInfo startInfo =
            ProcessAppleCalendarOpenCommand.createStartInfo(calendarFilePath);

        Assert.Equal("/usr/bin/open", startInfo.FileName);
        Assert.False(startInfo.UseShellExecute);
        Assert.True(startInfo.CreateNoWindow);
        Assert.True(startInfo.RedirectStandardError);
        Assert.Equal(
            new[] { "-b", "com.apple.iCal", sourcePath },
            startInfo.ArgumentList);
    }
}
