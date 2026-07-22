using System;
using System.IO;

using TimetableGenerator.Desktop.Integrations.GoogleCalendar;

using Xunit;

namespace TimetableGenerator.Desktop.Tests.Integrations.GoogleCalendar;

public sealed class GoogleCalendarOAuthConfigurationPathTests
{
    [Fact]
    public void RelativeFilePathIsNormalized()
    {
        GoogleCalendarOAuthConfigurationPath path = new GoogleCalendarOAuthConfigurationPath("google-calendar.local.json");

        Assert.Equal(Path.GetFullPath("google-calendar.local.json"), path.Value);
    }

    [Fact]
    public void ExistingDirectoryPathIsRejected()
    {
        string directoryPath = Directory.GetCurrentDirectory();

        Assert.Throws<ArgumentException>(() => new GoogleCalendarOAuthConfigurationPath(directoryPath));
    }
}
