using Microsoft.VisualStudio.TestTools.UnitTesting;
using SOPRO.Application.Services;
using SOPRO.Core.Entities;
using SOPRO.Tests.TestInfrastructure;

namespace SOPRO.Tests.Services.Programacion;

// Hallazgo Critic #2: DistributeUniformBatch construía CalendarioCache sin propagar
// el TimeProvider inyectado. Con fechas extremas (MinValue/MaxValue) el constructor
// delega en CalendarioCache.SanitizarFecha, que caía a TimeProvider.System: el
// resultado dependía del reloj real y no del reloj inyectado.
//
// El escenario fija el reloj en 2031-07-14 (lunes) y usa una actividad con fechas
// extremas; los periodos viven en 2031-07. Si el servicio no propaga el reloj, la
// caché se centra en la fecha real del sistema (muy lejos de 2031) y los periodos
// quedan fuera de rango: la distribución resultante deja de coincidir con la
// esperada determinista de 5/3 días hábiles.
[TestClass]
public sealed class ProgramacionDistributionServiceTimeProviderTests
{
    [TestMethod]
    public void DistributeUniformBatch_FechasExtremas_UsaElRelojInyectadoParaLaCache()
    {
        var reloj = new RelojFijo(new DateTimeOffset(2031, 7, 14, 12, 0, 0, TimeSpan.Zero));

        using var context = TestDbFactory.CreateContext();

        var proyecto = new Proyecto
        {
            Nombre = "Distribución reloj inyectado",
            Descripcion = string.Empty,
            Ubicacion = string.Empty,
            Convocante = string.Empty,
            Contratista = string.Empty,
            ApoderadoLegal = string.Empty,
            FechaInicio = new DateTime(2031, 1, 1),
            FechaTermino = new DateTime(2031, 12, 31),
            PlazoEjecucion = 365,
            DecimalesCantidad = 2,
            DecimalesImporte = 2,
            DecimalesPorcentaje = 2
        };
        context.Proyectos.Add(proyecto);
        context.SaveChanges();

        var programa = new ProgramaObra
        {
            ProyectoId = proyecto.Id,
            Nombre = "Programa",
            FechaInicioPrograma = new DateTime(2031, 7, 14),
            Activo = true
        };
        context.ProgramasObra.Add(programa);
        context.SaveChanges();

        // P1: lunes 14 → domingo 20 = 5 días hábiles. P2: lunes 21 → miércoles 23 = 3 días hábiles.
        context.PeriodosPrograma.AddRange(
            new PeriodoPrograma
            {
                ProgramaObraId = programa.Id,
                NumeroPeriodo = 1,
                Etiqueta = "P1",
                FechaInicio = new DateTime(2031, 7, 14),
                FechaFin = new DateTime(2031, 7, 20)
            },
            new PeriodoPrograma
            {
                ProgramaObraId = programa.Id,
                NumeroPeriodo = 2,
                Etiqueta = "P2",
                FechaInicio = new DateTime(2031, 7, 21),
                FechaFin = new DateTime(2031, 7, 23)
            });
        context.SaveChanges();

        // Fechas extremas: fuerzan la normalización a través del TimeProvider inyectado.
        var actividad = new ActividadProgramada
        {
            ProgramaObraId = programa.Id,
            Descripcion = "Actividad con fechas extremas",
            Unidad = "m2",
            CantidadTotal = 10m,
            PrecioUnitario = 10m,
            ImporteTotal = 100m,
            DuracionDiasHabiles = 1,
            FechaInicioProgramada = DateTime.MinValue,
            FechaFinProgramada = DateTime.MaxValue
        };
        context.ActividadesProgramadas.Add(actividad);
        context.SaveChanges();

        new ProgramacionDistributionService(reloj).DistributeUniformBatch(context, programa.Id);

        var distribuciones = context.DistribucionesPeriodo
            .Where(d => d.ActividadProgramadaId == actividad.Id)
            .OrderBy(d => d.PeriodoProgramaId)
            .ToList();

        Assert.AreEqual(2, distribuciones.Count);

        // 5/8 y 3/8 sobre cantidad total 10 → 6.25 / 3.75; importes 62.50 / 37.50.
        Assert.AreEqual(6.25m, distribuciones[0].CantidadProgramada);
        Assert.AreEqual(62.50m, distribuciones[0].PorcentajeProgramado);
        Assert.AreEqual(62.50m, distribuciones[0].ImporteProgramado);

        Assert.AreEqual(3.75m, distribuciones[1].CantidadProgramada);
        Assert.AreEqual(37.50m, distribuciones[1].PorcentajeProgramado);
        Assert.AreEqual(37.50m, distribuciones[1].ImporteProgramado);

        Assert.AreEqual(10m, distribuciones.Sum(d => d.CantidadProgramada));
        Assert.AreEqual(100m, distribuciones.Sum(d => d.ImporteProgramado));

        var actualizada = context.ActividadesProgramadas.Find(actividad.Id)!;
        Assert.AreEqual(10m, actualizada.CantidadProgramada);
        Assert.AreEqual(100m, actualizada.ImporteProgramado);
    }

    private sealed class RelojFijo(DateTimeOffset ahora) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => ahora;
        public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;
    }
}
