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
                Apply(dto, material, isNew: true);
                await repository.AddAsync(material);
            }
            else
            {
                Apply(dto, material!, isNew: false);
                material!.FechaModificacion = DateTime.Now;
                await repository.UpdateAsync(material);
            }

            await repository.SaveChangesAsync();

            var recalculated = !isNew && RecalculationCoordinatorService.RecalculateAfterMaterialUpdate(context, material!.Id, material.ProyectoId);

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
                Apply(dto, manoDeObra, isNew: true);
                await repository.AddAsync(manoDeObra);
            }
            else
            {
                Apply(dto, manoDeObra!, isNew: false);
                manoDeObra!.FechaModificacion = DateTime.Now;
                await repository.UpdateAsync(manoDeObra);
            }

            await repository.SaveChangesAsync();

            var recalculated = !isNew && RecalculationCoordinatorService.RecalculateAfterManoDeObraUpdate(context, manoDeObra!.Id, manoDeObra.ProyectoId);

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
                Apply(dto, maquinaria, isNew: true);
                await repository.AddAsync(maquinaria);
            }
            else
            {
                Apply(dto, maquinaria!, isNew: false);
                maquinaria!.FechaModificacion = DateTime.Now;
                await repository.UpdateAsync(maquinaria);
            }

            await repository.SaveChangesAsync();

            var recalculated = !isNew && RecalculationCoordinatorService.RecalculateAfterMaquinariaUpdate(context, maquinaria!.Id, maquinaria.ProyectoId);

            return new CatalogSaveResult
            {
                EntityId = maquinaria!.Id,
                IsNew = isNew,
                TriggeredRecalculation = recalculated
            };
        }

        private static void Apply(MaterialEditDto dto, Material material, bool isNew)
        {
            material.Clave = dto.Clave.Trim().ToUpperInvariant();
            material.Descripcion = dto.Descripcion.Trim();
            material.Unidad = dto.Unidad.Trim().ToLowerInvariant();
            material.PrecioUnitario = dto.PrecioUnitario;
            material.Notas = dto.Notas?.Trim() ?? string.Empty;
            if (isNew)
                material.Origen = dto.ProyectoId.HasValue || !dto.GuardarEnMaestro
                    ? OrigenInsumo.Proyecto
                    : OrigenInsumo.Maestro;
            ApplyOwnership(dto.ProyectoId, isNew, material.ProyectoId, "material", value => material.ProyectoId = value);
        }

        private static void Apply(ManoDeObraEditDto dto, ManoDeObra manoDeObra, bool isNew)
        {
            manoDeObra.Clave = dto.Clave.Trim().ToUpperInvariant();
            manoDeObra.Descripcion = dto.Descripcion.Trim();
            manoDeObra.Unidad = dto.Unidad.Trim().ToLowerInvariant();
            manoDeObra.SalarioBase = dto.SalarioBase;
            manoDeObra.FactorSalarioReal = dto.FactorSalarioReal;
            manoDeObra.SalarioReal = dto.SalarioReal;
            manoDeObra.Notas = dto.Notas?.Trim() ?? string.Empty;
            if (isNew)
                manoDeObra.Origen = dto.ProyectoId.HasValue || !dto.GuardarEnMaestro
                    ? OrigenInsumo.Proyecto
                    : OrigenInsumo.Maestro;
            ApplyOwnership(dto.ProyectoId, isNew, manoDeObra.ProyectoId, "mano de obra", value => manoDeObra.ProyectoId = value);
        }

        private static void Apply(MaquinariaEditDto dto, Maquinaria maquinaria, bool isNew)
        {
            maquinaria.Clave = dto.Clave.Trim().ToUpperInvariant();
            maquinaria.Descripcion = dto.Descripcion.Trim();
            maquinaria.PotenciaNominal = dto.PotenciaNominal;
            maquinaria.TipoCombustible = dto.TipoCombustible;
            maquinaria.CostoHorario = dto.CostoHorario;
            maquinaria.EsCostoCalculado = dto.EsCostoCalculado;
            maquinaria.Notas = dto.Notas?.Trim() ?? string.Empty;
            if (isNew)
                maquinaria.Origen = dto.ProyectoId.HasValue || !dto.GuardarEnMaestro
                    ? OrigenInsumo.Proyecto
                    : OrigenInsumo.Maestro;
            ApplyOwnership(dto.ProyectoId, isNew, maquinaria.ProyectoId, "maquinaria", value => maquinaria.ProyectoId = value);
        }

        /// <summary>
        /// La propiedad de un insumo la define <c>ProyectoId</c>, no el flag legacy
        /// <c>GuardarEnMaestro</c>: una fila de proyecto conserva su proyecto y una
        /// fila global legada (ProyectoId null) permanece intacta. Solo una fila
        /// nueva adopta el proyecto del DTO; nunca se crea una fila global desde una
        /// ruta de guardado de proyecto. El <c>Origen</c> de una fila existente se
        /// conserva (no se migra en silencio por el flag).
        /// </summary>
        private static void ApplyOwnership(int? proyectoIdDto, bool isNew, int? proyectoIdActual, string entidad, Action<int?> asignar)
        {
            if (isNew)
            {
                asignar(proyectoIdDto);
                return;
            }

            if (proyectoIdDto.HasValue && proyectoIdDto != proyectoIdActual)
            {
                throw new InvalidOperationException(
                    $"El {entidad} pertenece a otro proyecto y no puede modificarse desde este contexto.");
            }
        }
    }
}
