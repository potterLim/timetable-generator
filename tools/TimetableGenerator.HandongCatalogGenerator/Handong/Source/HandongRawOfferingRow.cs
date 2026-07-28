using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using TimetableGenerator.HandongCatalogGenerator.Domain;

namespace TimetableGenerator.HandongCatalogGenerator.Handong.Source;

internal sealed class HandongRawOfferingRow
{
    private readonly IReadOnlyList<IReadOnlyList<string>> mCellLinesByColumn;

    public SourceRecordNumber SourceRecordNumber { get; }
    public HandongSourceLinkMetadata? SourceLinkMetadataOrNull { get; }

    public HandongRawOfferingRow(SourceRecordNumber sourceRecordNumber, IReadOnlyList<IReadOnlyList<string>> cellLinesByColumn, HandongSourceLinkMetadata? sourceLinkMetadataOrNull)
    {
        ArgumentNullException.ThrowIfNull(cellLinesByColumn);

        if (cellLinesByColumn.Count != HandongExportSchema.COLUMN_COUNT)
        {
            throw new ArgumentException("A Handong offering row must contain exactly 16 columns.", nameof(cellLinesByColumn));
        }

        SourceRecordNumber = sourceRecordNumber;
        SourceLinkMetadataOrNull = sourceLinkMetadataOrNull;
        mCellLinesByColumn = copyCellLinesByColumn(cellLinesByColumn);
    }

    public IReadOnlyList<string> GetCellLines(EHandongColumn column)
    {
        int columnIndex = HandongExportSchema.GetColumnIndex(column);
        return mCellLinesByColumn[columnIndex];
    }

    private static IReadOnlyList<IReadOnlyList<string>> copyCellLinesByColumn(IReadOnlyList<IReadOnlyList<string>> cellLinesByColumn)
    {
        List<IReadOnlyList<string>> copiedCellLinesByColumn = new List<IReadOnlyList<string>>(cellLinesByColumn.Count);

        foreach (IReadOnlyList<string> cellLines in cellLinesByColumn)
        {
            List<string> copiedCellLines = new List<string>(cellLines.Count);
            foreach (string cellLine in cellLines)
            {
                copiedCellLines.Add(cellLine);
            }

            copiedCellLinesByColumn.Add(new ReadOnlyCollection<string>(copiedCellLines));
        }

        return new ReadOnlyCollection<IReadOnlyList<string>>(copiedCellLinesByColumn);
    }
}
