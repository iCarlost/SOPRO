using SOPRO.Core.Entities;

namespace SOPRO.Application.Models.Presupuesto
{
    /// <summary>
    /// Resultado de una edición de cabecera de matriz: la clasificación de la
    /// operación y, solo si se confirmó, la instantánea inmutable de lo que quedó
    /// persistido.
    /// </summary>
    public sealed class BudgetMatrixHeaderEditResult
    {
        internal BudgetMatrixHeaderEditResult(
            BudgetOperationResult operation,
            BudgetMatrixHeaderEditSnapshot? confirmed,
            string? diagnostic)
        {
            Operation = operation ?? throw new ArgumentNullException(nameof(operation));
            Confirmed = confirmed;
            Diagnostic = diagnostic;
        }

        public BudgetOperationResult Operation { get; }

        public BudgetOperationStatus Status => Operation.Status;

        public bool IsCommitted => Operation.IsCommitted;

        /// <summary>Mensaje seguro para el usuario.</summary>
        public string? Message => Operation.Message;

        /// <summary>Detalle técnico para registro y pruebas; nunca se muestra en la UI.</summary>
        public string? Diagnostic { get; }

        /// <summary>Valores confirmados; null si la operación no se confirmó.</summary>
        public BudgetMatrixHeaderEditSnapshot? Confirmed { get; }
    }

    /// <summary>Cabecera persistida tras confirmar el comando (solo lectura).</summary>
    public sealed class BudgetMatrixHeaderEditSnapshot
    {
        internal BudgetMatrixHeaderEditSnapshot(
            int projectId,
            int matrixId,
            string clave,
            string descripcion,
            string unidad,
            TipoMatriz tipo,
            DateTime fechaModificacion)
        {
            ProjectId = projectId;
            MatrixId = matrixId;
            Clave = clave ?? throw new ArgumentNullException(nameof(clave));
            Descripcion = descripcion ?? throw new ArgumentNullException(nameof(descripcion));
            Unidad = unidad ?? throw new ArgumentNullException(nameof(unidad));
            Tipo = tipo;
            FechaModificacion = fechaModificacion;
        }

        public int ProjectId { get; }

        public int MatrixId { get; }

        public string Clave { get; }

        public string Descripcion { get; }

        public string Unidad { get; }

        public TipoMatriz Tipo { get; }

        public DateTime FechaModificacion { get; }
    }
}
