using System;
using System.Diagnostics;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;

using Avalonia.Threading;

using TimetableGenerator.Desktop.Presentation;
using TimetableGenerator.Desktop.Product.CatalogUpdates;
using TimetableGenerator.Desktop.Product.Loading;

namespace TimetableGenerator.Desktop.Product;

internal sealed partial class ProductShellViewModel
{
    private const EProductWorkspaceRecoveryFlags RECOVERY_NOTICE_FLAGS =
        EProductWorkspaceRecoveryFlags.CatalogPreviousGeneration
        | EProductWorkspaceRecoveryFlags.WorkspacePreviousGeneration;

    private readonly IProductCatalogUpdateService mCatalogUpdateService;

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
            return (mStartupRecoveryFlags & RECOVERY_NOTICE_FLAGS)
                != EProductWorkspaceRecoveryFlags.None;
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

    private void startCatalogUpdateCheck(
        ProductWorkspacePresentation presentation,
        CancellationTokenSource cancellationSource)
    {
        if (presentation.CatalogOrigin == EProductCatalogOrigin.RemoteDownload)
        {
            mCatalogUpdateTask = Task.CompletedTask;
            return;
        }

        mCatalogUpdateTask = checkCatalogUpdateAsync(
            presentation,
            cancellationSource);
    }

    private async Task checkCatalogUpdateAsync(
        ProductWorkspacePresentation presentation,
        CancellationTokenSource cancellationSource)
    {
        try
        {
            ProductCatalogUpdateResult result =
                await mCatalogUpdateService.CheckAndStageAsync(
                    presentation.ActiveCatalogPackage,
                    presentation.WorkspaceSnapshot,
                    cancellationSource.Token).ConfigureAwait(false);
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
            Trace.TraceWarning(
                "The background catalog update check failed while the active cache remained available: {0}",
                exception);
        }
    }

    private bool canApplyCatalogUpdateResult(
        CancellationTokenSource cancellationSource)
    {
        return mIsDisposed == false
            && cancellationSource.IsCancellationRequested == false
            && ReferenceEquals(
                mCatalogUpdateCancellationSource,
                cancellationSource);
    }

    private void applyCatalogUpdateResult(ProductCatalogUpdateResult result)
    {
        switch (result.Status)
        {
            case EProductCatalogUpdateStatus.Current:
            case EProductCatalogUpdateStatus.TransitionRejected:
                mCatalogUpdateNotice = string.Empty;
                break;
            case EProductCatalogUpdateStatus.Staged:
                mCatalogUpdateNotice = "과목 데이터 r"
                    + result.CandidateRevision.Value.ToString(
                        "D4",
                        CultureInfo.InvariantCulture)
                    + " 준비됨: 다음 실행에서 확인 후 적용";
                break;
            case EProductCatalogUpdateStatus.WorkspaceIncompatible:
                mCatalogUpdateNotice =
                    "새 과목 데이터가 현재 시간표와 맞지 않아 기존 버전을 유지합니다.";
                break;
            case EProductCatalogUpdateStatus.RevisionArtifactChanged:
                mCatalogUpdateNotice =
                    "같은 버전의 서버 데이터가 변경되어 안전을 위해 무시했습니다.";
                break;
            default:
                throw new ArgumentOutOfRangeException(
                    nameof(result),
                    result.Status,
                    "Unknown product catalog update status.");
        }

        raiseProductNoticePropertiesChanged();
    }

    private static string createStartupRecoveryNotice(
        EProductWorkspaceRecoveryFlags recoveryFlags)
    {
        EProductWorkspaceRecoveryFlags noticeFlags =
            recoveryFlags & RECOVERY_NOTICE_FLAGS;
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
}
