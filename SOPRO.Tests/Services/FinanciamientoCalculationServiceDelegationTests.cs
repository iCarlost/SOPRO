using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SOPRO.Application.Services;
using SOPRO.Core.Entities;
using SOPRO.Data.Context;
using SOPRO.Tests.TestInfrastructure;

namespace SOPRO.Tests.Services;

/// <summary>
/// Comportamientos del adaptador de financiamiento (N7-17c) que no capturan los
/// goldens de N7-17a: recálculo reemplazando filas, guardas que conservan filas
/// previamente persistidas sin mutar configuración, y preservación de la hora de
/// las fechas de los períodos base (el legado persistía los DateTime originales).
/// La paridad aritmética completa queda probada por los 8 goldens de N7-17a, que
/// pasan contra el servicio delegado.
/// Toda verificación se lee desde un contexto NUEVO sobre la misma base SQLite
/// (los contextos de lectura no recrean la base; el escenario sí, una sola vez).
/// </summary>
[TestClass]
public class FinanciamientoCalculationServiceDelegationTests
{
    private readonly List<Escenario> _escenarios = new();

    [TestCleanup]
    public void Cleanup()
    {
        foreach (var e in _escenarios)
        {
            e.Context.Dispose();
            try
            {
                Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
                File.Delete(e.DbPath);
            }
            catch
            {
                // Archivo temporal SQLite: si el borrado falla se ignora.
            }
        }
        _escenarios.Clear();
    }

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

        // Pequeña pausa para que el recálculo sea estrictamente posterior y se
        // pueda comprobar que FechaCalculo se actualizó (no solo se conservó).
        System.Threading.Thread.Sleep(5);
        var segunda = service.Calcular(e.Context, e.Config, e.Proyecto);
        var segundaFilas = Filas(e, out var segundaIds);

        Assert.AreEqual(0.16737m, primera, "Cálculo base con anticipo 30%.");
        Assert.AreNotEqual(primera, segunda, "Anticipo distinto produce resultado distinto.");
        Assert.AreEqual(3, primeraFilas.Count, "3 filas en el primer cálculo.");
        Assert.AreEqual(primeraFilas.Count, segundaFilas.Count, "3 filas en el segundo cálculo.");
        Assert.IsTrue(
            primeraIds.All(id => !segundaIds.Contains(id)),
            "El recálculo reemplaza las filas previstas (Ids nuevos), no las reutiliza.");
        Assert.IsTrue(
            FilasCompletas(e).All(f => f.AnticipoRecibido == 0m),
            "Sin anticipo: todas las filas persistidas con anticipo 0.");

        // Configuración verificada desde un contexto nuevo (no desde la instancia tracked).
        using (var fresh = new SOPROContext(e.DbPath))
        {
            var guardada = fresh.ConfiguracionesFinanciamiento.Single(x => x.Id == e.Config.Id);
            Assert.AreEqual(segunda, guardada.PorcentajeCalculado, "Porcentaje persistido = retorno.");
            Assert.IsNotNull(guardada.FechaCalculo);
            Assert.IsTrue(
                guardada.FechaCalculo > primeraFecha,
                "FechaCalculo se actualiza en el recálculo (estrictamente posterior).");
        }
    }

    [TestMethod]
    public void GuardSinProgramaActivo_ConservaFilasPreviamentePersistidasYConfiguracion()
    {
        var e = CrearEscenario(porcentajeAnticipo: 30m, desfaseCobro: 1, baseCalculo: "Acumulable");
        var service = new FinanciamientoCalculationService();

        service.Calcular(e.Context, e.Config, e.Proyecto);
        Assert.AreEqual(3, Filas(e, out _).Count, "El cálculo inicial persiste 3 filas.");
        Assert.IsNotNull(e.Config.FechaCalculo, "El cálculo inicial fija FechaCalculo.");
        var filasAntes = Filas(e, out _);
        var configAntes = ClaveConfig(e);

        var programa = e.Context.ProgramasObra.Single(p => p.ProyectoId == e.Proyecto.Id);
        programa.Activo = false;
        e.Context.SaveChanges();

        var porcentaje = service.Calcular(e.Context, e.Config, e.Proyecto);

        Assert.AreEqual(0m, porcentaje, "Guard devuelve 0.");
        AssertFilasIguales(filasAntes, Filas(e, out _), "programa inactivo");
        Assert.AreEqual(configAntes, ClaveConfig(e), "Guard NO muta la configuración.");
    }

    [TestMethod]
    public void GuardBaseCero_ConservaFilasPreviamentePersistidasYConfiguracion()
    {
        var e = CrearEscenario(porcentajeAnticipo: 30m, desfaseCobro: 1, baseCalculo: "Acumulable");
        var service = new FinanciamientoCalculationService();

        service.Calcular(e.Context, e.Config, e.Proyecto);
        Assert.AreEqual(3, Filas(e, out _).Count, "El cálculo inicial persiste 3 filas.");
        Assert.IsNotNull(e.Config.FechaCalculo, "El cálculo inicial fija FechaCalculo.");
        var filasAntes = Filas(e, out _);
        var configAntes = ClaveConfig(e);

        var concepto = e.Context.ConceptosPresupuesto.Single();
        concepto.Cantidad = 0m;
        concepto.ImporteTotal = 0m;
        e.Context.SaveChanges();

        var porcentaje = service.Calcular(e.Context, e.Config, e.Proyecto);

        Assert.AreEqual(0m, porcentaje, "Base cero devuelve 0.");
        AssertFilasIguales(filasAntes, Filas(e, out _), "base cero");
        Assert.AreEqual(configAntes, ClaveConfig(e), "Guard NO muta la configuración.");
    }

    [TestMethod]
    public void GuardBaseNegativa_ConservaFilasPreviamentePersistidasYConfiguracion()
    {
        var e = CrearEscenario(porcentajeAnticipo: 30m, desfaseCobro: 1, baseCalculo: "Acumulable");
        var service = new FinanciamientoCalculationService();

        service.Calcular(e.Context, e.Config, e.Proyecto);
        Assert.AreEqual(3, Filas(e, out _).Count, "El cálculo inicial persiste 3 filas.");
        Assert.IsNotNull(e.Config.FechaCalculo, "El cálculo inicial fija FechaCalculo.");
        var filasAntes = Filas(e, out _);
        var configAntes = ClaveConfig(e);

        var concepto = e.Context.ConceptosPresupuesto.Single();
        concepto.CostoDirectoUnitario = -100m;
        concepto.CostoDirectoTotal = -2000m;
        e.Context.SaveChanges();

        var porcentaje = service.Calcular(e.Context, e.Config, e.Proyecto);

        Assert.AreEqual(0m, porcentaje, "Base negativa devuelve 0.");
        AssertFilasIguales(filasAntes, Filas(e, out _), "base negativa");
        Assert.AreEqual(configAntes, ClaveConfig(e), "Guard NO muta la configuración.");
    }

    [TestMethod]
    public void GuardProgramaActivoSinPeriodos_ConservaFilasPreviamentePersistidasYConfiguracion()
    {
        var e = CrearEscenario(porcentajeAnticipo: 30m, desfaseCobro: 1, baseCalculo: "Acumulable");
        var service = new FinanciamientoCalculationService();

        service.Calcular(e.Context, e.Config, e.Proyecto);
        Assert.AreEqual(3, Filas(e, out _).Count, "El cálculo inicial persiste 3 filas.");
        Assert.IsNotNull(e.Config.FechaCalculo, "El cálculo inicial fija FechaCalculo.");
        var filasAntes = Filas(e, out _);
        var configAntes = ClaveConfig(e);

        e.Context.DistribucionesPeriodo.RemoveRange(
            e.Context.DistribucionesPeriodo.Where(d => d.ActividadProgramada.ProgramaObraId == e.Programa.Id));
        e.Context.PeriodosPrograma.RemoveRange(
            e.Context.PeriodosPrograma.Where(p => p.ProgramaObraId == e.Programa.Id));
        e.Context.SaveChanges();

        var porcentaje = service.Calcular(e.Context, e.Config, e.Proyecto);

        Assert.AreEqual(0m, porcentaje, "Programa activo sin períodos devuelve 0.");
        AssertFilasIguales(filasAntes, Filas(e, out _), "programa sin períodos");
        Assert.AreEqual(configAntes, ClaveConfig(e), "Guard NO muta la configuración.");
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

        Assert.AreNotEqual(0m, porcentaje, "El cálculo no debe caer en guardas.");
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

    [TestMethod]
    public void Mapeo_VariosConceptos_YDistribucionesDesordenadas_AsignaCadaPeriodo()
    {
        // Dos conceptos con distribuciones insertadas deliberadamente desordenadas
        // (A-P2, B-P2, A-P1): el adaptador debe agrupar por concepto, ordenar por
        // índice de período y llevar el CD/CI/EGRESO/estimación a la fila correcta.
        // A: Cantidad 2 x 100 con P1=1.5 y P2=0.5 → CD [150, 50].
        // B: Cantidad 1 x 200 en P2 → CD [0, 200]. Total CD = 400.
        // Estimaciones: A-P1 150, A-P2 50, B-P2 200 → [150, 250].
        // CI oficial (10% central sobre CD) = 40 → [15, 25].
        // Egresos: P1 165, P2 275.
        var e = CrearEscenarioMultiConcepto();
        var service = new FinanciamientoCalculationService();

        var porcentaje = service.Calcular(e.Context, e.Config, e.Proyecto);

        Assert.AreNotEqual(0m, porcentaje, "El cálculo no debe caer en guardas.");
        var filas = FilasCompletas(e);
        Assert.AreEqual(2, filas.Count, "Sin desfase: solo las 2 filas base.");

        Assert.AreEqual(1, filas[0].NumeroPeriodo);
        Assert.AreEqual(165.00m, filas[0].Egresos, "P1: CD 150 + CI 15.");
        Assert.AreEqual(150.00m, filas[0].EstimacionCobrada, "P1: solo ImporteProgramado de A.");
        Assert.AreEqual(2, filas[1].NumeroPeriodo);
        Assert.AreEqual(275.00m, filas[1].Egresos, "P2: CD 250 + CI 25.");
        Assert.AreEqual(250.00m, filas[1].EstimacionCobrada, "P2: ImporteProgramado de A y B.");
    }

    private static void AssertFilasIguales(List<(int Id, string Clave)> antes, List<(int Id, string Clave)> despues, string contexto)
    {
        Assert.AreEqual(antes.Count, despues.Count, $"[{contexto}] Mismo número de filas.");
        Assert.IsTrue(
            antes.Select(f => f.Id).SequenceEqual(despues.Select(f => f.Id)),
            $"[{contexto}] Las filas conservan los mismos Ids (no se borran y reinsertan).");
        Assert.IsTrue(
            antes.Select(f => f.Clave).SequenceEqual(despues.Select(f => f.Clave)),
            $"[{contexto}] El contenido completo de las filas se conserva.");
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

    private static List<FilaFlujoCajaFinanciamiento> FilasCompletas(Escenario e)
    {
        using var fresh = new SOPROContext(e.DbPath);
        return fresh.FilasFlujoCajaFinanciamiento
            .AsNoTracking()
            .Where(f => f.ConfiguracionFinanciamientoId == e.Config.Id)
            .OrderBy(f => f.NumeroPeriodo)
            .ToList();
    }

    private static string Clave(FilaFlujoCajaFinanciamiento f) =>
        $"{f.NumeroPeriodo}|{f.Etiqueta}|{f.FechaInicio:O}|{f.FechaFin:O}|{f.DiasPeriodo}|{f.Egresos}|" +
        $"{f.AnticipoRecibido}|{f.EstimacionCobrada}|{f.AmortizacionAnticipo}|{f.FlujoNeto}|{f.SaldoAcumulado}|{f.InteresPeriodo}";

    private static string ClaveConfig(Escenario e)
    {
        using var fresh = new SOPROContext(e.DbPath);
        var c = fresh.ConfiguracionesFinanciamiento.Single(x => x.Id == e.Config.Id);
        return $"{c.ProyectoId}|{c.TasaTIIE}|{c.PuntosAdicionales}|{c.PorcentajeAnticipo}|" +
               $"{c.PeriodosAmortizacionAnticipo}|{c.DesfaseCobro}|{c.BaseCalculo}|{c.InteresesNegativos}|" +
               $"{c.InteresesPositivos}|{c.FinanciamientoNeto}|{c.PorcentajeCalculado}|" +
               (c.FechaCalculo.HasValue ? c.FechaCalculo.Value.ToString("O") : "null");
    }

    private Escenario CrearEscenario(decimal porcentajeAnticipo, int desfaseCobro, string baseCalculo)
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

        var escenario = new Escenario
        {
            DbPath = dbPath,
            Context = context,
            Proyecto = proyecto,
            Config = config,
            Programa = programa
        };
        _escenarios.Add(escenario);
        return escenario;
    }

    private Escenario CrearEscenarioMultiConcepto()
    {
        var dbPath = TestDbFactory.CreateTempDbPath();
        var context = TestDbFactory.CreateContextAt(dbPath);

        var proyecto = new Proyecto
        {
            Nombre = "Proyecto financiamiento multi concepto",
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
            Nombre = "Programa multi concepto",
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
            Descripcion = "Matriz base",
            Unidad = "m2",
            Tipo = TipoMatriz.APU,
            CostoDirecto = 100m
        };
        context.Matrices.Add(matriz);
        context.SaveChanges();

        var conceptoA = new ConceptoPresupuesto
        {
            ProyectoId = proyecto.Id,
            Clave = "C-A",
            Descripcion = "Concepto A",
            Unidad = "m2",
            Cantidad = 2m,
            MatrizId = matriz.Id,
            CostoDirectoUnitario = 100m,
            CostoDirectoTotal = 200m,
            PrecioUnitario = 100m,
            ImporteTotal = 200m
        };
        var conceptoB = new ConceptoPresupuesto
        {
            ProyectoId = proyecto.Id,
            Clave = "C-B",
            Descripcion = "Concepto B",
            Unidad = "m3",
            Cantidad = 1m,
            MatrizId = matriz.Id,
            CostoDirectoUnitario = 200m,
            CostoDirectoTotal = 200m,
            PrecioUnitario = 200m,
            ImporteTotal = 200m
        };
        context.ConceptosPresupuesto.AddRange(conceptoA, conceptoB);
        context.SaveChanges();

        var actividadA = new ActividadProgramada
        {
            ProgramaObraId = programa.Id,
            ConceptoPresupuestoId = conceptoA.Id,
            Clave = conceptoA.Clave,
            Descripcion = conceptoA.Descripcion,
            Unidad = conceptoA.Unidad,
            CantidadTotal = 2m,
            PrecioUnitario = 100m,
            ImporteTotal = 200m,
            DuracionDiasHabiles = 10,
            FechaInicioProgramada = new DateTime(2026, 1, 1),
            FechaFinProgramada = new DateTime(2026, 1, 14)
        };
        var actividadB = new ActividadProgramada
        {
            ProgramaObraId = programa.Id,
            ConceptoPresupuestoId = conceptoB.Id,
            Clave = conceptoB.Clave,
            Descripcion = conceptoB.Descripcion,
            Unidad = conceptoB.Unidad,
            CantidadTotal = 1m,
            PrecioUnitario = 200m,
            ImporteTotal = 200m,
            DuracionDiasHabiles = 10,
            FechaInicioProgramada = new DateTime(2026, 1, 1),
            FechaFinProgramada = new DateTime(2026, 1, 14)
        };
        context.ActividadesProgramadas.AddRange(actividadA, actividadB);
        context.SaveChanges();

        // Distribuciones insertadas desordenadas a propósito (A-P2, B-P2, A-P1).
        context.DistribucionesPeriodo.AddRange(
            new DistribucionPeriodo
            {
                ActividadProgramadaId = actividadA.Id,
                PeriodoProgramaId = periodo2.Id,
                CantidadProgramada = 0.5m,
                PorcentajeProgramado = 25m,
                PrecioUnitario = 100m,
                ImporteProgramado = 50m
            },
            new DistribucionPeriodo
            {
                ActividadProgramadaId = actividadB.Id,
                PeriodoProgramaId = periodo2.Id,
                CantidadProgramada = 1m,
                PorcentajeProgramado = 100m,
                PrecioUnitario = 200m,
                ImporteProgramado = 200m
            },
            new DistribucionPeriodo
            {
                ActividadProgramadaId = actividadA.Id,
                PeriodoProgramaId = periodo1.Id,
                CantidadProgramada = 1.5m,
                PorcentajeProgramado = 75m,
                PrecioUnitario = 100m,
                ImporteProgramado = 150m
            });
        context.SaveChanges();

        var config = new ConfiguracionFinanciamiento
        {
            ProyectoId = proyecto.Id,
            TasaTIIE = 12m,
            PuntosAdicionales = 0m,
            PorcentajeAnticipo = 0m,
            DesfaseCobro = 0,
            BaseCalculo = "SobreCD"
        };
        context.ConfiguracionesFinanciamiento.Add(config);
        context.SaveChanges();

        var escenario = new Escenario
        {
            DbPath = dbPath,
            Context = context,
            Proyecto = proyecto,
            Config = config,
            Programa = programa
        };
        _escenarios.Add(escenario);
        return escenario;
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