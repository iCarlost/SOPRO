using SOPRO.Core.Entities;

namespace SOPRO.Application.Models.Presupuesto
{
    /// <summary>
    /// Intención de alta en lote de componentes sobre una matriz YA persistida del
    /// proyecto (panel embebido de matrices, ADR-0001). Lleva identidad, las bases
    /// esperadas del proyecto, de la matriz, el estado COMPLETO de todos sus
    /// componentes actuales y el rowset COMPLETO del presupuesto.
    /// Las altas son intenciones tipadas: Id de catálogo, base esperada de origen,
    /// cantidad y notas. Los importes derivados los calcula Application con los
    /// servicios canónicos, nunca el llamador. Todos los valores son de solo lectura
    /// para que un reintento repita exactamente la misma operación.
    /// </summary>
    public sealed class BudgetMatrixComponentAdditionCommand
    {
        public BudgetMatrixComponentAdditionCommand(
            int projectId,
            BudgetProjectCalculationBase expectedProject,
            BudgetMatrixCalculationBase expectedMatrix,
            BudgetMatrixComponentStateFingerprint expectedComponents,
            BudgetPriceRefreshRowset expectedBudgetRows,
            IEnumerable<BudgetMatrixComponentAdditionItem> additions)
        {
            if (projectId <= 0)
                throw new ArgumentOutOfRangeException(nameof(projectId));

            ArgumentNullException.ThrowIfNull(expectedProject);
            ArgumentNullException.ThrowIfNull(expectedMatrix);
            ArgumentNullException.ThrowIfNull(expectedComponents);
            ArgumentNullException.ThrowIfNull(expectedBudgetRows);
            ArgumentNullException.ThrowIfNull(additions);

            if (expectedMatrix.ProjectId != projectId)
                throw new ArgumentException(
                    "La matriz esperada no pertenece al proyecto del comando.", nameof(expectedMatrix));

            if (expectedComponents.Rows.Any(r => r.MatrixId != expectedMatrix.MatrixId))
                throw new ArgumentException(
                    "El fingerprint contiene componentes de otra matriz.", nameof(expectedComponents));

            if (expectedBudgetRows.Rows.Any(r => r.ProjectId != projectId))
                throw new ArgumentException(
                    "El rowset contiene filas de otro proyecto.", nameof(expectedBudgetRows));

            var copia = additions.ToArray();
            if (copia.Any(a => a is null))
                throw new ArgumentException("El lote de altas no admite elementos nulos.", nameof(additions));

            ProjectId = projectId;
            ExpectedProject = expectedProject;
            ExpectedMatrix = expectedMatrix;
            ExpectedComponents = expectedComponents;
            ExpectedBudgetRows = expectedBudgetRows;
            Additions = copia;
        }

        public int ProjectId { get; }

        public int MatrixId => ExpectedMatrix.MatrixId;

        /// <summary>Base económica del proyecto con la que la vista calculó lo que muestra.</summary>
        public BudgetProjectCalculationBase ExpectedProject { get; }

        /// <summary>Base de la matriz que la vista cargó antes de las altas.</summary>
        public BudgetMatrixCalculationBase ExpectedMatrix { get; }

        /// <summary>
        /// Estado completo de TODOS los componentes persistidos de la matriz (Id,
        /// tipo, referencias, cantidad, rendimiento, importe y orden). Una fila
        /// añadida, eliminada, reordenada o con cualquier valor económico distinto en
        /// la base invalida el lote.
        /// </summary>
        public BudgetMatrixComponentStateFingerprint ExpectedComponents { get; }

        /// <summary>Filas persistidas que la vista tenía del presupuesto, en el orden de la base.</summary>
        public BudgetPriceRefreshRowset ExpectedBudgetRows { get; }

        /// <summary>Altas propuestas, en el orden en que deben quedar dentro de la matriz.</summary>
        public IReadOnlyList<BudgetMatrixComponentAdditionItem> Additions { get; }
    }

    /// <summary>
    /// Huella del estado completo de los componentes persistidos de una matriz,
    /// ordenada por Orden e Id. A diferencia de
    /// <see cref="BudgetMatrixComponentFingerprint"/> (solo Id y Orden), esta huella
    /// también revalida tipo, referencias, cantidad, rendimiento e importe.
    /// </summary>
    public sealed class BudgetMatrixComponentStateFingerprint
    {
        public BudgetMatrixComponentStateFingerprint(
            IEnumerable<BudgetMatrixComponentQuantityBase> rows)
        {
            ArgumentNullException.ThrowIfNull(rows);

            var copia = rows.ToArray();
            if (copia.Any(r => r is null))
                throw new ArgumentException("El fingerprint no admite filas nulas.", nameof(rows));
            if (copia.Select(r => r.ComponentId).Distinct().Count() != copia.Length)
                throw new ArgumentException("El fingerprint no admite componentes repetidos.", nameof(rows));

            Rows = copia
                .OrderBy(r => r.Orden)
                .ThenBy(r => r.ComponentId)
                .ToArray();
        }

        public IReadOnlyList<BudgetMatrixComponentQuantityBase> Rows { get; }

        public static BudgetMatrixComponentStateFingerprint FromComponentes(
            IEnumerable<ComponenteMatriz> componentes)
        {
            ArgumentNullException.ThrowIfNull(componentes);

            return new BudgetMatrixComponentStateFingerprint(componentes
                .Where(c => c is not null && c.Id > 0)
                .Select(BudgetMatrixComponentQuantityBase.FromComponente));
        }
    }

    /// <summary>
    /// Alta propuesta de un componente desde un insumo tipado del catálogo. El
    /// llamador nunca envía importes: la cantidad y las notas son la única entrada
    /// propia de la intención.
    /// </summary>
    public sealed class BudgetMatrixComponentAdditionItem
    {
        public BudgetMatrixComponentAdditionItem(
            TipoComponenteMatriz tipoComponente,
            int sourceId,
            BudgetMatrixComponentSourceBase expectedSource,
            decimal proposedQuantity,
            string? notes)
        {
            if (sourceId <= 0)
                throw new ArgumentOutOfRangeException(nameof(sourceId));
            if (proposedQuantity <= 0m)
                throw new ArgumentOutOfRangeException(
                    nameof(proposedQuantity), "La cantidad propuesta debe ser mayor que cero.");

            ArgumentNullException.ThrowIfNull(expectedSource);

            if (expectedSource.TipoComponente != tipoComponente)
                throw new ArgumentException(
                    "La base esperada del origen no coincide con el tipo del alta.", nameof(expectedSource));
            if (expectedSource.SourceId != sourceId)
                throw new ArgumentException(
                    "La base esperada del origen no coincide con el Id del alta.", nameof(expectedSource));

            TipoComponente = tipoComponente;
            SourceId = sourceId;
            ExpectedSource = expectedSource;
            ProposedQuantity = proposedQuantity;
            Notes = notes?.Trim() ?? string.Empty;
        }

        public TipoComponenteMatriz TipoComponente { get; }

        public int SourceId { get; }

        /// <summary>Base de catálogo que la vista mostró y con la que se debe revalidar.</summary>
        public BudgetMatrixComponentSourceBase ExpectedSource { get; }

        /// <summary>Cantidad propuesta por el usuario, sin convertirla a importes.</summary>
        public decimal ProposedQuantity { get; }

        /// <summary>Notas del componente, se persisten tal como se capturaron.</summary>
        public string Notes { get; }
    }

    /// <summary>
    /// Base esperada de un insumo de catálogo para un alta de componente: identidad,
    /// pertenencia (nula = maestro, o el proyecto), datos visibles y el valor que
    /// interviene en el cálculo (precio unitario, salario real o costo horario) más
    /// la marca de porcentaje de mano de obra. Cualquier cambio en la base invalida
    /// el lote antes de escribir.
    /// </summary>
    public sealed class BudgetMatrixComponentSourceBase
    {
        public BudgetMatrixComponentSourceBase(
            TipoComponenteMatriz tipoComponente,
            int sourceId,
            int? projectId,
            string clave,
            string descripcion,
            string unidad,
            decimal precioUnitario,
            bool esPorcentajeManoDeObra)
        {
            if (sourceId <= 0)
                throw new ArgumentOutOfRangeException(nameof(sourceId));

            TipoComponente = tipoComponente;
            SourceId = sourceId;
            ProjectId = projectId;
            Clave = clave ?? string.Empty;
            Descripcion = descripcion ?? string.Empty;
            Unidad = unidad ?? string.Empty;
            PrecioUnitario = precioUnitario;
            EsPorcentajeManoDeObra = esPorcentajeManoDeObra;
        }

        public TipoComponenteMatriz TipoComponente { get; }

        public int SourceId { get; }

        /// <summary>Proyecto propietario; null cuando la fila es del catálogo maestro.</summary>
        public int? ProjectId { get; }

        public string Clave { get; }

        public string Descripcion { get; }

        public string Unidad { get; }

        /// <summary>
        /// Precio que usa el cálculo según el tipo: PrecioUnitario (material y
        /// herramienta), SalarioReal (mano de obra) o CostoHorario (maquinaria).
        /// </summary>
        public decimal PrecioUnitario { get; }

        public bool EsPorcentajeManoDeObra { get; }

        public static BudgetMatrixComponentSourceBase FromMaterial(Material material)
        {
            ArgumentNullException.ThrowIfNull(material);

            return new BudgetMatrixComponentSourceBase(
                TipoComponenteMatriz.Material,
                material.Id,
                material.ProyectoId,
                material.Clave,
                material.Descripcion,
                material.Unidad,
                material.PrecioUnitario,
                esPorcentajeManoDeObra: false);
        }

        public static BudgetMatrixComponentSourceBase FromManoDeObra(ManoDeObra manoDeObra)
        {
            ArgumentNullException.ThrowIfNull(manoDeObra);

            return new BudgetMatrixComponentSourceBase(
                TipoComponenteMatriz.ManoDeObra,
                manoDeObra.Id,
                manoDeObra.ProyectoId,
                manoDeObra.Clave,
                manoDeObra.Descripcion,
                manoDeObra.Unidad,
                manoDeObra.SalarioReal,
                manoDeObra.EsPorcentajeMO);
        }

        public static BudgetMatrixComponentSourceBase FromMaquinaria(Maquinaria maquinaria)
        {
            ArgumentNullException.ThrowIfNull(maquinaria);

            return new BudgetMatrixComponentSourceBase(
                TipoComponenteMatriz.Maquinaria,
                maquinaria.Id,
                maquinaria.ProyectoId,
                maquinaria.Clave,
                maquinaria.Descripcion,
                string.Empty,
                maquinaria.CostoHorario,
                esPorcentajeManoDeObra: false);
        }

        public static BudgetMatrixComponentSourceBase FromHerramienta(Herramienta herramienta)
        {
            ArgumentNullException.ThrowIfNull(herramienta);

            return new BudgetMatrixComponentSourceBase(
                TipoComponenteMatriz.Herramienta,
                herramienta.Id,
                herramienta.ProyectoId,
                herramienta.Clave,
                herramienta.Descripcion,
                herramienta.Unidad,
                herramienta.PrecioUnitario,
                herramienta.EsPorcentajeMO);
        }
    }
}
