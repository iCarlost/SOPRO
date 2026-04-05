using Microsoft.EntityFrameworkCore;
using SOPRO.Application.DTOs.Programacion;
using SOPRO.Core.Entities;
using SOPRO.Data.Context;

namespace SOPRO.Application.Services
{
    public sealed class ProgramacionGanttService
    {
        public GanttRenderModel BuildProgramGantt(ProgramLoadDto? programa, TipoPeriodoPrograma tipoPeriodo, GanttViewMode viewMode = GanttViewMode.ProgramaObra)
        {
            var model = new GanttRenderModel
            {
                TipoPeriodo = tipoPeriodo,
                ViewMode = viewMode
            };

            if (programa == null)
                return model;

            model.Filas = programa.Actividades
                .OrderBy(a => a.Orden)
                .ThenBy(a => a.Id)
                .Select(a => new GanttRowDto
                {
                    Id = a.Id,
                    Clave = a.Clave,
                    Descripcion = a.Descripcion,
                    Inicio = a.FechaInicioProgramada?.Date,
                    Fin = a.FechaFinProgramada?.Date,
                    EsResumen = a.EsResumen,
                    EsCritica = a.RutaCritica,
                    Nivel = a.Nivel,
                    Orden = a.Orden
                })
                .ToList();

            AplicarRangoYEscala(model);
            return model;
        }

        public GanttRenderModel BuildFinancialGantt(SOPROContext context, ProgramLoadDto? programa, TipoPeriodoPrograma tipoPeriodo, bool mixedMode = false)
        {
            var model = BuildProgramGantt(programa, tipoPeriodo, mixedMode ? GanttViewMode.Mixto : GanttViewMode.Erogaciones);
            if (programa == null || model.Filas.Count == 0)
                return model;

            var actividadesOrdenadas = programa.Actividades
                .OrderBy(a => a.Orden)
                .ThenBy(a => a.Id)
                .ToList();
            var filasPorId = model.Filas.ToDictionary(x => x.Id);
            var actividadIds = actividadesOrdenadas.Select(a => a.Id).ToList();

            var distribucionesCrudas = context.DistribucionesPeriodo
                .AsNoTracking()
                .Where(d => actividadIds.Contains(d.ActividadProgramadaId))
                .ToList();

            var totalCantidadProgramada = distribucionesCrudas.Sum(x => x.CantidadProgramada);
            var totalImporteProgramado = distribucionesCrudas.Sum(x => x.ImporteProgramado);

            var segmentosPorActividad = distribucionesCrudas
                .Join(
                    context.PeriodosPrograma.AsNoTracking(),
                    d => d.PeriodoProgramaId,
                    p => p.Id,
                    (d, p) => new
                    {
                        d.ActividadProgramadaId,
                        d.PeriodoProgramaId,
                        p.Etiqueta,
                        p.FechaInicio,
                        p.FechaFin,
                        d.CantidadProgramada,
                        d.PorcentajeProgramado,
                        d.ImporteProgramado
                    })
                .AsEnumerable()
                .GroupBy(x => x.ActividadProgramadaId)
                .ToDictionary(
                    g => g.Key,
                    g => g.OrderBy(x => x.FechaInicio)
                          .ThenBy(x => x.PeriodoProgramaId)
                          .Select(x => new GanttPeriodSegmentDto
                          {
                              PeriodoProgramaId = x.PeriodoProgramaId,
                              EtiquetaPeriodo = x.Etiqueta ?? string.Empty,
                              FechaInicio = x.FechaInicio.Date,
                              FechaFin = x.FechaFin.Date,
                              CantidadProgramada = x.CantidadProgramada,
                              PorcentajeProgramado = x.PorcentajeProgramado,
                              ImporteProgramado = x.ImporteProgramado,
                              PorcentajeFisicoGlobal = totalCantidadProgramada == 0m ? 0m : decimal.Round((x.CantidadProgramada / totalCantidadProgramada) * 100m, 4, MidpointRounding.AwayFromZero),
                              PorcentajeFinancieroGlobal = totalImporteProgramado == 0m ? 0m : decimal.Round((x.ImporteProgramado / totalImporteProgramado) * 100m, 4, MidpointRounding.AwayFromZero)
                          })
                          .ToList());

            foreach (var actividad in actividadesOrdenadas.Where(a => !a.EsResumen))
            {
                if (!filasPorId.TryGetValue(actividad.Id, out var fila))
                    continue;

                if (segmentosPorActividad.TryGetValue(actividad.Id, out var segmentos))
                {
                    fila.SegmentosFinancieros = segmentos;
                    fila.ImporteProgramadoTotal = segmentos.Sum(x => x.ImporteProgramado);
                }
                else
                {
                    fila.SegmentosFinancieros = new List<GanttPeriodSegmentDto>();
                    fila.ImporteProgramadoTotal = 0m;
                }

                fila.TieneImporteProgramado = fila.ImporteProgramadoTotal > 0m;
            }

            for (int i = actividadesOrdenadas.Count - 1; i >= 0; i--)
            {
                var actividad = actividadesOrdenadas[i];
                if (!actividad.EsResumen)
                    continue;

                if (!filasPorId.TryGetValue(actividad.Id, out var filaResumen))
                    continue;

                var descendientes = ObtenerDescendientesDelBloque(actividadesOrdenadas, i)
                    .Where(x => !x.EsResumen)
                    .ToList();

                var segmentosResumen = descendientes
                    .SelectMany(x => segmentosPorActividad.TryGetValue(x.Id, out var segmentos) ? segmentos : Enumerable.Empty<GanttPeriodSegmentDto>())
                    .GroupBy(x => new { x.PeriodoProgramaId, x.EtiquetaPeriodo, x.FechaInicio, x.FechaFin })
                    .OrderBy(g => g.Key.FechaInicio)
                    .ThenBy(g => g.Key.PeriodoProgramaId)
                    .Select(g => new GanttPeriodSegmentDto
                    {
                        PeriodoProgramaId = g.Key.PeriodoProgramaId,
                        EtiquetaPeriodo = g.Key.EtiquetaPeriodo,
                        FechaInicio = g.Key.FechaInicio,
                        FechaFin = g.Key.FechaFin,
                        CantidadProgramada = g.Sum(x => x.CantidadProgramada),
                        PorcentajeProgramado = g.Sum(x => x.PorcentajeProgramado),
                        ImporteProgramado = g.Sum(x => x.ImporteProgramado),
                        PorcentajeFisicoGlobal = g.Sum(x => x.PorcentajeFisicoGlobal),
                        PorcentajeFinancieroGlobal = g.Sum(x => x.PorcentajeFinancieroGlobal)
                    })
                    .ToList();

                filaResumen.SegmentosFinancieros = segmentosResumen;
                filaResumen.ImporteProgramadoTotal = segmentosResumen.Sum(x => x.ImporteProgramado);
                filaResumen.TieneImporteProgramado = filaResumen.ImporteProgramadoTotal > 0m;
            }

            model.FooterPeriodos = new ProgramacionCurvaSService()
                .BuildFinancialCurve(context, programa.ProgramaObraId)
                .Select(x => new GanttFooterPeriodDto
                {
                    Etiqueta = x.Etiqueta,
                    FechaInicio = x.FechaInicio,
                    FechaFin = x.FechaFin,
                    ImportePeriodo = x.ImportePeriodo,
                    ImporteAcumulado = x.ImporteAcumulado,
                    PorcentajePeriodo = x.PorcentajePeriodo,
                    PorcentajeAcumulado = x.PorcentajeAcumulado
                })
                .ToList();

            return model;
        }

        private static void AplicarRangoYEscala(GanttRenderModel model)
        {
            var fechas = model.Filas
                .Where(x => x.Inicio.HasValue && x.Fin.HasValue)
                .SelectMany(x => new[] { x.Inicio!.Value.Date, x.Fin!.Value.Date })
                .OrderBy(x => x)
                .ToList();

            if (fechas.Count == 0)
                return;

            var inicio = AlinearInicio(fechas.First(), model.TipoPeriodo);
            var fin = AlinearFin(fechas.Last(), model.TipoPeriodo);

            model.FechaInicioRango = inicio;
            model.FechaFinRango = fin;
            model.Escala = CrearEscala(inicio, fin, model.TipoPeriodo);
        }

        private static List<ActivityGridRowDto> ObtenerDescendientesDelBloque(List<ActivityGridRowDto> ordenadas, int indiceAgrupador)
        {
            var resultado = new List<ActivityGridRowDto>();
            if (indiceAgrupador < 0 || indiceAgrupador >= ordenadas.Count)
                return resultado;

            var agrupador = ordenadas[indiceAgrupador];
            for (int i = indiceAgrupador + 1; i < ordenadas.Count; i++)
            {
                var candidata = ordenadas[i];
                if (candidata.Nivel <= agrupador.Nivel)
                    break;

                resultado.Add(candidata);
            }

            return resultado;
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

        private static DateTime AlinearInicio(DateTime fecha, TipoPeriodoPrograma tipoPeriodo)
        {
            fecha = fecha.Date;
            return tipoPeriodo switch
            {
                TipoPeriodoPrograma.Dia => fecha,
                TipoPeriodoPrograma.Semana => StartOfIsoWeek(fecha),
                TipoPeriodoPrograma.Quincena => fecha.Day <= 15 ? new DateTime(fecha.Year, fecha.Month, 1) : new DateTime(fecha.Year, fecha.Month, 16),
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
                TipoPeriodoPrograma.Semana => StartOfIsoWeek(fecha).AddDays(6),
                TipoPeriodoPrograma.Quincena => fecha.Day <= 15 ? new DateTime(fecha.Year, fecha.Month, 15) : new DateTime(fecha.Year, fecha.Month, DateTime.DaysInMonth(fecha.Year, fecha.Month)),
                TipoPeriodoPrograma.Mes => new DateTime(fecha.Year, fecha.Month, DateTime.DaysInMonth(fecha.Year, fecha.Month)),
                _ => fecha
            };
        }

        private static DateTime StartOfIsoWeek(DateTime fecha)
        {
            int diff = ((int)fecha.DayOfWeek + 6) % 7;
            return fecha.AddDays(-diff).Date;
        }

        private static DateTime Min(DateTime a, DateTime b) => a <= b ? a : b;
    }
}
