using Microsoft.VisualStudio.TestTools.UnitTesting;
using SOPRO.Application.Services;
using SOPRO.Application.Services.Programacion;
using SOPRO.Core.Entities;
using SOPRO.Tests.TestInfrastructure;
using Sopro.Calculation;

namespace SOPRO.Tests.Services.Programacion;

[TestClass]
public class ProgramacionCalculationServiceTests
{
    [TestMethod]
    public void RecalculateProgram_ConFechaInicioProgramaInválida_NolanzaYSaneaALaFechaActual()
    {
        using var context = TestDbFactory.CreateContext();

        var proyecto = CrearProyecto("Proyecto fecha corrupta");
        context.Proyectos.Add(proyecto);
        context.SaveChanges();

        var programa = new ProgramaObra
        {
            ProyectoId = proyecto.Id,
            Nombre = "Programa corrupto",
            FechaInicioPrograma = DateTime.MinValue,
            Activo = true
        };
        context.ProgramasObra.Add(programa);
        context.SaveChanges();

        var actividad = new ActividadProgramada
        {
            ProgramaObraId = programa.Id,
            EsResumen = false,
            EsManual = false,
            Descripcion = "Actividad sin fechas",
            DuracionDiasHabiles = 5,
            MetodoDistribucion = MetodoDistribucionActividad.Uniforme
        };
        context.ActividadesProgramadas.Add(actividad);
        context.SaveChanges();

        var service = new ProgramacionCalculationService();

        service.RecalculateProgram(context, programa.Id);

        Assert.AreEqual(DateTime.Today.Date, programa.FechaInicioPrograma.Date);
        Assert.AreEqual(DateTime.Today.Date, actividad.FechaInicioProgramada!.Value.Date);
        Assert.IsTrue(actividad.FechaFinProgramada.HasValue);
    }

    [TestMethod]
    public void RecalculateActivity_ConFechaInicioEnMinValue_NoLanza()
    {
        using var context = TestDbFactory.CreateContext();

        var proyecto = CrearProyecto("Proyecto actividad corrupta");
        context.Proyectos.Add(proyecto);
        context.SaveChanges();

        var programa = new ProgramaObra
        {
            ProyectoId = proyecto.Id,
            Nombre = "Programa",
            FechaInicioPrograma = new DateTime(2026, 1, 1),
            Activo = true
        };
        context.ProgramasObra.Add(programa);
        context.SaveChanges();

        var actividad = new ActividadProgramada
        {
            ProgramaObraId = programa.Id,
            EsResumen = false,
            Descripcion = "Actividad corrupta",
            FechaInicioProgramada = DateTime.MinValue,
            DuracionDiasHabiles = 5,
            MetodoDistribucion = MetodoDistribucionActividad.Uniforme
        };
        context.ActividadesProgramadas.Add(actividad);
        context.SaveChanges();

        new ProgramacionCalculationService().RecalculateActivity(context, actividad.Id);
    }

    [TestMethod]
    public void SyncFromBudget_ConProgramaExistenteSinFechaInicio_NoLanzaYUsaFechaActual()
    {
        using var context = TestDbFactory.CreateContext();

        var proyecto = CrearProyecto("Proyecto sync corrupto");
        context.Proyectos.Add(proyecto);
        context.SaveChanges();

        var programa = new ProgramaObra
        {
            ProyectoId = proyecto.Id,
            Nombre = "Programa corrupto",
            FechaInicioPrograma = DateTime.MinValue,
            TipoPeriodo = TipoPeriodoPrograma.Semana,
            DuracionPeriodoDias = 7,
            Activo = true
        };
        context.ProgramasObra.Add(programa);
        context.SaveChanges();

        var concepto = new ConceptoPresupuesto
        {
            ProyectoId = proyecto.Id,
            Clave = "C-01",
            Descripcion = "Concepto activo",
            Cantidad = 100,
            PrecioUnitario = 1m,
            ImporteTotal = 100m,
            Orden = 1
        };
        context.ConceptosPresupuesto.Add(concepto);
        context.SaveChanges();

        var result = new ProgramacionSynchronizationService().SyncFromBudget(context, proyecto.Id);

        Assert.IsTrue(result.Success);
        var actividad = context.ActividadesProgramadas.Single(a => a.ProgramaObraId == programa.Id && !a.EsResumen);
        Assert.AreEqual(DateTime.Today.Date, actividad.FechaInicioProgramada!.Value.Date);
    }

    private static Proyecto CrearProyecto(string nombre) => new()
    {
        Nombre = nombre,
        Descripcion = string.Empty,
        Ubicacion = string.Empty,
        Convocante = string.Empty,
        Contratista = string.Empty,
        ApoderadoLegal = string.Empty,
        FechaInicio = new DateTime(2026, 1, 1),
        FechaTermino = new DateTime(2026, 12, 31),
        PlazoEjecucion = 365,
        DecimalesCantidad = 2,
        DecimalesImporte = 2,
        DecimalesPorcentaje = 4
    };

    [TestMethod]
    public void RecalculateActivity_BateriaParidadConFachada()
    {
        var rnd = new Random(20260827);
        for (int iter = 0; iter < 50; iter++)
        {
            using var ctx = TestDbFactory.CreateContext();
            var decCant = rnd.Next(0, 5);
            var decImp = rnd.Next(0, 5);
            var decPct = rnd.Next(0, 7);
            var proyecto = CrearProyectoConPrecisiones($"N5-17-{iter}", decCant, decImp, decPct);
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

            var cantidad = rnd.Next(1, 100000) / 1000m;
            var precio = rnd.Next(1, 100000) / 1000m;
            var actividad = new ActividadProgramada
            {
                ProgramaObraId = programa.Id,
                EsResumen = false,
                Descripcion = "Actividad paridad",
                CantidadTotal = cantidad,
                PrecioUnitario = precio,
                FechaInicioProgramada = new DateTime(2026, 1, 1),
                FechaFinProgramada = new DateTime(2026, 1, 10),
                DuracionDiasHabiles = 5,
                MetodoDistribucion = MetodoDistribucionActividad.Uniforme
            };
            ctx.ActividadesProgramadas.Add(actividad);
            ctx.SaveChanges();

            new ProgramacionCalculationService().RecalculateActivity(ctx, actividad.Id);

            var actualizada = ctx.ActividadesProgramadas.Find(actividad.Id)!;
            var esperado = new MotorCalculoSopro(proyecto).Multiplicar(cantidad, precio);
            Assert.AreEqual(esperado, actualizada.ImporteTotal, $"iter={iter} ImporteTotal");
        }
    }

    [TestMethod]
    public void RecalculateActivity_Dorado_ParidadYValoresConocidos()
    {
        using var ctx = TestDbFactory.CreateContext();
        var proyecto = CrearProyectoConPrecisiones("N5-17-dorado", 2, 2, 4);
        ctx.Proyectos.Add(proyecto);
        ctx.SaveChanges();

        var programa = new ProgramaObra
        {
            ProyectoId = proyecto.Id,
            Nombre = "Programa dorado",
            FechaInicioPrograma = new DateTime(2026, 1, 1),
            Activo = true
        };
        ctx.ProgramasObra.Add(programa);
        ctx.SaveChanges();

        var actividad = new ActividadProgramada
        {
            ProgramaObraId = programa.Id,
            EsResumen = false,
            Descripcion = "Actividad dorado",
            CantidadTotal = 10m,
            PrecioUnitario = 10m,
            FechaInicioProgramada = new DateTime(2026, 1, 1),
            FechaFinProgramada = new DateTime(2026, 1, 10),
            DuracionDiasHabiles = 5,
            MetodoDistribucion = MetodoDistribucionActividad.Uniforme
        };
        ctx.ActividadesProgramadas.Add(actividad);
        ctx.SaveChanges();

        new ProgramacionCalculationService().RecalculateActivity(ctx, actividad.Id);

        var actualizada = ctx.ActividadesProgramadas.Find(actividad.Id)!;
        Assert.AreEqual(100m, actualizada.ImporteTotal);
    }

    [TestMethod]
    public void RecalculateActivityInternal_SinProyecto_RedondeaProductoCrudo()
    {
        var actividad = CrearActividadRedondeo();
        var engine = new SoproCalculationEngine(2, 2, 4);

        ProgramacionCalculationService.RecalculateActivityInternal(
            actividad, CalendarioCache.Fallback, engine, proyectoPresente: false);

        Assert.AreEqual(0.02m, actividad.ImporteTotal);
    }

    [TestMethod]
    public void RecalculateActivityInternal_ConProyecto_RedondeaPrecioAntesDeMultiplicar()
    {
        var actividad = CrearActividadRedondeo();
        var engine = new SoproCalculationEngine(2, 2, 4);

        ProgramacionCalculationService.RecalculateActivityInternal(
            actividad, CalendarioCache.Fallback, engine, proyectoPresente: true);

        Assert.AreEqual(0.03m, actividad.ImporteTotal);
    }

    [TestMethod]
    public void RecalculateActivity_RegresionRedondeo_ConProyectoEsEquivalente()
    {
        using var context = TestDbFactory.CreateContext();
        var proyecto = CrearProyectoConPrecisiones("N5-17-regresion-actividad", 2, 2, 4);
        context.Proyectos.Add(proyecto);
        context.SaveChanges();

        var programa = new ProgramaObra
        {
            ProyectoId = proyecto.Id,
            Nombre = "Programa regresion actividad",
            FechaInicioPrograma = new DateTime(2026, 1, 1),
            Activo = true
        };
        context.ProgramasObra.Add(programa);
        context.SaveChanges();

        var actividad = new ActividadProgramada
        {
            ProgramaObraId = programa.Id,
            EsResumen = false,
            Descripcion = "Regresion actividad",
            CantidadTotal = 3m,
            PrecioUnitario = 0.005m,
            FechaInicioProgramada = new DateTime(2026, 1, 1),
            FechaFinProgramada = new DateTime(2026, 1, 10),
            DuracionDiasHabiles = 5,
            MetodoDistribucion = MetodoDistribucionActividad.Uniforme
        };
        context.ActividadesProgramadas.Add(actividad);
        context.SaveChanges();

        new ProgramacionCalculationService().RecalculateActivity(context, actividad.Id);

        var actualizada = context.ActividadesProgramadas.Find(actividad.Id)!;
        Assert.AreEqual(0.03m, actualizada.ImporteTotal);
    }

    [TestMethod]
    public void RecalculateProgram_RegresionRedondeo_ConProyectoEsEquivalente()
    {
        using var context = TestDbFactory.CreateContext();
        var proyecto = CrearProyectoConPrecisiones("N5-17-regresion-programa", 2, 2, 4);
        context.Proyectos.Add(proyecto);
        context.SaveChanges();

        var programa = new ProgramaObra
        {
            ProyectoId = proyecto.Id,
            Nombre = "Programa regresion",
            FechaInicioPrograma = new DateTime(2026, 1, 1),
            Activo = true
        };
        context.ProgramasObra.Add(programa);
        context.SaveChanges();

        var actividad = new ActividadProgramada
        {
            ProgramaObraId = programa.Id,
            EsResumen = false,
            Descripcion = "Regresion programa",
            CantidadTotal = 3m,
            PrecioUnitario = 0.005m,
            FechaInicioProgramada = new DateTime(2026, 1, 1),
            FechaFinProgramada = new DateTime(2026, 1, 10),
            DuracionDiasHabiles = 5,
            MetodoDistribucion = MetodoDistribucionActividad.Uniforme
        };
        context.ActividadesProgramadas.Add(actividad);
        context.SaveChanges();

        new ProgramacionCalculationService().RecalculateProgram(context, programa.Id);

        var actualizada = context.ActividadesProgramadas.Find(actividad.Id)!;
        Assert.AreEqual(0.03m, actualizada.ImporteTotal);
    }

    private static ActividadProgramada CrearActividadRedondeo() => new()
    {
        EsResumen = false,
        Descripcion = "Regresion redondeo",
        CantidadTotal = 3m,
        PrecioUnitario = 0.005m,
        FechaInicioProgramada = new DateTime(2026, 1, 1),
        FechaFinProgramada = new DateTime(2026, 1, 10),
        DuracionDiasHabiles = 5,
        MetodoDistribucion = MetodoDistribucionActividad.Uniforme
    };

    private static Proyecto CrearProyectoConPrecisiones(string nombre, int decCant, int decImp, int decPct) => new()
    {
        Nombre = nombre,
        Descripcion = string.Empty,
        Ubicacion = string.Empty,
        Convocante = string.Empty,
        Contratista = string.Empty,
        ApoderadoLegal = string.Empty,
        FechaInicio = new DateTime(2026, 1, 1),
        FechaTermino = new DateTime(2026, 12, 31),
        PlazoEjecucion = 365,
        DecimalesCantidad = decCant,
        DecimalesImporte = decImp,
        DecimalesPorcentaje = decPct
    };
}
