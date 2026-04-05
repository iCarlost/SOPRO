using Microsoft.EntityFrameworkCore;
using SOPRO.Application.Models.Catalogs;
using SOPRO.Core.Entities;
using SOPRO.Data.Context;

namespace SOPRO.Application.Services
{
    public sealed class CatalogLoadService
    {
        private static bool EsImportado(string? notas)
            => !string.IsNullOrWhiteSpace(ImportOriginStampService.ExtractProjectName(notas));

        public async Task<List<Material>> LoadMaterialesAsync(SOPROContext context, CatalogFilterInput filter)
        {
            IQueryable<Material> query = context.Materiales;

            if (filter.ProyectoId.HasValue)
                query = query.Where(m => m.ProyectoId == filter.ProyectoId.Value);

            if (!string.IsNullOrWhiteSpace(filter.SearchText))
            {
                var term = filter.SearchText.Trim().ToLower();
                query = query.Where(m => m.Clave.ToLower().Contains(term) || m.Descripcion.ToLower().Contains(term));
            }

            var list = await query.OrderBy(m => m.Clave).ToListAsync();
            if (filter.SoloProyecto)
                list = list.Where(m => !EsImportado(m.Notas)).ToList();
            else if (filter.SoloMaestros)
                list = list.Where(m => EsImportado(m.Notas)).ToList();

            return list;
        }

        public async Task<List<ManoDeObra>> LoadManoDeObraAsync(SOPROContext context, CatalogFilterInput filter)
        {
            IQueryable<ManoDeObra> query = context.ManoDeObra;

            if (filter.ProyectoId.HasValue)
                query = query.Where(m => m.ProyectoId == filter.ProyectoId.Value);

            if (!string.IsNullOrWhiteSpace(filter.SearchText))
            {
                var term = filter.SearchText.Trim().ToLower();
                query = query.Where(m => m.Clave.ToLower().Contains(term) || m.Descripcion.ToLower().Contains(term));
            }

            var list = await query.OrderBy(m => m.Clave).ToListAsync();
            if (filter.SoloProyecto)
                list = list.Where(m => !EsImportado(m.Notas)).ToList();
            else if (filter.SoloMaestros)
                list = list.Where(m => EsImportado(m.Notas)).ToList();

            return list;
        }

        public async Task<List<Maquinaria>> LoadMaquinariaAsync(SOPROContext context, CatalogFilterInput filter)
        {
            IQueryable<Maquinaria> query = context.Maquinaria;

            if (filter.ProyectoId.HasValue)
                query = query.Where(m => m.ProyectoId == filter.ProyectoId.Value);

            if (!string.IsNullOrWhiteSpace(filter.SearchText))
            {
                var term = filter.SearchText.Trim().ToLower();
                query = query.Where(m => m.Clave.ToLower().Contains(term) || m.Descripcion.ToLower().Contains(term));
            }

            var list = await query.OrderBy(m => m.Clave).ToListAsync();
            if (filter.SoloProyecto)
                list = list.Where(m => !EsImportado(m.Notas)).ToList();
            else if (filter.SoloMaestros)
                list = list.Where(m => EsImportado(m.Notas)).ToList();

            return list;
        }
    }
}
