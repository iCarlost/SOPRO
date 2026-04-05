namespace SOPRO.Application.Models.Explosion
{
    public sealed class ExplosionInsumoAccumulated
    {
        public string Clave { get; set; } = string.Empty;
        public string Descripcion { get; set; } = string.Empty;
        public string Unidad { get; set; } = string.Empty;

        /// <summary>
        /// Importe acumulado (se muestra como "Total" en el reporte).
        /// Nombrado Cantidad por compatibilidad con el código de UI existente.
        /// </summary>
        public decimal Cantidad { get; set; }

        /// <summary>
        /// Cantidad del reporte:
        ///   Normal:     INFERIDA = ImporteAcumulado / PU_catálogo
        ///   Porcentual: acumulada físicamente (comp.Cantidad × cantidad concepto)
        /// </summary>
        public decimal CantidadFisica { get; set; }

        /// <summary>
        /// Precio unitario del reporte:
        ///   Normal:     fijo del catálogo
        ///   Porcentual: INFERIDO = ImporteAcumulado / CantidadFisica (promedio ponderado)
        /// </summary>
        public decimal PrecioUnitario { get; set; }

        /// <summary>
        /// True si el insumo es de tipo %MO (porcentaje sobre mano de obra).
        /// En ese caso Cantidad es física acumulada y PrecioUnitario es inferido.
        /// </summary>
        public bool EsPorcentual { get; set; }
    }
}
