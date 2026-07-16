using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;

using TimetableGenerator.Desktop.Presentation;
using TimetableGenerator.Desktop.Presentation.ViewModels;
using TimetableGenerator.Desktop.Product.CatalogUpdates;
using TimetableGenerator.Desktop.Product.Loading;

namespace TimetableGenerator.Desktop.Product;

internal sealed partial class ProductShellViewModel : ObservableObject, IDisposable
{
    private readonly IProductWorkspaceLoader mWorkspaceLoader;

    private readonly AsyncDelegateCommand mRetryCommand;

    private CancellationTokenSource mLoadCancellationSource;

    private EProductShellState mState;

    private PlannerWorkspaceViewModel? mWorkspaceOrNull;

    private string mStatusTitle;

    private string mStatusMessage;

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

    public ICommand RetryCommand
    {
        get
        {
            return mRetryCommand;
        }
    }

    public ProductShellViewModel(
        IProductWorkspaceLoader workspaceLoader,
        IProductCatalogUpdateService catalogUpdateService)
    {
        if (workspaceLoader == null)
        {
            throw new ArgumentNullException(nameof(workspaceLoader));
        }

        if (catalogUpdateService == null)
        {
            throw new ArgumentNullException(nameof(catalogUpdateService));
        }

        mWorkspaceLoader = workspaceLoader;
        mCatalogUpdateService = catalogUpdateService;
        mLoadCancellationSource = new CancellationTokenSource();
        mCatalogUpdateCancellationSource = new CancellationTokenSource();
        mCatalogUpdateTask = Task.CompletedTask;
        mState = EProductShellState.Loading;
        mStatusTitle = "시간표를 준비하고 있어요";
        mStatusMessage = "과목 데이터와 저장된 계획을 안전하게 확인하는 중입니다.";
        mShutdownState = EShutdownPresentationState.Idle;
        mShutdownTitle = string.Empty;
        mShutdownMessage = string.Empty;
        mCatalogUpdateNotice = string.Empty;
        mStartupRecoveryFlags = EProductWorkspaceRecoveryFlags.None;
        mRetryCommand = new AsyncDelegateCommand(StartAsync, showUnexpectedFailure);
        mDismissShutdownErrorCommand = new DelegateCommand(dismissShutdownError);
        mDismissProductNoticeCommand = new DelegateCommand(dismissProductNotice);
    }

    public async Task StartAsync()
    {
        throwIfDisposed();
        mLoadCancellationSource.Cancel();
        mLoadCancellationSource.Dispose();
        CancellationTokenSource loadCancellationSource =
            new CancellationTokenSource();
        mLoadCancellationSource = loadCancellationSource;
        mCatalogUpdateCancellationSource.Cancel();
        mCatalogUpdateCancellationSource.Dispose();
        CancellationTokenSource catalogUpdateCancellationSource =
            new CancellationTokenSource();
        mCatalogUpdateCancellationSource = catalogUpdateCancellationSource;
        setLoadingState();

        try
        {
            ProductWorkspacePresentation presentation =
                await mWorkspaceLoader.LoadAsync(loadCancellationSource.Token);
            PlannerWorkspaceViewModel workspace = presentation.Workspace;
            if (loadCancellationSource.IsCancellationRequested
                || ReferenceEquals(mLoadCancellationSource, loadCancellationSource) == false)
            {
                workspace.Dispose();
                return;
            }

            mWorkspaceOrNull = workspace;
            mStartupRecoveryFlags = presentation.RecoveryFlags;
            mState = EProductShellState.Ready;
            raiseStatePropertiesChanged();
            raisePropertyChanged(nameof(WorkspaceOrNull));
            raisePropertyChanged(nameof(AccessibleWindowName));
            raiseProductNoticePropertiesChanged();
            startCatalogUpdateCheck(
                presentation,
                catalogUpdateCancellationSource);
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
        mCatalogUpdateCancellationSource.Cancel();
        mCatalogUpdateCancellationSource.Dispose();
        if (mWorkspaceOrNull != null)
        {
            mWorkspaceOrNull.Dispose();
        }
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
        mCatalogUpdateNotice = string.Empty;
        mStartupRecoveryFlags = EProductWorkspaceRecoveryFlags.None;
        raiseStatePropertiesChanged();
        raisePropertyChanged(nameof(WorkspaceOrNull));
        raisePropertyChanged(nameof(AccessibleWindowName));
        raisePropertyChanged(nameof(StatusTitle));
        raisePropertyChanged(nameof(StatusMessage));
        raiseProductNoticePropertiesChanged();
    }

    private void raiseStatePropertiesChanged()
    {
        raisePropertyChanged(nameof(State));
        raisePropertyChanged(nameof(IsLoading));
        raisePropertyChanged(nameof(IsReady));
        raisePropertyChanged(nameof(HasError));
        raisePropertyChanged(nameof(IsStartupVisible));
    }

    private void throwIfDisposed()
    {
        if (mIsDisposed)
        {
            throw new ObjectDisposedException(nameof(ProductShellViewModel));
        }
    }
}
