using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

namespace Shiftapp_demo.FrameWork
{
    public sealed class RelayCommand : ICommand
    {
        private readonly Func<object?, Task>? _execAsync;
        private readonly Action<object?>? _exec;
        private readonly Func<object?, bool>? _can;

        public RelayCommand(Action<object?> exec, Func<object?, bool>? can = null)
        { _exec = exec; _can = can; }

        public RelayCommand(Func<object?, Task> execAsync, Func<object?, bool>? can = null)
        { _execAsync = execAsync; _can = can; }

        public bool CanExecute(object? parameter) => _can?.Invoke(parameter) ?? true;

        public async void Execute(object? parameter)
        {
            if (_execAsync != null) await _execAsync(parameter);
            else _exec?.Invoke(parameter);
        }

        public event EventHandler? CanExecuteChanged
        { add { CommandManager.RequerySuggested += value; } remove { CommandManager.RequerySuggested -= value; } }
    }
}
