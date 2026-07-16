using System;
using System.Threading.Tasks;
using System.Windows.Input;

namespace TimetableGenerator.Desktop.Presentation;

internal sealed class AsyncDelegateCommand : ICommand
{
    private readonly Func<Task> mExecuteAsync;

    private readonly Action<Exception> mFailureHandler;

    private bool mIsExecuting;

    private Task mExecutionTask;

    public event EventHandler? CanExecuteChanged;

    internal Task ExecutionTask
    {
        get
        {
            return mExecutionTask;
        }
    }

    public AsyncDelegateCommand(
        Func<Task> executeAsync,
        Action<Exception> failureHandler)
    {
        if (executeAsync == null)
        {
            throw new ArgumentNullException(nameof(executeAsync));
        }

        if (failureHandler == null)
        {
            throw new ArgumentNullException(nameof(failureHandler));
        }

        mExecuteAsync = executeAsync;
        mFailureHandler = failureHandler;
        mExecutionTask = Task.CompletedTask;
    }

    public bool CanExecute(object? parameterOrNull)
    {
        return mIsExecuting == false;
    }

    public void Execute(object? parameterOrNull)
    {
        if (CanExecute(parameterOrNull) == false)
        {
            return;
        }

        mIsExecuting = true;
        notifyCanExecuteChanged();
        mExecutionTask = executeAndObserveAsync();
    }

    private async Task executeAndObserveAsync()
    {
        try
        {
            await mExecuteAsync();
        }
        catch (Exception exception)
        {
            mFailureHandler(exception);
        }
        finally
        {
            mIsExecuting = false;
            notifyCanExecuteChanged();
        }
    }

    private void notifyCanExecuteChanged()
    {
        EventHandler? canExecuteChangedOrNull = CanExecuteChanged;
        if (canExecuteChangedOrNull != null)
        {
            canExecuteChangedOrNull(this, EventArgs.Empty);
        }
    }
}
