using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TimetableGenerator.Core.Domain;
using TimetableGenerator.Presentation.Schedules;

namespace TimetableGeneratorCore.Tests;

[TestClass]
public sealed class AcademicPeriodTimePolicyTests
{
    [TestMethod]
    public void GetTimeRangeMapsTheFirstPeriodToTheProductTimeContract()
    {
        Period firstPeriod = new Period(1);

        AcademicPeriodTimeRange timeRange = AcademicPeriodTimePolicy.GetTimeRange(firstPeriod);

        Assert.AreEqual(firstPeriod, timeRange.Period);
        Assert.AreEqual(new TimeOnly(8, 30), timeRange.StartTime);
        Assert.AreEqual(new TimeOnly(9, 45), timeRange.EndTime);
        Assert.AreEqual(TimeSpan.FromMinutes(75), timeRange.Duration);
        Assert.IsTrue(timeRange.IsValid);
    }

    [TestMethod]
    public void GetTimeRangeIncludesTheFifteenMinuteBreakBetweenPeriods()
    {
        AcademicPeriodTimeRange firstPeriodRange = AcademicPeriodTimePolicy.GetTimeRange(
            new Period(1));
        AcademicPeriodTimeRange secondPeriodRange = AcademicPeriodTimePolicy.GetTimeRange(
            new Period(2));

        Assert.AreEqual(new TimeOnly(10, 0), secondPeriodRange.StartTime);
        Assert.AreEqual(new TimeOnly(11, 15), secondPeriodRange.EndTime);
        Assert.AreEqual(
            TimeSpan.FromMinutes(15),
            secondPeriodRange.StartTime - firstPeriodRange.EndTime);
    }

    [TestMethod]
    public void GetTimeRangeSupportsPeriodTenWithoutCrossingTheDayBoundary()
    {
        Period maximumSupportedPeriod = AcademicPeriodTimePolicy.MaximumSupportedPeriod;

        AcademicPeriodTimeRange timeRange = AcademicPeriodTimePolicy.GetTimeRange(
            maximumSupportedPeriod);

        Assert.AreEqual(10, maximumSupportedPeriod.Value);
        Assert.AreEqual(new TimeOnly(22, 0), timeRange.StartTime);
        Assert.AreEqual(new TimeOnly(23, 15), timeRange.EndTime);
    }

    [TestMethod]
    public void GetTimeRangeRejectsInvalidAndOutOfRangePeriods()
    {
        Assert.ThrowsExactly<ArgumentException>(
            () => AcademicPeriodTimePolicy.GetTimeRange(default(Period)));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(
            () => AcademicPeriodTimePolicy.GetTimeRange(new Period(11)));
    }
}
