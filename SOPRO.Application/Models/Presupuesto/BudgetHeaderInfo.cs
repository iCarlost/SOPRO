namespace SOPRO.Application.Models.Presupuesto
{
    /// <summary>
    /// Valor individual de porcentaje del proyecto listo para presentarse como
    /// un chip independiente en el encabezado del presupuesto.
    /// </summary>
    public class BudgetHeaderPorcentajeChip
    {
        public string Etiqueta { get; set; } = string.Empty;
        public string Valor { get; set; } = string.Empty;
    }

    public class BudgetHeaderInfo
    {
        public string ProyectoNombre { get; set; } = string.Empty;
        public string PorcentajesTexto { get; set; } = string.Empty;
        public decimal FactorPrecioUnitario { get; set; }
        public bool TienePorcentajesActivos { get; set; }

        /// <summary>
        /// Porcentajes activos separados por chip (etiqueta + valor formateado).
        /// Vacío cuando no hay porcentajes configurados; en ese caso la UI
        /// presenta <see cref="PorcentajesTexto"/> como advertencia única.
        /// </summary>
        public IReadOnlyList<BudgetHeaderPorcentajeChip> PorcentajesChips { get; set; } =
            Array.Empty<BudgetHeaderPorcentajeChip>();
    }
}
