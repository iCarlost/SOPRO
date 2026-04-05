using SOPRO.Core.Entities;
using SOPRO.Data.Context;
using System.Collections.Generic;
using System.Linq;

namespace SOPRO.WinForms.Helpers
{
    public static class ColumnasMaquinariaHelper
    {
        public static void CrearColumnasPredeterminadas(SOPROContext context, int proyectoId)
        {
            if (context.ColumnasMaquinaria.Any(c => c.ProyectoId == proyectoId))
                return;

            var cols = new[]
            {
                new ColumnaMaquinaria { ProyectoId = proyectoId, Nombre = "Clave",          NombreInterno = "Clave",         AnchoColumna = 110, Orden = 1, Alineacion = AlineacionColumna.Centro,   Visible = true },
                new ColumnaMaquinaria { ProyectoId = proyectoId, Nombre = "Descripción",    NombreInterno = "Descripcion",   AnchoColumna = 320, Orden = 2, Alineacion = AlineacionColumna.Izquierda,Visible = true },
                new ColumnaMaquinaria { ProyectoId = proyectoId, Nombre = "Potencia (HP)",  NombreInterno = "PotenciaNominal",AnchoColumna= 100, Orden = 3, Alineacion = AlineacionColumna.Derecha,  Visible = true, FormatoNumerico = "N2" },
                new ColumnaMaquinaria { ProyectoId = proyectoId, Nombre = "Combustible",    NombreInterno = "Combustible",   AnchoColumna = 100, Orden = 4, Alineacion = AlineacionColumna.Centro,   Visible = true },
                new ColumnaMaquinaria { ProyectoId = proyectoId, Nombre = "Costo Horario",  NombreInterno = "CostoHorario",  AnchoColumna = 120, Orden = 5, Alineacion = AlineacionColumna.Derecha,  Visible = true, FormatoNumerico = "#,##0.00" },
                new ColumnaMaquinaria { ProyectoId = proyectoId, Nombre = "Tipo",           NombreInterno = "TipoCosto",     AnchoColumna =  80, Orden = 6, Alineacion = AlineacionColumna.Centro,   Visible = true },
                new ColumnaMaquinaria { ProyectoId = proyectoId, Nombre = "Origen",         NombreInterno = "Origen",        AnchoColumna =  80, Orden = 7, Alineacion = AlineacionColumna.Centro,   Visible = true },
            };

            context.ColumnasMaquinaria.AddRange(cols);
            context.SaveChanges();
        }

        public static List<ColumnaMaquinaria> ObtenerColumnas(SOPROContext context, int proyectoId)
        {
            CrearColumnasPredeterminadas(context, proyectoId);
            return context.ColumnasMaquinaria
                .Where(c => c.ProyectoId == proyectoId)
                .OrderBy(c => c.Orden)
                .ToList();
        }
    }
}
