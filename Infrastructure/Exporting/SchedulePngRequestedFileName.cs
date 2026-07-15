using System;
using System.IO;

namespace TimetableGenerator.Infrastructure.Exporting;

public sealed record SchedulePngRequestedFileName
{
    private const string PNG_FILE_EXTENSION = ".png";

    public string Value { get; }

    public string FileStem
    {
        get
        {
            return Path.GetFileNameWithoutExtension(Value);
        }
    }

    internal SchedulePngRequestedFileName(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException(
                "Requested PNG file names cannot be empty.",
                nameof(value));
        }

        if (string.Equals(Path.GetFileName(value), value, StringComparison.Ordinal) == false)
        {
            throw new ArgumentException(
                "Requested PNG file names cannot contain a directory path.",
                nameof(value));
        }

        if (value.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
        {
            throw new ArgumentException(
                "Requested PNG file names cannot contain invalid file name characters.",
                nameof(value));
        }

        if (string.Equals(
            Path.GetExtension(value),
            PNG_FILE_EXTENSION,
            StringComparison.OrdinalIgnoreCase) == false)
        {
            throw new ArgumentException(
                "Requested PNG file names must use the .png extension.",
                nameof(value));
        }

        if (Path.GetFileNameWithoutExtension(value).Length == 0)
        {
            throw new ArgumentException(
                "Requested PNG file names require a file stem.",
                nameof(value));
        }

        Value = value;
    }

    public override string ToString()
    {
        return Value;
    }
}
