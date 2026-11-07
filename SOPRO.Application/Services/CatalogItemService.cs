using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
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
            await using IDbContextTransaction? transaction = context.Database.CurrentTransaction == null
                ? await context.Database.BeginTransactionAsync()
                : null;
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

            var recalculated = false;
            if (!isNew)
            {
                recalculated = RecalculationCoordinatorService.RefreshLinkedMachineryAfterMaterialUpdate(context, material!.Id, material.ProyectoId);
                recalculated |= RecalculationCoordinatorService.RecalculateAfterMaterialUpdate(context, material.Id, material.ProyectoId);
            }
            await context.SaveChangesAsync();
            if (transaction != null) await transaction.CommitAsync();

            return new CatalogSaveResult
            {
                EntityId = material!.Id,
                IsNew = isNew,
                TriggeredRecalculation = recalculated
            };
        }

        public static async Task<CatalogSaveResult> SaveManoDeObraAsync(SOPROContext context, ManoDeObraEditDto dto, ManoDeObra? manoDeObra = null)
        {
            await using IDbContextTransaction? transaction = context.Database.CurrentTransaction == null
                ? await context.Database.BeginTransactionAsync()
                : null;
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

            var recalculated = false;
            if (!isNew)
            {
                recalculated = RecalculationCoordinatorService.RefreshLinkedMachineryAfterManoDeObraUpdate(context, manoDeObra!.Id, manoDeObra.ProyectoId);
                recalculated |= RecalculationCoordinatorService.RecalculateAfterManoDeObraUpdate(context, manoDeObra.Id, manoDeObra.ProyectoId);
            }
            await context.SaveChangesAsync();
            if (transaction != null) await transaction.CommitAsync();

            return new CatalogSaveResult
            {
                EntityId = manoDeObra!.Id,
                IsNew = isNew,
                TriggeredRecalculation = recalculated
            };
        }

        public static async Task<CatalogSaveResult> SaveMaquinariaAsync(SOPROContext context, MaquinariaEditDto dto, Maquinaria? maquinaria = null)
        {
            if (!dto.EsCostoCalculado && (dto.MaterialCombustibleId.HasValue || dto.MaterialAceiteId.HasValue || dto.ManoDeObraOperadorId.HasValue))
                throw new InvalidOperationException("Elimine los vínculos de combustible, aceite y operador antes de guardar maquinaria con costo manual.");
            await using IDbContextTransaction? transaction = context.Database.CurrentTransaction == null
                ? await context.Database.BeginTransactionAsync()
                : null;
            var repository = new Repository<Maquinaria>(context);
            var isNew = maquinaria == null;
            if (!isNew && dto.ProyectoId.HasValue && dto.ProyectoId != maquinaria!.ProyectoId)
                throw new InvalidOperationException("La maquinaria pertenece a otro proyecto y no puede modificarse desde este contexto.");
            int? ownerProjectId = isNew ? dto.ProyectoId : maquinaria!.ProyectoId;
            ValidateLinkOwnership(context, dto.MaterialCombustibleId, ownerProjectId, "combustible");
            ValidateLinkOwnership(context, dto.MaterialAceiteId, ownerProjectId, "aceite");
            ValidateLinkOwnership(context, dto.ManoDeObraOperadorId, ownerProjectId, "mano de obra");

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

            ApplyLinksAndCalculate(context, maquinaria!, dto);
            await repository.SaveChangesAsync();
            PricePropagationService.PropagarMaquinariaSinGuardar(context, maquinaria!.Id, maquinaria.ProyectoId);
            await context.SaveChangesAsync();
            if (transaction != null) await transaction.CommitAsync();
            bool recalculated = !isNew;

            return new CatalogSaveResult
            {
                EntityId = maquinaria!.Id,
                IsNew = isNew,
                TriggeredRecalculation = recalculated
            };
        }

        private static void ValidateLinkOwnership(SOPROContext context, int? id, int? projectId, string role)
        {
            if (!id.HasValue) return;
            bool valid = context.Materiales.Any(m => m.Id == id && m.ProyectoId == projectId);
            if (role == "mano de obra") valid = context.ManoDeObra.Any(m => m.Id == id && m.ProyectoId == projectId);
            if (!valid)
                throw new InvalidOperationException($"El insumo vinculado de {role} debe pertenecer exactamente al mismo proyecto (o ser maestro cuando la maquinaria es maestra).");
        }

        private static void ApplyLinksAndCalculate(SOPROContext context, Maquinaria machine, MaquinariaEditDto dto)
        {
            machine.MaterialCombustibleId = dto.MaterialCombustibleId;
            machine.MaterialAceiteId = dto.MaterialAceiteId;
            machine.ManoDeObraOperadorId = dto.ManoDeObraOperadorId;
            if (!dto.EsCostoCalculado) return;
            if (dto.MaterialCombustibleId.HasValue)
                machine.PrecioCombustible = context.Materiales.Single(m => m.Id == dto.MaterialCombustibleId).PrecioUnitario;
            if (dto.MaterialAceiteId.HasValue)
                machine.PrecioAceite = context.Materiales.Single(m => m.Id == dto.MaterialAceiteId).PrecioUnitario;
            if (dto.ManoDeObraOperadorId.HasValue)
            {
                ManoDeObra operatorSource = context.ManoDeObra.Single(m => m.Id == dto.ManoDeObraOperadorId);
                machine.SalarioOperador = operatorSource.SalarioBase;
                machine.FactorSalarioReal = operatorSource.FactorSalarioReal;
            }
            machine.CalcularCostoHorario();
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
