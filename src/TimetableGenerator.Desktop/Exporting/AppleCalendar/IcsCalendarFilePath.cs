using System;
using System.IO;

namespace TimetableGenerator.Desktop.Exporting.AppleCalendar;

internal sealed class IcsCalendarFilePath
{
    private const string FILE_EXTENSION = ".ics";

    public string Value { get; }

    public IcsCalendarFilePath(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);

        if (Path.IsPathFullyQualified(value) == false)
        {
            throw new ArgumentException(
                "The iCalendar file path must be fully qualified.",
                nameof(value));
        }

        if (string.Equals(
                Path.GetExtension(value),
                FILE_EXTENSION,
                StringComparison.OrdinalIgnoreCase) == false)
        {
            throw new ArgumentException(
                "The calendar import file must use the .ics extension.",
                nameof(value));
        }

        Value = value;
    }
}
