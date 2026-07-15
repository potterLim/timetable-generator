using System;
using System.IO;

namespace TimetableGenerator.Infrastructure.Csv;

public readonly record struct CsvInputFilePath
{
    private const string CSV_FILE_EXTENSION = ".csv";

    public string Value { get; }

    public bool IsValid
    {
        get
        {
            if (string.IsNullOrWhiteSpace(Value))
            {
                return false;
            }

            string fileExtension = Path.GetExtension(Value);
            return string.Equals(fileExtension, CSV_FILE_EXTENSION, StringComparison.OrdinalIgnoreCase);
        }
    }

    public CsvInputFilePath(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("CSV input file paths cannot be empty.", nameof(value));
        }

        string normalizedPath = Path.GetFullPath(value.Trim());
        string fileExtension = Path.GetExtension(normalizedPath);
        if (string.Equals(fileExtension, CSV_FILE_EXTENSION, StringComparison.OrdinalIgnoreCase) == false)
        {
            throw new ArgumentException("CSV input files must use the .csv extension.", nameof(value));
        }

        Value = normalizedPath;
    }

    public override string ToString()
    {
        return Value;
    }
}
