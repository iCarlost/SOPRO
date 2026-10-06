using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Sopro.Calculation.Scheduling;
using SOPRO.Application.DTOs.Programacion;
using SOPRO.Application.Services;
using SOPRO.Application.Services.Programacion;
using SOPRO.Core.Entities;
using SOPRO.Tests.TestInfrastructure;

namespace SOPRO.Tests.Services.Programacion;

/// <summary>
/// Saneo defensivo de <see cref="TipoDependenciaActividad"/>: valores indefinidos
/// (0 o fuera de 1..4) se normalizan a FS sin alterar los datos persistidos.
/// </summary>
[TestClass]
public sealed class TipoDependenciaActividadNormalizacionTests
{
    [DataTestMethod]
    [DataRow((TipoDependenciaActividad)0, TipoDependenciaActividad.FS)]
    [DataRow((TipoDependenciaActividad)5, TipoDependenciaActividad.FS)]
    [DataRow((TipoDependenciaActividad)99, TipoDependenciaActividad.FS)]
    [DataRow((TipoDependenciaActividad)(-1), TipoDependenciaActividad.FS)]
    [DataRow(TipoDependenciaActividad.FS, TipoDependenciaActividad.FS)]
    [DataRow(TipoDependenciaActividad.SS, TipoDependenciaActividad.SS)]
    [DataRow(TipoDependenciaActividad.FF, TipoDependenciaActividad.FF)]
    [DataRow(TipoDependenciaActividad.SF, TipoDependenciaActividad.SF)]
    public void Normalizar_ValoresInvalidosCaenAFSYLosValidosSeConservan(
        TipoDependenciaActividad entrada, TipoDependenciaActividad esperado)
    {
        Assert.AreEqual(esperado, entrada.Normalizar());
    }

    [DataTestMethod]
    [DataRow((TipoDependenciaActividad)0)]
    [DataRow((TipoDependenciaActividad)5)]
    public void Adaptador_TipoInvalido_SeMapeaComoFinishToStart(TipoDependenciaActividad tipo)
    {
        var origen = new ActividadProgramada
        {
            Id = 1,
            Orden = 1,
            DuracionDiasHabiles = 2,
            FechaInicioProgramada = new DateTime(2026, 1, 5)
        };
        var destino = new ActividadProgramada
        {
            Id = 2,
            Orden = 2,
            DuracionDiasHabiles = 2,
            FechaInicioProgramada = new DateTime(2026, 1, 5)
        };
        origen.Predecesoras.Add(new DependenciaActividad
        {
            ActividadOrigenId = origen.Id,
            ActividadDestinoId = destino.Id,
            TipoDependencia = tipo,
            DesfaseDias = 0
        });

        var input = ActivityNetworkAdapter.ToNetworkInput(
            new DateTime(2026, 1, 5), new[] { origen, destino }, null);

        Assert.AreEqual(1, input.Dependencies.Count);
        Assert.AreEqual(ActivityDependencyType.FinishToStart, input.Dependencies.Single().Type);
    }

    [TestMethod]
    public void Recalculo_DependenciaInvalidaEnBd_NoLanzaSeComportaComoFSYSinPersistirElSaneo()
    {
        using var context = TestDbFactory.CreateContext();
        var proyecto = new Proyecto
        {
            Nombre = "Saneo dependencia",
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
        context.Proyectos.Add(proyecto);
        context.SaveChanges();

        var programa = new ProgramaObra
        {
            ProyectoId = proyecto.Id,
            Nombre = "Red invalida",
            FechaInicioPrograma = new DateTime(2026, 1, 5)
        };
        context.ProgramasObra.Add(programa);
        context.SaveChanges();

        var origen = CrearActividad(programa.Id, "Origen", new DateTime(2026, 1, 5), 2, 1);
        var destino = CrearActividad(programa.Id, "Destino", new DateTime(2026, 1, 5), 2, 2);
        context.ActividadesProgramadas.AddRange(origen, destino);
        context.SaveChanges();
        context.DependenciasActividad.Add(new DependenciaActividad
        {
            ActividadOrigenId = origen.Id,
            ActividadDestinoId = destino.Id,
            TipoDependencia = (TipoDependenciaActividad)0,
            DesfaseDias = 0
        });
        context.SaveChanges();

        new ProgramacionCalculationService().RecalculateProgram(context, programa.Id);

        // Semántica FS: origen 05..06, destino inicia el siguiente día hábil (07) y termina el 08.
        var actual = context.ActividadesProgramadas.Find(destino.Id)!;
        Assert.AreEqual(new DateTime(2026, 1, 7), actual.FechaInicioProgramada!.Value.Date);
        Assert.AreEqual(new DateTime(2026, 1, 8), actual.FechaFinProgramada!.Value.Date);

        // El saneo es defensivo en memoria: el valor inválido persistido no se reescribe.
        Assert.AreEqual((TipoDependenciaActividad)0, context.DependenciasActividad.First().TipoDependencia);
    }

    [TestMethod]
    public void SaveDependencies_TipoInvalido_PersisteFS()
    {
        using var context = TestDbFactory.CreateContext();
        var proyecto = new Proyecto
        {
            Nombre = "Persistencia dependencia",
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
        context.Proyectos.Add(proyecto);
        context.SaveChanges();

        var programa = new ProgramaObra
        {
            ProyectoId = proyecto.Id,
            Nombre = "Red persistida",
            FechaInicioPrograma = new DateTime(2026, 1, 5)
        };
        context.ProgramasObra.Add(programa);
        context.SaveChanges();

        var origen = CrearActividad(programa.Id, "Origen", new DateTime(2026, 1, 5), 2, 1);
        var destino = CrearActividad(programa.Id, "Destino", new DateTime(2026, 1, 5), 2, 2);
        context.ActividadesProgramadas.AddRange(origen, destino);
        context.SaveChanges();

        var service = new ProgramacionPersistenceService();
        var result = service.SaveDependencies(context, destino.Id, new List<DependencyEditDto>
        {
            new()
            {
                ActividadOrigenId = origen.Id,
                ActividadDestinoId = destino.Id,
                TipoDependencia = (TipoDependenciaActividad)0,
                DesfaseDias = 0
            }
        });

        Assert.IsTrue(result.Ok, result.Error);
        var persistida = context.DependenciasActividad.AsNoTracking().Single();
        Assert.AreEqual(TipoDependenciaActividad.FS, persistida.TipoDependencia);
    }

    private static ActividadProgramada CrearActividad(
        int programaId, string descripcion, DateTime inicio, int duracion, int orden) => new()
    {
        ProgramaObraId = programaId,
        Descripcion = descripcion,
        FechaInicioProgramada = inicio,
        DuracionDiasHabiles = duracion,
        Orden = orden,
        MetodoDistribucion = MetodoDistribucionActividad.Uniforme
    };
}
