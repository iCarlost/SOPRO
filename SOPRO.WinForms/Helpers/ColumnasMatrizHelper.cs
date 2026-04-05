using SOPRO.Core.Entities;
using SOPRO.Data.Context;
using System.Collections.Generic;
using System.Linq;

namespace SOPRO.WinForms.Helpers
{
    public static class ColumnasMatrizHelper
    {
        public static void CrearColumnasPredeterminadas(SOPROContext context, int proyectoId)
        {
            if (context.ColumnasMatriz.Any(c => c.ProyectoId == proyectoId))
                return;

            var cols = new[]
            {
                new ColumnaMatriz { ProyectoId = proyectoId, Nombre = "Clave",         NombreInterno = "Clave",        AnchoColumna = 120, Orden = 1, Alineacion = AlineacionColumna.Centro,    Visible = true },
                new ColumnaMatriz { ProyectoId = proyectoId, Nombre = "Descripción",   NombreInterno = "Descripcion",  AnchoColumna = 350, Orden = 2, Alineacion = AlineacionColumna.Izquierda, Visible = true },
                new ColumnaMatriz { ProyectoId = proyectoId, Nombre = "Unidad",        NombreInterno = "Unidad",       AnchoColumna =  80, Orden = 3, Alineacion = AlineacionColumna.Centro,    Visible = true },
                new ColumnaMatriz { ProyectoId = proyectoId, Nombre = "Tipo",          NombreInterno = "Tipo",         AnchoColumna = 100, Orden = 4, Alineacion = AlineacionColumna.Centro,    Visible = true },
                new ColumnaMatriz { ProyectoId = proyectoId, Nombre = "Origen",        NombreInterno = "OrigenDetalle", AnchoColumna = 180, Orden = 5, Alineacion = AlineacionColumna.Izquierda, Visible = true },
                new ColumnaMatriz { ProyectoId = proyectoId, Nombre = "Costo Directo", NombreInterno = "CostoDirecto", AnchoColumna = 120, Orden = 6, Alineacion = AlineacionColumna.Derecha,   Visible = true, FormatoNumerico = "#,##0.0000" },
                new ColumnaMatriz { ProyectoId = proyectoId, Nombre = "# Insumos",     NombreInterno = "NumInsumos",   AnchoColumna =  90, Orden = 7, Alineacion = AlineacionColumna.Centro,    Visible = true },
            };

            context.ColumnasMatriz.AddRange(cols);
            context.SaveChanges();
        }

        public static List<ColumnaMatriz> ObtenerColumnas(SOPROContext context, int proyectoId)
        {
            CrearColumnasPredeterminadas(context, proyectoId);
            AsegurarColumnasNuevas(context, proyectoId);
            return context.ColumnasMatriz
                .Where(c => c.ProyectoId == proyectoId)
                .OrderBy(c => c.Orden)
                .ToList();
        }

        private static void AsegurarColumnasNuevas(SOPROContext context, int proyectoId)
        {
            var existentes = context.ColumnasMatriz.Where(c => c.ProyectoId == proyectoId).ToList();
            if (!existentes.Any(c => c.NombreInterno == "OrigenDetalle"))
            {
                context.ColumnasMatriz.Add(new ColumnaMatriz
                {
                    ProyectoId = proyectoId,
                    Nombre = "Origen",
                    NombreInterno = "OrigenDetalle",
                    AnchoColumna = 180,
                    Orden = 5,
                    Alineacion = AlineacionColumna.Izquierda,
                    Visible = true
                });

                foreach (var col in existentes.Where(c => c.Orden >= 5))
                    col.Orden += 1;

                context.SaveChanges();
            }
        }
    }
}
