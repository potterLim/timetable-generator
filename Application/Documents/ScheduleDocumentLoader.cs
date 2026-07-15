using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using TimetableGenerator.Core.Application.Scheduling;
using TimetableGenerator.Core.Domain;
using TimetableGenerator.Infrastructure.Csv;
using TimetableGenerator.Presentation.Schedules;
using CorePeriod = TimetableGenerator.Core.Domain.Period;
using CoreScheduleGenerator = TimetableGenerator.Core.Application.Scheduling.ScheduleGenerator;

namespace TimetableGenerator.Application.Documents;

public sealed class ScheduleDocumentLoader
{
    private readonly ICourseCsvImporter mCourseCsvImporter;
    private readonly CoreScheduleGenerator mScheduleGenerator;
    private readonly ScheduleDocumentLoadOptions mOptions;

    public ScheduleDocumentLoader()
        : this(ScheduleDocumentLoadOptions.CreateDefault())
    {
    }

    public ScheduleDocumentLoader(ScheduleDocumentLoadOptions options)
        : this(options, new CourseCsvImporter())
    {
    }

    public ScheduleDocumentLoader(
        ScheduleDocumentLoadOptions options,
        ICourseCsvImporter courseCsvImporter)
    {
        if (options == null)
        {
            throw new ArgumentNullException(nameof(options));
        }

        if (courseCsvImporter == null)
        {
            throw new ArgumentNullException(nameof(courseCsvImporter));
        }

        mCourseCsvImporter = courseCsvImporter;
        mScheduleGenerator = new CoreScheduleGenerator();
        mOptions = options;
    }

    public Task<ScheduleDocumentLoadResult> LoadDocumentAsync(
        CsvInputFilePath sourceFilePath,
        CancellationToken cancellationToken)
    {
        // Expected failures and cancellation remain typed so the UI has one completion path.
        return Task.Run(() => loadDocument(sourceFilePath, cancellationToken));
    }

    private ScheduleDocumentLoadResult loadDocument(
        CsvInputFilePath sourceFilePath,
        CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return createFailureResult(EScheduleDocumentLoadStatus.Canceled);
        }

        try
        {
            return loadDocumentWhenNotCanceled(sourceFilePath, cancellationToken);
        }
        catch (OperationCanceledException exception)
            when (exception.CancellationToken == cancellationToken
                && cancellationToken.IsCancellationRequested)
        {
            return createFailureResult(EScheduleDocumentLoadStatus.Canceled);
        }
    }

    private ScheduleDocumentLoadResult loadDocumentWhenNotCanceled(
        CsvInputFilePath sourceFilePath,
        CancellationToken cancellationToken)
    {
        CourseImportResult courseImportResult = mCourseCsvImporter.ImportCourses(
            sourceFilePath,
            mOptions.CourseImportOptions,
            cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();

        if (courseImportResult.IsSuccessful == false)
        {
            ScheduleDocumentLoadFailure importFailure =
                ScheduleDocumentLoadFailure.createFromImportResult(courseImportResult);
            return ScheduleDocumentLoadResult.createFailed(importFailure);
        }

        if (hasUnsupportedAcademicPeriod(
            courseImportResult.CourseOfferings,
            cancellationToken))
        {
            return createFailureResult(
                EScheduleDocumentLoadStatus.UnsupportedAcademicPeriod);
        }

        ScheduleGenerationResult generationResult = mScheduleGenerator.GenerateSchedules(
            courseImportResult.CourseOfferings,
            mOptions.ScheduleGenerationOptions,
            cancellationToken);
        if (generationResult.IsCanceled)
        {
            return createFailureResult(EScheduleDocumentLoadStatus.Canceled);
        }

        if (generationResult.Schedules.Count == 0)
        {
            return createFailureResult(EScheduleDocumentLoadStatus.NoValidSchedules);
        }

        ScheduleDocument document = createDocument(
            sourceFilePath,
            generationResult.Schedules,
            cancellationToken);
        EScheduleDocumentLoadStatus loadedStatus = getLoadedStatus(generationResult.Completion);
        return ScheduleDocumentLoadResult.createLoaded(document, loadedStatus);
    }

    private static bool hasUnsupportedAcademicPeriod(
        IReadOnlyList<CourseOffering> courseOfferings,
        CancellationToken cancellationToken)
    {
        CorePeriod maximumSupportedPeriod = AcademicPeriodTimePolicy.MaximumSupportedPeriod;
        foreach (CourseOffering courseOffering in courseOfferings)
        {
            cancellationToken.ThrowIfCancellationRequested();

            foreach (ScheduleSlot scheduleSlot in courseOffering.ScheduleSlots)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (scheduleSlot.Period.Value > maximumSupportedPeriod.Value)
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static ScheduleDocument createDocument(
        CsvInputFilePath sourceFilePath,
        IReadOnlyList<GeneratedSchedule> generatedSchedules,
        CancellationToken cancellationToken)
    {
        List<ScheduleDocumentSchedule> documentSchedules =
            new List<ScheduleDocumentSchedule>(generatedSchedules.Count);
        foreach (GeneratedSchedule generatedSchedule in generatedSchedules)
        {
            cancellationToken.ThrowIfCancellationRequested();

            ScheduleGridViewModel gridViewModel =
                ScheduleGridViewModelFactory.Create(generatedSchedule);
            ScheduleDocumentSchedule documentSchedule = new ScheduleDocumentSchedule(
                generatedSchedule,
                gridViewModel);
            documentSchedules.Add(documentSchedule);
        }

        cancellationToken.ThrowIfCancellationRequested();
        return new ScheduleDocument(sourceFilePath, documentSchedules);
    }

    private static EScheduleDocumentLoadStatus getLoadedStatus(
        EScheduleGenerationCompletion generationCompletion)
    {
        switch (generationCompletion)
        {
            case EScheduleGenerationCompletion.Completed:
                return EScheduleDocumentLoadStatus.Loaded;
            case EScheduleGenerationCompletion.MaximumScheduleCountReached:
                return EScheduleDocumentLoadStatus.LoadedWithMaximumScheduleCountReached;
            case EScheduleGenerationCompletion.Canceled:
                throw new InvalidOperationException(
                    "Canceled schedule generation cannot create a document.");
            default:
                Debug.Fail("Unexpected schedule generation completion: " + generationCompletion);
                throw new ArgumentOutOfRangeException(nameof(generationCompletion));
        }
    }

    private static ScheduleDocumentLoadResult createFailureResult(
        EScheduleDocumentLoadStatus status)
    {
        ScheduleDocumentLoadFailure failure =
            ScheduleDocumentLoadFailure.createWithoutImportDiagnostics(status);
        return ScheduleDocumentLoadResult.createFailed(failure);
    }
}
