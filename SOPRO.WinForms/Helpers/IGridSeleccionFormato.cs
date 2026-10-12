using System.Collections.Generic;
using SOPRO.Application.Models.Reporting.ReportColumns;

namespace SOPRO.WinForms.Helpers
{
    /// <summary>
    /// Destino del formato que el ribbon aplicará sobre el grid activo.
    /// </summary>
    public enum DestinoFormatoGrid
    {
        /// <summary>No hay nada formateable seleccionado.</summary>
        Ninguno = 0,

        /// <summary>Se formateará la columna cuyo encabezado está seleccionado.</summary>
        Columna = 1,

        /// <summary>Se formatearán las celdas seleccionadas del grid.</summary>
        Celdas = 2,
    }

    /// <summary>
    /// Par (dirección estable, overlay antes, overlay después) de UNA celda
    /// afectada por una operación de formato desde el ribbon. <see cref="Antes"/>
    /// puede ser <c>null</c> si la celda no tenía override previo. El consumidor
    /// de persistencia (Fase 4.1) usará <see cref="Despues"/> para rehacer y
    /// <see cref="Antes"/> para deshacer, ambos como upsert parcial sobre
    /// <c>FormatoReporteService</c>.
    /// </summary>
    /// <param name="Direccion">Referencia ESTABLE de la celda destino.</param>
    /// <param name="Antes">Overlay previo de la celda, o <c>null</c>.</param>
    /// <param name="Despues">Overlay aplicado a la celda.</param>
    public sealed record CambioFormatoSeleccion(
        ReportCellAddress Direccion,
        ReportStyleOverlay? Antes,
        ReportStyleOverlay Despues);

    /// <summary>
    /// Contrato que implementa un grid para reportar su SELECCIÓN DE CELDAS
    /// (una celda, un rango contiguo con Shift o varios rangos discontinuos con
    /// Ctrl) como direcciones ESTABLES (<see cref="ReportCellAddress"/>) y para
    /// aplicar formato por celda pintando de inmediato en el control.
    ///
    /// Los grids que NO implementen esta interfaz conservan el comportamiento
    /// por COLUMNA de <see cref="IGridFormato"/>: el ribbon no cambia para ellos.
    /// La selección de ENCABEZADO también mantiene el comportamiento por columna.
    /// </summary>
    public interface IGridSeleccionFormato
    {
        /// <summary>
        /// <c>true</c> si el grid tiene actualmente una selección de CELDAS (no
        /// de encabezado). Cuando es <c>false</c> el ribbon usa la columna.
        /// </summary>
        bool TieneSeleccionCeldas { get; }

        /// <summary>
        /// Celdas seleccionadas como direcciones ESTABLES. Puede contener
        /// direcciones repetidas (Ctrl) y celdas no formateables; el coordinador
        /// deduplica y descarta filas vacías / columnas no exportables.
        /// </summary>
        IReadOnlyList<ReportCellAddress> ObtenerDireccionesSeleccionadas();

        /// <summary>
        /// <c>false</c> para filas vacías o columnas no exportables, que deben
        /// ignorarse al aplicar formato.
        /// </summary>
        bool EsCeldaFormateable(ReportCellAddress direccion);

        /// <summary>
        /// Overlay actual (estado "antes") de una celda, o <c>null</c> si la
        /// celda no tiene override propio.
        /// </summary>
        ReportStyleOverlay? ObtenerOverlayActual(ReportCellAddress direccion);

        /// <summary>
        /// Aplica (upsert parcial) el overlay a UNA celda y pinta el cambio de
        /// inmediato en el control.
        /// </summary>
        void AplicarOverlayCelda(ReportCellAddress direccion, ReportStyleOverlay overlay);
    }
}
