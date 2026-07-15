using System;
using System.Diagnostics;

namespace TimetableGenerator.Application.Documents;

public sealed class ScheduleDocumentLoadResult
{
    public EScheduleDocumentLoadStatus Status { get; }

    private readonly ScheduleDocument? mDocumentOrNull;

    public bool HasDocument
    {
        get
        {
            return mDocumentOrNull != null;
        }
    }

    private readonly ScheduleDocumentLoadFailure? mFailureOrNull;

    public bool HasFailure
    {
        get
        {
            return mFailureOrNull != null;
        }
    }

    public bool IsSuccessful
    {
        get
        {
            return HasDocument;
        }
    }

    public bool HasReachedMaximumScheduleCount
    {
        get
        {
            return Status ==
                EScheduleDocumentLoadStatus.LoadedWithMaximumScheduleCountReached;
        }
    }

    private ScheduleDocumentLoadResult(
        EScheduleDocumentLoadStatus status,
        ScheduleDocument? documentOrNull,
        ScheduleDocumentLoadFailure? failureOrNull)
    {
        if (Enum.IsDefined(typeof(EScheduleDocumentLoadStatus), status) == false)
        {
            throw new ArgumentOutOfRangeException(nameof(status));
        }

        bool hasDocument = documentOrNull != null;
        bool hasFailure = failureOrNull != null;
        if (hasDocument == hasFailure)
        {
            throw new ArgumentException(
                "Document load results require exactly one document or failure.");
        }

        if (hasDocument && isSuccessfulStatus(status) == false)
        {
            throw new ArgumentException(
                "Document load result status does not describe a loaded document.",
                nameof(status));
        }

        if (failureOrNull != null && failureOrNull.Status != status)
        {
            throw new ArgumentException(
                "Document load failure status must match its result status.",
                nameof(failureOrNull));
        }

        Status = status;
        mDocumentOrNull = documentOrNull;
        mFailureOrNull = failureOrNull;
    }

    public ScheduleDocument GetDocument()
    {
        if (mDocumentOrNull == null)
        {
            throw new InvalidOperationException(
                "A failed document load result does not contain a schedule document.");
        }

        return mDocumentOrNull;
    }

    public ScheduleDocumentLoadFailure GetFailure()
    {
        if (mFailureOrNull == null)
        {
            throw new InvalidOperationException(
                "A successful document load result does not contain failure information.");
        }

        return mFailureOrNull;
    }

    internal static ScheduleDocumentLoadResult createLoaded(
        ScheduleDocument document,
        EScheduleDocumentLoadStatus status)
    {
        if (document == null)
        {
            throw new ArgumentNullException(nameof(document));
        }

        if (isSuccessfulStatus(status) == false)
        {
            throw new ArgumentOutOfRangeException(nameof(status));
        }

        return new ScheduleDocumentLoadResult(status, document, null);
    }

    internal static ScheduleDocumentLoadResult createFailed(
        ScheduleDocumentLoadFailure failure)
    {
        if (failure == null)
        {
            throw new ArgumentNullException(nameof(failure));
        }

        return new ScheduleDocumentLoadResult(failure.Status, null, failure);
    }

    private static bool isSuccessfulStatus(EScheduleDocumentLoadStatus status)
    {
        switch (status)
        {
            case EScheduleDocumentLoadStatus.Loaded:
            case EScheduleDocumentLoadStatus.LoadedWithMaximumScheduleCountReached:
                return true;
            case EScheduleDocumentLoadStatus.ImportFailed:
            case EScheduleDocumentLoadStatus.NoValidSchedules:
            case EScheduleDocumentLoadStatus.UnsupportedAcademicPeriod:
            case EScheduleDocumentLoadStatus.Canceled:
                return false;
            default:
                Debug.Fail("Unexpected document load status: " + status);
                return false;
        }
    }
}
