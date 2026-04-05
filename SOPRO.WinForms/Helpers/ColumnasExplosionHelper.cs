using SOPRO.Core.Entities;
using SOPRO.Data.Context;
using System.Collections.Generic;
using System.Linq;

namespace SOPRO.WinForms.Helpers
{
    public static class ColumnasExplosionHelper
    {
        public static void CrearColumnasPredeterminadas(SOPROContext context, int proyectoId)
        {
            if (context.ColumnasExplosion.Any(c => c.ProyectoId == proyectoId))
                return;

            var cols = new[]
            {
                new ColumnaExplosion { ProyectoId = proyectoId, Nombre = "Clave",       NombreInterno = "Clave",          AnchoColumna = 80,  Orden = 1, Alineacion = AlineacionColumna.Izquierda, Visible = true  },
                new ColumnaExplosion { ProyectoId = proyectoId, Nombre = "Descripcion", NombreInterno = "Descripcion",    AnchoColumna = 350, Orden = 2, Alineacion = AlineacionColumna.Izquierda, Visible = true  },
                new ColumnaExplosion { ProyectoId = proyectoId, Nombre = "Unidad",      NombreInterno = "Unidad",         AnchoColumna = 70,  Orden = 3, Alineacion = AlineacionColumna.Centro,    Visible = true  },
                new ColumnaExplosion { ProyectoId = proyectoId, Nombre = "Cantidad",    NombreInterno = "Cantidad",       AnchoColumna = 100, Orden = 4, Alineacion = AlineacionColumna.Derecha,   Visible = true,  FormatoNumerico = "N4" },
                new ColumnaExplosion { ProyectoId = proyectoId, Nombre = "P.U.",        NombreInterno = "PrecioUnitario", AnchoColumna = 120, Orden = 5, Alineacion = AlineacionColumna.Derecha,   Visible = true,  FormatoNumerico = "C4" },
                new ColumnaExplosion { ProyectoId = proyectoId, Nombre = "Importe",     NombreInterno = "Importe",        AnchoColumna = 130, Orden = 6, Alineacion = AlineacionColumna.Derecha,   Visible = true,  FormatoNumerico = "C2" },
                new ColumnaExplosion { ProyectoId = proyectoId, Nombre = "%",           NombreInterno = "Porcentaje",     AnchoColumna = 80,  Orden = 7, Alineacion = AlineacionColumna.Derecha,   Visible = true,  FormatoNumerico = "P2" },
            };

            context.ColumnasExplosion.AddRange(cols);
            context.SaveChanges();
        }

        public static List<ColumnaExplosion> ObtenerColumnas(SOPROContext context, int proyectoId)
        {
            CrearColumnasPredeterminadas(context, proyectoId);
            return context.ColumnasExplosion
                .Where(c => c.ProyectoId == proyectoId)
                .OrderBy(c => c.Orden)
                .ToList();
        }
    }
}
