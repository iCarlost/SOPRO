using System;
using System.Threading.Tasks;
using SOPRO.Application.DTOs.Catalog;
using SOPRO.Application.Models;
using SOPRO.Core.Entities;
using SOPRO.Data.Context;
using SOPRO.Data.Repositories;

namespace SOPRO.Application.Services
{
    public static class CatalogItemService
    {
        public static async Task<CatalogSaveResult> SaveMaterialAsync(SOPROContext context, MaterialEditDto dto, Material? material = null)
        {
            var repository = new Repository<Material>(context);
            var isNew = material == null;

            if (isNew)
            {
                material = new Material();
                Apply(dto, material);
                await repository.AddAsync(material);
            }
            else
            {
                Apply(dto, material!);
                material!.FechaModificacion = DateTime.Now;
                await repository.UpdateAsync(material);
            }

            await repository.SaveChangesAsync();

            var recalculated = !isNew && RecalculationCoordinatorService.RecalculateAfterMaterialUpdate(context, material!.Id);

            return new CatalogSaveResult
            {
                EntityId = material!.Id,
                IsNew = isNew,
                TriggeredRecalculation = recalculated
            };
        }

        public static async Task<CatalogSaveResult> SaveManoDeObraAsync(SOPROContext context, ManoDeObraEditDto dto, ManoDeObra? manoDeObra = null)
        {
            var repository = new Repository<ManoDeObra>(context);
            var isNew = manoDeObra == null;

            if (isNew)
            {
                manoDeObra = new ManoDeObra();
                Apply(dto, manoDeObra);
                await repository.AddAsync(manoDeObra);
            }
            else
            {
                Apply(dto, manoDeObra!);
                manoDeObra!.FechaModificacion = DateTime.Now;
                await repository.UpdateAsync(manoDeObra);
            }

            await repository.SaveChangesAsync();

            var recalculated = !isNew && RecalculationCoordinatorService.RecalculateAfterManoDeObraUpdate(context, manoDeObra!.Id);

            return new CatalogSaveResult
            {
                EntityId = manoDeObra!.Id,
                IsNew = isNew,
                TriggeredRecalculation = recalculated
            };
        }

        public static async Task<CatalogSaveResult> SaveMaquinariaAsync(SOPROContext context, MaquinariaEditDto dto, Maquinaria? maquinaria = null)
        {
            var repository = new Repository<Maquinaria>(context);
            var isNew = maquinaria == null;

            if (isNew)
            {
                maquinaria = new Maquinaria();
                Apply(dto, maquinaria);
                await repository.AddAsync(maquinaria);
            }
            else
            {
                Apply(dto, maquinaria!);
                maquinaria!.FechaModificacion = DateTime.Now;
                await repository.UpdateAsync(maquinaria);
            }

            await repository.SaveChangesAsync();

            var recalculated = !isNew && RecalculationCoordinatorService.RecalculateAfterMaquinariaUpdate(context, maquinaria!.Id);

            return new CatalogSaveResult
            {
                EntityId = maquinaria!.Id,
                IsNew = isNew,
                TriggeredRecalculation = recalculated
            };
        }

        private static void Apply(MaterialEditDto dto, Material material)
        {
            material.Clave = dto.Clave.Trim().ToUpperInvariant();
            material.Descripcion = dto.Descripcion.Trim();
            material.Unidad = dto.Unidad.Trim().ToLowerInvariant();
            material.PrecioUnitario = dto.PrecioUnitario;
            material.Notas = dto.Notas?.Trim() ?? string.Empty;
            material.Origen = dto.GuardarEnMaestro ? OrigenInsumo.Maestro : OrigenInsumo.Proyecto;
            material.ProyectoId = dto.GuardarEnMaestro ? null : dto.ProyectoId;
        }

        private static void Apply(ManoDeObraEditDto dto, ManoDeObra manoDeObra)
        {
            manoDeObra.Clave = dto.Clave.Trim().ToUpperInvariant();
            manoDeObra.Descripcion = dto.Descripcion.Trim();
            manoDeObra.Unidad = dto.Unidad.Trim().ToLowerInvariant();
            manoDeObra.SalarioBase = dto.SalarioBase;
            manoDeObra.FactorSalarioReal = dto.FactorSalarioReal;
            manoDeObra.SalarioReal = dto.SalarioReal;
            manoDeObra.Notas = dto.Notas?.Trim() ?? string.Empty;
            manoDeObra.Origen = dto.GuardarEnMaestro ? OrigenInsumo.Maestro : OrigenInsumo.Proyecto;
            manoDeObra.ProyectoId = dto.GuardarEnMaestro ? null : dto.ProyectoId;
        }

        private static void Apply(MaquinariaEditDto dto, Maquinaria maquinaria)
        {
            maquinaria.Clave = dto.Clave.Trim().ToUpperInvariant();
            maquinaria.Descripcion = dto.Descripcion.Trim();
            maquinaria.PotenciaNominal = dto.PotenciaNominal;
            maquinaria.TipoCombustible = dto.TipoCombustible;
            maquinaria.CostoHorario = dto.CostoHorario;
            maquinaria.EsCostoCalculado = dto.EsCostoCalculado;
            maquinaria.Notas = dto.Notas?.Trim() ?? string.Empty;
            maquinaria.Origen = dto.GuardarEnMaestro ? OrigenInsumo.Maestro : OrigenInsumo.Proyecto;
            maquinaria.ProyectoId = dto.GuardarEnMaestro ? null : dto.ProyectoId;
        }
    }
}
