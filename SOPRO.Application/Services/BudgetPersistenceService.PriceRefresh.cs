using Microsoft.EntityFrameworkCore;
using Sopro.Calculation;
using SOPRO.Application.Models.Presupuesto;
using SOPRO.Core.Entities;
using SOPRO.Data.Context;
using SOPRO.Data.Factories;

namespace SOPRO.Application.Services
{
    public static partial class BudgetPersistenceService
    {
        /// <summary>
        /// Refresca los precios de todo el presupuesto (ADR-0001) sobre un contexto
        /// NUEVO por operación: revalida la base económica del proyecto y el rowset
        /// COMPLETO de filas persistidas (Ids, orden, jerarquía, matriz, cantidad y
        /// los cuatro valores económicos), relee cada matriz de la base —su cambio es
        /// el motivo del refresco, por eso no se compara— y recalcula las hojas con
        /// <see cref="BudgetPricingService"/> y el motor del proyecto. Una hoja sin
        /// matriz conserva su costo directo y recalcula P.U. e importe con los
        /// porcentajes vigentes. Los agrupadores se actualizan con la política única
        /// de <see cref="PricePropagationService.ActualizarAgrupadores"/> y todo se
        /// confirma con un solo SaveChanges dentro de la transacción.
        /// </summary>
        public static BudgetPriceRefreshResult RefreshBudgetPrices(
            IProjectDbContextFactory factory,
            string databasePath,
            BudgetPriceRefreshCommand command)
        {
            ArgumentNullException.ThrowIfNull(factory);
            if (string.IsNullOrWhiteSpace(databasePath))
                throw new ArgumentException("La ruta de la base del proyecto es requerida.", nameof(databasePath));
            ArgumentNullException.ThrowIfNull(command);

            var execution = BudgetWriteExecutor.Execute(
                factory,
                databasePath,
                useTransaction: true,
                context => StagePriceRefresh(context, command));

            return new BudgetPriceRefreshResult(execution.Result, execution.Confirmed, execution.Diagnostic);
        }

        private static BudgetWriteStep<BudgetPriceRefreshSnapshot> StagePriceRefresh(
            SOPROContext context,
            BudgetPriceRefreshCommand command)
        {
            var proyecto = context.Proyectos
                .AsNoTracking()
                .FirstOrDefault(p => p.Id == command.ProjectId);

            if (proyecto == null)
                return BudgetWriteStep<BudgetPriceRefreshSnapshot>.Abort(
                    BudgetOperationResult.Conflict(
                        $"El proyecto {command.ProjectId} no existe en la base; la vista debe recargarse."));

            if (!MatchesProjectBase(proyecto, command.ExpectedProject))
                return BudgetWriteStep<BudgetPriceRefreshSnapshot>.Abort(
                    BudgetOperationResult.Conflict(
                        "Los parámetros de cálculo del proyecto cambiaron en la base desde que se cargó; " +
                        "la vista debe recargarse."));

            var filas = context.ConceptosPresupuesto
                .Where(c => c.ProyectoId == command.ProjectId)
                .OrderBy(c => c.Orden)
                .ThenBy(c => c.Id)
                .ToList();

            if (!MatchesPriceRefreshRowset(filas, command.ExpectedRows))
                return BudgetWriteStep<BudgetPriceRefreshSnapshot>.Abort(
                    BudgetOperationResult.Conflict(
                        "Las filas del presupuesto cambiaron en la base desde que se cargó; " +
                        "la vista debe recargarse."));

            var matrices = CargarMatricesDelRefresco(context, filas, command.ProjectId, out var errorMatriz);
            if (matrices is null)
                return BudgetWriteStep<BudgetPriceRefreshSnapshot>.Abort(errorMatriz!);

            var engine = CalculationEngineFactory.FromProyecto(proyecto);

            foreach (var fila in filas)
            {
                if (fila.EsAgrupador)
                    continue;

                decimal costoDirectoUnitario;
                decimal costoDirectoTotal;

                if (fila.MatrizId is int matrizId)
                {
                    var matriz = matrices[matrizId];
                    costoDirectoUnitario = engine.RoundAmount(matriz.CostoDirecto);
                    costoDirectoTotal = BudgetPricingService.MultiplyUsingDisplayPrecision(
                        proyecto, fila.Cantidad, costoDirectoUnitario);
                }
                else
                {
                    // Sin matriz no hay costo nuevo que leer: se conserva el costo
                    // directo persistido y solo se reexpresa el precio con los
                    // porcentajes vigentes.
                    costoDirectoUnitario = fila.CostoDirectoUnitario;
                    costoDirectoTotal = fila.CostoDirectoTotal;
                }

                var precioUnitario = BudgetPricingService.CalculateUnitPrice(proyecto, costoDirectoUnitario);
                var importeTotal = BudgetPricingService.MultiplyUsingDisplayPrecision(
                    proyecto, fila.Cantidad, precioUnitario);

                if (fila.CostoDirectoUnitario == costoDirectoUnitario
                    && fila.CostoDirectoTotal == costoDirectoTotal
                    && fila.PrecioUnitario == precioUnitario
                    && fila.ImporteTotal == importeTotal)
                    continue;

                fila.CostoDirectoUnitario = costoDirectoUnitario;
                fila.CostoDirectoTotal = costoDirectoTotal;
                fila.PrecioUnitario = precioUnitario;
                fila.ImporteTotal = importeTotal;
            }

            // Política única compartida con RecalculoGlobalService: los agrupadores
            // se recalculan bottom-up sobre las hojas ya refrescadas en este contexto.
            PricePropagationService.ActualizarAgrupadores(context, new List<Proyecto> { proyecto });

            var snapshot = new BudgetPriceRefreshSnapshot(
                command.ProjectId,
                BudgetProjectCalculationBase.FromProyecto(proyecto),
                filas.Select(BudgetConfirmedConceptRow.FromConcepto).ToArray());

            return BudgetWriteStep<BudgetPriceRefreshSnapshot>.Commit(
                snapshot, filas.Select(f => f.Id).ToArray());
        }

        /// <summary>
        /// Relee las matrices referenciadas por las hojas. La ausencia o la
        /// pertenencia a otro proyecto son conflicto: recalcular con una matriz
        /// ajena produciría importes que el usuario nunca vio.
        /// </summary>
        private static Dictionary<int, Matriz>? CargarMatricesDelRefresco(
            SOPROContext context,
            IReadOnlyList<ConceptoPresupuesto> filas,
            int projectId,
            out BudgetOperationResult? error)
        {
            error = null;

            var ids = filas
                .Where(f => !f.EsAgrupador && f.MatrizId.HasValue)
                .Select(f => f.MatrizId!.Value)
                .Distinct()
                .ToList();

            if (ids.Count == 0)
                return new Dictionary<int, Matriz>();

            var matrices = context.Matrices
                .AsNoTracking()
                .Where(m => ids.Contains(m.Id))
                .ToDictionary(m => m.Id);

            foreach (int id in ids)
            {
                if (!matrices.TryGetValue(id, out var matriz))
                {
                    error = BudgetOperationResult.Conflict(
                        $"La matriz {id} ya no existe en la base; la vista debe recargarse.");
                    return null;
                }

                if (matriz.ProyectoId != projectId)
                {
                    error = BudgetOperationResult.Conflict(
                        $"La matriz {id} no pertenece al proyecto {projectId} en la base; " +
                        "la vista debe recargarse.");
                    return null;
                }
            }

            return matrices;
        }

        private static bool MatchesPriceRefreshRowset(
            IReadOnlyList<ConceptoPresupuesto> actual,
            BudgetPriceRefreshRowset expected)
        {
            if (actual.Count != expected.Rows.Count)
                return false;

            for (int i = 0; i < actual.Count; i++)
            {
                var fila = actual[i];
                var esperada = expected.Rows[i];

                if (fila.Id != esperada.ConceptId
                    || fila.ProyectoId != esperada.ProjectId
                    || fila.Orden != esperada.Orden
                    || fila.Nivel != esperada.Nivel
                    || fila.EsAgrupador != esperada.EsAgrupador
                    || fila.MatrizId != esperada.MatrizId
                    || fila.Cantidad != esperada.Cantidad
                    || fila.CostoDirectoUnitario != esperada.CostoDirectoUnitario
                    || fila.CostoDirectoTotal != esperada.CostoDirectoTotal
                    || fila.PrecioUnitario != esperada.PrecioUnitario
                    || fila.ImporteTotal != esperada.ImporteTotal)
                    return false;
            }

            return true;
        }
    }
}
