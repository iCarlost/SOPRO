using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SOPRO.Application.Services;
using SOPRO.Core.Entities;
using SOPRO.Tests.TestInfrastructure;

namespace SOPRO.Tests.Services.Programacion;

[TestClass]
public class ProgramacionCurvaSServiceTests
{
    [TestMethod]
    public void BuildFinancialCurve_DebeAcumularImportesCantidadesYPorcentajesConPrecisionDelProyecto()
    {
        using var context = TestDbFactory.CreateContext();

        var proyecto = new Proyecto
        {
            Nombre = "Proyecto prueba Curva S",
            Descripcion = string.Empty,
            Ubicacion = string.Empty,
            Convocante = string.Empty,
            Contratista = string.Empty,
            ApoderadoLegal = string.Empty,
            FechaInicio = new DateTime(2026, 1, 1),
            FechaTermino = new DateTime(2026, 1, 31),
            PlazoEjecucion = 31,
            DecimalesCantidad = 3,
            DecimalesImporte = 2,
            DecimalesPorcentaje = 4
        };
        context.Proyectos.Add(proyecto);
        context.SaveChanges();

        var programa = new ProgramaObra
        {
            ProyectoId = proyecto.Id,
            Nombre = "Programa base",
            FechaInicioPrograma = new DateTime(2026, 1, 1),
            Activo = true
        };
        context.ProgramasObra.Add(programa);
        context.SaveChanges();

        var periodo1 = new PeriodoPrograma
        {
            ProgramaObraId = programa.Id,
            NumeroPeriodo = 1,
            Etiqueta = "P1",
            FechaInicio = new DateTime(2026, 1, 1),
            FechaFin = new DateTime(2026, 1, 7)
        };
        var periodo2 = new PeriodoPrograma
        {
            ProgramaObraId = programa.Id,
            NumeroPeriodo = 2,
            Etiqueta = "P2",
            FechaInicio = new DateTime(2026, 1, 8),
            FechaFin = new DateTime(2026, 1, 14)
        };
        context.PeriodosPrograma.AddRange(periodo1, periodo2);
        context.SaveChanges();

        var actividad = new ActividadProgramada
        {
            ProgramaObraId = programa.Id,
            Descripcion = "Pavimento hidráulico",
            Unidad = "m2",
            CantidadTotal = 10m,
            PrecioUnitario = 100m,
            ImporteTotal = 1000m,
            DuracionDiasHabiles = 10,
            FechaInicioProgramada = new DateTime(2026, 1, 1),
            FechaFinProgramada = new DateTime(2026, 1, 14)
        };
        context.ActividadesProgramadas.Add(actividad);
        context.SaveChanges();

        context.DistribucionesPeriodo.AddRange(
            new DistribucionPeriodo
            {
                ActividadProgramadaId = actividad.Id,
                PeriodoProgramaId = periodo1.Id,
                CantidadProgramada = 3.3333m,
                PorcentajeProgramado = 33.3333m,
                PrecioUnitario = 100m,
                ImporteProgramado = 333.335m
            },
            new DistribucionPeriodo
            {
                ActividadProgramadaId = actividad.Id,
                PeriodoProgramaId = periodo2.Id,
                CantidadProgramada = 6.6667m,
                PorcentajeProgramado = 66.6667m,
                PrecioUnitario = 100m,
                ImporteProgramado = 666.665m
            });
        context.SaveChanges();

        var service = new ProgramacionCurvaSService();

        var rows = service.BuildFinancialCurve(context, programa.Id, proyecto);

        Assert.AreEqual(2, rows.Count);

        Assert.AreEqual(333.34m, rows[0].ImportePeriodo);
        Assert.AreEqual(333.34m, rows[0].ImporteAcumulado);
        Assert.AreEqual(3.333m, rows[0].CantidadPeriodo);
        Assert.AreEqual(3.333m, rows[0].CantidadAcumulada);
        Assert.AreEqual(33.3340m, rows[0].PorcentajePeriodo);
        Assert.AreEqual(33.3340m, rows[0].PorcentajeAcumulado);

        Assert.AreEqual(666.67m, rows[1].ImportePeriodo);
        Assert.AreEqual(1000.01m, rows[1].ImporteAcumulado);
        Assert.AreEqual(6.667m, rows[1].CantidadPeriodo);
        Assert.AreEqual(10.000m, rows[1].CantidadAcumulada);
        Assert.AreEqual(66.6670m, rows[1].PorcentajePeriodo);
        Assert.AreEqual(100.0010m, rows[1].PorcentajeAcumulado);
    }
}
