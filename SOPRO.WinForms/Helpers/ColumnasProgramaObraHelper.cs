using SOPRO.Application.UseCases.Reporting;
using SOPRO.Core.Entities;
using SOPRO.Data.Context;
using System.Collections.Generic;
using System.Linq;

namespace SOPRO.WinForms.Helpers
{
    /// <summary>
    /// Configuración persistida de columnas del Programa de Obra. Los defaults se
    /// construyen en memoria desde el contrato neutral
    /// (<see cref="ProgramaObraReportSnapshotBuilder.ColumnasPredeterminadas"/>) para
    /// tener una única fuente de verdad con el reporte.
    /// </summary>
    public static class ColumnasProgramaObraHelper
    {
        /// <summary>
        /// Devuelve las columnas predeterminadas del Programa de Obra en memoria
        /// (sin escribir en la base de datos).
        /// </summary>
        public static List<ColumnaProgramaObra> ColumnasPredeterminadas(int proyectoId)
        {
            return ProgramaObraReportSnapshotBuilder.ColumnasPredeterminadas()
                .Select(c => new ColumnaProgramaObra
                {
                    ProyectoId = proyectoId,
                    Nombre = c.Nombre,
                    NombreInterno = c.NombreInterno,
                    AnchoColumna = c.AnchoColumna,
                    Orden = c.Orden,
                    Alineacion = c.Alineacion,
                    Visible = c.Visible,
                    FormatoNumerico = c.FormatoNumerico
                })
                .ToList();
        }

        public static void CrearColumnasPredeterminadas(SOPROContext context, int proyectoId)
        {
            if (context.ColumnasProgramaObra.Any(c => c.ProyectoId == proyectoId))
                return;

            context.ColumnasProgramaObra.AddRange(ColumnasPredeterminadas(proyectoId));
            context.SaveChanges();
        }

        public static List<ColumnaProgramaObra> ObtenerColumnas(SOPROContext context, int proyectoId)
        {
            CrearColumnasPredeterminadas(context, proyectoId);
            return context.ColumnasProgramaObra.Where(c => c.ProyectoId == proyectoId).OrderBy(c => c.Orden).ToList();
        }
    }
}
