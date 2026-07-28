using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TimetableGenerator.HandongCatalogGenerator.Handong.Source;

namespace TimetableGenerator.HandongCatalogGenerator.Tests.Handong.Source;

[TestClass]
public sealed class HandongExportReaderTests
{
    [TestMethod]
    public async Task ReadAsync_Cp949HtmlWithExactSixteenColumns_ReadsOfferingAsync()
    {
        using (TemporaryHandongSourceFile sourceFile = new TemporaryHandongSourceFile(HandongExportTestHtml.Create()))
        {
            HandongExportDocument document = await HandongExportReader.ReadAsync(sourceFile.FilePath, CancellationToken.None);

            Assert.AreEqual(HandongExportSchema.DECLARED_CHARSET, document.DeclaredCharset);
            Assert.HasCount(1, document.Rows);
            Assert.AreEqual(2, document.Rows[0].SourceRecordNumber.Value);
            EHandongColumn[] columns = Enum.GetValues<EHandongColumn>();
            Assert.HasCount(HandongExportSchema.COLUMN_COUNT, columns);
            foreach (EHandongColumn column in columns)
            {
                Assert.IsNotNull(document.Rows[0].GetCellLines(column));
            }

            CollectionAssert.AreEqual(new string[] { "GCS10001" }, toArray(document.Rows[0].GetCellLines(EHandongColumn.CourseCode)));
            CollectionAssert.AreEqual(new string[] { "01" }, toArray(document.Rows[0].GetCellLines(EHandongColumn.Section)));
        }
    }

    [TestMethod]
    public async Task ReadAsync_BreakElements_PreserveSemanticCellLinesAsync()
    {
        using (TemporaryHandongSourceFile sourceFile = new TemporaryHandongSourceFile(HandongExportTestHtml.Create()))
        {
            HandongExportDocument document = await HandongExportReader.ReadAsync(sourceFile.FilePath, CancellationToken.None);
            HandongRawOfferingRow row = document.Rows[0];

            CollectionAssert.AreEqual(new string[] { "소프트웨어 입문", "(Introduction to Programming)" }, toArray(row.GetCellLines(EHandongColumn.CourseName)));
            CollectionAssert.AreEqual(new string[] { "GLS 주간", "테스트 담당자" }, toArray(row.GetCellLines(EHandongColumn.OfferingInformation)));
            CollectionAssert.AreEqual(new string[] { "화5,금5", "Tue5,Fri5" }, toArray(row.GetCellLines(EHandongColumn.Period)));
        }
    }

    [TestMethod]
    public async Task ReadAsync_PhpWarningOutsideTable_DoesNotBecomeCatalogDataAsync()
    {
        using (TemporaryHandongSourceFile sourceFile = new TemporaryHandongSourceFile(HandongExportTestHtml.Create()))
        {
            HandongExportDocument document = await HandongExportReader.ReadAsync(sourceFile.FilePath, CancellationToken.None);
            StringBuilder catalogTextBuilder = new StringBuilder();
            foreach (EHandongColumn column in Enum.GetValues<EHandongColumn>())
            {
                foreach (string line in document.Rows[0].GetCellLines(column))
                {
                    catalogTextBuilder.AppendLine(line);
                }
            }

            string catalogText = catalogTextBuilder.ToString();
            Assert.IsFalse(catalogText.Contains("ORA-00923", StringComparison.Ordinal));
            Assert.IsFalse(catalogText.Contains("/srv/example/", StringComparison.Ordinal));
            Assert.HasCount(1, document.Rows);
        }
    }

    [TestMethod]
    public async Task ReadAsync_OfferingLink_ExtractsTermCourseAndSectionAsync()
    {
        using (TemporaryHandongSourceFile sourceFile = new TemporaryHandongSourceFile(HandongExportTestHtml.Create()))
        {
            HandongExportDocument document = await HandongExportReader.ReadAsync(sourceFile.FilePath, CancellationToken.None);
            HandongSourceLinkMetadata? metadataOrNull = document.Rows[0].SourceLinkMetadataOrNull;

            Assert.IsNotNull(metadataOrNull);
            Assert.AreEqual("2026-2", metadataOrNull.AcademicTerm.Id);
            Assert.AreEqual("GCS10001", metadataOrNull.CourseCode.Value);
            Assert.AreEqual("01", metadataOrNull.CourseSectionCode.Value);
            Assert.HasCount(1, document.AcademicTerms);
            Assert.AreEqual("2026-2", document.AcademicTerms[0].Id);
        }
    }

    [TestMethod]
    public async Task ReadAsync_ChangedHeader_RejectsSchemaDriftAsync()
    {
        string sourceHtml = HandongExportTestHtml.CreateWithCourseCodeHeader("<td>교과목코드</td>");
        using (TemporaryHandongSourceFile sourceFile = new TemporaryHandongSourceFile(sourceHtml))
        {
            HandongSourceFormatException exception =
                await Assert.ThrowsExactlyAsync<HandongSourceFormatException>(
                    () => HandongExportReader.ReadAsync(
                        sourceFile.FilePath,
                        CancellationToken.None));

            StringAssert.Contains(exception.Message, "exact 16-column");
        }
    }

    [TestMethod]
    public async Task ReadAsync_DataRowWithFifteenColumns_RejectsRecordAsync()
    {
        string sourceHtml = HandongExportTestHtml.CreateWithOfferingColumnCount(15);
        using (TemporaryHandongSourceFile sourceFile = new TemporaryHandongSourceFile(sourceHtml))
        {
            HandongSourceFormatException exception =
                await Assert.ThrowsExactlyAsync<HandongSourceFormatException>(
                    () => HandongExportReader.ReadAsync(
                        sourceFile.FilePath,
                        CancellationToken.None));

            StringAssert.Contains(exception.Message, "Source record 2");
            StringAssert.Contains(exception.Message, "15 columns");
        }
    }

    [TestMethod]
    public async Task ReadAsync_CompoundFileBiffBytes_RejectsBinaryWorkbookAsync()
    {
        byte[] compoundFileHeader = new byte[]
        {
            0xD0,
            0xCF,
            0x11,
            0xE0,
            0xA1,
            0xB1,
            0x1A,
            0xE1,
        };
        using (TemporaryHandongSourceFile sourceFile = new TemporaryHandongSourceFile(compoundFileHeader))
        {
            await Assert.ThrowsExactlyAsync<HandongSourceFormatException>(
                () => HandongExportReader.ReadAsync(
                    sourceFile.FilePath,
                    CancellationToken.None));
        }
    }

    [TestMethod]
    public async Task ReadAsync_InvalidCp949ByteSequence_RejectsSourceAsync()
    {
        byte[] invalidCp949Bytes = new byte[] { 0x81 };
        using (TemporaryHandongSourceFile sourceFile = new TemporaryHandongSourceFile(invalidCp949Bytes))
        {
            HandongSourceFormatException exception =
                await Assert.ThrowsExactlyAsync<HandongSourceFormatException>(
                    () => HandongExportReader.ReadAsync(
                        sourceFile.FilePath,
                        CancellationToken.None));

            StringAssert.Contains(exception.Message, "not valid CP949");
        }
    }

    private static string[] toArray(IReadOnlyList<string> values)
    {
        string[] result = new string[values.Count];
        for (int valueIndex = 0; valueIndex < values.Count; ++valueIndex)
        {
            result[valueIndex] = values[valueIndex];
        }

        return result;
    }
}
