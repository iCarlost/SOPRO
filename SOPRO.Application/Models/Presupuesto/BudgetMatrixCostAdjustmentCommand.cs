using SOPRO.Application.Services;
using SOPRO.Core.Entities;

namespace SOPRO.Application.Models.Presupuesto
{
    /// <summary>
    /// Naturaleza del monto objetivo del reajuste: el P.U. mostrado del concepto
    /// hoja o su importe total. Application convierte el valor a costo directo con
    /// el factor vigente del proyecto; el llamador nunca envía ese costo directo.
    /// </summary>
    public enum BudgetMatrixCostAdjustmentTargetKind
    {
        /// <summary>Objetivo expresado como Precio Unitario del concepto hoja.</summary>
        PrecioUnitario = 0,

        /// <summary>Alias de <see cref="PrecioUnitario"/> (terminología de la UI).</summary>
        PU = PrecioUnitario,

        /// <summary>Objetivo expresado como Importe Total del concepto hoja.</summary>
        Importe = 1
    }

    /// <summary>
    /// Intención de reajuste de costo de UNA matriz a partir de un concepto hoja del
    /// presupuesto (ADR-0001). Lleva identidad, la base esperada del proyecto, la base
    /// esperada de TODO el cierre ascendente (cada matriz con costo/tipo/propiedad,
    /// fingerprint ECONÓMICO completo de sus componentes y fuentes relevantes), el
    /// rowset COMPLETO del presupuesto, el concepto hoja seleccionado, los rubros a
    /// ajustar y el monto objetivo (P.U. o importe). No transporta costo directo
    /// objetivo, factor ni importes derivados: los calcula Application con los
    /// servicios canónicos. Todos los valores son de solo lectura para que un
    /// reintento repita exactamente la misma operación.
    /// </summary>
    /// <remarks>
    /// Captura pre-diálogo sugerida para la vista:
    /// <c>var cierre = BudgetPersistenceService.LoadMatrixCostAdjustmentClosure(factory, ruta, proyectoId, matrizId);</c>
    /// y, si el origen en pantalla debe mandar sobre lo leído,
    /// <c>cierre = cierre.WithEntry(BudgetMatrixCostAdjustmentClosureEntry.FromMatriz(matrizVista));</c>.
    /// El comando se construye con ese cierre completo en lugar de la matriz/fuentes sueltas.
    /// </remarks>
    public sealed class BudgetMatrixCostAdjustmentCommand
    {
        public BudgetMatrixCostAdjustmentCommand(
            int projectId,
            BudgetProjectCalculationBase expectedProject,
            BudgetMatrixCostAdjustmentClosureBase expectedClosure,
            BudgetPriceRefreshRowset expectedBudgetRows,
            BudgetMatrixCostAdjustmentLeaf expectedLeaf,
            MatrixAdjustmentScopes scopes,
            BudgetMatrixCostAdjustmentTargetKind targetKind,
            decimal targetValue)
        {
            if (projectId <= 0)
                throw new ArgumentOutOfRangeException(nameof(projectId));

            ArgumentNullException.ThrowIfNull(expectedProject);
            ArgumentNullException.ThrowIfNull(expectedClosure);
            ArgumentNullException.ThrowIfNull(expectedBudgetRows);
            ArgumentNullException.ThrowIfNull(expectedLeaf);

            if (expectedClosure.Entries.Any(e => e.Matrix.ProjectId != projectId))
                throw new ArgumentException(
                    "La base del cierre contiene matrices de otro proyecto.", nameof(expectedClosure));

            var entradaOrigen = expectedClosure.Entries
                .FirstOrDefault(e => e.Matrix.MatrixId == expectedLeaf.MatrixId);

            if (entradaOrigen is null)
                throw new ArgumentException(
                    "La base del cierre no incluye la matriz del concepto hoja.", nameof(expectedClosure));

            if (expectedBudgetRows.Rows.Any(r => r.ProjectId != projectId))
                throw new ArgumentException(
                    "El rowset contiene filas de otro proyecto.", nameof(expectedBudgetRows));

            var filaHoja = expectedBudgetRows.Rows
                .FirstOrDefault(r => r.ConceptId == expectedLeaf.ConceptId);

            if (filaHoja is null)
                throw new ArgumentException(
                    "El concepto hoja esperado no figura en el rowset del presupuesto.", nameof(expectedLeaf));

            if (filaHoja.EsAgrupador
                || filaHoja.MatrizId != expectedLeaf.MatrixId
                || filaHoja.Cantidad != expectedLeaf.Cantidad)
                throw new ArgumentException(
                    "El concepto hoja esperado no coincide con la fila del rowset.", nameof(expectedLeaf));

            ProjectId = projectId;
            ExpectedProject = expectedProject;
            ExpectedClosure = expectedClosure;
            ExpectedMatrix = entradaOrigen.Matrix;
            ExpectedComponents = entradaOrigen.Components;
            ExpectedSources = entradaOrigen.Sources;
            ExpectedBudgetRows = expectedBudgetRows;
            ExpectedLeaf = expectedLeaf;
            Scopes = scopes;
            TargetKind = targetKind;
            TargetValue = targetValue;
        }

        public int ProjectId { get; }

        public int MatrixId => ExpectedMatrix.MatrixId;

        public int ConceptId => ExpectedLeaf.ConceptId;

        /// <summary>Base económica del proyecto con la que la vista calculó lo que muestra.</summary>
        public BudgetProjectCalculationBase ExpectedProject { get; }

        /// <summary>
        /// Base esperada de TODO el cierre ascendente que el comando recalculará: la
        /// matriz seleccionada y todos sus ancestros auxiliares, cada uno con cabecera,
        /// fingerprint económico de componentes y fuentes relevantes. Application exige
        /// igualdad exacta contra la base fresca antes de mutar.
        /// </summary>
        public BudgetMatrixCostAdjustmentClosureBase ExpectedClosure { get; }

        /// <summary>Base de la matriz que la vista cargó antes del reajuste.</summary>
        public BudgetMatrixCalculationBase ExpectedMatrix { get; }

        /// <summary>
        /// Estado económico COMPLETO de TODOS los componentes persistidos de la matriz
        /// (Id, tipo, referencias, cantidad, rendimiento, importe y orden). Una fila
        /// añadida, eliminada, reordenada o con cualquier valor distinto en la base
        /// invalida el reajuste.
        /// </summary>
        public BudgetMatrixComponentStateFingerprint ExpectedComponents { get; }

        /// <summary>
        /// Base de cada insumo de catálogo o matriz auxiliar que interviene en el
        /// cálculo de la matriz, en una colección inmutable. Se cubren exactamente las
        /// fuentes referenciadas por <see cref="ExpectedComponents"/>.
        /// </summary>
        public IReadOnlyList<BudgetMatrixComponentSourceBase> ExpectedSources { get; }

        /// <summary>Filas persistidas que la vista tenía del presupuesto, en el orden de la base.</summary>
        public BudgetPriceRefreshRowset ExpectedBudgetRows { get; }

        /// <summary>Concepto hoja seleccionado: identidad, matriz y cantidad esperadas.</summary>
        public BudgetMatrixCostAdjustmentLeaf ExpectedLeaf { get; }

        /// <summary>Rubros de la matriz que el usuario autorizó a reajustar.</summary>
        public MatrixAdjustmentScopes Scopes { get; }

        /// <summary>Naturaleza del monto objetivo.</summary>
        public BudgetMatrixCostAdjustmentTargetKind TargetKind { get; }

        /// <summary>Monto objetivo tal como lo capturó el usuario (P.U. o importe).</summary>
        public decimal TargetValue { get; }
    }

    /// <summary>
    /// Base esperada de TODO el cierre ascendente que un reajuste puede recalcular:
    /// la matriz seleccionada y todos sus ancestros auxiliares. Capturada antes del
    /// diálogo; Application exige el conjunto EXACTO de matrices y, por cada una,
    /// cabecera, fingerprint económico de componentes y fuentes relevantes. Así un
    /// cambio coherente hecho fuera de la vista (componente y matriz a la vez) también
    /// es conflicto, no una base válida.
    /// </summary>
    public sealed class BudgetMatrixCostAdjustmentClosureBase
    {
        public BudgetMatrixCostAdjustmentClosureBase(
            IEnumerable<BudgetMatrixCostAdjustmentClosureEntry> entries)
            : this(entries, exigirAlMenosUna: true)
        {
        }

        private BudgetMatrixCostAdjustmentClosureBase(
            IEnumerable<BudgetMatrixCostAdjustmentClosureEntry> entries,
            bool exigirAlMenosUna)
        {
            ArgumentNullException.ThrowIfNull(entries);

            var copia = entries.ToArray();
            if (exigirAlMenosUna && copia.Length == 0)
                throw new ArgumentException(
                    "La base del cierre debe incluir al menos la matriz seleccionada.", nameof(entries));
            if (copia.Any(e => e is null))
                throw new ArgumentException(
                    "La base del cierre no admite entradas nulas.", nameof(entries));

            var ids = new HashSet<int>();
            foreach (var entrada in copia)
            {
                if (!ids.Add(entrada.Matrix.MatrixId))
                    throw new ArgumentException(
                        "La base del cierre no admite matrices repetidas.", nameof(entries));
            }

            Entries = Array.AsReadOnly(copia
                .OrderBy(e => e.Matrix.MatrixId)
                .ToArray());
        }

        /// <summary>
        /// Cierre sin matrices. Solo lo usa la captura de un agrupador cuyas hojas
        /// visibles no tienen matriz asignada: el comando clasifica la operación como
        /// fallo sin escribir, pero la vista puede construir el comando y mostrar el
        /// mensaje correspondiente.
        /// </summary>
        internal static BudgetMatrixCostAdjustmentClosureBase Vacio { get; } =
            new(Array.Empty<BudgetMatrixCostAdjustmentClosureEntry>(), exigirAlMenosUna: false);

        /// <summary>Entradas del cierre ordenadas por Id de matriz, en colección inmutable.</summary>
        public IReadOnlyList<BudgetMatrixCostAdjustmentClosureEntry> Entries { get; }

        public BudgetMatrixCostAdjustmentClosureEntry? FindEntry(int matrixId)
            => Entries.FirstOrDefault(e => e.Matrix.MatrixId == matrixId);

        /// <summary>
        /// Devuelve una base nueva con la entrada indicada reemplazando la de su misma
        /// matriz o agregándola. La vista la usa para sustituir el origen cargado en
        /// pantalla sobre el cierre leído de la base antes del diálogo.
        /// </summary>
        public BudgetMatrixCostAdjustmentClosureBase WithEntry(
            BudgetMatrixCostAdjustmentClosureEntry entry)
        {
            ArgumentNullException.ThrowIfNull(entry);

            return new BudgetMatrixCostAdjustmentClosureBase(
                Entries
                    .Where(e => e.Matrix.MatrixId != entry.Matrix.MatrixId)
                    .Append(entry));
        }
    }

    /// <summary>
    /// Base esperada de UNA matriz del cierre: cabecera y costo directo, fingerprint
    /// económico completo de sus componentes y las fuentes de catálogo o matrices
    /// auxiliares que intervienen en su cálculo, cada una con pertenencia, valor,
    /// porcentaje de mano de obra y, si es auxiliar, tipo de matriz.
    /// </summary>
    public sealed class BudgetMatrixCostAdjustmentClosureEntry
    {
        public BudgetMatrixCostAdjustmentClosureEntry(
            BudgetMatrixCalculationBase matrix,
            BudgetMatrixComponentStateFingerprint components,
            IEnumerable<BudgetMatrixComponentSourceBase> sources)
        {
            ArgumentNullException.ThrowIfNull(matrix);
            ArgumentNullException.ThrowIfNull(components);
            ArgumentNullException.ThrowIfNull(sources);

            if (components.Rows.Any(r => r.MatrixId != matrix.MatrixId))
                throw new ArgumentException(
                    "El fingerprint contiene componentes de otra matriz.", nameof(components));

            var fuentes = sources.ToArray();
            if (fuentes.Any(f => f is null))
                throw new ArgumentException(
                    "La base de fuentes no admite elementos nulos.", nameof(sources));

            var clavesFuentes = new HashSet<(TipoComponenteMatriz Tipo, int Id)>();
            foreach (var fuente in fuentes)
            {
                if (!clavesFuentes.Add((fuente.TipoComponente, fuente.SourceId)))
                    throw new ArgumentException(
                        "La base de fuentes no admite insumos repetidos.", nameof(sources));

                if (matrix.ProjectId.HasValue
                    && fuente.ProjectId.HasValue
                    && fuente.ProjectId.Value != matrix.ProjectId.Value)
                    throw new ArgumentException(
                        "La base de fuentes contiene un insumo de otro proyecto.", nameof(sources));
            }

            var clavesComponentes = new HashSet<(TipoComponenteMatriz Tipo, int Id)>();
            foreach (var row in components.Rows)
                clavesComponentes.Add(SourceKeyOf(row));

            if (!clavesFuentes.SetEquals(clavesComponentes))
                throw new ArgumentException(
                    "La base de fuentes debe cubrir exactamente las fuentes de los componentes de la matriz.",
                    nameof(sources));

            Matrix = matrix;
            Components = components;
            Sources = Array.AsReadOnly(fuentes
                .OrderBy(f => f.TipoComponente)
                .ThenBy(f => f.SourceId)
                .ToArray());
        }

        public BudgetMatrixCalculationBase Matrix { get; }

        public BudgetMatrixComponentStateFingerprint Components { get; }

        public IReadOnlyList<BudgetMatrixComponentSourceBase> Sources { get; }

        /// <summary>
        /// Captura la base de una matriz con sus navegaciones cargadas. Es la única
        /// traducción componente→fuente, compartida por la captura y las pruebas.
        /// </summary>
        public static BudgetMatrixCostAdjustmentClosureEntry FromMatriz(Matriz matriz)
        {
            ArgumentNullException.ThrowIfNull(matriz);

            var componentes = matriz.Componentes
                .OrderBy(c => c.Orden)
                .ThenBy(c => c.Id)
                .ToList();

            return new BudgetMatrixCostAdjustmentClosureEntry(
                BudgetMatrixCalculationBase.FromMatriz(matriz),
                BudgetMatrixComponentStateFingerprint.FromComponentes(componentes),
                componentes.Select(BudgetMatrixComponentSourceBase.FromComponente));
        }

        private static (TipoComponenteMatriz Tipo, int Id) SourceKeyOf(
            BudgetMatrixComponentQuantityBase row)
        {
            var id = row.TipoComponente switch
            {
                TipoComponenteMatriz.Material => row.MaterialId,
                TipoComponenteMatriz.ManoDeObra => row.ManoDeObraId,
                TipoComponenteMatriz.Maquinaria => row.MaquinariaId,
                TipoComponenteMatriz.Auxiliar => row.AuxiliarId,
                TipoComponenteMatriz.Herramienta => row.HerramientaId,
                _ => null
            };

            if (!id.HasValue || id.Value <= 0)
                throw new ArgumentException(
                    "El fingerprint contiene un componente sin fuente válida.",
                    nameof(row));

            return (row.TipoComponente, id.Value);
        }
    }

    /// <summary>
    /// Base esperada del concepto hoja seleccionado: identidad, matriz asignada y
    /// cantidad con la que la vista convirtió un objetivo de importe a P.U. Si la
    /// cantidad o la matriz cambiaron en la base, el objetivo ya no representa lo
    /// que el usuario vio.
    /// </summary>
    public sealed class BudgetMatrixCostAdjustmentLeaf
    {
        public BudgetMatrixCostAdjustmentLeaf(int conceptId, int matrixId, decimal cantidad)
        {
            if (conceptId <= 0)
                throw new ArgumentOutOfRangeException(nameof(conceptId));
            if (matrixId <= 0)
                throw new ArgumentOutOfRangeException(nameof(matrixId));

            ConceptId = conceptId;
            MatrixId = matrixId;
            Cantidad = cantidad;
        }

        public int ConceptId { get; }

        public int MatrixId { get; }

        public decimal Cantidad { get; }

        public static BudgetMatrixCostAdjustmentLeaf FromConcepto(ConceptoPresupuesto concepto)
        {
            ArgumentNullException.ThrowIfNull(concepto);

            if (!concepto.MatrizId.HasValue)
                throw new ArgumentException(
                    "El concepto no tiene matriz asignada.", nameof(concepto));

            return new BudgetMatrixCostAdjustmentLeaf(
                concepto.Id,
                concepto.MatrizId.Value,
                concepto.Cantidad);
        }
    }
}
