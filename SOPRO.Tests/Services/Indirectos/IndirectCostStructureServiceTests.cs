using Microsoft.VisualStudio.TestTools.UnitTesting;
using SOPRO.Application.Services;
using SOPRO.Core.Entities;
using SOPRO.Tests.TestInfrastructure;

namespace SOPRO.Tests.Services.Indirectos;

[TestClass]
public class IndirectCostStructureServiceTests
{
    [TestMethod]
    public void CrearEstructuraPredeterminada_DebeCrearNueveGruposYConfiguracion()
    {
        using var context = TestDbFactory.CreateContext();
        var proyecto = CreateProyecto(context);

        IndirectCostStructureService.CrearEstructuraPredeterminada(context, proyecto.Id);

        var grupos = context.GruposIndirectos
            .Where(g => g.ProyectoId == proyecto.Id)
            .OrderBy(g => g.Orden)
            .ToList();

        Assert.AreEqual(9, grupos.Count);
        Assert.AreEqual(5, grupos.Count(g => g.Tipo == TipoIndirecto.OficinaCentral));
        Assert.AreEqual(4, grupos.Count(g => g.Tipo == TipoIndirecto.Campo));
        CollectionAssert.AreEqual(
            new[] { 1, 2, 3, 4, 5, 6, 7, 8, 9 },
            grupos.Select(g => g.Orden).ToArray());
        Assert.IsTrue(grupos.All(g => g.Conceptos.Count > 0));
        Assert.IsTrue(context.ConfiguracionesIndirectos.Any(c => c.ProyectoId == proyecto.Id));
    }

    [TestMethod]
    public void CrearEstructuraPredeterminada_ConGruposExistentes_NoDuplicaLaEstructura()
    {
        using var context = TestDbFactory.CreateContext();
        var proyecto = CreateProyecto(context);

        IndirectCostStructureService.CrearEstructuraPredeterminada(context, proyecto.Id);
        IndirectCostStructureService.CrearEstructuraPredeterminada(context, proyecto.Id);

        var grupos = context.GruposIndirectos
            .Where(g => g.ProyectoId == proyecto.Id)
            .ToList();

        Assert.AreEqual(9, grupos.Count);
        Assert.AreEqual(1, context.ConfiguracionesIndirectos.Count(c => c.ProyectoId == proyecto.Id));
    }

    private static Proyecto CreateProyecto(SOPRO.Data.Context.SOPROContext context)
    {
        var proyecto = new Proyecto
        {
            Nombre = "Proyecto indirectos",
            Descripcion = string.Empty,
            Ubicacion = string.Empty,
            Convocante = string.Empty,
            Contratista = string.Empty,
            ApoderadoLegal = string.Empty,
            FechaInicio = new DateTime(2026, 1, 1),
            FechaTermino = new DateTime(2026, 1, 31),
            PlazoEjecucion = 31
        };
        context.Proyectos.Add(proyecto);
        context.SaveChanges();
        return proyecto;
    }
}
