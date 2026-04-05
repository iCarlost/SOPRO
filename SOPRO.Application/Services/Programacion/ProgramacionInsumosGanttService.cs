using Microsoft.EntityFrameworkCore;
using SOPRO.Application.DTOs.Programacion;
using SOPRO.Application.DTOs.Programacion.Insumos;
using SOPRO.Core.Entities;
using SOPRO.Data.Context;

namespace SOPRO.Application.Services.Programacion
{
    public sealed class ProgramacionInsumosGanttService
    {
        private readonly ProgramacionInsumosService _insumosService = new();

        public GanttRenderModel Build(SOPROContext context, Proyecto proyecto, ProgramaInsumoTipo tipo, GanttViewMode viewMode, out ProgramaInsumosResultDto programa)
        {
            programa = _insumosService.Build(context, proyecto, tipo);
            var resultado = programa;
            var model = new GanttRenderModel
            {
                ViewMode = viewMode,
                TipoPeriodo = ObtenerTipoPeriodo(context, proyecto)
            };

            var programaObra = context.ProgramasObra
                .AsNoTracking()
                .FirstOrDefault(p => p.ProyectoId == proyecto.Id && p.Activo);

            if (programaObra == null)
                return model;

            var periodos = context.PeriodosPrograma
                .AsNoTracking()
                .Where(p => p.ProgramaObraId == programaObra.Id)
                .OrderBy(p => p.NumeroPeriodo)
                .Select(p => new
                {
                    p.Id,
                    p.NumeroPeriodo,
                    p.Etiqueta,
                    FechaInicio = p.FechaInicio.Date,
                    FechaFin = p.FechaFin.Date
                })
                .ToList();

            if (periodos.Count == 0 || resultado.Rows.Count == 0)
                return model;

            // La escala se construye más abajo con el mismo criterio temporal real que usa Programa de Obra.

            var totalCantidad = resultado.Rows.Sum(r => r.Total);
            var totalImporte = resultado.Rows.Sum(r => r.ImporteTotal);
            var rangosPorInsumo = ConstruirRangosRealesPorInsumo(context, programaObra.Id, proyecto, tipo);

            foreach (var row in resultado.Rows.OrderBy(r => r.Clave).ThenBy(r => r.Descripcion))
            {
                var segmentos = new List<GanttPeriodSegmentDto>();

                foreach (var periodo in periodos)
                {
                    row.CantidadesPorPeriodo.TryGetValue(periodo.Id, out var cant);
                    row.ImportesPorPeriodo.TryGetValue(periodo.Id, out var imp);

                    if (cant == 0m && imp == 0m)
                        continue;

                    segmentos.Add(new GanttPeriodSegmentDto
                    {
                        PeriodoProgramaId = periodo.Id,
                        EtiquetaPeriodo = string.IsNullOrWhiteSpace(periodo.Etiqueta) ? $"P{periodo.NumeroPeriodo:00}" : periodo.Etiqueta,
                        FechaInicio = periodo.FechaInicio,
                        FechaFin = periodo.FechaFin,
                        CantidadProgramada = cant,
                        ImporteProgramado = imp,
                        PorcentajeProgramado = row.Total == 0m ? 0m : decimal.Round((cant / row.Total) * 100m, 4, MidpointRounding.AwayFromZero),
                        PorcentajeFisicoGlobal = totalCantidad == 0m ? 0m : decimal.Round((cant / totalCantidad) * 100m, 4, MidpointRounding.AwayFromZero),
                        PorcentajeFinancieroGlobal = totalImporte == 0m ? 0m : decimal.Round((imp / totalImporte) * 100m, 4, MidpointRounding.AwayFromZero)
                    });
                }

                DateTime? inicio = null;
                DateTime? fin = null;
                if (rangosPorInsumo.TryGetValue(row.InsumoId, out var rangoReal))
                {
                    inicio = rangoReal.inicio;
                    fin = rangoReal.fin;
                }

                if (!inicio.HasValue && segmentos.Count > 0)
                    inicio = segmentos.Min(s => s.FechaInicio);
                if (!fin.HasValue && segmentos.Count > 0)
                    fin = segmentos.Max(s => s.FechaFin);

                model.Filas.Add(new GanttRowDto
                {
                    Id = row.InsumoId,
                    Clave = row.Clave,
                    Descripcion = row.Descripcion,
                    Inicio = inicio,
                    Fin = fin,
                    EsResumen = false,
                    EsCritica = false,
                    Nivel = 0,
                    Orden = model.Filas.Count,
                    ImporteProgramadoTotal = row.ImporteTotal,
                    TieneImporteProgramado = row.ImporteTotal > 0m,
                    SegmentosFinancieros = segmentos
                });
            }

            model.FooterPeriodos = periodos
                .Select(p =>
                {
                    var importePeriodo = resultado.Rows.Sum(r => r.ImportesPorPeriodo.TryGetValue(p.Id, out var v) ? v : 0m);
                    var cantidadPeriodo = resultado.Rows.Sum(r => r.CantidadesPorPeriodo.TryGetValue(p.Id, out var v) ? v : 0m);
                    return new GanttFooterPeriodDto
                    {
                        Etiqueta = string.IsNullOrWhiteSpace(p.Etiqueta) ? $"P{p.NumeroPeriodo:00}" : p.Etiqueta,
                        FechaInicio = p.FechaInicio,
                        FechaFin = p.FechaFin,
                        ImportePeriodo = importePeriodo,
                        PorcentajePeriodo = totalImporte == 0m ? 0m : decimal.Round((importePeriodo / totalImporte) * 100m, 4, MidpointRounding.AwayFromZero),
                        // temporarily store physical % in cantidad? no
                    };
                })
                .OrderBy(x => x.FechaInicio)
                .ToList();

            decimal runningImporte = 0m;
            decimal runningPorcentaje = 0m;
            foreach (var footer in model.FooterPeriodos.OrderBy(x => x.FechaInicio))
            {
                runningImporte += footer.ImportePeriodo;
                runningPorcentaje += footer.PorcentajePeriodo;
                footer.ImporteAcumulado = runningImporte;
                footer.PorcentajeAcumulado = runningPorcentaje;
            }

            if (model.Filas.Count > 0 && model.Filas.Any(f => f.Inicio.HasValue && f.Fin.HasValue))
            {
                var minFecha = model.Filas.Where(f => f.Inicio.HasValue).Min(f => f.Inicio!.Value.Date);
                var maxFecha = model.Filas.Where(f => f.Fin.HasValue).Max(f => f.Fin!.Value.Date);
                var inicioAlineado = AlinearInicio(minFecha, model.TipoPeriodo);
                var finAlineado = AlinearFin(maxFecha, model.TipoPeriodo);
                model.FechaInicioRango = inicioAlineado;
                model.FechaFinRango = finAlineado;
                model.Escala = CrearEscala(inicioAlineado, finAlineado, model.TipoPeriodo);
            }
            else
            {
                var minFecha = periodos.Min(p => p.FechaInicio);
                var maxFecha = periodos.Max(p => p.FechaFin);
                var inicioAlineado = AlinearInicio(minFecha, model.TipoPeriodo);
                var finAlineado = AlinearFin(maxFecha, model.TipoPeriodo);
                model.FechaInicioRango = inicioAlineado;
                model.FechaFinRango = finAlineado;
                model.Escala = CrearEscala(inicioAlineado, finAlineado, model.TipoPeriodo);
            }

            return model;
        }

        private Dictionary<int, (DateTime inicio, DateTime fin)> ConstruirRangosRealesPorInsumo(SOPROContext context, int programaObraId, Proyecto proyecto, ProgramaInsumoTipo tipo)
        {
            var actividades = context.ActividadesProgramadas
                .AsNoTracking()
                .Where(a => a.ProgramaObraId == programaObraId && !a.EsResumen && a.ConceptoPresupuestoId != null && a.FechaInicioProgramada != null && a.FechaFinProgramada != null)
                .Select(a => new { a.Id, a.ConceptoPresupuestoId, a.FechaInicioProgramada, a.FechaFinProgramada })
                .ToList();

            if (actividades.Count == 0)
                return new Dictionary<int, (DateTime inicio, DateTime fin)>();

            var conceptoIds = actividades.Where(a => a.ConceptoPresupuestoId.HasValue).Select(a => a.ConceptoPresupuestoId!.Value).Distinct().ToList();
            var conceptoMatriz = context.ConceptosPresupuesto
                .AsNoTracking()
                .Where(c => conceptoIds.Contains(c.Id) && c.MatrizId != null)
                .Select(c => new { c.Id, c.MatrizId })
                .ToDictionary(x => x.Id, x => x.MatrizId!.Value);

            var matrizIds = conceptoMatriz.Values.Distinct().ToList();
            if (matrizIds.Count == 0)
                return new Dictionary<int, (DateTime inicio, DateTime fin)>();

            var matrices = context.Matrices
                .Include(m => m.Componentes).ThenInclude(c => c.Material)
                .Include(m => m.Componentes).ThenInclude(c => c.ManoDeObra)
                .Include(m => m.Componentes).ThenInclude(c => c.Maquinaria)
                .Include(m => m.Componentes).ThenInclude(c => c.Herramienta)
                .Include(m => m.Componentes).ThenInclude(c => c.Auxiliar)
                .Where(m => matrizIds.Contains(m.Id) || context.Matrices.Any(mx => matrizIds.Contains(mx.Id) && mx.Componentes.Any(c => c.AuxiliarId == m.Id)))
                .AsNoTracking()
                .ToList();

            var extraAuxIds = matrices.SelectMany(m => m.Componentes).Where(c => c.AuxiliarId.HasValue).Select(c => c.AuxiliarId!.Value).ToHashSet();
            while (extraAuxIds.Except(matrices.Select(m => m.Id)).Any())
            {
                var faltantes = extraAuxIds.Except(matrices.Select(m => m.Id)).ToList();
                if (faltantes.Count == 0) break;
                var extra = context.Matrices
                    .Include(m => m.Componentes).ThenInclude(c => c.Material)
                    .Include(m => m.Componentes).ThenInclude(c => c.ManoDeObra)
                    .Include(m => m.Componentes).ThenInclude(c => c.Maquinaria)
                    .Include(m => m.Componentes).ThenInclude(c => c.Herramienta)
                    .Include(m => m.Componentes).ThenInclude(c => c.Auxiliar)
                    .Where(m => faltantes.Contains(m.Id))
                    .AsNoTracking()
                    .ToList();
                if (extra.Count == 0) break;
                matrices.AddRange(extra);
                foreach (var id in extra.SelectMany(m => m.Componentes).Where(c => c.AuxiliarId.HasValue).Select(c => c.AuxiliarId!.Value))
                    extraAuxIds.Add(id);
            }

            var matrizPorId = matrices.GroupBy(m => m.Id).Select(g => g.First()).ToDictionary(m => m.Id);
            foreach (var mat in matrizPorId.Values)
            {
                foreach (var comp in mat.Componentes)
                {
                    if (comp.AuxiliarId.HasValue && matrizPorId.TryGetValue(comp.AuxiliarId.Value, out var aux))
                        comp.Auxiliar = aux;
                }
            }

            var rangos = new Dictionary<int, (DateTime inicio, DateTime fin)>();
            foreach (var actividad in actividades)
            {
                if (!actividad.ConceptoPresupuestoId.HasValue)
                    continue;
                if (!conceptoMatriz.TryGetValue(actividad.ConceptoPresupuestoId.Value, out var matrizId))
                    continue;
                if (!matrizPorId.TryGetValue(matrizId, out var matriz))
                    continue;

                foreach (var insumoId in EnumerarInsumos(matriz, tipo, new HashSet<int>()))
                {
                    var inicio = actividad.FechaInicioProgramada!.Value.Date;
                    var fin = actividad.FechaFinProgramada!.Value.Date;
                    if (rangos.TryGetValue(insumoId, out var existente))
                        rangos[insumoId] = (existente.inicio <= inicio ? existente.inicio : inicio, existente.fin >= fin ? existente.fin : fin);
                    else
                        rangos[insumoId] = (inicio, fin);
                }
            }

            return rangos;
        }

        private static IEnumerable<int> EnumerarInsumos(Matriz matriz, ProgramaInsumoTipo tipo, HashSet<int> visitados)
        {
            if (matriz == null || !visitados.Add(matriz.Id) || matriz.Componentes == null)
                yield break;

            foreach (var comp in matriz.Componentes)
            {
                switch (comp.TipoComponente)
                {
                    case TipoComponenteMatriz.Material when tipo == ProgramaInsumoTipo.Materiales && comp.MaterialId.HasValue:
                        yield return comp.MaterialId.Value;
                        break;
                    case TipoComponenteMatriz.ManoDeObra when tipo == ProgramaInsumoTipo.ManoDeObra && comp.ManoDeObraId.HasValue:
                        yield return comp.ManoDeObraId.Value;
                        break;
                    case TipoComponenteMatriz.Maquinaria when tipo == ProgramaInsumoTipo.Maquinaria && comp.MaquinariaId.HasValue:
                        yield return comp.MaquinariaId.Value;
                        break;
                    case TipoComponenteMatriz.Herramienta when tipo == ProgramaInsumoTipo.Herramienta && comp.HerramientaId.HasValue:
                        yield return comp.HerramientaId.Value;
                        break;
                    case TipoComponenteMatriz.Auxiliar when comp.Auxiliar != null:
                        foreach (var id in EnumerarInsumos(comp.Auxiliar, tipo, visitados))
                            yield return id;
                        break;
                }
            }
        }

        private static TipoPeriodoPrograma ObtenerTipoPeriodo(SOPROContext context, Proyecto proyecto)
        {
            var programa = context.ProgramasObra
                .AsNoTracking()
                .Where(p => p.ProyectoId == proyecto.Id && p.Activo)
                .Select(p => (TipoPeriodoPrograma?)p.TipoPeriodo)
                .FirstOrDefault();
            return programa ?? TipoPeriodoPrograma.Semana;
        }

        private static DateTime AlinearInicio(DateTime fecha, TipoPeriodoPrograma tipoPeriodo)
        {
            fecha = fecha.Date;
            return tipoPeriodo switch
            {
                TipoPeriodoPrograma.Dia => fecha,
                TipoPeriodoPrograma.Semana => fecha.AddDays(-(((int)fecha.DayOfWeek - (int)DayOfWeek.Monday + 7) % 7)),
                TipoPeriodoPrograma.Quincena => fecha.Day <= 15
                    ? new DateTime(fecha.Year, fecha.Month, 1)
                    : new DateTime(fecha.Year, fecha.Month, 16),
                TipoPeriodoPrograma.Mes => new DateTime(fecha.Year, fecha.Month, 1),
                _ => fecha
            };
        }

        private static DateTime AlinearFin(DateTime fecha, TipoPeriodoPrograma tipoPeriodo)
        {
            fecha = fecha.Date;
            return tipoPeriodo switch
            {
                TipoPeriodoPrograma.Dia => fecha,
                TipoPeriodoPrograma.Semana => fecha.AddDays(6 - (((int)fecha.DayOfWeek - (int)DayOfWeek.Monday + 7) % 7)),
                TipoPeriodoPrograma.Quincena => fecha.Day <= 15
                    ? new DateTime(fecha.Year, fecha.Month, 15)
                    : new DateTime(fecha.Year, fecha.Month, DateTime.DaysInMonth(fecha.Year, fecha.Month)),
                TipoPeriodoPrograma.Mes => new DateTime(fecha.Year, fecha.Month, DateTime.DaysInMonth(fecha.Year, fecha.Month)),
                _ => fecha
            };
        }

        private static List<GanttScaleCellDto> CrearEscala(DateTime inicio, DateTime fin, TipoPeriodoPrograma tipoPeriodo)
        {
            var celdas = new List<GanttScaleCellDto>();
            var cursor = inicio.Date;

            while (cursor <= fin.Date)
            {
                var celdaInicio = cursor.Date;
                DateTime celdaFin;
                string etiqueta;
                string grupo;

                switch (tipoPeriodo)
                {
                    case TipoPeriodoPrograma.Dia:
                        celdaFin = celdaInicio;
                        etiqueta = celdaInicio.ToString("dd");
                        grupo = celdaInicio.ToString("MMM yyyy");
                        break;
                    case TipoPeriodoPrograma.Semana:
                        celdaFin = Min(fin.Date, celdaInicio.AddDays(6));
                        etiqueta = $"S{System.Globalization.ISOWeek.GetWeekOfYear(celdaInicio):00}";
                        grupo = celdaInicio.ToString("MMM yyyy");
                        break;
                    case TipoPeriodoPrograma.Quincena:
                        var mitad = celdaInicio.Day <= 15 ? 1 : 2;
                        celdaFin = mitad == 1
                            ? new DateTime(celdaInicio.Year, celdaInicio.Month, 15)
                            : new DateTime(celdaInicio.Year, celdaInicio.Month, DateTime.DaysInMonth(celdaInicio.Year, celdaInicio.Month));
                        celdaFin = Min(fin.Date, celdaFin);
                        etiqueta = mitad == 1 ? "Q1" : "Q2";
                        grupo = celdaInicio.ToString("MMM yyyy");
                        break;
                    case TipoPeriodoPrograma.Mes:
                    default:
                        celdaFin = new DateTime(celdaInicio.Year, celdaInicio.Month, DateTime.DaysInMonth(celdaInicio.Year, celdaInicio.Month));
                        celdaFin = Min(fin.Date, celdaFin);
                        etiqueta = celdaInicio.ToString("MMM").ToUpperInvariant();
                        grupo = celdaInicio.ToString("yyyy");
                        break;
                }

                celdas.Add(new GanttScaleCellDto
                {
                    FechaInicio = celdaInicio,
                    FechaFin = celdaFin,
                    Etiqueta = etiqueta,
                    GrupoEtiqueta = grupo
                });

                cursor = celdaFin.AddDays(1);
            }

            return celdas;
        }

        private static DateTime Min(DateTime a, DateTime b) => a <= b ? a : b;

        private static string ObtenerGrupoEtiqueta(TipoPeriodoPrograma tipoPeriodo, DateTime fecha)
        {
            return tipoPeriodo switch
            {
                TipoPeriodoPrograma.Dia => fecha.ToString("MMM yyyy"),
                TipoPeriodoPrograma.Semana => fecha.ToString("MMM yyyy"),
                TipoPeriodoPrograma.Quincena => fecha.ToString("MMM yyyy"),
                TipoPeriodoPrograma.Mes => fecha.ToString("yyyy"),
                _ => fecha.ToString("MMM yyyy")
            };
        }
    }
}
