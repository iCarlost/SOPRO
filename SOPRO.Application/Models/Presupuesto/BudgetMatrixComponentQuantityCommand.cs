using SOPRO.Core.Entities;

namespace SOPRO.Application.Models.Presupuesto
{
    /// <summary>
    /// Intención de edición de la Cantidad de un componente YA persistido de una
    /// matriz del proyecto (celda del panel embebido y diálogo de cantidad, ADR-0001).
    /// Lleva identidad, las bases esperadas del proyecto, de la matriz, del
    /// componente y del rowset COMPLETO del presupuesto, más la cantidad propuesta.
    /// Los importes derivados los calcula Application con los servicios canónicos,
    /// nunca el llamador; todos los valores son de solo lectura para que un reintento
    /// repita exactamente la misma operación.
    /// </summary>
    public sealed class BudgetMatrixComponentQuantityCommand
    {
        public BudgetMatrixComponentQuantityCommand(
            int projectId,
            BudgetProjectCalculationBase expectedProject,
            BudgetMatrixCalculationBase expectedMatrix,
            BudgetMatrixComponentFingerprint expectedComponents,
            BudgetMatrixComponentQuantityBase expectedComponent,
            BudgetPriceRefreshRowset expectedBudgetRows,
            decimal proposedQuantity)
        {
            if (projectId <= 0)
                throw new ArgumentOutOfRangeException(nameof(projectId));

            ArgumentNullException.ThrowIfNull(expectedProject);
            ArgumentNullException.ThrowIfNull(expectedMatrix);
            ArgumentNullException.ThrowIfNull(expectedComponents);
            ArgumentNullException.ThrowIfNull(expectedComponent);
            ArgumentNullException.ThrowIfNull(expectedBudgetRows);

            if (expectedMatrix.ProjectId != projectId)
                throw new ArgumentException(
                    "La matriz esperada no pertenece al proyecto del comando.", nameof(expectedMatrix));

            if (expectedComponent.MatrixId != expectedMatrix.MatrixId)
                throw new ArgumentException(
                    "El componente esperado no pertenece a la matriz del comando.", nameof(expectedComponent));

            if (!expectedComponents.Rows.Any(r => r.ComponentId == expectedComponent.ComponentId))
                throw new ArgumentException(
                    "El componente esperado no figura en el fingerprint de componentes.", nameof(expectedComponent));

            if (expectedBudgetRows.Rows.Any(r => r.ProjectId != projectId))
                throw new ArgumentException(
                    "El rowset contiene filas de otro proyecto.", nameof(expectedBudgetRows));

            ProjectId = projectId;
            ExpectedProject = expectedProject;
            ExpectedMatrix = expectedMatrix;
            ExpectedComponents = expectedComponents;
            ExpectedComponent = expectedComponent;
            ExpectedBudgetRows = expectedBudgetRows;
            ProposedQuantity = proposedQuantity;
        }

        public int ProjectId { get; }

        public int MatrixId => ExpectedMatrix.MatrixId;

        public int ComponentId => ExpectedComponent.ComponentId;

        /// <summary>Base económica del proyecto con la que la vista calculó lo que muestra.</summary>
        public BudgetProjectCalculationBase ExpectedProject { get; }

        /// <summary>Base de la matriz que la vista cargó antes de la edición.</summary>
        public BudgetMatrixCalculationBase ExpectedMatrix { get; }

        /// <summary>Ids y Orden exactos de TODOS los componentes persistidos de la matriz.</summary>
        public BudgetMatrixComponentFingerprint ExpectedComponents { get; }

        /// <summary>Base del componente editado: tipo, referencias, cantidad, rendimiento e importe.</summary>
        public BudgetMatrixComponentQuantityBase ExpectedComponent { get; }

        /// <summary>Filas persistidas que la vista tenía del presupuesto, en el orden de la base.</summary>
        public BudgetPriceRefreshRowset ExpectedBudgetRows { get; }

        /// <summary>Cantidad propuesta por el usuario, sin redondear ni convertir a importes.</summary>
        public decimal ProposedQuantity { get; }
    }

    /// <summary>
    /// Cabecera y costo directo de la matriz que la vista cargó. Si cualquiera de
    /// estos valores cambió en la base, recalcular produciría importes que el
    /// usuario nunca vio.
    /// </summary>
    public sealed class BudgetMatrixCalculationBase
    {
        public BudgetMatrixCalculationBase(
            int matrixId,
            int? projectId,
            string clave,
            string descripcion,
            string unidad,
            TipoMatriz tipo,
            decimal costoDirecto)
        {
            if (matrixId <= 0)
                throw new ArgumentOutOfRangeException(nameof(matrixId));

            MatrixId = matrixId;
            ProjectId = projectId;
            Clave = clave ?? string.Empty;
            Descripcion = descripcion ?? string.Empty;
            Unidad = unidad ?? string.Empty;
            Tipo = tipo;
            CostoDirecto = costoDirecto;
        }

        public int MatrixId { get; }

        public int? ProjectId { get; }

        public string Clave { get; }

        public string Descripcion { get; }

        public string Unidad { get; }

        public TipoMatriz Tipo { get; }

        public decimal CostoDirecto { get; }

        public static BudgetMatrixCalculationBase FromMatriz(Matriz matriz)
        {
            ArgumentNullException.ThrowIfNull(matriz);

            return new BudgetMatrixCalculationBase(
                matriz.Id,
                matriz.ProyectoId,
                matriz.Clave,
                matriz.Descripcion,
                matriz.Unidad,
                matriz.Tipo,
                matriz.CostoDirecto);
        }
    }

    /// <summary>
    /// Huella de los componentes persistidos de una matriz: conjunto EXACTO de Ids
    /// con su Orden, ordenado por Orden e Id. Una fila añadida, eliminada o
    /// reordenada en la base invalida la edición.
    /// </summary>
    public sealed class BudgetMatrixComponentFingerprint
    {
        public BudgetMatrixComponentFingerprint(IEnumerable<BudgetMatrixComponentFingerprintRow> rows)
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

        public IReadOnlyList<BudgetMatrixComponentFingerprintRow> Rows { get; }

        public static BudgetMatrixComponentFingerprint FromComponentes(IEnumerable<ComponenteMatriz> componentes)
        {
            ArgumentNullException.ThrowIfNull(componentes);

            return new BudgetMatrixComponentFingerprint(componentes
                .Where(c => c is not null && c.Id > 0)
                .Select(c => new BudgetMatrixComponentFingerprintRow(c.Id, c.Orden)));
        }
    }

    /// <summary>Identidad y posición persistida de un componente dentro de su matriz.</summary>
    public sealed class BudgetMatrixComponentFingerprintRow
    {
        public BudgetMatrixComponentFingerprintRow(int componentId, int orden)
        {
            if (componentId <= 0)
                throw new ArgumentOutOfRangeException(nameof(componentId));

            ComponentId = componentId;
            Orden = orden;
        }

        public int ComponentId { get; }

        public int Orden { get; }
    }

    /// <summary>
    /// Base del componente editado. Compara tipo, referencias (una sola llena según
    /// el tipo), cantidad, rendimiento, importe y posición: si algo cambió en la
    /// base desde que se cargó, el resultado ya no representaría lo que el usuario vio.
    /// </summary>
    public sealed class BudgetMatrixComponentQuantityBase
    {
        public BudgetMatrixComponentQuantityBase(
            int componentId,
            int matrixId,
            TipoComponenteMatriz tipoComponente,
            int? materialId,
            int? manoDeObraId,
            int? maquinariaId,
            int? auxiliarId,
            int? herramientaId,
            decimal cantidad,
            decimal rendimiento,
            decimal importe,
            int orden)
        {
            if (componentId <= 0)
                throw new ArgumentOutOfRangeException(nameof(componentId));
            if (matrixId <= 0)
                throw new ArgumentOutOfRangeException(nameof(matrixId));

            ComponentId = componentId;
            MatrixId = matrixId;
            TipoComponente = tipoComponente;
            MaterialId = materialId;
            ManoDeObraId = manoDeObraId;
            MaquinariaId = maquinariaId;
            AuxiliarId = auxiliarId;
            HerramientaId = herramientaId;
            Cantidad = cantidad;
            Rendimiento = rendimiento;
            Importe = importe;
            Orden = orden;
        }

        public int ComponentId { get; }

        public int MatrixId { get; }

        public TipoComponenteMatriz TipoComponente { get; }

        public int? MaterialId { get; }

        public int? ManoDeObraId { get; }

        public int? MaquinariaId { get; }

        public int? AuxiliarId { get; }

        public int? HerramientaId { get; }

        public decimal Cantidad { get; }

        public decimal Rendimiento { get; }

        public decimal Importe { get; }

        public int Orden { get; }

        public static BudgetMatrixComponentQuantityBase FromComponente(ComponenteMatriz componente)
        {
            ArgumentNullException.ThrowIfNull(componente);

            return new BudgetMatrixComponentQuantityBase(
                componente.Id,
                componente.MatrizId,
                componente.TipoComponente,
                componente.MaterialId,
                componente.ManoDeObraId,
                componente.MaquinariaId,
                componente.AuxiliarId,
                componente.HerramientaId,
                componente.Cantidad,
                componente.Rendimiento,
                componente.Importe,
                componente.Orden);
        }
    }
}
