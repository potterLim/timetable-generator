using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace TimetableGenerator.Infrastructure.Exporting;

public sealed record ScheduleExportBaseName
{
    private const int MAXIMUM_STEM_LENGTH = 80;
    private const string DEFAULT_BASE_NAME = "시간표";

    private static readonly HashSet<string> RESERVED_WINDOWS_NAMES =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "CON",
            "PRN",
            "AUX",
            "NUL",
            "COM1",
            "COM2",
            "COM3",
            "COM4",
            "COM5",
            "COM6",
            "COM7",
            "COM8",
            "COM9",
            "LPT1",
            "LPT2",
            "LPT3",
            "LPT4",
            "LPT5",
            "LPT6",
            "LPT7",
            "LPT8",
            "LPT9",
        };

    public string Value { get; }

    public ScheduleExportBaseName(string value)
    {
        if (value == null)
        {
            throw new ArgumentNullException(nameof(value));
        }

        Value = normalizeValue(value);
    }

    public override string ToString()
    {
        return Value;
    }

    private static string normalizeValue(string value)
    {
        string trimmedValue = value.Trim().Normalize(NormalizationForm.FormC);
        StringBuilder safeValueBuilder = new StringBuilder(trimmedValue.Length);
        bool wasPreviousCharacterReplacement = false;

        foreach (char character in trimmedValue)
        {
            bool isInvalidCharacter = isInvalidFileNameCharacter(character);
            if (isInvalidCharacter)
            {
                if (wasPreviousCharacterReplacement == false)
                {
                    safeValueBuilder.Append('_');
                }

                wasPreviousCharacterReplacement = true;
                continue;
            }

            safeValueBuilder.Append(character);
            wasPreviousCharacterReplacement = false;
        }

        string safeValue = safeValueBuilder.ToString().Trim().TrimEnd('.', ' ');
        if (safeValue.Length == 0)
        {
            return DEFAULT_BASE_NAME;
        }

        if (safeValue.Length > MAXIMUM_STEM_LENGTH)
        {
            safeValue = safeValue.Substring(0, MAXIMUM_STEM_LENGTH).TrimEnd('.', ' ');
            if (safeValue.Length > 0 && char.IsHighSurrogate(safeValue[safeValue.Length - 1]))
            {
                safeValue = safeValue.Substring(0, safeValue.Length - 1);
            }
        }

        string reservedNameCandidate = Path.GetFileNameWithoutExtension(safeValue);
        if (RESERVED_WINDOWS_NAMES.Contains(reservedNameCandidate))
        {
            safeValue += "_";
        }

        return safeValue;
    }

    private static bool isInvalidFileNameCharacter(char character)
    {
        if (character < ' ')
        {
            return true;
        }

        switch (character)
        {
            case '<':
            case '>':
            case ':':
            case '"':
            case '/':
            case '\\':
            case '|':
            case '?':
            case '*':
                return true;
            default:
                return false;
        }
    }
}
