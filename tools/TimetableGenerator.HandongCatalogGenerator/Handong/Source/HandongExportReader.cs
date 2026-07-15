using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AngleSharp.Dom;
using AngleSharp.Html.Dom;
using AngleSharp.Html.Parser;
using TimetableGenerator.HandongCatalogGenerator.Domain;

namespace TimetableGenerator.HandongCatalogGenerator.Handong.Source;

internal static class HandongExportReader
{
    private const int CP949_CODE_PAGE = 949;
    private const int HEADER_ROW_INDEX = 0;
    private const int FIRST_DATA_ROW_INDEX = 1;

    private static readonly Encoding CP949_ENCODING = createCp949Encoding();

    public static async Task<HandongExportDocument> ReadAsync(
        CatalogSourceFilePath sourceFilePath,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(sourceFilePath);

        byte[] sourceBytes = await File.ReadAllBytesAsync(
            sourceFilePath.Value,
            cancellationToken).ConfigureAwait(false);
        string sourceSha256Hex = calculateSha256Hex(sourceBytes);
        string sourceHtml = decodeSourceHtml(sourceBytes, sourceFilePath);
        IHtmlDocument htmlDocument = await parseSourceHtmlAsync(
            sourceHtml,
            cancellationToken).ConfigureAwait(false);

        string declaredCharset = readDeclaredCharset(htmlDocument);
        IElement catalogTable = findCatalogTable(htmlDocument);
        IReadOnlyList<IElement> tableRows = getTableRows(catalogTable);
        ReadRowsResult rowsResult = readOfferingRows(tableRows);

        return new HandongExportDocument(
            sourceSha256Hex,
            sourceBytes.LongLength,
            declaredCharset,
            rowsResult.AcademicTerms,
            rowsResult.Rows);
    }

    private static Encoding createCp949Encoding()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        return Encoding.GetEncoding(
            CP949_CODE_PAGE,
            EncoderFallback.ExceptionFallback,
            DecoderFallback.ExceptionFallback);
    }

    private static string calculateSha256Hex(byte[] sourceBytes)
    {
        byte[] sourceHashBytes = SHA256.HashData(sourceBytes);
        return Convert.ToHexString(sourceHashBytes).ToLowerInvariant();
    }

    private static string decodeSourceHtml(
        byte[] sourceBytes,
        CatalogSourceFilePath sourceFilePath)
    {
        try
        {
            return CP949_ENCODING.GetString(sourceBytes);
        }
        catch (DecoderFallbackException exception)
        {
            throw new HandongSourceFormatException(
                "The Handong export is not valid CP949 text: " + sourceFilePath.Value,
                exception);
        }
    }

    private static async Task<IHtmlDocument> parseSourceHtmlAsync(
        string sourceHtml,
        CancellationToken cancellationToken)
    {
        HtmlParserOptions parserOptions = default;
        parserOptions.IsScripting = false;
        parserOptions.IsStrictMode = false;

        HtmlParser htmlParser = new HtmlParser(parserOptions);
        try
        {
            return await htmlParser.ParseDocumentAsync(
                sourceHtml,
                cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (HtmlParseException exception)
        {
            throw new HandongSourceFormatException(
                "The Handong export could not be parsed as HTML.",
                exception);
        }
    }

    private static string readDeclaredCharset(IHtmlDocument htmlDocument)
    {
        string? declaredCharsetOrNull = findDeclaredCharsetOrNull(htmlDocument);
        if (declaredCharsetOrNull == null)
        {
            throw new HandongSourceFormatException(
                "The Handong export does not declare an HTML charset.");
        }

        if (string.Equals(
            declaredCharsetOrNull,
            HandongExportSchema.DECLARED_CHARSET,
            StringComparison.OrdinalIgnoreCase) == false)
        {
            throw new HandongSourceFormatException(
                "The Handong export declares unsupported charset '"
                + declaredCharsetOrNull + "'. Expected '"
                + HandongExportSchema.DECLARED_CHARSET + "'.");
        }

        return declaredCharsetOrNull;
    }

    private static string? findDeclaredCharsetOrNull(IHtmlDocument htmlDocument)
    {
        foreach (IElement metadataElement in htmlDocument.QuerySelectorAll("meta"))
        {
            string? directCharsetOrNull = metadataElement.GetAttribute("charset");
            if (string.IsNullOrWhiteSpace(directCharsetOrNull) == false)
            {
                return directCharsetOrNull.Trim();
            }

            string? contentOrNull = metadataElement.GetAttribute("content");
            if (string.IsNullOrWhiteSpace(contentOrNull))
            {
                continue;
            }

            string? contentCharsetOrNull = findCharsetInContentOrNull(contentOrNull);
            if (contentCharsetOrNull != null)
            {
                return contentCharsetOrNull;
            }
        }

        return null;
    }

    private static string? findCharsetInContentOrNull(string metadataContent)
    {
        const string CHARSET_TOKEN = "charset";

        int charsetTokenIndex = metadataContent.IndexOf(
            CHARSET_TOKEN,
            StringComparison.OrdinalIgnoreCase);
        if (charsetTokenIndex < 0)
        {
            return null;
        }

        int separatorIndex = charsetTokenIndex + CHARSET_TOKEN.Length;
        while (separatorIndex < metadataContent.Length
            && char.IsWhiteSpace(metadataContent[separatorIndex]))
        {
            ++separatorIndex;
        }

        if (separatorIndex >= metadataContent.Length
            || metadataContent[separatorIndex] != '=')
        {
            return null;
        }

        int charsetValueIndex = separatorIndex + 1;
        while (charsetValueIndex < metadataContent.Length
            && (char.IsWhiteSpace(metadataContent[charsetValueIndex])
                || metadataContent[charsetValueIndex] == '\''
                || metadataContent[charsetValueIndex] == '"'))
        {
            ++charsetValueIndex;
        }

        int charsetValueEndIndex = charsetValueIndex;
        while (charsetValueEndIndex < metadataContent.Length
            && char.IsWhiteSpace(metadataContent[charsetValueEndIndex]) == false
            && metadataContent[charsetValueEndIndex] != ';'
            && metadataContent[charsetValueEndIndex] != '\''
            && metadataContent[charsetValueEndIndex] != '"')
        {
            ++charsetValueEndIndex;
        }

        if (charsetValueEndIndex == charsetValueIndex)
        {
            return null;
        }

        return metadataContent.Substring(
            charsetValueIndex,
            charsetValueEndIndex - charsetValueIndex);
    }

    private static IElement findCatalogTable(IHtmlDocument htmlDocument)
    {
        IHtmlCollection<IElement> tableElements = htmlDocument.QuerySelectorAll("table");
        if (tableElements.Length == 0)
        {
            throw new HandongSourceFormatException(
                "The Handong export does not contain an HTML table.");
        }

        List<IElement> matchingTableElements = new List<IElement>();
        foreach (IElement tableElement in tableElements)
        {
            IReadOnlyList<IElement> tableRows = getTableRows(tableElement);
            if (tableRows.Count == 0)
            {
                continue;
            }

            if (hasExpectedHeader(tableRows[HEADER_ROW_INDEX]))
            {
                matchingTableElements.Add(tableElement);
            }
        }

        if (matchingTableElements.Count == 0)
        {
            throw new HandongSourceFormatException(
                "No HTML table has the exact 16-column Handong export header.");
        }

        if (matchingTableElements.Count > 1)
        {
            throw new HandongSourceFormatException(
                "The Handong export contains more than one matching catalog table.");
        }

        return matchingTableElements[0];
    }

    private static IReadOnlyList<IElement> getTableRows(IElement tableElement)
    {
        List<IElement> tableRows = new List<IElement>();
        foreach (IElement tableChildElement in tableElement.Children)
        {
            if (isElementNamed(tableChildElement, "tr"))
            {
                tableRows.Add(tableChildElement);
                continue;
            }

            if (isTableSectionElement(tableChildElement) == false)
            {
                continue;
            }

            foreach (IElement sectionChildElement in tableChildElement.Children)
            {
                if (isElementNamed(sectionChildElement, "tr"))
                {
                    tableRows.Add(sectionChildElement);
                }
            }
        }

        return new ReadOnlyCollection<IElement>(tableRows);
    }

    private static bool isTableSectionElement(IElement element)
    {
        return isElementNamed(element, "thead")
            || isElementNamed(element, "tbody")
            || isElementNamed(element, "tfoot");
    }

    private static bool isElementNamed(IElement element, string expectedLocalName)
    {
        return string.Equals(
            element.LocalName,
            expectedLocalName,
            StringComparison.OrdinalIgnoreCase);
    }

    private static bool hasExpectedHeader(IElement headerRowElement)
    {
        IReadOnlyList<IElement> headerCellElements = getRowCells(headerRowElement);
        if (headerCellElements.Count != HandongExportSchema.COLUMN_COUNT)
        {
            return false;
        }

        foreach (EHandongColumn column in HandongExportSchema.Columns)
        {
            int columnIndex = HandongExportSchema.GetColumnIndex(column);
            IReadOnlyList<string> headerLines = HandongHtmlCellReader.ReadLines(
                headerCellElements[columnIndex]);
            if (HandongExportSchema.IsExpectedHeader(column, headerLines) == false)
            {
                return false;
            }
        }

        return true;
    }

    private static IReadOnlyList<IElement> getRowCells(IElement rowElement)
    {
        List<IElement> cellElements = new List<IElement>();
        foreach (IElement rowChildElement in rowElement.Children)
        {
            if (isElementNamed(rowChildElement, "td")
                || isElementNamed(rowChildElement, "th"))
            {
                cellElements.Add(rowChildElement);
            }
        }

        return new ReadOnlyCollection<IElement>(cellElements);
    }

    private static ReadRowsResult readOfferingRows(IReadOnlyList<IElement> tableRows)
    {
        List<HandongRawOfferingRow> offeringRows = new List<HandongRawOfferingRow>();
        HashSet<AcademicTerm> academicTermSet = new HashSet<AcademicTerm>();

        for (int tableRowIndex = FIRST_DATA_ROW_INDEX;
            tableRowIndex < tableRows.Count;
            ++tableRowIndex)
        {
            SourceRecordNumber sourceRecordNumber = new SourceRecordNumber(tableRowIndex + 1);
            IElement rowElement = tableRows[tableRowIndex];
            IReadOnlyList<IElement> cellElements = getRowCells(rowElement);
            if (cellElements.Count != HandongExportSchema.COLUMN_COUNT)
            {
                throw new HandongSourceFormatException(
                    "Source record " + sourceRecordNumber + " contains "
                    + cellElements.Count + " columns; exactly 16 are required.");
            }

            List<IReadOnlyList<string>> cellLinesByColumn =
                new List<IReadOnlyList<string>>(HandongExportSchema.COLUMN_COUNT);
            foreach (IElement cellElement in cellElements)
            {
                cellLinesByColumn.Add(HandongHtmlCellReader.ReadLines(cellElement));
            }

            HandongSourceLinkMetadata? sourceLinkMetadataOrNull =
                HandongSourceLinkMetadataReader.ReadMetadataOrNull(
                    rowElement,
                    sourceRecordNumber);
            if (sourceLinkMetadataOrNull != null)
            {
                academicTermSet.Add(sourceLinkMetadataOrNull.AcademicTerm);
            }

            offeringRows.Add(
                new HandongRawOfferingRow(
                    sourceRecordNumber,
                    cellLinesByColumn,
                    sourceLinkMetadataOrNull));
        }

        if (offeringRows.Count == 0)
        {
            throw new HandongSourceFormatException(
                "The Handong export catalog table does not contain offering rows.");
        }

        List<AcademicTerm> academicTerms = new List<AcademicTerm>(academicTermSet);
        academicTerms.Sort(compareAcademicTerms);
        return new ReadRowsResult(offeringRows, academicTerms);
    }

    private static int compareAcademicTerms(
        AcademicTerm leftAcademicTerm,
        AcademicTerm rightAcademicTerm)
    {
        int academicYearComparison = leftAcademicTerm.AcademicYear.Value.CompareTo(
            rightAcademicTerm.AcademicYear.Value);
        if (academicYearComparison != 0)
        {
            return academicYearComparison;
        }

        return leftAcademicTerm.Semester.Value.CompareTo(rightAcademicTerm.Semester.Value);
    }

    private sealed class ReadRowsResult
    {
        public IReadOnlyList<HandongRawOfferingRow> Rows { get; }
        public IReadOnlyList<AcademicTerm> AcademicTerms { get; }

        public ReadRowsResult(
            IReadOnlyList<HandongRawOfferingRow> rows,
            IReadOnlyList<AcademicTerm> academicTerms)
        {
            Rows = rows;
            AcademicTerms = academicTerms;
        }
    }
}
