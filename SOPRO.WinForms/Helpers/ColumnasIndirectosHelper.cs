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

        /// <summary>
        /// Carga las columnas de indirectos del proyecto creando los defaults
        /// persistidos si aún no existen. Uso legítimo de UI (configuración/formato):
        /// NO debe invocarse desde las rutas de exportación (ver
        /// <see cref="ObtenerColumnasSoloLectura"/>).
        /// </summary>
        public static List<ColumnaIndirectos> ObtenerColumnas(SOPROContext context, int proyectoId)
        {
            CrearColumnasPredeterminadas(context, proyectoId);
            return context.ColumnasIndirectos
                .Where(c => c.ProyectoId == proyectoId)
                .OrderBy(c => c.Orden)
                .ToList();
        }

        /// <summary>
        /// Consulta de SOLO LECTURA de las columnas de indirectos del proyecto.
        /// NO crea valores por defecto ni ejecuta <c>SaveChanges</c>: exportar un
        /// reporte no debe modificar la configuración persistida (mismo criterio que
        /// <c>FormPresupuesto.Reportes.ObtenerConfiguracionColumnasSoloLectura</c>).
        /// Si no hay configuración devuelve una lista vacía y el builder del snapshot
        /// aplica sus defaults en memoria.
        /// </summary>
        public static List<ColumnaIndirectos> ObtenerColumnasSoloLectura(SOPROContext context, int proyectoId)
            => context.ColumnasIndirectos
                .Where(c => c.ProyectoId == proyectoId)
                .OrderBy(c => c.Orden)
                .ToList();
    }
}
