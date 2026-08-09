using Microsoft.EntityFrameworkCore;
using SOPRO.Application.Services.Programacion;
using SOPRO.Core.Entities;
using SOPRO.Data.Context;

namespace SOPRO.Application.Services
{
    public sealed class ProgramacionSynchronizationService
    {
        private readonly ProgramacionGenerationService _generationService = new();
        private readonly ProgramacionCalculationService _calculationService = new();
        private readonly ProgramacionDistributionService _distributionService = new();

        public ProgramSyncResult SyncFromBudget(SOPROContext context, int proyectoId)
        {
            var proyecto = context.Proyectos.FirstOrDefault(p => p.Id == proyectoId);
            if (proyecto == null)
                return ProgramSyncResult.Fail("El proyecto no existe.");

            var programa = context.ProgramasObra
                .Include(p => p.Actividades)
                .Include(p => p.Periodos)
                .FirstOrDefault(p => p.ProyectoId == proyectoId && p.Activo);

            if (programa == null)
            {
                var fechaInicio = proyecto.FechaInicio == default ? DateTime.Today : proyecto.FechaInicio.Date;
                var generated = _generationService.GenerateFromBudget(context, proyectoId, fechaInicio, TipoPeriodoPrograma.Semana);
                return generated.Success
                    ? ProgramSyncResult.Ok(generated.Message, generated.ProgramaObraId, generated.ActividadesGeneradas, generated.PeriodosGenerados)
                    : ProgramSyncResult.Fail(generated.Message);
            }

            var conceptos = context.ConceptosPresupuesto
                .AsNoTracking()
                .Where(c => c.ProyectoId == proyectoId)
                .OrderBy(c => c.Orden)
                .ToList();

            // Base saneada: un programa legacy puede tener FechaInicioPrograma = default
            var fechaBasePrograma = CalendarioCache.SanitizarFecha(programa.FechaInicioPrograma);

            var actividadesExistentes = context.ActividadesProgramadas
                .Where(a => a.ProgramaObraId == programa.Id)
                .ToList();

            var porConceptoId = actividadesExistentes
                .Where(a => a.ConceptoPresupuestoId.HasValue)
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
                    if (actividad.ActividadPadreId.HasValue && idsAEliminar.Contains(actividad.ActividadPadreId.Value))
                        actividad.ActividadPadreId = null;
                }

                context.SaveChanges();

                context.ActividadesProgramadas.RemoveRange(aEliminar);
                context.SaveChanges();
                actividadesExistentes = context.ActividadesProgramadas.Where(a => a.ProgramaObraId == programa.Id).ToList();
                porConceptoId = actividadesExistentes
                    .Where(a => a.ConceptoPresupuestoId.HasValue)
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
                        FechaCreacion = DateTime.Now,
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
                actividad.FechaModificacion = DateTime.Now;

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
            return ProgramSyncResult.Ok($"Programa sincronizado con presupuesto. Nuevos: {nuevos}. Eliminados: {aEliminar.Count}.", programa.Id, context.ActividadesProgramadas.Count(a => a.ProgramaObraId == programa.Id), context.PeriodosPrograma.Count(p => p.ProgramaObraId == programa.Id));
        }

        public ProgramSyncResult RebuildProgram(SOPROContext context, int proyectoId)
        {
            var proyecto = context.Proyectos.FirstOrDefault(p => p.Id == proyectoId);
            if (proyecto == null)
                return ProgramSyncResult.Fail("El proyecto no existe.");

            var programa = context.ProgramasObra.FirstOrDefault(p => p.ProyectoId == proyectoId && p.Activo);
            var fechaInicio = programa != null
                ? CalendarioCache.SanitizarFecha(programa.FechaInicioPrograma)
                : (proyecto.FechaInicio == default ? DateTime.Today : proyecto.FechaInicio.Date);
            var tipoPeriodo = programa?.TipoPeriodo ?? TipoPeriodoPrograma.Semana;

            var deleteResult = DeleteProgram(context, proyectoId, includeCalendars: false);
            if (!deleteResult.Success)
                return deleteResult;

            var generated = _generationService.GenerateFromBudget(context, proyectoId, fechaInicio, tipoPeriodo);
            return generated.Success
                ? ProgramSyncResult.Ok("Programa reconstruido desde presupuesto correctamente.", generated.ProgramaObraId, generated.ActividadesGeneradas, generated.PeriodosGenerados)
                : ProgramSyncResult.Fail(generated.Message);
        }

        public ProgramSyncResult DeleteProgram(SOPROContext context, int proyectoId, bool includeCalendars = true)
        {
            var programas = context.ProgramasObra.Where(p => p.ProyectoId == proyectoId).ToList();
            if (programas.Count == 0)
                return ProgramSyncResult.Ok("No existe programa de obra para eliminar.", null, 0, 0);

            try
            {
                var programaIds = programas.Select(p => p.Id).ToList();
                var actividades = context.ActividadesProgramadas.Where(a => programaIds.Contains(a.ProgramaObraId)).ToList();
                var actividadIds = actividades.Select(a => a.Id).ToList();

                if (actividadIds.Count > 0)
                {
                    var dependencias = context.DependenciasActividad
                        .Where(d => actividadIds.Contains(d.ActividadOrigenId) || actividadIds.Contains(d.ActividadDestinoId))
                        .ToList();
                    if (dependencias.Any())
                        context.DependenciasActividad.RemoveRange(dependencias);

                    var distribuciones = context.DistribucionesPeriodo
                        .Where(d => actividadIds.Contains(d.ActividadProgramadaId))
                        .ToList();
                    if (distribuciones.Any())
                        context.DistribucionesPeriodo.RemoveRange(distribuciones);

                    foreach (var actividad in actividades)
                        actividad.ActividadPadreId = null;

                    context.SaveChanges();

                    context.ActividadesProgramadas.RemoveRange(actividades);
                    context.SaveChanges();
                }

                var periodos = context.PeriodosPrograma.Where(p => programaIds.Contains(p.ProgramaObraId)).ToList();
                if (periodos.Any())
                {
                    context.PeriodosPrograma.RemoveRange(periodos);
                    context.SaveChanges();
                }

                context.ProgramasObra.RemoveRange(programas);
                context.SaveChanges();

                if (includeCalendars)
                {
                    var calendarios = context.CalendariosLaborales.Where(c => c.ProyectoId == proyectoId).ToList();
                    if (calendarios.Count > 0)
                    {
                        context.CalendariosLaborales.RemoveRange(calendarios);
                        context.SaveChanges();
                    }
                }

                return ProgramSyncResult.Ok("Programa eliminado correctamente.", null, 0, 0);
            }
            catch (Exception ex)
            {
                return ProgramSyncResult.Fail($"No se pudo eliminar el programa. {ex.Message}");
            }
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

        private static int EstimateDuration(ConceptoPresupuesto concepto)
        {
            if (concepto.Cantidad <= 0) return 0;
            if (concepto.Cantidad <= 1) return 1;
            if (concepto.Cantidad <= 10) return 3;
            if (concepto.Cantidad <= 100) return 5;
            return 10;
        }
    }

    public sealed class ProgramSyncResult
    {
        public bool Success { get; private set; }
        public string Message { get; private set; } = string.Empty;
        public int? ProgramaObraId { get; private set; }
        public int Actividades { get; private set; }
        public int Periodos { get; private set; }

        public static ProgramSyncResult Ok(string message, int? programaObraId, int actividades, int periodos)
            => new() { Success = true, Message = message, ProgramaObraId = programaObraId, Actividades = actividades, Periodos = periodos };

        public static ProgramSyncResult Fail(string message)
            => new() { Success = false, Message = message };
    }
}
