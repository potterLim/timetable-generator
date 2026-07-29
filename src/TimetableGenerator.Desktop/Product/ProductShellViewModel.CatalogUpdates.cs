using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;

using Avalonia.Threading;

using TimetableGenerator.Desktop.Presentation;
using TimetableGenerator.Desktop.Product.CatalogUpdates;
using TimetableGenerator.Desktop.Product.Loading;
using TimetableGenerator.Infrastructure.Catalogs;

namespace TimetableGenerator.Desktop.Product;

internal sealed partial class ProductShellViewModel
{
    private const EProductWorkspaceRecoveryFlags RECOVERY_NOTICE_FLAGS = EProductWorkspaceRecoveryFlags.CatalogPreviousGeneration | EProductWorkspaceRecoveryFlags.WorkspacePreviousGeneration;

    private static readonly TimeSpan DEFAULT_STARTUP_CATALOG_UPDATE_WAIT = TimeSpan.FromSeconds(2.0);

    private readonly IProductCatalogUpdateService mCatalogUpdateService;

    private readonly TimeSpan mStartupCatalogUpdateWait;

    private readonly DelegateCommand mDismissProductNoticeCommand;

    private CancellationTokenSource mCatalogUpdateCancellationSource;

    private Task mCatalogUpdateTask;

    private string mCatalogUpdateNotice;

    private EProductWorkspaceRecoveryFlags mStartupRecoveryFlags;

    public bool HasCatalogUpdateNotice
    {
        get
        {
            return string.IsNullOrEmpty(mCatalogUpdateNotice) == false;
        }
    }

    public string CatalogUpdateNotice
    {
        get
        {
            return mCatalogUpdateNotice;
        }
    }

    public bool HasStartupRecoveryNotice
    {
        get
        {
            return (mStartupRecoveryFlags & RECOVERY_NOTICE_FLAGS) != EProductWorkspaceRecoveryFlags.None;
        }
    }

    public string StartupRecoveryNotice
    {
        get
        {
            return createStartupRecoveryNotice(mStartupRecoveryFlags);
        }
    }

    public bool HasProductNotice
    {
        get
        {
            return HasStartupRecoveryNotice || HasCatalogUpdateNotice;
        }
    }

    public string ProductNotice
    {
        get
        {
            string startupRecoveryNotice = StartupRecoveryNotice;
            if (string.IsNullOrEmpty(startupRecoveryNotice) == false)
            {
                return startupRecoveryNotice;
            }

            return mCatalogUpdateNotice;
        }
    }

    public ICommand DismissProductNoticeCommand
    {
        get
        {
            return mDismissProductNoticeCommand;
        }
    }

    internal Task CatalogUpdateTask
    {
        get
        {
            return mCatalogUpdateTask;
        }
    }

    private void clearCatalogUpdateNotice()
    {
        if (string.IsNullOrEmpty(mCatalogUpdateNotice))
        {
            return;
        }

        mCatalogUpdateNotice = string.Empty;
        raiseProductNoticePropertiesChanged();
    }

    private void dismissProductNotice()
    {
        if (HasStartupRecoveryNotice)
        {
            mStartupRecoveryFlags &= ~RECOVERY_NOTICE_FLAGS;
            raiseProductNoticePropertiesChanged();
            return;
        }

        clearCatalogUpdateNotice();
    }

    private async Task<StartupCatalogRefreshResult> refreshCatalogAtStartupAsync(ProductWorkspacePresentation presentation, CancellationTokenSource loadCancellationSource, CancellationTokenSource catalogUpdateCancellationSource)
    {
        if (presentation.CatalogOrigin == EProductCatalogOrigin.RemoteDownload)
        {
            return StartupCatalogRefreshResult.Completed(presentation);
        }

        Task<ProductCatalogUpdateResult> updateTask;
        try
        {
            updateTask = mCatalogUpdateService.CheckAndStageAsync(presentation.ActiveCatalogPackage, presentation.WorkspaceSnapshot, catalogUpdateCancellationSource.Token);
            _ = observeCatalogUpdateTaskAsync(updateTask);
        }
        catch (Exception exception)
        {
            Trace.TraceWarning("The startup catalog update check could not be started while the active cache remained available: {0}", exception);
            return StartupCatalogRefreshResult.Failed(presentation, exception);
        }

        Task waitTask = Task.Delay(mStartupCatalogUpdateWait, loadCancellationSource.Token);
        Task completedTask = await Task.WhenAny(updateTask, waitTask);
        if (ReferenceEquals(completedTask, updateTask) == false)
        {
            if (loadCancellationSource.IsCancellationRequested)
            {
                presentation.Workspace.Dispose();
                loadCancellationSource.Token.ThrowIfCancellationRequested();
            }

            return StartupCatalogRefreshResult.Pending(presentation, updateTask);
        }

        ProductCatalogUpdateResult updateResult;
        try
        {
            updateResult = await updateTask;
        }
        catch (OperationCanceledException)
            when (catalogUpdateCancellationSource.IsCancellationRequested)
        {
            presentation.Workspace.Dispose();
            throw;
        }
        catch (Exception exception)
        {
            Trace.TraceWarning("The startup catalog update check failed while the active cache remained available: {0}", exception);
            return StartupCatalogRefreshResult.Failed(presentation, exception);
        }

        if (updateResult.Status != EProductCatalogUpdateStatus.Staged)
        {
            return StartupCatalogRefreshResult.Completed(presentation, updateResult);
        }

        try
        {
            ProductWorkspacePresentation refreshedPresentation = await mWorkspaceLoader.LoadAsync(loadCancellationSource.Token);
            presentation.Workspace.Dispose();
            return StartupCatalogRefreshResult.Completed(refreshedPresentation);
        }
        catch (OperationCanceledException)
            when (loadCancellationSource.IsCancellationRequested)
        {
            presentation.Workspace.Dispose();
            throw;
        }
        catch (Exception exception)
        {
            Trace.TraceWarning("The staged catalog could not be applied during startup and will be retried on the next launch: {0}", exception);
            return StartupCatalogRefreshResult.Completed(presentation, updateResult);
        }
    }

    private void finishStartupCatalogRefresh(StartupCatalogRefreshResult refreshResult)
    {
        if (refreshResult.CompletedUpdateOrNull != null)
        {
            applyCatalogUpdateResult(refreshResult.CompletedUpdateOrNull);
        }
        else if (refreshResult.CompletedFailureOrNull != null)
        {
            applyCatalogUpdateFailure(refreshResult.CompletedFailureOrNull);
        }

        if (refreshResult.PendingUpdateOrNull == null)
        {
            mCatalogUpdateTask = Task.CompletedTask;
        }
    }

    private void observePendingCatalogUpdate(StartupCatalogRefreshResult refreshResult, CancellationTokenSource cancellationSource)
    {
        if (refreshResult.PendingUpdateOrNull == null)
        {
            return;
        }

        mCatalogUpdateTask = finishCatalogUpdateCheckAsync(refreshResult.PendingUpdateOrNull, cancellationSource);
    }

    private static async Task observeCatalogUpdateTaskAsync(Task<ProductCatalogUpdateResult> updateTask)
    {
        try
        {
            await updateTask.ConfigureAwait(false);
        }
        catch
        {
        }
    }

    private async Task finishCatalogUpdateCheckAsync(Task<ProductCatalogUpdateResult> updateTask, CancellationTokenSource cancellationSource)
    {
        try
        {
            ProductCatalogUpdateResult result = await updateTask.ConfigureAwait(false);
            if (cancellationSource.IsCancellationRequested)
            {
                return;
            }

            await Dispatcher.UIThread.InvokeAsync(
                delegate
                {
                    if (canApplyCatalogUpdateResult(cancellationSource))
                    {
                        applyCatalogUpdateResult(result);
                    }
                });
        }
        catch (OperationCanceledException)
            when (cancellationSource.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            Trace.TraceWarning("The background catalog update check failed while the active cache remained available: {0}", exception);
            string failureNotice = createCatalogUpdateFailureNotice(exception);
            if (string.IsNullOrEmpty(failureNotice) == false)
            {
                await Dispatcher.UIThread.InvokeAsync(
                    delegate
                    {
                        if (canApplyCatalogUpdateResult(cancellationSource))
                        {
                            setCatalogUpdateNotice(failureNotice);
                        }
                    });
            }
        }
    }

    private bool canApplyCatalogUpdateResult(CancellationTokenSource cancellationSource)
    {
        return mIsDisposed == false
            && cancellationSource.IsCancellationRequested == false
            && ReferenceEquals(mCatalogUpdateCancellationSource, cancellationSource);
    }

    private void applyCatalogUpdateResult(ProductCatalogUpdateResult result)
    {
        string notice;
        switch (result.Status)
        {
            case EProductCatalogUpdateStatus.Current:
            case EProductCatalogUpdateStatus.TransitionRejected:
                notice = string.Empty;
                break;
            case EProductCatalogUpdateStatus.Staged:
                notice = "새 과목 정보가 있습니다. 다음 실행 시 자동으로 적용됩니다.";
                break;
            case EProductCatalogUpdateStatus.WorkspaceIncompatible:
                notice = "새 과목 정보를 현재 시간표에 적용할 수 없어 기존 정보를 계속 사용합니다.";
                break;
            case EProductCatalogUpdateStatus.RevisionArtifactChanged:
                notice = "새 과목 정보를 안전하게 확인할 수 없어 기존 정보를 계속 사용합니다.";
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(result), result.Status, "Unknown product catalog update status.");
        }

        setCatalogUpdateNotice(notice);
    }

    private void applyCatalogUpdateFailure(Exception exception)
    {
        string notice = createCatalogUpdateFailureNotice(exception);
        if (string.IsNullOrEmpty(notice))
        {
            return;
        }

        setCatalogUpdateNotice(notice);
    }

    private static string createCatalogUpdateFailureNotice(Exception exception)
    {
        if (exception is CatalogCachePersistenceException)
        {
            return "새 과목 정보를 저장하지 못해 기존 정보를 계속 사용합니다.";
        }

        if (exception is RemoteCatalogSynchronizationException synchronizationException)
        {
            switch (synchronizationException.FailureKind)
            {
                case ERemoteCatalogSynchronizationFailureKind.InvalidRemoteData:
                case ERemoteCatalogSynchronizationFailureKind.ResourceLimit:
                case ERemoteCatalogSynchronizationFailureKind.SecurityPolicy:
                    return "새 과목 정보를 안전하게 확인할 수 없어 기존 정보를 계속 사용합니다.";
                case ERemoteCatalogSynchronizationFailureKind.LocalPersistence:
                    return "새 과목 정보를 저장하지 못해 기존 정보를 계속 사용합니다.";
                case ERemoteCatalogSynchronizationFailureKind.Network:
                    return string.Empty;
                default:
                    throw new ArgumentOutOfRangeException(nameof(exception), synchronizationException.FailureKind, "Unknown remote catalog synchronization failure kind.");
            }
        }

        return string.Empty;
    }

    private void setCatalogUpdateNotice(string notice)
    {
        mCatalogUpdateNotice = notice;
        raiseProductNoticePropertiesChanged();
    }

    private static string createStartupRecoveryNotice(EProductWorkspaceRecoveryFlags recoveryFlags)
    {
        EProductWorkspaceRecoveryFlags noticeFlags = recoveryFlags & RECOVERY_NOTICE_FLAGS;
        switch (noticeFlags)
        {
            case EProductWorkspaceRecoveryFlags.None:
                return string.Empty;
            case EProductWorkspaceRecoveryFlags.CatalogPreviousGeneration:
                return "최신 과목 데이터 저장본을 열 수 없어 이전에 검증한 데이터로 시작했습니다. 다음 실행 때 다시 확인합니다.";
            case EProductWorkspaceRecoveryFlags.WorkspacePreviousGeneration:
                return "최근 시간표 저장본을 열 수 없어 이전 안전 저장본을 복구했습니다. 최근 변경 일부가 보이지 않을 수 있습니다.";
            case EProductWorkspaceRecoveryFlags.CatalogPreviousGeneration
                | EProductWorkspaceRecoveryFlags.WorkspacePreviousGeneration:
                return "최신 과목 데이터와 최근 시간표 저장본을 열 수 없어 이전 안전 저장본으로 복구했습니다. 최근 변경 일부가 보이지 않을 수 있습니다.";
            default:
                Debug.Fail("Unexpected recovery notice flags: " + noticeFlags);
                return "이전 안전 저장본으로 복구했습니다. 최근 변경 일부가 보이지 않을 수 있습니다.";
        }
    }

    private void raiseProductNoticePropertiesChanged()
    {
        raisePropertyChanged(nameof(HasCatalogUpdateNotice));
        raisePropertyChanged(nameof(CatalogUpdateNotice));
        raisePropertyChanged(nameof(HasStartupRecoveryNotice));
        raisePropertyChanged(nameof(StartupRecoveryNotice));
        raisePropertyChanged(nameof(HasProductNotice));
        raisePropertyChanged(nameof(ProductNotice));
    }

    private sealed class StartupCatalogRefreshResult
    {
        public ProductWorkspacePresentation Presentation { get; }

        public ProductCatalogUpdateResult? CompletedUpdateOrNull { get; }

        public Exception? CompletedFailureOrNull { get; }

        public Task<ProductCatalogUpdateResult>? PendingUpdateOrNull { get; }

        private StartupCatalogRefreshResult(ProductWorkspacePresentation presentation, ProductCatalogUpdateResult? completedUpdateOrNull, Exception? completedFailureOrNull, Task<ProductCatalogUpdateResult>? pendingUpdateOrNull)
        {
            Presentation = presentation;
            CompletedUpdateOrNull = completedUpdateOrNull;
            CompletedFailureOrNull = completedFailureOrNull;
            PendingUpdateOrNull = pendingUpdateOrNull;
        }

        public static StartupCatalogRefreshResult Completed(ProductWorkspacePresentation presentation, ProductCatalogUpdateResult? completedUpdateOrNull = null)
        {
            return new StartupCatalogRefreshResult(presentation, completedUpdateOrNull, null, null);
        }

        public static StartupCatalogRefreshResult Failed(ProductWorkspacePresentation presentation, Exception completedFailure)
        {
            return new StartupCatalogRefreshResult(presentation, null, completedFailure, null);
        }

        public static StartupCatalogRefreshResult Pending(ProductWorkspacePresentation presentation, Task<ProductCatalogUpdateResult> pendingUpdate)
        {
            return new StartupCatalogRefreshResult(presentation, null, null, pendingUpdate);
        }
    }
}
