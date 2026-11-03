using SOPRO.Core.Entities;

namespace SOPRO.Application.Models.Presupuesto
{
    /// <summary>
    /// Primera alta de una matriz nueva desde el panel embebido: crea la matriz,
    /// inserta el lote completo de componentes y la vincula al concepto persistido
    /// de origen en UNA sola transacción. La cabecera y el concepto viajan como
    /// bases esperadas para revalidar que la vista no quedó obsoleta.
    /// </summary>
    public sealed class BudgetNewMatrixWithComponentsCommand
    {
        public BudgetNewMatrixWithComponentsCommand(
            int projectId,
            BudgetProjectCalculationBase expectedProject,
            int conceptId,
            BudgetConceptAssignmentBase expectedConcept,
            BudgetAssignmentRowset expectedRowset,
            string clave,
            string descripcion,
            string unidad,
            TipoMatriz tipo,
            decimal proposedQuantity,
            IReadOnlyList<BudgetMatrixComponentAdditionItem> additions)
        {
            if (projectId <= 0)
                throw new ArgumentOutOfRangeException(nameof(projectId));
            ArgumentNullException.ThrowIfNull(expectedProject);
            if (conceptId <= 0)
                throw new ArgumentOutOfRangeException(nameof(conceptId));
            ArgumentNullException.ThrowIfNull(expectedConcept);
            ArgumentNullException.ThrowIfNull(expectedRowset);
            if (string.IsNullOrWhiteSpace(clave))
                throw new ArgumentException("La clave de la matriz es requerida.", nameof(clave));
            if (string.IsNullOrWhiteSpace(descripcion))
                throw new ArgumentException("La descripción de la matriz es requerida.", nameof(descripcion));
            if (string.IsNullOrWhiteSpace(unidad))
                throw new ArgumentException("La unidad de la matriz es requerida.", nameof(unidad));
            if (proposedQuantity <= 0m)
                throw new ArgumentOutOfRangeException(nameof(proposedQuantity));
            ArgumentNullException.ThrowIfNull(additions);

            var copia = additions.ToArray();
            if (copia.Any(a => a is null))
                throw new ArgumentException("El lote de altas no admite elementos nulos.", nameof(additions));

            ProjectId = projectId;
            ExpectedProject = expectedProject;
            ConceptId = conceptId;
            ExpectedConcept = expectedConcept;
            ExpectedRowset = expectedRowset;
            Clave = clave.Trim();
            Descripcion = descripcion.Trim();
            Unidad = unidad.Trim();
            Tipo = tipo;
            ProposedQuantity = proposedQuantity;
            Additions = copia;
        }

        public int ProjectId { get; }

        public BudgetProjectCalculationBase ExpectedProject { get; }

        /// <summary>Concepto persistido que recibirá la matriz creada.</summary>
        public int ConceptId { get; }

        public BudgetConceptAssignmentBase ExpectedConcept { get; }

        /// <summary>Rowset completo que la vista tenía antes de la primera alta.</summary>
        public BudgetAssignmentRowset ExpectedRowset { get; }

        public string Clave { get; }

        public string Descripcion { get; }

        public string Unidad { get; }

        public TipoMatriz Tipo { get; }

        /// <summary>Cantidad del concepto que la vista propone al crear la matriz.</summary>
        public decimal ProposedQuantity { get; }

        public IReadOnlyList<BudgetMatrixComponentAdditionItem> Additions { get; }
    }

    /// <summary>
    /// Resultado de la primera alta vinculada: clasificación y, solo si se
    /// confirmó, la instantánea de la matriz/componentes creados y la asignación
    /// confirmada al concepto.
    /// </summary>
    public sealed class BudgetNewMatrixWithComponentsResult
    {
        internal BudgetNewMatrixWithComponentsResult(
            BudgetOperationResult operation,
            BudgetNewMatrixWithComponentsSnapshot? confirmed,
            string? diagnostic)
        {
            Operation = operation ?? throw new ArgumentNullException(nameof(operation));
            Confirmed = confirmed;
            Diagnostic = diagnostic;
        }

        public BudgetOperationResult Operation { get; }

        public BudgetOperationStatus Status => Operation.Status;

        public bool IsCommitted => Operation.IsCommitted;

        public string? Message => Operation.Message;

        public string? Diagnostic { get; }

        public BudgetNewMatrixWithComponentsSnapshot? Confirmed { get; }
    }

    /// <summary>
    /// Instantánea confirmada de la primera alta: reutiliza las instantáneas
    /// canónicas de alta de componentes y de asignación de matriz para que la UI
    /// aplique lo confirmado sin releer ni recalcular por su cuenta.
    /// </summary>
    public sealed class BudgetNewMatrixWithComponentsSnapshot
    {
        internal BudgetNewMatrixWithComponentsSnapshot(
            int projectId,
            BudgetMatrixComponentAdditionSnapshot addition,
            BudgetMatrixAssignmentSnapshot assignment)
        {
            ProjectId = projectId;
            Addition = addition ?? throw new ArgumentNullException(nameof(addition));
            Assignment = assignment ?? throw new ArgumentNullException(nameof(assignment));
        }

        public int ProjectId { get; }

        public BudgetMatrixComponentAdditionSnapshot Addition { get; }

        public BudgetMatrixAssignmentSnapshot Assignment { get; }

        public int MatrixId => Addition.Matrix.MatrixId;

        public int ConceptId => Assignment.ConceptId;
    }
}
