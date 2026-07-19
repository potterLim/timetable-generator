using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

using TimetableGenerator.Domain.Planning;

namespace TimetableGenerator.Desktop.Exporting;

internal static class SchedulePngFileNameFactory
{
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
        if (planNameOrNull == null)
        {
            return FALLBACK_BASE_NAME + PNG_EXTENSION;
        }

        string sanitizedBaseName = sanitizeBaseName(planNameOrNull.Value);
        if (string.IsNullOrWhiteSpace(sanitizedBaseName))
        {
            return FALLBACK_BASE_NAME + PNG_EXTENSION;
        }

        return sanitizedBaseName + PNG_EXTENSION;
    }

    private static string sanitizeBaseName(string value)
    {
        char[] platformInvalidCharacters = Path.GetInvalidFileNameChars();
        StringBuilder sanitizedNameBuilder = new StringBuilder(value.Length);
        foreach (char character in value)
        {
            bool isPlatformInvalid = Array.IndexOf(
                platformInvalidCharacters,
                character) >= 0;
            bool isWindowsInvalid = character < ' '
                || WINDOWS_INVALID_FILE_NAME_CHARACTERS.Contains(character);
            sanitizedNameBuilder.Append(
                isPlatformInvalid || isWindowsInvalid
                    ? REPLACEMENT_CHARACTER
                    : character);
        }

        string sanitizedBaseName = sanitizedNameBuilder
            .ToString()
            .TrimEnd(' ', '.');
        string windowsDeviceBaseName = getWindowsDeviceBaseName(
            sanitizedBaseName);
        if (WINDOWS_RESERVED_BASE_NAMES.Contains(windowsDeviceBaseName))
        {
            sanitizedBaseName = escapeWindowsReservedBaseName(
                sanitizedBaseName);
        }

        return sanitizedBaseName;
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

        return baseName.Insert(
            extensionSeparatorIndex,
            REPLACEMENT_CHARACTER.ToString());
    }
}
