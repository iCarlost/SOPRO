using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SOPRO.Application.DTOs.Programacion;
using SOPRO.Application.Services;
using SOPRO.Core.Entities;
using SOPRO.Data.Context;
using SOPRO.Tests.TestInfrastructure;

namespace SOPRO.Tests.Services.Programacion;

// [N5-15] Paridad de ProgramacionDistributionService contra la fachada:
// las 3 construcciones (DistributeUniform, DistributeUniformBatch,
// DistributeByPercentages) y las 24 operaciones (Multiply ×6, RoundAmount ×3,
// RoundQuantity ×6, RoundPercentage ×9) usan SoproCalculationEngine.
// El oráculo replica el flujo completo con MotorCalculoSopro en un contexto
// gemelo construido desde los MISMOS valores.

[TestClass]
public class ProgramacionDistributionServiceParityTests
{
    [TestMethod]
    public void DistributeUniform_BateriaParidadConFachada_SemillaFija()
    {
        var rnd = new Random(20260824);

        for (int iter = 0; iter < 30; iter++)
        {
            using var ctx = TestDbFactory.CreateContext();
            using var ctxRef = TestDbFactory.CreateContext();

            var v = GenerarValores(rnd);
            var (actividadId, programaId) = CrearEscenarioUniforme(ctx, v, iter);
            var (actividadIdRef, programaIdRef) = CrearEscenarioUniforme(ctxRef, v, iter);

            var service = new ProgramacionDistributionService();
            service.DistributeUniform(ctx, actividadId);
            DistributeUniformConFachada(ctxRef, actividadIdRef);

            CompararDistribuciones(ctx, ctxRef, actividadId, actividadIdRef, iter);
        }
    }

    [TestMethod]
    public void DistributeUniformBatch_BateriaParidadConFachada_SemillaFija()
    {
        var rnd = new Random(20260826);

        for (int iter = 0; iter < 30; iter++)
        {
            using var ctx = TestDbFactory.CreateContext();
            using var ctxRef = TestDbFactory.CreateContext();

            var v1 = GenerarValores(rnd);
            var v2 = GenerarValores(rnd);
            var (programaId, actividadIds) = CrearEscenarioBatch(ctx, v1, v2, iter);
            var (programaIdRef, actividadIdsRef) = CrearEscenarioBatch(ctxRef, v1, v2, iter);

            var service = new ProgramacionDistributionService();
            service.DistributeUniformBatch(ctx, programaId);
            DistributeUniformBatchConFachada(ctxRef, programaIdRef);

            for (int i = 0; i < actividadIds.Count; i++)
                CompararDistribuciones(ctx, ctxRef, actividadIds[i], actividadIdsRef[i], iter);
        }
    }

    [TestMethod]
    public void DistributeByPercentages_BateriaParidadConFachada_SemillaFija()
    {
        var rnd = new Random(20260825);

        for (int iter = 0; iter < 30; iter++)
        {
            using var ctx = TestDbFactory.CreateContext();
            using var ctxRef = TestDbFactory.CreateContext();

            var v = GenerarValoresPorcentaje(rnd);
            var (actividadId, programaId, periodoIds) = CrearEscenarioPorcentaje(ctx, v, iter);
            var (actividadIdRef, programaIdRef, periodoIdsRef) = CrearEscenarioPorcentaje(ctxRef, v, iter);

            var inputs = new List<PeriodDistributionInput>
            {
                new PeriodDistributionInput { PeriodoProgramaId = periodoIds[0], PorcentajeProgramado = v.Pct1, CantidadProgramada = 0m },
                new PeriodDistributionInput { PeriodoProgramaId = periodoIds[1], PorcentajeProgramado = v.Pct2, CantidadProgramada = 0m }
            };
            var inputsRef = new List<PeriodDistributionInput>
            {
                new PeriodDistributionInput { PeriodoProgramaId = periodoIdsRef[0], PorcentajeProgramado = v.Pct1, CantidadProgramada = 0m },
                new PeriodDistributionInput { PeriodoProgramaId = periodoIdsRef[1], PorcentajeProgramado = v.Pct2, CantidadProgramada = 0m }
            };

            var service = new ProgramacionDistributionService();
            service.DistributeByPercentages(ctx, actividadId, inputs);
            DistributeByPercentagesConFachada(ctxRef, actividadIdRef, inputsRef);

            CompararDistribuciones(ctx, ctxRef, actividadId, actividadIdRef, iter);
        }
    }

    [TestMethod]
    public void DistributeUniform_Dorado_ParidadYValoresConocidos()
    {
        using var ctx = TestDbFactory.CreateContext();
        using var ctxRef = TestDbFactory.CreateContext();

        var proyecto = CrearProyecto(ctx, 2, 2, 4);
        var proyectoRef = CrearProyecto(ctxRef, 2, 2, 4);

        var programaId = CrearProgramaConPeriodos(ctx, proyecto.Id);
        var programaIdRef = CrearProgramaConPeriodos(ctxRef, proyectoRef.Id);

        var actividadId = CrearActividad(ctx, programaId, 10m, 100m);
        var actividadIdRef = CrearActividad(ctxRef, programaIdRef, 10m, 100m);

        var service = new ProgramacionDistributionService();
        service.DistributeUniform(ctx, actividadId);
        DistributeUniformConFachada(ctxRef, actividadIdRef);

        var dists = ctx.DistribucionesPeriodo.Where(d => d.ActividadProgramadaId == actividadId).OrderBy(d => d.PeriodoProgramaId).ToList();
        var distsRef = ctxRef.DistribucionesPeriodo.Where(d => d.ActividadProgramadaId == actividadIdRef).OrderBy(d => d.PeriodoProgramaId).ToList();

        Assert.AreEqual(2, dists.Count);
        CompararDistribuciones(ctx, ctxRef, actividadId, actividadIdRef, 999);

        // Con 10 días hábiles totales (5+5), 50% cada periodo:
        // P1: cantidad 5.00, importe 500.00, porcentaje 50.00
        // P2: cantidad 5.00, importe 500.00, porcentaje 50.00 (residuo)
        Assert.AreEqual(5.00m, dists[0].CantidadProgramada);
        Assert.AreEqual(500.00m, dists[0].ImporteProgramado);
        Assert.AreEqual(50.00m, dists[0].PorcentajeProgramado);
        Assert.AreEqual(5.00m, dists[1].CantidadProgramada);
        Assert.AreEqual(500.00m, dists[1].ImporteProgramado);
    }

    // ── Escenarios ────────────────────────────────────────────────────────────

    private sealed record Valores(
        int DecCantidad, int DecImporte, int DecPorcentaje,
        decimal CantidadTotal, decimal PrecioUnitario);

    private sealed record ValoresPct(
        int DecCantidad, int DecImporte, int DecPorcentaje,
        decimal CantidadTotal, decimal PrecioUnitario,
        decimal Pct1, decimal Pct2);

    private static Valores GenerarValores(Random rnd) => new(
        rnd.Next(0, 5),
        rnd.Next(0, 5),
        rnd.Next(0, 7),
        rnd.Next(1, 10000) / 100m,
        rnd.Next(1, 100000) / 1000m);

    private static ValoresPct GenerarValoresPorcentaje(Random rnd) => new(
        rnd.Next(0, 5),
        rnd.Next(0, 5),
        rnd.Next(0, 7),
        rnd.Next(1, 10000) / 100m,
        rnd.Next(1, 100000) / 1000m,
        30m, 70m);

    private static Proyecto CrearProyecto(SOPROContext ctx, int decCant, int decImp, int decPct)
    {
        var p = new Proyecto
        {
            Nombre = "Proyecto Distribución",
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
        ctx.Proyectos.Add(p);
        ctx.SaveChanges();
        return p;
    }

    private static (int actividadId, int programaId) CrearEscenarioUniforme(SOPROContext ctx, Valores v, int iter)
    {
        var proyecto = CrearProyecto(ctx, v.DecCantidad, v.DecImporte, v.DecPorcentaje);
        var programaId = CrearProgramaConPeriodos(ctx, proyecto.Id);
        var actividadId = CrearActividad(ctx, programaId, v.CantidadTotal, v.PrecioUnitario);
        return (actividadId, programaId);
    }

    private static (int actividadId, int programaId, List<int> periodoIds) CrearEscenarioPorcentaje(SOPROContext ctx, ValoresPct v, int iter)
    {
        var proyecto = CrearProyecto(ctx, v.DecCantidad, v.DecImporte, v.DecPorcentaje);
        var programaId = CrearProgramaConPeriodos(ctx, proyecto.Id);
        var actividadId = CrearActividad(ctx, programaId, v.CantidadTotal, v.PrecioUnitario);
        var periodoIds = ctx.PeriodosPrograma.Where(p => p.ProgramaObraId == programaId).OrderBy(p => p.NumeroPeriodo).Select(p => p.Id).ToList();
        return (actividadId, programaId, periodoIds);
    }

    private static (int programaId, List<int> actividadIds) CrearEscenarioBatch(SOPROContext ctx, Valores v1, Valores v2, int iter)
    {
        var proyecto = CrearProyecto(ctx, v1.DecCantidad, v1.DecImporte, v1.DecPorcentaje);
        var programaId = CrearProgramaConPeriodos(ctx, proyecto.Id);
        var act1 = CrearActividad(ctx, programaId, v1.CantidadTotal, v1.PrecioUnitario);
        var act2 = CrearActividad(ctx, programaId, v2.CantidadTotal, v2.PrecioUnitario);
        return (programaId, new List<int> { act1, act2 });
    }

    private static int CrearProgramaConPeriodos(SOPROContext ctx, int proyectoId)
    {
        var programa = new ProgramaObra
        {
            ProyectoId = proyectoId,
            Nombre = "Programa",
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
        return programa.Id;
    }

    private static int CrearActividad(SOPROContext ctx, int programaId, decimal cantidadTotal, decimal precioUnitario)
    {
        var actividad = new ActividadProgramada
        {
            ProgramaObraId = programaId,
            Descripcion = "Actividad",
            Unidad = "m2",
            CantidadTotal = cantidadTotal,
            PrecioUnitario = precioUnitario,
            ImporteTotal = cantidadTotal * precioUnitario,
            DuracionDiasHabiles = 10,
            FechaInicioProgramada = new DateTime(2026, 1, 1),
            FechaFinProgramada = new DateTime(2026, 1, 14)
        };
        ctx.ActividadesProgramadas.Add(actividad);
        ctx.SaveChanges();
        return actividad.Id;
    }

    private static void CompararDistribuciones(SOPROContext ctx, SOPROContext ctxRef, int actividadId, int actividadIdRef, int iter)
    {
        var dists = ctx.DistribucionesPeriodo.Where(d => d.ActividadProgramadaId == actividadId).OrderBy(d => d.PeriodoProgramaId).ToList();
        var distsRef = ctxRef.DistribucionesPeriodo.Where(d => d.ActividadProgramadaId == actividadIdRef).OrderBy(d => d.PeriodoProgramaId).ToList();

        Assert.AreEqual(distsRef.Count, dists.Count, $"iter={iter} distribuciones count");
        for (int i = 0; i < dists.Count; i++)
        {
            Assert.AreEqual(distsRef[i].CantidadProgramada, dists[i].CantidadProgramada, $"iter={iter} [{i}] CantidadProgramada");
            Assert.AreEqual(distsRef[i].PorcentajeProgramado, dists[i].PorcentajeProgramado, $"iter={iter} [{i}] PorcentajeProgramado");
            Assert.AreEqual(distsRef[i].ImporteProgramado, dists[i].ImporteProgramado, $"iter={iter} [{i}] ImporteProgramado");
            Assert.AreEqual(distsRef[i].PrecioUnitario, dists[i].PrecioUnitario, $"iter={iter} [{i}] PrecioUnitario");
        }

        var act = ctx.ActividadesProgramadas.Find(actividadId)!;
        var actRef = ctxRef.ActividadesProgramadas.Find(actividadIdRef)!;
        Assert.AreEqual(actRef.CantidadProgramada, act.CantidadProgramada, $"iter={iter} actividad CantidadProgramada");
        Assert.AreEqual(actRef.ImporteProgramado, act.ImporteProgramado, $"iter={iter} actividad ImporteProgramado");
        Assert.AreEqual(actRef.AvanceProgramadoPorcentaje, act.AvanceProgramadoPorcentaje, $"iter={iter} actividad AvanceProgramadoPorcentaje");
    }

    // ── Referencia con fachada ──────────────────────────────────────────────

    private static void DistributeUniformConFachada(SOPROContext context, int actividadId)
    {
        var actividad = context.ActividadesProgramadas
            .Include(a => a.ProgramaObra).ThenInclude(p => p.Proyecto)
            .Include(a => a.ProgramaObra).ThenInclude(p => p.CalendarioLaboral).ThenInclude(c => c!.Excepciones)
            .Include(a => a.Distribuciones)
            .FirstOrDefault(a => a.Id == actividadId);
        if (actividad == null) return;

        var todosLosPeriodos = context.PeriodosPrograma.Where(p => p.ProgramaObraId == actividad.ProgramaObraId).OrderBy(p => p.NumeroPeriodo).ToList();
        if (!todosLosPeriodos.Any()) return;
        context.DistribucionesPeriodo.RemoveRange(actividad.Distribuciones);

        var proyecto = actividad.ProgramaObra?.Proyecto;
        var motor = proyecto != null ? new MotorCalculoSopro(proyecto) : new MotorCalculoSopro(4, 2, 4);

        var fechaInicio = actividad.FechaInicioProgramada?.Date;
        var fechaFin = actividad.FechaFinProgramada?.Date ?? fechaInicio;
        var periodos = todosLosPeriodos;
        if (fechaInicio.HasValue && fechaFin.HasValue)
            periodos = todosLosPeriodos.Where(p => p.FechaInicio.Date <= fechaFin.Value && p.FechaFin.Date >= fechaInicio.Value).OrderBy(p => p.NumeroPeriodo).ToList();
        if (!periodos.Any())
            periodos = fechaInicio.HasValue ? todosLosPeriodos.Where(p => p.FechaInicio.Date <= fechaInicio.Value && p.FechaFin.Date >= fechaInicio.Value).ToList() : todosLosPeriodos.Take(1).ToList();
        if (!periodos.Any()) periodos = todosLosPeriodos.Take(1).ToList();

        var tramos = BuildWorkingDayDistribution(periodos, actividad.ProgramaObra?.CalendarioLaboral, fechaInicio, fechaFin);
        if (!tramos.Any())
            tramos = periodos.Select((periodo, index) => new { Periodo = periodo, WorkingDays = index == 0 ? 1 : 0 }).Where(x => x.WorkingDays > 0).Select(x => new DistributionSlice(x.Periodo, x.WorkingDays)).ToList();
        var totalDias = tramos.Sum(x => x.WorkingDays);
        if (totalDias <= 0) totalDias = tramos.Count;
        decimal importeTotal = motor.Multiplicar(actividad.CantidadTotal, actividad.PrecioUnitario);
        decimal cantidadAcumulada = 0m, porcentajeAcumulado = 0m, importeAcumulado = 0m;
        for (int i = 0; i < tramos.Count; i++)
        {
            var tramo = tramos[i]; var esUltimo = i == tramos.Count - 1;
            decimal cantidad = esUltimo ? motor.RedondearCantidad(actividad.CantidadTotal - cantidadAcumulada) : motor.RedondearCantidad(actividad.CantidadTotal * (totalDias == 0 ? 0m : (decimal)tramo.WorkingDays / totalDias));
            cantidadAcumulada += cantidad;
            decimal porcentaje = esUltimo ? motor.RedondearPorcentaje(100m - porcentajeAcumulado) : (actividad.CantidadTotal == 0m ? motor.RedondearPorcentaje((totalDias == 0 ? 0m : (decimal)tramo.WorkingDays / totalDias) * 100m) : motor.RedondearPorcentaje((cantidad / actividad.CantidadTotal) * 100m));
            porcentajeAcumulado += porcentaje;
            decimal importe = esUltimo ? motor.RedondearImporte(importeTotal - importeAcumulado) : motor.Multiplicar(cantidad, actividad.PrecioUnitario);
            importeAcumulado += importe;
            context.DistribucionesPeriodo.Add(new DistribucionPeriodo { ActividadProgramadaId = actividad.Id, PeriodoProgramaId = tramo.Periodo.Id, CantidadProgramada = cantidad, PorcentajeProgramado = porcentaje, PrecioUnitario = actividad.PrecioUnitario, ImporteProgramado = importe });
        }
        actividad.MetodoDistribucion = MetodoDistribucionActividad.Uniforme;
        actividad.CantidadProgramada = actividad.CantidadTotal;
        actividad.ImporteProgramado = importeTotal;
        actividad.AvanceProgramadoPorcentaje = actividad.CantidadTotal == 0m ? 0m : 100m;
        actividad.FechaModificacion = DateTime.Now;
        context.SaveChanges();
    }

    private static void DistributeUniformBatchConFachada(SOPROContext context, int programaObraId)
    {
        var actividades = context.ActividadesProgramadas
            .Include(a => a.ProgramaObra).ThenInclude(p => p.Proyecto)
            .Include(a => a.ProgramaObra).ThenInclude(p => p.CalendarioLaboral).ThenInclude(c => c!.Excepciones)
            .Include(a => a.Distribuciones)
            .Where(a => a.ProgramaObraId == programaObraId && !a.EsResumen)
            .ToList();
        if (!actividades.Any()) return;
        var todosLosPeriodos = context.PeriodosPrograma.Where(p => p.ProgramaObraId == programaObraId).OrderBy(p => p.NumeroPeriodo).ToList();
        if (!todosLosPeriodos.Any()) return;
        var todasLasDistribuciones = actividades.SelectMany(a => a.Distribuciones).ToList();
        context.DistribucionesPeriodo.RemoveRange(todasLasDistribuciones);
        foreach (var actividad in actividades)
        {
            var proyecto = actividad.ProgramaObra?.Proyecto;
            var motor = proyecto != null ? new MotorCalculoSopro(proyecto) : new MotorCalculoSopro(4, 2, 4);
            var fechaInicio = actividad.FechaInicioProgramada?.Date;
            var fechaFin = actividad.FechaFinProgramada?.Date ?? fechaInicio;
            var periodos = todosLosPeriodos;
            if (fechaInicio.HasValue && fechaFin.HasValue)
                periodos = todosLosPeriodos.Where(p => p.FechaInicio.Date <= fechaFin.Value && p.FechaFin.Date >= fechaInicio.Value).OrderBy(p => p.NumeroPeriodo).ToList();
            if (!periodos.Any())
                periodos = fechaInicio.HasValue ? todosLosPeriodos.Where(p => p.FechaInicio.Date <= fechaInicio.Value && p.FechaFin.Date >= fechaInicio.Value).ToList() : todosLosPeriodos.Take(1).ToList();
            if (!periodos.Any()) periodos = todosLosPeriodos.Take(1).ToList();
            var tramos = BuildWorkingDayDistribution(periodos, actividad.ProgramaObra?.CalendarioLaboral, fechaInicio, fechaFin);
            if (!tramos.Any())
                tramos = periodos.Select((periodo, index) => new DistributionSlice(periodo, index == 0 ? 1 : 0)).Where(x => x.WorkingDays > 0).ToList();
            var totalDias = tramos.Sum(x => x.WorkingDays);
            if (totalDias <= 0) totalDias = tramos.Count;
            decimal importeTotal = motor.Multiplicar(actividad.CantidadTotal, actividad.PrecioUnitario);
            decimal cantidadAcumulada = 0m, porcentajeAcumulado = 0m, importeAcumulado = 0m;
            for (int i = 0; i < tramos.Count; i++)
            {
                var tramo = tramos[i]; var esUltimo = i == tramos.Count - 1;
                decimal cantidad = esUltimo ? motor.RedondearCantidad(actividad.CantidadTotal - cantidadAcumulada) : motor.RedondearCantidad(actividad.CantidadTotal * (totalDias == 0 ? 0m : (decimal)tramo.WorkingDays / totalDias));
                cantidadAcumulada += cantidad;
                decimal porcentaje = esUltimo ? motor.RedondearPorcentaje(100m - porcentajeAcumulado) : (actividad.CantidadTotal == 0m ? motor.RedondearPorcentaje((totalDias == 0 ? 0m : (decimal)tramo.WorkingDays / totalDias) * 100m) : motor.RedondearPorcentaje((cantidad / actividad.CantidadTotal) * 100m));
                porcentajeAcumulado += porcentaje;
                decimal importe = esUltimo ? motor.RedondearImporte(importeTotal - importeAcumulado) : motor.Multiplicar(cantidad, actividad.PrecioUnitario);
                importeAcumulado += importe;
                context.DistribucionesPeriodo.Add(new DistribucionPeriodo { ActividadProgramadaId = actividad.Id, PeriodoProgramaId = tramo.Periodo.Id, CantidadProgramada = cantidad, PorcentajeProgramado = porcentaje, PrecioUnitario = actividad.PrecioUnitario, ImporteProgramado = importe });
            }
            actividad.MetodoDistribucion = MetodoDistribucionActividad.Uniforme;
            actividad.CantidadProgramada = actividad.CantidadTotal;
            actividad.ImporteProgramado = importeTotal;
            actividad.AvanceProgramadoPorcentaje = actividad.CantidadTotal == 0m ? 0m : 100m;
            actividad.FechaModificacion = DateTime.Now;
        }
        context.SaveChanges();
    }

    private static void DistributeByPercentagesConFachada(SOPROContext context, int actividadId, List<PeriodDistributionInput> inputs)
    {
        var actividad = context.ActividadesProgramadas.Include(a => a.Distribuciones).Include(a => a.ProgramaObra).ThenInclude(p => p.Proyecto).FirstOrDefault(a => a.Id == actividadId);
        if (actividad == null) return;
        context.DistribucionesPeriodo.RemoveRange(actividad.Distribuciones);
        var proyecto = actividad.ProgramaObra?.Proyecto;
        var motor = proyecto != null ? new MotorCalculoSopro(proyecto) : new MotorCalculoSopro(4, 2, 4);
        var validInputs = inputs.Where(x => x.PorcentajeProgramado != 0 || x.CantidadProgramada != 0).ToList();
        if (!validInputs.Any())
        {
            actividad.MetodoDistribucion = MetodoDistribucionActividad.ManualPorPorcentaje;
            actividad.CantidadProgramada = 0m; actividad.ImporteProgramado = 0m; actividad.AvanceProgramadoPorcentaje = 0m; actividad.FechaModificacion = DateTime.Now; context.SaveChanges(); return;
        }
        decimal importeTotal = motor.Multiplicar(actividad.CantidadTotal, actividad.PrecioUnitario);
        decimal totalCantidad = 0m, totalImporte = 0m, porcentajeAcumulado = 0m, importeAcumulado = 0m;
        for (int i = 0; i < validInputs.Count; i++)
        {
            var input = validInputs[i]; var esUltimo = i == validInputs.Count - 1;
            decimal cantidad = input.CantidadProgramada;
            if (cantidad == 0m && actividad.CantidadTotal > 0m)
                cantidad = esUltimo ? motor.RedondearCantidad(actividad.CantidadTotal - totalCantidad) : motor.RedondearCantidad(actividad.CantidadTotal * (input.PorcentajeProgramado / 100m));
            decimal porcentaje = input.PorcentajeProgramado;
            if (actividad.CantidadTotal > 0m)
                porcentaje = esUltimo ? motor.RedondearPorcentaje(100m - porcentajeAcumulado) : motor.RedondearPorcentaje((cantidad / actividad.CantidadTotal) * 100m);
            decimal importe = esUltimo ? motor.RedondearImporte(importeTotal - importeAcumulado) : motor.Multiplicar(cantidad, actividad.PrecioUnitario);
            totalCantidad += cantidad; totalImporte += importe; porcentajeAcumulado += porcentaje; importeAcumulado += importe;
            context.DistribucionesPeriodo.Add(new DistribucionPeriodo { ActividadProgramadaId = actividadId, PeriodoProgramaId = input.PeriodoProgramaId, CantidadProgramada = cantidad, PorcentajeProgramado = porcentaje, PrecioUnitario = actividad.PrecioUnitario, ImporteProgramado = importe });
        }
        actividad.MetodoDistribucion = MetodoDistribucionActividad.ManualPorPorcentaje;
        actividad.CantidadProgramada = totalCantidad;
        actividad.ImporteProgramado = importeTotal;
        actividad.AvanceProgramadoPorcentaje = actividad.CantidadTotal == 0 ? 0m : motor.RedondearPorcentaje((actividad.CantidadProgramada / actividad.CantidadTotal) * 100m);
        actividad.FechaModificacion = DateTime.Now;
        context.SaveChanges();
    }

    private static List<DistributionSlice> BuildWorkingDayDistribution(List<PeriodoPrograma> periodos, CalendarioLaboral? calendario, DateTime? fechaInicio, DateTime? fechaFin)
    {
        var resultado = new List<DistributionSlice>();
        if (!fechaInicio.HasValue || !fechaFin.HasValue) return resultado;
        var inicioActividad = fechaInicio.Value.Date; var finActividad = fechaFin.Value.Date;
        if (finActividad < inicioActividad) return resultado;
        foreach (var periodo in periodos)
        {
            var inicio = inicioActividad > periodo.FechaInicio.Date ? inicioActividad : periodo.FechaInicio.Date;
            var fin = finActividad < periodo.FechaFin.Date ? finActividad : periodo.FechaFin.Date;
            if (fin < inicio) continue;
            var dias = CountWorkingDays(calendario, inicio, fin);
            if (dias <= 0) continue;
            resultado.Add(new DistributionSlice(periodo, dias));
        }
        return resultado;
    }

    private static int CountWorkingDays(CalendarioLaboral? calendario, DateTime inicio, DateTime fin)
    {
        if (fin < inicio) return 0; int count = 0;
        for (var current = inicio.Date; current <= fin.Date; current = current.AddDays(1))
            if (IsWorkingDay(calendario, current)) count++;
        return count;
    }

    private static bool IsWorkingDay(CalendarioLaboral? calendario, DateTime date)
    {
        if (calendario == null) return date.DayOfWeek != DayOfWeek.Saturday && date.DayOfWeek != DayOfWeek.Sunday;
        var ex = calendario.Excepciones.FirstOrDefault(x => x.Fecha.Date == date.Date);
        if (ex != null) return ex.Tipo == TipoExcepcionCalendario.LaborableEspecial;
        return date.DayOfWeek switch { DayOfWeek.Monday => calendario.Lunes, DayOfWeek.Tuesday => calendario.Martes, DayOfWeek.Wednesday => calendario.Miercoles, DayOfWeek.Thursday => calendario.Jueves, DayOfWeek.Friday => calendario.Viernes, DayOfWeek.Saturday => calendario.Sabado, DayOfWeek.Sunday => calendario.Domingo, _ => false };
    }

    private sealed record DistributionSlice(PeriodoPrograma Periodo, int WorkingDays);
}
