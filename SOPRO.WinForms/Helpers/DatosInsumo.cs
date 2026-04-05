namespace SOPRO.WinForms.Helpers
{
    /// <summary>
    /// Acumulador de insumo para la explosión de insumos.
    /// Cantidad = importe acumulado (método OPUS PLANET).
    /// PrecioUnitario = PU del insumo (fijo del catálogo para normales, inferido para %MO).
    /// EsPorcentual = true para insumos %MO.
    /// </summary>
    public struct DatosInsumo
    {
        public string Clave, Descripcion, Unidad;
        public decimal Cantidad;        // importe acumulado (Total en reporte)
        public decimal CantidadFisica;  // cantidad inferida (normal) o física acumulada (%MO)
        public decimal PrecioUnitario;  // fijo del catálogo (normal) o inferido (%MO)
        public bool EsPorcentual;       // true = %MO, mostrar cantidad y PU con "—" o inferido
    }
}
