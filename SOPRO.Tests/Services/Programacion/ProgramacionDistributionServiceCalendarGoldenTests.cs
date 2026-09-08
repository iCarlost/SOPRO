using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Sopro.Calculation.Calendar;
using SOPRO.Application.Services;
using SOPRO.Core.Entities;
using SOPRO.Data.Context;
using SOPRO.Tests.TestInfrastructure;

namespace SOPRO.Tests.Services.Programacion;

// [N7-16e] La distribución uniforme de una actividad ya no lleva el predicado de
// día hábil inline: delega a WorkingCalendarCalculator vía WorkingCalendarAdapter.
// Este golden ejercita el caso que las baterías de paridad no cubren (ambas usan
// calendario nulo por defecto): un calendario con excepciones (Inhabil y
// LaborableEspecial) que cambia la proporción de días hábiles por periodo.

[TestClass]
public class ProgramacionDistributionServiceCalendarGoldenTests
{
    [TestMethod]
    public void DistributeUniform_ConExcepcionesDeCalendario_SplitPorDiasHabilesReales()
    {
        using var context = TestDbFactory.CreateContext();
        var proyecto = new Proyecto
        {
            Nombre = "Distribución con excepciones",
            Descripcion = string.Empty,
            Ubicacion = string.Empty,
            Convocante = string.Empty,
            Contratista = string.Empty,
            ApoderadoLegal = string.Empty,
            FechaInicio = new DateTime(2026, 1, 1),
            FechaTermino = new DateTime(2026, 1, 31),
            PlazoEjecucion = 31,
            DecimalesCantidad = 2,
            DecimalesImporte = 2,
            DecimalesPorcentaje = 2
        };
        context.Proyectos.Add(proyecto);
        context.SaveChanges();

        var calendario = new CalendarioLaboral { ProyectoId = proyecto.Id };
        context.CalendariosLaborales.Add(calendario);
        context.SaveChanges();
        context.ExcepcionesCalendario.AddRange(
            new ExcepcionCalendario
            {
                CalendarioLaboralId = calendario.Id,
                Fecha = new DateTime(2026, 1, 6),
                Descripcion = "Inhábil martes",
                Tipo = TipoExcepcionCalendario.Inhabil
            },
            new ExcepcionCalendario
            {
                CalendarioLaboralId = calendario.Id,
                Fecha = new DateTime(2026, 1, 10),
                Descripcion = "Sábado laborable especial",
                Tipo = TipoExcepcionCalendario.LaborableEspecial
            });
        context.SaveChanges();

        var programa = new ProgramaObra
        {
            ProyectoId = proyecto.Id,
            Nombre = "Programa",
            FechaInicioPrograma = new DateTime(2026, 1, 1),
            CalendarioLaboralId = calendario.Id,
            Activo = true
        };
        context.ProgramasObra.Add(programa);
        context.SaveChanges();

        context.PeriodosPrograma.AddRange(
            new PeriodoPrograma
            {
                ProgramaObraId = programa.Id,
                NumeroPeriodo = 1,
                Etiqueta = "P1",
                FechaInicio = new DateTime(2026, 1, 1),
                FechaFin = new DateTime(2026, 1, 7)
            },
            new PeriodoPrograma
            {
                ProgramaObraId = programa.Id,
                NumeroPeriodo = 2,
                Etiqueta = "P2",
                FechaInicio = new DateTime(2026, 1, 8),
                FechaFin = new DateTime(2026, 1, 14)
            });
        context.SaveChanges();

        var actividad = new ActividadProgramada
        {
            ProgramaObraId = programa.Id,
            Descripcion = "Actividad",
            Unidad = "m2",
            CantidadTotal = 10m,
            PrecioUnitario = 10m,
            ImporteTotal = 100m,
            DuracionDiasHabiles = 10,
            FechaInicioProgramada = new DateTime(2026, 1, 1),
            FechaFinProgramada = new DateTime(2026, 1, 14)
        };
        context.ActividadesProgramadas.Add(actividad);
        context.SaveChanges();

        // Contrato del calendario puro (referencia independiente del servicio):
        // P1 (01-01..01-07) pierde el 06 (Inhabil): 4 días hábiles.
        // P2 (01-08..01-14) gana el sábado 10 (LaborableEspecial): 6 días hábiles.
        var workingCalendar = new WorkingCalendar
        {
            Exceptions = new List<CalendarException>
            {
                new() { Date = new DateTime(2026, 1, 6), Kind = CalendarExceptionKind.NonWorking },
                new() { Date = new DateTime(2026, 1, 10), Kind = CalendarExceptionKind.Working }
            }
        };
        Assert.AreEqual(4, WorkingCalendarCalculator.CountWorkingDays(workingCalendar, new DateTime(2026, 1, 1), new DateTime(2026, 1, 7)));
        Assert.AreEqual(6, WorkingCalendarCalculator.CountWorkingDays(workingCalendar, new DateTime(2026, 1, 8), new DateTime(2026, 1, 14)));

        new ProgramacionDistributionService().DistributeUniform(context, actividad.Id);

        var distribuciones = context.DistribucionesPeriodo
            .Where(d => d.ActividadProgramadaId == actividad.Id)
            .OrderBy(d => d.PeriodoProgramaId)
            .ToList();

        Assert.AreEqual(2, distribuciones.Count);
        Assert.AreEqual(4.00m, distribuciones[0].CantidadProgramada);
        Assert.AreEqual(40.00m, distribuciones[0].ImporteProgramado);
        Assert.AreEqual(40.00m, distribuciones[0].PorcentajeProgramado);
        Assert.AreEqual(6.00m, distribuciones[1].CantidadProgramada);
        Assert.AreEqual(60.00m, distribuciones[1].ImporteProgramado);
        Assert.AreEqual(60.00m, distribuciones[1].PorcentajeProgramado);

        var actualizada = context.ActividadesProgramadas.Find(actividad.Id)!;
        Assert.AreEqual(10m, actualizada.CantidadProgramada);
        Assert.AreEqual(100m, actualizada.ImporteProgramado);
        Assert.AreEqual(100m, actualizada.AvanceProgramadoPorcentaje);
    }
}