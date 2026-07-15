using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TimetableGenerator.UI.Product;

namespace TimetableGeneratorCore.Tests;

[TestClass]
public sealed class ScheduleNavigationValueTests
{
    [TestMethod]
    public void ScheduleIndexUsesZeroBasedTypedNavigation()
    {
        ScheduleIndex firstScheduleIndex = new ScheduleIndex(0);
        ScheduleIndex secondScheduleIndex = firstScheduleIndex.GetNext();

        Assert.IsFalse(firstScheduleIndex.HasPrevious);
        Assert.AreEqual(1, secondScheduleIndex.Value);
        Assert.IsTrue(secondScheduleIndex.HasPrevious);
        Assert.AreEqual(firstScheduleIndex, secondScheduleIndex.GetPrevious());
        Assert.ThrowsExactly<InvalidOperationException>(
            () => firstScheduleIndex.GetPrevious());
    }

    [TestMethod]
    public void ScheduleIndexRejectsNegativeAndOverflowingNavigation()
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new ScheduleIndex(-1));

        ScheduleIndex maximumScheduleIndex = new ScheduleIndex(int.MaxValue);
        Assert.ThrowsExactly<InvalidOperationException>(() => maximumScheduleIndex.GetNext());
    }

    [TestMethod]
    public void ScheduleNumberConvertsIndexesToOneBasedDisplayNumbers()
    {
        ScheduleNumber firstScheduleNumber = ScheduleNumber.FromIndex(new ScheduleIndex(0));
        ScheduleNumber thirdScheduleNumber = ScheduleNumber.FromIndex(new ScheduleIndex(2));

        Assert.AreEqual(1, firstScheduleNumber.Value);
        Assert.IsTrue(firstScheduleNumber.IsValid);
        Assert.AreEqual("1", firstScheduleNumber.ToString());
        Assert.AreEqual(3, thirdScheduleNumber.Value);
        Assert.IsFalse(default(ScheduleNumber).IsValid);
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new ScheduleNumber(0));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(
            () => ScheduleNumber.FromIndex(new ScheduleIndex(int.MaxValue)));
    }
}
