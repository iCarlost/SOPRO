using Microsoft.EntityFrameworkCore;
using Sopro.Calculation;
using SOPRO.Application.Contracts;
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
        internal const string DiagnosticsSource = "SOPRO.Application";
        internal const string OperationGuardarMatrizRollbackFallido = "GuardarMatrizRollbackFallido";

        private const string DefaultProductVersion = "0.0.0";

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
        /// adjunta al contexto de la operación. El sink/versión opcionales solo
        /// alimentan el evento de un fallo de reversión; no alteran la operación.
        /// </summary>
        public static Task<MatrixSaveResult> SaveAsync(
            SOPROContext context,
            MatrixEditDto dto,
            Matriz? existingMatrix = null,
            IDiagnosticsSink? diagnosticsSink = null,
            string? productVersion = null)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            return SaveAsync(
                DefaultDbContextFactory,
                context.DatabasePath,
                dto,
                existingMatrix,
                context,
                diagnosticsSink,
                productVersion);
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
        /// sobrescribirlas). El tracker no se guarda ni se limpia. El
        /// sink/versión opcionales solo alimentan el evento de un fallo de
        /// reversión; un fallo best-effort del sink nunca reemplaza la excepción
        /// original ni cambia la transacción.
        /// </summary>
        public static async Task<MatrixSaveResult> SaveAsync(
            IProjectDbContextFactory dbContextFactory,
            string databasePath,
            MatrixEditDto dto,
            Matriz? existingMatrix = null,
            SOPROContext? sessionContext = null,
            IDiagnosticsSink? diagnosticsSink = null,
            string? productVersion = null)
        {
            if (dbContextFactory == null) throw new ArgumentNullException(nameof(dbContextFactory));
            if (string.IsNullOrWhiteSpace(databasePath))
                throw new ArgumentException("La ruta de la base del proyecto es requerida.", nameof(databasePath));
            if (dto == null) throw new ArgumentNullException(nameof(dto));

            // Una sola correlación por operación: el evento de reversión fallida
            // (si ocurre) se enlaza con esta guarda y con nada más.
            var correlationId = DiagnosticsCorrelationId.Create(null);

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

            // Las ediciones de descripción de las fuentes también se validan antes
            // de cualquier escritura: un lote contradictorio o una fuente inválida
            // no debe dejar la matriz a medio guardar.
            var sourceDescriptionPatches = ValidateSourceDescriptionEdits(dto, existingMatrix?.Id);

            await using var operationContext = dbContextFactory.Create(databasePath);

            // Si la sesión tiene pendiente la descripción de una fuente, esa
            // edición es ajena a esta guarda (el staging nuevo ya no muta la
            // navegación): se rechaza antes de abrir la transacción.
            GuardPendingSourceDescriptionEdits(sessionContext, sourceDescriptionPatches);

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

                // Las descripciones de las fuentes se aplican después de la matriz
                // y sus componentes y antes de la cascada: cualquier fallo
                // posterior revierte las tres cosas juntas.
                if (sourceDescriptionPatches.Count > 0)
                {
                    await ApplySourceDescriptionEditsAsync(
                        operationContext,
                        dto.ProyectoId,
                        sourceDescriptionPatches);
                }

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
                    // Evento estructurado, uno solo y sin datos de negocio: la
                    // reversión no se pudo confirmar y la operación queda incierta.
                    WriteRollbackFailureDiagnostics(
                        rollbackFailure,
                        correlationId,
                        diagnosticsSink,
                        productVersion);
                }

                throw;
            }
        }

        /// <summary>
        /// Registra el fallo de reversión como un evento estructurado con
        /// operación fija. Nunca incluye el DTO, el mensaje, la traza, el SQL, la
        /// ruta ni datos de negocio: solo tipo, HResult y cadena de tipos de la
        /// excepción de reversión. Write y Flush son best-effort e independientes;
        /// nada de esto altera la transacción ni la excepción original.
        /// </summary>
        private static void WriteRollbackFailureDiagnostics(
            Exception rollbackFailure,
            string correlationId,
            IDiagnosticsSink? diagnosticsSink,
            string? productVersion)
        {
            if (diagnosticsSink is null)
            {
                return;
            }

            var version = string.IsNullOrWhiteSpace(productVersion)
                ? DefaultProductVersion
                : productVersion;

            try
            {
                diagnosticsSink.Write(DiagnosticsEvent.FromException(
                    DateTimeOffset.UtcNow,
                    DiagnosticsSeverity.Error,
                    DiagnosticsSource,
                    OperationGuardarMatrizRollbackFallido,
                    version,
                    correlationId,
                    rollbackFailure));
            }
            catch
            {
                // Best-effort: un sink defectuoso no reemplaza la excepción original.
            }

            try
            {
                diagnosticsSink.Flush();
            }
            catch
            {
                // El vaciado también es best-effort y nunca altera la operación.
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

            // El editor trabaja ahora sobre copias AsNoTracking: el DTO captura el
            // lote completo y ningún ComponenteMatriz de la matriz objetivo debería
            // estar rastreado como pendiente. Cualquier alta, modificación o baja
            // rastreada es trabajo ajeno de la sesión; el reemplazo por operación la
            // dejaría obsoleta y un guardado posterior podría sobrescribir los
            // valores recién confirmados o intentar actualizar filas eliminadas.
            // Solo Detached/Unchanged son inocuos.
            var ambiguousTargetComponents = sessionContext.ChangeTracker.Entries<ComponenteMatriz>()
                .Where(entry => entry.Entity.MatrizId == targetMatrixId
                    && (entry.State == EntityState.Added
                        || entry.State == EntityState.Modified
                        || entry.State == EntityState.Deleted))
                .Select(entry => entry.State)
                .ToList();

            if (ambiguousTargetComponents.Count > 0)
            {
                throw new InvalidOperationException(
                    $"Hay componentes agregados, modificados o eliminados sin guardar en la matriz {targetMatrixId}; " +
                    "el reemplazo por operación los dejaría obsoletos. " +
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
        /// Normaliza y valida las ediciones explícitas de descripción de fuentes.
        /// La descripción final se recorta igual que la edición de la cuadrícula
        /// (<see cref="MatrixComponentEditingService.UpdateDescription"/>); la
        /// original se conserva literal porque es la base de la comparación
        /// optimista contra la base. Un mismo insumo puede repetirse solo si
        /// baseline y descripción final coinciden.
        /// </summary>
        private static List<NormalizedSourceDescriptionPatch> ValidateSourceDescriptionEdits(
            MatrixEditDto dto,
            int? targetMatrixId)
        {
            var edits = dto.SourceDescriptionEdits;
            if (edits == null || edits.Count == 0)
            {
                return new List<NormalizedSourceDescriptionPatch>();
            }

            var unique = new Dictionary<(TipoComponenteMatriz Tipo, int SourceId), NormalizedSourceDescriptionPatch>();

            foreach (var edit in edits)
            {
                if (edit == null)
                {
                    throw new InvalidOperationException(
                        "La solicitud incluye una edición de descripción de insumo vacía.");
                }

                if (!IsSupportedSourceKind(edit.TipoComponente))
                {
                    throw new InvalidOperationException(
                        $"El tipo de componente {edit.TipoComponente} no admite edición de descripción desde la matriz.");
                }

                if (edit.SourceId <= 0)
                {
                    throw new InvalidOperationException(
                        "La edición de descripción requiere un identificador de insumo válido.");
                }

                if (edit.OriginalDescription == null)
                {
                    throw new InvalidOperationException(
                        $"La descripción original del insumo {edit.SourceId} es requerida para validar la edición.");
                }

                if (targetMatrixId.HasValue
                    && edit.TipoComponente == TipoComponenteMatriz.Auxiliar
                    && edit.SourceId == targetMatrixId.Value)
                {
                    throw new InvalidOperationException(
                        $"La matriz {targetMatrixId.Value} no puede editar su propia descripción como auxiliar; " +
                        "use el campo Descripción de la matriz.");
                }

                var patch = new NormalizedSourceDescriptionPatch(
                    edit.TipoComponente,
                    edit.SourceId,
                    edit.OriginalDescription,
                    (edit.NewDescription ?? string.Empty).Trim());

                var key = (patch.TipoComponente, patch.SourceId);
                if (unique.TryGetValue(key, out var existing))
                {
                    if (existing.OriginalDescription != patch.OriginalDescription
                        || existing.NormalizedDescription != patch.NormalizedDescription)
                    {
                        throw new InvalidOperationException(
                            $"Hay dos ediciones de descripción contradictorias para {DescribeSource(patch.TipoComponente)} " +
                            $"{patch.SourceId}. Deje una sola versión del cambio.");
                    }

                    continue;
                }

                unique.Add(key, patch);
            }

            return unique.Values
                .OrderBy(patch => patch.TipoComponente)
                .ThenBy(patch => patch.SourceId)
                .ToList();
        }

        private static bool IsSupportedSourceKind(TipoComponenteMatriz tipo)
            => tipo == TipoComponenteMatriz.Material
                || tipo == TipoComponenteMatriz.ManoDeObra
                || tipo == TipoComponenteMatriz.Maquinaria
                || tipo == TipoComponenteMatriz.Auxiliar
                || tipo == TipoComponenteMatriz.Herramienta;

        /// <summary>
        /// Rechaza, antes de escribir, cuando el tracker de la sesión tiene
        /// pendiente la descripción de una fuente del lote (o la eliminó). Las
        /// demás propiedades modificadas de la fuente no son conflicto: quedan
        /// pendientes y sin guardar.
        /// </summary>
        private static void GuardPendingSourceDescriptionEdits(
            SOPROContext? sessionContext,
            IReadOnlyList<NormalizedSourceDescriptionPatch> patches)
        {
            if (sessionContext == null || patches.Count == 0)
            {
                return;
            }

            sessionContext.ChangeTracker.DetectChanges();

            var conflicts = patches
                .Where(patch => HasPendingSourceDescriptionChange(sessionContext, patch))
                .Select(patch => $"{DescribeSource(patch.TipoComponente)} {patch.SourceId}")
                .ToList();

            if (conflicts.Count > 0)
            {
                throw new InvalidOperationException(
                    $"La descripción de {string.Join(", ", conflicts)} tiene cambios sin guardar " +
                    "o fue eliminada en la sesión. Guarde o descarte esos cambios antes de editar esta matriz.");
            }
        }

        private static bool HasPendingSourceDescriptionChange(
            SOPROContext sessionContext,
            NormalizedSourceDescriptionPatch patch)
        {
            return patch.TipoComponente switch
            {
                TipoComponenteMatriz.Material => HasPendingDescriptionChange<Material>(
                    sessionContext, patch.SourceId, material => material.Id),
                TipoComponenteMatriz.ManoDeObra => HasPendingDescriptionChange<ManoDeObra>(
                    sessionContext, patch.SourceId, manoDeObra => manoDeObra.Id),
                TipoComponenteMatriz.Maquinaria => HasPendingDescriptionChange<Maquinaria>(
                    sessionContext, patch.SourceId, maquinaria => maquinaria.Id),
                TipoComponenteMatriz.Auxiliar => HasPendingDescriptionChange<Matriz>(
                    sessionContext, patch.SourceId, matriz => matriz.Id),
                TipoComponenteMatriz.Herramienta => HasPendingDescriptionChange<Herramienta>(
                    sessionContext, patch.SourceId, herramienta => herramienta.Id),
                _ => false
            };
        }

        private static bool HasPendingDescriptionChange<TEntity>(
            SOPROContext sessionContext,
            int sourceId,
            Func<TEntity, int> idSelector)
            where TEntity : class
        {
            return sessionContext.ChangeTracker.Entries<TEntity>()
                .Where(entry => idSelector(entry.Entity) == sourceId)
                .Any(entry => entry.State == EntityState.Deleted
                    || (entry.State == EntityState.Modified
                        && entry.Property(nameof(Matriz.Descripcion)).IsModified));
        }

        /// <summary>
        /// Aplica cada edición con un UPDATE condicional de una sola propiedad
        /// (Id, proyecto y descripción original). Si la fuente no existe en el
        /// proyecto o su descripción cambió, no se toca ninguna fila y se lanza
        /// el conflicto para que la transacción revierta el resto. Los demás
        /// campos de la fuente (costos, precios, notas) no se modifican.
        /// </summary>
        private static async Task ApplySourceDescriptionEditsAsync(
            SOPROContext context,
            int proyectoId,
            IReadOnlyList<NormalizedSourceDescriptionPatch> patches)
        {
            foreach (var patch in patches)
            {
                var affected = patch.TipoComponente switch
                {
                    TipoComponenteMatriz.Material => await context.Materiales
                        .Where(material => material.Id == patch.SourceId
                            && material.ProyectoId == proyectoId
                            && material.Descripcion == patch.OriginalDescription)
                        .ExecuteUpdateAsync(setters => setters
                            .SetProperty(material => material.Descripcion, patch.NormalizedDescription)),
                    TipoComponenteMatriz.ManoDeObra => await context.ManoDeObra
                        .Where(manoDeObra => manoDeObra.Id == patch.SourceId
                            && manoDeObra.ProyectoId == proyectoId
                            && manoDeObra.Descripcion == patch.OriginalDescription)
                        .ExecuteUpdateAsync(setters => setters
                            .SetProperty(manoDeObra => manoDeObra.Descripcion, patch.NormalizedDescription)),
                    TipoComponenteMatriz.Maquinaria => await context.Maquinaria
                        .Where(maquinaria => maquinaria.Id == patch.SourceId
                            && maquinaria.ProyectoId == proyectoId
                            && maquinaria.Descripcion == patch.OriginalDescription)
                        .ExecuteUpdateAsync(setters => setters
                            .SetProperty(maquinaria => maquinaria.Descripcion, patch.NormalizedDescription)),
                    TipoComponenteMatriz.Auxiliar => await context.Matrices
                        .Where(auxiliar => auxiliar.Id == patch.SourceId
                            && auxiliar.ProyectoId == proyectoId
                            && auxiliar.Descripcion == patch.OriginalDescription)
                        .ExecuteUpdateAsync(setters => setters
                            .SetProperty(auxiliar => auxiliar.Descripcion, patch.NormalizedDescription)),
                    TipoComponenteMatriz.Herramienta => await context.Herramientas
                        .Where(herramienta => herramienta.Id == patch.SourceId
                            && herramienta.ProyectoId == proyectoId
                            && herramienta.Descripcion == patch.OriginalDescription)
                        .ExecuteUpdateAsync(setters => setters
                            .SetProperty(herramienta => herramienta.Descripcion, patch.NormalizedDescription)),
                    _ => 0
                };

                if (affected != 1)
                {
                    throw new InvalidOperationException(
                        $"No se pudo actualizar la descripción de {DescribeSource(patch.TipoComponente)} " +
                        $"{patch.SourceId}: el insumo ya no existe en el proyecto o su descripción cambió " +
                        "desde que se cargó la matriz. Recargue la vista e intente de nuevo.");
                }
            }
        }

        private static string DescribeSource(TipoComponenteMatriz tipo) => tipo switch
        {
            TipoComponenteMatriz.Material => "el material",
            TipoComponenteMatriz.ManoDeObra => "la mano de obra",
            TipoComponenteMatriz.Maquinaria => "la maquinaria",
            TipoComponenteMatriz.Auxiliar => "el auxiliar",
            TipoComponenteMatriz.Herramienta => "la herramienta",
            _ => $"el insumo ({tipo})"
        };

        private sealed record NormalizedSourceDescriptionPatch(
            TipoComponenteMatriz TipoComponente,
            int SourceId,
            string OriginalDescription,
            string NormalizedDescription);

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
