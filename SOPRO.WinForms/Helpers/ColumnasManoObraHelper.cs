using SOPRO.Core.Entities;
using SOPRO.Data.Context;
using System.Collections.Generic;
using System.Linq;

namespace SOPRO.WinForms.Helpers
{
    public static class ColumnasManoObraHelper
    {
        public static void CrearColumnasPredeterminadas(SOPROContext context, int proyectoId)
        {
            if (context.ColumnasManoObra.Any(c => c.ProyectoId == proyectoId))
                return;

            var cols = new[]
            {
                new ColumnaManoObra { ProyectoId = proyectoId, Nombre = "Clave",        NombreInterno = "Clave",            AnchoColumna = 110, Orden = 1, Alineacion = AlineacionColumna.Centro,   Visible = true },
                new ColumnaManoObra { ProyectoId = proyectoId, Nombre = "Descripción",  NombreInterno = "Descripcion",      AnchoColumna = 350, Orden = 2, Alineacion = AlineacionColumna.Izquierda,Visible = true },
                new ColumnaManoObra { ProyectoId = proyectoId, Nombre = "Unidad",       NombreInterno = "Unidad",           AnchoColumna =  70, Orden = 3, Alineacion = AlineacionColumna.Centro,   Visible = true },
                new ColumnaManoObra { ProyectoId = proyectoId, Nombre = "Salario Base", NombreInterno = "SalarioBase",      AnchoColumna = 120, Orden = 4, Alineacion = AlineacionColumna.Derecha,  Visible = true, FormatoNumerico = "C2" },
                new ColumnaManoObra { ProyectoId = proyectoId, Nombre = "FSR",          NombreInterno = "FactorSalarioReal",AnchoColumna =  80, Orden = 5, Alineacion = AlineacionColumna.Derecha,  Visible = true, FormatoNumerico = "N4" },
                new ColumnaManoObra { ProyectoId = proyectoId, Nombre = "Salario Real", NombreInterno = "SalarioReal",      AnchoColumna = 120, Orden = 6, Alineacion = AlineacionColumna.Derecha,  Visible = true, FormatoNumerico = "C2" },
                new ColumnaManoObra { ProyectoId = proyectoId, Nombre = "Origen",       NombreInterno = "Origen",           AnchoColumna =  80, Orden = 7, Alineacion = AlineacionColumna.Centro,   Visible = true },
            };

            context.ColumnasManoObra.AddRange(cols);
            context.SaveChanges();
        }

        public static List<ColumnaManoObra> ObtenerColumnas(SOPROContext context, int proyectoId)
        {
            CrearColumnasPredeterminadas(context, proyectoId);
            return context.ColumnasManoObra
                .Where(c => c.ProyectoId == proyectoId)
                .OrderBy(c => c.Orden)
                .ToList();
        }
    }
}
