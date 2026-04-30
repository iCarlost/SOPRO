using Microsoft.VisualStudio.TestTools.UnitTesting;
using SOPRO.Application.Services;
using SOPRO.Core.Entities;
using SOPRO.Tests.TestInfrastructure;

namespace SOPRO.Tests.Services.Programacion;

[TestClass]
public class ProgramacionCalculationServiceTests
{
    [TestMethod]
    public void CalculateFinishDate_DebeRespetarCalendarioLaboralSinSabadoNiDomingo()
    {
        using var context = TestDbFactory.CreateContext();

        var proyecto = CrearProyecto("Proyecto calendario");
        context.Proyectos.Add(proyecto);
        context.SaveChanges();

        var calendario = new CalendarioLaboral
        {
            ProyectoId = proyecto.Id,
            Nombre = "Lunes a viernes",
            Lunes = true,
            Martes = true,
            Miercoles = true,
            Jueves = true,
            Viernes = true,
            Sabado = false,
            Domingo = false,
            Activo = true
        };
        context.CalendariosLaborales.Add(calendario);
        context.SaveChanges();

        var programa = new ProgramaObra
        {
            ProyectoId = proyecto.Id,
            CalendarioLaboralId = calendario.Id,
            FechaInicioPrograma = new DateTime(2026, 1, 2), // viernes
            Nombre = "Programa calendario",
            Activo = true
        };
        context.ProgramasObra.Add(programa);
        context.SaveChanges();

        var service = new ProgramacionCalculationService();

        var fin = service.CalculateFinishDate(context, programa.Id, new DateTime(2026, 1, 2), 3);
        var dias = service.CalculateBusinessDaysInclusive(context, programa.Id, new DateTime(2026, 1, 2), fin);

        Assert.AreEqual(new DateTime(2026, 1, 6), fin);
        Assert.AreEqual(3, dias);
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
