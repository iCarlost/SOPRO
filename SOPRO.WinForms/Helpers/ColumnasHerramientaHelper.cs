using SOPRO.Core.Entities;
using SOPRO.Data.Context;
using System.Collections.Generic;
using System.Linq;

namespace SOPRO.WinForms.Helpers
{
    public static class ColumnasHerramientaHelper
    {
        public static void CrearColumnasPredeterminadas(SOPROContext context, int proyectoId)
        {
            if (context.ColumnasHerramienta.Any(c => c.ProyectoId == proyectoId))
                return;

            var cols = new[]
            {
                new ColumnaHerramienta { ProyectoId = proyectoId, Nombre = "Clave",             NombreInterno = "Clave",          AnchoColumna = 110, Orden = 1, Alineacion = AlineacionColumna.Centro,   Visible = true },
                new ColumnaHerramienta { ProyectoId = proyectoId, Nombre = "Descripción",       NombreInterno = "Descripcion",    AnchoColumna = 400, Orden = 2, Alineacion = AlineacionColumna.Izquierda,Visible = true },
                new ColumnaHerramienta { ProyectoId = proyectoId, Nombre = "Unidad",            NombreInterno = "Unidad",         AnchoColumna =  80, Orden = 3, Alineacion = AlineacionColumna.Centro,   Visible = true },
                new ColumnaHerramienta { ProyectoId = proyectoId, Nombre = "Precio/Porcentaje", NombreInterno = "PrecioUnitario", AnchoColumna = 150, Orden = 4, Alineacion = AlineacionColumna.Derecha,  Visible = true },
                new ColumnaHerramienta { ProyectoId = proyectoId, Nombre = "Origen",            NombreInterno = "OrigenDetalle", AnchoColumna = 180, Orden = 5, Alineacion = AlineacionColumna.Izquierda,Visible = true },
            };

            context.ColumnasHerramienta.AddRange(cols);
            context.SaveChanges();
        }

        public static List<ColumnaHerramienta> ObtenerColumnas(SOPROContext context, int proyectoId)
        {
            CrearColumnasPredeterminadas(context, proyectoId);
            var existentes = context.ColumnasHerramienta.Where(c => c.ProyectoId == proyectoId).ToList();
            if (!existentes.Any(c => c.NombreInterno == "OrigenDetalle"))
            {
                context.ColumnasHerramienta.Add(new ColumnaHerramienta
                {
                    ProyectoId = proyectoId,
                    Nombre = "Origen",
                    NombreInterno = "OrigenDetalle",
                    AnchoColumna = 180,
                    Orden = (existentes.Count == 0 ? 5 : existentes.Max(c => c.Orden) + 1),
                    Alineacion = AlineacionColumna.Izquierda,
                    Visible = true
                });
                context.SaveChanges();
            }
            return context.ColumnasHerramienta
                .Where(c => c.ProyectoId == proyectoId)
                .OrderBy(c => c.Orden)
                .ToList();
        }
    }
}
