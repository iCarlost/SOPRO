using SOPRO.Core.Entities;
using SOPRO.Data.Context;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;

namespace SOPRO.WinForms.Helpers
{
    public static class ColumnasProgramaObraHelper
    {
        public static void CrearColumnasPredeterminadas(SOPROContext context, int proyectoId)
        {
            if (context.ColumnasProgramaObra.Any(c => c.ProyectoId == proyectoId))
                return;

            var cols = new List<ColumnaProgramaObra>
            {
                new ColumnaProgramaObra { ProyectoId = proyectoId, Nombre = "Orden", NombreInterno = "colOrden", AnchoColumna = 60, Orden = 1, Alineacion = AlineacionColumna.Centro, Visible = true },
                new ColumnaProgramaObra { ProyectoId = proyectoId, Nombre = "Clave", NombreInterno = "colClave", AnchoColumna = 110, Orden = 2, Alineacion = AlineacionColumna.Centro, Visible = true },
                new ColumnaProgramaObra { ProyectoId = proyectoId, Nombre = "Descripción", NombreInterno = "colDescripcion", AnchoColumna = 360, Orden = 3, Alineacion = AlineacionColumna.Izquierda, Visible = true },
                new ColumnaProgramaObra { ProyectoId = proyectoId, Nombre = "Unidad", NombreInterno = "colUnidad", AnchoColumna = 70, Orden = 4, Alineacion = AlineacionColumna.Centro, Visible = true },
                new ColumnaProgramaObra { ProyectoId = proyectoId, Nombre = "Predecesora", NombreInterno = "colPredecesora", AnchoColumna = 140, Orden = 5, Alineacion = AlineacionColumna.Izquierda, Visible = true },
                new ColumnaProgramaObra { ProyectoId = proyectoId, Nombre = "Cantidad", NombreInterno = "colCantidad", AnchoColumna = 90, Orden = 6, Alineacion = AlineacionColumna.Derecha, Visible = true },
                new ColumnaProgramaObra { ProyectoId = proyectoId, Nombre = "Inicio", NombreInterno = "colFechaInicio", AnchoColumna = 95, Orden = 7, Alineacion = AlineacionColumna.Centro, Visible = true },
                new ColumnaProgramaObra { ProyectoId = proyectoId, Nombre = "Fin", NombreInterno = "colFechaFin", AnchoColumna = 95, Orden = 8, Alineacion = AlineacionColumna.Centro, Visible = true },
                new ColumnaProgramaObra { ProyectoId = proyectoId, Nombre = "Días hábiles", NombreInterno = "colDuracionDias", AnchoColumna = 90, Orden = 9, Alineacion = AlineacionColumna.Derecha, Visible = true },
                new ColumnaProgramaObra { ProyectoId = proyectoId, Nombre = "Rend. diario", NombreInterno = "colRendimientoDiario", AnchoColumna = 95, Orden = 10, Alineacion = AlineacionColumna.Derecha, Visible = true },
                new ColumnaProgramaObra { ProyectoId = proyectoId, Nombre = "Frentes", NombreInterno = "colFrentes", AnchoColumna = 70, Orden = 11, Alineacion = AlineacionColumna.Derecha, Visible = true },
                new ColumnaProgramaObra { ProyectoId = proyectoId, Nombre = "P.U.", NombreInterno = "colPrecioUnitario", AnchoColumna = 90, Orden = 12, Alineacion = AlineacionColumna.Derecha, Visible = true },
                new ColumnaProgramaObra { ProyectoId = proyectoId, Nombre = "Importe", NombreInterno = "colImporte", AnchoColumna = 105, Orden = 13, Alineacion = AlineacionColumna.Derecha, Visible = true },
                new ColumnaProgramaObra { ProyectoId = proyectoId, Nombre = "Crítica", NombreInterno = "colRutaCritica", AnchoColumna = 70, Orden = 14, Alineacion = AlineacionColumna.Centro, Visible = true },
            };
            context.ColumnasProgramaObra.AddRange(cols);
            context.SaveChanges();
        }

        public static void SincronizarDesdeGrid(SOPROContext context, int proyectoId, DataGridViewColumnCollection columns)
        {
            var existentes = context.ColumnasProgramaObra.Where(c => c.ProyectoId == proyectoId).ToList();
            int maxOrden = existentes.Count == 0 ? 0 : existentes.Max(c => c.Orden);
            foreach (DataGridViewColumn col in columns)
            {
                if (col.Name == "colDummy") continue;
                if (existentes.Any(c => c.NombreInterno == col.Name)) continue;
                maxOrden++;
                context.ColumnasProgramaObra.Add(new ColumnaProgramaObra
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

        public static List<ColumnaProgramaObra> ObtenerColumnas(SOPROContext context, int proyectoId)
        {
            CrearColumnasPredeterminadas(context, proyectoId);
            return context.ColumnasProgramaObra.Where(c => c.ProyectoId == proyectoId).OrderBy(c => c.Orden).ToList();
        }
    }
}
