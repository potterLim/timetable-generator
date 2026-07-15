using System;
using System.Collections.Generic;

namespace TimetableGenerator.Infrastructure.Exporting;

public sealed class SchedulePngExportResult
{
    private readonly IReadOnlyList<ExportedSchedulePng> mExportedFiles;

    public IReadOnlyList<ExportedSchedulePng> ExportedFiles
    {
        get
        {
            return mExportedFiles;
        }
    }

    private readonly IReadOnlyList<SchedulePngExportFailure> mFailures;

    public IReadOnlyList<SchedulePngExportFailure> Failures
    {
        get
        {
            return mFailures;
        }
    }

    private readonly IReadOnlyList<SchedulePngExportArtifact> mRetainedArtifacts;

    public IReadOnlyList<SchedulePngExportArtifact> RetainedArtifacts
    {
        get
        {
            return mRetainedArtifacts;
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

    private readonly ESchedulePngExportCompletion mCompletion;

    public ESchedulePngExportCompletion Completion
    {
        get
        {
            return mCompletion;
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

        validateResultHasItems(copiedExportedFiles, copiedFailures);
        validateUniqueScheduleNumbers(copiedExportedFiles, copiedFailures);

        mExportedFiles = copiedExportedFiles.AsReadOnly();
        mFailures = copiedFailures.AsReadOnly();
        mRetainedArtifacts = Array.Empty<SchedulePngExportArtifact>();
        mCompletion = findCompletion(copiedExportedFiles, copiedFailures);
    }

    private SchedulePngExportResult(
        IEnumerable<SchedulePngExportArtifact> retainedArtifacts)
    {
        if (retainedArtifacts == null)
        {
            throw new ArgumentNullException(nameof(retainedArtifacts));
        }

        List<SchedulePngExportArtifact> copiedArtifacts = copyArtifacts(
            retainedArtifacts);
        mExportedFiles = Array.Empty<ExportedSchedulePng>();
        mFailures = Array.Empty<SchedulePngExportFailure>();
        mRetainedArtifacts = copiedArtifacts.AsReadOnly();
        mCompletion = ESchedulePngExportCompletion.Canceled;
    }

    internal static SchedulePngExportResult createCanceled(
        IEnumerable<SchedulePngExportArtifact> retainedArtifacts)
    {
        return new SchedulePngExportResult(retainedArtifacts);
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

    private static List<SchedulePngExportArtifact> copyArtifacts(
        IEnumerable<SchedulePngExportArtifact> retainedArtifacts)
    {
        List<SchedulePngExportArtifact> copiedArtifacts =
            new List<SchedulePngExportArtifact>();
        foreach (SchedulePngExportArtifact retainedArtifact in retainedArtifacts)
        {
            if (retainedArtifact == null)
            {
                throw new ArgumentException(
                    "Retained artifacts cannot contain null values.",
                    nameof(retainedArtifacts));
            }

            copiedArtifacts.Add(retainedArtifact);
        }

        return copiedArtifacts;
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

    private static void validateResultHasItems(
        IReadOnlyCollection<ExportedSchedulePng> exportedFiles,
        IReadOnlyCollection<SchedulePngExportFailure> failures)
    {
        if (exportedFiles.Count + failures.Count == 0)
        {
            throw new ArgumentException("Export results require at least one item.");
        }
    }

    private static ESchedulePngExportCompletion findCompletion(
        IReadOnlyCollection<ExportedSchedulePng> exportedFiles,
        IReadOnlyCollection<SchedulePngExportFailure> failures)
    {
        if (failures.Count == 0)
        {
            return ESchedulePngExportCompletion.Succeeded;
        }

        if (exportedFiles.Count == 0)
        {
            return ESchedulePngExportCompletion.Failed;
        }

        return ESchedulePngExportCompletion.PartiallySucceeded;
    }
}
