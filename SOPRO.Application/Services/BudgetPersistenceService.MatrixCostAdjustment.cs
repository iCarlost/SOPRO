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
        /// Reajusta el costo de UNA matriz del proyecto a partir de un concepto hoja
        /// (ADR-0001) sobre un contexto NUEVO por operación: revalida la base económica
        /// del proyecto, la matriz, el fingerprint ECONÓMICO completo de sus
        /// componentes, las bases de los insumos de catálogo o matrices auxiliares que
        /// intervienen en el cálculo, el rowset COMPLETO del presupuesto y el concepto
        /// hoja seleccionado. Antes de ajustar, revalida también que cada matriz del
        /// cierre auxiliar reproduzca con sus componentes y fuentes frescas el estado
        /// persistido; un ancestro obsoleto es conflicto y no se escribe nada. Deriva el
        /// factor del proyecto validado, convierte el
        /// monto objetivo (P.U. o importe) con la precisión de pantalla vigente y
        /// delega el ajuste en la semántica canónica de
        /// <see cref="MatrixCostAdjustmentService.AdjustMatrixByTargetCost"/> sobre la
        /// matriz rastreada fresca. Después recalcula, sin guardar, la matriz ajustada
        /// y todas sus ascendientes auxiliares en orden topológico, TODAS las hojas
        /// del presupuesto que referencian alguna de esas matrices y todos los
        /// agrupadores con la política única de <see cref="PricePropagationService"/>.
        /// Un único SaveChanges cierra la transacción: un conflicto, un objetivo
        /// inválido, un ciclo o un fallo dejan la base sin cambios y el reintento del
        /// mismo comando no duplica nada.
        /// </summary>
        public static BudgetMatrixCostAdjustmentResult ExecuteMatrixCostAdjustmentCommand(
            IProjectDbContextFactory factory,
            string databasePath,
            BudgetMatrixCostAdjustmentCommand command)
        {
            ArgumentNullException.ThrowIfNull(factory);
            if (string.IsNullOrWhiteSpace(databasePath))
                throw new ArgumentException("La ruta de la base del proyecto es requerida.", nameof(databasePath));
            ArgumentNullException.ThrowIfNull(command);

            var execution = BudgetWriteExecutor.Execute(
                factory,
                databasePath,
                useTransaction: true,
                context => StageMatrixCostAdjustment(context, command));

            return new BudgetMatrixCostAdjustmentResult(
                execution.Result, execution.Confirmed, execution.Diagnostic);
        }

        /// <summary>
        /// Captura PRE-DIÁLOGO la base esperada de todo el cierre ascendente de una
        /// matriz (la seleccionada y sus ancestros auxiliares), con cabecera, costo,
        /// tipo, fingerprint de componentes y fuentes. El llamador debe invocarla con
        /// estado cargado antes de mostrar el diálogo y conservar el resultado dentro
        /// del comando inmutable; el ejecutor la comparará exactamente contra la base.
        /// </summary>
        public static BudgetMatrixCostAdjustmentClosureBase CaptureMatrixCostAdjustmentClosure(
            SOPROContext context,
            int projectId,
            int matrixId)
        {
            ArgumentNullException.ThrowIfNull(context);
            if (projectId <= 0)
                throw new ArgumentOutOfRangeException(nameof(projectId));
            if (matrixId <= 0)
                throw new ArgumentOutOfRangeException(nameof(matrixId));

            var ids = PricePropagationService.CalcularCierreAscendente(
                context, new[] { matrixId }, projectId);

            var matrices = PricePropagationService
                .CargarMatricesConNavegaciones(context, ids.ToList())
                .Where(m => m.ProyectoId == projectId)
                .OrderBy(m => m.Id)
                .ToList();

            if (!matrices.Any(m => m.Id == matrixId))
                throw new InvalidOperationException(
                    $"La matriz {matrixId} no existe en el proyecto {projectId}.");

            return new BudgetMatrixCostAdjustmentClosureBase(
                matrices.Select(BudgetMatrixCostAdjustmentClosureEntry.FromMatriz));
        }

        /// <summary>
        /// Variante de captura sobre un contexto NUEVO: garantiza que el cierre esperado
        /// se lea de la base y no de instancias potencialmente rastreadas o modificadas
        /// por la vista. La captura del origen que el usuario ve puede sustituirse con
        /// <see cref="BudgetMatrixCostAdjustmentClosureBase.WithEntry"/>.
        /// </summary>
        public static BudgetMatrixCostAdjustmentClosureBase LoadMatrixCostAdjustmentClosure(
            IProjectDbContextFactory factory,
            string databasePath,
            int projectId,
            int matrixId)
        {
            ArgumentNullException.ThrowIfNull(factory);
            if (string.IsNullOrWhiteSpace(databasePath))
                throw new ArgumentException("La ruta de la base del proyecto es requerida.", nameof(databasePath));

            using var context = factory.Create(databasePath);
            return CaptureMatrixCostAdjustmentClosure(context, projectId, matrixId);
        }

        private static BudgetWriteStep<BudgetMatrixCostAdjustmentSnapshot> StageMatrixCostAdjustment(
            SOPROContext context,
            BudgetMatrixCostAdjustmentCommand command)
        {
            // La intención se rechaza completa antes de leer o mutar nada: un alcance
            // vacío o con bits desconocidos, un tipo de objetivo no definido o un
            // monto no positivo no son conflictos de base sino comandos inválidos.
            if (!IsSupportedAdjustmentScopes(command.Scopes))
                return BudgetWriteStep<BudgetMatrixCostAdjustmentSnapshot>.Abort(
                    BudgetOperationResult.Failed(
                        "El alcance del reajuste está vacío o contiene rubros no soportados; " +
                        "no se guardó nada."));

            if (!Enum.IsDefined(command.TargetKind))
                return BudgetWriteStep<BudgetMatrixCostAdjustmentSnapshot>.Abort(
                    BudgetOperationResult.Failed(
                        "El tipo de monto objetivo no es válido; no se guardó nada."));

            if (command.TargetValue <= 0m)
                return BudgetWriteStep<BudgetMatrixCostAdjustmentSnapshot>.Abort(
                    BudgetOperationResult.Failed(
                        "El monto objetivo debe ser mayor que cero; no se guardó nada."));

            var proyecto = context.Proyectos
                .AsNoTracking()
                .FirstOrDefault(p => p.Id == command.ProjectId);

            if (proyecto == null)
                return BudgetWriteStep<BudgetMatrixCostAdjustmentSnapshot>.Abort(
                    BudgetOperationResult.Conflict(
                        $"El proyecto {command.ProjectId} no existe en la base; la vista debe recargarse."));

            if (!MatchesProjectBase(proyecto, command.ExpectedProject))
                return BudgetWriteStep<BudgetMatrixCostAdjustmentSnapshot>.Abort(
                    BudgetOperationResult.Conflict(
                        "Los parámetros de cálculo del proyecto cambiaron en la base desde que se cargó; " +
                        "la vista debe recargarse."));

            // Se carga TRACKEADA y con TODAS las navegaciones: la validación, el
            // reajuste y el recálculo del cierre deben operar sobre las mismas
            // instancias que luego viajan en el único SaveChanges.
            var matriz = PricePropagationService
                .CargarMatricesConNavegaciones(context, new List<int> { command.MatrixId })
                .FirstOrDefault();

            if (matriz == null)
                return BudgetWriteStep<BudgetMatrixCostAdjustmentSnapshot>.Abort(
                    BudgetOperationResult.Conflict(
                        $"La matriz {command.MatrixId} ya no existe en la base; la vista debe recargarse."));

            if (matriz.ProyectoId != command.ProjectId)
                return BudgetWriteStep<BudgetMatrixCostAdjustmentSnapshot>.Abort(
                    BudgetOperationResult.Conflict(
                        $"La matriz {command.MatrixId} no pertenece al proyecto {command.ProjectId} " +
                        "en la base; la vista debe recargarse."));

            var filas = LoadOrderedRows(context, command.ProjectId);

            if (!MatchesPriceRefreshRowset(filas, command.ExpectedBudgetRows))
                return BudgetWriteStep<BudgetMatrixCostAdjustmentSnapshot>.Abort(
                    BudgetOperationResult.Conflict(
                        "Las filas del presupuesto cambiaron en la base desde que se cargó; " +
                        "la vista debe recargarse."));

            var leaf = filas.FirstOrDefault(f => f.Id == command.ExpectedLeaf.ConceptId);

            if (leaf == null)
                return BudgetWriteStep<BudgetMatrixCostAdjustmentSnapshot>.Abort(
                    BudgetOperationResult.Conflict(
                        $"El concepto {command.ExpectedLeaf.ConceptId} ya no existe en el presupuesto; " +
                        "la vista debe recargarse."));

            if (leaf.EsAgrupador
                || leaf.MatrizId != command.MatrixId
                || leaf.Cantidad != command.ExpectedLeaf.Cantidad)
                return BudgetWriteStep<BudgetMatrixCostAdjustmentSnapshot>.Abort(
                    BudgetOperationResult.Conflict(
                        $"El concepto {command.ExpectedLeaf.ConceptId} cambió en la base desde que se cargó; " +
                        "la vista debe recargarse."));

            var factor = BudgetPricingService.CalculateFactor(proyecto);
            if (factor <= 0m)
                return BudgetWriteStep<BudgetMatrixCostAdjustmentSnapshot>.Abort(
                    BudgetOperationResult.Failed(
                        "No se pudo calcular el factor del precio unitario del proyecto; no se guardó nada."));

            if (!TryConvertTargetToDirectCost(
                    proyecto, leaf, command.TargetKind, command.TargetValue, factor,
                    out var targetCd, out var errorObjetivo))
                return BudgetWriteStep<BudgetMatrixCostAdjustmentSnapshot>.Abort(
                    BudgetOperationResult.Failed(errorObjetivo + " No se guardó nada."));

            // Revalidación de la base VISTA de TODO el cierre ascendente antes de
            // mutar: proyecto, cada matriz (costo, tipo, propiedad), fingerprint
            // económico de sus componentes, fuentes relevantes y rowset. Un cambio
            // coherente hecho fuera de la vista también invalida la intención.
            var errorCierre = ValidateExpectedClosure(
                command.ExpectedClosure,
                context,
                proyecto,
                new[] { matriz.Id },
                command.ProjectId);
            if (errorCierre != null)
                return BudgetWriteStep<BudgetMatrixCostAdjustmentSnapshot>.Abort(errorCierre);

            // Semántica canónica única del reajuste: alcances, mínimo alcanzable,
            // búsqueda del factor y redondeo del motor del proyecto. Opera sobre la
            // matriz rastreada fresca; no toca catálogos ni guarda.
            var ajuste = MatrixCostAdjustmentService.AdjustMatrixByTargetCost(
                matriz, proyecto, command.Scopes, targetCd);

            if (!ajuste.Success)
                return BudgetWriteStep<BudgetMatrixCostAdjustmentSnapshot>.Abort(
                    BudgetOperationResult.Failed(
                        string.IsNullOrWhiteSpace(ajuste.Message)
                            ? "No se pudo reajustar la matriz; no se guardó nada."
                            : ajuste.Message + " No se guardó nada."));

            // Cierre completo SIN guardar: matriz reajustada + ascendientes auxiliares
            // en orden topológico, hojas que referencian cualquiera de ellas y
            // agrupadores. Un ciclo alcanzable o una navegación incompleta lanza y el
            // ejecutor clasifica el fallo como revertido, sin escritura.
            var afectadas = PricePropagationService.RecalcularCierreAuxiliarSinGuardar(
                context,
                new[] { matriz.Id },
                command.ProjectId);

            var snapshot = BuildMatrixCostAdjustmentSnapshot(
                context, command, proyecto, matriz, afectadas, ajuste, leaf);

            // El comando no pidió conceptos del presupuesto: los efectos derivados
            // (hojas y agrupadores) viajan en la instantánea.
            return BudgetWriteStep<BudgetMatrixCostAdjustmentSnapshot>.Commit(
                snapshot, Array.Empty<int>());
        }

        private static bool IsSupportedAdjustmentScopes(MatrixAdjustmentScopes scopes)
        {
            const MatrixAdjustmentScopes soportados =
                MatrixAdjustmentScopes.Materiales
                | MatrixAdjustmentScopes.ManoDeObra
                | MatrixAdjustmentScopes.Maquinaria
                | MatrixAdjustmentScopes.Herramienta;

            return scopes != MatrixAdjustmentScopes.None
                && (scopes & ~soportados) == MatrixAdjustmentScopes.None;
        }

        /// <summary>
        /// Convierte el monto objetivo capturado a costo directo con el factor vigente
        /// del proyecto validado. El objetivo de importe se expresa primero como P.U.
        /// con la precisión de pantalla (<see cref="SoproCalculationEngine.RoundAmount"/>)
        /// y luego se divide por el factor; el de P.U. se divide directamente.
        /// </summary>
        private static bool TryConvertTargetToDirectCost(
            Proyecto proyecto,
            ConceptoPresupuesto leaf,
            BudgetMatrixCostAdjustmentTargetKind targetKind,
            decimal targetValue,
            decimal factor,
            out decimal targetCost,
            out string? error)
        {
            targetCost = 0m;
            error = null;

            var engine = CalculationEngineFactory.FromProyecto(proyecto);

            decimal targetUnitPrice;
            switch (targetKind)
            {
                case BudgetMatrixCostAdjustmentTargetKind.PrecioUnitario:
                    targetUnitPrice = engine.RoundAmount(targetValue);
                    break;

                case BudgetMatrixCostAdjustmentTargetKind.Importe:
                    if (leaf.Cantidad <= 0m)
                    {
                        error = "La cantidad del concepto debe ser mayor que cero para reajustar por importe.";
                        return false;
                    }

                    targetUnitPrice = engine.RoundAmount(targetValue / leaf.Cantidad);
                    break;

                default:
                    error = "El tipo de monto objetivo no es válido.";
                    return false;
            }

            targetCost = targetUnitPrice / factor;
            return true;
        }

        /// <summary>
        /// Revalida la base esperada capturada antes del diálogo contra la base fresca
        /// de TODO el cierre ascendente, antes de mutar: conjunto EXACTO de matrices,
        /// cabecera/costo/tipo/propiedad, fingerprint económico de componentes y fuentes
        /// relevantes (pertenencia, valor, %MO y tipo de auxiliar). Un cambio coherente
        /// hecho fuera de la vista también es conflicto. Después exige que cada matriz
        /// reproduzca con esos datos su estado persistido, para no recalcular una matriz
        /// internamente inconsistente que el usuario tampoco vio.
        /// </summary>
        private static BudgetOperationResult? ValidateExpectedClosure(
            BudgetMatrixCostAdjustmentClosureBase expectedClosure,
            SOPROContext context,
            Proyecto proyecto,
            IReadOnlyCollection<int> originMatrixIds,
            int projectId)
        {
            ArgumentNullException.ThrowIfNull(expectedClosure);
            ArgumentNullException.ThrowIfNull(originMatrixIds);

            var entradasEsperadas = expectedClosure.Entries;
            var idsEsperados = entradasEsperadas
                .Select(e => e.Matrix.MatrixId)
                .ToHashSet();

            var idsFrescos = PricePropagationService.CalcularCierreAscendente(
                context, originMatrixIds, projectId);

            if (!idsFrescos.SetEquals(idsEsperados))
            {
                var etiquetaOrigen = originMatrixIds.Count == 1
                    ? $"de la matriz {originMatrixIds.First()}"
                    : "de las matrices del reajuste";

                return BudgetOperationResult.Conflict(
                    $"El cierre auxiliar {etiquetaOrigen} cambió en la base desde que se cargó; " +
                    "la vista debe recargarse.");
            }

            var matricesFrescas = PricePropagationService
                .CargarMatricesConNavegaciones(context, idsFrescos.ToList())
                .Where(m => m.ProyectoId == projectId)
                .ToDictionary(m => m.Id);

            var origenFaltante = originMatrixIds
                .Where(id => !matricesFrescas.ContainsKey(id))
                .OrderBy(id => id)
                .FirstOrDefault();

            if (origenFaltante != 0)
                return BudgetOperationResult.Conflict(
                    $"La matriz {origenFaltante} ya no participa del cierre del proyecto {projectId}; " +
                    "la vista debe recargarse.");

            var engine = CalculationEngineFactory.FromProyecto(proyecto);

            foreach (var entrada in entradasEsperadas)
            {
                if (!matricesFrescas.TryGetValue(entrada.Matrix.MatrixId, out var matriz))
                    return BudgetOperationResult.Conflict(
                        $"La matriz {entrada.Matrix.MatrixId} del cierre ya no existe en la base; " +
                        "la vista debe recargarse.");

                if (!MatchesMatrixBase(matriz, entrada.Matrix))
                    return BudgetOperationResult.Conflict(
                        $"La matriz {matriz.Id} del cierre cambió en la base desde que se cargó; " +
                        "la vista debe recargarse.");

                var componentes = matriz.Componentes
                    .OrderBy(c => c.Orden)
                    .ThenBy(c => c.Id)
                    .ToList();

                if (!MatchesComponentStateFingerprint(componentes, entrada.Components))
                    return BudgetOperationResult.Conflict(
                        $"Los componentes de la matriz {matriz.Id} del cierre cambiaron en la base " +
                        "desde que se cargaron; la vista debe recargarse.");

                var errorFuentes = ValidateMatrixSources(
                    componentes, entrada.Sources, projectId, matriz.Id);
                if (errorFuentes != null)
                    return errorFuentes;

                // Coherencia interna con las MISMAS fuentes ya validadas: si la matriz
                // persistida no reproduce sus importes/costo, recalcularla introduciría
                // cambios ajenos al reajuste.
                var costoSincronizado = matriz.CostoDirecto;
                var importesSincronizados = componentes.ToDictionary(c => c, c => c.Importe);

                var totales = MatrixComponentCalculationService.Recalculate(
                    componentes, proyecto.DecimalesImporte);

                var costoFresco = engine.RoundAmount(totales.CostoDirectoTotal);
                var componentesSinCambios = componentes.All(
                    c => c.Importe == importesSincronizados[c]);

                // La validación no debe alterar la instantánea ni el punto de partida
                // del reajuste: se restituyen los importes rastreados.
                foreach (var sincronizado in importesSincronizados)
                    sincronizado.Key.Importe = sincronizado.Value;

                if (!componentesSinCambios || costoFresco != costoSincronizado)
                    return BudgetOperationResult.Conflict(
                        $"La matriz {matriz.Id} del cierre no reproduce su estado persistido; " +
                        "la vista debe recargarse.");
            }

            return null;
        }

        /// <summary>
        /// Compara las fuentes frescas de UNA matriz del cierre con las esperadas: cada
        /// componente debe conservar su fuente, pertenencia y valor exacto. La cobertura
        /// exacta ya la garantiza la entrada del cierre.
        /// </summary>
        private static BudgetOperationResult? ValidateMatrixSources(
            IReadOnlyList<ComponenteMatriz> componentes,
            IReadOnlyList<BudgetMatrixComponentSourceBase> esperadas,
            int projectId,
            int matrixId)
        {
            var porClave = esperadas.ToDictionary(s => (s.TipoComponente, s.SourceId));

            foreach (var componente in componentes)
            {
                var actual = BuildActualSource(componente, out var error);
                if (actual == null)
                    return error;

                if (actual.ProjectId.HasValue && actual.ProjectId.Value != projectId)
                    return BudgetOperationResult.Conflict(
                        $"El insumo {actual.TipoComponente} {actual.SourceId} de la matriz {matrixId} " +
                        $"no pertenece al proyecto {projectId}; la vista debe recargarse.");

                if (!porClave.TryGetValue((actual.TipoComponente, actual.SourceId), out var esperada))
                    return BudgetOperationResult.Conflict(
                        $"La base del insumo {actual.TipoComponente} {actual.SourceId} de la matriz " +
                        $"{matrixId} no fue informada; la vista debe recargarse.");

                if (!MatchesCatalogSourceBase(actual, esperada))
                    return BudgetOperationResult.Conflict(
                        $"El insumo {actual.TipoComponente} {actual.SourceId} de la matriz {matrixId} " +
                        "cambió en la base desde que se cargó; la vista debe recargarse.");
            }

            return null;
        }

        private static BudgetMatrixComponentSourceBase? BuildActualSource(
            ComponenteMatriz componente,
            out BudgetOperationResult? error)
        {
            error = null;

            switch (componente.TipoComponente)
            {
                case TipoComponenteMatriz.Material:
                    if (componente.Material == null)
                        return MissingSource(componente, out error);
                    return BudgetMatrixComponentSourceBase.FromMaterial(componente.Material);

                case TipoComponenteMatriz.ManoDeObra:
                    if (componente.ManoDeObra == null)
                        return MissingSource(componente, out error);
                    return BudgetMatrixComponentSourceBase.FromManoDeObra(componente.ManoDeObra);

                case TipoComponenteMatriz.Maquinaria:
                    if (componente.Maquinaria == null)
                        return MissingSource(componente, out error);
                    return BudgetMatrixComponentSourceBase.FromMaquinaria(componente.Maquinaria);

                case TipoComponenteMatriz.Herramienta:
                    if (componente.Herramienta == null)
                        return MissingSource(componente, out error);
                    return BudgetMatrixComponentSourceBase.FromHerramienta(componente.Herramienta);

                case TipoComponenteMatriz.Auxiliar:
                    if (componente.Auxiliar == null)
                        return MissingSource(componente, out error);
                    return BudgetMatrixComponentSourceBase.FromAuxiliar(componente.Auxiliar);

                default:
                    error = BudgetOperationResult.Failed(
                        $"El componente {componente.Id} tiene un tipo no soportado; no se guardó nada.");
                    return null;
            }
        }

        private static BudgetMatrixComponentSourceBase? MissingSource(
            ComponenteMatriz componente,
            out BudgetOperationResult? error)
        {
            error = BudgetOperationResult.Conflict(
                $"El insumo del componente {componente.Id} ya no existe en la base; " +
                "la vista debe recargarse.");
            return null;
        }

        private static BudgetMatrixCostAdjustmentSnapshot BuildMatrixCostAdjustmentSnapshot(
            SOPROContext context,
            BudgetMatrixCostAdjustmentCommand command,
            Proyecto proyecto,
            Matriz matriz,
            IReadOnlyList<Matriz> afectadas,
            MatrixCostAdjustmentResult ajuste,
            ConceptoPresupuesto leaf)
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

            var currentTargetValue = ToDisplayedTargetValue(
                proyecto, leaf.Cantidad, ajuste.CurrentCost, command.TargetKind);
            var achievedTargetValue = ToDisplayedTargetValue(
                proyecto, leaf.Cantidad, ajuste.AchievedCost, command.TargetKind);

            return new BudgetMatrixCostAdjustmentSnapshot(
                command.ProjectId,
                BudgetProjectCalculationBase.FromProyecto(proyecto),
                BudgetConfirmedMatrixRow.FromMatriz(matriz),
                matricesAfectadas,
                componentesAfectados,
                hojas,
                agrupadores,
                leaf.Id,
                command.TargetKind,
                command.TargetValue,
                currentTargetValue,
                achievedTargetValue,
                ajuste.CurrentCost,
                ajuste.TargetCost,
                ajuste.AchievedCost,
                ajuste.FactorApplied);
        }

        private static decimal ToDisplayedTargetValue(
            Proyecto proyecto,
            decimal cantidad,
            decimal costoDirecto,
            BudgetMatrixCostAdjustmentTargetKind targetKind)
        {
            var precioUnitario = BudgetPricingService.CalculateUnitPrice(proyecto, costoDirecto);

            return targetKind == BudgetMatrixCostAdjustmentTargetKind.PrecioUnitario
                ? precioUnitario
                : BudgetPricingService.MultiplyUsingDisplayPrecision(proyecto, cantidad, precioUnitario);
        }
    }
}
