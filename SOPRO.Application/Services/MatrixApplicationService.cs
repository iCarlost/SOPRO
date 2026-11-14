using Microsoft.EntityFrameworkCore;
using Sopro.Calculation;
using SOPRO.Application.DTOs.Matrices;
using SOPRO.Application.Models;
using SOPRO.Application.Models.Matrices;
using SOPRO.Core.Entities;
using SOPRO.Data.Context;
using SOPRO.Data.Factories;

namespace SOPRO.Application.Services
{
    // ╔══════════════════════════════════════════════════════════════════════════╗
    // ║  MatrixApplicationService — [N5-10]                                    ║
    // ║  Única operación del motor: RedondearImporte del CostoDirectoTotal      ║
    // ║  recalculado en la cascada (PropagateCascadeAsync). Migrada de           ║
    // ║  MotorCalculoSopro a SoproCalculationEngine.RoundAmount                  ║
    // ║  (SOPRO.Calculation). MapComponent conserva Math.Round(1/cantidad, 5):   ║
    // ║  precisión fija por contrato de captura, no es operación del motor.      ║
    // ║  La sobrecarga heredada conserva su firma; la numeración de la cascada   ║
    // ║  y los importes observables no cambian.                                  ║
    // ║                                                                          ║
    // ║  #121: la escritura NUNCA usa el contexto de la sesión. SaveAsync crea   ║
    // ║  un contexto por operación con IProjectDbContextFactory sobre la ruta    ║
    // ║  del llamador y envuelve matriz, componentes, cascada y commit en una    ║
    // ║  sola transacción. El tracker del llamador queda intacto (stale): no se  ║
    // ║  guarda ni se limpia desde aquí.                                         ║
    // ╚══════════════════════════════════════════════════════════════════════════╝
    public static class MatrixApplicationService
    {
        // Fábrica por defecto de la sobrecarga heredada, que aún recibe el
        // contexto de la sesión solo como fuente de la ruta de la base.
        private static readonly IProjectDbContextFactory DefaultDbContextFactory = new ProjectDbContextFactory();

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

        /// <summary>
        /// Sobrecarga de compatibilidad: usa <paramref name="context"/> únicamente
        /// como fuente de la ruta de la base. La escritura corre en un contexto
        /// propio (una operación, un contexto) para no confirmar ni revertir
        /// ediciones ajenas rastreadas por el contexto de la sesión.
        /// <paramref name="existingMatrix"/> se usa solo como identidad; nunca se
        /// adjunta al contexto de la operación.
        /// </summary>
        public static Task<MatrixSaveResult> SaveAsync(SOPROContext context, MatrixEditDto dto, Matriz? existingMatrix = null)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            return SaveAsync(DefaultDbContextFactory, context.DatabasePath, dto, existingMatrix, context);
        }

        /// <summary>
        /// Guarda matriz (alta o edición), reemplaza sus componentes y propaga la
        /// cascada de auxiliares en un contexto por operación y una sola
        /// transacción. Cualquier fallo revierte todo y se propaga al llamador.
        /// <paramref name="sessionContext"/> es opcional: si se aporta, la guarda
        /// rechaza antes de escribir cuando el tracker de la sesión tiene ediciones
        /// pendientes sobre la matriz objetivo (o altas/bajas ajenas de sus
        /// componentes) o sobre las matrices/componentes que dependen de ella (la
        /// cascada las recalcularía y el guardado compartido posterior podría
        /// sobrescribirlas). El tracker no se guarda ni se limpia.
        /// </summary>
        public static async Task<MatrixSaveResult> SaveAsync(
            IProjectDbContextFactory dbContextFactory,
            string databasePath,
            MatrixEditDto dto,
            Matriz? existingMatrix = null,
            SOPROContext? sessionContext = null)
        {
            if (dbContextFactory == null) throw new ArgumentNullException(nameof(dbContextFactory));
            if (string.IsNullOrWhiteSpace(databasePath))
                throw new ArgumentException("La ruta de la base del proyecto es requerida.", nameof(databasePath));
            if (dto == null) throw new ArgumentNullException(nameof(dto));

            var isNew = existingMatrix == null;

            // La referencia de cada componente se valida ANTES de cualquier
            // escritura: el guardado de una matriz existente elimina sus componentes
            // anteriores, así que un rechazo posterior dejaría la matriz incompleta.
            // Una referencia ausente o de otro tipo produce una navegación nula al
            // recalcular; nunca debe persistirse.
            var mappedComponents = dto.Componentes
                .Select((component, index) => MapComponent(existingMatrix?.Id ?? 0, component, index))
                .ToList();

            var referenceValidation = MatrixComponentEditingService.ValidateReferences(mappedComponents);
            if (!referenceValidation.Success)
                throw new MatrixComponentReferenceException(referenceValidation.ErrorMessage);

            await using var operationContext = dbContextFactory.Create(databasePath);

            // Guarda previa a cualquier escritura: la cascada recalcula las matrices
            // que usan a esta como auxiliar. Si la sesión tiene ediciones pendientes
            // sobre ellas, un SaveChanges del contexto de la sesión podría
            // sobrescribir el recálculo o dejar costos inconsistentes.
            if (!isNew)
            {
                await GuardPendingSessionEditsAsync(operationContext, sessionContext, existingMatrix!.Id, dto);
            }

            await using var transaction = await operationContext.Database.BeginTransactionAsync();

            try
            {
                Matriz matrix;

                if (isNew)
                {
                    matrix = new Matriz();
                    Apply(dto, matrix);
                    operationContext.Matrices.Add(matrix);
                    await operationContext.SaveChangesAsync();

                    // Para una matriz nueva el Id solo existe después del SaveChanges:
                    // se remapea el lote ya validado con el Id definitivo.
                    mappedComponents = dto.Componentes
                        .Select((component, index) => MapComponent(matrix.Id, component, index))
                        .ToList();
                }
                else
                {
                    matrix = await operationContext.Matrices
                        .FirstOrDefaultAsync(m => m.Id == existingMatrix!.Id)
                        ?? throw new InvalidOperationException(
                            $"La matriz {existingMatrix!.Id} ya no existe en la base; recargue la vista.");

                    Apply(dto, matrix);

                    var oldComponents = operationContext.ComponentesMatriz.Where(c => c.MatrizId == matrix.Id);
                    operationContext.ComponentesMatriz.RemoveRange(oldComponents);
                    await operationContext.SaveChangesAsync();
                }

                if (mappedComponents.Count > 0)
                {
                    operationContext.ComponentesMatriz.AddRange(mappedComponents);
                }

                await operationContext.SaveChangesAsync();

                var cascaded = isNew
                    ? 0
                    : await PropagateCascadeAsync(operationContext, matrix.Id);

                await transaction.CommitAsync();

                return new MatrixSaveResult
                {
                    MatrixId = matrix.Id,
                    IsNew = isNew,
                    CascadedMatricesUpdated = cascaded
                };
            }
            catch
            {
                try
                {
                    await transaction.RollbackAsync();
                }
                catch (Exception rollbackFailure)
                {
                    // El fallo original es el que debe llegar al llamador; el de la
                    // reversión se deja como rastro diagnóstico.
                    System.Diagnostics.Debug.WriteLine(
                        $"MatrixApplicationService.SaveAsync rollback: {rollbackFailure}");
                }

                throw;
            }
        }

        /// <summary>
        /// Rechaza la guarda si el contexto de la sesión tiene ediciones pendientes
        /// que la cascada o el guardado compartido podrían sobrescribir. Debe
        /// ejecutarse antes de abrir la transacción y no escribe nada.
        /// </summary>
        private static async Task GuardPendingSessionEditsAsync(
            SOPROContext operationContext,
            SOPROContext? sessionContext,
            int targetMatrixId,
            MatrixEditDto dto)
        {
            if (sessionContext == null || targetMatrixId <= 0)
            {
                return;
            }

            var dependentMatrixIds = await operationContext.ComponentesMatriz
                .Where(c => c.AuxiliarId == targetMatrixId)
                .Select(c => c.MatrizId)
                .Distinct()
                .ToListAsync();

            // DetectChanges descubre ediciones hechas sobre entidades ya rastreadas.
            // No guarda ni limpia el tracker: solo materializa el estado pendiente.
            sessionContext.ChangeTracker.DetectChanges();

            // La matriz objetivo puede estar Modified como copia de trabajo del
            // editor: esa edición no es un conflicto si coincide con lo que esta
            // misma guarda escribirá desde el DTO. Cualquier otro pendiente sobre
            // ella sí lo es, igual que cualquier pendiente sobre una dependiente.
            var expectedTarget = new Matriz();
            Apply(dto, expectedTarget);
            var targetEntries = sessionContext.ChangeTracker.Entries<Matriz>()
                .Where(entry => entry.Entity.Id == targetMatrixId);

            var pendingMatrices = targetEntries
                .Where(entry => entry.State == EntityState.Deleted
                    || (entry.State == EntityState.Modified && !CoincideConDto(entry.Entity, expectedTarget)))
                .Select(entry => entry.Entity.Id)
                .ToList();

            pendingMatrices.AddRange(sessionContext.ChangeTracker.Entries<Matriz>()
                .Where(entry => dependentMatrixIds.Contains(entry.Entity.Id)
                    && (entry.State == EntityState.Modified || entry.State == EntityState.Deleted))
                .Select(entry => entry.Entity.Id));

            pendingMatrices = pendingMatrices
                .Distinct()
                .OrderBy(id => id)
                .ToList();

            if (pendingMatrices.Count > 0)
            {
                throw new InvalidOperationException(
                    $"Hay cambios sin guardar en la matriz {targetMatrixId} o en matrices que dependen de ella " +
                    $"({string.Join(", ", pendingMatrices)}). La cascada recalcularía sus costos; " +
                    "guarde o descarte esos cambios antes de editar esta matriz.");
            }

            // Los componentes Modified de la matriz objetivo son las copias de
            // trabajo del editor y el DTO ya captura altas/bajas. En cambio, un
            // Added/Deleted ajeno (otro flujo de la sesión) sería descartado por la
            // reconciliación posterior: se rechaza antes de escribir.
            var ambiguousTargetComponents = sessionContext.ChangeTracker.Entries<ComponenteMatriz>()
                .Where(entry => entry.Entity.MatrizId == targetMatrixId
                    && (entry.State == EntityState.Added || entry.State == EntityState.Deleted))
                .Select(entry => entry.State)
                .ToList();

            if (ambiguousTargetComponents.Count > 0)
            {
                throw new InvalidOperationException(
                    $"Hay componentes agregados o eliminados sin guardar en la matriz {targetMatrixId}; " +
                    "la reconciliación tras guardar los descartaría. " +
                    "Guarde o descarte esos cambios antes de editar esta matriz.");
            }

            if (dependentMatrixIds.Count == 0)
            {
                return;
            }

            var pendingDependentComponentMatrices = sessionContext.ChangeTracker.Entries<ComponenteMatriz>()
                .Where(entry => dependentMatrixIds.Contains(entry.Entity.MatrizId)
                    && entry.State != EntityState.Unchanged)
                .Select(entry => entry.Entity.MatrizId)
                .Distinct()
                .OrderBy(id => id)
                .ToList();

            if (pendingDependentComponentMatrices.Count > 0)
            {
                throw new InvalidOperationException(
                    $"Hay cambios sin guardar en componentes de matrices que dependen de la matriz {targetMatrixId} " +
                    $"({string.Join(", ", pendingDependentComponentMatrices)}). La cascada recalcularía sus costos; " +
                    "guarde o descarte esos cambios antes de editar esta matriz.");
            }
        }

        /// <summary>
        /// Compara la copia rastreada con los campos que <see cref="Apply"/> escribe
        /// desde el DTO. <c>FechaModificacion</c> se ignora: siempre la fija la guarda.
        /// </summary>
        private static bool CoincideConDto(Matriz tracked, Matriz expected)
            => tracked.Clave == expected.Clave
                && tracked.Descripcion == expected.Descripcion
                && tracked.Unidad == expected.Unidad
                && tracked.Tipo == expected.Tipo
                && tracked.CostoDirecto == expected.CostoDirecto
                && tracked.ProyectoId == expected.ProyectoId;

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
                Rendimiento = component.TipoComponente == TipoComponenteMatriz.Maquinaria
                    ? Maquinaria.CalcularRendimiento(component.Cantidad) : 0m,
                Importe = component.Importe,
                Orden = component.Orden > 0 ? component.Orden : index + 1,
                Notas = component.Notas ?? string.Empty
            };
        }

        private static async Task<int> PropagateCascadeAsync(SOPROContext context, int matrixId)
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
                if (proyecto == null)
                {
                    throw new InvalidOperationException(
                        $"No se encontró el proyecto {affectedMatrix.ProyectoId} de la matriz dependiente {affectedMatrix.Id}.");
                }

                var totals = MatrixComponentCalculationService.Recalculate(
                    affectedMatrix.Componentes.ToList(), proyecto.DecimalesImporte);
                affectedMatrix.CostoDirecto = CalculationEngineFactory.FromProyecto(proyecto)
                    .RoundAmount(totals.CostoDirectoTotal);
            }

            await context.SaveChangesAsync();
            return affectedMatrixIds.Count;
        }
    }
}
