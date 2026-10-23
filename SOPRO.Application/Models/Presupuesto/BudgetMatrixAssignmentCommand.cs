using SOPRO.Core.Entities;

namespace SOPRO.Application.Models.Presupuesto
{
    /// <summary>
    /// Intención de asignar una matriz APU o copiar otro concepto del mismo presupuesto
    /// (ADR-0001). Lleva identidad, bases esperadas, la fuente resuelta y los
    /// textos/cantidad propuestos: los importes derivados se calculan en Application
    /// con los servicios canónicos, nunca en el llamador. Si <see cref="ConceptId"/>
    /// es <c>null</c> la fila aún no se persistió (Id 0/Tag null); en ese caso el
    /// comando revalida el rowset completo y crea el concepto en la posición indicada.
    /// Todos sus valores son de solo lectura para que un reintento repita exactamente
    /// la misma operación.
    /// </summary>
    public sealed class BudgetMatrixAssignmentCommand
    {
        /// <summary>Constructor de la ruta existente de asignación de matriz por Id.</summary>
        public BudgetMatrixAssignmentCommand(
            int projectId,
            int conceptId,
            BudgetProjectCalculationBase expectedProject,
            BudgetConceptAssignmentBase expectedConcept,
            int matrixId,
            string proposedKey,
            string? descriptionOverride,
            string? unitOverride,
            decimal proposedQuantity)
            : this(
                projectId,
                conceptId,
                expectedProject,
                expectedConcept,
                BudgetAssignmentTarget.ForMatrix(matrixId, expectedMatrix: null),
                proposedKey,
                descriptionOverride,
                unitOverride,
                proposedQuantity,
                expectedRowset: null,
                proposedInsertionIndex: null)
        {
        }

        private BudgetMatrixAssignmentCommand(
            int projectId,
            int? conceptId,
            BudgetProjectCalculationBase expectedProject,
            BudgetConceptAssignmentBase? expectedConcept,
            BudgetAssignmentTarget target,
            string proposedKey,
            string? descriptionOverride,
            string? unitOverride,
            decimal proposedQuantity,
            BudgetAssignmentRowset? expectedRowset,
            int? proposedInsertionIndex)
        {
            if (projectId <= 0)
                throw new ArgumentOutOfRangeException(nameof(projectId));
            if (conceptId is <= 0)
                throw new ArgumentOutOfRangeException(nameof(conceptId));
            if (conceptId.HasValue && expectedConcept == null)
                throw new ArgumentNullException(nameof(expectedConcept),
                    "Una fila existente requiere la base esperada del concepto.");
            if (!conceptId.HasValue && expectedConcept != null)
                throw new ArgumentException(
                    "Una fila nueva no debe declarar una base esperada de concepto.",
                    nameof(expectedConcept));
            if (!conceptId.HasValue && expectedRowset == null)
                throw new ArgumentNullException(nameof(expectedRowset),
                    "Una fila nueva requiere el rowset persistido esperado.");
            if (!conceptId.HasValue
                && target.Kind == BudgetAssignmentTargetKind.Matrix
                && target.ExpectedMatrix == null)
                throw new ArgumentException(
                    "La asignación de matriz de una fila nueva requiere la base esperada de la matriz.",
                    nameof(target));
            if (proposedQuantity <= 0m)
                throw new ArgumentOutOfRangeException(nameof(proposedQuantity));
            if (string.IsNullOrWhiteSpace(proposedKey))
                throw new ArgumentException("La clave propuesta es requerida.", nameof(proposedKey));
            if (proposedInsertionIndex is int indice
                && (indice < 0 || (expectedRowset != null && indice > expectedRowset.Rows.Count)))
                throw new ArgumentOutOfRangeException(nameof(proposedInsertionIndex));
            if (conceptId.HasValue && proposedInsertionIndex.HasValue)
                throw new ArgumentException(
                    "Una fila existente no propone una posición de inserción.",
                    nameof(proposedInsertionIndex));

            ProjectId = projectId;
            ConceptId = conceptId;
            ExpectedProject = expectedProject ?? throw new ArgumentNullException(nameof(expectedProject));
            ExpectedConcept = expectedConcept;
            Target = target ?? throw new ArgumentNullException(nameof(target));
            ProposedKey = proposedKey.Trim();
            DescriptionOverride = string.IsNullOrWhiteSpace(descriptionOverride)
                ? null : descriptionOverride.Trim();
            UnitOverride = string.IsNullOrWhiteSpace(unitOverride)
                ? null : unitOverride.Trim();
            ProposedQuantity = proposedQuantity;
            ExpectedRowset = expectedRowset;
            ProposedInsertionIndex = proposedInsertionIndex;
        }

        /// <summary>Intención de actualizar un concepto YA persistido.</summary>
        public static BudgetMatrixAssignmentCommand ForExistingRow(
            int projectId,
            int conceptId,
            BudgetProjectCalculationBase expectedProject,
            BudgetConceptAssignmentBase expectedConcept,
            BudgetAssignmentTarget target,
            string proposedKey,
            string? descriptionOverride,
            string? unitOverride,
            decimal proposedQuantity)
            => new(
                projectId,
                conceptId,
                expectedProject,
                expectedConcept,
                target,
                proposedKey,
                descriptionOverride,
                unitOverride,
                proposedQuantity,
                expectedRowset: null,
                proposedInsertionIndex: null);

        /// <summary>
        /// Intención de crear una fila genuinamente nueva (Id 0/Tag null) en la posición
        /// <paramref name="proposedInsertionIndex"/> del rowset persistido esperado.
        /// </summary>
        public static BudgetMatrixAssignmentCommand ForNewRow(
            int projectId,
            BudgetProjectCalculationBase expectedProject,
            BudgetAssignmentTarget target,
            string proposedKey,
            string? descriptionOverride,
            string? unitOverride,
            decimal proposedQuantity,
            BudgetAssignmentRowset expectedRowset,
            int proposedInsertionIndex)
            => new(
                projectId,
                conceptId: null,
                expectedProject,
                expectedConcept: null,
                target,
                proposedKey,
                descriptionOverride,
                unitOverride,
                proposedQuantity,
                expectedRowset,
                proposedInsertionIndex);

        public int ProjectId { get; }

        /// <summary>Id del concepto existente, o <c>null</c> si la fila aún no se persistió.</summary>
        public int? ConceptId { get; }

        /// <summary>Base económica del proyecto con la que la vista calculó lo que muestra.</summary>
        public BudgetProjectCalculationBase ExpectedProject { get; }

        /// <summary>Base del concepto que la vista cargó antes de la asignación.</summary>
        public BudgetConceptAssignmentBase? ExpectedConcept { get; }

        /// <summary>Fuente resuelta: matriz APU o copia desde otro concepto.</summary>
        public BudgetAssignmentTarget Target { get; }

        /// <summary>Identidad de la matriz APU de origen, o <c>null</c> si la fuente es un concepto.</summary>
        public int? MatrixId => Target.Kind == BudgetAssignmentTargetKind.Matrix
            ? Target.ReferenceId
            : null;

        /// <summary>Clave que se escribirá en el concepto (la que el usuario editó).</summary>
        public string ProposedKey { get; }

        /// <summary>Descripción explícita; <c>null</c> para tomar la de la fuente.</summary>
        public string? DescriptionOverride { get; }

        /// <summary>Unidad explícita; <c>null</c> para tomar la de la fuente.</summary>
        public string? UnitOverride { get; }

        /// <summary>Cantidad propuesta por el usuario, sin convertirla a importes.</summary>
        public decimal ProposedQuantity { get; }

        /// <summary>
        /// Rowset COMPLETO de filas persistidas que la vista tenía al proponer el alta,
        /// ordenado por Orden e Id. Obligatorio para una fila nueva.
        /// </summary>
        public BudgetAssignmentRowset? ExpectedRowset { get; }

        /// <summary>Posición de inserción dentro de <see cref="ExpectedRowset"/>.</summary>
        public int? ProposedInsertionIndex { get; }
    }

    /// <summary>Una fila persistida del presupuesto en el rowset que la vista propone.</summary>
    public sealed class BudgetAssignmentRow
    {
        public BudgetAssignmentRow(int conceptId, int orden, int nivel, bool esAgrupador)
        {
            if (conceptId <= 0)
                throw new ArgumentOutOfRangeException(nameof(conceptId));

            ConceptId = conceptId;
            Orden = orden;
            Nivel = nivel;
            EsAgrupador = esAgrupador;
        }

        public int ConceptId { get; }

        public int Orden { get; }

        public int Nivel { get; }

        public bool EsAgrupador { get; }

        public static BudgetAssignmentRow FromConcepto(ConceptoPresupuesto concepto)
        {
            ArgumentNullException.ThrowIfNull(concepto);

            return new BudgetAssignmentRow(concepto.Id, concepto.Orden, concepto.Nivel, concepto.EsAgrupador);
        }
    }

    /// <summary>
    /// Rowset COMPLETO de filas persistidas que la vista tenía antes de crear una fila.
    /// El comando lo contrasta contra la base para detectar altas, bajas o cambios de
    /// orden/nivel que harían que la posición propuesta ya no represente lo que el
    /// usuario vio.
    /// </summary>
    public sealed class BudgetAssignmentRowset
    {
        public BudgetAssignmentRowset(IEnumerable<BudgetAssignmentRow> rows)
        {
            ArgumentNullException.ThrowIfNull(rows);

            var copia = rows.ToArray();
            if (copia.Any(r => r is null))
                throw new ArgumentException("El rowset no admite filas nulas.", nameof(rows));
            if (copia.Select(r => r.ConceptId).Distinct().Count() != copia.Length)
                throw new ArgumentException("El rowset no admite conceptos repetidos.", nameof(rows));

            Rows = copia
                .OrderBy(r => r.Orden)
                .ThenBy(r => r.ConceptId)
                .ToArray();
        }

        /// <summary>Filas ordenadas por Orden y, en empate, por Id.</summary>
        public IReadOnlyList<BudgetAssignmentRow> Rows { get; }

        public static BudgetAssignmentRowset FromConceptos(IEnumerable<ConceptoPresupuesto> conceptos)
        {
            ArgumentNullException.ThrowIfNull(conceptos);

            return new BudgetAssignmentRowset(conceptos
                .Where(c => c is not null && c.Id > 0)
                .Select(BudgetAssignmentRow.FromConcepto));
        }
    }

    /// <summary>
    /// Base inmutable de la matriz APU elegida en la vista: identidad, proyecto, textos,
    /// tipo y costo directo con los que se calculó lo que el usuario vio. El comando la
    /// recarga y revalida antes de usar la aritmética canónica.
    /// </summary>
    public sealed class BudgetMatrixAssignmentBase
    {
        public BudgetMatrixAssignmentBase(
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

        public static BudgetMatrixAssignmentBase FromMatriz(Matriz matriz)
        {
            ArgumentNullException.ThrowIfNull(matriz);

            return new BudgetMatrixAssignmentBase(
                matriz.Id,
                matriz.ProyectoId,
                matriz.Clave ?? string.Empty,
                matriz.Descripcion ?? string.Empty,
                matriz.Unidad ?? string.Empty,
                matriz.Tipo,
                matriz.CostoDirecto);
        }
    }

    /// <summary>Origen de una asignación de matriz.</summary>
    public enum BudgetAssignmentTargetKind
    {
        Matrix = 1,
        SourceConcept = 2
    }

    /// <summary>
    /// Fuente inmutable de la asignación. Una matriz viaja con su base esperada; una
    /// copia desde concepto viaja con la instantánea y la base esperada del origen.
    /// </summary>
    public sealed class BudgetAssignmentTarget
    {
        private BudgetAssignmentTarget(
            BudgetAssignmentTargetKind kind,
            int referenceId,
            BudgetMatrixAssignmentBase? expectedMatrix,
            BudgetPasteSourceConceptSnapshot? sourceConcept)
        {
            Kind = kind;
            ReferenceId = referenceId;
            ExpectedMatrix = expectedMatrix;
            SourceConcept = sourceConcept;
        }

        public BudgetAssignmentTargetKind Kind { get; }

        /// <summary>Id de la matriz APU o del concepto de origen según <see cref="Kind"/>.</summary>
        public int ReferenceId { get; }

        public BudgetMatrixAssignmentBase? ExpectedMatrix { get; }

        public BudgetPasteSourceConceptSnapshot? SourceConcept { get; }

        public static BudgetAssignmentTarget ForMatrix(
            int matrixId,
            BudgetMatrixAssignmentBase? expectedMatrix)
        {
            if (matrixId <= 0)
                throw new ArgumentOutOfRangeException(nameof(matrixId));
            if (expectedMatrix != null && expectedMatrix.MatrixId != matrixId)
                throw new ArgumentException(
                    "La base esperada de la matriz no corresponde al Id de la matriz.",
                    nameof(expectedMatrix));

            return new BudgetAssignmentTarget(
                BudgetAssignmentTargetKind.Matrix, matrixId, expectedMatrix, null);
        }

        public static BudgetAssignmentTarget ForSourceConcept(
            BudgetPasteSourceConceptSnapshot sourceConcept)
        {
            ArgumentNullException.ThrowIfNull(sourceConcept);

            return new BudgetAssignmentTarget(
                BudgetAssignmentTargetKind.SourceConcept,
                sourceConcept.ConceptId,
                null,
                sourceConcept);
        }
    }

    /// <summary>
    /// Base del concepto que interviene en la asignación de matriz: identidad, textos,
    /// cantidad, matriz vigente y valores derivados. Si alguno cambió en la base, el
    /// resultado ya no representaría lo que el usuario vio.
    /// </summary>
    public sealed class BudgetConceptAssignmentBase
    {
        public BudgetConceptAssignmentBase(
            string clave,
            string descripcion,
            string unidad,
            decimal cantidad,
            int? matrizId,
            decimal costoDirectoUnitario,
            decimal costoDirectoTotal,
            decimal precioUnitario,
            decimal importeTotal,
            bool esAgrupador)
        {
            Clave = clave ?? string.Empty;
            Descripcion = descripcion ?? string.Empty;
            Unidad = unidad ?? string.Empty;
            Cantidad = cantidad;
            MatrizId = matrizId;
            CostoDirectoUnitario = costoDirectoUnitario;
            CostoDirectoTotal = costoDirectoTotal;
            PrecioUnitario = precioUnitario;
            ImporteTotal = importeTotal;
            EsAgrupador = esAgrupador;
        }

        public string Clave { get; }

        public string Descripcion { get; }

        public string Unidad { get; }

        public decimal Cantidad { get; }

        public int? MatrizId { get; }

        public decimal CostoDirectoUnitario { get; }

        public decimal CostoDirectoTotal { get; }

        public decimal PrecioUnitario { get; }

        public decimal ImporteTotal { get; }

        public bool EsAgrupador { get; }

        /// <summary>Copia la base del concepto tal como la cargó la vista (antes de asignar).</summary>
        public static BudgetConceptAssignmentBase FromConcepto(ConceptoPresupuesto concepto)
        {
            ArgumentNullException.ThrowIfNull(concepto);

            return new BudgetConceptAssignmentBase(
                concepto.Clave,
                concepto.Descripcion,
                concepto.Unidad,
                concepto.Cantidad,
                concepto.MatrizId,
                concepto.CostoDirectoUnitario,
                concepto.CostoDirectoTotal,
                concepto.PrecioUnitario,
                concepto.ImporteTotal,
                concepto.EsAgrupador);
        }
    }
}
