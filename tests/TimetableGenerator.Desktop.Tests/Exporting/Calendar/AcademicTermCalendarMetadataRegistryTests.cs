using System;

using TimetableGenerator.Desktop.Exporting.Calendar;
using TimetableGenerator.Domain.Catalogs;
using TimetableGenerator.Domain.Scheduling;

using Xunit;

namespace TimetableGenerator.Desktop.Tests.Exporting.Calendar;

public sealed class AcademicTermCalendarMetadataRegistryTests
{
    [Fact]
    public void SecondSemesterOf2026UsesTheConfiguredInclusiveClassPeriod()
    {
        AcademicTermCalendarMetadata metadata =
            AcademicTermCalendarMetadataRegistry.FindByTerm(
                AcademicTerm.Parse("2026-2"));

        Assert.Equal(new DateOnly(2026, 8, 31), metadata.DateRange.StartDate);
        Assert.Equal(new DateOnly(2026, 12, 20), metadata.DateRange.EndDate);
        Assert.Equal("Asia/Seoul", metadata.TimeZoneId.Value);
        Assert.Equal(TimeSpan.FromHours(9), metadata.UtcOffset.Value);
    }

    [Fact]
    public void LastIncludedInstantConvertsKoreanLocalTimeToUtc()
    {
        AcademicTermCalendarMetadata metadata =
            AcademicTermCalendarMetadataRegistry.FindByTerm(
                AcademicTerm.Parse("2026-2"));

        DateTimeOffset lastIncludedInstant =
            metadata.GetLastIncludedInstantUtc();

        Assert.Equal(
            new DateTimeOffset(2026, 12, 20, 14, 59, 59, TimeSpan.Zero),
            lastIncludedInstant);
    }

    [Theory]
    [InlineData(EDay.Monday, 2026, 8, 31)]
    [InlineData(EDay.Sunday, 2026, 9, 6)]
    public void FirstOccurrenceIncludesEverySupportedWeekday(
        EDay day,
        int year,
        int month,
        int date)
    {
        AcademicTermCalendarMetadata metadata =
            AcademicTermCalendarMetadataRegistry.FindByTerm(
                AcademicTerm.Parse("2026-2"));

        DateOnly firstOccurrenceDate = metadata.FindFirstOccurrenceDate(day);

        Assert.Equal(new DateOnly(year, month, date), firstOccurrenceDate);
    }

    [Fact]
    public void UnconfiguredAcademicTermIsRejected()
    {
        Assert.Throws<NotSupportedException>(
            () => AcademicTermCalendarMetadataRegistry.FindByTerm(
                AcademicTerm.Parse("2027-1")));
    }

    [Fact]
    public void DefaultUtcOffsetCannotBypassMetadataValidation()
    {
        Assert.False(default(CalendarUtcOffset).IsValid);
        Assert.Throws<ArgumentException>(
            () => new AcademicTermCalendarMetadata(
                AcademicTerm.Parse("2026-2"),
                new AcademicTermDateRange(
                    new DateOnly(2026, 8, 31),
                    new DateOnly(2026, 12, 20)),
                new CalendarTimeZoneId("Asia/Seoul"),
                default));
    }

    [Fact]
    public void DefaultDateRangeCannotBypassMetadataValidation()
    {
        Assert.False(default(AcademicTermDateRange).IsValid);
        Assert.Throws<ArgumentException>(
            () => new AcademicTermCalendarMetadata(
                AcademicTerm.Parse("2026-2"),
                default,
                new CalendarTimeZoneId("Asia/Seoul"),
                new CalendarUtcOffset(TimeSpan.FromHours(9))));
    }
}
