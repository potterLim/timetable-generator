using System;

using TimetableGenerator.Desktop.Exporting.Calendar;

using Xunit;

namespace TimetableGenerator.Desktop.Tests.Exporting.Calendar;

public sealed class AcademicTermDateRangeTests
{
    [Fact]
    public void InclusiveDatesArePreserved()
    {
        DateOnly startDate = new DateOnly(2026, 8, 31);
        DateOnly endDate = new DateOnly(2026, 12, 20);

        AcademicTermDateRange dateRange = new AcademicTermDateRange(startDate, endDate);

        Assert.True(dateRange.IsValid);
        Assert.Equal(startDate, dateRange.StartDate);
        Assert.Equal(endDate, dateRange.EndDate);
    }

    [Fact]
    public void SingleDayAcademicTermIsValid()
    {
        DateOnly classDate = new DateOnly(2026, 8, 31);

        AcademicTermDateRange dateRange = new AcademicTermDateRange(classDate, classDate);

        Assert.True(dateRange.IsValid);
    }

    [Fact]
    public void ReversedDatesAreRejectedAtTheDateRangeBoundary()
    {
        ArgumentException exception = Assert.Throws<ArgumentException>(
            () => new AcademicTermDateRange(
                new DateOnly(2026, 12, 20),
                new DateOnly(2026, 8, 31)));

        Assert.Equal("endDate", exception.ParamName);
    }

    [Fact]
    public void DefaultDateRangeIsInvalid()
    {
        Assert.False(default(AcademicTermDateRange).IsValid);
    }
}
