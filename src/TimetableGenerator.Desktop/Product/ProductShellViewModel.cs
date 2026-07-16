using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using TimetableGenerator.Application.Planning;
using TimetableGenerator.Desktop.Configuration;
using TimetableGenerator.Desktop.Presentation;
using TimetableGenerator.Desktop.Presentation.ViewModels;
using TimetableGenerator.Desktop.Product.Loading;
using TimetableGenerator.Infrastructure.Catalogs;
using TimetableGenerator.Infrastructure.Persistence;

namespace TimetableGenerator.Desktop.Product;

internal sealed class ProductShellViewModel : ObservableObject, IDisposable
{
    private readonly IProductWorkspaceLoader mWorkspaceLoader;

    private readonly AsyncDelegateCommand mRetryCommand;

    private readonly DelegateCommand mDismissShutdownErrorCommand;

    private CancellationTokenSource mLoadCancellationSource;

    private EProductShellState mState;

    private PlannerWorkspaceViewModel? mWorkspaceOrNull;

    private string mStatusTitle;

    private string mStatusMessage;

    private EShutdownPresentationState mShutdownState;

    private string mShutdownTitle;

    private string mShutdownMessage;

    private bool mIsDisposed;

    public EProductShellState State
    {
        get
        {
            return mState;
        }
    }

    public PlannerWorkspaceViewModel? WorkspaceOrNull
    {
        get
        {
            return mWorkspaceOrNull;
        }
    }

    public string StatusTitle
    {
        get
        {
            return mStatusTitle;
        }
    }

    public string StatusMessage
    {
        get
        {
            return mStatusMessage;
        }
    }

    public bool IsLoading
    {
        get
        {
            return State == EProductShellState.Loading;
        }
    }

    public bool IsReady
    {
        get
        {
            return State == EProductShellState.Ready;
        }
    }

    public bool HasError
    {
        get
        {
            return State == EProductShellState.Error;
        }
    }

    public bool IsStartupVisible
    {
        get
        {
            return IsReady == false;
        }
    }

    public string AccessibleWindowName
    {
        get
        {
            if (mWorkspaceOrNull == null)
            {
                return "시간표";
            }

            return mWorkspaceOrNull.InstitutionName + " 시간표 계획";
        }
    }

    public bool IsProductInteractionEnabled
    {
        get
        {
            return mShutdownState != EShutdownPresentationState.Saving;
        }
    }

    public bool IsShutdownOverlayVisible
    {
        get
        {
            return mShutdownState != EShutdownPresentationState.Idle;
        }
    }

    public bool IsShutdownInProgress
    {
        get
        {
            return mShutdownState == EShutdownPresentationState.Saving;
        }
    }

    public bool HasShutdownError
    {
        get
        {
            return mShutdownState == EShutdownPresentationState.Failed;
        }
    }

    public string ShutdownTitle
    {
        get
        {
            return mShutdownTitle;
        }
    }

    public string ShutdownMessage
    {
        get
        {
            return mShutdownMessage;
        }
    }

    public ICommand RetryCommand
    {
        get
        {
            return mRetryCommand;
        }
    }

    public ICommand DismissShutdownErrorCommand
    {
        get
        {
            return mDismissShutdownErrorCommand;
        }
    }

    public ProductShellViewModel(IProductWorkspaceLoader workspaceLoader)
    {
        if (workspaceLoader == null)
        {
            throw new ArgumentNullException(nameof(workspaceLoader));
        }

        mWorkspaceLoader = workspaceLoader;
        mLoadCancellationSource = new CancellationTokenSource();
        mState = EProductShellState.Loading;
        mStatusTitle = "시간표를 준비하고 있어요";
        mStatusMessage = "과목 데이터와 저장된 계획을 안전하게 확인하는 중입니다.";
        mShutdownState = EShutdownPresentationState.Idle;
        mShutdownTitle = string.Empty;
        mShutdownMessage = string.Empty;
        mRetryCommand = new AsyncDelegateCommand(StartAsync, showUnexpectedFailure);
        mDismissShutdownErrorCommand = new DelegateCommand(dismissShutdownError);
    }

    public async Task StartAsync()
    {
        throwIfDisposed();
        mLoadCancellationSource.Cancel();
        mLoadCancellationSource.Dispose();
        CancellationTokenSource loadCancellationSource =
            new CancellationTokenSource();
        mLoadCancellationSource = loadCancellationSource;
        setLoadingState();

        try
        {
            PlannerWorkspaceViewModel workspace = await mWorkspaceLoader.LoadAsync(
                loadCancellationSource.Token);
            if (loadCancellationSource.IsCancellationRequested
                || ReferenceEquals(mLoadCancellationSource, loadCancellationSource) == false)
            {
                workspace.Dispose();
                return;
            }

            mWorkspaceOrNull = workspace;
            mState = EProductShellState.Ready;
            raiseStatePropertiesChanged();
            raisePropertyChanged(nameof(WorkspaceOrNull));
            raisePropertyChanged(nameof(AccessibleWindowName));
        }
        catch (OperationCanceledException)
            when (loadCancellationSource.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            if (loadCancellationSource.IsCancellationRequested == false
                && ReferenceEquals(
                    mLoadCancellationSource,
                    loadCancellationSource))
            {
                showFailure(exception);
            }
        }
    }

    public void Dispose()
    {
        if (mIsDisposed)
        {
            return;
        }

        mIsDisposed = true;
        mLoadCancellationSource.Cancel();
        mLoadCancellationSource.Dispose();
        if (mWorkspaceOrNull != null)
        {
            mWorkspaceOrNull.Dispose();
        }
    }

    public Task FlushAutosaveAsync(CancellationToken cancellationToken)
    {
        if (mWorkspaceOrNull == null)
        {
            return Task.CompletedTask;
        }

        return mWorkspaceOrNull.FlushAutosaveAsync(cancellationToken);
    }

    public Task CompleteAutosaveAsync(CancellationToken cancellationToken)
    {
        if (mWorkspaceOrNull == null)
        {
            return Task.CompletedTask;
        }

        return mWorkspaceOrNull.CompleteAutosaveAsync(cancellationToken);
    }

    internal void beginShutdown()
    {
        throwIfDisposed();
        mShutdownState = EShutdownPresentationState.Saving;
        mShutdownTitle = "변경 사항을 저장하고 있어요";
        mShutdownMessage = "안전하게 저장한 뒤 창을 닫습니다.";
        raiseShutdownPropertiesChanged();
    }

    internal void showShutdownFailure(Exception exception)
    {
        if (exception == null)
        {
            throw new ArgumentNullException(nameof(exception));
        }

        throwIfDisposed();
        mShutdownState = EShutdownPresentationState.Failed;
        mShutdownTitle = "저장하지 못해 창을 닫지 않았어요";
        if (exception is OperationCanceledException)
        {
            mShutdownMessage =
                "저장이 예상보다 오래 걸렸습니다. 저장 상태를 확인한 뒤 다시 닫아 주세요.";
        }
        else
        {
            mShutdownMessage =
                "계획은 화면에 그대로 남아 있습니다. 저장 오류를 해결한 뒤 다시 시도해 주세요.";
        }

        raiseShutdownPropertiesChanged();
    }

    private void setLoadingState()
    {
        if (mWorkspaceOrNull != null)
        {
            mWorkspaceOrNull.Dispose();
        }

        mWorkspaceOrNull = null;
        mState = EProductShellState.Loading;
        mStatusTitle = "시간표를 준비하고 있어요";
        mStatusMessage = "과목 데이터와 저장된 계획을 안전하게 확인하는 중입니다.";
        raiseStatePropertiesChanged();
        raisePropertyChanged(nameof(WorkspaceOrNull));
        raisePropertyChanged(nameof(AccessibleWindowName));
        raisePropertyChanged(nameof(StatusTitle));
        raisePropertyChanged(nameof(StatusMessage));
    }

    private void showFailure(Exception exception)
    {
        mWorkspaceOrNull = null;
        mState = EProductShellState.Error;
        mStatusTitle = "과목 데이터를 불러오지 못했어요";
        mStatusMessage = findFailureMessage(exception);
        raiseStatePropertiesChanged();
        raisePropertyChanged(nameof(WorkspaceOrNull));
        raisePropertyChanged(nameof(StatusTitle));
        raisePropertyChanged(nameof(StatusMessage));
    }

    private void showUnexpectedFailure(Exception exception)
    {
        showFailure(exception);
    }

    private void dismissShutdownError()
    {
        if (mShutdownState != EShutdownPresentationState.Failed)
        {
            return;
        }

        mShutdownState = EShutdownPresentationState.Idle;
        mShutdownTitle = string.Empty;
        mShutdownMessage = string.Empty;
        raiseShutdownPropertiesChanged();
    }

    private static string findFailureMessage(Exception exception)
    {
        if (exception is CatalogSourceConfigurationException)
        {
            return "이 설치본에 과목 데이터 주소가 설정되지 않았습니다. 배포 설정을 확인한 뒤 다시 시도해 주세요.";
        }

        RemoteCatalogSynchronizationException? synchronizationExceptionOrNull =
            exception as RemoteCatalogSynchronizationException;
        if (synchronizationExceptionOrNull != null)
        {
            switch (synchronizationExceptionOrNull.FailureKind)
            {
                case ERemoteCatalogSynchronizationFailureKind.Network:
                    return "학교 과목 데이터 서버에 연결할 수 없습니다. 인터넷 연결을 확인한 뒤 다시 시도해 주세요.";
                case ERemoteCatalogSynchronizationFailureKind.LocalPersistence:
                    return "검증한 과목 데이터를 이 기기에 저장하지 못했습니다. 폴더 권한과 남은 용량을 확인해 주세요.";
                case ERemoteCatalogSynchronizationFailureKind.InvalidRemoteData:
                case ERemoteCatalogSynchronizationFailureKind.ResourceLimit:
                case ERemoteCatalogSynchronizationFailureKind.SecurityPolicy:
                    return "서버의 과목 데이터를 안전하게 검증할 수 없습니다. 기존 데이터는 변경하지 않았습니다.";
                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(exception),
                        synchronizationExceptionOrNull.FailureKind,
                        "Unknown remote catalog synchronization failure kind.");
            }
        }

        if (exception is ProductWorkspaceCatalogCompatibilityException)
        {
            return "저장된 계획을 현재 과목 데이터와 안전하게 연결할 수 없습니다. 기존 계획은 변경하지 않았습니다.";
        }

        if (exception is CatalogCacheUpgradeRequiredException
            || exception is PlanningWorkspaceUpgradeRequiredException)
        {
            return "더 새로운 버전에서 저장한 데이터입니다. 앱을 업데이트한 뒤 다시 열어 주세요.";
        }

        if (exception is CatalogCachePersistenceException
            || exception is WorkspacePersistenceException)
        {
            return "이 기기의 저장 공간에 접근할 수 없습니다. 폴더 권한과 남은 용량을 확인한 뒤 다시 시도해 주세요.";
        }

        return "저장된 데이터가 손상되었거나 접근할 수 없습니다. 잠시 후 다시 시도해 주세요.";
    }

    private void raiseStatePropertiesChanged()
    {
        raisePropertyChanged(nameof(State));
        raisePropertyChanged(nameof(IsLoading));
        raisePropertyChanged(nameof(IsReady));
        raisePropertyChanged(nameof(HasError));
        raisePropertyChanged(nameof(IsStartupVisible));
    }

    private void raiseShutdownPropertiesChanged()
    {
        raisePropertyChanged(nameof(IsProductInteractionEnabled));
        raisePropertyChanged(nameof(IsShutdownOverlayVisible));
        raisePropertyChanged(nameof(IsShutdownInProgress));
        raisePropertyChanged(nameof(HasShutdownError));
        raisePropertyChanged(nameof(ShutdownTitle));
        raisePropertyChanged(nameof(ShutdownMessage));
    }

    private void throwIfDisposed()
    {
        if (mIsDisposed)
        {
            throw new ObjectDisposedException(nameof(ProductShellViewModel));
        }
    }
}
