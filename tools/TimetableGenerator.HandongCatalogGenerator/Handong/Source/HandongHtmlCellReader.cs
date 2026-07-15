using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text;
using AngleSharp.Dom;

namespace TimetableGenerator.HandongCatalogGenerator.Handong.Source;

internal static class HandongHtmlCellReader
{
    private const string HTML_LINE_BREAK_ELEMENT_NAME = "br";

    public static IReadOnlyList<string> ReadLines(IElement cellElement)
    {
        ArgumentNullException.ThrowIfNull(cellElement);

        List<StringBuilder> lineTextBuilders = new List<StringBuilder>();
        lineTextBuilders.Add(new StringBuilder());
        appendNodeTextRecursive(cellElement, lineTextBuilders);

        List<string> normalizedLines = new List<string>(lineTextBuilders.Count);
        foreach (StringBuilder lineTextBuilder in lineTextBuilders)
        {
            normalizedLines.Add(normalizeLineText(lineTextBuilder.ToString()));
        }

        return copyMeaningfulLineRange(normalizedLines);
    }

    private static void appendNodeTextRecursive(
        INode node,
        List<StringBuilder> lineTextBuilders)
    {
        if (node is IElement element
            && string.Equals(
                element.LocalName,
                HTML_LINE_BREAK_ELEMENT_NAME,
                StringComparison.OrdinalIgnoreCase))
        {
            lineTextBuilders.Add(new StringBuilder());
            return;
        }

        if (node is IText textNode)
        {
            StringBuilder currentLineTextBuilder = lineTextBuilders[lineTextBuilders.Count - 1];
            currentLineTextBuilder.Append(textNode.Data);
            return;
        }

        foreach (INode childNode in node.ChildNodes)
        {
            appendNodeTextRecursive(childNode, lineTextBuilders);
        }
    }

    private static string normalizeLineText(string lineText)
    {
        StringBuilder normalizedLineTextBuilder = new StringBuilder(lineText.Length);
        bool hasPendingSeparator = false;

        foreach (char character in lineText)
        {
            if (char.IsWhiteSpace(character) || character == '\u00A0')
            {
                hasPendingSeparator = normalizedLineTextBuilder.Length > 0;
                continue;
            }

            if (hasPendingSeparator)
            {
                normalizedLineTextBuilder.Append(' ');
                hasPendingSeparator = false;
            }

            normalizedLineTextBuilder.Append(character);
        }

        return normalizedLineTextBuilder.ToString();
    }

    private static IReadOnlyList<string> copyMeaningfulLineRange(
        IReadOnlyList<string> normalizedLines)
    {
        int firstMeaningfulLineIndex = findFirstMeaningfulLineIndex(normalizedLines);
        if (firstMeaningfulLineIndex < 0)
        {
            return Array.Empty<string>();
        }

        int lastMeaningfulLineIndex = findLastMeaningfulLineIndex(normalizedLines);
        int meaningfulLineCount = lastMeaningfulLineIndex - firstMeaningfulLineIndex + 1;
        List<string> meaningfulLines = new List<string>(meaningfulLineCount);

        for (int lineIndex = firstMeaningfulLineIndex;
            lineIndex <= lastMeaningfulLineIndex;
            ++lineIndex)
        {
            meaningfulLines.Add(normalizedLines[lineIndex]);
        }

        return new ReadOnlyCollection<string>(meaningfulLines);
    }

    private static int findFirstMeaningfulLineIndex(IReadOnlyList<string> normalizedLines)
    {
        for (int lineIndex = 0; lineIndex < normalizedLines.Count; ++lineIndex)
        {
            if (normalizedLines[lineIndex].Length > 0)
            {
                return lineIndex;
            }
        }

        return -1;
    }

    private static int findLastMeaningfulLineIndex(IReadOnlyList<string> normalizedLines)
    {
        for (int lineIndex = normalizedLines.Count - 1; lineIndex >= 0; --lineIndex)
        {
            if (normalizedLines[lineIndex].Length > 0)
            {
                return lineIndex;
            }
        }

        return -1;
    }
}
