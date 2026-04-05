using SOPRO.Core.Entities;
using SOPRO.Data.Context;
using System.Collections.Generic;
using System.Linq;

namespace SOPRO.WinForms.Helpers
{
    public static class ColumnasIndirectosHelper
    {
        public static void CrearColumnasPredeterminadas(SOPROContext context, int proyectoId)
        {
            if (context.ColumnasIndirectos.Any(c => c.ProyectoId == proyectoId))
                return;

            var cols = new[]
            {
                new ColumnaIndirectos { ProyectoId = proyectoId, Nombre = "Grupo / Concepto",  NombreInterno = "Grupo",          AnchoColumna = 400, Orden = 1, Alineacion = AlineacionColumna.Izquierda, Visible = true },
                new ColumnaIndirectos { ProyectoId = proyectoId, Nombre = "Importe Mensual $", NombreInterno = "ImporteMensual", AnchoColumna = 180, Orden = 2, Alineacion = AlineacionColumna.Derecha,   Visible = true, FormatoNumerico = "N2" },
                new ColumnaIndirectos { ProyectoId = proyectoId, Nombre = "Duración (Meses)",  NombreInterno = "Duracion",       AnchoColumna = 150, Orden = 3, Alineacion = AlineacionColumna.Centro,    Visible = true },
                new ColumnaIndirectos { ProyectoId = proyectoId, Nombre = "Importe Total $",   NombreInterno = "ImporteTotal",   AnchoColumna = 180, Orden = 4, Alineacion = AlineacionColumna.Derecha,   Visible = true, FormatoNumerico = "N2" },
            };

            context.ColumnasIndirectos.AddRange(cols);
            context.SaveChanges();
        }

        public static List<ColumnaIndirectos> ObtenerColumnas(SOPROContext context, int proyectoId)
        {
            CrearColumnasPredeterminadas(context, proyectoId);
            return context.ColumnasIndirectos
                .Where(c => c.ProyectoId == proyectoId)
                .OrderBy(c => c.Orden)
                .ToList();
        }
    }
}
