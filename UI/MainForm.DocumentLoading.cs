using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using TimetableGenerator.Application.Documents;
using TimetableGenerator.Infrastructure.Csv;
using TimetableGenerator.Presentation.Schedules;
using TimetableGenerator.UI.Product;

namespace TimetableGenerator;

internal sealed partial class MainForm
{
    private void onCsvOpenRequested(object? senderOrNull, EventArgs eventArgs)
    {
        requestCsvOpen();
    }

    private void onCsvFileDropped(object? senderOrNull, CsvFileDroppedEventArgs eventArgs)
    {
        if (mOperation != EAppOperation.None)
        {
            return;
        }

        beginDocumentLoad(eventArgs.SourceFilePath);
    }

    private void onExampleFormatRequested(object? senderOrNull, EventArgs eventArgs)
    {
        using (ExampleFormatDialog dialog = new ExampleFormatDialog())
        {
            dialog.ShowDialog(this);
        }
    }

    private void requestCsvOpen()
    {
        if (mOperation != EAppOperation.None)
        {
            return;
        }

        using (OpenFileDialog dialog = new OpenFileDialog())
        {
            dialog.AddExtension = true;
            dialog.CheckFileExists = true;
            dialog.CheckPathExists = true;
            dialog.DefaultExt = "csv";
            dialog.Filter = "CSV 파일 (*.csv)|*.csv";
            dialog.Multiselect = false;
            dialog.RestoreDirectory = true;
            dialog.Title = "시간표 CSV 파일 선택";

            string? initialDirectoryOrNull = findInitialCsvDirectoryOrNull();
            if (initialDirectoryOrNull != null)
            {
                dialog.InitialDirectory = initialDirectoryOrNull;
            }

            if (dialog.ShowDialog(this) != DialogResult.OK)
            {
                return;
            }

            try
            {
                CsvInputFilePath sourceFilePath = new CsvInputFilePath(dialog.FileName);
                beginDocumentLoad(sourceFilePath);
            }
            catch (ArgumentException exception)
            {
                showUnexpectedInputPath(exception);
            }
        }
    }

    private string? findInitialCsvDirectoryOrNull()
    {
        if (mDocumentOrNull != null)
        {
            return Path.GetDirectoryName(mDocumentOrNull.SourceFilePath.Value);
        }

        string documentsDirectory = Environment.GetFolderPath(
            Environment.SpecialFolder.MyDocuments);
        if (string.IsNullOrWhiteSpace(documentsDirectory))
        {
            return null;
        }

        return documentsDirectory;
    }

    private void beginDocumentLoad(CsvInputFilePath sourceFilePath)
    {
        if (mOperation != EAppOperation.None)
        {
            return;
        }

        mOperation = EAppOperation.LoadingDocument;
        mOperationCancellationOrNull = new CancellationTokenSource();
        mHeaderControl.showCurrentFileName(sourceFilePath.FileName);
        mLoadingControl.showDocumentLoading(sourceFilePath.FileName);
        showView(EAppViewState.Loading);
        mStatusControl.showStatus(EAppStatusKind.Busy, "CSV를 확인하고 시간표 조합을 만들고 있습니다");
        updateCommandAvailability();

        Task<ScheduleDocumentLoadResult> loadTask = mDocumentLoader.LoadDocumentAsync(
            sourceFilePath,
            mOperationCancellationOrNull.Token);
        Task continuationTask = loadTask.ContinueWith(
            completeDocumentLoad,
            CancellationToken.None,
            TaskContinuationOptions.None,
            TaskScheduler.FromCurrentSynchronizationContext());
        observeContinuationTask(continuationTask);
    }

    private void completeDocumentLoad(Task<ScheduleDocumentLoadResult> loadTask)
    {
        ScheduleDocumentLoadResult? loadResultOrNull = null;
        try
        {
            loadResultOrNull = loadTask.GetAwaiter().GetResult();
        }
        catch (Exception exception)
        {
            EOperationFinishAction finishAction = finishCurrentOperation();
            if (finishAction == EOperationFinishAction.CloseWindow)
            {
                Close();
                return;
            }

            showUnexpectedOperationFailure(
                exception,
                new MessageStateDescription("CSV를 처리하지 못했습니다."));
            return;
        }

        if (loadResultOrNull == null)
        {
            Debug.Fail("A completed document load task returned null.");
            throw new InvalidOperationException(
                "A completed document load task did not return a result.");
        }

        ScheduleDocumentLoadResult loadResult = loadResultOrNull;
        EOperationFinishAction successfulFinishAction = finishCurrentOperation();
        if (successfulFinishAction == EOperationFinishAction.CloseWindow)
        {
            Close();
            return;
        }

        if (loadResult.IsSuccessful)
        {
            showLoadedDocument(loadResult);
            return;
        }

        showDocumentLoadFailure(loadResult.GetFailure());
    }

    private void showLoadedDocument(ScheduleDocumentLoadResult loadResult)
    {
        ScheduleDocument document = loadResult.GetDocument();
        mDocumentOrNull = document;
        mSelectedScheduleIndex = new ScheduleIndex(0);
        showReadyDocument(document, mSelectedScheduleIndex);

        if (loadResult.HasReachedMaximumScheduleCount)
        {
            mStatusControl.showStatus(
                EAppStatusKind.Success,
                document.ScheduleCount + "개의 시간표를 표시합니다 · 생성 상한에 도달해 입력 조건을 좁히는 것을 권장합니다");
        }
        else
        {
            mStatusControl.showStatus(
                EAppStatusKind.Success,
                document.ScheduleCount + "개의 충돌 없는 시간표를 만들었습니다");
        }
    }

    private void showDocumentLoadFailure(ScheduleDocumentLoadFailure failure)
    {
        if (failure == null)
        {
            throw new ArgumentNullException(nameof(failure));
        }

        if (failure.Status == EScheduleDocumentLoadStatus.Canceled)
        {
            restoreDocumentOrWelcome("작업을 취소했습니다");
            return;
        }

        mShouldRestoreDocumentFromMessageAction = mDocumentOrNull != null;
        MessageStateActionText actionText = new MessageStateActionText(
            mShouldRestoreDocumentFromMessageAction
                ? "이전 시간표로 돌아가기"
                : "다른 CSV 선택");
        MessageStateContent messageContent = createDocumentLoadFailureContent(
            failure,
            actionText);

        mMessageStateControl.showContent(messageContent);
        showView(EAppViewState.Message);
        mStatusControl.showStatus(EAppStatusKind.Error, messageContent.Title.Value);
        updateCommandAvailability();
    }

    private static MessageStateContent createDocumentLoadFailureContent(
        ScheduleDocumentLoadFailure failure,
        MessageStateActionText actionText)
    {
        switch (failure.Status)
        {
            case EScheduleDocumentLoadStatus.ImportFailed:
                string diagnosticDetails = CourseImportDiagnosticTextFormatter.formatDiagnostics(
                    failure.ImportDiagnostics,
                    failure.ImportDiagnosticCollectionCompletion);
                return new MessageStateContent(
                    EMessageStateKind.Error,
                    new MessageStateTitle("CSV를 확인해 주세요"),
                    new MessageStateDescription("형식을 고친 뒤 같은 파일을 다시 불러올 수 있습니다."),
                    new MessageStateDetail(diagnosticDetails),
                    actionText);
            case EScheduleDocumentLoadStatus.NoValidSchedules:
                return new MessageStateContent(
                    EMessageStateKind.Empty,
                    new MessageStateTitle("충돌 없는 시간표를 만들 수 없어요"),
                    new MessageStateDescription("모든 과목에서 하나씩 선택했을 때 수업 시간이 서로 겹칩니다."),
                    new MessageStateDetail("같은 CourseId의 대안 분반과 TimeSlots를 확인해 주세요."),
                    actionText);
            case EScheduleDocumentLoadStatus.UnsupportedAcademicPeriod:
                return new MessageStateContent(
                    EMessageStateKind.Error,
                    new MessageStateTitle("지원 교시 범위를 확인해 주세요"),
                    new MessageStateDescription("제품 시간표는 1교시부터 10교시까지 표시합니다."),
                    new MessageStateDetail("1교시는 08:30에 시작하며 수업 75분과 휴식 15분을 기준으로 합니다."),
                    actionText);
            case EScheduleDocumentLoadStatus.Canceled:
            case EScheduleDocumentLoadStatus.Loaded:
            case EScheduleDocumentLoadStatus.LoadedWithMaximumScheduleCountReached:
            default:
                Debug.Fail("Unexpected document load failure status: " + failure.Status);
                throw new ArgumentOutOfRangeException(nameof(failure));
        }
    }

    private void showReadyDocument(
        ScheduleDocument document,
        ScheduleIndex scheduleIndex)
    {
        IReadOnlyList<ScheduleGridViewModel> scheduleGrids = getScheduleGrids(document);
        mHeaderControl.showCurrentFileName(document.SourceFilePath.FileName);
        mReadyScheduleControl.showSchedules(scheduleGrids, scheduleIndex);
        showView(EAppViewState.Ready);
        mShouldRestoreDocumentFromMessageAction = false;
        updateCommandAvailability();
    }

    private static IReadOnlyList<ScheduleGridViewModel> getScheduleGrids(
        ScheduleDocument document)
    {
        if (document == null)
        {
            throw new ArgumentNullException(nameof(document));
        }

        List<ScheduleGridViewModel> scheduleGrids = new List<ScheduleGridViewModel>(
            document.ScheduleCount);
        foreach (ScheduleDocumentSchedule documentSchedule in document.Schedules)
        {
            scheduleGrids.Add(documentSchedule.GridViewModel);
        }

        return scheduleGrids.AsReadOnly();
    }
}
