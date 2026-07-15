using System;
using System.Collections.Generic;

namespace TimetableGenerator.Infrastructure.Exporting;

public sealed class SchedulePngExportResult
{
    private readonly IReadOnlyList<ExportedSchedulePng> mExportedFiles;
    private readonly IReadOnlyList<SchedulePngExportFailure> mFailures;

    public IReadOnlyList<ExportedSchedulePng> ExportedFiles
    {
        get
        {
            return mExportedFiles;
        }
    }

    public IReadOnlyList<SchedulePngExportFailure> Failures
    {
        get
        {
            return mFailures;
        }
    }

    public int TotalScheduleCount
    {
        get
        {
            return mExportedFiles.Count + mFailures.Count;
        }
    }

    public bool HasExportedFiles
    {
        get
        {
            return mExportedFiles.Count > 0;
        }
    }

    public bool HasFailures
    {
        get
        {
            return mFailures.Count > 0;
        }
    }

    public ESchedulePngExportCompletion Completion
    {
        get
        {
            if (mFailures.Count == 0)
            {
                return ESchedulePngExportCompletion.Succeeded;
            }

            if (mExportedFiles.Count == 0)
            {
                return ESchedulePngExportCompletion.Failed;
            }

            return ESchedulePngExportCompletion.PartiallySucceeded;
        }
    }

    internal SchedulePngExportResult(
        IEnumerable<ExportedSchedulePng> exportedFiles,
        IEnumerable<SchedulePngExportFailure> failures)
    {
        if (exportedFiles == null)
        {
            throw new ArgumentNullException(nameof(exportedFiles));
        }

        if (failures == null)
        {
            throw new ArgumentNullException(nameof(failures));
        }

        List<ExportedSchedulePng> copiedExportedFiles = copyExportedFiles(exportedFiles);
        List<SchedulePngExportFailure> copiedFailures = copyFailures(failures);

        if (copiedExportedFiles.Count + copiedFailures.Count == 0)
        {
            throw new ArgumentException("Export results require at least one item.");
        }

        validateUniqueScheduleNumbers(copiedExportedFiles, copiedFailures);

        mExportedFiles = copiedExportedFiles.AsReadOnly();
        mFailures = copiedFailures.AsReadOnly();
    }

    private static List<ExportedSchedulePng> copyExportedFiles(
        IEnumerable<ExportedSchedulePng> exportedFiles)
    {
        List<ExportedSchedulePng> copiedExportedFiles = new List<ExportedSchedulePng>();
        foreach (ExportedSchedulePng exportedFile in exportedFiles)
        {
            if (exportedFile == null)
            {
                throw new ArgumentException(
                    "Export results cannot contain null exported files.",
                    nameof(exportedFiles));
            }

            copiedExportedFiles.Add(exportedFile);
        }

        return copiedExportedFiles;
    }

    private static List<SchedulePngExportFailure> copyFailures(
        IEnumerable<SchedulePngExportFailure> failures)
    {
        List<SchedulePngExportFailure> copiedFailures = new List<SchedulePngExportFailure>();
        foreach (SchedulePngExportFailure failure in failures)
        {
            if (failure == null)
            {
                throw new ArgumentException(
                    "Export results cannot contain null failures.",
                    nameof(failures));
            }

            copiedFailures.Add(failure);
        }

        return copiedFailures;
    }

    private static void validateUniqueScheduleNumbers(
        IEnumerable<ExportedSchedulePng> exportedFiles,
        IEnumerable<SchedulePngExportFailure> failures)
    {
        HashSet<ScheduleExportNumber> scheduleNumbers = new HashSet<ScheduleExportNumber>();
        foreach (ExportedSchedulePng exportedFile in exportedFiles)
        {
            if (scheduleNumbers.Add(exportedFile.ScheduleNumber) == false)
            {
                throw new ArgumentException("Export results cannot repeat schedule numbers.");
            }
        }

        foreach (SchedulePngExportFailure failure in failures)
        {
            if (scheduleNumbers.Add(failure.ScheduleNumber) == false)
            {
                throw new ArgumentException("Export results cannot repeat schedule numbers.");
            }
        }
    }
}
