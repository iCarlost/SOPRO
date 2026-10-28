using Microsoft.EntityFrameworkCore;
using SOPRO.Application.Models.Presupuesto;
using SOPRO.Core.Entities;
using SOPRO.Data.Context;
using SOPRO.Data.Factories;

namespace SOPRO.Application.Services
{
    public static partial class BudgetPersistenceService
    {
        /// <summary>
        /// Elimina UN componente YA persistido de una matriz del proyecto (ADR-0001)
        /// sobre un contexto NUEVO por operación: revalida la base económica del
        /// proyecto, la matriz, el fingerprint EXACTO de componentes (Ids y Orden), el
        /// componente a eliminar y el rowset COMPLETO de filas del presupuesto. Retira
        /// solo ese componente y lo excluye del cálculo antes de recalcular, sin
        /// guardar todavía, la matriz editada y todas sus ascendientes auxiliares en
        /// orden topológico, TODAS las hojas del presupuesto que referencian alguna de
        /// esas matrices y todos los agrupadores con la política única de
        /// <see cref="PricePropagationService"/>. Un único SaveChanges cierra la
        /// transacción: un conflicto, un ciclo o un fallo dejan la base sin cambios y
        /// el reintento del mismo comando no duplica nada.
        /// </summary>
        public static BudgetMatrixComponentDeletionResult ExecuteMatrixComponentDeletionCommand(
            IProjectDbContextFactory factory,
            string databasePath,
            BudgetMatrixComponentDeletionCommand command)
        {
            ArgumentNullException.ThrowIfNull(factory);
            if (string.IsNullOrWhiteSpace(databasePath))
                throw new ArgumentException("La ruta de la base del proyecto es requerida.", nameof(databasePath));
            ArgumentNullException.ThrowIfNull(command);

            var execution = BudgetWriteExecutor.Execute(
                factory,
                databasePath,
                useTransaction: true,
                context => StageMatrixComponentDeletion(context, command));

            return new BudgetMatrixComponentDeletionResult(
                execution.Result, execution.Confirmed, execution.Diagnostic);
        }

        private static BudgetWriteStep<BudgetMatrixComponentDeletionSnapshot> StageMatrixComponentDeletion(
            SOPROContext context,
            BudgetMatrixComponentDeletionCommand command)
        {
            var proyecto = context.Proyectos
                .AsNoTracking()
                .FirstOrDefault(p => p.Id == command.ProjectId);

            if (proyecto == null)
                return BudgetWriteStep<BudgetMatrixComponentDeletionSnapshot>.Abort(
                    BudgetOperationResult.Conflict(
                        $"El proyecto {command.ProjectId} no existe en la base; la vista debe recargarse."));

            if (!MatchesProjectBase(proyecto, command.ExpectedProject))
                return BudgetWriteStep<BudgetMatrixComponentDeletionSnapshot>.Abort(
                    BudgetOperationResult.Conflict(
                        "Los parámetros de cálculo del proyecto cambiaron en la base desde que se cargó; " +
                        "la vista debe recargarse."));

            // Se carga TRACKEADA y con TODAS las navegaciones: la validación, la
            // eliminación y el recálculo del cierre deben operar sobre las mismas
            // instancias que luego viajan en el único SaveChanges.
            var matriz = PricePropagationService
                .CargarMatricesConNavegaciones(context, new List<int> { command.MatrixId })
                .FirstOrDefault();

            if (matriz == null)
                return BudgetWriteStep<BudgetMatrixComponentDeletionSnapshot>.Abort(
                    BudgetOperationResult.Conflict(
                        $"La matriz {command.MatrixId} ya no existe en la base; la vista debe recargarse."));

            if (matriz.ProyectoId != command.ProjectId)
                return BudgetWriteStep<BudgetMatrixComponentDeletionSnapshot>.Abort(
                    BudgetOperationResult.Conflict(
                        $"La matriz {command.MatrixId} no pertenece al proyecto {command.ProjectId} " +
                        "en la base; la vista debe recargarse."));

            if (!MatchesMatrixBase(matriz, command.ExpectedMatrix))
                return BudgetWriteStep<BudgetMatrixComponentDeletionSnapshot>.Abort(
                    BudgetOperationResult.Conflict(
                        $"La matriz {command.MatrixId} cambió en la base desde que se cargó; " +
                        "la vista debe recargarse."));

            var componentes = matriz.Componentes
                .OrderBy(c => c.Orden)
                .ThenBy(c => c.Id)
                .ToList();

            if (!MatchesComponentFingerprint(componentes, command.ExpectedComponents))
                return BudgetWriteStep<BudgetMatrixComponentDeletionSnapshot>.Abort(
                    BudgetOperationResult.Conflict(
                        $"Los componentes de la matriz {command.MatrixId} cambiaron en la base " +
                        "desde que se cargaron; la vista debe recargarse."));

            var componente = componentes.FirstOrDefault(c => c.Id == command.ComponentId);

            if (componente == null)
                return BudgetWriteStep<BudgetMatrixComponentDeletionSnapshot>.Abort(
                    BudgetOperationResult.Conflict(
                        $"El componente {command.ComponentId} ya no pertenece a la matriz " +
                        $"{command.MatrixId} en la base; la vista debe recargarse."));

            if (!MatchesComponentBase(componente, command.ExpectedComponent))
                return BudgetWriteStep<BudgetMatrixComponentDeletionSnapshot>.Abort(
                    BudgetOperationResult.Conflict(
                        $"El componente {command.ComponentId} cambió en la base desde que se cargó; " +
                        "la vista debe recargarse."));

            var filas = LoadOrderedRows(context, command.ProjectId);

            if (!MatchesPriceRefreshRowset(filas, command.ExpectedBudgetRows))
                return BudgetWriteStep<BudgetMatrixComponentDeletionSnapshot>.Abort(
                    BudgetOperationResult.Conflict(
                        "Las filas del presupuesto cambiaron en la base desde que se cargó; " +
                        "la vista debe recargarse."));

            // El componente queda marcado como Deleted (el único SaveChanges emitirá su
            // DELETE) y se retira de la navegación YA cargada: el recálculo no debe
            // sumar su importe. EF no lo vuelve a fijar en la colección porque la
            // instancia rastreada está en estado Deleted.
            context.ComponentesMatriz.Remove(componente);
            matriz.Componentes.Remove(componente);

            // Cierre completo SIN guardar: matriz editada + ascendientes auxiliares en
            // orden topológico, hojas que referencian cualquiera de ellas y agrupadores.
            // Un ciclo alcanzable o una navegación incompleta lanza y el ejecutor
            // clasifica el fallo como revertido, sin escritura.
            var afectadas = PricePropagationService.RecalcularCierreAuxiliarSinGuardar(
                context,
                new[] { matriz.Id },
                command.ProjectId);

            var snapshot = BuildMatrixComponentDeletionSnapshot(
                context, command, proyecto, matriz, afectadas);

            // El comando no pidió conceptos del presupuesto: los efectos derivados
            // (hojas y agrupadores) viajan en la instantánea.
            return BudgetWriteStep<BudgetMatrixComponentDeletionSnapshot>.Commit(
                snapshot, Array.Empty<int>());
        }

        private static BudgetMatrixComponentDeletionSnapshot BuildMatrixComponentDeletionSnapshot(
            SOPROContext context,
            BudgetMatrixComponentDeletionCommand command,
            Proyecto proyecto,
            Matriz matriz,
            IReadOnlyList<Matriz> afectadas)
        {
            var matricesAfectadas = afectadas
                .OrderBy(m => m.Id)
                .Select(BudgetConfirmedMatrixRow.FromMatriz)
                .ToArray();

            var componentesAfectados = afectadas
                .OrderBy(m => m.Id)
                .SelectMany(m => m.Componentes
                    .OrderBy(c => c.Orden)
                    .ThenBy(c => c.Id)
                    .Select(BudgetConfirmedMatrixComponentRow.FromComponente))
                .ToArray();

            var afectadasIds = afectadas.Select(m => m.Id).ToHashSet();

            // Las filas se releen del contexto rastreado: el recálculo ya escribió en
            // esas mismas instancias, así que la instantánea refleja lo que el único
            // SaveChanges va a persistir.
            var filas = LoadOrderedRows(context, command.ProjectId);

            var hojas = filas
                .Where(f => !f.EsAgrupador
                    && f.MatrizId.HasValue
                    && afectadasIds.Contains(f.MatrizId.Value))
                .Select(BudgetConfirmedConceptRow.FromConcepto)
                .ToArray();

            var agrupadores = filas
                .Where(f => f.EsAgrupador)
                .Select(BudgetConfirmedConceptRow.FromConcepto)
                .ToArray();

            return new BudgetMatrixComponentDeletionSnapshot(
                command.ProjectId,
                BudgetProjectCalculationBase.FromProyecto(proyecto),
                BudgetConfirmedMatrixRow.FromMatriz(matriz),
                matricesAfectadas,
                componentesAfectados,
                hojas,
                agrupadores,
                command.ComponentId);
        }
    }
}
