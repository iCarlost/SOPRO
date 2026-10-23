using SOPRO.Core.Entities;

namespace SOPRO.Application.Models.Presupuesto
{
    /// <summary>
    /// Origen de la clave pegada en una fila del lote, ya resuelto por la vista:
    /// una matriz APU del proyecto, un concepto de otra fila del mismo presupuesto
    /// (copia) o ninguna coincidencia (edición simple de la clave).
    /// </summary>
    public enum BudgetPasteKeyTargetKind
    {
        None = 0,
        Matrix = 1,
        SourceConcept = 2
    }

    /// <summary>
    /// Instantánea inmutable del concepto de origen de una copia, tomada por la vista
    /// ANTES de pegar. Transporta la base esperada (para detectar cambios ajenos) y los
    /// valores que alimentan el borrador canónico. El comando la revalida contra una
    /// lectura sin tracking y construye la copia con estos valores, nunca con el estado
    /// que otra fila del mismo lote haya podido dejar en memoria.
    /// </summary>
    public sealed class BudgetPasteSourceConceptSnapshot
    {
        public BudgetPasteSourceConceptSnapshot(
            int conceptId,
            BudgetConceptAssignmentBase expectedConcept,
            string clave,
            string descripcion,
            string unidad,
            decimal cantidad,
            decimal costoDirectoUnitario,
            int? matrizId)
        {
            if (conceptId <= 0) throw new ArgumentOutOfRangeException(nameof(conceptId));

            ConceptId = conceptId;
            ExpectedConcept = expectedConcept ?? throw new ArgumentNullException(nameof(expectedConcept));
            Clave = clave ?? string.Empty;
            Descripcion = descripcion ?? string.Empty;
            Unidad = unidad ?? string.Empty;
            Cantidad = cantidad;
            CostoDirectoUnitario = costoDirectoUnitario;
            MatrizId = matrizId;
        }

        public int ConceptId { get; }

        /// <summary>Base del origen que la vista cargó antes de pegar.</summary>
        public BudgetConceptAssignmentBase ExpectedConcept { get; }

        public string Clave { get; }

        public string Descripcion { get; }

        public string Unidad { get; }

        public decimal Cantidad { get; }

        public decimal CostoDirectoUnitario { get; }

        public int? MatrizId { get; }

        /// <summary>Copia la instantánea del concepto tal como lo cargó la vista (antes de pegar).</summary>
        public static BudgetPasteSourceConceptSnapshot FromConcepto(ConceptoPresupuesto concepto)
        {
            ArgumentNullException.ThrowIfNull(concepto);
            if (concepto.Id <= 0) throw new ArgumentOutOfRangeException(nameof(concepto));

            return new BudgetPasteSourceConceptSnapshot(
                concepto.Id,
                BudgetConceptAssignmentBase.FromConcepto(concepto),
                concepto.Clave ?? string.Empty,
                concepto.Descripcion ?? string.Empty,
                concepto.Unidad ?? string.Empty,
                concepto.Cantidad,
                concepto.CostoDirectoUnitario,
                concepto.MatrizId);
        }
    }

    /// <summary>
    /// Referencia inmutable a la fuente de la clave pegada. Una matriz viaja solo por
    /// Id y el comando la recarga; una copia desde concepto viaja además con la
    /// instantánea de valores y la base esperada del origen.
    /// </summary>
    public sealed class BudgetPasteKeyTarget
    {
        private BudgetPasteKeyTarget(
            BudgetPasteKeyTargetKind kind,
            int referenceId,
            BudgetPasteSourceConceptSnapshot? sourceConcept)
        {
            Kind = kind;
            ReferenceId = referenceId;
            SourceConcept = sourceConcept;
        }

        public static BudgetPasteKeyTarget None { get; } =
            new(BudgetPasteKeyTargetKind.None, 0, null);

        public BudgetPasteKeyTargetKind Kind { get; }

        /// <summary>Id de la matriz APU o del concepto de origen según <see cref="Kind"/>.</summary>
        public int ReferenceId { get; }

        /// <summary>Instantánea del origen; solo presente cuando <see cref="Kind"/> es <see cref="BudgetPasteKeyTargetKind.SourceConcept"/>.</summary>
        public BudgetPasteSourceConceptSnapshot? SourceConcept { get; }

        public static BudgetPasteKeyTarget ForMatrix(int matrixId)
        {
            if (matrixId <= 0) throw new ArgumentOutOfRangeException(nameof(matrixId));
            return new BudgetPasteKeyTarget(BudgetPasteKeyTargetKind.Matrix, matrixId, null);
        }

        public static BudgetPasteKeyTarget ForSourceConcept(BudgetPasteSourceConceptSnapshot sourceConcept)
        {
            if (sourceConcept is null) throw new ArgumentNullException(nameof(sourceConcept));
            return new BudgetPasteKeyTarget(
                BudgetPasteKeyTargetKind.SourceConcept,
                sourceConcept.ConceptId,
                sourceConcept);
        }
    }

    /// <summary>
    /// Intención de pegado para UNA fila ya persistida (ADR-0001). Lleva la base
    /// esperada del concepto, los valores ya normalizados de las columnas aceptadas y
    /// la fuente de la clave; los importes derivados los calcula Application con los
    /// servicios canónicos, nunca el llamador. Inmutable para que un reintento repita
    /// exactamente la misma operación.
    /// </summary>
    public sealed class BudgetPasteRowCommand
    {
        public BudgetPasteRowCommand(
            int conceptId,
            BudgetConceptAssignmentBase expectedConcept,
            string? clave,
            string? descripcion,
            string? unidad,
            decimal? cantidad,
            BudgetPasteKeyTarget keyTarget)
        {
            if (conceptId <= 0) throw new ArgumentOutOfRangeException(nameof(conceptId));
            if (cantidad is <= 0m) throw new ArgumentOutOfRangeException(nameof(cantidad));

            KeyTarget = keyTarget ?? throw new ArgumentNullException(nameof(keyTarget));
            if (KeyTarget.Kind != BudgetPasteKeyTargetKind.None && string.IsNullOrWhiteSpace(clave))
                throw new ArgumentException("La clave es requerida cuando el lote resuelve una asignación.", nameof(clave));
            if (KeyTarget.Kind == BudgetPasteKeyTargetKind.SourceConcept && KeyTarget.SourceConcept is null)
                throw new ArgumentException(
                    "La copia desde concepto requiere la instantánea inmutable del origen.", nameof(keyTarget));

            ConceptId = conceptId;
            ExpectedConcept = expectedConcept ?? throw new ArgumentNullException(nameof(expectedConcept));
            Clave = clave;
            Descripcion = descripcion;
            Unidad = unidad;
            Cantidad = cantidad;
        }

        public int ConceptId { get; }

        /// <summary>Base del concepto que la vista cargó antes de pegar.</summary>
        public BudgetConceptAssignmentBase ExpectedConcept { get; }

        /// <summary>Clave pegada; <c>null</c> si la columna no participó del bloque pegado.</summary>
        public string? Clave { get; }

        /// <summary>Descripción pegada; <c>null</c> si la columna no participó del bloque pegado.</summary>
        public string? Descripcion { get; }

        /// <summary>Unidad pegada; <c>null</c> si la columna no participó del bloque pegado.</summary>
        public string? Unidad { get; }

        /// <summary>Cantidad pegada ya normalizada; <c>null</c> si la columna no participó.</summary>
        public decimal? Cantidad { get; }

        public BudgetPasteKeyTarget KeyTarget { get; }
    }

    /// <summary>
    /// Intención atómica de pegar un bloque de celdas sobre filas YA persistidas del
    /// presupuesto (ADR-0001). Todas las filas viajan en un solo comando: el comando
    /// revalida proyecto, bases esperadas, matrices/conceptos de origen y escribe una
    /// única vez dentro de una transacción, agrupadores incluidos.
    /// </summary>
    public sealed class BudgetPasteCommand
    {
        public BudgetPasteCommand(
            int projectId,
            BudgetProjectCalculationBase expectedProject,
            IReadOnlyList<BudgetPasteRowCommand> rows)
        {
            if (projectId <= 0) throw new ArgumentOutOfRangeException(nameof(projectId));
            if (rows == null) throw new ArgumentNullException(nameof(rows));

            var copia = rows.ToArray();
            if (copia.Length == 0)
                throw new ArgumentException("El pegado requiere al menos una fila.", nameof(rows));
            if (copia.Any(r => r is null))
                throw new ArgumentException("El pegado no admite filas nulas.", nameof(rows));
            if (copia.Select(r => r.ConceptId).Distinct().Count() != copia.Length)
                throw new ArgumentException("El pegado no puede repetir la misma fila.", nameof(rows));

            ProjectId = projectId;
            ExpectedProject = expectedProject ?? throw new ArgumentNullException(nameof(expectedProject));
            Rows = copia;
        }

        public int ProjectId { get; }

        /// <summary>Base económica del proyecto con la que la vista calculó lo que muestra.</summary>
        public BudgetProjectCalculationBase ExpectedProject { get; }

        public IReadOnlyList<BudgetPasteRowCommand> Rows { get; }
    }
}
