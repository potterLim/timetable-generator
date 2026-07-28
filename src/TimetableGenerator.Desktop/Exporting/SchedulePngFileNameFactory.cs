using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

using TimetableGenerator.Domain.Planning;

namespace TimetableGenerator.Desktop.Exporting;

internal static class SchedulePngFileNameFactory
{
    private const int MAXIMUM_UTF8_COMPONENT_BYTE_COUNT = 255;

    private const string FALLBACK_BASE_NAME = "시간표";

    private const string PNG_EXTENSION = ".png";

    private const char REPLACEMENT_CHARACTER = '-';

    private const string WINDOWS_INVALID_FILE_NAME_CHARACTERS = "<>:\"/\\|?*";

    private static readonly HashSet<string> WINDOWS_RESERVED_BASE_NAMES =
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
            "COM¹",
            "COM²",
            "COM³",
            "LPT1",
            "LPT2",
            "LPT3",
            "LPT4",
            "LPT5",
            "LPT6",
            "LPT7",
            "LPT8",
            "LPT9",
            "LPT¹",
            "LPT²",
            "LPT³",
        };

    public static string Create(PlanName? planNameOrNull)
    {
        return createFileSystemComponent(getBaseName(planNameOrNull), PNG_EXTENSION);
    }

    public static string CreateBatchFolderName(PlanName? planNameOrNull)
    {
        return CreateBatchFolderName(planNameOrNull, 1);
    }

    public static string CreateBatchFolderName(PlanName? planNameOrNull, int copyNumber)
    {
        if (copyNumber < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(copyNumber), copyNumber, "A PNG export folder copy number must be positive.");
        }

        string copySuffix = copyNumber == 1 ? string.Empty : " (" + copyNumber + ")";
        return createFileSystemComponent(getBaseName(planNameOrNull), copySuffix);
    }

    public static string CreateBatchCandidate(PlanName? planNameOrNull, SchedulePngCandidateNumber candidateNumber)
    {
        int digitCount = candidateNumber.Total.ToString(CultureInfo.InvariantCulture).Length;
        string sequenceText = candidateNumber.Value.ToString("D" + digitCount, CultureInfo.InvariantCulture);
        string suffix = " (" + sequenceText + ")" + PNG_EXTENSION;
        return createFileSystemComponent(getBaseName(planNameOrNull), suffix);
    }

    private static string getBaseName(PlanName? planNameOrNull)
    {
        if (planNameOrNull == null)
        {
            return FALLBACK_BASE_NAME;
        }

        string sanitizedBaseName = sanitizeBaseName(planNameOrNull.Value);
        return string.IsNullOrWhiteSpace(sanitizedBaseName) ? FALLBACK_BASE_NAME : sanitizedBaseName;
    }

    private static string sanitizeBaseName(string value)
    {
        char[] platformInvalidCharacters = Path.GetInvalidFileNameChars();
        StringBuilder sanitizedNameBuilder = new StringBuilder(value.Length);
        foreach (char character in value)
        {
            bool isPlatformInvalid = Array.IndexOf(platformInvalidCharacters, character) >= 0;
            bool isWindowsInvalid = character < ' ' || WINDOWS_INVALID_FILE_NAME_CHARACTERS.Contains(character);
            sanitizedNameBuilder.Append(isPlatformInvalid || isWindowsInvalid ? REPLACEMENT_CHARACTER : character);
        }

        string sanitizedBaseName = sanitizedNameBuilder.ToString().TrimEnd(' ', '.');
        string windowsDeviceBaseName = getWindowsDeviceBaseName(sanitizedBaseName);
        if (WINDOWS_RESERVED_BASE_NAMES.Contains(windowsDeviceBaseName))
        {
            sanitizedBaseName = escapeWindowsReservedBaseName(sanitizedBaseName);
        }

        return sanitizedBaseName;
    }

    private static string createFileSystemComponent(string baseName, string suffix)
    {
        int suffixByteCount = Encoding.UTF8.GetByteCount(suffix);
        int baseNameByteBudget = MAXIMUM_UTF8_COMPONENT_BYTE_COUNT - suffixByteCount;
        if (baseNameByteBudget <= 0)
        {
            throw new InvalidOperationException("The PNG export suffix exceeds the file-system name limit.");
        }

        string truncatedBaseName = truncateToUtf8ByteCount(baseName, baseNameByteBudget);
        if (string.IsNullOrWhiteSpace(truncatedBaseName))
        {
            truncatedBaseName = FALLBACK_BASE_NAME;
        }

        return truncatedBaseName + suffix;
    }

    private static string truncateToUtf8ByteCount(string value, int maximumByteCount)
    {
        StringBuilder builder = new StringBuilder(value.Length);
        TextElementEnumerator enumerator = StringInfo.GetTextElementEnumerator(value);
        int byteCount = 0;
        while (enumerator.MoveNext())
        {
            string textElement = enumerator.GetTextElement();
            int elementByteCount = Encoding.UTF8.GetByteCount(textElement);
            if (byteCount + elementByteCount > maximumByteCount)
            {
                break;
            }

            builder.Append(textElement);
            byteCount += elementByteCount;
        }

        return builder.ToString().TrimEnd(' ', '.');
    }

    private static string getWindowsDeviceBaseName(string baseName)
    {
        int extensionSeparatorIndex = baseName.IndexOf('.');
        if (extensionSeparatorIndex < 0)
        {
            return baseName;
        }

        return baseName.Substring(0, extensionSeparatorIndex);
    }

    private static string escapeWindowsReservedBaseName(string baseName)
    {
        int extensionSeparatorIndex = baseName.IndexOf('.');
        if (extensionSeparatorIndex < 0)
        {
            return baseName + REPLACEMENT_CHARACTER;
        }

        return baseName.Insert(extensionSeparatorIndex, REPLACEMENT_CHARACTER.ToString());
    }
}
