using Microsoft.VisualStudio.TestTools.UnitTesting;
using Sopro.Calculation;

namespace SOPRO.Tests.Services.Reportes;

/// <summary>
/// [N7-6] Caracterización de la aritmética cruda de reportes WinForms (APU y
/// catálogos): multiplicaciones Cantidad × precio sin redondeo de pantalla.
/// Estos tests congelan la divergencia contra el motor canónico; la migración
/// requiere su propio slice con decisión de redondeo visible (filas 22-24 N0-TABLA).
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
            "divergencia congelada: el reporte muestra 3.001, el motor calcula 3.00");
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
            "divergencia congelada: el catálogo muestra 30.015, el motor calcula 30.03");
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
            "divergencia congelada: el encabezado APU muestra 70.035, el motor calcula 70.07");
    }

    [TestMethod]
    public void FallbackPresupuesto_Crudo_DifiereDeMultiply()
    {
        var engine = new SoproCalculationEngine(2, 2, 4);

        // Réplica cruda del fallback ImporteTotal == 0 (GeneradorExcelPresupuesto.cs:353
        // y GeneradorPdfPresupuesto.cs:418): Cantidad × CDU × factorPU sin redondeo.
        decimal fallbackCrudo = 3m * 33.335m * 1.0m;
        decimal fallbackCanonico = engine.Multiply(3m, engine.RoundAmount(33.335m * 1.0m));

        Assert.AreEqual(100.005m, fallbackCrudo);
        Assert.AreEqual(100.02m, fallbackCanonico);
        Assert.AreNotEqual(fallbackCanonico, fallbackCrudo,
            "divergencia congelada: el fallback muestra 100.005, el motor calcula 100.02");
    }
}
