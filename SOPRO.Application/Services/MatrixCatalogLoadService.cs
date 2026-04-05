using Microsoft.EntityFrameworkCore;
using SOPRO.Application.Models.Catalogs;
using SOPRO.Core.Entities;
using SOPRO.Data.Context;

namespace SOPRO.Application.Services
{
    public sealed class MatrixCatalogLoadService
    {
        public async Task<List<Matriz>> LoadMatricesAsync(SOPROContext context, MatrixCatalogFilterInput filter)
        {
            IQueryable<Matriz> query = context.Matrices
                .Include(m => m.Componentes)
                .Where(m => m.ProyectoId == filter.ProyectoId);

            if (filter.Tipo.HasValue)
                query = query.Where(m => m.Tipo == filter.Tipo.Value);

            if (!string.IsNullOrWhiteSpace(filter.SearchText))
            {
                var term = filter.SearchText.Trim().ToLower();
                query = query.Where(m =>
                    m.Clave.ToLower().Contains(term) ||
                    m.Descripcion.ToLower().Contains(term));
            }

            return await query.OrderBy(m => m.Clave).ToListAsync();
        }
    }
}
