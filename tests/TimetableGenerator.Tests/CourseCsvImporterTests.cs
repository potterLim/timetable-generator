using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TimetableGenerator.Core.Domain;
using TimetableGenerator.Infrastructure.Csv;

namespace TimetableGeneratorCore.Tests;

[TestClass]
public sealed class CourseCsvImporterTests
{
    private const string FOUR_COLUMN_HEADER = "CourseId,Section,Name,TimeSlots\r\n";
    private const string FIVE_COLUMN_HEADER = "CourseId,Section,Name,TimeSlots,Classroom\r\n";

    [TestMethod]
    public void ImportCoursesReadsTheExactFourColumnSchema()
    {
        string fileContent = FOUR_COLUMN_HEADER +
            "1,01,자료구조,월요일1교시/수요일2교시\r\n";

        using (TemporaryCsvFile temporaryCsvFile = new TemporaryCsvFile(fileContent))
        {
            CourseImportResult result = importCourses(temporaryCsvFile);

            Assert.IsTrue(result.IsSuccessful);
            Assert.HasCount(1, result.CourseOfferings);
            Assert.AreEqual("자료구조", result.CourseOfferings[0].Name.Value);
            Assert.HasCount(2, result.CourseOfferings[0].ScheduleSlots);
            Assert.IsFalse(result.CourseOfferings[0].ClassroomAssignment.IsAssigned);
        }
    }

    [TestMethod]
    public void ImportCoursesReadsTheExactFiveColumnSchemaAndMultiWordBuildings()
    {
        string fileContent = FIVE_COLUMN_HEADER +
            "1,01,자료구조,월요일1교시,Engineering Hall 101\r\n";

        using (TemporaryCsvFile temporaryCsvFile = new TemporaryCsvFile(fileContent))
        {
            CourseImportResult result = importCourses(temporaryCsvFile);

            Assert.IsTrue(result.IsSuccessful);
            ClassroomAssignment classroomAssignment =
                result.CourseOfferings[0].ClassroomAssignment;
            Assert.IsTrue(classroomAssignment.IsAssigned);
            ClassroomLocation classroomLocation = classroomAssignment.GetClassroomLocation();
            Assert.AreEqual("Engineering Hall", classroomLocation.BuildingName.Value);
            Assert.AreEqual("101", classroomLocation.RoomIdentifier.Value);
        }
    }

    [TestMethod]
    public void ImportCoursesSupportsQuotedCommasEscapedQuotesAndNewlines()
    {
        string fileContent = FOUR_COLUMN_HEADER +
            "1,01,\"자료, \"\"구조\"\"\",월요일1교시\r\n" +
            "2,01,\"운영체제\r\n심화\",화요일2교시\r\n";

        using (TemporaryCsvFile temporaryCsvFile = new TemporaryCsvFile(fileContent))
        {
            CourseImportResult result = importCourses(temporaryCsvFile);

            Assert.IsTrue(result.IsSuccessful);
            Assert.AreEqual("자료, \"구조\"", result.CourseOfferings[0].Name.Value);
            Assert.AreEqual("운영체제\r\n심화", result.CourseOfferings[1].Name.Value);
        }
    }

    [TestMethod]
    public void ImportCoursesReportsThePhysicalRowAfterAQuotedMultilineRecord()
    {
        string fileContent = FOUR_COLUMN_HEADER +
            "1,01,\"운영체제\r\n심화\",월요일1교시\r\n" +
            "2,01,자료구조,월요일1교시junk\r\n";

        using (TemporaryCsvFile temporaryCsvFile = new TemporaryCsvFile(fileContent))
        {
            CourseImportResult result = importCourses(temporaryCsvFile);

            Assert.IsFalse(result.IsSuccessful);
            Assert.HasCount(1, result.Diagnostics);
            Assert.AreEqual(
                ECourseImportErrorCode.InvalidScheduleSlot,
                result.Diagnostics[0].ErrorCode);
            Assert.IsTrue(result.Diagnostics[0].SourcePosition.HasRowNumber);
            Assert.AreEqual(
                4L,
                result.Diagnostics[0].SourcePosition.GetRowNumber().Value);
        }
    }

    [TestMethod]
    public void ImportCoursesRejectsHeadersThatDoNotExactlyMatchTheSchema()
    {
        string fileContent = "Section,CourseId,Name,TimeSlots\r\n" +
            "01,1,자료구조,월요일1교시\r\n";

        using (TemporaryCsvFile temporaryCsvFile = new TemporaryCsvFile(fileContent))
        {
            CourseImportResult result = importCourses(temporaryCsvFile);

            assertSingleDiagnostic(result, ECourseImportErrorCode.InvalidHeader);
        }
    }

    [TestMethod]
    public void ImportCoursesRejectsTrailingTimeSlotTextAndEmptyTokens()
    {
        string fileContent = FOUR_COLUMN_HEADER +
            "1,01,자료구조,월요일1교시junk\r\n" +
            "2,01,알고리즘,화요일2교시//수요일3교시\r\n";

        using (TemporaryCsvFile temporaryCsvFile = new TemporaryCsvFile(fileContent))
        {
            CourseImportResult result = importCourses(temporaryCsvFile);

            Assert.IsFalse(result.IsSuccessful);
            Assert.IsEmpty(result.CourseOfferings);
            Assert.HasCount(2, result.Diagnostics);
            Assert.AreEqual(
                ECourseImportErrorCode.InvalidScheduleSlot,
                result.Diagnostics[0].ErrorCode);
            Assert.AreEqual(
                ECourseImportErrorCode.EmptyScheduleSlot,
                result.Diagnostics[1].ErrorCode);
        }
    }

    [TestMethod]
    public void ImportCoursesRejectsRecordColumnCountsThatDifferFromTheHeader()
    {
        string fileContent = FIVE_COLUMN_HEADER +
            "1,01,자료구조,월요일1교시\r\n";

        using (TemporaryCsvFile temporaryCsvFile = new TemporaryCsvFile(fileContent))
        {
            CourseImportResult result = importCourses(temporaryCsvFile);

            assertSingleDiagnostic(result, ECourseImportErrorCode.InvalidColumnCount);
        }
    }

    [TestMethod]
    public void ImportCoursesReturnsATypedDiagnosticForMalformedQuotedRecords()
    {
        string fileContent = FOUR_COLUMN_HEADER +
            "1,01,\"자료구조,월요일1교시\r\n";

        using (TemporaryCsvFile temporaryCsvFile = new TemporaryCsvFile(fileContent))
        {
            CourseImportResult result = importCourses(temporaryCsvFile);

            assertSingleDiagnostic(result, ECourseImportErrorCode.MalformedCsvRecord);
            Assert.IsTrue(result.Diagnostics[0].SourcePosition.HasRowNumber);
            Assert.AreEqual(
                2L,
                result.Diagnostics[0].SourcePosition.GetRowNumber().Value);
        }
    }

    [TestMethod]
    public void ImportCoursesCollectsDiagnosticsUntilTheConfiguredLimit()
    {
        string fileContent = FOUR_COLUMN_HEADER +
            "bad,01,자료구조,월요일1교시\r\n" +
            "also-bad,01,알고리즘,화요일2교시\r\n";

        using (TemporaryCsvFile temporaryCsvFile = new TemporaryCsvFile(fileContent))
        {
            CourseCsvImporter importer = new CourseCsvImporter();
            CourseCsvImportOptions options = new CourseCsvImportOptions(
                new DiagnosticCountLimit(1));

            CourseImportResult result = importer.ImportCourses(
                temporaryCsvFile.FilePath,
                options);

            Assert.IsFalse(result.IsSuccessful);
            Assert.HasCount(1, result.Diagnostics);
            Assert.IsTrue(result.HasReachedDiagnosticLimit);
        }
    }

    [TestMethod]
    public void ImportCoursesReturnsATypedFileNotFoundDiagnostic()
    {
        string missingFilePath = Path.Combine(
            Path.GetTempPath(),
            Guid.NewGuid().ToString("N") + ".csv");
        CsvInputFilePath inputFilePath = new CsvInputFilePath(missingFilePath);
        CourseCsvImporter importer = new CourseCsvImporter();

        CourseImportResult result = importer.ImportCourses(inputFilePath);

        assertSingleDiagnostic(result, ECourseImportErrorCode.FileNotFound);
        Assert.AreEqual(ECsvColumn.File, result.Diagnostics[0].Column);
    }

    private static CourseImportResult importCourses(TemporaryCsvFile temporaryCsvFile)
    {
        CourseCsvImporter importer = new CourseCsvImporter();
        return importer.ImportCourses(temporaryCsvFile.FilePath);
    }

    private static void assertSingleDiagnostic(
        CourseImportResult result,
        ECourseImportErrorCode expectedErrorCode)
    {
        Assert.IsFalse(result.IsSuccessful);
        Assert.IsEmpty(result.CourseOfferings);
        Assert.HasCount(1, result.Diagnostics);
        Assert.AreEqual(expectedErrorCode, result.Diagnostics[0].ErrorCode);
    }
}
