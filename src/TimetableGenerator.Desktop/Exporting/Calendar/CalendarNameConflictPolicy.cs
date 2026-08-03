using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

using TimetableGenerator.Domain.Planning;

namespace TimetableGenerator.Desktop.Exporting.Calendar;

internal static class CalendarNameConflictPolicy
{
    private const int FIRST_COPY_NUMBER = 2;

    public static bool IsSameName(PlanName firstName, PlanName secondName)
    {
        if (firstName == null)
        {
            throw new ArgumentNullException(nameof(firstName));
        }

        if (secondName == null)
        {
            throw new ArgumentNullException(nameof(secondName));
        }

        string canonicalFirstName = createCanonicalName(firstName);
        string canonicalSecondName = createCanonicalName(secondName);
        return StringComparer.Ordinal.Equals(canonicalFirstName, canonicalSecondName);
    }

    public static bool IsNameInUse(PlanName calendarName, IEnumerable<PlanName> existingNames)
    {
        if (calendarName == null)
        {
            throw new ArgumentNullException(nameof(calendarName));
        }

        HashSet<string> canonicalExistingNames = createCanonicalExistingNames(existingNames);
        string canonicalCalendarName = createCanonicalName(calendarName);
        return canonicalExistingNames.Contains(canonicalCalendarName);
    }

    public static PlanName FindNextAvailableName(PlanName requestedName, IEnumerable<PlanName> existingNames)
    {
        if (requestedName == null)
        {
            throw new ArgumentNullException(nameof(requestedName));
        }

        HashSet<string> canonicalExistingNames = createCanonicalExistingNames(existingNames);
        string normalizedRequestedName = requestedName.Value.Trim().Normalize(NormalizationForm.FormC);

        int copyNumber = FIRST_COPY_NUMBER;
        while (true)
        {
            string copySuffix = createCopySuffix(copyNumber);
            string candidateBaseName = truncateWithoutSplittingTextElement(normalizedRequestedName, PlanName.MAXIMUM_LENGTH - copySuffix.Length);
            PlanName candidateName = new PlanName(candidateBaseName + copySuffix);
            string canonicalCandidateName = createCanonicalName(candidateName);
            if (canonicalExistingNames.Contains(canonicalCandidateName) == false)
            {
                return candidateName;
            }

            if (copyNumber == int.MaxValue)
            {
                throw new InvalidOperationException("No available numbered calendar name could be allocated.");
            }

            ++copyNumber;
        }
    }

    public static void EnsureResolutionIsSupported(CalendarNameConflict conflict, ECalendarNameConflictResolution resolution)
    {
        if (conflict == null)
        {
            throw new ArgumentNullException(nameof(conflict));
        }

        switch (resolution)
        {
            case ECalendarNameConflictResolution.ReplaceExisting:
                if (conflict.CanReplace == false)
                {
                    throw new InvalidOperationException("The existing calendar cannot be replaced safely.");
                }

                return;
            case ECalendarNameConflictResolution.CreateWithAvailableName:
            case ECalendarNameConflictResolution.Cancel:
                return;
            case ECalendarNameConflictResolution.None:
            default:
                throw new ArgumentOutOfRangeException(nameof(resolution), resolution, "A supported calendar name conflict resolution is required.");
        }
    }

    private static HashSet<string> createCanonicalExistingNames(IEnumerable<PlanName> existingNames)
    {
        if (existingNames == null)
        {
            throw new ArgumentNullException(nameof(existingNames));
        }

        HashSet<string> canonicalExistingNames = new HashSet<string>(StringComparer.Ordinal);
        foreach (PlanName existingName in existingNames)
        {
            if (existingName == null)
            {
                throw new ArgumentException("Existing calendar names cannot contain null values.", nameof(existingNames));
            }

            canonicalExistingNames.Add(createCanonicalName(existingName));
        }

        return canonicalExistingNames;
    }

    private static string createCanonicalName(PlanName calendarName)
    {
        return normalizeName(calendarName.Value);
    }

    internal static string normalizeName(string calendarName)
    {
        if (calendarName == null)
        {
            throw new ArgumentNullException(nameof(calendarName));
        }

        string normalizedName = calendarName.Trim().Normalize(NormalizationForm.FormC);
        StringBuilder canonicalName = new StringBuilder(normalizedName.Length);
        foreach (char character in normalizedName)
        {
            char canonicalCharacter = character;
            if (character >= 'a' && character <= 'z')
            {
                canonicalCharacter = (char)(character - ('a' - 'A'));
            }

            canonicalName.Append(canonicalCharacter);
        }
        return canonicalName.ToString();
    }

    private static string createCopySuffix(int copyNumber)
    {
        string copyNumberText = copyNumber.ToString(CultureInfo.InvariantCulture);
        return " (" + copyNumberText + ")";
    }

    private static string truncateWithoutSplittingTextElement(string value, int maximumLength)
    {
        if (value.Length <= maximumLength)
        {
            return value;
        }

        TextElementEnumerator textElements = StringInfo.GetTextElementEnumerator(value);
        int truncatedLength = 0;
        while (textElements.MoveNext())
        {
            string textElement = textElements.GetTextElement();
            int candidateLength = truncatedLength + textElement.Length;
            if (candidateLength > maximumLength)
            {
                break;
            }

            truncatedLength = candidateLength;
        }

        return value.Substring(0, truncatedLength);
    }
}
