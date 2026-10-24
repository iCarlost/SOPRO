namespace SOPRO.Application.Models.Presupuesto
{
    /// <summary>
    /// Intención de eliminación de UN componente YA persistido de una matriz del
    /// proyecto (panel embebido de matrices, ADR-0001). Lleva identidad y las bases
    /// esperadas del proyecto, la matriz, el fingerprint EXACTO de componentes, el
    /// componente a eliminar y el rowset COMPLETO del presupuesto. Los valores son de
    /// solo lectura para que un reintento repita exactamente la misma operación.
    /// </summary>
    public sealed class BudgetMatrixComponentDeletionCommand
    {
        public BudgetMatrixComponentDeletionCommand(
            int projectId,
            BudgetProjectCalculationBase expectedProject,
            BudgetMatrixCalculationBase expectedMatrix,
            BudgetMatrixComponentFingerprint expectedComponents,
            BudgetMatrixComponentQuantityBase expectedComponent,
            BudgetPriceRefreshRowset expectedBudgetRows)
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
        }

        public int ProjectId { get; }

        public int MatrixId => ExpectedMatrix.MatrixId;

        public int ComponentId => ExpectedComponent.ComponentId;

        /// <summary>Base económica del proyecto con la que la vista calculó lo que muestra.</summary>
        public BudgetProjectCalculationBase ExpectedProject { get; }

        /// <summary>Base de la matriz que la vista cargó antes de la eliminación.</summary>
        public BudgetMatrixCalculationBase ExpectedMatrix { get; }

        /// <summary>Ids y Orden exactos de TODOS los componentes persistidos de la matriz.</summary>
        public BudgetMatrixComponentFingerprint ExpectedComponents { get; }

        /// <summary>Base del componente a eliminar: tipo, referencias, cantidad, rendimiento e importe.</summary>
        public BudgetMatrixComponentQuantityBase ExpectedComponent { get; }

        /// <summary>Filas persistidas que la vista tenía del presupuesto, en el orden de la base.</summary>
        public BudgetPriceRefreshRowset ExpectedBudgetRows { get; }
    }
}
