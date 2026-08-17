using Microsoft.VisualStudio.TestTools.UnitTesting;
using SOPRO.Application.Models.Presupuesto;
using SOPRO.Application.Services;
using SOPRO.Core.Entities;

namespace SOPRO.Tests.Services.Presupuesto;

[TestClass]
public class BudgetPricingServiceTests
{
    private const string Acumulables = "Acumulables";
    private const string SobreCD = "SobreCD";

    // ── Paridad con la fachada legacy (N5-1: el servicio migró al motor, la
    //    fachada sigue siendo el oráculo diferencial contra SOPRO.Calculation) ──

    [TestMethod]
    [DataRow(2, 2, 4)]
    [DataRow(0, 2, 4)]
    [DataRow(2, 4, 4)]
    [DataRow(3, 3, 6)]
    public void CalculateUnitPrice_Acumulables_ParidadConFachada(int decQty, int decAmt, int decPct)
    {
        var proyecto = CrearProyecto(decQty, decAmt, decPct);
        var input = CrearInput(5m, 5m, 2m, 8m, 3m, Acumulables);
        AplicarPorcentajes(proyecto, input);

        foreach (var cd in new[] { 0.05m, 1m, 10.005m, 1000m, 652m, 9999.99m })
        {
            var esperado = new MotorCalculoSopro(proyecto)
                .CalcularPrecioUnitario(cd, input).PrecioUnitario;
            var actual = BudgetPricingService.CalculateUnitPrice(proyecto, cd);
            Assert.AreEqual(esperado, actual, $"CD={cd}");
        }
    }

    [TestMethod]
    [DataRow(2, 2, 4)]
    [DataRow(0, 2, 4)]
    [DataRow(2, 4, 4)]
    [DataRow(3, 3, 6)]
    public void CalculateUnitPrice_SobreCD_ParidadConFachada(int decQty, int decAmt, int decPct)
    {
        var proyecto = CrearProyecto(decQty, decAmt, decPct);
        var input = CrearInput(5m, 5m, 2m, 8m, 3m, SobreCD);
        AplicarPorcentajes(proyecto, input);

        foreach (var cd in new[] { 0.05m, 1m, 10.005m, 1000m, 652m, 9999.99m })
        {
            var esperado = new MotorCalculoSopro(proyecto)
                .CalcularPrecioUnitario(cd, input).PrecioUnitario;
            var actual = BudgetPricingService.CalculateUnitPrice(proyecto, cd);
            Assert.AreEqual(esperado, actual, $"CD={cd}");
        }
    }

    [TestMethod]
    public void CalculateUnitPrice_SinProyecto_ParidadConFachada()
    {
        var input = CrearInput(10m, 5m, 2m, 10m, 1m, Acumulables);

        foreach (var cd in new[] { 0.05m, 1m, 10.005m, 1000m, 9999.99m })
        {
            var esperado = new MotorCalculoSopro(2, 2, 4)
                .CalcularPrecioUnitario(cd, input).PrecioUnitario;
            var actual = BudgetPricingService.CalculateUnitPrice(input, cd);
            Assert.AreEqual(esperado, actual, $"CD={cd}");
        }
    }

    [TestMethod]
    public void MultiplyUsingDisplayPrecision_ParidadConFachada()
    {
        var proyecto = CrearProyecto(2, 2, 4);

        foreach (var (cantidad, pu) in new[]
                 {
                     (652m, 13.3875m),
                     (0.05m, 99.999m),
                     (1234.567m, 0.01m),
                     (1m, 1m),
                     (999.999m, 0.005m)
                 })
        {
            var esperado = new MotorCalculoSopro(proyecto).Multiplicar(cantidad, pu);
            var actual = BudgetPricingService.MultiplyUsingDisplayPrecision(proyecto, cantidad, pu);
            Assert.AreEqual(esperado, actual, $"({cantidad} × {pu})");
        }
    }

    [TestMethod]
    public void RoundImporte_ParidadConFachada()
    {
        var proyecto = CrearProyecto(2, 2, 4);

        foreach (var valor in new[]
                 {
                     0m, 2.005m, -2.005m, 2.015m, 13.3875m, 999.999m,
                     1000m, 0.0049m, 12345.6789m
                 })
        {
            var esperado = new MotorCalculoSopro(proyecto).RedondearImporte(valor);
            var actual = BudgetPricingService.RoundImporte(proyecto, valor);
            Assert.AreEqual(esperado, actual, $"valor={valor}");
        }
    }

    // ── Valores dorados (cascada del motor, AwayFromZero) ────────────────────

    [TestMethod]
    public void CalculateUnitPrice_Acumulables_ValorDorado()
    {
        var proyecto = CrearProyecto(2, 2, 4);
        AplicarPorcentajes(proyecto, CrearInput(5m, 5m, 2m, 8m, 3m, Acumulables));

        // CD 1000 → ind 100.00 → sub1 1100.00 → fin 22.00 → sub2 1122.00
        // → util 89.76 → sub3 1211.76 → cargos 36.35 → PU 1248.11
        var actual = BudgetPricingService.CalculateUnitPrice(proyecto, 1000m);
        Assert.AreEqual(1248.11m, actual);
    }

    [TestMethod]
    public void CalculateUnitPrice_SobreCD_ValorDorado()
    {
        var proyecto = CrearProyecto(2, 2, 4);
        AplicarPorcentajes(proyecto, CrearInput(5m, 5m, 2m, 8m, 3m, SobreCD));

        // Todos los porcentajes sobre CD: ind 100 → fin 20 → util 80 → cargos 30 → PU 1230.00
        var actual = BudgetPricingService.CalculateUnitPrice(proyecto, 1000m);
        Assert.AreEqual(1230.00m, actual);
    }

    [TestMethod]
    public void MultiplyUsingDisplayPrecision_ValorDorado()
    {
        var proyecto = CrearProyecto(2, 2, 4);

        // 652 × 13.3875 → P.U. visible 13.39 → 652 × 13.39 = 8730.28
        var actual = BudgetPricingService.MultiplyUsingDisplayPrecision(proyecto, 652m, 13.3875m);
        Assert.AreEqual(8730.28m, actual);
    }

    [TestMethod]
    public void RoundImporte_ValorDorado_AwayFromZero()
    {
        var proyecto = CrearProyecto(2, 2, 4);
        Assert.AreEqual(2.01m, BudgetPricingService.RoundImporte(proyecto, 2.005m));
        Assert.AreEqual(-2.01m, BudgetPricingService.RoundImporte(proyecto, -2.005m));
        Assert.AreEqual(0.00m, BudgetPricingService.RoundImporte(proyecto, 0.0049m));
    }

    // ── Factor (sin redondeo, es un multiplicador) ────────────────────────────

    [TestMethod]
    public void CalculateFactor_Acumulables_DebeMultiplicarEnCascada()
    {
        var proyecto = CrearProyecto(2, 2, 4);
        proyecto.PorcentajeIndirectosCentral = 10m;
        proyecto.PorcentajeIndirectosCampo = 5m;
        proyecto.PorcentajeFinanciamiento = 2m;
        proyecto.PorcentajeUtilidad = 10m;
        proyecto.PorcentajeCargosAdicionales = 1m;
        proyecto.ModoCalculoPorcentajes = Acumulables;

        // 1.15 × 1.02 × 1.10 × 1.01 = 1.3032030
        Assert.AreEqual(1.3032030m, BudgetPricingService.CalculateFactor(proyecto));
    }

    [TestMethod]
    public void CalculateFactor_SobreCD_DebeSumarPorcentajesSobreBase()
    {
        var proyecto = CrearProyecto(2, 2, 4);
        proyecto.PorcentajeIndirectosCentral = 10m;
        proyecto.PorcentajeIndirectosCampo = 5m;
        proyecto.PorcentajeFinanciamiento = 2m;
        proyecto.PorcentajeUtilidad = 10m;
        proyecto.PorcentajeCargosAdicionales = 1m;
        proyecto.ModoCalculoPorcentajes = SobreCD;

        Assert.AreEqual(1.28m, BudgetPricingService.CalculateFactor(proyecto));
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static BudgetPercentageInput CrearInput(decimal indCentral, decimal indCampo,
        decimal fin, decimal util, decimal cargos, string modo) => new()
    {
        IndirectosCentral = indCentral,
        IndirectosCampo = indCampo,
        Financiamiento = fin,
        Utilidad = util,
        CargosAdicionales = cargos,
        ModoCalculoPorcentajes = modo
    };

    private static void AplicarPorcentajes(Proyecto proyecto, BudgetPercentageInput input)
    {
        proyecto.PorcentajeIndirectosCentral = input.IndirectosCentral;
        proyecto.PorcentajeIndirectosCampo = input.IndirectosCampo;
        proyecto.PorcentajeFinanciamiento = input.Financiamiento;
        proyecto.PorcentajeUtilidad = input.Utilidad;
        proyecto.PorcentajeCargosAdicionales = input.CargosAdicionales;
        proyecto.ModoCalculoPorcentajes = input.ModoCalculoPorcentajes;
    }

    private static Proyecto CrearProyecto(int decQty, int decAmt, int decPct) => new()
    {
        Nombre = "Proyecto BudgetPricingService pruebas",
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
}