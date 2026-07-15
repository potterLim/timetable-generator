using System;
using TimetableGenerator.Infrastructure.Csv;

namespace TimetableGenerator.UI.Product;

internal sealed class CsvFileDroppedEventArgs : EventArgs
{
    internal CsvInputFilePath SourceFilePath { get; }

    internal CsvFileDroppedEventArgs(CsvInputFilePath sourceFilePath)
    {
        if (sourceFilePath.IsValid == false)
        {
            throw new ArgumentException(
                "Dropped CSV file paths must be valid.",
                nameof(sourceFilePath));
        }

        SourceFilePath = sourceFilePath;
    }
}
