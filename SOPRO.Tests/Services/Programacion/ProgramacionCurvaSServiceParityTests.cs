using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SOPRO.Application.Services;
using SOPRO.Core.Entities;
using SOPRO.Data.Context;
using SOPRO.Tests.TestInfrastructure;

namespace SOPRO.Tests.Services.Programacion;

// [N5-14] Paridad de ProgramacionCurvaSService contra la fachada:
// los 10 redondeos de BuildInternal (RoundAmount ×3, RoundQuantity ×3,
// RoundPercentage ×4) más las dos construcciones del motor en BuildFinancialCurve
// usan SoproCalculationEngine con las tres precisiones del proyecto.
// El oráculo replica el flujo completo con MotorCalculoSopro en un contexto
// gemelo construido desde los MISMOS valores.

[TestClass]
public class ProgramacionCurvaSServiceParityTests
{
    [TestMethod]
    public void BuildFinancialCurve_BateriaParidadConFachada_SemillaFija()
    {
        var rnd = new Random(20260823);

        for (int iter = 0; iter < 50; iter++)
        {
            using var ctx = TestDbFactory.CreateContext();
            using var ctxRef = TestDbFactory.CreateContext();

            var v = GenerarValores(rnd);
            var (proyecto, programaId) = CrearEscenario(ctx, v, iter);
            var (proyectoRef, programaIdRef) = CrearEscenario(ctxRef, v, iter);

            var service = new ProgramacionCurvaSService();
            var rows = service.BuildFinancialCurve(ctx, programaId, proyecto);
            var rowsRef = BuildFinancialCurveConFachada(ctxRef, programaIdRef, proyectoRef);

            CompararFilas(rows, rowsRef, iter);
        }
    }

    [TestMethod]
    public void BuildFinancialCurve_Dorado_ParidadYValoresConocidos()
    {
        using var ctx = TestDbFactory.CreateContext();
        using var ctxRef = TestDbFactory.CreateContext();

        var proyecto = CrearProyecto(ctx, 3, 2, 4);
        var proyectoRef = CrearProyecto(ctxRef, 3, 2, 4);

        var programaId = CrearEscenarioFijo(ctx, proyecto);
        var programaIdRef = CrearEscenarioFijo(ctxRef, proyectoRef);

        var service = new ProgramacionCurvaSService();
        var rows = service.BuildFinancialCurve(ctx, programaId, proyecto);
        var rowsRef = BuildFinancialCurveConFachada(ctxRef, programaIdRef, proyectoRef);

        Assert.AreEqual(2, rows.Count);
        CompararFilas(rows, rowsRef, 999);

        // Valores documentados con Math.Round (AwayFromZero):
        // P1: importe 333.335 → 333.34, cantidad 3.3333 → 3.333
        //     pct 333.34/1000.01*100=33.333... → 33.3340
        // P2: importe 666.665 → 666.67, cantidad 6.6667 → 6.667
        //     acumulado importe 1000.01, cantidad 10.000
        //     pct 66.6670, acumulado 100.0010
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

    [TestMethod]
    public void BuildFinancialCurve_Sobrecargas_ConProyectoExistente_Coinciden()
    {
        using var ctx = TestDbFactory.CreateContext();
        using var ctxRef = TestDbFactory.CreateContext();

        var proyecto = CrearProyecto(ctx, 2, 2, 4);
        var proyectoRef = CrearProyecto(ctxRef, 2, 2, 4);
        var programaId = CrearEscenarioFijo(ctx, proyecto);
        var programaIdRef = CrearEscenarioFijo(ctxRef, proyectoRef);

        var service = new ProgramacionCurvaSService();
        var rowsConProyecto = service.BuildFinancialCurve(ctx, programaId, proyecto);
        var rowsFallback = service.BuildFinancialCurve(ctx, programaId);
        var rowsRef = BuildFinancialCurveFallbackConFachada(ctxRef, programaIdRef);

        Assert.AreEqual(rowsConProyecto.Count, rowsFallback.Count);
        CompararFilas(rowsConProyecto, rowsFallback, 998);

        // La sobrecarga que carga el proyecto debe coincidir con la fachada
        CompararFilas(rowsFallback, rowsRef, 997);

        // Valores con 2/2/4 (proyecto de este escenario)
        Assert.AreEqual(333.34m, rowsFallback[0].ImportePeriodo);
        Assert.AreEqual(3.33m, rowsFallback[0].CantidadPeriodo);
    }

    // ── Escenario ─────────────────────────────────────────────────────────────

    private sealed record Valores(
        int DecCantidad, int DecImporte, int DecPorcentaje,
        decimal ImporteRaw1, decimal CantidadRaw1,
        decimal ImporteRaw2, decimal CantidadRaw2);

    private static Valores GenerarValores(Random rnd) => new(
        rnd.Next(0, 5),
        rnd.Next(0, 5),
        rnd.Next(0, 7),
        rnd.Next(1, 1_000_000) / 1000m,
        rnd.Next(1, 1_000_000) / 1000m,
        rnd.Next(1, 1_000_000) / 1000m,
        rnd.Next(1, 1_000_000) / 1000m);

    private static Proyecto CrearProyecto(SOPROContext ctx, int decCant, int decImp, int decPct)
    {
        var proyecto = new Proyecto
        {
            Nombre = "Proyecto Curva S",
            Descripcion = string.Empty,
            Ubicacion = string.Empty,
            Convocante = string.Empty,
            Contratista = string.Empty,
            ApoderadoLegal = string.Empty,
            FechaInicio = new DateTime(2026, 1, 1),
            FechaTermino = new DateTime(2026, 1, 31),
            PlazoEjecucion = 31,
            DecimalesCantidad = decCant,
            DecimalesImporte = decImp,
            DecimalesPorcentaje = decPct
        };
        ctx.Proyectos.Add(proyecto);
        ctx.SaveChanges();
        return proyecto;
    }

    private static (Proyecto proyecto, int programaId) CrearEscenario(SOPROContext ctx, Valores v, int iter)
    {
        var proyecto = new Proyecto
        {
            Nombre = $"Proyecto Curva S {iter}",
            Descripcion = string.Empty,
            Ubicacion = string.Empty,
            Convocante = string.Empty,
            Contratista = string.Empty,
            ApoderadoLegal = string.Empty,
            FechaInicio = new DateTime(2026, 1, 1),
            FechaTermino = new DateTime(2026, 1, 31),
            PlazoEjecucion = 31,
            DecimalesCantidad = v.DecCantidad,
            DecimalesImporte = v.DecImporte,
            DecimalesPorcentaje = v.DecPorcentaje
        };
        ctx.Proyectos.Add(proyecto);
        ctx.SaveChanges();

        var programa = new ProgramaObra
        {
            ProyectoId = proyecto.Id,
            Nombre = $"Programa {iter}",
            FechaInicioPrograma = new DateTime(2026, 1, 1),
            Activo = true
        };
        ctx.ProgramasObra.Add(programa);
        ctx.SaveChanges();

        var p1 = new PeriodoPrograma
        {
            ProgramaObraId = programa.Id,
            NumeroPeriodo = 1,
            Etiqueta = "P1",
            FechaInicio = new DateTime(2026, 1, 1),
            FechaFin = new DateTime(2026, 1, 7)
        };
        var p2 = new PeriodoPrograma
        {
            ProgramaObraId = programa.Id,
            NumeroPeriodo = 2,
            Etiqueta = "P2",
            FechaInicio = new DateTime(2026, 1, 8),
            FechaFin = new DateTime(2026, 1, 14)
        };
        ctx.PeriodosPrograma.AddRange(p1, p2);
        ctx.SaveChanges();

        var actividad = new ActividadProgramada
        {
            ProgramaObraId = programa.Id,
            Descripcion = "Actividad",
            Unidad = "m2",
            CantidadTotal = 10m,
            PrecioUnitario = 100m,
            ImporteTotal = 1000m,
            DuracionDiasHabiles = 10,
            FechaInicioProgramada = new DateTime(2026, 1, 1),
            FechaFinProgramada = new DateTime(2026, 1, 14)
        };
        ctx.ActividadesProgramadas.Add(actividad);
        ctx.SaveChanges();

        ctx.DistribucionesPeriodo.AddRange(
            new DistribucionPeriodo
            {
                ActividadProgramadaId = actividad.Id,
                PeriodoProgramaId = p1.Id,
                CantidadProgramada = v.CantidadRaw1,
                PorcentajeProgramado = 50m,
                PrecioUnitario = 100m,
                ImporteProgramado = v.ImporteRaw1
            },
            new DistribucionPeriodo
            {
                ActividadProgramadaId = actividad.Id,
                PeriodoProgramaId = p2.Id,
                CantidadProgramada = v.CantidadRaw2,
                PorcentajeProgramado = 50m,
                PrecioUnitario = 100m,
                ImporteProgramado = v.ImporteRaw2
            });
        ctx.SaveChanges();

        return (proyecto, programa.Id);
    }

    private static int CrearEscenarioFijo(SOPROContext ctx, Proyecto proyecto)
    {
        var programa = new ProgramaObra
        {
            ProyectoId = proyecto.Id,
            Nombre = "Programa base",
            FechaInicioPrograma = new DateTime(2026, 1, 1),
            Activo = true
        };
        ctx.ProgramasObra.Add(programa);
        ctx.SaveChanges();

        var p1 = new PeriodoPrograma
        {
            ProgramaObraId = programa.Id,
            NumeroPeriodo = 1,
            Etiqueta = "P1",
            FechaInicio = new DateTime(2026, 1, 1),
            FechaFin = new DateTime(2026, 1, 7)
        };
        var p2 = new PeriodoPrograma
        {
            ProgramaObraId = programa.Id,
            NumeroPeriodo = 2,
            Etiqueta = "P2",
            FechaInicio = new DateTime(2026, 1, 8),
            FechaFin = new DateTime(2026, 1, 14)
        };
        ctx.PeriodosPrograma.AddRange(p1, p2);
        ctx.SaveChanges();

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
        ctx.ActividadesProgramadas.Add(actividad);
        ctx.SaveChanges();

        ctx.DistribucionesPeriodo.AddRange(
            new DistribucionPeriodo
            {
                ActividadProgramadaId = actividad.Id,
                PeriodoProgramaId = p1.Id,
                CantidadProgramada = 3.3333m,
                PorcentajeProgramado = 33.3333m,
                PrecioUnitario = 100m,
                ImporteProgramado = 333.335m
            },
            new DistribucionPeriodo
            {
                ActividadProgramadaId = actividad.Id,
                PeriodoProgramaId = p2.Id,
                CantidadProgramada = 6.6667m,
                PorcentajeProgramado = 66.6667m,
                PrecioUnitario = 100m,
                ImporteProgramado = 666.665m
            });
        ctx.SaveChanges();

        return programa.Id;
    }

    private static void CompararFilas(
        List<SOPRO.Application.DTOs.Programacion.CurvaSRowDto> actual,
        List<SOPRO.Application.DTOs.Programacion.CurvaSRowDto> esperado,
        int iter)
    {
        Assert.AreEqual(esperado.Count, actual.Count, $"iter={iter} filas");
        for (int i = 0; i < actual.Count; i++)
        {
            Assert.AreEqual(esperado[i].ImportePeriodo, actual[i].ImportePeriodo, $"iter={iter} [{i}] ImportePeriodo");
            Assert.AreEqual(esperado[i].ImporteAcumulado, actual[i].ImporteAcumulado, $"iter={iter} [{i}] ImporteAcumulado");
            Assert.AreEqual(esperado[i].CantidadPeriodo, actual[i].CantidadPeriodo, $"iter={iter} [{i}] CantidadPeriodo");
            Assert.AreEqual(esperado[i].CantidadAcumulada, actual[i].CantidadAcumulada, $"iter={iter} [{i}] CantidadAcumulada");
            Assert.AreEqual(esperado[i].PorcentajePeriodo, actual[i].PorcentajePeriodo, $"iter={iter} [{i}] PorcentajePeriodo");
            Assert.AreEqual(esperado[i].PorcentajeAcumulado, actual[i].PorcentajeAcumulado, $"iter={iter} [{i}] PorcentajeAcumulado");
            Assert.AreEqual(esperado[i].PorcentajeFisicoPeriodo, actual[i].PorcentajeFisicoPeriodo, $"iter={iter} [{i}] PorcentajeFisicoPeriodo");
            Assert.AreEqual(esperado[i].PorcentajeFisicoAcumulado, actual[i].PorcentajeFisicoAcumulado, $"iter={iter} [{i}] PorcentajeFisicoAcumulado");
        }
    }

    // ── Referencia con fachada ────────────────────────────────────────────────

    private static List<SOPRO.Application.DTOs.Programacion.CurvaSRowDto> BuildFinancialCurveConFachada(
        SOPROContext ctx, int programaObraId, Proyecto proyecto)
    {
        var motor = new MotorCalculoSopro(proyecto);
        return BuildInternalConFachada(ctx, programaObraId, motor);
    }

    private static List<SOPRO.Application.DTOs.Programacion.CurvaSRowDto> BuildFinancialCurveFallbackConFachada(
        SOPROContext ctx, int programaObraId)
    {
        var proyecto = ctx.ProgramasObra
            .Include(p => p.Proyecto)
            .AsNoTracking()
            .Where(p => p.Id == programaObraId)
            .Select(p => p.Proyecto)
            .FirstOrDefault();

        var motor = proyecto != null
            ? new MotorCalculoSopro(proyecto)
            : new MotorCalculoSopro(2, 2, 4);

        return BuildInternalConFachada(ctx, programaObraId, motor);
    }

    private static List<SOPRO.Application.DTOs.Programacion.CurvaSRowDto> BuildInternalConFachada(
        SOPROContext ctx, int programaObraId, MotorCalculoSopro motor)
    {
        var periodos = ctx.PeriodosPrograma
            .AsNoTracking()
            .Where(p => p.ProgramaObraId == programaObraId)
            .OrderBy(p => p.NumeroPeriodo)
            .Select(p => new { p.Id, p.NumeroPeriodo, p.Etiqueta, p.FechaInicio, p.FechaFin })
            .ToList();

        if (periodos.Count == 0)
            return new List<SOPRO.Application.DTOs.Programacion.CurvaSRowDto>();

        var actividadIds = ctx.ActividadesProgramadas
            .AsNoTracking()
            .Where(a => a.ProgramaObraId == programaObraId && !a.EsResumen)
            .Select(a => a.Id)
            .ToList();

        var distribuciones = ctx.DistribucionesPeriodo
            .AsNoTracking()
            .Where(d => actividadIds.Contains(d.ActividadProgramadaId))
            .AsEnumerable()
            .ToList();

        var importesPorPeriodo = distribuciones
            .GroupBy(d => d.PeriodoProgramaId)
            .ToDictionary(g => g.Key, g => g.Sum(x => x.ImporteProgramado));

        var cantidadesPorPeriodo = distribuciones
            .GroupBy(d => d.PeriodoProgramaId)
            .ToDictionary(g => g.Key, g => g.Sum(x => x.CantidadProgramada));

        decimal total = motor.RedondearImporte(importesPorPeriodo.Values.Sum());
        decimal totalCantidad = motor.RedondearCantidad(cantidadesPorPeriodo.Values.Sum());

        decimal acumulado = 0m;
        decimal acumuladoCantidad = 0m;

        var rows = new List<SOPRO.Application.DTOs.Programacion.CurvaSRowDto>(periodos.Count);

        foreach (var p in periodos)
        {
            var importeRaw = importesPorPeriodo.TryGetValue(p.Id, out var imp) ? imp : 0m;
            var cantidadRaw = cantidadesPorPeriodo.TryGetValue(p.Id, out var cant) ? cant : 0m;

            decimal importe = motor.RedondearImporte(importeRaw);
            decimal cantidad = motor.RedondearCantidad(cantidadRaw);

            acumulado = motor.RedondearImporte(acumulado + importe);
            acumuladoCantidad = motor.RedondearCantidad(acumuladoCantidad + cantidad);

            decimal pctPeriodo = total == 0m ? 0m : motor.RedondearPorcentaje(importe / total * 100m);
            decimal pctAcum = total == 0m ? 0m : motor.RedondearPorcentaje(acumulado / total * 100m);
            decimal pctFisPeriodo = totalCantidad == 0m ? 0m : motor.RedondearPorcentaje(cantidad / totalCantidad * 100m);
            decimal pctFisAcum = totalCantidad == 0m ? 0m : motor.RedondearPorcentaje(acumuladoCantidad / totalCantidad * 100m);

            rows.Add(new SOPRO.Application.DTOs.Programacion.CurvaSRowDto
            {
                NumeroPeriodo = p.NumeroPeriodo,
                Etiqueta = p.Etiqueta,
                FechaInicio = p.FechaInicio,
                FechaFin = p.FechaFin,
                CantidadPeriodo = cantidad,
                CantidadAcumulada = acumuladoCantidad,
                ImportePeriodo = importe,
                ImporteAcumulado = acumulado,
                PorcentajePeriodo = pctPeriodo,
                PorcentajeAcumulado = pctAcum,
                PorcentajeFisicoPeriodo = pctFisPeriodo,
                PorcentajeFisicoAcumulado = pctFisAcum
            });
        }

        return rows;
    }
}
