using System.Linq;
using Microsoft.EntityFrameworkCore;
using SOPRO.Core.Entities;
using SOPRO.Data.Context;

namespace SOPRO.WinForms.Helpers
{
    /// <summary>
    /// Refresco seguro del catálogo de matrices después de una escritura externa
    /// (por ejemplo, la eliminación en lote de insumos, que confirma por un contexto
    /// nuevo). <see cref="MatrixCatalogLoadService"/> consulta con seguimiento: si
    /// el contexto compartido ya rastrea una matriz, la consulta devuelve la MISMA
    /// instancia con los valores del identity map y el precio recalculado no se ve.
    ///
    /// Con el tracker limpio se descartan las matrices y componentes rastreados para
    /// que la siguiente consulta materialice valores frescos de la base. Si hay
    /// trabajo pendiente no se toca nada: el llamador debe abstenerse de recargar
    /// para no perder ediciones del usuario.
    /// </summary>
    internal static class MatrixCatalogRefreshHelper
    {
        public static bool DescartarRastreados(SOPROContext context)
        {
            if (context.ChangeTracker.HasChanges()) return false;

            foreach (var entry in context.ChangeTracker.Entries<ComponenteMatriz>().ToList())
                entry.State = EntityState.Detached;

            foreach (var entry in context.ChangeTracker.Entries<Matriz>().ToList())
                entry.State = EntityState.Detached;

            return true;
        }
    }
}
