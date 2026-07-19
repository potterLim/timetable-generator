using TimetableGenerator.Desktop.Exporting.Calendar;
using TimetableGenerator.Domain.Planning;

using Xunit;

namespace TimetableGenerator.Desktop.Tests.Exporting.Calendar;

public sealed class CalendarExportFileNameFactoryTests
{
    [Fact]
    public void FileNameUsesThePlanNameAndCalendarExtension()
    {
        string fileName = CalendarExportFileNameFactory.Create(
            new PlanName("2026-2학기 시간표"));

        Assert.Equal("2026-2학기 시간표.ics", fileName);
    }

    [Theory]
    [InlineData("공강/실습:안", "공강-실습-안.ics")]
    [InlineData("CON", "CON-.ics")]
    [InlineData("일정.", "일정.ics")]
    public void FileNameIsSafeAcrossDesktopPlatforms(
        string planNameValue,
        string expectedFileName)
    {
        string fileName = CalendarExportFileNameFactory.Create(
            new PlanName(planNameValue));

        Assert.Equal(expectedFileName, fileName);
    }

    [Fact]
    public void MissingPlanNameUsesTheFallbackFileName()
    {
        string fileName = CalendarExportFileNameFactory.Create(null);

        Assert.Equal("시간표.ics", fileName);
    }
}
