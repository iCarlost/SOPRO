using Microsoft.VisualStudio.TestTools.UnitTesting;
using SOPRO.Application.Models.Presupuesto;
using SOPRO.Application.Services;
using SOPRO.Core.Entities;
using SOPRO.Data.Context;
using SOPRO.Tests.TestInfrastructure;

namespace SOPRO.Tests.Services.Presupuesto;

[TestClass]
public class UtilidadCalculationParityTests
{
    // ── N5-7: Calcular migró sus 5 RedondearImporte de MotorCalculoSopro a
    //    SoproCalculationEngine.RoundAmount (baseUtilidad, importeUtilidad,
    //    importeIsr, importePtu, utilidadNeta). Los Math.Round(..., 5) de los
    //    coeficientes intermedios se conservan por diseño. BuildPreview (N5-3)
    //    es la misma llamada compartida en ambos lados.
    //    Paridad exacta contra la referencia compuesta con la fachada
    //    (oráculo diferencial) + dorados.

    [TestMethod]
    public void Calcular_ParidadConFachada_BateriaAleatoriaConSemilla()
    {
        var rnd = new Random(246813579);

        for (int iter = 0; iter < 300; iter++)
        {
            using var context = TestDbFactory.CreateContext();

            var proyecto = CrearProyecto(rnd.Next(0, 5), rnd.Next(0, 5), rnd.Next(0, 7));
            context.Proyectos.Add(proyecto);
            context.SaveChanges();

            // Mitad de las iteraciones: preview calculado desde conceptos reales
            if (iter % 2 == 0)
            {
                var matriz = new Matriz
                {
                    ProyectoId = proyecto.Id,
                    Clave = $"APU-{iter}",
                    Descripcion = string.Empty,
                    Unidad = "pza",
                    Tipo = TipoMatriz.APU,
                    Notas = string.Empty
                };
                context.Matrices.Add(matriz);
                context.SaveChanges();

                int nConceptos = rnd.Next(1, 5);
                for (int i = 0; i < nConceptos; i++)
                {
                    context.ConceptosPresupuesto.Add(new ConceptoPresupuesto
                    {
                        ProyectoId = proyecto.Id,
                        Clave = $"C-{iter}-{i}",
                        Descripcion = string.Empty,
                        Unidad = "pza",
                        Cantidad = Valor(rnd),
                        MatrizId = matriz.Id,
                        CostoDirectoUnitario = Valor(rnd),
                        CostoDirectoTotal = 0m,
                        PrecioUnitario = 0m,
                        ImporteTotal = 0m,
                        Nivel = 1,
                        Orden = i + 1,
                        EsAgrupador = false,
                        ColumnasPersonalizadasJSON = string.Empty,
                        Notas = string.Empty
                    });
                }
                context.SaveChanges();
            }

            var input = CrearInputAleatorio(rnd);
            var servicio = new UtilidadCalculationService().Calcular(context, proyecto, input);
            var referencia = CalcularConFachada(context, proyecto, input);

            CompararResultados(servicio, referencia, $"iter={iter}");
        }
    }

    [TestMethod]
    public void Calcular_Dorado_ModoDirecto()
    {
        using var context = TestDbFactory.CreateContext();
        var proyecto = CrearProyecto(2, 2, 4);
        context.Proyectos.Add(proyecto);
        context.SaveChanges();

        var result = new UtilidadCalculationService().Calcular(context, proyecto, new UtilidadCalculationInput
        {
            CostoDirectoReferencia = 1000m,
            IndirectosCentral = 10m,
            IndirectosCampo = 5m,
            Financiamiento = 2m,
            UtilidadDirecta = 8m,
            Isr = 30m,
            Ptu = 10m,
            ModoCalculoPorcentajes = "Acumulables",
            ModoAsistido = false
        });

        // CD 1000 + Ind 15% (150) = 1150 + Fin 2% (23) = Subtotal2 1173 → base 1173.00
        // factor 1-(0.3+0.1) = 0.6 → neto 8×0.6 = 4.80000
        // utilidad 1173×8% = 93.84; ISR 93.84×30% = 28.15; PTU 93.84×10% = 9.38; neta 56.31
        Assert.AreEqual("Directo", result.Modo);
        Assert.AreEqual(1173.00m, result.BaseUtilidad);
        Assert.AreEqual(8.00000m, result.PorcentajeUtilidadBruta);
        Assert.AreEqual(4.80000m, result.PorcentajeUtilidadNeta);
        Assert.AreEqual(30m, result.Isr);
        Assert.AreEqual(10m, result.Ptu);
        Assert.AreEqual(93.84m, result.ImporteUtilidad);
        Assert.AreEqual(28.15m, result.ImporteIsr);
        Assert.AreEqual(9.38m, result.ImportePtu);
        Assert.AreEqual(56.31m, result.UtilidadNetaEstimada);
    }

    [TestMethod]
    public void Calcular_Dorado_ModoAsistido()
    {
        using var context = TestDbFactory.CreateContext();
        var proyecto = CrearProyecto(2, 2, 4);
        context.Proyectos.Add(proyecto);
        context.SaveChanges();

        var result = new UtilidadCalculationService().Calcular(context, proyecto, new UtilidadCalculationInput
        {
            CostoDirectoReferencia = 1000m,
            IndirectosCentral = 10m,
            IndirectosCampo = 5m,
            Financiamiento = 2m,
            UtilidadNetaDeseada = 6m,
            Isr = 20m,
            Ptu = 5m,
            ModoCalculoPorcentajes = "Acumulables",
            ModoAsistido = true
        });

        // factor 1-(0.2+0.05) = 0.75 → bruto R5(6/0.75) = 8.00000; neto = deseada = 6.00000
        // utilidad 1173×8% = 93.84; ISR 93.84×20% = 18.77; PTU 93.84×5% = 4.69; neta 70.38
        Assert.AreEqual("Asistido", result.Modo);
        Assert.AreEqual(1173.00m, result.BaseUtilidad);
        Assert.AreEqual(8.00000m, result.PorcentajeUtilidadBruta);
        Assert.AreEqual(6.00000m, result.PorcentajeUtilidadNeta);
        Assert.AreEqual(93.84m, result.ImporteUtilidad);
        Assert.AreEqual(18.77m, result.ImporteIsr);
        Assert.AreEqual(4.69m, result.ImportePtu);
        Assert.AreEqual(70.38m, result.UtilidadNetaEstimada);
    }

    [TestMethod]
    public void Calcular_Dorado_SinConceptos_ConReferenciaDeTresDecimales()
    {
        using var context = TestDbFactory.CreateContext();
        var proyecto = CrearProyecto(2, 2, 4);
        context.Proyectos.Add(proyecto);
        context.SaveChanges();

        var result = new UtilidadCalculationService().Calcular(context, proyecto, new UtilidadCalculationInput
        {
            CostoDirectoReferencia = 1234.567m,
            IndirectosCentral = 5m,
            IndirectosCampo = 5m,
            Financiamiento = 2m,
            UtilidadDirecta = 8m,
            Isr = 30m,
            Ptu = 10m,
            ModoCalculoPorcentajes = "Acumulables",
            ModoAsistido = false
        });

        // Preview N5-3: CD 1234.57 → sub2 1385.19 → base 1385.19
        // utilidad 1385.19×8% = 110.82; ISR 110.82×30% = 33.25; PTU 110.82×10% = 11.08; neta 66.49
        Assert.AreEqual(1385.19m, result.BaseUtilidad);
        Assert.AreEqual(8.00000m, result.PorcentajeUtilidadBruta);
        Assert.AreEqual(4.80000m, result.PorcentajeUtilidadNeta);
        Assert.AreEqual(110.82m, result.ImporteUtilidad);
        Assert.AreEqual(33.25m, result.ImporteIsr);
        Assert.AreEqual(11.08m, result.ImportePtu);
        Assert.AreEqual(66.49m, result.UtilidadNetaEstimada);
    }

    [TestMethod]
    public void Calcular_Dorado_FactorMenorOIgualACero()
    {
        using var context = TestDbFactory.CreateContext();
        var proyecto = CrearProyecto(2, 2, 4);
        context.Proyectos.Add(proyecto);
        context.SaveChanges();

        // ISR 60 + PTU 50 → factor 1-1.1 = -0.1 ≤ 0: asistido devuelve bruto 0
        // (sin división), directo devuelve neto negativo (sin clamp, heredado).
        var asistido = new UtilidadCalculationService().Calcular(context, proyecto, new UtilidadCalculationInput
        {
            CostoDirectoReferencia = 1000m,
            IndirectosCentral = 10m,
            IndirectosCampo = 5m,
            Financiamiento = 2m,
            UtilidadNetaDeseada = 6m,
            Isr = 60m,
            Ptu = 50m,
            ModoCalculoPorcentajes = "Acumulables",
            ModoAsistido = true
        });

        Assert.AreEqual(0.00000m, asistido.PorcentajeUtilidadBruta);
        Assert.AreEqual(6.00000m, asistido.PorcentajeUtilidadNeta);
        Assert.AreEqual(0.00m, asistido.ImporteUtilidad);
        Assert.AreEqual(0.00m, asistido.ImporteIsr);
        Assert.AreEqual(0.00m, asistido.ImportePtu);
        Assert.AreEqual(0.00m, asistido.UtilidadNetaEstimada);

        var directo = new UtilidadCalculationService().Calcular(context, proyecto, new UtilidadCalculationInput
        {
            CostoDirectoReferencia = 1000m,
            IndirectosCentral = 10m,
            IndirectosCampo = 5m,
            Financiamiento = 2m,
            UtilidadDirecta = 8m,
            Isr = 60m,
            Ptu = 50m,
            ModoCalculoPorcentajes = "Acumulables",
            ModoAsistido = false
        });

        Assert.AreEqual(8.00000m, directo.PorcentajeUtilidadBruta);
        Assert.AreEqual(-0.80000m, directo.PorcentajeUtilidadNeta);
        Assert.AreEqual(93.84m, directo.ImporteUtilidad);
        Assert.AreEqual(56.30m, directo.ImporteIsr);
        Assert.AreEqual(46.92m, directo.ImportePtu);
        Assert.AreEqual(-9.38m, directo.UtilidadNetaEstimada);
    }

    // ── Referencia compuesta con la fachada (código pre-migración) ────────────

    private static UtilidadCalculationResult CalcularConFachada(
        SOPROContext context, Proyecto proyecto, UtilidadCalculationInput input)
    {
        var motor = new MotorCalculoSopro(proyecto);

        var preview = BudgetPreviewCalculationService.BuildPreview(context, proyecto,
            new BudgetPercentageInput
            {
                CostoDirectoReferencia = input.CostoDirectoReferencia,
                IndirectosCentral      = input.IndirectosCentral,
                IndirectosCampo        = input.IndirectosCampo,
                Financiamiento         = input.Financiamiento,
                Utilidad               = 0m,
                CargosAdicionales      = 0m,
                ModoCalculoPorcentajes = input.ModoCalculoPorcentajes
            });

        decimal baseUtilidad = motor.RedondearImporte(preview.Subtotal2);

        decimal porcentajeBruto;
        decimal porcentajeNeto;

        if (input.ModoAsistido)
        {
            porcentajeNeto = Math.Max(0m, input.UtilidadNetaDeseada);
            decimal factor = 1m - ((Math.Max(0m, input.Isr) + Math.Max(0m, input.Ptu)) / 100m);
            porcentajeBruto = factor > 0m
                ? Math.Round(porcentajeNeto / factor, 5, MidpointRounding.AwayFromZero)
                : 0m;
        }
        else
        {
            porcentajeBruto = Math.Max(0m, input.UtilidadDirecta);
            decimal factor  = 1m - ((Math.Max(0m, input.Isr) + Math.Max(0m, input.Ptu)) / 100m);
            porcentajeNeto  = Math.Round(porcentajeBruto * factor, 5, MidpointRounding.AwayFromZero);
        }

        decimal importeUtilidad = motor.RedondearImporte(baseUtilidad * porcentajeBruto / 100m);
        decimal importeIsr      = motor.RedondearImporte(importeUtilidad * Math.Max(0m, input.Isr) / 100m);
        decimal importePtu      = motor.RedondearImporte(importeUtilidad * Math.Max(0m, input.Ptu) / 100m);
        decimal utilidadNeta    = motor.RedondearImporte(importeUtilidad - importeIsr - importePtu);

        return new UtilidadCalculationResult
        {
            BaseUtilidad            = baseUtilidad,
            PorcentajeUtilidadBruta = porcentajeBruto,
            PorcentajeUtilidadNeta  = porcentajeNeto,
            Isr                     = input.Isr,
            Ptu                     = input.Ptu,
            ImporteUtilidad         = importeUtilidad,
            ImporteIsr              = importeIsr,
            ImportePtu              = importePtu,
            UtilidadNetaEstimada    = utilidadNeta,
            Modo                    = input.ModoAsistido ? "Asistido" : "Directo"
        };
    }

    private static void CompararResultados(
        UtilidadCalculationResult servicio, UtilidadCalculationResult referencia, string tag)
    {
        Assert.AreEqual(referencia.BaseUtilidad, servicio.BaseUtilidad, $"{tag} BaseUtilidad");
        Assert.AreEqual(referencia.PorcentajeUtilidadBruta, servicio.PorcentajeUtilidadBruta, $"{tag} PorcentajeUtilidadBruta");
        Assert.AreEqual(referencia.PorcentajeUtilidadNeta, servicio.PorcentajeUtilidadNeta, $"{tag} PorcentajeUtilidadNeta");
        Assert.AreEqual(referencia.Isr, servicio.Isr, $"{tag} Isr");
        Assert.AreEqual(referencia.Ptu, servicio.Ptu, $"{tag} Ptu");
        Assert.AreEqual(referencia.ImporteUtilidad, servicio.ImporteUtilidad, $"{tag} ImporteUtilidad");
        Assert.AreEqual(referencia.ImporteIsr, servicio.ImporteIsr, $"{tag} ImporteIsr");
        Assert.AreEqual(referencia.ImportePtu, servicio.ImportePtu, $"{tag} ImportePtu");
        Assert.AreEqual(referencia.UtilidadNetaEstimada, servicio.UtilidadNetaEstimada, $"{tag} UtilidadNetaEstimada");
        Assert.AreEqual(referencia.Modo, servicio.Modo, $"{tag} Modo");
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static UtilidadCalculationInput CrearInputAleatorio(Random rnd) => new()
    {
        CostoDirectoReferencia = rnd.Next(0, 10) < 4 ? Valor(rnd) : 0m,
        IndirectosCentral = rnd.Next(0, 1500) / 100m,
        IndirectosCampo = rnd.Next(0, 1500) / 100m,
        Financiamiento = rnd.Next(0, 500) / 100m,
        UtilidadDirecta = rnd.Next(0, 4000) / 100m,
        UtilidadNetaDeseada = rnd.Next(0, 4000) / 100m,
        Isr = rnd.Next(-5, 40),
        Ptu = rnd.Next(-5, 30),
        ModoCalculoPorcentajes = rnd.Next(0, 4) switch
        {
            0 => "Acumulables",
            1 => "SobreCD",
            2 => "sobrecd",
            _ => null!
        },
        ModoAsistido = rnd.Next(0, 2) == 0
    };

    private static Proyecto CrearProyecto(int decQty, int decAmt, int decPct) => new()
    {
        Nombre = "Proyecto utilidad paridad",
        Descripcion = string.Empty,
        Ubicacion = string.Empty,
        Convocante = string.Empty,
        Contratista = string.Empty,
        ApoderadoLegal = string.Empty,
        FechaInicio = new DateTime(2026, 1, 1),
        FechaTermino = new DateTime(2026, 1, 31),
        PlazoEjecucion = 31,
        DecimalesCantidad = decQty,
        DecimalesImporte = decAmt,
        DecimalesPorcentaje = decPct
    };

    private static decimal Valor(Random rnd)
        => rnd.Next(1, 1_000_000) / 1000m;
}