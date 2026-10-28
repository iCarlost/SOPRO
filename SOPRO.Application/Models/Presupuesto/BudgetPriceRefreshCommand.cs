using SOPRO.Core.Entities;

namespace SOPRO.Application.Models.Presupuesto
{
    /// <summary>
    /// Intención de refrescar los precios del presupuesto desde las matrices y la
    /// configuración vigente del proyecto (ADR-0001). Lleva la base económica del
    /// proyecto y el rowset COMPLETO de filas persistidas que la vista mostraba:
    /// el comando las revalida contra la base y deriva los importes con los
    /// servicios canónicos, nunca con valores calculados por el llamador.
    /// Los precios de las matrices NO viajan en la intención: su cambio es el
    /// motivo legítimo del refresco y se leen frescos de la base.
    /// </summary>
    public sealed class BudgetPriceRefreshCommand
    {
        public BudgetPriceRefreshCommand(
            int projectId,
            BudgetProjectCalculationBase expectedProject,
            BudgetPriceRefreshRowset expectedRows)
        {
            if (projectId <= 0)
                throw new ArgumentOutOfRangeException(nameof(projectId));

            ProjectId = projectId;
            ExpectedProject = expectedProject ?? throw new ArgumentNullException(nameof(expectedProject));
            ExpectedRows = expectedRows ?? throw new ArgumentNullException(nameof(expectedRows));

            if (expectedRows.Rows.Any(r => r.ProjectId != projectId))
                throw new ArgumentException(
                    "El rowset contiene filas de otro proyecto.", nameof(expectedRows));
        }

        public int ProjectId { get; }

        /// <summary>Base económica con la que la vista calculó lo que muestra.</summary>
        public BudgetProjectCalculationBase ExpectedProject { get; }

        /// <summary>Filas persistidas que la vista tenía, en el orden de la base.</summary>
        public BudgetPriceRefreshRowset ExpectedRows { get; }
    }

    /// <summary>
    /// Base de una fila persistida del presupuesto para el refresco de precios:
    /// identidad, pertenencia, posición jerárquica, matriz, cantidad y los cuatro
    /// valores económicos que el comando puede reescribir. Se comparan todos contra
    /// la base para no pisar una actualización ya propagada.
    /// </summary>
    public sealed class BudgetPriceRefreshRow
    {
        public BudgetPriceRefreshRow(
            int conceptId,
            int projectId,
            int orden,
            int nivel,
            bool esAgrupador,
            int? matrizId,
            decimal cantidad,
            decimal costoDirectoUnitario,
            decimal costoDirectoTotal,
            decimal precioUnitario,
            decimal importeTotal)
        {
            if (conceptId <= 0)
                throw new ArgumentOutOfRangeException(nameof(conceptId));
            if (projectId <= 0)
                throw new ArgumentOutOfRangeException(nameof(projectId));

            ConceptId = conceptId;
            ProjectId = projectId;
            Orden = orden;
            Nivel = nivel;
            EsAgrupador = esAgrupador;
            MatrizId = matrizId;
            Cantidad = cantidad;
            CostoDirectoUnitario = costoDirectoUnitario;
            CostoDirectoTotal = costoDirectoTotal;
            PrecioUnitario = precioUnitario;
            ImporteTotal = importeTotal;
        }

        public int ConceptId { get; }

        public int ProjectId { get; }

        public int Orden { get; }

        public int Nivel { get; }

        public bool EsAgrupador { get; }

        public int? MatrizId { get; }

        public decimal Cantidad { get; }

        public decimal CostoDirectoUnitario { get; }

        public decimal CostoDirectoTotal { get; }

        public decimal PrecioUnitario { get; }

        public decimal ImporteTotal { get; }

        public static BudgetPriceRefreshRow FromConcepto(ConceptoPresupuesto concepto)
        {
            ArgumentNullException.ThrowIfNull(concepto);

            return new BudgetPriceRefreshRow(
                concepto.Id,
                concepto.ProyectoId,
                concepto.Orden,
                concepto.Nivel,
                concepto.EsAgrupador,
                concepto.MatrizId,
                concepto.Cantidad,
                concepto.CostoDirectoUnitario,
                concepto.CostoDirectoTotal,
                concepto.PrecioUnitario,
                concepto.ImporteTotal);
        }
    }

    /// <summary>
    /// Rowset COMPLETO de filas persistidas que la vista tenía antes del refresco.
    /// El comando exige el mismo conjunto exacto de Ids y el mismo orden (por
    /// Orden e Id) para garantizar que los totales calculados correspondan a la
    /// jerarquía que el usuario vio.
    /// </summary>
    public sealed class BudgetPriceRefreshRowset
    {
        public BudgetPriceRefreshRowset(IEnumerable<BudgetPriceRefreshRow> rows)
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

        public IReadOnlyList<BudgetPriceRefreshRow> Rows { get; }

        public static BudgetPriceRefreshRowset FromConceptos(IEnumerable<ConceptoPresupuesto> conceptos)
        {
            ArgumentNullException.ThrowIfNull(conceptos);

            return new BudgetPriceRefreshRowset(conceptos
                .Where(c => c is not null && c.Id > 0)
                .Select(BudgetPriceRefreshRow.FromConcepto));
        }
    }
}
