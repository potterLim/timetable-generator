using System;
using System.Text;

namespace TimetableGenerator.Desktop.Presentation.Models;

internal sealed class CourseSearchQuery
{
    private readonly string mNormalizedText;

    public bool IsEmpty
    {
        get
        {
            return mNormalizedText.Length == 0;
        }
    }

    private CourseSearchQuery(string normalizedText)
    {
        mNormalizedText = normalizedText;
    }

    public static CourseSearchQuery Create(string sourceText)
    {
        if (sourceText == null)
        {
            throw new ArgumentNullException(nameof(sourceText));
        }

        return new CourseSearchQuery(normalizeSearchText(sourceText));
    }

    public bool IsExactMatch(string candidateText)
    {
        string normalizedCandidateText = normalizeCandidateText(candidateText);
        return normalizedCandidateText.Equals(mNormalizedText, StringComparison.OrdinalIgnoreCase);
    }

    public bool IsPrefixMatch(string candidateText)
    {
        string normalizedCandidateText = normalizeCandidateText(candidateText);
        return normalizedCandidateText.StartsWith(mNormalizedText, StringComparison.OrdinalIgnoreCase);
    }

    public bool IsContainedIn(string candidateText)
    {
        string normalizedCandidateText = normalizeCandidateText(candidateText);
        return normalizedCandidateText.Contains(mNormalizedText, StringComparison.OrdinalIgnoreCase);
    }

    private static string normalizeCandidateText(string candidateText)
    {
        if (candidateText == null)
        {
            throw new ArgumentNullException(nameof(candidateText));
        }

        return normalizeSearchText(candidateText);
    }

    private static string normalizeSearchText(string sourceText)
    {
        StringBuilder normalizedText = new StringBuilder(sourceText.Length);
        foreach (char character in sourceText)
        {
            if (char.IsWhiteSpace(character) == false)
            {
                normalizedText.Append(character);
            }
        }

        return normalizedText.ToString();
    }
}
