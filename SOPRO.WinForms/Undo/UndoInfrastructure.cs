using System;
using System.Collections.Generic;
using System.Linq;

namespace SOPRO.WinForms.Undo
{
    /// <summary>
    /// Familia de una acción del historial. Las operaciones de PROGRAMA (edición de
    /// actividades/dependencias, comandos atómicos de sincronización/recálculo/borrado)
    /// invalidan el historial del programa; las de FORMATO (overlays de celda y estilos
    /// de fila) invalidan el historial de formato. Descartar una familia NUNCA altera
    /// el bloqueo por reconciliación ni el historial de la otra.
    /// </summary>
    internal enum UndoFamily
    {
        Programa,
        Formato
    }

    internal interface IUndoableAction
    {
        string Description { get; }
        UndoFamily Familia { get; }
        void Undo();
        void Redo();
    }

    internal sealed class DelegateUndoableAction : IUndoableAction
    {
        private readonly Action _undo;
        private readonly Action _redo;

        public DelegateUndoableAction(
            string description,
            Action undo,
            Action redo,
            UndoFamily familia = UndoFamily.Programa)
        {
            Description = description ?? string.Empty;
            _undo = undo ?? throw new ArgumentNullException(nameof(undo));
            _redo = redo ?? throw new ArgumentNullException(nameof(redo));
            Familia = familia;
        }

        public string Description { get; }
        public UndoFamily Familia { get; }
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
            Action<IReadOnlyList<UndoChange>> applyAfter,
            UndoFamily familia = UndoFamily.Formato)
        {
            Description = description ?? string.Empty;
            _changes = changes ?? throw new ArgumentNullException(nameof(changes));
            _applyBefore = applyBefore ?? throw new ArgumentNullException(nameof(applyBefore));
            _applyAfter = applyAfter ?? throw new ArgumentNullException(nameof(applyAfter));
            Familia = familia;
        }

        public string Description { get; }

        public UndoFamily Familia { get; }

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
        private bool _requiresReconciliation;

        /// <summary>
        /// Indica que la persistencia quedó en estado INCIERTO
        /// (<see cref="SOPRO.Application.Models.Presupuesto.BudgetOperationStatus.OutcomeUnknown"/>):
        /// el historial y las acciones de formato deben quedar bloqueados hasta una
        /// lectura fresca de la base. Nunca se revierte ni se reintenta solo.
        /// </summary>
        public bool RequiresReconciliation => _requiresReconciliation;

        public bool CanUndo => !_requiresReconciliation && _undoStack.Count > 0;
        public bool CanRedo => !_requiresReconciliation && _redoStack.Count > 0;

        /// <summary>Número de acciones apiladas para deshacer (solo lectura).</summary>
        public int UndoCount => _undoStack.Count;

        /// <summary>Número de acciones apiladas para rehacer (solo lectura).</summary>
        public int RedoCount => _redoStack.Count;

        /// <summary>
        /// Bloquea Undo/Redo/Execute tras un resultado incierto. NO borra el
        /// historial: la reconciliación explícita decide qué descartar.
        /// </summary>
        public void SuspendForReconciliation()
        {
            _requiresReconciliation = true;
        }

        /// <summary>
        /// Libera el bloqueo SOLO después de una lectura fresca de los overlays
        /// persistidos y descarta el historial, que ya no corresponde a ninguna
        /// base conocida. Es una acción explícita del consumidor.
        /// </summary>
        public void ReconciliarTrasLecturaFresca()
        {
            _undoStack.Clear();
            _redoStack.Clear();
            _requiresReconciliation = false;
        }

        public void Push(IUndoableAction action)
        {
            if (action == null) throw new ArgumentNullException(nameof(action));
            if (_requiresReconciliation)
                throw new InvalidOperationException(
                    "La persistencia está en estado incierto: recargue el formato antes de continuar.");

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
            if (_requiresReconciliation)
                throw new InvalidOperationException(
                    "La persistencia está en estado incierto: recargue el formato antes de continuar.");

            if (action is CompositeUndoableAction compuesta && !compuesta.HasChanges)
                return false;

            // "Do": aplicar el estado después. Si lanza, nada se apila.
            action.Redo();

            _undoStack.Push(action);
            _redoStack.Clear();
            return true;
        }

        /// <summary>
        /// Deshace la última acción con el invariante Peek → aplicar → Pop/Push:
        /// si el callback lanza, ni el stack de undo ni el de redo cambian (no se
        /// pierde la acción ni se finge que el cambio se revirtió).
        /// </summary>
        public bool Undo()
        {
            if (_requiresReconciliation)
                throw new InvalidOperationException(
                    "La persistencia está en estado incierto: recargue el formato antes de continuar.");

            if (_undoStack.Count == 0) return false;

            var action = _undoStack.Peek();
            action.Undo();
            _undoStack.Pop();
            _redoStack.Push(action);
            return true;
        }

        /// <summary>
        /// Rehace la última acción con el mismo invariante que <see cref="Undo"/>.
        /// </summary>
        public bool Redo()
        {
            if (_requiresReconciliation)
                throw new InvalidOperationException(
                    "La persistencia está en estado incierto: recargue el formato antes de continuar.");

            if (_redoStack.Count == 0) return false;

            var action = _redoStack.Peek();
            action.Redo();
            _redoStack.Pop();
            _undoStack.Push(action);
            return true;
        }

        public void Clear()
        {
            _undoStack.Clear();
            _redoStack.Clear();
        }

        /// <summary>
        /// Descarta SÓLO las acciones de <paramref name="familia"/> en undo y redo,
        /// conservando el resto del historial y SIN liberar
        /// <see cref="RequiresReconciliation"/>: descartar acciones obsoletas no es
        /// reconciliar con la base. El orden de las acciones conservadas no cambia.
        /// </summary>
        public void DescartarPorFamilia(UndoFamily familia)
        {
            DescartarFamilia(_undoStack, familia);
            DescartarFamilia(_redoStack, familia);
        }

        private static void DescartarFamilia(Stack<IUndoableAction> stack, UndoFamily familia)
        {
            if (stack.Count == 0) return;
            if (!stack.Any(a => a.Familia == familia)) return;

            // La enumeración de Stack va de tope a base: se reempuja al revés para
            // que el tope conserve el mismo orden relativo.
            var conservadas = stack.Where(a => a.Familia != familia).Reverse().ToList();
            stack.Clear();
            foreach (var accion in conservadas)
                stack.Push(accion);
        }
    }
}
