using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TimetableGenerator.HandongCatalogGenerator.Domain;

namespace TimetableGenerator.HandongCatalogGenerator.Tests.Domain;

[TestClass]
public sealed class StrongValueTests
{
    [TestMethod]
    public void CourseSectionCode_TwoDigitValue_PreservesLeadingZero()
    {
        CourseSectionCode sectionCode = new CourseSectionCode("01");

        Assert.AreEqual("01", sectionCode.Value);
    }

    [TestMethod]
    public void CourseCredits_HalfCreditValue_PreservesFraction()
    {
        CourseCredits credits = new CourseCredits(0.5m);

        Assert.AreEqual(0.5m, credits.Value);
    }

    [TestMethod]
    public void EnglishInstructionPercentage_AboveOneHundred_Throws()
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new EnglishInstructionPercentage(101));
    }
}
