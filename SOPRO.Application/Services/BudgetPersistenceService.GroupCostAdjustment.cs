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
        /// Reajusta el IMPORTE TOTAL de un agrupador sobre un contexto NUEVO por
        /// operación (ADR-0001): revalida la base económica del proyecto, el rowset
        /// COMPLETO del presupuesto, la coherencia visible agrupador=hojas, el conjunto
        /// EXACTO de hojas descendientes, el costo vigente de cada hoja con matriz, los
        /// rubros solicitados y la unión exacta de los cierres auxiliares de las
        /// matrices seleccionadas (cabecera, componentes y fuentes). Las hojas sin
        /// matriz o con matriz sin rubros ajustables son costo fijo: su importe no se
        /// toca. Busca un único factor con una búsqueda acotada que evalúa el importe
        /// REAL recalculado del grupo con los servicios canónicos, ajusta cada matriz
        /// una sola vez y recalcula el cierre topológico completo sin guardar. Un solo
        /// SaveChanges cierra la transacción; un conflicto, un objetivo inalcanzable o
        /// un fallo dejan la base sin cambios.
        /// </summary>
        public static BudgetGroupCostAdjustmentResult ExecuteGroupCostAdjustmentCommand(
            IProjectDbContextFactory factory,
            string databasePath,
            BudgetGroupCostAdjustmentCommand command)
        {
            ArgumentNullException.ThrowIfNull(factory);
            if (string.IsNullOrWhiteSpace(databasePath))
                throw new ArgumentException("La ruta de la base del proyecto es requerida.", nameof(databasePath));
            ArgumentNullException.ThrowIfNull(command);

            var execution = BudgetWriteExecutor.Execute(
                factory,
                databasePath,
                useTransaction: true,
                context => StageGroupCostAdjustment(context, command));

            return new BudgetGroupCostAdjustmentResult(
                execution.Result, execution.Confirmed, execution.Diagnostic);
        }

        /// <summary>
        /// Captura PRE-DIÁLOGO las bases de un reajuste agrupado: proyecto, rowset
        /// completo, agrupador, hojas descendientes visibles y unión de los cierres
        /// auxiliares de las matrices distintas asignadas a esas hojas. El llamador
        /// debe invocarla antes de mostrar el diálogo y conservar el resultado dentro
        /// del comando inmutable.
        /// </summary>
        public static BudgetGroupCostAdjustmentBases CaptureGroupCostAdjustmentBases(
            SOPROContext context,
            int projectId,
            int groupConceptId)
        {
            ArgumentNullException.ThrowIfNull(context);
            if (projectId <= 0)
                throw new ArgumentOutOfRangeException(nameof(projectId));
            if (groupConceptId <= 0)
                throw new ArgumentOutOfRangeException(nameof(groupConceptId));

            var proyecto = context.Proyectos
                .FirstOrDefault(p => p.Id == projectId)
                ?? throw new InvalidOperationException(
                    $"El proyecto {projectId} no existe en la base.");

            var filas = LoadOrderedRows(context, projectId);

            var grupo = filas.FirstOrDefault(f => f.Id == groupConceptId && f.EsAgrupador)
                ?? throw new InvalidOperationException(
                    $"El agrupador {groupConceptId} no existe en el presupuesto del proyecto {projectId}.");

            var hojas = CollectGroupVisibleLeaves(filas, groupConceptId);

            var matricesSeleccionadas = hojas
                .Where(h => h.MatrizId.HasValue)
                .Select(h => h.MatrizId!.Value)
                .Distinct()
                .OrderBy(id => id)
                .ToList();

            var idsCierre = matricesSeleccionadas.Count == 0
                ? new HashSet<int>()
                : PricePropagationService.CalcularCierreAscendente(
                    context, matricesSeleccionadas, projectId);

            var matrices = idsCierre.Count == 0
                ? new List<Matriz>()
                : PricePropagationService
                    .CargarMatricesConNavegaciones(context, idsCierre.ToList())
                    .Where(m => m.ProyectoId == projectId)
                    .OrderBy(m => m.Id)
                    .ToList();

            var faltante = matricesSeleccionadas
                .FirstOrDefault(id => !matrices.Any(m => m.Id == id));
            if (faltante != 0)
                throw new InvalidOperationException(
                    $"La matriz {faltante} de una hoja visible no existe en el proyecto {projectId}.");

            var cierre = matrices.Count == 0
                ? BudgetMatrixCostAdjustmentClosureBase.Vacio
                : new BudgetMatrixCostAdjustmentClosureBase(
                    matrices.Select(BudgetMatrixCostAdjustmentClosureEntry.FromMatriz));

            return new BudgetGroupCostAdjustmentBases(
                projectId,
                BudgetProjectCalculationBase.FromProyecto(proyecto),
                BudgetPriceRefreshRowset.FromConceptos(filas),
                BudgetGroupCostAdjustmentGroupBase.FromConcepto(grupo),
                hojas.Select(BudgetGroupCostAdjustmentLeafBase.FromConcepto),
                cierre);
        }

        /// <summary>
        /// Variante de captura sobre un contexto NUEVO: garantiza que las bases del
        /// reajuste agrupado se lean de la base y no de instancias potencialmente
        /// rastreadas o modificadas por la vista.
        /// </summary>
        public static BudgetGroupCostAdjustmentBases LoadGroupCostAdjustmentBases(
            IProjectDbContextFactory factory,
            string databasePath,
            int projectId,
            int groupConceptId)
        {
            ArgumentNullException.ThrowIfNull(factory);
            if (string.IsNullOrWhiteSpace(databasePath))
                throw new ArgumentException("La ruta de la base del proyecto es requerida.", nameof(databasePath));

            using var context = factory.Create(databasePath);
            return CaptureGroupCostAdjustmentBases(context, projectId, groupConceptId);
        }

        private static BudgetWriteStep<BudgetGroupCostAdjustmentSnapshot> StageGroupCostAdjustment(
            SOPROContext context,
            BudgetGroupCostAdjustmentCommand command)
        {
            if (!IsSupportedAdjustmentScopes(command.Scopes))
                return BudgetWriteStep<BudgetGroupCostAdjustmentSnapshot>.Abort(
                    BudgetOperationResult.Failed(
                        "El alcance del reajuste está vacío o contiene rubros no soportados; " +
                        "no se guardó nada."));

            if (command.TargetAmount <= 0m)
                return BudgetWriteStep<BudgetGroupCostAdjustmentSnapshot>.Abort(
                    BudgetOperationResult.Failed(
                        "El importe objetivo debe ser mayor que cero; no se guardó nada."));

            var proyecto = context.Proyectos
                .AsNoTracking()
                .FirstOrDefault(p => p.Id == command.ProjectId);

            if (proyecto == null)
                return BudgetWriteStep<BudgetGroupCostAdjustmentSnapshot>.Abort(
                    BudgetOperationResult.Conflict(
                        $"El proyecto {command.ProjectId} no existe en la base; la vista debe recargarse."));

            if (!MatchesProjectBase(proyecto, command.ExpectedProject))
                return BudgetWriteStep<BudgetGroupCostAdjustmentSnapshot>.Abort(
                    BudgetOperationResult.Conflict(
                        "Los parámetros de cálculo del proyecto cambiaron en la base desde que se cargó; " +
                        "la vista debe recargarse."));

            var filas = LoadOrderedRows(context, command.ProjectId);

            if (!MatchesPriceRefreshRowset(filas, command.ExpectedBudgetRows))
                return BudgetWriteStep<BudgetGroupCostAdjustmentSnapshot>.Abort(
                    BudgetOperationResult.Conflict(
                        "Las filas del presupuesto cambiaron en la base desde que se cargó; " +
                        "la vista debe recargarse."));

            var grupo = filas.FirstOrDefault(f => f.Id == command.GroupConceptId);

            if (grupo == null || !grupo.EsAgrupador)
                return BudgetWriteStep<BudgetGroupCostAdjustmentSnapshot>.Abort(
                    BudgetOperationResult.Conflict(
                        $"El agrupador {command.GroupConceptId} ya no existe en la base; " +
                        "la vista debe recargarse."));

            if (!MatchesGroupBase(grupo, command.ExpectedGroup))
                return BudgetWriteStep<BudgetGroupCostAdjustmentSnapshot>.Abort(
                    BudgetOperationResult.Conflict(
                        $"El agrupador {grupo.Id} cambió en la base desde que se cargó; " +
                        "la vista debe recargarse."));

            var hojasFrescas = CollectGroupVisibleLeaves(filas, command.GroupConceptId);

            if (!MatchesVisibleLeaves(hojasFrescas, command.ExpectedVisibleLeaves))
                return BudgetWriteStep<BudgetGroupCostAdjustmentSnapshot>.Abort(
                    BudgetOperationResult.Conflict(
                        $"Las hojas descendientes del agrupador {command.GroupConceptId} cambiaron en la " +
                        "base desde que se cargaron; la vista debe recargarse."));

            var engine = CalculationEngineFactory.FromProyecto(proyecto);

            // Coherencia visible: el total que el usuario vio en el agrupador debe ser
            // exactamente el que la política canónica deriva de sus hojas visibles.
            var totalVisible = engine.SumAmounts(hojasFrescas.Select(h => h.ImporteTotal));
            if (grupo.ImporteTotal != totalVisible || grupo.CostoDirectoTotal != totalVisible)
                return BudgetWriteStep<BudgetGroupCostAdjustmentSnapshot>.Abort(
                    BudgetOperationResult.Conflict(
                        $"El agrupador {grupo.Id} no coincide con la suma de sus hojas visibles en la base; " +
                        "la vista debe recargarse."));

            var matricesSeleccionadas = command.SelectedMatrixIds
                .Distinct()
                .OrderBy(id => id)
                .ToList();

            if (matricesSeleccionadas.Count == 0)
                return BudgetWriteStep<BudgetGroupCostAdjustmentSnapshot>.Abort(
                    BudgetOperationResult.Failed(
                        $"El agrupador {command.GroupConceptId} no contiene conceptos hoja con matriz asignada; " +
                        "no se guardó nada."));

            var errorCierre = ValidateExpectedClosure(
                command.ExpectedClosure,
                context,
                proyecto,
                matricesSeleccionadas,
                command.ProjectId);
            if (errorCierre != null)
                return BudgetWriteStep<BudgetGroupCostAdjustmentSnapshot>.Abort(errorCierre);

            var matricesCierre = PricePropagationService
                .CargarMatricesConNavegaciones(
                    context,
                    command.ExpectedClosure.Entries.Select(e => e.Matrix.MatrixId).ToList())
                .Where(m => m.ProyectoId == command.ProjectId)
                .ToDictionary(m => m.Id);

            var matrices = matricesSeleccionadas.ToDictionary(id => id, id => matricesCierre[id]);

            var errorHoja = ValidateVisibleLeafMatrixState(
                hojasFrescas, matrices, proyecto, command.ProjectId);
            if (errorHoja != null)
                return BudgetWriteStep<BudgetGroupCostAdjustmentSnapshot>.Abort(errorHoja);

            var ajustables = new Dictionary<int, MatrixAdjustmentBasis>();
            foreach (int matrixId in matricesSeleccionadas)
            {
                var matriz = matrices[matrixId];
                var basis = MatrixCostAdjustmentService.GetAdjustmentBasis(matriz, proyecto, command.Scopes);
                if (basis != null && basis.HasAdjustableScope)
                    ajustables[matrixId] = basis;
            }

            if (ajustables.Count == 0)
                return BudgetWriteStep<BudgetGroupCostAdjustmentSnapshot>.Abort(
                    BudgetOperationResult.Failed(
                        "Los rubros seleccionados no tienen importe ajustable dentro del agrupador; " +
                        "no se guardó nada."));

            var matricesQueCambian = CalculateChangeableSelectedMatrices(
                matricesSeleccionadas,
                matricesCierre,
                ajustables.Keys.ToHashSet());

            // Simulación AISLADA: los clones se reajustan y recalculan para evaluar el
            // importe real del grupo sin mutar varias veces las entidades rastreadas.
            var simulacion = new GroupCostAdjustmentSimulation(
                proyecto,
                matricesCierre,
                ajustables.Keys.ToHashSet(),
                command.Scopes,
                hojasFrescas.Select(BudgetGroupCostAdjustmentLeafBase.FromConcepto).ToList());

            if (!simulacion.TryEvaluate(1m, out var currentSimulado, out var errorSimulacion))
                return BudgetWriteStep<BudgetGroupCostAdjustmentSnapshot>.Abort(
                    BudgetOperationResult.Failed(
                        errorSimulacion + " No se guardó nada."));

            if (!simulacion.TryEvaluate(0m, out var minimo, out errorSimulacion))
                return BudgetWriteStep<BudgetGroupCostAdjustmentSnapshot>.Abort(
                    BudgetOperationResult.Failed(
                        errorSimulacion + " No se guardó nada."));

            const decimal tolerancia = 0.01m;
            if (command.TargetAmount < minimo - tolerancia)
                return BudgetWriteStep<BudgetGroupCostAdjustmentSnapshot>.Abort(
                    BudgetOperationResult.Failed(
                        "No es posible bajar el agrupador al importe solicitado con los rubros " +
                        $"seleccionados. El mínimo alcanzable es {minimo:C2}. No se guardó nada."));

            var factor = 1m;
            var logradoSimulado = currentSimulado;
            var diferenciaObjetivo = Math.Abs(command.TargetAmount - currentSimulado);

            if (Math.Abs(command.TargetAmount - currentSimulado) > tolerancia)
            {
                decimal low;
                decimal high;

                if (command.TargetAmount < currentSimulado)
                {
                    low = 0m;
                    high = 1m;
                }
                else
                {
                    low = 1m;
                    high = 2m;

                    if (!simulacion.TryEvaluate(high, out var alto, out errorSimulacion))
                        return BudgetWriteStep<BudgetGroupCostAdjustmentSnapshot>.Abort(
                            BudgetOperationResult.Failed(
                                errorSimulacion + " No se guardó nada."));

                    int guarda = 0;
                    while (alto < command.TargetAmount && guarda < 20)
                    {
                        high *= 2m;
                        if (!simulacion.TryEvaluate(high, out alto, out errorSimulacion))
                            return BudgetWriteStep<BudgetGroupCostAdjustmentSnapshot>.Abort(
                                BudgetOperationResult.Failed(
                                    errorSimulacion + " No se guardó nada."));
                        guarda++;
                    }

                    // El bracket [1, high] debe CONTENER el objetivo: si el extremo
                    // superior simulado todavía no lo alcanza, ningún factor del rango
                    // explorado lo logra y la búsqueda no puede acotarlo. Se aborta
                    // antes de tocar las entidades reales.
                    if (alto < command.TargetAmount)
                        return BudgetWriteStep<BudgetGroupCostAdjustmentSnapshot>.Abort(
                            BudgetOperationResult.Failed(
                                "No se pudo acotar el factor del reajuste: el importe simulado no alcanza " +
                                $"el objetivo {command.TargetAmount:C2} dentro del rango explorado. " +
                                "No se guardó nada."));
                }

                for (int i = 0; i < 40; i++)
                {
                    var medio = (low + high) / 2m;

                    if (!simulacion.TryEvaluate(medio, out var importe, out errorSimulacion))
                        return BudgetWriteStep<BudgetGroupCostAdjustmentSnapshot>.Abort(
                            BudgetOperationResult.Failed(
                                errorSimulacion + " No se guardó nada."));

                    var diferencia = Math.Abs(importe - command.TargetAmount);
                    if (diferencia < diferenciaObjetivo)
                    {
                        diferenciaObjetivo = diferencia;
                        logradoSimulado = importe;
                        factor = medio;
                    }

                    if (importe == command.TargetAmount)
                        break;

                    if (importe < command.TargetAmount)
                        low = medio;
                    else
                        high = medio;
                }
            }

            // Aplicación REAL: cada matriz ajustable recibe el factor UNA sola vez.
            // El recálculo topológico posterior propaga el resultado a las hojas y
            // agrupadores con la política única de PricePropagationService.
            foreach (int matrixId in matricesSeleccionadas.Where(id => ajustables.ContainsKey(id)))
            {
                var ajuste = MatrixCostAdjustmentService.AdjustMatrixByFactor(
                    matrices[matrixId], proyecto, command.Scopes, factor);

                if (!ajuste.Success)
                    return BudgetWriteStep<BudgetGroupCostAdjustmentSnapshot>.Abort(
                        BudgetOperationResult.Failed(
                            string.IsNullOrWhiteSpace(ajuste.Message)
                                ? "No se pudo reajustar una matriz del agrupador; no se guardó nada."
                                : ajuste.Message + " No se guardó nada."));
            }

            var afectadas = PricePropagationService.RecalcularCierreAuxiliarSinGuardar(
                context,
                matricesSeleccionadas,
                command.ProjectId);

            var filasFinales = LoadOrderedRows(context, command.ProjectId);
            var grupoFinal = filasFinales.First(f => f.Id == command.GroupConceptId);
            var hojasFinales = CollectGroupVisibleLeaves(filasFinales, command.GroupConceptId);
            var logrado = engine.SumAmounts(hojasFinales.Select(h => h.ImporteTotal));

            if (Math.Abs(logrado - logradoSimulado) > tolerancia)
                return BudgetWriteStep<BudgetGroupCostAdjustmentSnapshot>.Abort(
                    BudgetOperationResult.Failed(
                        "El importe logrado no coincide con el importe recalculado de la simulación; " +
                        "no se guardó nada."));

            var snapshot = BuildGroupCostAdjustmentSnapshot(
                command,
                proyecto,
                grupoFinal,
                hojasFinales,
                filasFinales,
                afectadas,
                matricesQueCambian,
                ajustables.Keys.ToHashSet(),
                matricesSeleccionadas,
                totalVisible,
                logrado,
                minimo,
                factor);

            var conceptosConfirmados = filasFinales
                .Where(f => f.EsAgrupador
                    || (f.MatrizId.HasValue && afectadas.Any(m => m.Id == f.MatrizId.Value)))
                .Select(f => f.Id)
                .Distinct()
                .ToArray();

            return BudgetWriteStep<BudgetGroupCostAdjustmentSnapshot>.Commit(
                snapshot, conceptosConfirmados);
        }

        private static BudgetGroupCostAdjustmentSnapshot BuildGroupCostAdjustmentSnapshot(
            BudgetGroupCostAdjustmentCommand command,
            Proyecto proyecto,
            ConceptoPresupuesto grupo,
            IReadOnlyList<ConceptoPresupuesto> hojasFinales,
            IReadOnlyList<ConceptoPresupuesto> filasFinales,
            IReadOnlyList<Matriz> afectadas,
            IReadOnlySet<int> matricesQueCambian,
            IReadOnlySet<int> matricesAjustadas,
            IReadOnlyList<int> matricesSeleccionadas,
            decimal importeActual,
            decimal importeLogrado,
            decimal importeMinimo,
            decimal factor)
        {
            var ordenadas = afectadas.OrderBy(m => m.Id).ToList();

            var matrices = ordenadas
                .Select(BudgetConfirmedMatrixRow.FromMatriz)
                .ToArray();

            var componentes = ordenadas
                .SelectMany(m => m.Componentes
                    .OrderBy(c => c.Orden)
                    .ThenBy(c => c.Id)
                    .Select(BudgetConfirmedMatrixComponentRow.FromComponente))
                .ToArray();

            var idsAfectados = ordenadas.Select(m => m.Id).ToHashSet();

            var hojasAfectadas = filasFinales
                .Where(f => !f.EsAgrupador
                    && f.MatrizId.HasValue
                    && idsAfectados.Contains(f.MatrizId.Value))
                .Select(BudgetConfirmedConceptRow.FromConcepto)
                .ToArray();

            var agrupadores = filasFinales
                .Where(f => f.EsAgrupador)
                .Select(BudgetConfirmedConceptRow.FromConcepto)
                .ToArray();

            var ajustadas = matricesSeleccionadas
                .Where(id => matricesAjustadas.Contains(id))
                .OrderBy(id => id)
                .ToArray();

            var hojasAjustables = hojasFinales
                .Count(h => h.MatrizId.HasValue && matricesQueCambian.Contains(h.MatrizId.Value));

            return new BudgetGroupCostAdjustmentSnapshot(
                command.ProjectId,
                BudgetProjectCalculationBase.FromProyecto(proyecto),
                BudgetConfirmedConceptRow.FromConcepto(grupo),
                hojasAfectadas,
                agrupadores,
                matrices,
                componentes,
                ajustadas,
                command.TargetAmount,
                importeActual,
                importeLogrado,
                importeMinimo,
                factor,
                hojasAjustables,
                hojasFinales.Count - hojasAjustables);
        }

        private static List<ConceptoPresupuesto> CollectGroupVisibleLeaves(
            IReadOnlyList<ConceptoPresupuesto> orderedRows,
            int groupConceptId)
        {
            var hojas = new List<ConceptoPresupuesto>();
            int indice = -1;

            for (int i = 0; i < orderedRows.Count; i++)
            {
                if (orderedRows[i].Id == groupConceptId)
                {
                    indice = i;
                    break;
                }
            }

            if (indice < 0)
                return hojas;

            int nivelAgrupador = orderedRows[indice].Nivel;
            for (int i = indice + 1; i < orderedRows.Count; i++)
            {
                var fila = orderedRows[i];

                // Misma política que PricePropagationService.ActualizarAgrupadores:
                // toda fila no agrupadora es nivel 5, sin importar su Nivel almacenado.
                int nivel = fila.EsAgrupador ? fila.Nivel : 5;
                if (nivel <= nivelAgrupador)
                    break;

                if (!fila.EsAgrupador)
                    hojas.Add(fila);
            }

            return hojas;
        }

        private static bool MatchesGroupBase(
            ConceptoPresupuesto grupo,
            BudgetGroupCostAdjustmentGroupBase expected)
        {
            return grupo.Id == expected.ConceptId
                && grupo.EsAgrupador
                && grupo.Orden == expected.Orden
                && grupo.Nivel == expected.Nivel
                && grupo.CostoDirectoTotal == expected.CostoDirectoTotal
                && grupo.ImporteTotal == expected.ImporteTotal;
        }

        private static bool MatchesVisibleLeaves(
            IReadOnlyList<ConceptoPresupuesto> actual,
            IReadOnlyList<BudgetGroupCostAdjustmentLeafBase> expected)
        {
            if (actual.Count != expected.Count)
                return false;

            var porId = expected.ToDictionary(h => h.ConceptId);

            foreach (var hoja in actual)
            {
                if (!porId.TryGetValue(hoja.Id, out var esperada))
                    return false;

                if (hoja.EsAgrupador
                    || hoja.MatrizId != esperada.MatrixId
                    || hoja.Cantidad != esperada.Cantidad
                    || hoja.CostoDirectoUnitario != esperada.CostoDirectoUnitario
                    || hoja.PrecioUnitario != esperada.PrecioUnitario
                    || hoja.ImporteTotal != esperada.ImporteTotal)
                    return false;
            }

            return true;
        }

        private static BudgetOperationResult? ValidateVisibleLeafMatrixState(
            IReadOnlyList<ConceptoPresupuesto> hojas,
            IReadOnlyDictionary<int, Matriz> matrices,
            Proyecto proyecto,
            int projectId)
        {
            var engine = CalculationEngineFactory.FromProyecto(proyecto);

            foreach (var hoja in hojas)
            {
                if (!hoja.MatrizId.HasValue)
                    continue;

                if (!matrices.TryGetValue(hoja.MatrizId.Value, out var matriz))
                    return BudgetOperationResult.Conflict(
                        $"La matriz {hoja.MatrizId.Value} de la hoja {hoja.Id} ya no existe en la base; " +
                        "la vista debe recargarse.");

                if (matriz.ProyectoId != projectId)
                    return BudgetOperationResult.Conflict(
                        $"La matriz {matriz.Id} de la hoja {hoja.Id} no pertenece al proyecto {projectId}; " +
                        "la vista debe recargarse.");

                var costoDirectoUnitario = engine.RoundAmount(matriz.CostoDirecto);
                if (hoja.CostoDirectoUnitario != costoDirectoUnitario)
                    return BudgetOperationResult.Conflict(
                        $"La hoja {hoja.Id} no refleja el costo directo vigente de la matriz {matriz.Id}; " +
                        "la vista debe recargarse.");

                var precioUnitario = BudgetPricingService.CalculateUnitPrice(proyecto, costoDirectoUnitario);
                if (hoja.PrecioUnitario != precioUnitario
                    || hoja.CostoDirectoTotal != engine.Multiply(hoja.Cantidad, costoDirectoUnitario)
                    || hoja.ImporteTotal != BudgetPricingService.MultiplyUsingDisplayPrecision(
                        proyecto, hoja.Cantidad, precioUnitario))
                    return BudgetOperationResult.Conflict(
                        $"La hoja {hoja.Id} no coincide con el costo vigente de la matriz {matriz.Id}; " +
                        "la vista debe recargarse.");
            }

            return null;
        }

        /// <summary>
        /// Matrices SELECCIONADAS cuyo costo puede cambiar: las que tienen rubros
        /// ajustables y todas las que dependen transitivamente de ellas dentro del
        /// cierre (aunque el camino pase por una matriz no seleccionada). Una matriz
        /// sin rubros propios pero con un auxiliar reajustable cambia igual, así que su
        /// hoja no es costo fijo.
        /// </summary>
        private static HashSet<int> CalculateChangeableSelectedMatrices(
            IReadOnlyList<int> matricesSeleccionadas,
            IReadOnlyDictionary<int, Matriz> matricesCierre,
            IReadOnlySet<int> matricesAjustables)
        {
            var cambian = new HashSet<int>(matricesAjustables);
            bool huboCambio;

            do
            {
                huboCambio = false;

                foreach (var par in matricesCierre)
                {
                    if (cambian.Contains(par.Key))
                        continue;

                    if (par.Value.Componentes.Any(c =>
                        c.TipoComponente == TipoComponenteMatriz.Auxiliar
                        && c.AuxiliarId.HasValue
                        && cambian.Contains(c.AuxiliarId.Value)))
                    {
                        cambian.Add(par.Key);
                        huboCambio = true;
                    }
                }
            }
            while (huboCambio);

            return matricesSeleccionadas
                .Where(cambian.Contains)
                .ToHashSet();
        }

        /// <summary>
        /// Simulación aislada del reajuste agrupado. Construye clones de las matrices
        /// del cierre, aplica el factor con la semántica canónica y evalúa el importe
        /// real del grupo sin tocar las entidades rastreadas. Cada candidato parte de
        /// las cantidades originales, de modo que una búsqueda no acumula mutaciones.
        /// </summary>
        private sealed class GroupCostAdjustmentSimulation
        {
            private readonly Proyecto _proyecto;
            private readonly SoproCalculationEngine _engine;
            private readonly MatrixAdjustmentScopes _scopes;
            private readonly HashSet<int> _matricesAjustables;
            private readonly IReadOnlyList<BudgetGroupCostAdjustmentLeafBase> _hojas;
            private readonly IReadOnlyList<Matriz> _ordenadas;
            private readonly Dictionary<int, Matriz> _clones;
            private readonly Dictionary<ComponenteMatriz, (decimal Cantidad, decimal Rendimiento, decimal Importe)> _originales;
            private readonly Dictionary<int, decimal> _costosOriginales;

            public GroupCostAdjustmentSimulation(
                Proyecto proyecto,
                IReadOnlyDictionary<int, Matriz> matrices,
                HashSet<int> matricesAjustables,
                MatrixAdjustmentScopes scopes,
                IReadOnlyList<BudgetGroupCostAdjustmentLeafBase> hojas)
            {
                _proyecto = proyecto;
                _engine = CalculationEngineFactory.FromProyecto(proyecto);
                _scopes = scopes;
                _matricesAjustables = matricesAjustables;
                _hojas = hojas;
                _clones = new Dictionary<int, Matriz>();
                _originales = new Dictionary<ComponenteMatriz, (decimal, decimal, decimal)>();
                _costosOriginales = new Dictionary<int, decimal>();

                foreach (var original in matrices.Values.OrderBy(m => m.Id))
                {
                    var clone = new Matriz
                    {
                        Id = original.Id,
                        Clave = original.Clave,
                        Descripcion = original.Descripcion,
                        Unidad = original.Unidad,
                        Tipo = original.Tipo,
                        CostoDirecto = original.CostoDirecto,
                        Origen = original.Origen,
                        MatrizMaestraId = original.MatrizMaestraId,
                        ProyectoId = original.ProyectoId,
                        Notas = original.Notas,
                        FechaCreacion = original.FechaCreacion,
                        FechaModificacion = original.FechaModificacion
                    };

                    _clones[original.Id] = clone;
                    _costosOriginales[original.Id] = original.CostoDirecto;
                }

                foreach (var original in matrices.Values.OrderBy(m => m.Id))
                {
                    var clone = _clones[original.Id];

                    foreach (var componente in original.Componentes
                        .OrderBy(c => c.Orden)
                        .ThenBy(c => c.Id))
                    {
                        var clonComponente = new ComponenteMatriz
                        {
                            Id = componente.Id,
                            MatrizId = componente.MatrizId,
                            Matriz = clone,
                            TipoComponente = componente.TipoComponente,
                            MaterialId = componente.MaterialId,
                            Material = componente.Material,
                            ManoDeObraId = componente.ManoDeObraId,
                            ManoDeObra = componente.ManoDeObra,
                            MaquinariaId = componente.MaquinariaId,
                            Maquinaria = componente.Maquinaria,
                            HerramientaId = componente.HerramientaId,
                            Herramienta = componente.Herramienta,
                            AuxiliarId = componente.AuxiliarId,
                            Auxiliar = componente.TipoComponente == TipoComponenteMatriz.Auxiliar
                                && componente.AuxiliarId.HasValue
                                && _clones.TryGetValue(componente.AuxiliarId.Value, out var auxiliarClon)
                                    ? auxiliarClon
                                    : componente.Auxiliar,
                            Cantidad = componente.Cantidad,
                            Rendimiento = componente.Rendimiento,
                            Importe = componente.Importe,
                            Orden = componente.Orden,
                            Notas = componente.Notas
                        };

                        clone.Componentes.Add(clonComponente);
                        _originales[clonComponente] = (
                            componente.Cantidad,
                            componente.Rendimiento,
                            componente.Importe);
                    }
                }

                _ordenadas = MatrixGraphOrderService.OrdenTopologico(_clones.Values);
            }

            public bool TryEvaluate(decimal factor, out decimal importe, out string? error)
            {
                RestoreOriginalState();

                foreach (var matriz in _ordenadas)
                {
                    if (_matricesAjustables.Contains(matriz.Id))
                    {
                        var ajuste = MatrixCostAdjustmentService.AdjustMatrixByFactor(
                            matriz, _proyecto, _scopes, factor);

                        if (!ajuste.Success)
                        {
                            importe = 0m;
                            error = string.IsNullOrWhiteSpace(ajuste.Message)
                                ? "No se pudo simular el reajuste de una matriz del agrupador."
                                : ajuste.Message;
                            return false;
                        }
                    }

                    var componentes = matriz.Componentes
                        .OrderBy(c => c.Orden)
                        .ThenBy(c => c.Id)
                        .ToList();

                    var totales = MatrixComponentCalculationService.Recalculate(
                        componentes, _proyecto.DecimalesImporte);

                    matriz.CostoDirecto = _engine.RoundAmount(totales.CostoDirectoTotal);
                }

                importe = CalculateGroupAmount();
                error = null;
                return true;
            }

            private void RestoreOriginalState()
            {
                foreach (var par in _originales)
                {
                    par.Key.Cantidad = par.Value.Cantidad;
                    par.Key.Rendimiento = par.Value.Rendimiento;
                    par.Key.Importe = par.Value.Importe;
                }

                foreach (var par in _costosOriginales)
                {
                    if (_clones.TryGetValue(par.Key, out var matriz))
                        matriz.CostoDirecto = par.Value;
                }
            }

            private decimal CalculateGroupAmount()
            {
                decimal total = 0m;

                foreach (var hoja in _hojas)
                {
                    if (hoja.MatrixId is int matrizId
                        && _clones.TryGetValue(matrizId, out var matriz))
                    {
                        // La matriz está en el conjunto seleccionado: su costo puede
                        // cambiar aunque no tenga rubros ajustables propios, porque un
                        // auxiliar relacionado sí los tenga. El clon ya refleja el
                        // recálculo topológico real.
                        var costoDirectoUnitario = _engine.RoundAmount(matriz.CostoDirecto);
                        var precioUnitario = BudgetPricingService.CalculateUnitPrice(
                            _proyecto, costoDirectoUnitario);
                        total += BudgetPricingService.MultiplyUsingDisplayPrecision(
                            _proyecto, hoja.Cantidad, precioUnitario);
                    }
                    else
                    {
                        // Hoja sin matriz: costo fijo.
                        total += hoja.ImporteTotal;
                    }
                }

                return total;
            }
        }
    }
}
