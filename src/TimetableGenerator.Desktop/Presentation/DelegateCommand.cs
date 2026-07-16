using System;
using System.Windows.Input;

namespace TimetableGenerator.Desktop.Presentation;

internal sealed class DelegateCommand : ICommand
{
    private readonly Action mExecute;
    private readonly Func<bool>? mCanExecuteOrNull;

    public event EventHandler? CanExecuteChanged;

    public DelegateCommand(Action execute)
        : this(execute, null)
    {
    }

    public DelegateCommand(Action execute, Func<bool>? canExecuteOrNull)
    {
        ArgumentNullException.ThrowIfNull(execute);

        mExecute = execute;
        mCanExecuteOrNull = canExecuteOrNull;
    }

    public bool CanExecute(object? parameterOrNull)
    {
        if (mCanExecuteOrNull == null)
        {
            return true;
        }

        return mCanExecuteOrNull();
    }

    public void Execute(object? parameterOrNull)
    {
        mExecute();
    }

    public void NotifyCanExecuteChanged()
    {
        EventHandler? canExecuteChangedOrNull = CanExecuteChanged;
        if (canExecuteChangedOrNull != null)
        {
            canExecuteChangedOrNull(this, EventArgs.Empty);
        }
    }
}
