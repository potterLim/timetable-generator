using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
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
            CourseImportResult courseImportResult = importCourses(temporaryCsvFile);

            Assert.IsTrue(courseImportResult.IsSuccessful);
            Assert.HasCount(1, courseImportResult.CourseOfferings);
            Assert.AreEqual("자료구조", courseImportResult.CourseOfferings[0].Name.Value);
            Assert.HasCount(2, courseImportResult.CourseOfferings[0].ScheduleSlots);
            Assert.IsFalse(courseImportResult.CourseOfferings[0].ClassroomAssignment.IsAssigned);
        }
    }

    [TestMethod]
    public void ImportCoursesReadsTheExactFiveColumnSchemaAndMultiWordBuildings()
    {
        string fileContent = FIVE_COLUMN_HEADER +
            "1,01,자료구조,월요일1교시,Engineering Hall 101\r\n";

        using (TemporaryCsvFile temporaryCsvFile = new TemporaryCsvFile(fileContent))
        {
            CourseImportResult courseImportResult = importCourses(temporaryCsvFile);

            Assert.IsTrue(courseImportResult.IsSuccessful);
            ClassroomAssignment classroomAssignment =
                courseImportResult.CourseOfferings[0].ClassroomAssignment;
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
            CourseImportResult courseImportResult = importCourses(temporaryCsvFile);

            Assert.IsTrue(courseImportResult.IsSuccessful);
            Assert.AreEqual("자료, \"구조\"", courseImportResult.CourseOfferings[0].Name.Value);
            Assert.AreEqual("운영체제\r\n심화", courseImportResult.CourseOfferings[1].Name.Value);
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
            CourseImportResult courseImportResult = importCourses(temporaryCsvFile);

            Assert.IsFalse(courseImportResult.IsSuccessful);
            Assert.HasCount(1, courseImportResult.Diagnostics);
            Assert.AreEqual(
                ECourseImportErrorCode.InvalidScheduleSlot,
                courseImportResult.Diagnostics[0].ErrorCode);
            Assert.IsTrue(courseImportResult.Diagnostics[0].SourcePosition.HasRowNumber);
            Assert.AreEqual(
                4L,
                courseImportResult.Diagnostics[0].SourcePosition.GetRowNumber().Value);
            Assert.AreEqual(
                "월요일1교시junk",
                courseImportResult.Diagnostics[0].RawValue.Value);
        }
    }

    [TestMethod]
    public void ImportCoursesRejectsHeadersThatDoNotExactlyMatchTheSchema()
    {
        string fileContent = "Section,CourseId,Name,TimeSlots\r\n" +
            "01,1,자료구조,월요일1교시\r\n";

        using (TemporaryCsvFile temporaryCsvFile = new TemporaryCsvFile(fileContent))
        {
            CourseImportResult courseImportResult = importCourses(temporaryCsvFile);

            assertSingleDiagnostic(courseImportResult, ECourseImportErrorCode.InvalidHeader);
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
            CourseImportResult courseImportResult = importCourses(temporaryCsvFile);

            Assert.IsFalse(courseImportResult.IsSuccessful);
            Assert.IsEmpty(courseImportResult.CourseOfferings);
            Assert.HasCount(2, courseImportResult.Diagnostics);
            Assert.AreEqual(
                ECourseImportErrorCode.InvalidScheduleSlot,
                courseImportResult.Diagnostics[0].ErrorCode);
            Assert.AreEqual(
                ECourseImportErrorCode.EmptyScheduleSlot,
                courseImportResult.Diagnostics[1].ErrorCode);
        }
    }

    [TestMethod]
    public void ImportCoursesRejectsRecordColumnCountsThatDifferFromTheHeader()
    {
        string fileContent = FIVE_COLUMN_HEADER +
            "1,01,자료구조,월요일1교시\r\n";

        using (TemporaryCsvFile temporaryCsvFile = new TemporaryCsvFile(fileContent))
        {
            CourseImportResult courseImportResult = importCourses(temporaryCsvFile);

            assertSingleDiagnostic(courseImportResult, ECourseImportErrorCode.InvalidColumnCount);
        }
    }

    [TestMethod]
    public void ImportCoursesReturnsATypedDiagnosticForMalformedQuotedRecords()
    {
        string fileContent = FOUR_COLUMN_HEADER +
            "1,01,\"자료구조,월요일1교시\r\n";

        using (TemporaryCsvFile temporaryCsvFile = new TemporaryCsvFile(fileContent))
        {
            CourseImportResult courseImportResult = importCourses(temporaryCsvFile);

            assertSingleDiagnostic(courseImportResult, ECourseImportErrorCode.MalformedCsvRecord);
            Assert.IsTrue(courseImportResult.Diagnostics[0].SourcePosition.HasRowNumber);
            Assert.AreEqual(
                2L,
                courseImportResult.Diagnostics[0].SourcePosition.GetRowNumber().Value);
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

            CourseImportResult courseImportResult = importer.ImportCourses(
                temporaryCsvFile.FilePath,
                options);

            Assert.IsFalse(courseImportResult.IsSuccessful);
            Assert.HasCount(1, courseImportResult.Diagnostics);
            Assert.IsTrue(courseImportResult.HasReachedDiagnosticLimit);
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

        CourseImportResult courseImportResult = importer.ImportCourses(inputFilePath);

        assertSingleDiagnostic(courseImportResult, ECourseImportErrorCode.FileNotFound);
        Assert.AreEqual(ECsvColumn.File, courseImportResult.Diagnostics[0].Column);
        Assert.AreEqual(missingFilePath, courseImportResult.Diagnostics[0].RawValue.Value);
        Assert.AreEqual(Path.GetFileName(missingFilePath), inputFilePath.FileName.Value);
    }

    [TestMethod]
    public void ImportCoursesWithDefaultOptionsThrowsForPreCanceledWork()
    {
        string fileContent = FOUR_COLUMN_HEADER +
            "1,01,자료구조,월요일1교시\r\n";

        using (TemporaryCsvFile temporaryCsvFile = new TemporaryCsvFile(fileContent))
        {
            using (CancellationTokenSource cancellationTokenSource =
                new CancellationTokenSource())
            {
                cancellationTokenSource.Cancel();
                CourseCsvImporter importer = new CourseCsvImporter();

                Assert.ThrowsExactly<OperationCanceledException>(
                    () => importer.ImportCourses(
                        temporaryCsvFile.FilePath,
                        cancellationTokenSource.Token));
            }
        }
    }

    [TestMethod]
    public void ImportCoursesWithExplicitOptionsThrowsForPreCanceledWork()
    {
        string fileContent = FOUR_COLUMN_HEADER +
            "1,01,자료구조,월요일1교시\r\n";

        using (TemporaryCsvFile temporaryCsvFile = new TemporaryCsvFile(fileContent))
        {
            using (CancellationTokenSource cancellationTokenSource =
                new CancellationTokenSource())
            {
                cancellationTokenSource.Cancel();
                CourseCsvImporter importer = new CourseCsvImporter();
                CourseCsvImportOptions options = CourseCsvImportOptions.CreateDefault();

                Assert.ThrowsExactly<OperationCanceledException>(
                    () => importer.ImportCourses(
                        temporaryCsvFile.FilePath,
                        options,
                        cancellationTokenSource.Token));
            }
        }
    }

    private static CourseImportResult importCourses(TemporaryCsvFile temporaryCsvFile)
    {
        CourseCsvImporter importer = new CourseCsvImporter();
        return importer.ImportCourses(temporaryCsvFile.FilePath);
    }

    private static void assertSingleDiagnostic(
        CourseImportResult courseImportResult,
        ECourseImportErrorCode expectedErrorCode)
    {
        Assert.IsFalse(courseImportResult.IsSuccessful);
        Assert.IsEmpty(courseImportResult.CourseOfferings);
        Assert.HasCount(1, courseImportResult.Diagnostics);
        Assert.AreEqual(expectedErrorCode, courseImportResult.Diagnostics[0].ErrorCode);
    }
}
