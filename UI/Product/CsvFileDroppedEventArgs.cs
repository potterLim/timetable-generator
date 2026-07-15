using System;

namespace TimetableGenerator.UI.Product;

internal sealed class CsvFileDroppedEventArgs : EventArgs
{
    internal string FilePath { get; }

    internal CsvFileDroppedEventArgs(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            throw new ArgumentException("Dropped CSV file paths cannot be empty.", nameof(filePath));
        }

        FilePath = filePath;
    }
}
