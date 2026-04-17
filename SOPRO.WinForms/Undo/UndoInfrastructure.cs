using System;
using System.Collections.Generic;

namespace SOPRO.WinForms.Undo
{
    internal interface IUndoableAction
    {
        string Description { get; }
        void Undo();
        void Redo();
    }

    internal sealed class DelegateUndoableAction : IUndoableAction
    {
        private readonly Action _undo;
        private readonly Action _redo;

        public DelegateUndoableAction(string description, Action undo, Action redo)
        {
            Description = description ?? string.Empty;
            _undo = undo ?? throw new ArgumentNullException(nameof(undo));
            _redo = redo ?? throw new ArgumentNullException(nameof(redo));
        }

        public string Description { get; }
        public void Undo() => _undo();
        public void Redo() => _redo();
    }

    internal sealed class UndoManager
    {
        private readonly Stack<IUndoableAction> _undoStack = new();
        private readonly Stack<IUndoableAction> _redoStack = new();

        public bool CanUndo => _undoStack.Count > 0;
        public bool CanRedo => _redoStack.Count > 0;

        public void Push(IUndoableAction action)
        {
            if (action == null) throw new ArgumentNullException(nameof(action));
            _undoStack.Push(action);
            _redoStack.Clear();
        }

        public bool Undo()
        {
            if (_undoStack.Count == 0) return false;
            var action = _undoStack.Pop();
            action.Undo();
            _redoStack.Push(action);
            return true;
        }

        public bool Redo()
        {
            if (_redoStack.Count == 0) return false;
            var action = _redoStack.Pop();
            action.Redo();
            _undoStack.Push(action);
            return true;
        }

        public void Clear()
        {
            _undoStack.Clear();
            _redoStack.Clear();
        }
    }
}
