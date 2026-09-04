using Microsoft.VisualStudio.TestTools.UnitTesting;
using Sopro.Calculation;

namespace SOPRO.Tests.Services.Reportes;

/// <summary>
/// [N7-6] Evidencia matemática de la aritmética cruda de reportes WinForms (APU y
/// catálogos): multiplicaciones Cantidad × precio sin redondeo de pantalla.
/// Estos tests replican las fórmulas de los generadores y las contrastan contra el
/// motor canónico; NO invocan a los generadores, por lo que un cambio en el código
/// de reportes no los haría fallar. Los goldens reales de salida quedan pendientes
/// para el slice de migración.
/// </summary>
[TestClass]
public class ReportRawArithmeticCharacterizationTests
{
    [TestMethod]
    public void TotalMO_Crudo_DifiereDeBaseCanonica_YPorcentajeMODifiere()
    {
        var engine = new SoproCalculationEngine(2, 2, 4);

        // Réplica cruda de CalcularTotalMO (GeneradorExcelAPU.cs:600-613 y espejos):
        // MO 1 × 10.005 + cuadrilla 1 × 20.005, sin redondeo.
        decimal totalMOCrudo = 1m * 10.005m + 1m * 20.005m;

        // Base canónica: cada importe ya sale redondeado de Multiply; la suma
        // por pasos conserva escala 2.
        decimal baseCanonica = engine.RoundAmount(engine.RoundAmount(0m + engine.Multiply(1m, 10.005m)) + engine.Multiply(1m, 20.005m));

        Assert.AreEqual(30.01m, totalMOCrudo);
        Assert.AreEqual(30.02m, baseCanonica);

        // %MO crudo (totalMO * cantidad, sin Round) vs canónico Multiply.
        decimal porcentajeCrudo = totalMOCrudo * .10m;
        decimal porcentajeCanonico = engine.Multiply(.10m, baseCanonica);

        Assert.AreEqual(3.001m, porcentajeCrudo);
        Assert.AreEqual(3.00m, porcentajeCanonico);
        Assert.AreNotEqual(porcentajeCanonico, porcentajeCrudo,
            "divergencia documentada: el reporte muestra 3.001, el motor calcula 3.00");
    }

    [TestMethod]
    public void ImporteComponente_Crudo_DifiereDeMultiply()
    {
        var engine = new SoproCalculationEngine(2, 2, 4);

        // Réplica cruda de EscribirComponente (GeneradorExcelCatalogoMatrices.cs:263
        // y GeneradorPdfCatalogoMatrices.cs:395): importe = Cantidad × costoUnitario.
        decimal importeCrudo = 3m * 10.005m;
        decimal importeCanonico = engine.Multiply(3m, 10.005m);

        Assert.AreEqual(30.015m, importeCrudo);
        Assert.AreEqual(30.03m, importeCanonico);
        Assert.AreNotEqual(importeCanonico, importeCrudo,
            "divergencia documentada: el catálogo muestra 30.015, el motor calcula 30.03");
    }

    [TestMethod]
    public void TotalConcepto_Crudo_DifiereDeMultiply()
    {
        var engine = new SoproCalculationEngine(2, 2, 4);

        // Réplica cruda de GeneradorExcelAPU.cs:413: total = Cantidad × PU guardado.
        decimal totalCrudo = 7m * 10.005m;
        decimal totalCanonico = engine.Multiply(7m, 10.005m);

        Assert.AreEqual(70.035m, totalCrudo);
        Assert.AreEqual(70.07m, totalCanonico);
        Assert.AreNotEqual(totalCanonico, totalCrudo,
            "divergencia documentada: el encabezado APU muestra 70.035, el motor calcula 70.07");
    }

    [TestMethod]
    public void FallbackPresupuesto_Crudo_DifiereDeMultiply()
    {
        var engine = new SoproCalculationEngine(2, 2, 4);

        // Réplica cruda del fallback (GeneradorExcelPresupuesto.cs:353 y
        // GeneradorPdfPresupuesto.cs:418): la condición real es ImporteTotal > 0,
        // por lo que el fallback sustituye tanto el cero como los negativos.
        static decimal FallbackCrudo(decimal importeGuardado, decimal cantidad, decimal cdu, decimal factor) =>
            importeGuardado > 0m ? importeGuardado : cantidad * cdu * factor;

        Assert.AreEqual(100.005m, FallbackCrudo(0m, 3m, 33.335m, 1.0m));

        // Caso contablemente relevante tras N7-2 (negativos conservados en la
        // canónica): un importe guardado negativo se sustituye por el fallback crudo.
        Assert.AreEqual(100.005m, FallbackCrudo(-50m, 3m, 33.335m, 1.0m),
            "ImporteTotal <= 0 dispara el fallback: el reporte sustituye el −50 guardado");

        decimal fallbackCanonico = engine.Multiply(3m, engine.RoundAmount(33.335m * 1.0m));
        Assert.AreEqual(100.02m, fallbackCanonico);
        Assert.AreNotEqual(fallbackCanonico, FallbackCrudo(0m, 3m, 33.335m, 1.0m),
            "divergencia documentada: el fallback muestra 100.005, el motor calcula 100.02");
    }

    [TestMethod]
    public void FallbackPrecioUnitario_Crudo_DifiereDeMultiply()
    {
        var engine = new SoproCalculationEngine(2, 2, 4);

        // Réplica cruda del fallback de PrecioUnitario (GeneradorExcelPresupuesto.cs:350
        // y GeneradorPdfPresupuesto.cs:417): PU > 0 ? guardado : CDU × factor, sin redondeo.
        static decimal PuFallbackCrudo(decimal puGuardado, decimal cdu, decimal factor) =>
            puGuardado > 0m ? puGuardado : cdu * factor;

        Assert.AreEqual(33.335m, PuFallbackCrudo(0m, 33.335m, 1.0m));
        Assert.AreEqual(33.335m, PuFallbackCrudo(-5m, 33.335m, 1.0m),
            "PU <= 0 dispara el fallback, incluido un PU negativo guardado");
        Assert.AreEqual(33.34m, engine.Multiply(1m, 33.335m));
    }

    [TestMethod]
    public void IvaDeTotales_Crudo_DifiereDeRedondeoExplicito()
    {
        var engine = new SoproCalculationEngine(2, 2, 4);

        // Réplica cruda de totales (GeneradorExcelPresupuesto.cs:377-379 y
        // GeneradorPdfPresupuesto.cs:364-366): subtotal crudo + IVA sin redondeo.
        decimal subtotalCrudo = 100.005m;
        decimal ivaCrudo = subtotalCrudo * 16m / 100m;

        decimal subtotalCanonico = engine.RoundAmount(100.005m);
        decimal ivaCanonico = engine.RoundAmount(subtotalCanonico * 16m / 100m);

        Assert.AreEqual(16.0008m, ivaCrudo);
        Assert.AreEqual(16.00m, ivaCanonico);
        Assert.AreNotEqual(ivaCanonico, ivaCrudo,
            "divergencia documentada: el reporte muestra 16.0008, el motor calcula 16.00");
    }
}
