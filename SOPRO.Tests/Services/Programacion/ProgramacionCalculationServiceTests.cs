using Microsoft.VisualStudio.TestTools.UnitTesting;
using SOPRO.Application.Services;
using SOPRO.Core.Entities;
using SOPRO.Tests.TestInfrastructure;

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
}
