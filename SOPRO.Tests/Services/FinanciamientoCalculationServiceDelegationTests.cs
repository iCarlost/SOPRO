using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SOPRO.Application.Services;
using SOPRO.Core.Entities;
using SOPRO.Data.Context;
using SOPRO.Tests.TestInfrastructure;

namespace SOPRO.Tests.Services;

/// <summary>
/// Comportamientos del adaptador de financiamiento (N7-17c) que no capturan los
/// goldens de N7-17a: recÃ¡lculo reemplazando filas, guardas que conservan filas
/// previamente persistidas sin mutar configuraciÃ³n, y preservaciÃ³n de la hora de
/// las fechas de los perÃ­odos base (el legado persistÃ­a los DateTime originales).
/// La paridad aritmÃ©tica completa queda probada por los 8 goldens de N7-17a, que
/// pasan contra el servicio delegado.
/// </summary>
[TestClass]
public class FinanciamientoCalculationServiceDelegationTests
{
    [TestMethod]
    public void Recalculo_ReemplazaFilasYActualizaConfig()
    {
        var e = CrearEscenario(porcentajeAnticipo: 30m, desfaseCobro: 1, baseCalculo: "Acumulable");
        var service = new FinanciamientoCalculationService();

        var primera = service.Calcular(e.Context, e.Config, e.Proyecto);
        var primeraFilas = Filas(e, out var primeraIds);
        var primeraFecha = e.Config.FechaCalculo;

        // Entradas distintas entre llamadas: sin anticipo el flujo cambia por completo.
        e.Config.PorcentajeAnticipo = 0m;
        e.Context.SaveChanges();

        var segunda = service.Calcular(e.Context, e.Config, e.Proyecto);
        var segundaFilas = Filas(e, out var segundaIds);

        Assert.AreEqual(0.16737m, primera, "CÃ¡lculo base con anticipo 30%.");
        Assert.AreNotEqual(primera, segunda, "Anticipo distinto produce resultado distinto.");
        Assert.AreEqual(primeraFilas.Count, segundaFilas.Count, "3 filas en ambos cÃ¡lculos (2 base + 1 desfase).");
        Assert.AreEqual(3, segundaFilas.Count);
        Assert.IsTrue(
            primeraIds.All(id => !segundaIds.Contains(id)),
            "El recÃ¡lculo reemplaza las filas previstas (Ids nuevos), no las reutiliza.");
        Assert.IsTrue(
            FilasCompletas(e).All(f => f.AnticipoRecibido == 0m),
            "Sin anticipo: todas las filas persistidas con anticipo 0.");
        Assert.AreEqual(segunda, e.Config.PorcentajeCalculado);
        Assert.IsNotNull(e.Config.FechaCalculo);
        Assert.IsTrue(
            e.Config.FechaCalculo >= primeraFecha,
            "FechaCalculo se actualiza en el recÃ¡lculo.");
    }

    [TestMethod]
    public void GuardSinProgramaActivo_ConservaFilasPreviamentePersistidasYConfiguracion()
    {
        var e = CrearEscenario(porcentajeAnticipo: 30m, desfaseCobro: 1, baseCalculo: "Acumulable");
        var service = new FinanciamientoCalculationService();

        service.Calcular(e.Context, e.Config, e.Proyecto);
        var filasAntes = Filas(e, out _);
        var configAntes = ClaveConfig(e);

        var programa = e.Context.ProgramasObra.Single(p => p.ProyectoId == e.Proyecto.Id);
        programa.Activo = false;
        e.Context.SaveChanges();

        var porcentaje = service.Calcular(e.Context, e.Config, e.Proyecto);

        Assert.AreEqual(0m, porcentaje, "Guard devuelve 0.");
        AssertFilasIguales(filasAntes, Filas(e, out _), "programa inactivo");
        Assert.AreEqual(configAntes, ClaveConfig(e), "Guard NO muta la configuraciÃ³n.");
    }

    [TestMethod]
    public void GuardBaseNoPositiva_ConservaFilasPreviamentePersistidasYConfiguracion()
    {
        var e = CrearEscenario(porcentajeAnticipo: 30m, desfaseCobro: 1, baseCalculo: "Acumulable");
        var service = new FinanciamientoCalculationService();

        service.Calcular(e.Context, e.Config, e.Proyecto);
        var filasAntes = Filas(e, out _);
        var configAntes = ClaveConfig(e);

        var concepto = e.Context.ConceptosPresupuesto.Single();
        concepto.Cantidad = 0m;
        concepto.ImporteTotal = 0m;
        e.Context.SaveChanges();

        var porcentaje = service.Calcular(e.Context, e.Config, e.Proyecto);

        Assert.AreEqual(0m, porcentaje, "Base no positiva devuelve 0.");
        AssertFilasIguales(filasAntes, Filas(e, out _), "base no positiva");
        Assert.AreEqual(configAntes, ClaveConfig(e), "Guard NO muta la configuraciÃ³n.");
    }

    [TestMethod]
    public void GuardProgramaActivoSinPeriodos_ConservaFilasPreviamentePersistidasYConfiguracion()
    {
        var e = CrearEscenario(porcentajeAnticipo: 30m, desfaseCobro: 1, baseCalculo: "Acumulable");
        var service = new FinanciamientoCalculationService();

        service.Calcular(e.Context, e.Config, e.Proyecto);
        var filasAntes = Filas(e, out _);
        var configAntes = ClaveConfig(e);

        e.Context.DistribucionesPeriodo.RemoveRange(
            e.Context.DistribucionesPeriodo.Where(d => d.ActividadProgramada.ProgramaObraId == e.Programa.Id));
        e.Context.PeriodosPrograma.RemoveRange(
            e.Context.PeriodosPrograma.Where(p => p.ProgramaObraId == e.Programa.Id));
        e.Context.SaveChanges();

        var porcentaje = service.Calcular(e.Context, e.Config, e.Proyecto);

        Assert.AreEqual(0m, porcentaje, "Programa activo sin perÃ­odos devuelve 0.");
        AssertFilasIguales(filasAntes, Filas(e, out _), "programa sin perÃ­odos");
        Assert.AreEqual(configAntes, ClaveConfig(e), "Guard NO muta la configuraciÃ³n.");
    }

    [TestMethod]
    public void FilasBase_PreservanHoraDeLaFecha_AlPersistir()
    {
        var e = CrearEscenario(porcentajeAnticipo: 30m, desfaseCobro: 1, baseCalculo: "Acumulable");

        var periodos = e.Context.PeriodosPrograma
            .Where(p => p.ProgramaObraId == e.Programa.Id)
            .OrderBy(p => p.NumeroPeriodo)
            .ToList();
        periodos[0].FechaInicio = new DateTime(2026, 1, 1, 8, 30, 0);
        periodos[0].FechaFin = new DateTime(2026, 1, 7, 18, 0, 0);
        periodos[1].FechaInicio = new DateTime(2026, 1, 8, 9, 15, 0);
        periodos[1].FechaFin = new DateTime(2026, 1, 14, 17, 30, 0);
        e.Context.SaveChanges();

        var service = new FinanciamientoCalculationService();
        var porcentaje = service.Calcular(e.Context, e.Config, e.Proyecto);

        Assert.AreNotEqual(0m, porcentaje, "El cÃ¡lculo no debe caer en guardas.");
        var filas = FilasCompletas(e);
        Assert.AreEqual(3, filas.Count);
        Assert.AreEqual(new DateTime(2026, 1, 1, 8, 30, 0), filas[0].FechaInicio);
        Assert.AreEqual(new DateTime(2026, 1, 7, 18, 0, 0), filas[0].FechaFin);
        Assert.AreEqual(new DateTime(2026, 1, 8, 9, 15, 0), filas[1].FechaInicio);
        Assert.AreEqual(new DateTime(2026, 1, 14, 17, 30, 0), filas[1].FechaFin);
        // La fila de desfase conserva el criterio legacy: arranca a medianoche.
        Assert.AreEqual(new DateTime(2026, 1, 15), filas[2].FechaInicio);
        Assert.AreEqual(new DateTime(2026, 1, 21), filas[2].FechaFin);
    }

    private static void AssertFilasIguales(List<(int Id, string Clave)> antes, List<(int Id, string Clave)> despues, string contexto)
    {
        Assert.AreEqual(antes.Count, despues.Count, $"[{contexto}] Mismo nÃºmero de filas.");
        Assert.IsTrue(
            antes.Select(f => f.Clave).SequenceEqual(despues.Select(f => f.Clave)),
            $"[{contexto}] El contenido completo de las filas se conserva.");
    }

    private static List<FilaFlujoCajaFinanciamiento> FilasCompletas(Escenario e)
    {
        using var fresh = new SOPROContext(e.DbPath);
        return fresh.FilasFlujoCajaFinanciamiento
            .AsNoTracking()
            .Where(f => f.ConfiguracionFinanciamientoId == e.Config.Id)
            .OrderBy(f => f.NumeroPeriodo)
            .ToList();
    }

    private static List<(int Id, string Clave)> Filas(Escenario e, out List<int> ids)
    {
        using var fresh = new SOPROContext(e.DbPath);
        var lista = fresh.FilasFlujoCajaFinanciamiento
            .AsNoTracking()
            .Where(f => f.ConfiguracionFinanciamientoId == e.Config.Id)
            .OrderBy(f => f.NumeroPeriodo)
            .AsEnumerable()
            .Select(f => (f.Id, Clave(f)))
            .ToList();
        ids = lista.Select(f => f.Id).ToList();
        return lista;
    }

    private static string Clave(FilaFlujoCajaFinanciamiento f) =>
        $"{f.NumeroPeriodo}|{f.Etiqueta}|{f.FechaInicio:O}|{f.FechaFin:O}|{f.DiasPeriodo}|{f.Egresos}|" +
        $"{f.AnticipoRecibido}|{f.EstimacionCobrada}|{f.AmortizacionAnticipo}|{f.FlujoNeto}|{f.SaldoAcumulado}|{f.InteresPeriodo}";

    private static string ClaveConfig(Escenario e)
    {
        using var fresh = new SOPROContext(e.DbPath);
        var c = fresh.ConfiguracionesFinanciamiento.Single(x => x.Id == e.Config.Id);
        return $"{c.InteresesNegativos}|{c.InteresesPositivos}|{c.FinanciamientoNeto}|{c.PorcentajeCalculado}|" +
               (c.FechaCalculo.HasValue ? c.FechaCalculo.Value.ToString("O") : "null");
    }

    private static Escenario CrearEscenario(decimal porcentajeAnticipo, int desfaseCobro, string baseCalculo)
    {
        var dbPath = TestDbFactory.CreateTempDbPath();
        var context = TestDbFactory.CreateContextAt(dbPath);

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

        return new Escenario
        {
            DbPath = dbPath,
            Context = context,
            Proyecto = proyecto,
            Config = config,
            Programa = programa
        };
    }

    private sealed class Escenario
    {
        public string DbPath { get; init; } = string.Empty;
        public SOPROContext Context { get; init; } = null!;
        public Proyecto Proyecto { get; init; } = null!;
        public ConfiguracionFinanciamiento Config { get; init; } = null!;
        public ProgramaObra Programa { get; init; } = null!;
    }
}
