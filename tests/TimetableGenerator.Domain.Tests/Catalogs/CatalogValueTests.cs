using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TimetableGenerator.Domain.Catalogs;

namespace TimetableGenerator.Domain.Tests.Catalogs;

[TestClass]
public sealed class CatalogValueTests
{
    [TestMethod]
    public void TextValuesNormalizeAndPreserveTheirMeanings()
    {
        InstitutionId institutionId = new InstitutionId("  handong-global-university  ");
        CatalogId catalogId = new CatalogId("  handong-global-university:2026-2:r0001  ");
        CourseId courseId = new CourseId("  handong-global-university:CSE30001  ");
        OfferingId offeringId = new OfferingId("  handong-global-university:2026-2:CSE30001:01  ");
        KoreanCourseName koreanName = new KoreanCourseName("  자료구조  ");
        EnglishCourseName englishName = new EnglishCourseName("  Data Structures  ");

        Assert.AreEqual("handong-global-university", institutionId.Value);
        Assert.AreEqual("handong-global-university:2026-2:r0001", catalogId.Value);
        Assert.AreEqual("handong-global-university:CSE30001", courseId.Value);
        Assert.AreEqual("handong-global-university:2026-2:CSE30001:01", offeringId.Value);
        Assert.AreEqual("자료구조", koreanName.Value);
        Assert.AreEqual("Data Structures", englishName.Value);
    }

    [TestMethod]
    public void TextValuesRejectMissingContent()
    {
        Assert.ThrowsExactly<ArgumentException>(() => new InstitutionName("  "));
        Assert.ThrowsExactly<ArgumentException>(() => new CatalogId(string.Empty));
        Assert.ThrowsExactly<ArgumentException>(() => new CourseId("\t"));
        Assert.ThrowsExactly<ArgumentException>(() => new OfferingId("\r\n"));
        Assert.ThrowsExactly<ArgumentNullException>(() => new KoreanCourseName(null!));
    }

    [TestMethod]
    public void CourseCodeAndSectionCodeEnforceCanonicalFormats()
    {
        CourseCode courseCode = new CourseCode(" CSE30001 ");
        CourseSectionCode sectionCode = new CourseSectionCode(" 01 ");

        Assert.AreEqual("CSE30001", courseCode.Value);
        Assert.AreEqual("01", sectionCode.Value);
        Assert.ThrowsExactly<ArgumentException>(() => new CourseCode("cse30001"));
        Assert.ThrowsExactly<ArgumentException>(() => new CourseSectionCode("1"));
    }

    [TestMethod]
    public void AcademicTermPreservesStrongYearAndSemesterValues()
    {
        AcademicTerm term = AcademicTerm.Parse("2026-2");

        Assert.IsTrue(term.IsValid);
        Assert.AreEqual(2026, term.AcademicYear.Value);
        Assert.AreEqual(2, term.Semester.Value);
        Assert.AreEqual("2026-2", term.Id);
        Assert.ThrowsExactly<FormatException>(() => AcademicTerm.Parse("2026"));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => AcademicTerm.Parse("2026-3"));
    }

    [TestMethod]
    public void CreditsAndRevisionRejectNonCanonicalValues()
    {
        CourseCredits credits = new CourseCredits(0.5m);
        CatalogRevision revision = new CatalogRevision(1);

        Assert.AreEqual(0.5m, credits.Value);
        Assert.AreEqual("r0001", revision.FileComponent);
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new CourseCredits(0.25m));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new CatalogRevision(0));
    }
}
