using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace eScope.Command
{
    public static class commandMgr
    {
        private static readonly Stack<ICommand> _undoStack = new();
        private static readonly Stack<ICommand> _redoStack = new();

        public static event EventHandler? StateChanged;

        public static bool CanUndo => _undoStack.Count > 0;
        public static bool CanRedo => _redoStack.Count > 0;

        public static string UndoLabel =>
            CanUndo ? $"Undo {_undoStack.Peek().Description}" : "Undo";

        public static string RedoLabel =>
            CanRedo ? $"Redo {_redoStack.Peek().Description}" : "Redo";

        public static void ExecuteCommand(ICommand command)
        {
            command.Execute();
            _undoStack.Push(command);
            _redoStack.Clear();
            OnStateChanged();
        }
        public static void Undo()
        {
            if (_undoStack.Count == 0) return;

            var command = _undoStack.Pop();
            command.Undo();
            _redoStack.Push(command);
            OnStateChanged();
        }

        public static void Redo()
        {
            if (_redoStack.Count == 0) return;

            var command = _redoStack.Pop();
            command.Execute();
            _undoStack.Push(command);
            OnStateChanged();
        }
        // drop history, e.g. when a trace referenced by stacked commands is closed
        public static void Clear()
        {
            _undoStack.Clear();
            _redoStack.Clear();
            OnStateChanged();
        }
        private static void OnStateChanged()
        => StateChanged?.Invoke(null, EventArgs.Empty);        
    }
    public class CompositeCommand : ICommand
    {
        public string Description { get; } = "Composite";

        private readonly List<ICommand> _commands = new();

        public CompositeCommand() { }
        public CompositeCommand(string description) => Description = description;

        public void Add(ICommand command)
            => _commands.Add(command);

        public void Execute()
        {
            foreach (var cmd in _commands)
                cmd.Execute();
        }

        public void Undo()
        {
            for (int i = _commands.Count - 1; i >= 0; i--)
                _commands[i].Undo();
        }
    }

}
