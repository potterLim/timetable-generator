using System;
using TimetableGenerator.Application.Documents;
using TimetableGenerator.Infrastructure.Csv;
using TimetableGenerator.Infrastructure.Exporting;

namespace TimetableGenerator.UI.Product;

internal sealed class ScheduleExportDialogContext
{
    internal ScheduleIndex SelectedScheduleIndex { get; }

    internal ScheduleNumber SelectedScheduleNumber { get; }

    internal int TotalScheduleCount { get; }

    internal CsvInputFileName SourceFileName { get; }

    internal ScheduleExportDirectoryPath InitialDirectory { get; }

    internal EScheduleExportScope InitialScope { get; }

    internal ScheduleExportDialogContext(
        ScheduleDocument document,
        ScheduleIndex selectedScheduleIndex,
        ScheduleExportDirectoryPath initialDirectory,
        EScheduleExportScope initialScope)
    {
        if (document == null)
        {
            throw new ArgumentNullException(nameof(document));
        }

        if (selectedScheduleIndex.Value >= document.ScheduleCount)
        {
            throw new ArgumentOutOfRangeException(nameof(selectedScheduleIndex));
        }

        if (initialDirectory.IsValid == false)
        {
            throw new ArgumentException("A valid initial export directory is required.", nameof(initialDirectory));
        }

        if (Enum.IsDefined(initialScope) == false)
        {
            throw new ArgumentOutOfRangeException(nameof(initialScope));
        }

        SelectedScheduleIndex = selectedScheduleIndex;
        SelectedScheduleNumber = ScheduleNumber.FromIndex(selectedScheduleIndex);
        TotalScheduleCount = document.ScheduleCount;
        SourceFileName = document.SourceFilePath.FileName;
        InitialDirectory = initialDirectory;
        InitialScope = initialScope;
    }
}
