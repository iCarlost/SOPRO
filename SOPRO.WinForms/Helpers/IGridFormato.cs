using SOPRO.Core.Entities;
using System.Windows.Forms;

namespace SOPRO.WinForms.Helpers
{
    /// <summary>
    /// Interfaz que implementan los forms hijo que soportan
    /// formato de columnas desde el ribbon de FormProyecto.
    /// </summary>
    public interface IGridFormato
    {
        DataGridView GridPrincipal { get; }
        ColumnaPersonalizada ColumnaSeleccionada { get; }
        void AplicarFormato(ColumnaPersonalizada fmt);
        void AplicarFormatoGlobal(ColumnaPersonalizada fmt);
        event System.EventHandler ColumnaSeleccionadaCambiada;

        /// <summary>
        /// Genera el reporte Excel del módulo activo.
        /// Devuelve true si se generó exitosamente, false si no aplica o fue cancelado.
        /// </summary>
        bool GenerarReporteExcel();
    }
}
