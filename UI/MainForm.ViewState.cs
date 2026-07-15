using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using TimetableGenerator.UI.Product;

namespace TimetableGenerator;

internal sealed partial class MainForm
{
    private void onCancelRequested(object? senderOrNull, EventArgs eventArgs)
    {
        cancelCurrentOperation();
    }

    private void onMessagePrimaryActionRequested(
        object? senderOrNull,
        EventArgs eventArgs)
    {
        if (mShouldRestoreDocumentFromMessageAction && mDocumentOrNull != null)
        {
            restoreDocumentOrWelcome("이전 시간표로 돌아왔습니다");
            return;
        }

        requestCsvOpen();
    }

    private void onSelectedScheduleChanged(
        object? senderOrNull,
        ScheduleSelectionChangedEventArgs eventArgs)
    {
        mSelectedScheduleIndex = eventArgs.SelectedIndex;
    }

    private void selectPreviousSchedule()
    {
        if (mOperation != EAppOperation.None ||
            mViewState != EAppViewState.Ready ||
            mSelectedScheduleIndex.HasPrevious == false)
        {
            return;
        }

        mReadyScheduleControl.selectSchedule(
            mSelectedScheduleIndex.GetPrevious());
    }

    private void selectNextSchedule()
    {
        if (mOperation != EAppOperation.None ||
            mViewState != EAppViewState.Ready ||
            mDocumentOrNull == null)
        {
            return;
        }

        ScheduleIndex nextScheduleIndex = mSelectedScheduleIndex.GetNext();
        if (nextScheduleIndex.Value >= mDocumentOrNull.ScheduleCount)
        {
            return;
        }

        mReadyScheduleControl.selectSchedule(nextScheduleIndex);
    }

    private void cancelCurrentOperation()
    {
        if (mOperationCancellationOrNull == null ||
            mOperationCancellationOrNull.IsCancellationRequested)
        {
            return;
        }

        mOperationCancellationOrNull.Cancel();
        mStatusControl.showStatus(
            EAppStatusKind.Busy,
            "현재 작업을 안전하게 취소하고 있습니다");
    }

    private EOperationFinishAction finishCurrentOperation()
    {
        if (mOperationCancellationOrNull != null)
        {
            mOperationCancellationOrNull.Dispose();
            mOperationCancellationOrNull = null;
        }

        mOperation = EAppOperation.None;
        if (mShouldCloseAfterOperation == false)
        {
            return EOperationFinishAction.Continue;
        }

        mShouldCloseAfterOperation = false;
        return EOperationFinishAction.CloseWindow;
    }

    private void restoreDocumentOrWelcome(string statusMessage)
    {
        if (mDocumentOrNull != null)
        {
            showReadyDocument(mDocumentOrNull, mSelectedScheduleIndex);
        }
        else
        {
            showWelcomeView();
        }

        mStatusControl.showStatus(EAppStatusKind.Neutral, statusMessage);
    }

    private void showUnexpectedInputPath(Exception exception)
    {
        Trace.TraceError(exception.ToString());
        mShouldRestoreDocumentFromMessageAction = mDocumentOrNull != null;
        MessageStateActionText actionText = new MessageStateActionText(
            mShouldRestoreDocumentFromMessageAction
                ? "이전 시간표로 돌아가기"
                : "다른 CSV 선택");
        MessageStateContent messageContent = new MessageStateContent(
            EMessageStateKind.Error,
            new MessageStateTitle("CSV 파일을 열 수 없어요"),
            new MessageStateDescription(
                "확장자가 .csv인 파일 하나를 선택해 주세요."),
            new MessageStateDetail(string.Empty),
            actionText);

        mMessageStateControl.showContent(messageContent);
        showView(EAppViewState.Message);
        mStatusControl.showStatus(
            EAppStatusKind.Error,
            messageContent.Title.Value);
        updateCommandAvailability();
    }

    private void showUnexpectedOperationFailure(
        Exception exception,
        MessageStateDescription userMessage)
    {
        if (userMessage == null)
        {
            throw new ArgumentNullException(nameof(userMessage));
        }

        Trace.TraceError(exception.ToString());
        mShouldRestoreDocumentFromMessageAction = mDocumentOrNull != null;
        MessageStateActionText actionText = new MessageStateActionText(
            mShouldRestoreDocumentFromMessageAction
                ? "이전 시간표로 돌아가기"
                : "다른 CSV 선택");
        MessageStateContent messageContent = new MessageStateContent(
            EMessageStateKind.Error,
            new MessageStateTitle("작업을 완료하지 못했어요"),
            userMessage,
            new MessageStateDetail(
                "파일이 다른 프로그램에서 사용 중인지 확인한 뒤 다시 시도해 주세요."),
            actionText);

        mMessageStateControl.showContent(messageContent);
        showView(EAppViewState.Message);
        mStatusControl.showStatus(EAppStatusKind.Error, userMessage.Value);
        updateCommandAvailability();
    }

    private void showWelcomeView()
    {
        mHeaderControl.clearCurrentFileName();
        mShouldRestoreDocumentFromMessageAction = false;
        showView(EAppViewState.Welcome);
        mStatusControl.showStatus(
            EAppStatusKind.Neutral,
            "CSV 파일을 불러오면 시작할 수 있습니다 · Ctrl+O");
        updateCommandAvailability();
    }

    private void showView(EAppViewState viewState)
    {
        mWelcomeControl.Visible = false;
        mLoadingControl.Visible = false;
        mMessageStateControl.Visible = false;
        mReadyScheduleControl.Visible = false;

        Control visibleControl = findViewControl(viewState);
        mViewState = viewState;
        visibleControl.Visible = true;
        visibleControl.BringToFront();
        mContentHost.PerformLayout();
        mContentHost.Invalidate(true);
        mContentHost.Update();
    }

    private Control findViewControl(EAppViewState viewState)
    {
        switch (viewState)
        {
            case EAppViewState.Welcome:
                return mWelcomeControl;
            case EAppViewState.Loading:
                return mLoadingControl;
            case EAppViewState.Message:
                return mMessageStateControl;
            case EAppViewState.Ready:
                return mReadyScheduleControl;
            default:
                Debug.Fail("Unexpected application view state: " + viewState);
                throw new ArgumentOutOfRangeException(nameof(viewState));
        }
    }

    private void updateCommandAvailability()
    {
        bool isIdle = mOperation == EAppOperation.None;
        bool hasDocument = mDocumentOrNull != null &&
            mViewState == EAppViewState.Ready;
        bool hasExportDirectory = mLastExportDirectory.IsValid &&
            Directory.Exists(mLastExportDirectory.Value);

        mHeaderControl.setCsvOpenAvailability(
            isIdle
                ? ECommandAvailability.Enabled
                : ECommandAvailability.Disabled);
        mHeaderControl.setPngExportAvailability(
            isIdle && hasDocument
                ? ECommandAvailability.Enabled
                : ECommandAvailability.Disabled);
        mHeaderControl.setOutputFolderAvailability(
            isIdle && hasExportDirectory
                ? ECommandAvailability.Enabled
                : ECommandAvailability.Disabled);
    }

    private void applyShellMetrics()
    {
        mShellLayout.RowStyles[0].Height = DesignTokens.scaleLogicalPixel(
            this,
            DesignTokens.APP_HEADER_HEIGHT);
        mShellLayout.RowStyles[2].Height = DesignTokens.scaleLogicalPixel(
            this,
            DesignTokens.APP_STATUS_HEIGHT);
    }

    private static void observeContinuationTask(Task continuationTask)
    {
        continuationTask.ContinueWith(
            traceContinuationFailure,
            CancellationToken.None,
            TaskContinuationOptions.OnlyOnFaulted |
                TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
    }

    private static void traceContinuationFailure(Task continuationTask)
    {
        if (continuationTask.Exception != null)
        {
            Trace.TraceError(continuationTask.Exception.ToString());
        }
    }
}
