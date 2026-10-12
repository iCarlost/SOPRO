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

    /// <summary>
    /// Cambio de estado de un único destino (p. ej. una celda) dentro de una
    /// acción compuesta: el estado <see cref="Before"/> y el estado
    /// <see cref="After"/>. Es agnóstico: el significado concreto (formato,
    /// color, alineación…) lo aporta el consumidor a través de los callbacks.
    /// </summary>
    internal sealed class UndoChange
    {
        public UndoChange(object? before, object? after)
        {
            Before = before;
            After = after;
        }

        public object? Before { get; }
        public object? After { get; }
    }

    /// <summary>
    /// Acción de undo COMPUESTA: agrupa N cambios (antes/después) en UNA sola
    /// entrada del stack de <see cref="UndoManager"/>. La aplicación del estado
    /// "después" (Do/Redo) y "antes" (Undo) se delega a callbacks que el
    /// consumidor inyecta; esta clase NO realiza I/O ni conoce el servicio de
    /// persistencia (separación de responsabilidades). La atomicidad respecto a
    /// la persistencia la garantiza <see cref="UndoManager.Execute"/>: si el
    /// callback de aplicación lanza, la acción no entra al stack.
    /// </summary>
    internal sealed class CompositeUndoableAction : IUndoableAction
    {
        private readonly IReadOnlyList<UndoChange> _changes;
        private readonly Action<IReadOnlyList<UndoChange>> _applyBefore;
        private readonly Action<IReadOnlyList<UndoChange>> _applyAfter;

        public CompositeUndoableAction(
            string description,
            IReadOnlyList<UndoChange> changes,
            Action<IReadOnlyList<UndoChange>> applyBefore,
            Action<IReadOnlyList<UndoChange>> applyAfter)
        {
            Description = description ?? string.Empty;
            _changes = changes ?? throw new ArgumentNullException(nameof(changes));
            _applyBefore = applyBefore ?? throw new ArgumentNullException(nameof(applyBefore));
            _applyAfter = applyAfter ?? throw new ArgumentNullException(nameof(applyAfter));
        }

        public string Description { get; }

        public int ChangeCount => _changes.Count;

        public bool HasChanges => _changes.Count > 0;

        public IReadOnlyList<UndoChange> Changes => _changes;

        public void Undo() => _applyBefore(_changes);

        public void Redo() => _applyAfter(_changes);
    }

    internal sealed class UndoManager
    {
        private readonly Stack<IUndoableAction> _undoStack = new();
        private readonly Stack<IUndoableAction> _redoStack = new();

        public bool CanUndo => _undoStack.Count > 0;
        public bool CanRedo => _redoStack.Count > 0;

        /// <summary>Número de acciones apiladas para deshacer (solo lectura).</summary>
        public int UndoCount => _undoStack.Count;

        /// <summary>Número de acciones apiladas para rehacer (solo lectura).</summary>
        public int RedoCount => _redoStack.Count;

        public void Push(IUndoableAction action)
        {
            if (action == null) throw new ArgumentNullException(nameof(action));
            _undoStack.Push(action);
            _redoStack.Clear();
        }

        /// <summary>
        /// Aplica el estado "después" de la acción (Do) y, SOLO si la aplicación
        /// tiene éxito, la apila como UNA entrada. Si el callback de aplicación
        /// lanza (p. ej. falla el commit de persistencia), la excepción se
        /// propaga y el stack queda intacto: no hay actualización parcial.
        /// Una acción compuesta sin cambios no entra al stack (devuelve false).
        /// </summary>
        public bool Execute(IUndoableAction action)
        {
            if (action == null) throw new ArgumentNullException(nameof(action));

            if (action is CompositeUndoableAction compuesta && !compuesta.HasChanges)
                return false;

            // "Do": aplicar el estado después. Si lanza, nada se apila.
            action.Redo();

            _undoStack.Push(action);
            _redoStack.Clear();
            return true;
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
