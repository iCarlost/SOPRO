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
        /// Agrega en lote componentes NUEVOS a una matriz YA persistida del proyecto
        /// (ADR-0001) sobre un contexto NUEVO por operación: revalida la base
        /// económica del proyecto, la matriz, el estado COMPLETO de todos sus
        /// componentes actuales (no solo Id y Orden), la pertenencia y la base de cada
        /// insumo del catálogo y el rowset COMPLETO de filas del presupuesto.
        /// Solo se admiten materiales, mano de obra, maquinaria y herramienta; un
        /// lote con cualquier componente auxiliar (incluidas las cuadrillas que puede
        /// devolver el selector de mano de obra) se rechaza completo, sin recortes ni
        /// sustituciones. La navegación de cada componente se toma del catálogo
        /// fresco, el rendimiento y el orden se derivan con la semántica existente y
        /// los importes, el cierre de matrices auxiliares, las hojas y los agrupadores
        /// se calculan con los servicios canónicos. Un único SaveChanges cierra la
        /// transacción; un conflicto, un ciclo o un fallo dejan la base sin cambios y
        /// el reintento del mismo comando no duplica nada. Un lote vacío es un no-op
        /// que no llama a SaveChanges.
        /// </summary>
        public static BudgetMatrixComponentAdditionResult ExecuteMatrixComponentAdditionCommand(
            IProjectDbContextFactory factory,
            string databasePath,
            BudgetMatrixComponentAdditionCommand command)
        {
            ArgumentNullException.ThrowIfNull(factory);
            if (string.IsNullOrWhiteSpace(databasePath))
                throw new ArgumentException("La ruta de la base del proyecto es requerida.", nameof(databasePath));
            ArgumentNullException.ThrowIfNull(command);

            var execution = BudgetWriteExecutor.Execute(
                factory,
                databasePath,
                useTransaction: true,
                context => StageMatrixComponentAddition(context, command));

            return new BudgetMatrixComponentAdditionResult(
                execution.Result, execution.Confirmed, execution.Diagnostic);
        }

        private static BudgetWriteStep<BudgetMatrixComponentAdditionSnapshot> StageMatrixComponentAddition(
            SOPROContext context,
            BudgetMatrixComponentAdditionCommand command)
        {
            // Rechazo del lote COMPLETO antes de leer o escribir: nunca se recorta un
            // auxiliar ni se cae a otro tipo. Es la salvaguarda para el selector de
            // mano de obra, que también puede devolver cuadrillas (matrices).
            if (command.Additions.Any(a => !IsSupportedAdditionType(a.TipoComponente)))
                return BudgetWriteStep<BudgetMatrixComponentAdditionSnapshot>.Abort(
                    BudgetOperationResult.Failed(
                        "El lote contiene un componente auxiliar o cuadrilla. " +
                        "La adición de auxiliares no está soportada; no se guardó nada."));

            var proyecto = context.Proyectos
                .AsNoTracking()
                .FirstOrDefault(p => p.Id == command.ProjectId);

            if (proyecto == null)
                return BudgetWriteStep<BudgetMatrixComponentAdditionSnapshot>.Abort(
                    BudgetOperationResult.Conflict(
                        $"El proyecto {command.ProjectId} no existe en la base; la vista debe recargarse."));

            if (!MatchesProjectBase(proyecto, command.ExpectedProject))
                return BudgetWriteStep<BudgetMatrixComponentAdditionSnapshot>.Abort(
                    BudgetOperationResult.Conflict(
                        "Los parámetros de cálculo del proyecto cambiaron en la base desde que se cargó; " +
                        "la vista debe recargarse."));

            // Se carga TRACKEADA y con TODAS las navegaciones: la validación, el alta
            // y el recálculo del cierre deben operar sobre las mismas instancias que
            // luego viajan en el único SaveChanges.
            var matriz = PricePropagationService
                .CargarMatricesConNavegaciones(context, new List<int> { command.MatrixId })
                .FirstOrDefault();

            if (matriz == null)
                return BudgetWriteStep<BudgetMatrixComponentAdditionSnapshot>.Abort(
                    BudgetOperationResult.Conflict(
                        $"La matriz {command.MatrixId} ya no existe en la base; la vista debe recargarse."));

            if (matriz.ProyectoId != command.ProjectId)
                return BudgetWriteStep<BudgetMatrixComponentAdditionSnapshot>.Abort(
                    BudgetOperationResult.Conflict(
                        $"La matriz {command.MatrixId} no pertenece al proyecto {command.ProjectId} " +
                        "en la base; la vista debe recargarse."));

            if (!MatchesMatrixBase(matriz, command.ExpectedMatrix))
                return BudgetWriteStep<BudgetMatrixComponentAdditionSnapshot>.Abort(
                    BudgetOperationResult.Conflict(
                        $"La matriz {command.MatrixId} cambió en la base desde que se cargó; " +
                        "la vista debe recargarse."));

            var componentes = matriz.Componentes
                .OrderBy(c => c.Orden)
                .ThenBy(c => c.Id)
                .ToList();

            if (!MatchesComponentStateFingerprint(componentes, command.ExpectedComponents))
                return BudgetWriteStep<BudgetMatrixComponentAdditionSnapshot>.Abort(
                    BudgetOperationResult.Conflict(
                        $"Los componentes de la matriz {command.MatrixId} cambiaron en la base " +
                        "desde que se cargaron; la vista debe recargarse."));

            var filas = LoadOrderedRows(context, command.ProjectId);

            if (!MatchesPriceRefreshRowset(filas, command.ExpectedBudgetRows))
                return BudgetWriteStep<BudgetMatrixComponentAdditionSnapshot>.Abort(
                    BudgetOperationResult.Conflict(
                        "Las filas del presupuesto cambiaron en la base desde que se cargó; " +
                        "la vista debe recargarse."));

            if (command.Additions.Count == 0)
            {
                // No-op real: se materializa la instantánea del estado actual, pero el
                // ejecutor no llama a SaveChanges.
                var snapshotSinCambios = BuildMatrixComponentAdditionSnapshot(
                    context,
                    command,
                    proyecto,
                    matriz,
                    new[] { matriz },
                    Array.Empty<ComponenteMatriz>());

                return BudgetWriteStep<BudgetMatrixComponentAdditionSnapshot>.NoChanges(snapshotSinCambios);
            }

            // Primera pasada: validar TODO el lote (propiedad y base de catálogo)
            // antes de mutar la navegación rastreada.
            var validadas = new List<ValidatedAddition>(command.Additions.Count);

            foreach (var item in command.Additions)
            {
                var error = TryLoadCatalogSource(context, command.ProjectId, item, out var validada);
                if (error != null)
                    return BudgetWriteStep<BudgetMatrixComponentAdditionSnapshot>.Abort(error);

                validadas.Add(validada!);
            }

            var ultimoOrden = componentes.Count == 0
                ? 0
                : Math.Max(componentes.Max(c => c.Orden), 0);

            var nuevos = new List<ComponenteMatriz>(validadas.Count);

            foreach (var validada in validadas)
            {
                var componente = validada.CreateComponent(matriz.Id, ++ultimoOrden);

                // La navegación rastreada es la fuente del cierre: se agrega a la
                // colección de la matriz para que la consulta de matrices afectadas no
                // pierda las filas aún no persistidas.
                context.ComponentesMatriz.Add(componente);
                if (!matriz.Componentes.Contains(componente))
                    matriz.Componentes.Add(componente);

                nuevos.Add(componente);
            }

            // Cierre completo SIN guardar: la matriz con sus altas + ascendientes
            // auxiliares en orden topológico, hojas que referencian cualquiera de esas
            // matrices y agrupadores. Un ciclo alcanzable o una navegación incompleta
            // lanza y el ejecutor clasifica el fallo como revertido, sin escritura.
            var afectadas = PricePropagationService.RecalcularCierreAuxiliarSinGuardar(
                context,
                new[] { matriz.Id },
                command.ProjectId);

            // El comando no pidió conceptos del presupuesto: los efectos derivados
            // (hojas y agrupadores) viajan en la instantánea. Los Ids de las filas
            // nuevas solo existen después del SaveChanges.
            return BudgetWriteStep<BudgetMatrixComponentAdditionSnapshot>.CommitAfterSave(
                () => BuildMatrixComponentAdditionSnapshot(
                    context, command, proyecto, matriz, afectadas, nuevos),
                () => Array.Empty<int>());
        }

        private static BudgetMatrixComponentAdditionSnapshot BuildMatrixComponentAdditionSnapshot(
            SOPROContext context,
            BudgetMatrixComponentAdditionCommand command,
            Proyecto proyecto,
            Matriz matriz,
            IReadOnlyList<Matriz> afectadas,
            IReadOnlyList<ComponenteMatriz> nuevos)
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
                    .Select(BudgetConfirmedMatrixComponentStateRow.FromComponente))
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

            return new BudgetMatrixComponentAdditionSnapshot(
                command.ProjectId,
                BudgetProjectCalculationBase.FromProyecto(proyecto),
                BudgetConfirmedMatrixRow.FromMatriz(matriz),
                nuevos.Select(c => c.Id).ToArray(),
                matricesAfectadas,
                componentesAfectados,
                hojas,
                agrupadores);
        }

        private static bool IsSupportedAdditionType(TipoComponenteMatriz tipo)
            => tipo is TipoComponenteMatriz.Material
                or TipoComponenteMatriz.ManoDeObra
                or TipoComponenteMatriz.Maquinaria
                or TipoComponenteMatriz.Herramienta;

        private static bool MatchesComponentStateFingerprint(
            IReadOnlyList<ComponenteMatriz> actual,
            BudgetMatrixComponentStateFingerprint expected)
        {
            if (actual.Count != expected.Rows.Count)
                return false;

            for (int i = 0; i < actual.Count; i++)
            {
                if (!MatchesComponentBase(actual[i], expected.Rows[i]))
                    return false;
            }

            return true;
        }

        /// <summary>
        /// Relee UN insumo del catálogo fresco y valida pertenencia y base esperada.
        /// Devuelve el error clasificado o null con la fila rastreada lista para
        /// construir el componente. No acepta auxiliares ni busca en otras tablas.
        /// </summary>
        private static BudgetOperationResult? TryLoadCatalogSource(
            SOPROContext context,
            int projectId,
            BudgetMatrixComponentAdditionItem item,
            out ValidatedAddition? validada)
        {
            validada = null;

            switch (item.TipoComponente)
            {
                case TipoComponenteMatriz.Material:
                {
                    var material = context.Materiales.FirstOrDefault(m => m.Id == item.SourceId);
                    if (material == null)
                        return CatalogMissing(item);

                    var actual = BudgetMatrixComponentSourceBase.FromMaterial(material);
                    var error = ValidateCatalogSource(projectId, actual, item.ExpectedSource);
                    if (error != null)
                        return error;

                    validada = new ValidatedAddition(item, material: material);
                    return null;
                }

                case TipoComponenteMatriz.ManoDeObra:
                {
                    var manoDeObra = context.ManoDeObra.FirstOrDefault(m => m.Id == item.SourceId);
                    if (manoDeObra == null)
                        return CatalogMissing(item);

                    var actual = BudgetMatrixComponentSourceBase.FromManoDeObra(manoDeObra);
                    var error = ValidateCatalogSource(projectId, actual, item.ExpectedSource);
                    if (error != null)
                        return error;

                    validada = new ValidatedAddition(item, manoDeObra: manoDeObra);
                    return null;
                }

                case TipoComponenteMatriz.Maquinaria:
                {
                    var maquinaria = context.Maquinaria.FirstOrDefault(m => m.Id == item.SourceId);
                    if (maquinaria == null)
                        return CatalogMissing(item);

                    var actual = BudgetMatrixComponentSourceBase.FromMaquinaria(maquinaria);
                    var error = ValidateCatalogSource(projectId, actual, item.ExpectedSource);
                    if (error != null)
                        return error;

                    validada = new ValidatedAddition(item, maquinaria: maquinaria);
                    return null;
                }

                case TipoComponenteMatriz.Herramienta:
                {
                    var herramienta = context.Herramientas.FirstOrDefault(h => h.Id == item.SourceId);
                    if (herramienta == null)
                        return CatalogMissing(item);

                    var actual = BudgetMatrixComponentSourceBase.FromHerramienta(herramienta);
                    var error = ValidateCatalogSource(projectId, actual, item.ExpectedSource);
                    if (error != null)
                        return error;

                    validada = new ValidatedAddition(item, herramienta: herramienta);
                    return null;
                }

                default:
                    return BudgetOperationResult.Failed(
                        "El lote contiene un componente auxiliar o cuadrilla. " +
                        "La adición de auxiliares no está soportada; no se guardó nada.");
            }
        }

        private static BudgetOperationResult CatalogMissing(BudgetMatrixComponentAdditionItem item)
            => BudgetOperationResult.Conflict(
                $"El insumo {item.TipoComponente} {item.SourceId} ya no existe en el catálogo; " +
                "la vista debe recargarse.");

        private static BudgetOperationResult? ValidateCatalogSource(
            int projectId,
            BudgetMatrixComponentSourceBase actual,
            BudgetMatrixComponentSourceBase expected)
        {
            // Un insumo maestro (ProyectoId nulo) es válido en cualquier proyecto; uno
            // local solo en su proyecto. Nunca se acepta el de otro proyecto.
            if (actual.ProjectId.HasValue && actual.ProjectId.Value != projectId)
                return BudgetOperationResult.Conflict(
                    $"El insumo {actual.TipoComponente} {actual.SourceId} no pertenece al proyecto " +
                    $"{projectId}; la vista debe recargarse.");

            if (!MatchesCatalogSourceBase(actual, expected))
                return BudgetOperationResult.Conflict(
                    $"El insumo {actual.TipoComponente} {actual.SourceId} cambió en el catálogo " +
                    "desde que se cargó; la vista debe recargarse.");

            return null;
        }

        private static bool MatchesCatalogSourceBase(
            BudgetMatrixComponentSourceBase actual,
            BudgetMatrixComponentSourceBase expected)
        {
            return actual.TipoComponente == expected.TipoComponente
                && actual.SourceId == expected.SourceId
                && actual.ProjectId == expected.ProjectId
                && string.Equals(actual.Clave, expected.Clave, StringComparison.Ordinal)
                && string.Equals(actual.Descripcion, expected.Descripcion, StringComparison.Ordinal)
                && string.Equals(actual.Unidad, expected.Unidad, StringComparison.Ordinal)
                && actual.PrecioUnitario == expected.PrecioUnitario
                && actual.EsPorcentajeManoDeObra == expected.EsPorcentajeManoDeObra;
        }

        /// <summary>
        /// Alta ya validada contra el catálogo fresco. Construye el componente con la
        /// navegación canónica y el rendimiento derivado de la semántica existente,
        /// sin importes: el cierre los calcula.
        /// </summary>
        private sealed class ValidatedAddition
        {
            public ValidatedAddition(
                BudgetMatrixComponentAdditionItem item,
                Material? material = null,
                ManoDeObra? manoDeObra = null,
                Maquinaria? maquinaria = null,
                Herramienta? herramienta = null)
            {
                Item = item;
                Material = material;
                ManoDeObra = manoDeObra;
                Maquinaria = maquinaria;
                Herramienta = herramienta;
            }

            public BudgetMatrixComponentAdditionItem Item { get; }

            public Material? Material { get; }

            public ManoDeObra? ManoDeObra { get; }

            public Maquinaria? Maquinaria { get; }

            public Herramienta? Herramienta { get; }

            public ComponenteMatriz CreateComponent(int matrixId, int orden)
            {
                var componente = new ComponenteMatriz
                {
                    MatrizId = matrixId,
                    TipoComponente = Item.TipoComponente,
                    Cantidad = Item.ProposedQuantity,
                    Orden = orden,
                    Notas = Item.Notes
                };

                switch (Item.TipoComponente)
                {
                    case TipoComponenteMatriz.Material:
                        componente.MaterialId = Material!.Id;
                        componente.Material = Material;
                        break;

                    case TipoComponenteMatriz.ManoDeObra:
                        componente.ManoDeObraId = ManoDeObra!.Id;
                        componente.ManoDeObra = ManoDeObra;
                        break;

                    case TipoComponenteMatriz.Maquinaria:
                        componente.MaquinariaId = Maquinaria!.Id;
                        componente.Maquinaria = Maquinaria;
                        break;

                    case TipoComponenteMatriz.Herramienta:
                        componente.HerramientaId = Herramienta!.Id;
                        componente.Herramienta = Herramienta;
                        break;

                    default:
                        throw new InvalidOperationException(
                            $"Tipo de alta no soportado: {Item.TipoComponente}.");
                }

                MatrixComponentEditingService.SyncRendimiento(componente);
                return componente;
            }
        }
    }
}
