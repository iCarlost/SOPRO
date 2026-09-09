using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SOPRO.Application.Services;
using SOPRO.Core.Entities;
using SOPRO.Tests.TestInfrastructure;

namespace SOPRO.Tests.Services;

/// <summary>
/// Comportamientos del adaptador de financiamiento (N7-17c) que no capturan los
/// goldens de N7-17a: recálculo reemplazando filas y guardas que conservan filas
/// previamente persistidas (el legado retorna 0 sin tocar filas ni configuración).
/// La paridad aritmética completa queda probada por los 8 goldens de N7-17a, que
/// pasan contra el servicio delegado.
/// </summary>
[TestClass]
public class FinanciamientoCalculationServiceDelegationTests
{
    [TestMethod]
    public void Recalculo_ReemplazaFilasYActualizaConfig()
    {
        var escenario = CrearEscenario(porcentajeAnticipo: 30m, desfaseCobro: 1, baseCalculo: "Acumulable");
        var service = new FinanciamientoCalculationService();

        var primera = service.Calcular(escenario.Context, escenario.Config, escenario.Proyecto);
        var filasPrimera = Filas(escenario);
        var fechaPrimera = escenario.Config.FechaCalculo;

        System.Threading.Thread.Sleep(10);
        var segunda = service.Calcular(escenario.Context, escenario.Config, escenario.Proyecto);
        var filasSegunda = Filas(escenario);

        Assert.AreEqual(0.16737m, primera);
        Assert.AreEqual(primera, segunda, "El recálculo produce el mismo porcentaje.");
        Assert.AreEqual(3, filasSegunda.Count, "Recálculo: 2 periodos + 1 fila de desfase.");

        Assert.AreEqual(filasPrimera.Count, filasSegunda.Count);
        Assert.AreEqual(1100.00m, filasSegunda[0].Egresos);
        Assert.AreEqual(600.00m, filasSegunda[0].AnticipoRecibido);
        Assert.AreEqual(3.6822m, escenario.Config.FinanciamientoNeto, "Config mutada en cada cálculo.");
        Assert.IsNotNull(escenario.Config.FechaCalculo);
        Assert.IsTrue(
            escenario.Config.FechaCalculo >= fechaPrimera,
            "FechaCalculo se actualiza (o se conserva) en el recálculo.");
        Assert.AreEqual(0.0000m, escenario.Config.InteresesPositivos);
        Assert.AreEqual(0.16737m, escenario.Config.PorcentajeCalculado);
    }

    [TestMethod]
    public void GuardSinProgramaActivo_ConservaFilasPreviamentePersistidas()
    {
        var escenario = CrearEscenario(porcentajeAnticipo: 30m, desfaseCobro: 1, baseCalculo: "Acumulable");
        var service = new FinanciamientoCalculationService();

        service.Calcular(escenario.Context, escenario.Config, escenario.Proyecto);
        Assert.AreEqual(3, Filas(escenario).Count, "Cálculo exitoso persiste 3 filas.");

        var programa = escenario.Context.ProgramasObra.Single(p => p.ProyectoId == escenario.Proyecto.Id);
        programa.Activo = false;
        escenario.Context.SaveChanges();
        var netoAntes = escenario.Config.FinanciamientoNeto;

        var porcentaje = service.Calcular(escenario.Context, escenario.Config, escenario.Proyecto);

        Assert.AreEqual(0m, porcentaje, "Guard devuelve 0.");
        Assert.AreEqual(3, Filas(escenario).Count, "Guard NO elimina las filas previamente persistidas.");
        Assert.AreEqual(netoAntes, escenario.Config.FinanciamientoNeto, "Guard NO muta la configuración.");
    }

    [TestMethod]
    public void GuardBaseCero_ConservaFilasPreviamentePersistidasYDevuelveCero()
    {
        var escenario = CrearEscenario(porcentajeAnticipo: 30m, desfaseCobro: 1, baseCalculo: "Acumulable");
        var service = new FinanciamientoCalculationService();

        service.Calcular(escenario.Context, escenario.Config, escenario.Proyecto);
        Assert.AreEqual(3, Filas(escenario).Count);

        var concepto = escenario.Context.ConceptosPresupuesto.Single();
        concepto.Cantidad = 0m;
        concepto.ImporteTotal = 0m;
        escenario.Context.SaveChanges();

        var porcentaje = service.Calcular(escenario.Context, escenario.Config, escenario.Proyecto);

        Assert.AreEqual(0m, porcentaje, "Base no positiva devuelve 0.");
        Assert.AreEqual(3, Filas(escenario).Count, "Guard NO elimina filas previas.");
        Assert.AreEqual(3.6822m, escenario.Config.FinanciamientoNeto, "Guard NO muta la configuración.");
    }

    private static List<FilaFlujoCajaFinanciamiento> Filas(
        (SOPRO.Data.Context.SOPROContext Context, Proyecto Proyecto, ConfiguracionFinanciamiento Config) escenario)
        => escenario.Context.FilasFlujoCajaFinanciamiento
            .AsNoTracking()
            .Where(f => f.ConfiguracionFinanciamientoId == escenario.Config.Id)
            .OrderBy(f => f.NumeroPeriodo)
            .ToList();

    private static (SOPRO.Data.Context.SOPROContext Context, Proyecto Proyecto, ConfiguracionFinanciamiento Config) CrearEscenario(
        decimal porcentajeAnticipo,
        int desfaseCobro,
        string baseCalculo)
    {
        var context = TestDbFactory.CreateContext();

        var proyecto = new Proyecto
        {
            Nombre = "Proyecto financiamiento delegacion",
            Descripcion = string.Empty,
            Ubicacion = string.Empty,
            Convocante = string.Empty,
            Contratista = string.Empty,
            ApoderadoLegal = string.Empty,
            FechaInicio = new DateTime(2026, 1, 1),
            FechaTermino = new DateTime(2026, 1, 31),
            PlazoEjecucion = 31,
            PorcentajeIndirectosCentral = 10m,
            PorcentajeIndirectosCampo = 0m,
            ModoCalculoPorcentajes = "SobreCD",
            DecimalesCantidad = 2,
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

        var matriz = new Matriz
        {
            ProyectoId = proyecto.Id,
            Clave = "APU-001",
            Descripcion = "Concepto base",
            Unidad = "m2",
            Tipo = TipoMatriz.APU,
            CostoDirecto = 100m
        };
        context.Matrices.Add(matriz);
        context.SaveChanges();

        var concepto = new ConceptoPresupuesto
        {
            ProyectoId = proyecto.Id,
            Clave = "C-001",
            Descripcion = "Pavimento",
            Unidad = "m2",
            Cantidad = 20m,
            MatrizId = matriz.Id,
            CostoDirectoUnitario = 100m,
            CostoDirectoTotal = 2000m,
            PrecioUnitario = 100m,
            ImporteTotal = 2000m
        };
        context.ConceptosPresupuesto.Add(concepto);
        context.SaveChanges();

        var actividad = new ActividadProgramada
        {
            ProgramaObraId = programa.Id,
            ConceptoPresupuestoId = concepto.Id,
            Clave = concepto.Clave,
            Descripcion = concepto.Descripcion,
            Unidad = concepto.Unidad,
            CantidadTotal = 20m,
            PrecioUnitario = 100m,
            ImporteTotal = 2000m,
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
                CantidadProgramada = 10m,
                PorcentajeProgramado = 50m,
                PrecioUnitario = 100m,
                ImporteProgramado = 1000m
            },
            new DistribucionPeriodo
            {
                ActividadProgramadaId = actividad.Id,
                PeriodoProgramaId = periodo2.Id,
                CantidadProgramada = 10m,
                PorcentajeProgramado = 50m,
                PrecioUnitario = 100m,
                ImporteProgramado = 1000m
            });
        context.SaveChanges();

        var config = new ConfiguracionFinanciamiento
        {
            ProyectoId = proyecto.Id,
            TasaTIIE = 12m,
            PuntosAdicionales = 0m,
            PorcentajeAnticipo = porcentajeAnticipo,
            DesfaseCobro = desfaseCobro,
            BaseCalculo = baseCalculo
        };
        context.ConfiguracionesFinanciamiento.Add(config);
        context.SaveChanges();

        return (context, proyecto, config);
    }
}