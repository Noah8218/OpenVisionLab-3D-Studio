using System;
using System.Windows.Input;
using PresentationRelayCommand = OpenVisionLab.ThreeD.Presentation.Commands.RelayCommand;

namespace OpenVisionLab.ThreeD.Viewer;

public sealed class RelayCommand : ICommand
{
    private readonly PresentationRelayCommand inner;

    public RelayCommand(Action<object?> execute, Func<object?, bool>? canExecute = null)
    {
        inner = new PresentationRelayCommand(execute, canExecute);
    }

    public event EventHandler? CanExecuteChanged
    {
        add => inner.CanExecuteChanged += value;
        remove => inner.CanExecuteChanged -= value;
    }

    public bool CanExecute(object? parameter) => inner.CanExecute(parameter);

    public void Execute(object? parameter) => inner.Execute(parameter);

    public void RaiseCanExecuteChanged() => inner.RaiseCanExecuteChanged();
}
