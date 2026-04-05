using SOPRO.Core.Entities;
using SOPRO.Data.Context;
using System.Collections.Generic;
using System.Linq;

namespace SOPRO.WinForms.Helpers
{
    public static class ColumnasFinanciamientoHelper
    {
        public static void CrearColumnasPredeterminadas(SOPROContext context, int proyectoId)
        {
            if (context.ColumnasFinanciamiento.Any(c => c.ProyectoId == proyectoId))
                return;

            var cols = new[]
            {
                new ColumnaFinanciamiento { ProyectoId = proyectoId, Nombre = "Período",        NombreInterno = "colPeriodo",   AnchoColumna = 90,  Orden = 1,  Alineacion = AlineacionColumna.Centro,    Visible = true },
                new ColumnaFinanciamiento { ProyectoId = proyectoId, Nombre = "Inicio",         NombreInterno = "colInicio",    AnchoColumna = 75,  Orden = 2,  Alineacion = AlineacionColumna.Centro,    Visible = true },
                new ColumnaFinanciamiento { ProyectoId = proyectoId, Nombre = "Fin",            NombreInterno = "colFin",       AnchoColumna = 75,  Orden = 3,  Alineacion = AlineacionColumna.Centro,    Visible = true },
                new ColumnaFinanciamiento { ProyectoId = proyectoId, Nombre = "Días",           NombreInterno = "colDias",      AnchoColumna = 45,  Orden = 4,  Alineacion = AlineacionColumna.Centro,    Visible = true },
                new ColumnaFinanciamiento { ProyectoId = proyectoId, Nombre = "CD $",           NombreInterno = "colCD",        AnchoColumna = 95,  Orden = 5,  Alineacion = AlineacionColumna.Derecha,   Visible = true, FormatoNumerico = "C2" },
                new ColumnaFinanciamiento { ProyectoId = proyectoId, Nombre = "CI $",           NombreInterno = "colCI",        AnchoColumna = 95,  Orden = 6,  Alineacion = AlineacionColumna.Derecha,   Visible = true, FormatoNumerico = "C2" },
                new ColumnaFinanciamiento { ProyectoId = proyectoId, Nombre = "Egreso $",       NombreInterno = "colEgresos",   AnchoColumna = 105, Orden = 7,  Alineacion = AlineacionColumna.Derecha,   Visible = true, FormatoNumerico = "C2" },
                new ColumnaFinanciamiento { ProyectoId = proyectoId, Nombre = "Anticipo $",     NombreInterno = "colAnticipo",  AnchoColumna = 95,  Orden = 8,  Alineacion = AlineacionColumna.Derecha,   Visible = true, FormatoNumerico = "C2" },
                new ColumnaFinanciamiento { ProyectoId = proyectoId, Nombre = "Estimación $",   NombreInterno = "colEstim",     AnchoColumna = 105, Orden = 9,  Alineacion = AlineacionColumna.Derecha,   Visible = true, FormatoNumerico = "C2" },
                new ColumnaFinanciamiento { ProyectoId = proyectoId, Nombre = "Amortización $", NombreInterno = "colAmort",     AnchoColumna = 110, Orden = 10, Alineacion = AlineacionColumna.Derecha,   Visible = true, FormatoNumerico = "C2" },
                new ColumnaFinanciamiento { ProyectoId = proyectoId, Nombre = "Cobro neto $",   NombreInterno = "colCobro",     AnchoColumna = 105, Orden = 11, Alineacion = AlineacionColumna.Derecha,   Visible = true, FormatoNumerico = "C2" },
                new ColumnaFinanciamiento { ProyectoId = proyectoId, Nombre = "Flujo $",        NombreInterno = "colFlujoNeto", AnchoColumna = 95,  Orden = 12, Alineacion = AlineacionColumna.Derecha,   Visible = true, FormatoNumerico = "C2" },
                new ColumnaFinanciamiento { ProyectoId = proyectoId, Nombre = "Saldo acum. $",  NombreInterno = "colSaldo",     AnchoColumna = 110, Orden = 13, Alineacion = AlineacionColumna.Derecha,   Visible = true, FormatoNumerico = "C2" },
                new ColumnaFinanciamiento { ProyectoId = proyectoId, Nombre = "Tasa período",   NombreInterno = "colTasa",      AnchoColumna = 85,  Orden = 14, Alineacion = AlineacionColumna.Centro,    Visible = true },
                new ColumnaFinanciamiento { ProyectoId = proyectoId, Nombre = "Interés $",      NombreInterno = "colInteres",   AnchoColumna = 95,  Orden = 15, Alineacion = AlineacionColumna.Derecha,   Visible = true, FormatoNumerico = "C2" },
            };

            context.ColumnasFinanciamiento.AddRange(cols);
            context.SaveChanges();
        }

        public static List<ColumnaFinanciamiento> ObtenerColumnas(SOPROContext context, int proyectoId)
        {
            CrearColumnasPredeterminadas(context, proyectoId);
            return context.ColumnasFinanciamiento
                .Where(c => c.ProyectoId == proyectoId)
                .OrderBy(c => c.Orden)
                .ToList();
        }
    }
}
