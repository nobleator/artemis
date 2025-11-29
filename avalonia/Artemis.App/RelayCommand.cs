using System;
using System.Windows.Input;

namespace Artemis.App;

public sealed class RelayCommand : ICommand
{
    private readonly Action<object?> _exec;
    public RelayCommand(Action<object?> exec) => _exec = exec;

    public bool CanExecute(object? p) => true;
    public void Execute(object? p) => _exec(p);
    public event EventHandler? CanExecuteChanged;
}
