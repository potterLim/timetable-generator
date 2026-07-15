using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using TimetableGenerator.Application.Documents;
using TimetableGenerator.Infrastructure.Exporting;
using TimetableGenerator.Presentation.Schedules;
using TimetableGenerator.UI.Product;

namespace TimetableGenerator;

internal sealed partial class MainForm
{
    private void onPngExportRequested(object? senderOrNull, EventArgs eventArgs)
    {
        requestScheduleExport(EScheduleExportScope.CurrentSchedule);
    }

    private void onOutputFolderOpenRequested(object? senderOrNull, EventArgs eventArgs)
    {
        openLastExportDirectory();
    }

    private void onScheduleExportProgress(SchedulePngExportProgress progress)
    {
        if (mOperation != EAppOperation.ExportingSchedules)
        {
            return;
        }

        mLoadingControl.showScheduleExportProgress(progress);
        mStatusControl.showStatus(
            EAppStatusKind.Busy,
            "PNG 내보내기 · " + progress.ProcessedScheduleCount + " / " +
            progress.TotalScheduleCount);
    }

    private void requestScheduleExport(EScheduleExportScope initialScope)
    {
        if (mDocumentOrNull == null || mOperation != EAppOperation.None)
        {
            return;
        }

        ScheduleExportDialogContext context = new ScheduleExportDialogContext(
            mDocumentOrNull,
            mSelectedScheduleIndex,
            findInitialExportDirectory(),
            initialScope);
        using (ScheduleExportDialog dialog = new ScheduleExportDialog(context))
        {
            if (dialog.ShowDialog(this) != DialogResult.OK)
            {
                return;
            }

            beginScheduleExport(dialog.getChoice());
        }
    }

    private ScheduleExportDirectoryPath findInitialExportDirectory()
    {
        if (mLastExportDirectory.IsValid && Directory.Exists(mLastExportDirectory.Value))
        {
            return mLastExportDirectory;
        }

        if (mDocumentOrNull != null)
        {
            string? sourceDirectoryOrNull = Path.GetDirectoryName(
                mDocumentOrNull.SourceFilePath.Value);
            if (sourceDirectoryOrNull != null && Directory.Exists(sourceDirectoryOrNull))
            {
                return new ScheduleExportDirectoryPath(sourceDirectoryOrNull);
            }
        }

        string picturesDirectory = Environment.GetFolderPath(
            Environment.SpecialFolder.MyPictures);
        if (string.IsNullOrWhiteSpace(picturesDirectory))
        {
            picturesDirectory = AppContext.BaseDirectory;
        }

        return new ScheduleExportDirectoryPath(picturesDirectory);
    }

    private void beginScheduleExport(ScheduleExportChoice exportChoice)
    {
        if (mDocumentOrNull == null || mOperation != EAppOperation.None)
        {
            return;
        }

        mOperation = EAppOperation.ExportingSchedules;
        mOperationCancellationOrNull = new CancellationTokenSource();
        mPendingExportDirectory = exportChoice.DestinationDirectory;
        mLoadingControl.showScheduleExportStarting(exportChoice);
        showView(EAppViewState.Loading);
        mStatusControl.showStatus(EAppStatusKind.Busy, "PNG 내보내기를 준비하고 있습니다");
        updateCommandAvailability();

        Progress<SchedulePngExportProgress> progress =
            new Progress<SchedulePngExportProgress>(onScheduleExportProgress);
        Task<SchedulePngExportResult> exportTask = createScheduleExportTaskAsync(
            exportChoice,
            progress,
            mOperationCancellationOrNull.Token);
        Task continuationTask = exportTask.ContinueWith(
            completeScheduleExport,
            CancellationToken.None,
            TaskContinuationOptions.None,
            TaskScheduler.FromCurrentSynchronizationContext());
        observeContinuationTask(continuationTask);
    }

    private Task<SchedulePngExportResult> createScheduleExportTaskAsync(
        ScheduleExportChoice exportChoice,
        IProgress<SchedulePngExportProgress> progress,
        CancellationToken cancellationToken)
    {
        if (mDocumentOrNull == null)
        {
            throw new InvalidOperationException("A schedule document is required for export.");
        }

        string sourceBaseName = Path.GetFileNameWithoutExtension(
            mDocumentOrNull.SourceFilePath.Value);
        ScheduleExportBaseName exportBaseName = new ScheduleExportBaseName(sourceBaseName);

        if (exportChoice.Scope == EScheduleExportScope.CurrentSchedule)
        {
            ScheduleDocumentSchedule selectedSchedule =
                mDocumentOrNull.Schedules[mSelectedScheduleIndex.Value];
            ScheduleExportNumber exportNumber = new ScheduleExportNumber(
                ScheduleNumber.FromIndex(mSelectedScheduleIndex).Value);
            SchedulePngExportRequest request = new SchedulePngExportRequest(
                selectedSchedule.GridViewModel,
                exportNumber,
                exportChoice.DestinationDirectory,
                exportBaseName);
            return mPngExporter.ExportCurrentAsync(request, progress, cancellationToken);
        }

        IReadOnlyList<ScheduleGridViewModel> scheduleGrids = getScheduleGrids(mDocumentOrNull);
        SchedulePngBatchExportRequest batchRequest = new SchedulePngBatchExportRequest(
            scheduleGrids,
            exportChoice.DestinationDirectory,
            exportBaseName);
        return mPngExporter.ExportAllAsync(batchRequest, progress, cancellationToken);
    }

    private void completeScheduleExport(Task<SchedulePngExportResult> exportTask)
    {
        SchedulePngExportResult? exportResultOrNull = null;
        try
        {
            exportResultOrNull = exportTask.GetAwaiter().GetResult();
        }
        catch (OperationCanceledException)
        {
            EOperationFinishAction finishAction = finishCurrentOperation();
            if (finishAction == EOperationFinishAction.CloseWindow)
            {
                Close();
                return;
            }

            restoreDocumentOrWelcome("PNG 내보내기를 취소했습니다");
            return;
        }
        catch (SchedulePngExportCleanupException exception)
        {
            Trace.TraceError(exception.ToString());
            EOperationFinishAction finishAction = finishCurrentOperation();
            mLastExportDirectory = mPendingExportDirectory;
            if (finishAction == EOperationFinishAction.CloseWindow)
            {
                showRetainedExportArtifacts(
                    exception.RetainedArtifacts,
                    EExportArtifactNoticeKind.Closing);
                Close();
                return;
            }

            restoreDocumentOrWelcome("PNG 내보내기를 완료하지 못했습니다");
            showRetainedExportArtifacts(
                exception.RetainedArtifacts,
                EExportArtifactNoticeKind.Failed);
            updateCommandAvailability();
            return;
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
                new MessageStateDescription("PNG를 내보내지 못했습니다."));
            return;
        }

        if (exportResultOrNull == null)
        {
            Debug.Fail("A completed PNG export task returned null.");
            throw new InvalidOperationException(
                "A completed PNG export task did not return a result.");
        }

        SchedulePngExportResult exportResult = exportResultOrNull;
        EOperationFinishAction successfulFinishAction = finishCurrentOperation();
        if (successfulFinishAction == EOperationFinishAction.CloseWindow)
        {
            if (exportResult.Completion == ESchedulePngExportCompletion.Canceled &&
                exportResult.RetainedArtifacts.Count > 0)
            {
                mLastExportDirectory = mPendingExportDirectory;
                showRetainedExportArtifacts(
                    exportResult.RetainedArtifacts,
                    EExportArtifactNoticeKind.Closing);
            }

            Close();
            return;
        }

        if (mDocumentOrNull != null)
        {
            showReadyDocument(mDocumentOrNull, mSelectedScheduleIndex);
        }

        if (exportResult.HasExportedFiles || exportResult.RetainedArtifacts.Count > 0)
        {
            mLastExportDirectory = mPendingExportDirectory;
        }

        showExportResult(exportResult);
        updateCommandAvailability();
    }

    private void showExportResult(SchedulePngExportResult exportResult)
    {
        int exportedFileCount = exportResult.ExportedFiles.Count;
        if (exportResult.Completion == ESchedulePngExportCompletion.Canceled)
        {
            showCanceledExportResult(exportResult.RetainedArtifacts);
            return;
        }

        if (exportResult.Completion == ESchedulePngExportCompletion.Succeeded)
        {
            mStatusControl.showStatus(
                EAppStatusKind.Success,
                exportedFileCount + "개의 PNG를 저장했습니다 · " +
                mPendingExportDirectory.Value);
            return;
        }

        int failureCount = exportResult.Failures.Count;
        if (exportResult.Completion == ESchedulePngExportCompletion.PartiallySucceeded)
        {
            mStatusControl.showStatus(
                EAppStatusKind.Error,
                exportedFileCount + "개 저장 · " + failureCount + "개 실패");
        }
        else
        {
            mStatusControl.showStatus(EAppStatusKind.Error, "PNG 파일을 저장하지 못했습니다");
        }

        showExportFailureDialog(exportResult.Failures);
    }

    private void showCanceledExportResult(
        IReadOnlyList<SchedulePngExportArtifact> retainedArtifacts)
    {
        if (retainedArtifacts.Count == 0)
        {
            mStatusControl.showStatus(
                EAppStatusKind.Neutral,
                "PNG 내보내기를 취소했습니다 · 저장된 파일은 없습니다");
            return;
        }

        mStatusControl.showStatus(
            EAppStatusKind.Error,
            "내보내기는 취소됐지만 " + retainedArtifacts.Count +
            "개 파일을 정리하지 못했습니다");
        showRetainedExportArtifacts(
            retainedArtifacts,
            EExportArtifactNoticeKind.Canceled);
    }

    private void showRetainedExportArtifacts(
        IReadOnlyList<SchedulePngExportArtifact> retainedArtifacts,
        EExportArtifactNoticeKind noticeKind)
    {
        StringBuilder messageBuilder = new StringBuilder();
        messageBuilder.AppendLine(findArtifactNoticeIntroduction(noticeKind));
        messageBuilder.AppendLine();
        int visibleFileCount = Math.Min(
            retainedArtifacts.Count,
            MAXIMUM_VISIBLE_EXPORT_FAILURE_COUNT);
        for (int fileIndex = 0; fileIndex < visibleFileCount; ++fileIndex)
        {
            messageBuilder.AppendLine(retainedArtifacts[fileIndex].FilePath.Value);
        }

        if (retainedArtifacts.Count > visibleFileCount)
        {
            messageBuilder.AppendLine();
            messageBuilder.Append("그 밖의 파일 ");
            messageBuilder.Append(retainedArtifacts.Count - visibleFileCount);
            messageBuilder.AppendLine("개가 더 남아 있습니다.");
        }

        messageBuilder.AppendLine();
        messageBuilder.Append("폴더: ");
        messageBuilder.Append(mPendingExportDirectory.Value);

        MessageBox.Show(
            this,
            messageBuilder.ToString().TrimEnd(),
            findArtifactNoticeTitle(noticeKind),
            MessageBoxButtons.OK,
            MessageBoxIcon.Warning);
    }

    private static string findArtifactNoticeTitle(EExportArtifactNoticeKind noticeKind)
    {
        switch (noticeKind)
        {
            case EExportArtifactNoticeKind.Canceled:
                return "PNG 내보내기 취소";
            case EExportArtifactNoticeKind.Failed:
            case EExportArtifactNoticeKind.Closing:
                return "PNG 내보내기 문제";
            default:
                Debug.Fail("Unexpected export artifact notice kind: " + noticeKind);
                throw new ArgumentOutOfRangeException(nameof(noticeKind));
        }
    }

    private static string findArtifactNoticeIntroduction(
        EExportArtifactNoticeKind noticeKind)
    {
        switch (noticeKind)
        {
            case EExportArtifactNoticeKind.Canceled:
                return "내보내기는 취소됐지만 다음 파일이 남아 있습니다.";
            case EExportArtifactNoticeKind.Failed:
                return "내보내기를 완료하지 못했고 다음 파일이 남아 있습니다.";
            case EExportArtifactNoticeKind.Closing:
                return "프로그램을 닫기 전에 정리하지 못한 파일을 확인해 주세요.";
            default:
                Debug.Fail("Unexpected export artifact notice kind: " + noticeKind);
                throw new ArgumentOutOfRangeException(nameof(noticeKind));
        }
    }

    private void showExportFailureDialog(
        IReadOnlyList<SchedulePngExportFailure> failures)
    {
        StringBuilder messageBuilder = new StringBuilder();
        int visibleFailureCount = Math.Min(
            failures.Count,
            MAXIMUM_VISIBLE_EXPORT_FAILURE_COUNT);
        for (int failureIndex = 0; failureIndex < visibleFailureCount; ++failureIndex)
        {
            if (failureIndex > 0)
            {
                messageBuilder.AppendLine();
                messageBuilder.AppendLine();
            }

            messageBuilder.Append(failures[failureIndex].Message);
        }

        if (failures.Count > visibleFailureCount)
        {
            messageBuilder.AppendLine();
            messageBuilder.AppendLine();
            messageBuilder.Append("그 밖의 실패 ");
            messageBuilder.Append(failures.Count - visibleFailureCount);
            messageBuilder.Append("개가 더 있습니다.");
        }

        MessageBox.Show(
            this,
            messageBuilder.ToString(),
            "PNG 내보내기 문제",
            MessageBoxButtons.OK,
            MessageBoxIcon.Warning);
    }

    private void openLastExportDirectory()
    {
        if (mOperation != EAppOperation.None || mLastExportDirectory.IsValid == false)
        {
            return;
        }

        if (Directory.Exists(mLastExportDirectory.Value) == false)
        {
            mStatusControl.showStatus(EAppStatusKind.Error, "마지막 내보내기 폴더를 찾을 수 없습니다");
            updateCommandAvailability();
            return;
        }

        try
        {
            ProcessStartInfo processStartInfo = new ProcessStartInfo();
            processStartInfo.FileName = mLastExportDirectory.Value;
            processStartInfo.UseShellExecute = true;
            Process? openedProcessOrNull = Process.Start(processStartInfo);
            if (openedProcessOrNull != null)
            {
                using (openedProcessOrNull)
                {
                }
            }
        }
        catch (Exception exception) when (
            exception is Win32Exception ||
            exception is InvalidOperationException)
        {
            Trace.TraceError(exception.ToString());
            mStatusControl.showStatus(EAppStatusKind.Error, "내보내기 폴더를 열지 못했습니다");
        }
    }
}
