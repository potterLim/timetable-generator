using System;
using System.Collections.Generic;
using TimetableGenerator.Infrastructure.Csv;

namespace TimetableGenerator.Application.Documents;

public sealed class ScheduleDocumentLoadFailure
{
    private readonly IReadOnlyList<CourseImportDiagnostic> mImportDiagnostics;

    public EScheduleDocumentLoadStatus Status { get; }

    public IReadOnlyList<CourseImportDiagnostic> ImportDiagnostics
    {
        get
        {
            return mImportDiagnostics;
        }
    }

    public EDiagnosticCollectionCompletion ImportDiagnosticCollectionCompletion { get; }

    public bool HasImportDiagnostics
    {
        get
        {
            return mImportDiagnostics.Count > 0;
        }
    }

    public bool HasReachedImportDiagnosticLimit
    {
        get
        {
            return ImportDiagnosticCollectionCompletion ==
                EDiagnosticCollectionCompletion.MaximumCountReached;
        }
    }

    private ScheduleDocumentLoadFailure(
        EScheduleDocumentLoadStatus status,
        IEnumerable<CourseImportDiagnostic> importDiagnostics,
        EDiagnosticCollectionCompletion importDiagnosticCollectionCompletion)
    {
        if (isFailureStatus(status) == false)
        {
            throw new ArgumentOutOfRangeException(nameof(status));
        }

        if (importDiagnostics == null)
        {
            throw new ArgumentNullException(nameof(importDiagnostics));
        }

        if (Enum.IsDefined(
            typeof(EDiagnosticCollectionCompletion),
            importDiagnosticCollectionCompletion) == false)
        {
            throw new ArgumentOutOfRangeException(
                nameof(importDiagnosticCollectionCompletion));
        }

        List<CourseImportDiagnostic> copiedImportDiagnostics =
            new List<CourseImportDiagnostic>();
        foreach (CourseImportDiagnostic importDiagnostic in importDiagnostics)
        {
            if (importDiagnostic == null)
            {
                throw new ArgumentException(
                    "Document load failures cannot contain null import diagnostics.",
                    nameof(importDiagnostics));
            }

            copiedImportDiagnostics.Add(importDiagnostic);
        }

        bool isImportFailure = status == EScheduleDocumentLoadStatus.ImportFailed;
        if (isImportFailure != (copiedImportDiagnostics.Count > 0))
        {
            throw new ArgumentException(
                "Only import failures can contain import diagnostics.",
                nameof(importDiagnostics));
        }

        if (isImportFailure == false &&
            importDiagnosticCollectionCompletion != EDiagnosticCollectionCompletion.Completed)
        {
            throw new ArgumentException(
                "Diagnostic collection completion applies only to import failures.",
                nameof(importDiagnosticCollectionCompletion));
        }

        Status = status;
        mImportDiagnostics = copiedImportDiagnostics.AsReadOnly();
        ImportDiagnosticCollectionCompletion = importDiagnosticCollectionCompletion;
    }

    internal static ScheduleDocumentLoadFailure createFromImportResult(
        CourseImportResult courseImportResult)
    {
        if (courseImportResult == null)
        {
            throw new ArgumentNullException(nameof(courseImportResult));
        }

        if (courseImportResult.IsSuccessful)
        {
            throw new ArgumentException(
                "Successful course imports cannot create document load failures.",
                nameof(courseImportResult));
        }

        return new ScheduleDocumentLoadFailure(
            EScheduleDocumentLoadStatus.ImportFailed,
            courseImportResult.Diagnostics,
            courseImportResult.DiagnosticCollectionCompletion);
    }

    internal static ScheduleDocumentLoadFailure createWithoutImportDiagnostics(
        EScheduleDocumentLoadStatus status)
    {
        if (status == EScheduleDocumentLoadStatus.ImportFailed)
        {
            throw new ArgumentException(
                "Import failures require at least one diagnostic.",
                nameof(status));
        }

        CourseImportDiagnostic[] noImportDiagnostics =
            Array.Empty<CourseImportDiagnostic>();
        return new ScheduleDocumentLoadFailure(
            status,
            noImportDiagnostics,
            EDiagnosticCollectionCompletion.Completed);
    }

    private static bool isFailureStatus(EScheduleDocumentLoadStatus status)
    {
        switch (status)
        {
            case EScheduleDocumentLoadStatus.ImportFailed:
            case EScheduleDocumentLoadStatus.NoValidSchedules:
            case EScheduleDocumentLoadStatus.UnsupportedAcademicPeriod:
            case EScheduleDocumentLoadStatus.Canceled:
                return true;
            case EScheduleDocumentLoadStatus.Loaded:
            case EScheduleDocumentLoadStatus.LoadedWithMaximumScheduleCountReached:
                return false;
            default:
                return false;
        }
    }
}
