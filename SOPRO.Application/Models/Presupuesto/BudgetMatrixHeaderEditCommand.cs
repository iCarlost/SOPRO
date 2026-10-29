using SOPRO.Core.Entities;

namespace SOPRO.Application.Models.Presupuesto
{
    /// <summary>
    /// Intención de editar la cabecera de una matriz YA persistida del proyecto
    /// (ADR-0001), tanto desde el botón Guardar como desde el guardado implícito al
    /// cambiar de concepto. Lleva identidad, la base persistida EXACTA de la
    /// cabecera que la vista cargó y los valores propuestos. La propuesta se
    /// normaliza aquí como en <see cref="MatrixApplicationService"/> (Clave en
    /// mayúsculas y sin espacios exteriores; Descripción y Unidad sin espacios
    /// exteriores); la base esperada conserva el texto persistido sin recortar para
    /// detectar conflictos reales contra la base.
    /// </summary>
    public sealed class BudgetMatrixHeaderEditCommand
    {
        public BudgetMatrixHeaderEditCommand(
            int projectId,
            BudgetMatrixHeaderBase expectedMatrix,
            string? proposedClave,
            string? proposedDescripcion,
            string? proposedUnidad,
            TipoMatriz proposedTipo)
        {
            if (projectId <= 0)
                throw new ArgumentOutOfRangeException(nameof(projectId));

            ArgumentNullException.ThrowIfNull(expectedMatrix);

            if (expectedMatrix.ProjectId.HasValue && expectedMatrix.ProjectId.Value != projectId)
                throw new ArgumentException(
                    "La matriz esperada no pertenece al proyecto del comando.", nameof(expectedMatrix));

            ProjectId = projectId;
            ExpectedMatrix = expectedMatrix;
            ProposedClave = (proposedClave ?? string.Empty).Trim().ToUpperInvariant();
            ProposedDescripcion = (proposedDescripcion ?? string.Empty).Trim();
            ProposedUnidad = (proposedUnidad ?? string.Empty).Trim();
            ProposedTipo = proposedTipo;
        }

        public int ProjectId { get; }

        public int MatrixId => ExpectedMatrix.MatrixId;

        /// <summary>Cabecera persistida que la vista cargó, con sus textos EXACTOS.</summary>
        public BudgetMatrixHeaderBase ExpectedMatrix { get; }

        /// <summary>Clave propuesta, normalizada a mayúsculas y sin espacios exteriores.</summary>
        public string ProposedClave { get; }

        /// <summary>Descripción propuesta, sin espacios exteriores.</summary>
        public string ProposedDescripcion { get; }

        /// <summary>Unidad propuesta, sin espacios exteriores.</summary>
        public string ProposedUnidad { get; }

        /// <summary>Tipo propuesto; el comando rechaza valores fuera del enum.</summary>
        public TipoMatriz ProposedTipo { get; }
    }

    /// <summary>
    /// Cabecera persistida de la matriz tal como la cargó la vista. Los textos se
    /// conservan EXACTOS (sin <c>Trim</c>) porque la comparación contra la base es
    /// ordinal: recortarlos ocultaría cambios reales de espacios o casing.
    /// </summary>
    public sealed class BudgetMatrixHeaderBase
    {
        public BudgetMatrixHeaderBase(
            int matrixId,
            int? projectId,
            string clave,
            string descripcion,
            string unidad,
            TipoMatriz tipo)
        {
            if (matrixId <= 0)
                throw new ArgumentOutOfRangeException(nameof(matrixId));

            MatrixId = matrixId;
            ProjectId = projectId;
            Clave = clave ?? string.Empty;
            Descripcion = descripcion ?? string.Empty;
            Unidad = unidad ?? string.Empty;
            Tipo = tipo;
        }

        public int MatrixId { get; }

        public int? ProjectId { get; }

        public string Clave { get; }

        public string Descripcion { get; }

        public string Unidad { get; }

        public TipoMatriz Tipo { get; }

        public static BudgetMatrixHeaderBase FromMatriz(Matriz matriz)
        {
            ArgumentNullException.ThrowIfNull(matriz);

            return new BudgetMatrixHeaderBase(
                matriz.Id,
                matriz.ProyectoId,
                matriz.Clave,
                matriz.Descripcion,
                matriz.Unidad,
                matriz.Tipo);
        }
    }
}
