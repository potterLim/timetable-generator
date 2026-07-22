using System;
using System.Collections.Generic;
using System.Text;
using TimetableGenerator.HandongCatalogGenerator.Handong.Source;

namespace TimetableGenerator.HandongCatalogGenerator.Handong.Normalization;

internal static class HandongCellValueReader
{
    internal static IReadOnlyList<string> getNonEmptyLines(
        HandongRawOfferingRow row,
        EHandongColumn column)
    {
        if (row == null)
        {
            throw new ArgumentNullException(nameof(row));
        }

        IReadOnlyList<string> sourceLines = row.GetCellLines(column);
        List<string> normalizedLines = new List<string>();
        foreach (string sourceLine in sourceLines)
        {
            if (sourceLine == null)
            {
                throw new InvalidHandongSourceRecordException(
                    row.SourceRecordNumber,
                    column,
                    "The source parser returned a null cell line.");
            }

            string normalizedLine = sourceLine.Trim();
            if (normalizedLine.Length > 0)
            {
                normalizedLines.Add(normalizedLine);
            }
        }

        return normalizedLines.AsReadOnly();
    }

    internal static string getRequiredSingleLine(HandongRawOfferingRow row, EHandongColumn column)
    {
        IReadOnlyList<string> lines = getNonEmptyLines(row, column);
        if (lines.Count != 1)
        {
            throw new InvalidHandongSourceRecordException(
                row.SourceRecordNumber,
                column,
                "Expected exactly one non-empty line but found " + lines.Count + ".");
        }

        return lines[0];
    }

    internal static string getCombinedText(IReadOnlyList<string> lines, int firstLineIndex)
    {
        if (lines == null)
        {
            throw new ArgumentNullException(nameof(lines));
        }

        if (firstLineIndex < 0 || firstLineIndex >= lines.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(firstLineIndex));
        }

        StringBuilder combinedText = new StringBuilder();
        for (int lineIndex = firstLineIndex; lineIndex < lines.Count; ++lineIndex)
        {
            if (combinedText.Length > 0)
            {
                combinedText.Append(' ');
            }

            combinedText.Append(lines[lineIndex]);
        }

        return combinedText.ToString();
    }
}
