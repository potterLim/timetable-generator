using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TimetableGenerator.Application.Documents;
using TimetableGenerator.Core.Application.Scheduling;
using TimetableGenerator.Core.Domain;
using TimetableGenerator.Infrastructure.Csv;
using TimetableGenerator.Presentation.Schedules;

namespace TimetableGeneratorCore.Tests;

[TestClass]
public sealed class ScheduleDocumentLoaderTests
{
    private const string HEADER = "CourseId,Section,Name,TimeSlots,Classroom\r\n";

    [TestMethod]
    public async Task LoadDocumentAsyncBuildsAnImmutableDocumentAndCorrespondingGridsAsync()
    {
        string fileContent = HEADER +
            "1,01,자료구조,월요일1교시,공학관 101\r\n" +
            "2,01,알고리즘,화요일2교시,공학관 202\r\n";

        using (TemporaryCsvFile temporaryCsvFile = new TemporaryCsvFile(fileContent))
        {
            ScheduleDocumentLoader loader = new ScheduleDocumentLoader();

            ScheduleDocumentLoadResult documentLoadResult = await loader.LoadDocumentAsync(
                temporaryCsvFile.FilePath,
                CancellationToken.None);

            Assert.AreEqual(EScheduleDocumentLoadStatus.Loaded, documentLoadResult.Status);
            Assert.IsTrue(documentLoadResult.IsSuccessful);
            Assert.IsTrue(documentLoadResult.HasDocument);
            Assert.IsFalse(documentLoadResult.HasFailure);
            Assert.IsFalse(documentLoadResult.HasReachedMaximumScheduleCount);
            Assert.ThrowsExactly<InvalidOperationException>(
                () => documentLoadResult.GetFailure());

            ScheduleDocument document = documentLoadResult.GetDocument();
            Assert.AreEqual(temporaryCsvFile.FilePath, document.SourceFilePath);
            Assert.AreEqual(1, document.ScheduleCount);

            ScheduleDocumentSchedule documentSchedule = document.Schedules[0];
            Assert.HasCount(2, documentSchedule.GeneratedSchedule.CourseOfferings);
            Assert.AreEqual(
                2,
                documentSchedule.GridViewModel.Summary.SelectedCourseCount);

            ScheduleCellViewModel mondayCell = documentSchedule.GridViewModel.GetCell(
                EDay.Monday,
                new Period(1));
            Assert.AreSame(
                documentSchedule.GeneratedSchedule.CourseOfferings[0],
                mondayCell.GetCourseOffering());

            IList<ScheduleDocumentSchedule> schedules =
                (IList<ScheduleDocumentSchedule>)document.Schedules;
            Assert.ThrowsExactly<NotSupportedException>(
                () => schedules.Add(documentSchedule));
        }
    }

    [TestMethod]
    public async Task LoadDocumentAsyncPreservesBoundedImportDiagnosticsAsync()
    {
        string fileContent = HEADER +
            "bad,01,자료구조,월요일1교시,\r\n" +
            "also-bad,01,알고리즘,화요일2교시,\r\n";
        CourseCsvImportOptions importOptions = new CourseCsvImportOptions(
            new DiagnosticCountLimit(1));
        ScheduleDocumentLoadOptions loadOptions = new ScheduleDocumentLoadOptions(
            importOptions,
            ScheduleGenerationOptions.CreateDefault());

        using (TemporaryCsvFile temporaryCsvFile = new TemporaryCsvFile(fileContent))
        {
            ScheduleDocumentLoader loader = new ScheduleDocumentLoader(loadOptions);

            ScheduleDocumentLoadResult documentLoadResult = await loader.LoadDocumentAsync(
                temporaryCsvFile.FilePath,
                CancellationToken.None);

            Assert.AreEqual(
                EScheduleDocumentLoadStatus.ImportFailed,
                documentLoadResult.Status);
            Assert.IsFalse(documentLoadResult.IsSuccessful);
            Assert.IsFalse(documentLoadResult.HasDocument);
            Assert.IsTrue(documentLoadResult.HasFailure);
            Assert.ThrowsExactly<InvalidOperationException>(
                () => documentLoadResult.GetDocument());

            ScheduleDocumentLoadFailure failure = documentLoadResult.GetFailure();
            Assert.AreEqual(documentLoadResult.Status, failure.Status);
            Assert.IsTrue(failure.HasImportDiagnostics);
            Assert.IsTrue(failure.HasReachedImportDiagnosticLimit);
            Assert.AreEqual(
                EDiagnosticCollectionCompletion.MaximumCountReached,
                failure.ImportDiagnosticCollectionCompletion);
            Assert.HasCount(1, failure.ImportDiagnostics);
            Assert.AreEqual(
                ECourseImportErrorCode.InvalidCourseChoiceGroupId,
                failure.ImportDiagnostics[0].ErrorCode);
        }
    }

    [TestMethod]
    public async Task LoadDocumentAsyncDistinguishesZeroValidSchedulesAsync()
    {
        string fileContent = HEADER +
            "1,01,자료구조,월요일1교시,\r\n" +
            "2,01,알고리즘,월요일1교시,\r\n";

        using (TemporaryCsvFile temporaryCsvFile = new TemporaryCsvFile(fileContent))
        {
            ScheduleDocumentLoader loader = new ScheduleDocumentLoader();

            ScheduleDocumentLoadResult documentLoadResult = await loader.LoadDocumentAsync(
                temporaryCsvFile.FilePath,
                CancellationToken.None);

            Assert.AreEqual(
                EScheduleDocumentLoadStatus.NoValidSchedules,
                documentLoadResult.Status);
            ScheduleDocumentLoadFailure failure = documentLoadResult.GetFailure();
            Assert.IsFalse(failure.HasImportDiagnostics);
            Assert.IsFalse(failure.HasReachedImportDiagnosticLimit);
        }
    }

    [TestMethod]
    public async Task LoadDocumentAsyncKeepsTheDocumentWhenTheScheduleLimitIsReachedAsync()
    {
        string fileContent = HEADER +
            "1,01,자료구조,월요일1교시,\r\n" +
            "1,02,자료구조,화요일1교시,\r\n";
        ScheduleGenerationOptions generationOptions = new ScheduleGenerationOptions(
            new ScheduleCountLimit(1));
        ScheduleDocumentLoadOptions loadOptions = new ScheduleDocumentLoadOptions(
            CourseCsvImportOptions.CreateDefault(),
            generationOptions);

        using (TemporaryCsvFile temporaryCsvFile = new TemporaryCsvFile(fileContent))
        {
            ScheduleDocumentLoader loader = new ScheduleDocumentLoader(loadOptions);

            ScheduleDocumentLoadResult documentLoadResult = await loader.LoadDocumentAsync(
                temporaryCsvFile.FilePath,
                CancellationToken.None);

            Assert.AreEqual(
                EScheduleDocumentLoadStatus.LoadedWithMaximumScheduleCountReached,
                documentLoadResult.Status);
            Assert.IsTrue(documentLoadResult.IsSuccessful);
            Assert.IsTrue(documentLoadResult.HasReachedMaximumScheduleCount);
            Assert.AreEqual(1, documentLoadResult.GetDocument().ScheduleCount);
        }
    }

    [TestMethod]
    public async Task LoadDocumentAsyncRejectsPeriodsOutsideTheProductTimePolicyAsync()
    {
        string fileContent = HEADER +
            "1,01,야간 강의,월요일11교시,\r\n";

        using (TemporaryCsvFile temporaryCsvFile = new TemporaryCsvFile(fileContent))
        {
            ScheduleDocumentLoader loader = new ScheduleDocumentLoader();

            ScheduleDocumentLoadResult documentLoadResult = await loader.LoadDocumentAsync(
                temporaryCsvFile.FilePath,
                CancellationToken.None);

            Assert.AreEqual(
                EScheduleDocumentLoadStatus.UnsupportedAcademicPeriod,
                documentLoadResult.Status);
            Assert.IsFalse(documentLoadResult.IsSuccessful);
            Assert.IsFalse(documentLoadResult.GetFailure().HasImportDiagnostics);
        }
    }

    [TestMethod]
    public async Task LoadDocumentAsyncReturnsATypedResultForPreCanceledWorkAsync()
    {
        string fileContent = HEADER +
            "1,01,자료구조,월요일1교시,\r\n";

        using (TemporaryCsvFile temporaryCsvFile = new TemporaryCsvFile(fileContent))
        {
            using (CancellationTokenSource cancellationTokenSource =
                new CancellationTokenSource())
            {
                cancellationTokenSource.Cancel();
                ScheduleDocumentLoader loader = new ScheduleDocumentLoader();

                ScheduleDocumentLoadResult documentLoadResult = await loader.LoadDocumentAsync(
                    temporaryCsvFile.FilePath,
                    cancellationTokenSource.Token);

                Assert.AreEqual(
                    EScheduleDocumentLoadStatus.Canceled,
                    documentLoadResult.Status);
                Assert.IsFalse(documentLoadResult.IsSuccessful);
                Assert.IsFalse(documentLoadResult.GetFailure().HasImportDiagnostics);
            }
        }
    }

    [TestMethod]
    public async Task LoadDocumentAsyncReturnsATypedResultWhenImportIsCanceledAsync()
    {
        string fileContent = HEADER +
            "1,01,자료구조,월요일1교시,\r\n";

        using (TemporaryCsvFile temporaryCsvFile = new TemporaryCsvFile(fileContent))
        {
            using (CancellationTokenSource cancellationTokenSource =
                new CancellationTokenSource())
            {
                CancelingCourseCsvImporter courseCsvImporter =
                    new CancelingCourseCsvImporter(cancellationTokenSource);
                ScheduleDocumentLoader loader = new ScheduleDocumentLoader(
                    ScheduleDocumentLoadOptions.CreateDefault(),
                    courseCsvImporter);

                ScheduleDocumentLoadResult documentLoadResult = await loader.LoadDocumentAsync(
                    temporaryCsvFile.FilePath,
                    cancellationTokenSource.Token);

                Assert.AreEqual(
                    EScheduleDocumentLoadStatus.Canceled,
                    documentLoadResult.Status);
                Assert.IsFalse(documentLoadResult.IsSuccessful);
                Assert.IsFalse(documentLoadResult.GetFailure().HasImportDiagnostics);
            }
        }
    }

    private sealed class CancelingCourseCsvImporter : ICourseCsvImporter
    {
        private readonly CancellationTokenSource mCancellationTokenSource;

        public CancelingCourseCsvImporter(
            CancellationTokenSource cancellationTokenSource)
        {
            mCancellationTokenSource = cancellationTokenSource;
        }

        public CourseImportResult ImportCourses(
            CsvInputFilePath inputFilePath,
            CourseCsvImportOptions options,
            CancellationToken cancellationToken)
        {
            mCancellationTokenSource.Cancel();
            cancellationToken.ThrowIfCancellationRequested();
            throw new InvalidOperationException(
                "The cancellation test importer did not observe its token.");
        }
    }
}
