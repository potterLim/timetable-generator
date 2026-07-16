using System;
using System.Windows.Input;

namespace TimetableGenerator.Desktop.Presentation;

internal sealed class ParameterizedCommand<T> : ICommand
    where T : class
{
    private readonly Action<T> mExecute;

    public event EventHandler? CanExecuteChanged;

    public ParameterizedCommand(Action<T> execute)
    {
        ArgumentNullException.ThrowIfNull(execute);
        mExecute = execute;
    }

    public bool CanExecute(object? parameterOrNull)
    {
        return parameterOrNull is T;
    }

    public void Execute(object? parameterOrNull)
    {
        T? typedParameterOrNull = parameterOrNull as T;
        if (typedParameterOrNull == null)
        {
            throw new ArgumentException("The command requires a strongly typed parameter.", nameof(parameterOrNull));
        }

        mExecute(typedParameterOrNull);
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
