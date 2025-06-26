using System;
using System.Threading.Tasks;
using System.Windows.Input;

namespace Bubbles4.Commands;

public class AsyncRelayCommand<T> : ICommand
{
    private readonly Func<T, Task> _execute;
    private readonly Predicate<T>? _canExecute;

    public AsyncRelayCommand(Func<T, Task> execute, Predicate<T>? canExecute = null)
    {
        _execute = execute;
        _canExecute = canExecute;
    }

    public bool CanExecute(object? parameter) =>
        _canExecute == null || (parameter is T t && _canExecute(t));

    public async void Execute(object? parameter)
    {
        if (parameter is T t)
            await _execute(t);
    }

    public event EventHandler? CanExecuteChanged;
}
