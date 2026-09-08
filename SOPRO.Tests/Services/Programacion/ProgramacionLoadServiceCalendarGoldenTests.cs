using Microsoft.VisualStudio.TestTools.UnitTesting;
using Sopro.Calculation.Calendar;
using SOPRO.Application.Services;
using SOPRO.Core.Entities;
using SOPRO.Data.Context;
using SOPRO.Tests.TestInfrastructure;

namespace SOPRO.Tests.Services.Programacion;

// [N7-16e] La carga de un programa ya no lleva el predicado de día hábil inline
// (CalcularDiasHabiles/EsDiaHabil): delega a WorkingCalendarCalculator con el
// mismo calendario (incluidas excepciones) que el cálculo de red e importes.
// Este golden fija el conteo convencional de la duración de un resumen flexible.

[TestClass]
public class ProgramacionLoadServiceCalendarGoldenTests
{
    [TestMethod]
    public void LoadProgramById_ResumenConExcepciones_CuentaDiasHabilesDelCalendarioPuro()
    {
        using var context = TestDbFactory.CreateContext();
        var proyecto = new Proyecto
        {
            Nombre = "Carga con excepciones",
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

        var resumen = new ActividadProgramada
        {
            ProgramaObraId = programa.Id,
            Descripcion = "Bloque",
            Unidad = "m2",
            EsResumen = true,
            Nivel = 1,
            Orden = 1
        };
        var hijo = new ActividadProgramada
        {
            ProgramaObraId = programa.Id,
            Descripcion = "Hijo",
            Unidad = "m2",
            CantidadTotal = 10m,
            PrecioUnitario = 10m,
            ImporteTotal = 100m,
            RendimientoDiario = 1m,
            FrentesTrabajo = 1,
            DuracionDiasHabiles = 10,
            Nivel = 2,
            Orden = 2,
            FechaInicioProgramada = new DateTime(2026, 1, 1),
            FechaFinProgramada = new DateTime(2026, 1, 14)
        };
        context.ActividadesProgramadas.AddRange(resumen, hijo);
        context.SaveChanges();

        // Contrato del calendario puro: 01-01..01-14 -> 10 días hábiles
        // (pierde el 06 Inhabil, gana el sábado 10 LaborableEspecial).
        var workingCalendar = new WorkingCalendar
        {
            Exceptions = new List<CalendarException>
            {
                new() { Date = new DateTime(2026, 1, 6), Kind = CalendarExceptionKind.NonWorking },
                new() { Date = new DateTime(2026, 1, 10), Kind = CalendarExceptionKind.Working }
            }
        };
        Assert.AreEqual(10, WorkingCalendarCalculator.CountWorkingDays(
            workingCalendar, new DateTime(2026, 1, 1), new DateTime(2026, 1, 14)));

        var resultado = new ProgramacionLoadService().LoadProgramById(context, programa.Id);

        Assert.IsNotNull(resultado);
        var resumenCargado = resultado.Actividades.Single(a => a.EsResumen);
        Assert.AreEqual(new DateTime(2026, 1, 1), resumenCargado.FechaInicioProgramada!.Value.Date);
        Assert.AreEqual(new DateTime(2026, 1, 14), resumenCargado.FechaFinProgramada!.Value.Date);
        Assert.AreEqual(10, resumenCargado.DuracionDiasHabiles);
        Assert.AreEqual(
            WorkingCalendarCalculator.CountWorkingDays(
                workingCalendar,
                resumenCargado.FechaInicioProgramada.Value,
                resumenCargado.FechaFinProgramada.Value),
            resumenCargado.DuracionDiasHabiles);
    }
}