using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;

using TimetableGenerator.Desktop.Presentation;

namespace TimetableGenerator.Desktop.Product;

internal sealed partial class ProductShellViewModel
{
    private readonly DelegateCommand mDismissShutdownErrorCommand;

    private EShutdownPresentationState mShutdownState;

    private string mShutdownTitle;

    private string mShutdownMessage;

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

    public ICommand DismissShutdownErrorCommand
    {
        get
        {
            return mDismissShutdownErrorCommand;
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

    private void raiseShutdownPropertiesChanged()
    {
        raisePropertyChanged(nameof(IsProductInteractionEnabled));
        raisePropertyChanged(nameof(IsShutdownOverlayVisible));
        raisePropertyChanged(nameof(IsShutdownInProgress));
        raisePropertyChanged(nameof(HasShutdownError));
        raisePropertyChanged(nameof(ShutdownTitle));
        raisePropertyChanged(nameof(ShutdownMessage));
    }
}
