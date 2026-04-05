using Microsoft.EntityFrameworkCore;
using SOPRO.Application.DTOs.Matrices;
using SOPRO.Application.Models;
using SOPRO.Core.Entities;
using SOPRO.Data.Context;

namespace SOPRO.Application.Services
{
    public static class MatrixApplicationService
    {
        public static async Task<bool> ExistsByKeyAsync(SOPROContext context, int proyectoId, string clave, int? excludeMatrixId = null)
        {
            if (string.IsNullOrWhiteSpace(clave))
            {
                return false;
            }

            var normalizedKey = clave.Trim().ToUpperInvariant();

            return await context.Matrices
                .AnyAsync(m => m.ProyectoId == proyectoId
                    && m.Clave == normalizedKey
                    && (!excludeMatrixId.HasValue || m.Id != excludeMatrixId.Value));
        }

        public static async Task<MatrixSaveResult> SaveAsync(SOPROContext context, MatrixEditDto dto, Matriz? existingMatrix = null)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            if (dto == null) throw new ArgumentNullException(nameof(dto));

            var isNew = existingMatrix == null;
            Matriz matrix;

            if (isNew)
            {
                matrix = new Matriz();
                Apply(dto, matrix);
                context.Matrices.Add(matrix);
                await context.SaveChangesAsync();
            }
            else
            {
                matrix = existingMatrix!;
                Apply(dto, matrix);

                var oldComponents = context.ComponentesMatriz.Where(c => c.MatrizId == matrix.Id);
                context.ComponentesMatriz.RemoveRange(oldComponents);
                await context.SaveChangesAsync();
            }

            var mappedComponents = dto.Componentes
                .Select((component, index) => MapComponent(matrix.Id, component, index))
                .ToList();

            if (mappedComponents.Count > 0)
            {
                context.ComponentesMatriz.AddRange(mappedComponents);
            }

            await context.SaveChangesAsync();

            var cascaded = isNew
                ? 0
                : await PropagateCascadeAsync(context, matrix.Id);

            return new MatrixSaveResult
            {
                MatrixId = matrix.Id,
                IsNew = isNew,
                CascadedMatricesUpdated = cascaded
            };
        }

        public static async Task<int> ImportMatricesFromMasterAsync(
            SOPROContext projectContext,
            SOPROContext masterContext,
            int proyectoId,
            IReadOnlyCollection<int> selectedIds)
        {
            if (selectedIds == null || selectedIds.Count == 0)
            {
                return 0;
            }

            var matrices = await masterContext.Matrices
                .Include(m => m.Componentes)
                .Where(m => selectedIds.Contains(m.Id))
                .ToListAsync();

            var imported = 0;

            foreach (var matriz in matrices)
            {
                var exists = await projectContext.Matrices
                    .AnyAsync(m => m.Clave == matriz.Clave && m.ProyectoId == proyectoId);

                if (exists)
                {
                    continue;
                }

                var newMatrix = new Matriz
                {
                    Clave = matriz.Clave,
                    Descripcion = matriz.Descripcion,
                    Unidad = matriz.Unidad,
                    Tipo = matriz.Tipo,
                    CostoDirecto = matriz.CostoDirecto,
                    ProyectoId = proyectoId,
                    Origen = OrigenInsumo.Maestro,
                    MatrizMaestraId = matriz.Id,
                    Notas = matriz.Notas ?? string.Empty
                };

                foreach (var component in matriz.Componentes.OrderBy(c => c.Orden).ThenBy(c => c.Id))
                {
                    newMatrix.Componentes.Add(new ComponenteMatriz
                    {
                        TipoComponente = component.TipoComponente,
                        MaterialId = component.MaterialId,
                        ManoDeObraId = component.ManoDeObraId,
                        MaquinariaId = component.MaquinariaId,
                        AuxiliarId = component.AuxiliarId,
                        HerramientaId = component.HerramientaId,
                        Cantidad = component.Cantidad,
                        Rendimiento = component.Rendimiento,
                        Importe = component.Importe,
                        Orden = component.Orden,
                        Notas = component.Notas ?? string.Empty
                    });
                }

                projectContext.Matrices.Add(newMatrix);
                imported++;
            }

            await projectContext.SaveChangesAsync();
            return imported;
        }

        public static List<MatrixListItemDto> GetProjectApus(SOPROContext context, int proyectoId)
        {
            return context.Matrices
                .Where(m => m.ProyectoId == proyectoId && m.Tipo == TipoMatriz.APU)
                .OrderBy(m => m.Clave)
                .Select(m => new MatrixListItemDto
                {
                    Id = m.Id,
                    Clave = m.Clave,
                    Descripcion = m.Descripcion,
                    Unidad = m.Unidad,
                    CostoDirecto = m.CostoDirecto,
                    Tipo = m.Tipo
                })
                .ToList();
        }



        public static List<MatrixListItemDto> GetProjectMatrices(SOPROContext context, int proyectoId, TipoMatriz? tipo = null)
        {
            var query = context.Matrices.Where(m => m.ProyectoId == proyectoId);
            if (tipo.HasValue)
                query = query.Where(m => m.Tipo == tipo.Value);

            return query
                .OrderBy(m => m.Tipo)
                .ThenBy(m => m.Clave)
                .Select(m => new MatrixListItemDto
                {
                    Id = m.Id,
                    Clave = m.Clave,
                    Descripcion = m.Descripcion,
                    Unidad = m.Unidad,
                    CostoDirecto = m.CostoDirecto,
                    Tipo = m.Tipo
                })
                .ToList();
        }

        private static void Apply(MatrixEditDto dto, Matriz matrix)
        {
            matrix.Clave = dto.Clave.Trim().ToUpperInvariant();
            matrix.Descripcion = dto.Descripcion.Trim();
            matrix.Unidad = dto.Unidad.Trim();
            matrix.Tipo = dto.Tipo;
            matrix.CostoDirecto = dto.CostoDirecto;
            matrix.ProyectoId = dto.ProyectoId;
            matrix.Notas ??= string.Empty;
            matrix.FechaModificacion = DateTime.Now;
        }

        private static ComponenteMatriz MapComponent(int matrixId, MatrixComponentEditDto component, int index)
        {
            return new ComponenteMatriz
            {
                MatrizId = matrixId,
                TipoComponente = component.TipoComponente,
                MaterialId = component.MaterialId,
                ManoDeObraId = component.ManoDeObraId,
                MaquinariaId = component.MaquinariaId,
                AuxiliarId = component.AuxiliarId,
                HerramientaId = component.HerramientaId,
                Cantidad = component.Cantidad,
                Rendimiento = component.TipoComponente == TipoComponenteMatriz.Maquinaria && component.Cantidad > 0
                    ? Math.Round(1m / component.Cantidad, 5, MidpointRounding.AwayFromZero) : 0m,
                Importe = component.Importe,
                Orden = component.Orden > 0 ? component.Orden : index + 1,
                Notas = component.Notas ?? string.Empty
            };
        }

        private static async Task<int> PropagateCascadeAsync(SOPROContext context, int matrixId)
        {
            try
            {
                var affectedMatrixIds = await context.ComponentesMatriz
                    .Where(c => c.AuxiliarId == matrixId)
                    .Select(c => c.MatrizId)
                    .Distinct()
                    .ToListAsync();

                if (affectedMatrixIds.Count == 0)
                {
                    return 0;
                }

                foreach (var affectedMatrixId in affectedMatrixIds)
                {
                    var affectedMatrix = await context.Matrices
                        .Include(m => m.Componentes).ThenInclude(c => c.Material)
                        .Include(m => m.Componentes).ThenInclude(c => c.ManoDeObra)
                        .Include(m => m.Componentes).ThenInclude(c => c.Maquinaria)
                        .Include(m => m.Componentes).ThenInclude(c => c.Auxiliar)
                        .Include(m => m.Componentes).ThenInclude(c => c.Herramienta)
                        .FirstOrDefaultAsync(m => m.Id == affectedMatrixId);

                    if (affectedMatrix == null)
                    {
                        continue;
                    }

                    // [FIX] Usar motor con decimales del proyecto en lugar de CalcularCostoDirecto()
                    var proyecto = await context.Proyectos.FindAsync(affectedMatrix.ProyectoId);
                    if (proyecto != null)
                    {
                        var totals = MatrixComponentCalculationService.Recalculate(
                            affectedMatrix.Componentes.ToList(), proyecto.DecimalesImporte);
                        affectedMatrix.CostoDirecto = new MotorCalculoSopro(proyecto)
                            .RedondearImporte(totals.CostoDirectoTotal);
                    }
                    else
                    {
                        // Sin proyecto no se puede calcular con precisión correcta — omitir esta matriz
                        System.Diagnostics.Debug.WriteLine(
                            $"MatrixApplicationService: ProyectoId={affectedMatrix.ProyectoId} no encontrado, " +
                            $"matriz {affectedMatrix.Id} omitida del recálculo.");
                        continue;
                    }
                }

                await context.SaveChangesAsync();
                return affectedMatrixIds.Count;
            }
            catch
            {
                return 0;
            }
        }
    }
}
