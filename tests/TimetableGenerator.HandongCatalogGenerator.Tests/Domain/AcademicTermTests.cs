using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TimetableGenerator.HandongCatalogGenerator.Domain;

namespace TimetableGenerator.HandongCatalogGenerator.Tests.Domain;

[TestClass]
public sealed class AcademicTermTests
{
    [TestMethod]
    public void Parse_ValidTerm_PreservesYearSemesterAndId()
    {
        AcademicTerm term = AcademicTerm.Parse("2026-2");

        Assert.AreEqual(2026, term.AcademicYear.Value);
        Assert.AreEqual(2, term.Semester.Value);
        Assert.AreEqual("2026-2", term.Id);
    }

    [TestMethod]
    [DataRow("")]
    [DataRow("2026")]
    [DataRow("2026-fall")]
    public void Parse_InvalidFormat_ThrowsFormatException(string value)
    {
        Assert.ThrowsExactly<FormatException>(() => AcademicTerm.Parse(value));
    }

    [TestMethod]
    public void Parse_UnsupportedSemester_ThrowsArgumentOutOfRangeException()
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => AcademicTerm.Parse("2026-3"));
    }
}
