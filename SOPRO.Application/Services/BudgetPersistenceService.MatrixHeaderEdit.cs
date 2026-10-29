using SOPRO.Application.Models.Presupuesto;
using SOPRO.Core.Entities;
using SOPRO.Data.Context;
using SOPRO.Data.Factories;

namespace SOPRO.Application.Services
{
    public static partial class BudgetPersistenceService
    {
        /// <summary>
        /// Edita la cabecera de una matriz YA persistida del proyecto (ADR-0001)
        /// sobre un contexto NUEVO por operación: revalida la cabecera persistida
        /// EXACTA que la vista cargó, los campos requeridos y sus longitudes, el
        /// tipo, la clave duplicada dentro del proyecto y, solo si el tipo cambia,
        /// que la matriz no esté referenciada por un concepto ni como auxiliar de
        /// otro componente. Un único SaveChanges escribe Clave, Descripción,
        /// Unidad, Tipo y FechaModificacion; un conflicto, una validación o un fallo
        /// dejan la base sin cambios y el reintento del mismo comando no duplica
        /// nada. No recalcula componentes ni conceptos: la cabecera no cambia el
        /// costo directo.
        /// </summary>
        public static BudgetMatrixHeaderEditResult ExecuteMatrixHeaderEditCommand(
            IProjectDbContextFactory factory,
            string databasePath,
            BudgetMatrixHeaderEditCommand command)
        {
            ArgumentNullException.ThrowIfNull(factory);
            if (string.IsNullOrWhiteSpace(databasePath))
                throw new ArgumentException("La ruta de la base del proyecto es requerida.", nameof(databasePath));
            ArgumentNullException.ThrowIfNull(command);

            var execution = BudgetWriteExecutor.Execute(
                factory,
                databasePath,
                useTransaction: true,
                context => StageMatrixHeaderEdit(context, command));

            return new BudgetMatrixHeaderEditResult(
                execution.Result, execution.Confirmed, execution.Diagnostic);
        }

        private static BudgetWriteStep<BudgetMatrixHeaderEditSnapshot> StageMatrixHeaderEdit(
            SOPROContext context,
            BudgetMatrixHeaderEditCommand command)
        {
            var validationMessage = ValidateHeaderEdit(command);
            if (validationMessage != null)
                return BudgetWriteStep<BudgetMatrixHeaderEditSnapshot>.Abort(
                    BudgetOperationResult.Failed(validationMessage));

            // Se carga TRACKEADA: la instancia que valida la base esperada es la
            // misma que viaja en el único SaveChanges.
            var matriz = context.Matrices
                .FirstOrDefault(m => m.Id == command.MatrixId);

            if (matriz == null)
                return BudgetWriteStep<BudgetMatrixHeaderEditSnapshot>.Abort(
                    BudgetOperationResult.Conflict(
                        $"La matriz {command.MatrixId} ya no existe en la base; la vista debe recargarse."));

            if (matriz.ProyectoId != command.ProjectId)
                return BudgetWriteStep<BudgetMatrixHeaderEditSnapshot>.Abort(
                    BudgetOperationResult.Conflict(
                        $"La matriz {command.MatrixId} no pertenece al proyecto {command.ProjectId} " +
                        "en la base; la vista debe recargarse."));

            if (!MatchesHeaderBase(matriz, command.ExpectedMatrix))
                return BudgetWriteStep<BudgetMatrixHeaderEditSnapshot>.Abort(
                    BudgetOperationResult.Conflict(
                        $"La cabecera de la matriz {command.MatrixId} cambió en la base desde que se cargó; " +
                        "la vista debe recargarse."));

            // Igualdad EXACTA como la del índice único (ProyectoId, Clave) de SQLite:
            // una clave legacy con otro casing no se declara duplicada aquí; el índice
            // sigue protegiendo la carrera y el ejecutor clasifica ese fallo como
            // revertido.
            bool claveDuplicada = context.Matrices.Any(m =>
                m.ProyectoId == command.ProjectId
                && m.Id != matriz.Id
                && m.Clave == command.ProposedClave);

            if (claveDuplicada)
                return BudgetWriteStep<BudgetMatrixHeaderEditSnapshot>.Abort(
                    BudgetOperationResult.Conflict(
                        $"Ya existe una matriz con la clave {command.ProposedClave} en el proyecto."));

            if (command.ProposedTipo != matriz.Tipo)
            {
                bool referenciada = context.ConceptosPresupuesto.Any(c => c.MatrizId == matriz.Id)
                    || context.ComponentesMatriz.Any(c => c.AuxiliarId == matriz.Id);

                if (referenciada)
                    return BudgetWriteStep<BudgetMatrixHeaderEditSnapshot>.Abort(
                        BudgetOperationResult.Failed(
                            $"No se puede cambiar el tipo de la matriz {matriz.Id}: está referenciada por " +
                            "un concepto o por un componente auxiliar. Elimine primero esas referencias."));
            }

            matriz.Clave = command.ProposedClave;
            matriz.Descripcion = command.ProposedDescripcion;
            matriz.Unidad = command.ProposedUnidad;
            matriz.Tipo = command.ProposedTipo;
            matriz.FechaModificacion = DateTime.Now;

            var snapshot = new BudgetMatrixHeaderEditSnapshot(
                command.ProjectId,
                matriz.Id,
                matriz.Clave,
                matriz.Descripcion,
                matriz.Unidad,
                matriz.Tipo,
                matriz.FechaModificacion);

            return BudgetWriteStep<BudgetMatrixHeaderEditSnapshot>.Commit(
                snapshot, Array.Empty<int>());
        }

        private static string? ValidateHeaderEdit(BudgetMatrixHeaderEditCommand command)
        {
            if (string.IsNullOrWhiteSpace(command.ProposedClave)
                || string.IsNullOrWhiteSpace(command.ProposedDescripcion)
                || string.IsNullOrWhiteSpace(command.ProposedUnidad))
                return "Clave, Descripción y Unidad son obligatorios.";

            if (command.ProposedClave.Length > 50)
                return "La clave no puede exceder 50 caracteres.";

            if (command.ProposedDescripcion.Length > 2000)
                return "La descripción no puede exceder 2000 caracteres.";

            if (command.ProposedUnidad.Length > 20)
                return "La unidad no puede exceder 20 caracteres.";

            if (!Enum.IsDefined(typeof(TipoMatriz), command.ProposedTipo))
                return "El tipo de matriz no es válido.";

            return null;
        }

        private static bool MatchesHeaderBase(Matriz matriz, BudgetMatrixHeaderBase expected)
        {
            return matriz.Id == expected.MatrixId
                && matriz.ProyectoId == expected.ProjectId
                && string.Equals(matriz.Clave ?? string.Empty, expected.Clave, StringComparison.Ordinal)
                && string.Equals(matriz.Descripcion ?? string.Empty, expected.Descripcion, StringComparison.Ordinal)
                && string.Equals(matriz.Unidad ?? string.Empty, expected.Unidad, StringComparison.Ordinal)
                && matriz.Tipo == expected.Tipo;
        }
    }
}
