using SOPRO.Application.Services;
using SOPRO.Core.Entities;

namespace SOPRO.Application.Models.Presupuesto
{
    /// <summary>
    /// Bases esperadas de un reajuste de costo de UN agrupador, capturadas ANTES del
    /// diálogo: la base económica del proyecto, el rowset COMPLETO del presupuesto, el
    /// agrupador seleccionado, todas las hojas descendientes VISIBLES y la unión de los
    /// cierres auxiliares de las matrices distintas asignadas a esas hojas. Es un
    /// objeto inmutable: un reintento del comando repite exactamente la misma
    /// comparación contra la base.
    /// </summary>
    /// <remarks>
    /// Captura sugerida para la vista:
    /// <c>var bases = BudgetPersistenceService.LoadGroupCostAdjustmentBases(factory, ruta, proyectoId, agrupadorId);</c>
    /// antes de mostrar el diálogo y, tras confirmar, construir el comando con
    /// <c>new BudgetGroupCostAdjustmentCommand(bases, rubros, importeObjetivo)</c>.
    /// </remarks>
    public sealed class BudgetGroupCostAdjustmentBases
    {
        public BudgetGroupCostAdjustmentBases(
            int projectId,
            BudgetProjectCalculationBase project,
            BudgetPriceRefreshRowset budgetRows,
            BudgetGroupCostAdjustmentGroupBase group,
            IEnumerable<BudgetGroupCostAdjustmentLeafBase> visibleLeaves,
            BudgetMatrixCostAdjustmentClosureBase closure)
        {
            if (projectId <= 0)
                throw new ArgumentOutOfRangeException(nameof(projectId));

            ArgumentNullException.ThrowIfNull(project);
            ArgumentNullException.ThrowIfNull(budgetRows);
            ArgumentNullException.ThrowIfNull(group);
            ArgumentNullException.ThrowIfNull(visibleLeaves);
            ArgumentNullException.ThrowIfNull(closure);

            if (budgetRows.Rows.Any(r => r.ProjectId != projectId))
                throw new ArgumentException(
                    "El rowset contiene filas de otro proyecto.", nameof(budgetRows));

            var copiaHojas = visibleLeaves.ToArray();
            if (copiaHojas.Any(h => h is null))
                throw new ArgumentException(
                    "La base de hojas visibles no admite elementos nulos.", nameof(visibleLeaves));
            if (copiaHojas.Select(h => h.ConceptId).Distinct().Count() != copiaHojas.Length)
                throw new ArgumentException(
                    "La base de hojas visibles no admite conceptos repetidos.", nameof(visibleLeaves));

            var filaGrupo = budgetRows.Rows.FirstOrDefault(r => r.ConceptId == group.ConceptId);
            if (filaGrupo is null)
                throw new ArgumentException(
                    "El agrupador esperado no figura en el rowset del presupuesto.", nameof(group));
            if (!filaGrupo.EsAgrupador
                || filaGrupo.Orden != group.Orden
                || filaGrupo.Nivel != group.Nivel
                || filaGrupo.CostoDirectoTotal != group.CostoDirectoTotal
                || filaGrupo.ImporteTotal != group.ImporteTotal)
                throw new ArgumentException(
                    "El agrupador esperado no coincide con la fila del rowset.", nameof(group));

            foreach (var hoja in copiaHojas)
            {
                var fila = budgetRows.Rows.FirstOrDefault(r => r.ConceptId == hoja.ConceptId);
                if (fila is null)
                    throw new ArgumentException(
                        $"La hoja {hoja.ConceptId} no figura en el rowset del presupuesto.",
                        nameof(visibleLeaves));

                if (fila.EsAgrupador
                    || fila.MatrizId != hoja.MatrixId
                    || fila.Cantidad != hoja.Cantidad
                    || fila.CostoDirectoUnitario != hoja.CostoDirectoUnitario
                    || fila.PrecioUnitario != hoja.PrecioUnitario
                    || fila.ImporteTotal != hoja.ImporteTotal)
                    throw new ArgumentException(
                        $"La hoja {hoja.ConceptId} esperada no coincide con la fila del rowset.",
                        nameof(visibleLeaves));
            }

            if (closure.Entries.Any(e => e.Matrix.ProjectId != projectId))
                throw new ArgumentException(
                    "La base del cierre contiene matrices de otro proyecto.", nameof(closure));

            var matricesSeleccionadas = copiaHojas
                .Where(h => h.MatrixId.HasValue)
                .Select(h => h.MatrixId!.Value)
                .Distinct()
                .OrderBy(id => id)
                .ToArray();

            var matricesCierre = closure.Entries
                .Select(e => e.Matrix.MatrixId)
                .ToHashSet();

            var fueraDelCierre = matricesSeleccionadas
                .FirstOrDefault(id => !matricesCierre.Contains(id));
            if (fueraDelCierre != 0)
                throw new ArgumentException(
                    $"La matriz {fueraDelCierre} de una hoja visible no figura en la base del cierre.",
                    nameof(closure));

            ProjectId = projectId;
            Project = project;
            BudgetRows = budgetRows;
            Group = group;
            VisibleLeaves = Array.AsReadOnly(copiaHojas
                .OrderBy(h => h.ConceptId)
                .ToArray());
            Closure = closure;
            SelectedMatrixIds = Array.AsReadOnly(matricesSeleccionadas);
        }

        public int ProjectId { get; }

        /// <summary>Base económica del proyecto con la que la vista calculó lo que muestra.</summary>
        public BudgetProjectCalculationBase Project { get; }

        /// <summary>Rowset COMPLETO de filas persistidas que la vista tenía.</summary>
        public BudgetPriceRefreshRowset BudgetRows { get; }

        /// <summary>Agrupador seleccionado: identidad, posición y totales visibles.</summary>
        public BudgetGroupCostAdjustmentGroupBase Group { get; }

        /// <summary>
        /// Hojas descendientes VISIBLES del agrupador, ordenadas por Id. Incluye las
        /// hojas sin matriz o con matriz sin rubros ajustables: el comando las trata
        /// como costo fijo y jamás las reescribe.
        /// </summary>
        public IReadOnlyList<BudgetGroupCostAdjustmentLeafBase> VisibleLeaves { get; }

        /// <summary>
        /// Unión de los cierres auxiliares de las matrices distintas asignadas a las
        /// hojas visibles. Cada entrada lleva cabecera, fingerprint económico de
        /// componentes y fuentes relevantes.
        /// </summary>
        public BudgetMatrixCostAdjustmentClosureBase Closure { get; }

        /// <summary>Matrices distintas referenciadas por las hojas visibles, por Id.</summary>
        public IReadOnlyList<int> SelectedMatrixIds { get; }
    }

    /// <summary>
    /// Base esperada del agrupador: identidad, posición jerárquica y los dos totales
    /// que la política canónica mantiene iguales al importe de sus hojas visibles.
    /// </summary>
    public sealed class BudgetGroupCostAdjustmentGroupBase
    {
        public BudgetGroupCostAdjustmentGroupBase(
            int conceptId,
            int orden,
            int nivel,
            decimal costoDirectoTotal,
            decimal importeTotal)
        {
            if (conceptId <= 0)
                throw new ArgumentOutOfRangeException(nameof(conceptId));

            ConceptId = conceptId;
            Orden = orden;
            Nivel = nivel;
            CostoDirectoTotal = costoDirectoTotal;
            ImporteTotal = importeTotal;
        }

        public int ConceptId { get; }

        public int Orden { get; }

        public int Nivel { get; }

        public decimal CostoDirectoTotal { get; }

        public decimal ImporteTotal { get; }

        public static BudgetGroupCostAdjustmentGroupBase FromConcepto(ConceptoPresupuesto concepto)
        {
            ArgumentNullException.ThrowIfNull(concepto);

            if (!concepto.EsAgrupador)
                throw new ArgumentException(
                    "El concepto base del reajuste agrupado no es un agrupador.", nameof(concepto));

            return new BudgetGroupCostAdjustmentGroupBase(
                concepto.Id,
                concepto.Orden,
                concepto.Nivel,
                concepto.CostoDirectoTotal,
                concepto.ImporteTotal);
        }
    }

    /// <summary>
    /// Base esperada de una hoja descendiente visible del agrupador. La matriz es
    /// opcional: una hoja sin matriz (o con matriz sin rubros ajustables) es costo
    /// fijo y su importe no se modifica. La cantidad, el costo directo unitario y el
    /// importe permiten detectar una hoja que ya no refleja la matriz vigente.
    /// </summary>
    public sealed class BudgetGroupCostAdjustmentLeafBase
    {
        public BudgetGroupCostAdjustmentLeafBase(
            int conceptId,
            int? matrixId,
            decimal cantidad,
            decimal costoDirectoUnitario,
            decimal precioUnitario,
            decimal importeTotal)
        {
            if (conceptId <= 0)
                throw new ArgumentOutOfRangeException(nameof(conceptId));
            if (matrixId.HasValue && matrixId.Value <= 0)
                throw new ArgumentOutOfRangeException(nameof(matrixId));

            ConceptId = conceptId;
            MatrixId = matrixId;
            Cantidad = cantidad;
            CostoDirectoUnitario = costoDirectoUnitario;
            PrecioUnitario = precioUnitario;
            ImporteTotal = importeTotal;
        }

        public int ConceptId { get; }

        public int? MatrixId { get; }

        public decimal Cantidad { get; }

        public decimal CostoDirectoUnitario { get; }

        public decimal PrecioUnitario { get; }

        public decimal ImporteTotal { get; }

        public static BudgetGroupCostAdjustmentLeafBase FromConcepto(ConceptoPresupuesto concepto)
        {
            ArgumentNullException.ThrowIfNull(concepto);

            if (concepto.EsAgrupador)
                throw new ArgumentException(
                    "La base de hoja visible no admite agrupadores.", nameof(concepto));

            return new BudgetGroupCostAdjustmentLeafBase(
                concepto.Id,
                concepto.MatrizId,
                concepto.Cantidad,
                concepto.CostoDirectoUnitario,
                concepto.PrecioUnitario,
                concepto.ImporteTotal);
        }
    }

    /// <summary>
    /// Intención inmutable de reajustar el IMPORTE TOTAL de un agrupador. Lleva las
    /// bases capturadas antes del diálogo, los rubros autorizados y el importe
    /// objetivo capturado por el usuario. Application deriva el factor, ajusta cada
    /// matriz una sola vez y recalcula el cierre con los servicios canónicos; el
    /// llamador nunca transporta costos, factores ni importes derivados.
    /// </summary>
    public sealed class BudgetGroupCostAdjustmentCommand
    {
        public BudgetGroupCostAdjustmentCommand(
            BudgetGroupCostAdjustmentBases expectedBases,
            MatrixAdjustmentScopes scopes,
            decimal targetAmount)
        {
            ExpectedBases = expectedBases ?? throw new ArgumentNullException(nameof(expectedBases));
            Scopes = scopes;
            TargetAmount = targetAmount;
        }

        /// <summary>Bases capturadas antes del diálogo y revalidadas antes de mutar.</summary>
        public BudgetGroupCostAdjustmentBases ExpectedBases { get; }

        public int ProjectId => ExpectedBases.ProjectId;

        public int GroupConceptId => ExpectedBases.Group.ConceptId;

        public BudgetProjectCalculationBase ExpectedProject => ExpectedBases.Project;

        public BudgetPriceRefreshRowset ExpectedBudgetRows => ExpectedBases.BudgetRows;

        public BudgetGroupCostAdjustmentGroupBase ExpectedGroup => ExpectedBases.Group;

        public IReadOnlyList<BudgetGroupCostAdjustmentLeafBase> ExpectedVisibleLeaves
            => ExpectedBases.VisibleLeaves;

        public BudgetMatrixCostAdjustmentClosureBase ExpectedClosure => ExpectedBases.Closure;

        public IReadOnlyList<int> SelectedMatrixIds => ExpectedBases.SelectedMatrixIds;

        /// <summary>Rubros de las matrices que el usuario autorizó a reajustar.</summary>
        public MatrixAdjustmentScopes Scopes { get; }

        /// <summary>Importe TOTAL objetivo del agrupador, tal como lo capturó el usuario.</summary>
        public decimal TargetAmount { get; }
    }
}
