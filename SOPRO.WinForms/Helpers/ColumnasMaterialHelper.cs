using SOPRO.Core.Entities;
using SOPRO.Data.Context;
using System.Collections.Generic;
using System.Linq;

namespace SOPRO.WinForms.Helpers
{
    public static class ColumnasMaterialHelper
    {
        public static void CrearColumnasPredeterminadas(SOPROContext context, int proyectoId)
        {
            if (context.ColumnasMaterial.Any(c => c.ProyectoId == proyectoId))
                return;

            var cols = new[]
            {
                new ColumnaMaterial { ProyectoId = proyectoId, Nombre = "Clave",          NombreInterno = "Clave",          AnchoColumna = 110, Orden = 1, Alineacion = AlineacionColumna.Centro,   Visible = true },
                new ColumnaMaterial { ProyectoId = proyectoId, Nombre = "Descripción",    NombreInterno = "Descripcion",    AnchoColumna = 400, Orden = 2, Alineacion = AlineacionColumna.Izquierda,Visible = true },
                new ColumnaMaterial { ProyectoId = proyectoId, Nombre = "Unidad",         NombreInterno = "Unidad",         AnchoColumna =  80, Orden = 3, Alineacion = AlineacionColumna.Centro,   Visible = true },
                new ColumnaMaterial { ProyectoId = proyectoId, Nombre = "Precio Unitario",NombreInterno = "PrecioUnitario", AnchoColumna = 140, Orden = 4, Alineacion = AlineacionColumna.Derecha,  Visible = true, FormatoNumerico = "C4" },
                new ColumnaMaterial { ProyectoId = proyectoId, Nombre = "Origen",         NombreInterno = "Origen",         AnchoColumna =  80, Orden = 5, Alineacion = AlineacionColumna.Centro,   Visible = true },
            };

            context.ColumnasMaterial.AddRange(cols);
            context.SaveChanges();
        }

        public static List<ColumnaMaterial> ObtenerColumnas(SOPROContext context, int proyectoId)
        {
            CrearColumnasPredeterminadas(context, proyectoId);
            return context.ColumnasMaterial
                .Where(c => c.ProyectoId == proyectoId)
                .OrderBy(c => c.Orden)
                .ToList();
        }
    }
}
