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
        AcademicTermCalendarMetadata metadata = getSeoulCalendarMetadata();

        Assert.Equal(new DateOnly(2026, 8, 31), metadata.DateRange.StartDate);
        Assert.Equal(new DateOnly(2026, 12, 20), metadata.DateRange.EndDate);
        Assert.Equal("Asia/Seoul", metadata.TimeZoneId.Value);
        Assert.Equal(
            TimeSpan.FromHours(9),
            metadata.TimeZoneId.FindUtcOffset(
                new DateOnly(2026, 8, 31),
                new TimeOnly(11, 30)).Value);
    }

    [Fact]
    public void LastIncludedInstantConvertsKoreanLocalTimeToUtc()
    {
        AcademicTermCalendarMetadata metadata = getSeoulCalendarMetadata();

        DateTimeOffset lastIncludedInstant = metadata.GetLastIncludedInstantUtc();

        Assert.Equal(new DateTimeOffset(2026, 12, 20, 14, 59, 59, TimeSpan.Zero), lastIncludedInstant);
    }

    [Fact]
    public void UtcMetadataUsesZeroOffsetAndPreservesTheInclusiveTermEnd()
    {
        AcademicTermCalendarMetadata metadata = AcademicTermCalendarMetadataRegistry.findByTerm(
            AcademicTerm.Parse("2026-2"),
            new CalendarTimeZoneId("Etc/UTC"));

        CalendarUtcOffset localOffset = metadata.TimeZoneId.FindUtcOffset(new DateOnly(2026, 8, 31), new TimeOnly(11, 30));
        DateTimeOffset lastIncludedInstant = metadata.GetLastIncludedInstantUtc();

        Assert.Equal(TimeSpan.Zero, localOffset.Value);
        Assert.Equal(new DateTimeOffset(2026, 12, 20, 23, 59, 59, TimeSpan.Zero), lastIncludedInstant);
    }

    [Theory]
    [InlineData(EDay.Monday, 2026, 8, 31)]
    [InlineData(EDay.Sunday, 2026, 9, 6)]
    public void FirstOccurrenceIncludesEverySupportedWeekday(EDay day, int year, int month, int date)
    {
        AcademicTermCalendarMetadata metadata = getSeoulCalendarMetadata();

        DateOnly firstOccurrenceDate = metadata.FindFirstOccurrenceDate(day);

        Assert.Equal(new DateOnly(year, month, date), firstOccurrenceDate);
    }

    [Fact]
    public void UnconfiguredAcademicTermIsRejected()
    {
        Assert.Throws<NotSupportedException>(
            () => AcademicTermCalendarMetadataRegistry.findByTerm(
                AcademicTerm.Parse("2027-1"),
                new CalendarTimeZoneId("Asia/Seoul")));
    }

    [Fact]
    public void DefaultRegistryMetadataUsesTheCurrentSystemTimeZone()
    {
        CalendarTimeZoneId expectedTimeZoneId = CalendarTimeZoneId.CreateFromSystemTimeZone(TimeZoneInfo.Local);

        AcademicTermCalendarMetadata metadata = AcademicTermCalendarMetadataRegistry.FindByTerm(AcademicTerm.Parse("2026-2"));

        Assert.Equal(expectedTimeZoneId, metadata.TimeZoneId);
    }

    [Fact]
    public void DefaultTimeZoneIdCannotBypassMetadataValidation()
    {
        Assert.False(default(CalendarTimeZoneId).IsValid);
        Assert.Throws<ArgumentException>(
            () => new AcademicTermCalendarMetadata(
                AcademicTerm.Parse("2026-2"),
                new AcademicTermDateRange(
                    new DateOnly(2026, 8, 31),
                    new DateOnly(2026, 12, 20)),
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
                new CalendarTimeZoneId("Asia/Seoul")));
    }

    [Fact]
    public void UtcOffsetsFollowIanaRulesForEachCalendarDate()
    {
        CalendarTimeZoneId timeZoneId = new CalendarTimeZoneId("America/New_York");

        CalendarUtcOffset winterOffset = timeZoneId.FindUtcOffset(
            new DateOnly(2026, 1, 15),
            new TimeOnly(11, 30));
        CalendarUtcOffset summerOffset = timeZoneId.FindUtcOffset(
            new DateOnly(2026, 7, 15),
            new TimeOnly(11, 30));

        Assert.Equal(TimeSpan.FromHours(-5), winterOffset.Value);
        Assert.Equal(TimeSpan.FromHours(-4), summerOffset.Value);
    }

    [Fact]
    public void NonexistentDaylightTransitionTimeIsRejected()
    {
        CalendarTimeZoneId timeZoneId = new CalendarTimeZoneId("America/New_York");

        Assert.Throws<InvalidOperationException>(
            () => timeZoneId.ResolveLocalDateTime(
                new DateOnly(2026, 3, 8),
                new TimeOnly(2, 30)));
    }

    [Fact]
    public void AmbiguousDaylightTransitionTimeUsesFirstOccurrence()
    {
        CalendarTimeZoneId timeZoneId = new CalendarTimeZoneId("America/New_York");

        DateTimeOffset resolvedTime = timeZoneId.ResolveLocalDateTime(
            new DateOnly(2026, 11, 1),
            new TimeOnly(1, 30));

        Assert.Equal(TimeSpan.FromHours(-4.0), resolvedTime.Offset);
        Assert.Equal(
            new DateTimeOffset(
                2026,
                11,
                1,
                5,
                30,
                0,
                TimeSpan.Zero),
            resolvedTime.ToUniversalTime());
    }

    [Fact]
    public void PlatformSpecificTimeZoneIdIsRejected()
    {
        Assert.Throws<ArgumentException>(
            () => new CalendarTimeZoneId("Korea Standard Time"));
    }

    [Fact]
    public void WindowsSystemTimeZoneIdIsNormalizedToIana()
    {
        TimeZoneInfo windowsTimeZone = TimeZoneInfo.CreateCustomTimeZone(
            "Korea Standard Time",
            TimeSpan.FromHours(9.0),
            "Korea Standard Time",
            "Korea Standard Time");

        CalendarTimeZoneId timeZoneId = CalendarTimeZoneId.CreateFromSystemTimeZone(windowsTimeZone);

        Assert.Equal("Asia/Seoul", timeZoneId.Value);
    }

    [Fact]
    public void IanaSystemTimeZoneIdIsPreserved()
    {
        TimeZoneInfo ianaTimeZone = TimeZoneInfo.FindSystemTimeZoneById("America/New_York");

        CalendarTimeZoneId timeZoneId = CalendarTimeZoneId.CreateFromSystemTimeZone(ianaTimeZone);

        Assert.Equal("America/New_York", timeZoneId.Value);
    }

    [Fact]
    public void UnknownSystemTimeZoneIdCannotBeExported()
    {
        TimeZoneInfo unsupportedTimeZone = TimeZoneInfo.CreateCustomTimeZone(
            "Product Custom Time",
            TimeSpan.FromHours(3.0),
            "Product Custom Time",
            "Product Custom Time");

        Assert.Throws<ArgumentException>(
            () => CalendarTimeZoneId.CreateFromSystemTimeZone(
                unsupportedTimeZone));
    }

    private static AcademicTermCalendarMetadata getSeoulCalendarMetadata()
    {
        return AcademicTermCalendarMetadataRegistry.findByTerm(
            AcademicTerm.Parse("2026-2"),
            new CalendarTimeZoneId("Asia/Seoul"));
    }
}
