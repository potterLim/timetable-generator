using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using TimetableGenerator.HandongCatalogGenerator.Domain;

namespace TimetableGenerator.HandongCatalogGenerator.Handong.Source;

internal sealed class HandongExportDocument
{
    public string SourceSha256Hex { get; }
    public long SizeBytes { get; }
    public string DeclaredCharset { get; }
    public IReadOnlyList<AcademicTerm> AcademicTerms { get; }
    public IReadOnlyList<HandongRawOfferingRow> Rows { get; }

    public HandongExportDocument(
        string sourceSha256Hex,
        long sizeBytes,
        string declaredCharset,
        IReadOnlyList<AcademicTerm> academicTerms,
        IReadOnlyList<HandongRawOfferingRow> rows)
    {
        if (string.IsNullOrWhiteSpace(sourceSha256Hex))
        {
            throw new ArgumentException("The source SHA-256 cannot be empty.", nameof(sourceSha256Hex));
        }

        if (sizeBytes < 0L)
        {
            throw new ArgumentOutOfRangeException(nameof(sizeBytes), sizeBytes, "The source size cannot be negative.");
        }

        if (string.IsNullOrWhiteSpace(declaredCharset))
        {
            throw new ArgumentException("The declared charset cannot be empty.", nameof(declaredCharset));
        }

        ArgumentNullException.ThrowIfNull(academicTerms);
        ArgumentNullException.ThrowIfNull(rows);

        SourceSha256Hex = sourceSha256Hex;
        SizeBytes = sizeBytes;
        DeclaredCharset = declaredCharset;
        AcademicTerms = copyAcademicTerms(academicTerms);
        Rows = copyRows(rows);
    }

    private static IReadOnlyList<AcademicTerm> copyAcademicTerms(
        IReadOnlyList<AcademicTerm> academicTerms)
    {
        List<AcademicTerm> copiedAcademicTerms = new List<AcademicTerm>(academicTerms.Count);
        foreach (AcademicTerm academicTerm in academicTerms)
        {
            copiedAcademicTerms.Add(academicTerm);
        }

        return new ReadOnlyCollection<AcademicTerm>(copiedAcademicTerms);
    }

    private static IReadOnlyList<HandongRawOfferingRow> copyRows(
        IReadOnlyList<HandongRawOfferingRow> rows)
    {
        List<HandongRawOfferingRow> copiedRows = new List<HandongRawOfferingRow>(rows.Count);
        foreach (HandongRawOfferingRow row in rows)
        {
            copiedRows.Add(row);
        }

        return new ReadOnlyCollection<HandongRawOfferingRow>(copiedRows);
    }
}
