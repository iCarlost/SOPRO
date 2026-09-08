using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SOPRO.Application.Services;
using SOPRO.Core.Entities;
using SOPRO.Tests.TestInfrastructure;

namespace SOPRO.Tests.Services;

/// <summary>
/// Caracterización legado del servicio de financiamiento (N7-17a).
/// Cada escenario fija el comportamiento exacto del flujo legacy de
/// <see cref="FinanciamientoCalculationService.Calcular"/> ANTES de cualquier
/// centralización hacia Sopro.Calculation. Los valores esperados se calcularon
/// a mano a partir de las fórmulas del negocio, con el fixture de referencia
/// del test existente (tasa 12%, 3 filas, neto 5.7534, porcentaje 0.26152)
/// como ancla de verificación:
/// - tasa efectiva = TasaTIIE + PuntosAdicionales;
/// - Egresos período = CD + CI (CI oficial reconciliado por proporción al CD);
/// - anticpo solo en el primer período; cobro con desfase (estimación N en N+desfase);
/// - amortización = Round(Cobrada x PorcentajeAnticipo/100, 6) limitada por pendiente y cobrada;
/// - interés = Round(Saldo x Round(tasa x Dias/365, 8), 4); clásico suma saldos negativos
///   (interés positivo en fila) y dual distingue ingresos (tasa TIIE) de costos (tasa efectiva);
/// - neto = Round(InteresesNegativos - InteresesPositivos, 4); porcentaje = Round(neto/Base, 5);
/// - montos de fila y config con redondeo de precisión del proyecto.
/// </summary>
[TestClass]
public class FinanciamientoCalculationLegacyCharacterizationTests
{
    [TestMethod]
    public void Anticipo30Desfase1_AmortizacionSimple_YConfigActualizada()
    {
        var escenario = CrearEscenario(
            porcentajeAnticipo: 30m,
            desfaseCobro: 1,
            baseCalculo: "Acumulable",
            modeloDual: false);

        var porcentaje = escenario.Calcular();

        var filas = escenario.Filas;

        Assert.AreEqual(3, filas.Count, "2 periodos + 1 fila de desfase.");
        Assert.AreEqual(7, filas[0].DiasPeriodo, "(FechaFin - FechaInicio).Days + 1.");

        Assert.AreEqual(1100.00m, filas[0].Egresos);
        Assert.AreEqual(600.00m, filas[0].AnticipoRecibido, "Anticipo 30% de 2000.");
        Assert.AreEqual(0.00m, filas[0].EstimacionCobrada, "Desfase: no hay cobro en el periodo 1.");
        Assert.AreEqual(0.00m, filas[0].AmortizacionAnticipo);
        Assert.AreEqual(-500.00m, filas[0].FlujoNeto);
        Assert.AreEqual(-500.00m, filas[0].SaldoAcumulado);
        Assert.AreEqual(1.1507m, filas[0].InteresPeriodo, "Round(500 x 0.00230137, 4).");

        Assert.AreEqual(1100.00m, filas[1].Egresos);
        Assert.AreEqual(0.00m, filas[1].AnticipoRecibido);
        Assert.AreEqual(1000.00m, filas[1].EstimacionCobrada, "Estimación del período 1.");
        Assert.AreEqual(300.00m, filas[1].AmortizacionAnticipo, "Round(1000 x 0.30, 6).");
        Assert.AreEqual(-400.00m, filas[1].FlujoNeto);
        Assert.AreEqual(-900.00m, filas[1].SaldoAcumulado);
        Assert.AreEqual(2.0712m, filas[1].InteresPeriodo, "Round(900 x 0.00230137, 4).");

        Assert.AreEqual(0.00m, filas[2].Egresos);
        Assert.AreEqual(0.00m, filas[2].AnticipoRecibido);
        Assert.AreEqual(1000.00m, filas[2].EstimacionCobrada, "Estimación del período 2, cobrada con desfase.");
        Assert.AreEqual(300.00m, filas[2].AmortizacionAnticipo, "Cap por pendiente (300 <= pendiente 300).");
        Assert.AreEqual(700.00m, filas[2].FlujoNeto);
        Assert.AreEqual(-200.00m, filas[2].SaldoAcumulado);
        Assert.AreEqual(0.4603m, filas[2].InteresPeriodo);

        Assert.AreEqual(3.6822m, escenario.Config.InteresesNegativos, "Round(1.1507 + 2.0712 + 0.4603, 4).");
        Assert.AreEqual(0.0000m, escenario.Config.InteresesPositivos);
        Assert.AreEqual(3.6822m, escenario.Config.FinanciamientoNeto);
        Assert.AreEqual(0.16737m, porcentaje, "Round(3.6822 / 2200 x 100, 5).");
        Assert.IsNotNull(escenario.Config.FechaCalculo);
    }

    [TestMethod]
    public void AnticipoCapByPendiente_SeAgotaAntesDelUltimoPeriodo()
    {
        var escenario = CrearEscenario(
            porcentajeAnticipo: 5m,
            desfaseCobro: 0,
            baseCalculo: "Acumulable",
            modeloDual: false);

        var filas = escenario.FilasAfterCalcular();

        Assert.AreEqual(2, filas.Count, "Sin desfase: 2 periodos.");

        Assert.AreEqual(100.00m, filas[0].AnticipoRecibido, "Anticipo 5% de 2000.");
        Assert.AreEqual(1000.00m, filas[0].EstimacionCobrada, "Desfase 0: cobra su propia estimación.");
        Assert.AreEqual(50.00m, filas[0].AmortizacionAnticipo, "Round(1000 x 0.05, 6); pendiente queda en 50.");
        Assert.AreEqual(-50.00m, filas[0].FlujoNeto);
        Assert.AreEqual(-50.00m, filas[0].SaldoAcumulado);
        Assert.AreEqual(0.1151m, filas[0].InteresPeriodo);

        Assert.AreEqual(1000.00m, filas[1].EstimacionCobrada);
        Assert.AreEqual(50.00m, filas[1].AmortizacionAnticipo, "Cap por pendiente: Round(50) limitado a pendiente 50.");
        Assert.AreEqual(-150.00m, filas[1].FlujoNeto);
        Assert.AreEqual(-200.00m, filas[1].SaldoAcumulado);
        Assert.AreEqual(0.4603m, filas[1].InteresPeriodo);

        Assert.AreEqual(0.5754m, escenario.Config.FinanciamientoNeto);
        Assert.AreEqual(0.02615m, escenario.Config.PorcentajeCalculado, "Round(0.5754 / 2200 x 100, 5).");
    }

    [TestMethod]
    public void BaseCalculoSobreCD_UsaSoloElDirectoParaElPorcentaje()
    {
        var escenario = CrearEscenario(
            porcentajeAnticipo: 30m,
            desfaseCobro: 1,
            baseCalculo: "SobreCD",
            modeloDual: false);

        var porcentaje = escenario.Calcular();

        Assert.AreEqual(3.6822m, escenario.Config.FinanciamientoNeto, "El flujo de caja no cambia.");
        Assert.AreEqual(0.18411m, porcentaje, "Round(3.6822 / 2000 x 100, 5).");
    }

    [TestMethod]
    public void ModeloDual_SaldosMixtos_DistinguenCostoYBeneficio()
    {
        var escenario = CrearEscenario(
            porcentajeAnticipo: 80m,
            desfaseCobro: 1,
            baseCalculo: "Acumulable",
            modeloDual: true);

        escenario.Calcular();

        var filas = escenario.Filas;

        Assert.AreEqual(500.00m, filas[0].SaldoAcumulado);
        Assert.AreEqual(1.1507m, filas[0].InteresPeriodo, "Saldo positivo: interés a favor con tasa TIIE.");

        Assert.AreEqual(-400.00m, filas[1].SaldoAcumulado);
        Assert.AreEqual(-0.9205m, filas[1].InteresPeriodo, "Saldo negativo: interés en contra, signo explícito.");

        Assert.AreEqual(-200.00m, filas[2].SaldoAcumulado);
        Assert.AreEqual(-0.4603m, filas[2].InteresPeriodo);

        Assert.AreEqual(1.1507m, escenario.Config.InteresesPositivos, "Round(1.1507, 4).");
        Assert.AreEqual(1.3808m, escenario.Config.InteresesNegativos, "Round(0.9205 + 0.4603, 4).");
        Assert.AreEqual(-0.2301m, escenario.Config.FinanciamientoNeto, "Round(1.1507 - 1.3808, 4).");
        Assert.AreEqual(-0.01046m, escenario.Config.PorcentajeCalculado);
    }

    [TestMethod]
    public void ResiduoDeDistribucion_SeApliqueSoloAlCostoDirectoDelUltimoPeriodo()
    {
        var escenario = CrearEscenario(
            porcentajeAnticipo: 0m,
            desfaseCobro: 0,
            baseCalculo: "Acumulable",
            modeloDual: false,
            cantidadesProgramadas: (10.5m, 10m));

        escenario.Calcular();

        var filas = escenario.Filas;

        Assert.AreEqual(1155.00m, filas[0].Egresos, "CD 1050 + CI 105 (CI 200 x 1050/2000).");
        Assert.AreEqual(1050.00m, filas[0].EstimacionCobrada, "Estimación = ImporteProgramado (no CD).");
        Assert.AreEqual(-105.00m, filas[0].FlujoNeto, "Cobra 1050 contra egreso 1155.");
        Assert.AreEqual(-105.00m, filas[0].SaldoAcumulado);
        Assert.AreEqual(0.2416m, filas[0].InteresPeriodo, "Round(105 x 0.00230137, 4).");

        Assert.AreEqual(1045.00m, filas[1].Egresos, "CD 950 (residuo -50) + CI 95.");
        Assert.AreEqual(1000.00m, filas[1].EstimacionCobrada, "ImporteProgramado del período 2, sin residuo.");
        Assert.AreEqual(-45.00m, filas[1].FlujoNeto);
        Assert.AreEqual(-150.00m, filas[1].SaldoAcumulado);
        Assert.AreEqual(0.3452m, filas[1].InteresPeriodo, "Round(150 x 0.00230137, 4).");

        Assert.AreEqual(0.5868m, escenario.Config.FinanciamientoNeto);
        Assert.AreEqual(0.02667m, escenario.Config.PorcentajeCalculado, "Round(0.5868 / 2200 x 100, 5).");
    }

    [TestMethod]
    public void SinProgramaActivo_DevuelveCero()
    {
        var escenario = CrearEscenario(
            porcentajeAnticipo: 30m,
            desfaseCobro: 1,
            baseCalculo: "Acumulable",
            modeloDual: false,
            programaActivo: false);

        var porcentaje = escenario.Calcular();

        Assert.AreEqual(0m, porcentaje);
        Assert.AreEqual(0, escenario.Context.FilasFlujoCajaFinanciamiento.Count());
        Assert.AreEqual(0m, escenario.Config.PorcentajeCalculado);
    }

    [TestMethod]
    public void ProgramaSinPeriodos_DevuelveCero()
    {
        var escenario = CrearEscenario(
            porcentajeAnticipo: 30m,
            desfaseCobro: 1,
            baseCalculo: "Acumulable",
            modeloDual: false,
            conPeriodos: false);

        var porcentaje = escenario.Calcular();

        Assert.AreEqual(0m, porcentaje);
        Assert.AreEqual(0, escenario.Context.FilasFlujoCajaFinanciamiento.Count());
    }

    [TestMethod]
    public void BaseCalculoCero_DevuelveCeroSinEscribirFilas()
    {
        var escenario = CrearEscenario(
            porcentajeAnticipo: 30m,
            desfaseCobro: 1,
            baseCalculo: "Acumulable",
            modeloDual: false,
            conConcepto: false);

        var porcentaje = escenario.Calcular();

        Assert.AreEqual(0m, porcentaje);
        Assert.AreEqual(0, escenario.Context.FilasFlujoCajaFinanciamiento.Count());
    }

    private static Escenario CrearEscenario(
        decimal porcentajeAnticipo,
        int desfaseCobro,
        string baseCalculo,
        bool modeloDual,
        (decimal P1, decimal P2)? cantidadesProgramadas = null,
        bool programaActivo = true,
        bool conPeriodos = true,
        bool conConcepto = true)
    {
        var context = TestDbFactory.CreateContext();

        var proyecto = new Proyecto
        {
            Nombre = "Proyecto financiamiento caracterizacion",
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
            Activo = programaActivo
        };
        context.ProgramasObra.Add(programa);
        context.SaveChanges();

        PeriodoPrograma periodo1 = null!;
        PeriodoPrograma periodo2 = null!;
        if (conPeriodos)
        {
            periodo1 = new PeriodoPrograma
            {
                ProgramaObraId = programa.Id,
                NumeroPeriodo = 1,
                Etiqueta = "P1",
                FechaInicio = new DateTime(2026, 1, 1),
                FechaFin = new DateTime(2026, 1, 7)
            };
            periodo2 = new PeriodoPrograma
            {
                ProgramaObraId = programa.Id,
                NumeroPeriodo = 2,
                Etiqueta = "P2",
                FechaInicio = new DateTime(2026, 1, 8),
                FechaFin = new DateTime(2026, 1, 14)
            };
            context.PeriodosPrograma.AddRange(periodo1, periodo2);
            context.SaveChanges();
        }

        if (conConcepto)
        {
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

            if (conPeriodos)
            {
                var (cantidad1, cantidad2) = cantidadesProgramadas ?? (10m, 10m);
                context.DistribucionesPeriodo.AddRange(
                    new DistribucionPeriodo
                    {
                        ActividadProgramadaId = actividad.Id,
                        PeriodoProgramaId = periodo1.Id,
                        CantidadProgramada = cantidad1,
                        PorcentajeProgramado = 50m,
                        PrecioUnitario = 100m,
                        ImporteProgramado = cantidad1 * 100m
                    },
                    new DistribucionPeriodo
                    {
                        ActividadProgramadaId = actividad.Id,
                        PeriodoProgramaId = periodo2.Id,
                        CantidadProgramada = cantidad2,
                        PorcentajeProgramado = 50m,
                        PrecioUnitario = 100m,
                        ImporteProgramado = cantidad2 * 100m
                    });
                context.SaveChanges();
            }
        }

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

        return new Escenario(context, proyecto, config, modeloDual);
    }

    private class Escenario
    {
        public Escenario(
            SOPRO.Data.Context.SOPROContext context,
            Proyecto proyecto,
            ConfiguracionFinanciamiento config,
            bool modeloDual)
        {
            Context = context;
            Proyecto = proyecto;
            Config = config;
            ModeloDual = modeloDual;
        }

        public SOPRO.Data.Context.SOPROContext Context { get; }
        public Proyecto Proyecto { get; }
        public ConfiguracionFinanciamiento Config { get; }
        public bool ModeloDual { get; }

        public List<FilaFlujoCajaFinanciamiento> Filas => Context.FilasFlujoCajaFinanciamiento
            .AsNoTracking()
            .Where(f => f.ConfiguracionFinanciamientoId == Config.Id)
            .OrderBy(f => f.NumeroPeriodo)
            .ToList();

        public decimal Calcular() => new FinanciamientoCalculationService().Calcular(Context, Config, Proyecto, ModeloDual);

        public List<FilaFlujoCajaFinanciamiento> FilasAfterCalcular()
        {
            Calcular();
            return Filas;
        }
    }
}