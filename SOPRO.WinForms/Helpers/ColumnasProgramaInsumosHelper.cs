using SOPRO.Core.Entities;
using SOPRO.Data.Context;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;

namespace SOPRO.WinForms.Helpers
{
    public static class ColumnasProgramaInsumosHelper
    {
        public static void CrearColumnasPredeterminadas(SOPROContext context, int proyectoId)
        {
            if (context.ColumnasProgramaInsumos.Any(c => c.ProyectoId == proyectoId))
                return;

            var cols = new List<ColumnaProgramaInsumos>
            {

            };
            context.ColumnasProgramaInsumos.AddRange(cols);
            context.SaveChanges();
        }

        public static void SincronizarDesdeGrid(SOPROContext context, int proyectoId, DataGridViewColumnCollection columns)
        {
            var existentes = context.ColumnasProgramaInsumos.Where(c => c.ProyectoId == proyectoId).ToList();
            int maxOrden = existentes.Count == 0 ? 0 : existentes.Max(c => c.Orden);
            foreach (DataGridViewColumn col in columns)
            {
                if (col.Name == "colDummy") continue;
                if (existentes.Any(c => c.NombreInterno == col.Name)) continue;
                maxOrden++;
                context.ColumnasProgramaInsumos.Add(new ColumnaProgramaInsumos
                {
                    ProyectoId = proyectoId,
                    Nombre = string.IsNullOrWhiteSpace(col.HeaderText) ? col.Name : col.HeaderText,
                    NombreInterno = col.Name,
                    Visible = col.Visible,
                    Orden = maxOrden,
                    AnchoColumna = col.Width > 20 ? col.Width : 100,
                    Alineacion = AlineacionColumna.Izquierda
                });
            }
            context.SaveChanges();
        }

        public static List<ColumnaProgramaInsumos> ObtenerColumnas(SOPROContext context, int proyectoId)
        {
            
            return context.ColumnasProgramaInsumos.Where(c => c.ProyectoId == proyectoId).OrderBy(c => c.Orden).ToList();
        }
    }
}
