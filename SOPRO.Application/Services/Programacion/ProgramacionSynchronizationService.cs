using Microsoft.EntityFrameworkCore;
using SOPRO.Application.Models.Presupuesto;
using SOPRO.Application.Services.Programacion;
using SOPRO.Application.Services.Reporting;
using SOPRO.Core.Entities;
using SOPRO.Data.Context;
using SOPRO.Data.Factories;

namespace SOPRO.Application.Services
{
    /// <summary>
    /// Operaciones de sincronización/reconstrucción/borrado del programa de obra y
    /// generación desde presupuesto. Las rutas vigentes reciben la fábrica de contextos
    /// y la ruta real de la base, y delegan en <see cref="BudgetWriteExecutor"/> para
    /// ejecutar TODO el trabajo sobre un contexto nuevo dentro de una única transacción:
    /// los SaveChanges internos de generación son válidos, pero el commit es uno solo y
    /// cualquier fallo posterior revierte la operación completa.
    /// </summary>
    public sealed class ProgramacionSynchronizationService
    {
        private const string TipoReporteProgramaObra = EstilosFilaPredeterminados.TipoReporteProgramaObra;
        private const string MensajeConflictoProgramaActivo =
            "El programa activo cambió en otra ventana. Recargue el programa antes de reintentar.";

        private readonly TimeProvider _timeProvider;
        private readonly ProgramacionGenerationService _generationService;
        private readonly ProgramacionCalculationService _calculationService;
        private readonly ProgramacionDistributionService _distributionService;

        public ProgramacionSynchronizationService(TimeProvider? timeProvider = null)
        {
            _timeProvider = timeProvider ?? TimeProvider.System;
            _generationService = new ProgramacionGenerationService(_timeProvider);
            _calculationService = new ProgramacionCalculationService(_timeProvider);
            _distributionService = new ProgramacionDistributionService(_timeProvider);
        }

        // ═══════════════════════ Rutas atómicas vigentes ═══════════════════════

        /// <summary>
        /// Sincroniza el programa activo con el presupuesto en una sola transacción.
        /// <paramref name="expectedActiveProgramaId"/> es el programa que la vista tenía
        /// como activo (null = no había ninguno); se revalida dentro de la transacción y
        /// un cambio en otra ventana devuelve conflicto sin escribir. Preserva actividades
        /// manuales y fórmulas generadas; si no hay programa activo, la generación ocurre
        /// dentro de la misma transacción.
        /// </summary>
        public ProgramSyncResult SyncFromBudget(
            IProjectDbContextFactory factory,
            string databasePath,
            int proyectoId,
            int? expectedActiveProgramaId)
        {
            var execution = BudgetWriteExecutor.ExecuteWorkTransaction(
                factory,
                databasePath,
                stage: context => StageProject(context, proyectoId),
                work: context => SyncFromBudgetWork(context, proyectoId, expectedActiveProgramaId));

            return ProgramSyncResult.FromExecution(execution);
        }

        /// <summary>
        /// Reconstruye el programa desde el presupuesto en una sola transacción: conserva
        /// calendario y formato del programa, elimina el grafo anterior y regenera. Si la
        /// generación falla después de escribir, el callback aborta y se revierte todo.
        /// El programa activo esperado se revalida dentro de la transacción.
        /// </summary>
        public ProgramSyncResult RebuildProgram(
            IProjectDbContextFactory factory,
            string databasePath,
            int proyectoId,
            int? expectedActiveProgramaId)
        {
            var execution = BudgetWriteExecutor.ExecuteWorkTransaction(
                factory,
                databasePath,
                stage: context => StageProject(context, proyectoId),
                work: context => RebuildProgramWork(context, proyectoId, expectedActiveProgramaId));

            return ProgramSyncResult.FromExecution(execution);
        }

        /// <summary>
        /// Genera el programa base desde presupuesto en una sola transacción. La generación
        /// hace varios SaveChanges internos (identidades por tramos), pero el commit es uno
        /// solo: un fallo tardío revierte programa, actividades, periodos y calendario.
        /// Si ya existe un programa activo distinto del esperado, devuelve conflicto sin
        /// escribir; si coincide con el esperado, no-op confirmado.
        /// </summary>
        public ProgramSyncResult GenerateProgram(
            IProjectDbContextFactory factory,
            string databasePath,
            int proyectoId,
            int? expectedActiveProgramaId,
            DateTime fechaInicio,
            TipoPeriodoPrograma tipoPeriodo)
        {
            var execution = BudgetWriteExecutor.ExecuteWorkTransaction(
                factory,
                databasePath,
                stage: context => StageProject(context, proyectoId),
                work: context => GenerateProgramWork(
                    context, proyectoId, expectedActiveProgramaId, fechaInicio, tipoPeriodo));

            return ProgramSyncResult.FromExecution(execution);
        }

        /// <summary>
        /// Elimina el programa completo del proyecto en una sola transacción y con un solo
        /// SaveChanges: dependencias, distribuciones, actividades, periodos, programas,
        /// calendarios (opcional) y los overrides de celda/estilo de fila persistidos para
        /// el reporte ProgramaObra. El programa activo esperado se revalida dentro de la
        /// transacción; el no-op solo se decide allí y solo si todas esas tablas están vacías.
        /// </summary>
        public ProgramSyncResult DeleteProgram(
            IProjectDbContextFactory factory,
            string databasePath,
            int proyectoId,
            int? expectedActiveProgramaId,
            bool includeCalendars = true)
        {
            var execution = BudgetWriteExecutor.ExecuteWorkTransaction(
                factory,
                databasePath,
                stage: context => StageProject(context, proyectoId),
                work: context => DeleteProgramWork(
                    context, proyectoId, expectedActiveProgramaId, includeCalendars));

            return ProgramSyncResult.FromExecution(execution);
        }

        /// <summary>
        /// Recalcula el programa activo y regenera periodos/distribuciones en una sola
        /// transacción: recálculo de red, reemplazo de periodos, distribución uniforme por
        /// lote y recálculo final comparten un único commit. <paramref name="tipoPeriodo"/>
        /// null conserva el tipo/duración actuales; un valor actualiza TipoPeriodo y
        /// DuracionPeriodoDias antes de recalcular/regenerar. <paramref name="calendarioLaboralId"/>
        /// enlaza el programa a ese calendario del proyecto SÓLO si aún no tenía uno, dentro
        /// de la misma transacción (flujo posterior a editar el calendario). El programa
        /// activo esperado se revalida exactamente dentro de la transacción; un reemplazo en
        /// otra ventana (o más de un activo) devuelve conflicto sin escribir.
        /// </summary>
        public ProgramSyncResult RecalculateProgram(
            IProjectDbContextFactory factory,
            string databasePath,
            int proyectoId,
            int? expectedActiveProgramaId,
            TipoPeriodoPrograma? tipoPeriodo = null,
            int? calendarioLaboralId = null)
        {
            var execution = BudgetWriteExecutor.ExecuteWorkTransaction(
                factory,
                databasePath,
                stage: context => StageProject(context, proyectoId),
                work: context => RecalculateProgramWork(
                    context, proyectoId, expectedActiveProgramaId, tipoPeriodo, calendarioLaboralId));

            return ProgramSyncResult.FromExecution(execution);
        }

        // ═════════════════════════ Etapas previas (sin escritura) ═════════════════════════

        private static BudgetWriteStep<ProgramSyncResult> AbortFailed(string message, string? diagnostic = null)
            => BudgetWriteStep<ProgramSyncResult>.Abort(BudgetOperationResult.Failed(message), diagnostic);

        private static BudgetWriteStep<ProgramSyncResult> StageProject(SOPROContext context, int proyectoId)
            => context.Proyectos.Any(p => p.Id == proyectoId)
                ? BudgetWriteStep<ProgramSyncResult>.Proceed()
                : AbortFailed("El proyecto no existe.");

        /// <summary>
        /// Revalida DENTRO de la transacción el programa activo que la vista tenía como
        /// base. La base solo coincide si no hay ningún activo con expected null, o si hay
        /// exactamente un activo y es el esperado. Devuelve el paso de conflicto (sin
        /// mutación) o null si la base coincide.
        /// </summary>
        private static BudgetWriteStep<ProgramSyncResult>? ConflictIfActiveProgramChanged(
            SOPROContext context,
            int proyectoId,
            int? expectedActiveProgramaId)
        {
            var activos = context.ProgramasObra
                .Where(p => p.ProyectoId == proyectoId && p.Activo)
                .OrderBy(p => p.Id)
                .Select(p => p.Id)
                .ToList();

            var coincide = expectedActiveProgramaId is int esperado
                ? activos.Count == 1 && activos[0] == esperado
                : activos.Count == 0;
            if (coincide)
                return null;

            var actual = activos.Count == 0 ? "ninguno" : string.Join(",", activos);
            var esperadoTexto = expectedActiveProgramaId?.ToString() ?? "ninguno";
            return BudgetWriteStep<ProgramSyncResult>.Abort(
                BudgetOperationResult.Conflict(MensajeConflictoProgramaActivo),
                $"programa activo esperado: {esperadoTexto}; actual: {actual}.");
        }

        // ═══════════════════════════ Trabajo transaccional ═══════════════════════════

        private BudgetWriteStep<ProgramSyncResult> DeleteProgramWork(
            SOPROContext context,
            int proyectoId,
            int? expectedActiveProgramaId,
            bool includeCalendars)
        {
            if (!context.Proyectos.Any(p => p.Id == proyectoId))
                return AbortFailed("El proyecto no existe.");

            var conflicto = ConflictIfActiveProgramChanged(context, proyectoId, expectedActiveProgramaId);
            if (conflicto != null)
                return conflicto;

            // Revalidación dentro de la transacción: el paso previo pudo quedar obsoleto.
            var programas = context.ProgramasObra.Where(p => p.ProyectoId == proyectoId).ToList();
            var formatos = context.FormatosCeldaReporte
                .Where(f => f.ProyectoId == proyectoId && f.TipoReporte == TipoReporteProgramaObra)
                .ToList();
            var estilos = context.ConfiguracionesEstiloFilaReporte
                .Where(f => f.ProyectoId == proyectoId && f.TipoReporte == TipoReporteProgramaObra)
                .ToList();
            var calendarios = includeCalendars
                ? context.CalendariosLaborales.Where(c => c.ProyectoId == proyectoId).ToList()
                : new List<CalendarioLaboral>();

            if (programas.Count == 0 && formatos.Count == 0 && estilos.Count == 0 && calendarios.Count == 0)
            {
                // No-op decidido con el estado real de la transacción: sin SaveChanges.
                return BudgetWriteStep<ProgramSyncResult>.Commit(
                    ProgramSyncResult.CommittedSinCambios("No existe programa de obra para eliminar."),
                    Array.Empty<int>());
            }

            RemoveProgramGraph(context, programas, formatos, estilos, calendarios);

            // Un único SaveChanges para todo el borrado (grafo + formato + calendarios).
            context.SaveChanges();

            return BudgetWriteStep<ProgramSyncResult>.Commit(
                ProgramSyncResult.Committed("Programa eliminado correctamente.", null, 0, 0),
                Array.Empty<int>());
        }

        private BudgetWriteStep<ProgramSyncResult> RebuildProgramWork(
            SOPROContext context,
            int proyectoId,
            int? expectedActiveProgramaId)
        {
            var proyecto = context.Proyectos.FirstOrDefault(p => p.Id == proyectoId);
            if (proyecto == null)
                return AbortFailed("El proyecto no existe.");

            var conflicto = ConflictIfActiveProgramChanged(context, proyectoId, expectedActiveProgramaId);
            if (conflicto != null)
                return conflicto;

            var programa = expectedActiveProgramaId is int esperado
                ? context.ProgramasObra.FirstOrDefault(p => p.Id == esperado && p.ProyectoId == proyectoId && p.Activo)
                : null;
            if (programa == null && expectedActiveProgramaId.HasValue)
            {
                return BudgetWriteStep<ProgramSyncResult>.Abort(
                    BudgetOperationResult.Conflict(MensajeConflictoProgramaActivo),
                    $"programa activo esperado: {expectedActiveProgramaId}; no se pudo leer dentro de la transacción.");
            }

            var fechaInicio = programa != null
                ? CalendarioCache.SanitizarFecha(programa.FechaInicioPrograma, _timeProvider)
                : (proyecto.FechaInicio == default
                    ? _timeProvider.GetLocalNow().Date
                    : proyecto.FechaInicio.Date);
            var tipoPeriodo = programa?.TipoPeriodo ?? TipoPeriodoPrograma.Semana;

            // Reconstruir elimina el grafo completo (todos los programas del proyecto) pero
            // conserva calendarios y formato persistido; luego genera dentro de la misma tx.
            var programas = context.ProgramasObra.Where(p => p.ProyectoId == proyectoId).ToList();
            RemoveProgramGraph(
                context,
                programas,
                Array.Empty<FormatoCeldaReporte>(),
                Array.Empty<ConfiguracionEstiloFilaReporte>(),
                Array.Empty<CalendarioLaboral>());

            // La generación comprueba que no exista un programa activo: se libera antes.
            context.SaveChanges();

            var generated = _generationService.GenerateFromBudget(context, proyectoId, fechaInicio, tipoPeriodo);
            if (!generated.Success)
            {
                return AbortFailed(generated.Message);
            }

            return BudgetWriteStep<ProgramSyncResult>.CommitAfterSave(
                () => ProgramSyncResult.Committed(
                    "Programa reconstruido desde presupuesto correctamente.",
                    generated.ProgramaObraId,
                    generated.ActividadesGeneradas,
                    generated.PeriodosGenerados),
                () => Array.Empty<int>());
        }

        private BudgetWriteStep<ProgramSyncResult> GenerateProgramWork(
            SOPROContext context,
            int proyectoId,
            int? expectedActiveProgramaId,
            DateTime fechaInicio,
            TipoPeriodoPrograma tipoPeriodo)
        {
            if (!context.Proyectos.Any(p => p.Id == proyectoId))
                return AbortFailed("El proyecto no existe.");

            var conflicto = ConflictIfActiveProgramChanged(context, proyectoId, expectedActiveProgramaId);
            if (conflicto != null)
                return conflicto;

            if (expectedActiveProgramaId.HasValue)
            {
                // La base ya tiene exactamente el programa esperado: no se genera ni se escribe.
                return BudgetWriteStep<ProgramSyncResult>.Commit(
                    ProgramSyncResult.CommittedSinCambios("El proyecto ya tiene un programa base generado."),
                    Array.Empty<int>());
            }

            var fecha = fechaInicio == default ? _timeProvider.GetLocalNow().Date : fechaInicio.Date;
            var generated = _generationService.GenerateFromBudget(context, proyectoId, fecha, tipoPeriodo);
            if (!generated.Success)
            {
                return AbortFailed(generated.Message);
            }

            return BudgetWriteStep<ProgramSyncResult>.CommitAfterSave(
                () => ProgramSyncResult.Committed(
                    generated.Message,
                    generated.ProgramaObraId,
                    generated.ActividadesGeneradas,
                    generated.PeriodosGenerados),
                () => Array.Empty<int>());
        }

        private BudgetWriteStep<ProgramSyncResult> RecalculateProgramWork(
            SOPROContext context,
            int proyectoId,
            int? expectedActiveProgramaId,
            TipoPeriodoPrograma? tipoPeriodo,
            int? calendarioLaboralId)
        {
            if (!context.Proyectos.Any(p => p.Id == proyectoId))
                return AbortFailed("El proyecto no existe.");

            var conflicto = ConflictIfActiveProgramChanged(context, proyectoId, expectedActiveProgramaId);
            if (conflicto != null)
                return conflicto;

            if (!expectedActiveProgramaId.HasValue)
                return AbortFailed("No existe un programa activo para recalcular.");

            var programaId = expectedActiveProgramaId.Value;
            var programa = context.ProgramasObra
                .FirstOrDefault(p => p.Id == programaId && p.ProyectoId == proyectoId && p.Activo);
            if (programa == null)
            {
                return BudgetWriteStep<ProgramSyncResult>.Abort(
                    BudgetOperationResult.Conflict(MensajeConflictoProgramaActivo),
                    $"programa activo esperado: {programaId}; no se pudo leer dentro de la transacción.");
            }

            // null conserva el tipo/duración actuales; un valor los actualiza antes del
            // recálculo/regeneración, todo dentro de la misma transacción.
            if (tipoPeriodo is TipoPeriodoPrograma nuevoTipo)
            {
                programa.TipoPeriodo = nuevoTipo;
                programa.DuracionPeriodoDias = DiasPorPeriodo(nuevoTipo);
                programa.FechaModificacion = _timeProvider.GetLocalNow().DateTime;
            }

            // Flujo posterior a editar el calendario: enlaza el calendario SÓLO si el
            // programa aún no tenía uno y el calendario pertenece al proyecto. La
            // asignación entra en el MISMO SaveChanges/transacción del recálculo (la UI
            // no hace una escritura previa separada).
            if (calendarioLaboralId is int calendarioId
                && !programa.CalendarioLaboralId.HasValue
                && context.CalendariosLaborales.Any(c => c.Id == calendarioId && c.ProyectoId == proyectoId))
            {
                programa.CalendarioLaboralId = calendarioId;
                programa.FechaModificacion = _timeProvider.GetLocalNow().DateTime;
            }

            var tipoEfectivo = tipoPeriodo ?? programa.TipoPeriodo;

            _calculationService.RecalculateProgram(context, programaId);
            _generationService.RegeneratePeriodsFromProgramRange(context, programaId, tipoEfectivo);
            _distributionService.DistributeUniformBatch(context, programaId);
            _calculationService.RecalculateProgram(context, programaId);

            return BudgetWriteStep<ProgramSyncResult>.CommitAfterSave(
                () => ProgramSyncResult.Committed(
                    "Programa recalculado correctamente.",
                    programaId,
                    context.ActividadesProgramadas.Count(a => a.ProgramaObraId == programaId),
                    context.PeriodosPrograma.Count(p => p.ProgramaObraId == programaId)),
                () => Array.Empty<int>());
        }

        private BudgetWriteStep<ProgramSyncResult> SyncFromBudgetWork(
            SOPROContext context,
            int proyectoId,
            int? expectedActiveProgramaId)
        {
            var proyecto = context.Proyectos.FirstOrDefault(p => p.Id == proyectoId);
            if (proyecto == null)
                return AbortFailed("El proyecto no existe.");

            var conflicto = ConflictIfActiveProgramChanged(context, proyectoId, expectedActiveProgramaId);
            if (conflicto != null)
                return conflicto;

            var programa = expectedActiveProgramaId is int esperado
                ? context.ProgramasObra
                    .Include(p => p.Actividades)
                    .Include(p => p.Periodos)
                    .FirstOrDefault(p => p.Id == esperado && p.ProyectoId == proyectoId && p.Activo)
                : null;

            if (programa == null)
            {
                if (expectedActiveProgramaId.HasValue)
                {
                    return BudgetWriteStep<ProgramSyncResult>.Abort(
                        BudgetOperationResult.Conflict(MensajeConflictoProgramaActivo),
                        $"programa activo esperado: {expectedActiveProgramaId}; no se pudo leer dentro de la transacción.");
                }

                var fechaInicio = proyecto.FechaInicio == default
                    ? _timeProvider.GetLocalNow().Date
                    : proyecto.FechaInicio.Date;
                var generated = _generationService.GenerateFromBudget(
                    context, proyectoId, fechaInicio, TipoPeriodoPrograma.Semana);

                return generated.Success
                    ? BudgetWriteStep<ProgramSyncResult>.CommitAfterSave(
                        () => ProgramSyncResult.Committed(
                            generated.Message,
                            generated.ProgramaObraId,
                            generated.ActividadesGeneradas,
                            generated.PeriodosGenerados),
                        () => Array.Empty<int>())
                    : AbortFailed(generated.Message);
            }

            var conceptos = context.ConceptosPresupuesto
                .AsNoTracking()
                .Where(c => c.ProyectoId == proyectoId)
                .OrderBy(c => c.Orden)
                .ToList();

            // Base saneada: un programa legacy puede tener FechaInicioPrograma = default
            var fechaBasePrograma = CalendarioCache.SanitizarFecha(programa.FechaInicioPrograma, _timeProvider);

            var actividadesExistentes = context.ActividadesProgramadas
                .Where(a => a.ProgramaObraId == programa.Id)
                .ToList();

            var porConceptoId = actividadesExistentes
                .Where(a => !a.EsManual && a.ConceptoPresupuestoId.HasValue)
                .GroupBy(a => a.ConceptoPresupuestoId!.Value)
                .ToDictionary(g => g.Key, g => g.OrderBy(x => x.Id).First());

            var conceptosVigentes = conceptos.Select(c => c.Id).ToHashSet();
            var aEliminar = actividadesExistentes
                .Where(a => !a.EsManual)
                .Where(a => !a.ConceptoPresupuestoId.HasValue || !conceptosVigentes.Contains(a.ConceptoPresupuestoId.Value))
                .ToList();

            if (aEliminar.Count > 0)
            {
                var idsAEliminar = aEliminar.Select(a => a.Id).ToHashSet();

                foreach (var actividad in actividadesExistentes)
                {
                    if (!actividad.EsManual
                        && actividad.ActividadPadreId.HasValue
                        && idsAEliminar.Contains(actividad.ActividadPadreId.Value))
                    {
                        actividad.ActividadPadreId = null;
                    }
                }

                context.SaveChanges();

                context.ActividadesProgramadas.RemoveRange(aEliminar);
                context.SaveChanges();
                actividadesExistentes = context.ActividadesProgramadas.Where(a => a.ProgramaObraId == programa.Id).ToList();
                porConceptoId = actividadesExistentes
                    .Where(a => !a.EsManual && a.ConceptoPresupuestoId.HasValue)
                    .GroupBy(a => a.ConceptoPresupuestoId!.Value)
                    .ToDictionary(g => g.Key, g => g.OrderBy(x => x.Id).First());
            }

            int orden = 1;
            int nuevos = 0;
            var actividadesPorConcepto = new Dictionary<int, ActividadProgramada>();

            foreach (var concepto in conceptos)
            {
                if (!porConceptoId.TryGetValue(concepto.Id, out var actividad))
                {
                    actividad = new ActividadProgramada
                    {
                        ProgramaObraId = programa.Id,
                        ConceptoPresupuestoId = concepto.Id,
                        EsManual = false,
                        FrentesTrabajo = 1,
                        MetodoDistribucion = MetodoDistribucionActividad.Uniforme,
                        FechaCreacion = _timeProvider.GetLocalNow().DateTime,
                    };
                    context.ActividadesProgramadas.Add(actividad);
                    nuevos++;
                }

                actividad.Clave = concepto.Clave ?? string.Empty;
                actividad.Descripcion = concepto.Descripcion ?? string.Empty;
                actividad.Unidad = concepto.EsAgrupador ? string.Empty : (concepto.Unidad ?? string.Empty);
                actividad.EsResumen = concepto.EsAgrupador;
                actividad.Nivel = Math.Max(0, concepto.Nivel);
                actividad.Orden = orden++;
                actividad.CantidadTotal = concepto.EsAgrupador ? 0m : concepto.Cantidad;
                actividad.PrecioUnitario = concepto.EsAgrupador ? 0m : concepto.PrecioUnitario;
                actividad.ImporteTotal = concepto.EsAgrupador ? 0m : concepto.ImporteTotal;
                actividad.FechaModificacion = _timeProvider.GetLocalNow().DateTime;

                if (concepto.EsAgrupador)
                {
                    actividad.FechaInicioProgramada = null;
                    actividad.FechaFinProgramada = null;
                    actividad.DuracionDiasHabiles = 0;
                    actividad.RendimientoDiario = 0;
                    actividad.FrentesTrabajo = 1;
                }
                else if (actividad.Id == 0 || (!actividad.FechaInicioProgramada.HasValue && !actividad.FechaFinProgramada.HasValue && actividad.DuracionDiasHabiles <= 0))
                {
                    actividad.FechaInicioProgramada = fechaBasePrograma;
                    actividad.DuracionDiasHabiles = EstimateDuration(concepto);
                    actividad.FechaFinProgramada = _calculationService.CalculateFinishDate(context, programa.Id, actividad.FechaInicioProgramada, actividad.DuracionDiasHabiles);
                }

                actividadesPorConcepto[concepto.Id] = actividad;
            }

            context.SaveChanges();

            foreach (var concepto in conceptos)
            {
                if (!actividadesPorConcepto.TryGetValue(concepto.Id, out var actividad))
                    continue;

                var padreConceptoId = ResolveParentConceptId(conceptos, concepto);
                if (padreConceptoId.HasValue && actividadesPorConcepto.TryGetValue(padreConceptoId.Value, out var padre))
                    actividad.ActividadPadreId = padre.Id;
                else
                    actividad.ActividadPadreId = null;
            }

            context.SaveChanges();

            _calculationService.RecalculateProgram(context, programa.Id);
            _generationService.RegeneratePeriodsFromProgramRange(context, programa.Id, programa.TipoPeriodo);

            var uniformes = context.ActividadesProgramadas
                .Where(a => a.ProgramaObraId == programa.Id && !a.EsResumen)
                .Select(a => new { a.Id, a.MetodoDistribucion })
                .ToList();

            foreach (var actividad in uniformes)
            {
                if (actividad.MetodoDistribucion == MetodoDistribucionActividad.Uniforme || !context.DistribucionesPeriodo.Any(d => d.ActividadProgramadaId == actividad.Id))
                    _distributionService.DistributeUniform(context, actividad.Id);
            }

            _calculationService.RecalculateProgram(context, programa.Id);

            return BudgetWriteStep<ProgramSyncResult>.CommitAfterSave(
                () => ProgramSyncResult.Committed(
                    $"Programa sincronizado con presupuesto. Nuevos: {nuevos}. Eliminados: {aEliminar.Count}.",
                    programa.Id,
                    context.ActividadesProgramadas.Count(a => a.ProgramaObraId == programa.Id),
                    context.PeriodosPrograma.Count(p => p.ProgramaObraId == programa.Id)),
                () => Array.Empty<int>());
        }

        // ═══════════════════════════════ Auxiliares ═══════════════════════════════

        private static void RemoveProgramGraph(
            SOPROContext context,
            IReadOnlyList<ProgramaObra> programas,
            IReadOnlyList<FormatoCeldaReporte> formatos,
            IReadOnlyList<ConfiguracionEstiloFilaReporte> estilos,
            IReadOnlyList<CalendarioLaboral> calendarios)
        {
            var programaIds = programas.Select(p => p.Id).ToList();
            if (programaIds.Count > 0)
            {
                var actividades = context.ActividadesProgramadas
                    .Where(a => programaIds.Contains(a.ProgramaObraId))
                    .ToList();
                var actividadIds = actividades.Select(a => a.Id).ToList();

                if (actividadIds.Count > 0)
                {
                    var dependencias = context.DependenciasActividad
                        .Where(d => actividadIds.Contains(d.ActividadOrigenId) || actividadIds.Contains(d.ActividadDestinoId))
                        .ToList();
                    if (dependencias.Count > 0)
                        context.DependenciasActividad.RemoveRange(dependencias);

                    var distribuciones = context.DistribucionesPeriodo
                        .Where(d => actividadIds.Contains(d.ActividadProgramadaId))
                        .ToList();
                    if (distribuciones.Count > 0)
                        context.DistribucionesPeriodo.RemoveRange(distribuciones);

                    foreach (var actividad in actividades)
                        actividad.ActividadPadreId = null;

                    context.ActividadesProgramadas.RemoveRange(actividades);
                }

                var periodos = context.PeriodosPrograma
                    .Where(p => programaIds.Contains(p.ProgramaObraId))
                    .ToList();
                if (periodos.Count > 0)
                    context.PeriodosPrograma.RemoveRange(periodos);

                context.ProgramasObra.RemoveRange(programas);
            }

            if (formatos.Count > 0)
                context.FormatosCeldaReporte.RemoveRange(formatos);
            if (estilos.Count > 0)
                context.ConfiguracionesEstiloFilaReporte.RemoveRange(estilos);
            if (calendarios.Count > 0)
                context.CalendariosLaborales.RemoveRange(calendarios);
        }

        private static int? ResolveParentConceptId(List<ConceptoPresupuesto> conceptos, ConceptoPresupuesto concepto)
        {
            if (concepto.PadreId.HasValue && conceptos.Any(c => c.Id == concepto.PadreId.Value))
                return concepto.PadreId.Value;

            var ordenados = conceptos.OrderBy(c => c.Orden).ToList();
            var index = ordenados.FindIndex(c => c.Id == concepto.Id);
            if (index <= 0)
                return null;

            for (int i = index - 1; i >= 0; i--)
            {
                var candidato = ordenados[i];
                if (candidato.Nivel < concepto.Nivel)
                    return candidato.Id;
            }

            return null;
        }

        private static int DiasPorPeriodo(TipoPeriodoPrograma tipoPeriodo)
            => tipoPeriodo switch
            {
                TipoPeriodoPrograma.Dia => 1,
                TipoPeriodoPrograma.Semana => 7,
                TipoPeriodoPrograma.Quincena => 15,
                TipoPeriodoPrograma.Mes => 30,
                _ => 7
            };

        private static int EstimateDuration(ConceptoPresupuesto concepto)
        {
            if (concepto.Cantidad <= 0) return 0;
            if (concepto.Cantidad <= 1) return 1;
            if (concepto.Cantidad <= 10) return 3;
            if (concepto.Cantidad <= 100) return 5;
            return 10;
        }
    }

    /// <summary>
    /// Resultado clasificado de una operación de programación. Solo
    /// <see cref="BudgetOperationStatus.Committed"/> autoriza a la vista a dar el trabajo
    /// por guardado; <see cref="Diagnostic"/> es detalle técnico y no debe mostrarse.
    /// <see cref="BudgetOperationStatus.Failed"/> implica que el rollback se confirmó (no
    /// existe un estado <c>RolledBack</c> separado); <see cref="IsOutcomeUnknown"/> indica
    /// que ni el commit ni la reversión pudieron verificarse.
    /// </summary>
    public sealed class ProgramSyncResult
    {
        private ProgramSyncResult(
            BudgetOperationStatus status,
            string message,
            int? programaObraId,
            int actividades,
            int periodos,
            bool sinCambios,
            string? diagnostic)
        {
            Status = status;
            Message = message;
            ProgramaObraId = programaObraId;
            Actividades = actividades;
            Periodos = periodos;
            SinCambios = sinCambios;
            Diagnostic = diagnostic;
        }

        public BudgetOperationStatus Status { get; }

        public string Message { get; }

        /// <summary>Id del programa resultante; solo se expone si la operación se confirmó.</summary>
        public int? ProgramaObraId { get; }

        public int Actividades { get; }

        public int Periodos { get; }

        /// <summary>La operación se confirmó sin necesidad de escribir en la base.</summary>
        public bool SinCambios { get; }

        /// <summary>Detalle técnico para registro y pruebas; nunca se muestra en la UI.</summary>
        public string? Diagnostic { get; }

        /// <summary>
        /// Compatibilidad con consumidores previos: hay un cambio confirmado. Un no-op
        /// confirmado (<see cref="SinCambios"/>) no cuenta como éxito para evitar que la
        /// vista dispare un reset de formato innecesario.
        /// </summary>
        public bool Success => Status == BudgetOperationStatus.Committed && !SinCambios;

        public bool IsOutcomeUnknown => Status == BudgetOperationStatus.OutcomeUnknown;

        public static ProgramSyncResult Ok(string message, int? programaObraId, int actividades, int periodos)
            => Committed(message, programaObraId, actividades, periodos);

        public static ProgramSyncResult Fail(string message)
            => Failed(message);

        public static ProgramSyncResult Committed(string message, int? programaObraId, int actividades, int periodos)
            => new(BudgetOperationStatus.Committed, message, programaObraId, actividades, periodos, false, null);

        public static ProgramSyncResult CommittedSinCambios(string message)
            => new(BudgetOperationStatus.Committed, message, null, 0, 0, true, null);

        public static ProgramSyncResult Failed(string message, string? diagnostic = null)
            => new(BudgetOperationStatus.Failed, message, null, 0, 0, false, diagnostic);

        public static ProgramSyncResult Conflict(string message, string? diagnostic = null)
            => new(BudgetOperationStatus.Conflict, message, null, 0, 0, false, diagnostic);

        public static ProgramSyncResult OutcomeUnknown(string message, string? diagnostic = null)
            => new(BudgetOperationStatus.OutcomeUnknown, message, null, 0, 0, false, diagnostic);

        internal static ProgramSyncResult FromExecution(BudgetWriteExecution<ProgramSyncResult> execution)
        {
            if (execution.Result.IsCommitted)
            {
                return execution.Confirmed
                    ?? ProgramSyncResult.Committed("Operación confirmada.", null, 0, 0);
            }

            var message = execution.Result.Message ?? "No se pudo completar la operación.";
            return execution.Result.Status switch
            {
                BudgetOperationStatus.Conflict => Conflict(message, execution.Diagnostic),
                BudgetOperationStatus.OutcomeUnknown => OutcomeUnknown(message, execution.Diagnostic),
                _ => Failed(message, execution.Diagnostic)
            };
        }
    }
}
