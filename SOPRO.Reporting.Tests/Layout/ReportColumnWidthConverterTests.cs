using System;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SOPRO.Reporting.Layout;

namespace SOPRO.Reporting.Tests.Layout;

/// <summary>
/// Tests de <see cref="ReportColumnWidthConverter"/>: paridad con el reparto
/// legacy (px→cm proporcional al área útil con la última columna absorbiendo el
/// resto) y del factor px→Excel (px/7, mínimo 4).
/// </summary>
[TestClass]
public class ReportColumnWidthConverterTests
{
    private const double UsableCm = 19.59; // Letter 21.59 - 2×1.0 (paridad ReportPageLayoutHelper)
    private const double Tolerancia = 1e-9;

    [TestMethod]
    public void PxToCm_AnchoUnico_ReproduceFormulaLegacyProporcional()
    {
        double esperado = 19.59 * 110 / 710.0;

        double actual = ReportColumnWidthConverter.PxToCm(110, UsableCm, 710);

        Assert.AreEqual(esperado, actual, Tolerancia);
    }

    [TestMethod]
    public void PxToCm_AnchoUnico_AplicaMinimoDe24Px()
    {
        double muyEstrecho = ReportColumnWidthConverter.PxToCm(1, UsableCm, 100);
        double minimo = ReportColumnWidthConverter.PxToCm(24, UsableCm, 100);

        Assert.AreEqual(minimo, muyEstrecho, Tolerancia,
            "Un ancho por debajo de 24px debe tratarse como 24px (paridad legacy).");
    }

    [TestMethod]
    public void PxToCm_Lista_SumaIgualAlAreaUtil()
    {
        int[] anchos = { 110, 300, 80, 140, 80 };

        double[] cm = ReportColumnWidthConverter.PxToCm(anchos, UsableCm);

        Assert.AreEqual(UsableCm, cm.Sum(), Tolerancia,
            "La suma de anchos PDF debe ser exactamente el área útil.");
    }

    [TestMethod]
    public void PxToCm_Lista_NoExcedeElAreaUtil()
    {
        int[] anchos = { 45, 80, 280, 55, 80, 90, 110 };

        double[] cm = ReportColumnWidthConverter.PxToCm(anchos, UsableCm);

        Assert.IsTrue(cm.Sum() <= UsableCm + Tolerancia,
            $"La suma ({cm.Sum()}) no debe exceder el área útil ({UsableCm}).");
    }

    [TestMethod]
    public void PxToCm_Lista_UltimaColumnaAbsorbeLaDiferencia()
    {
        int[] anchos = { 110, 300, 80, 140, 80 };

        double[] cm = ReportColumnWidthConverter.PxToCm(anchos, UsableCm);

        double restoEsperado = UsableCm - cm.Take(cm.Length - 1).Sum();
        Assert.AreEqual(restoEsperado, cm[^1], Tolerancia,
            "La última columna debe absorber la diferencia hasta el área útil.");
    }

    [TestMethod]
    public void PxToCm_Lista_RespetaMinimoPorColumna_CuandoEsFactible()
    {
        // Tres columnas estrechas seguidas de una enorme: el mínimo (1.2 cm) es
        // factible porque la última columna absorbe el resto.
        int[] anchos = { 24, 24, 24, 1000 };

        double[] cm = ReportColumnWidthConverter.PxToCm(anchos, UsableCm);

        for (int i = 0; i < 3; i++)
            Assert.AreEqual(ReportColumnWidthConverter.DefaultPdfMinimumWidthCm, cm[i], Tolerancia,
                $"La columna {i} debe respetar el mínimo de {ReportColumnWidthConverter.DefaultPdfMinimumWidthCm} cm.");
        Assert.AreEqual(UsableCm, cm.Sum(), Tolerancia);
    }

    [TestMethod]
    public void PxToCm_Lista_MuchasColumnasEstrechas_SumaExactaYNoDesborda()
    {
        int[] anchos = Enumerable.Repeat(24, 12).ToArray();

        double[] cm = ReportColumnWidthConverter.PxToCm(anchos, UsableCm);

        Assert.AreEqual(12, cm.Length);
        Assert.IsTrue(cm.All(w => w >= ReportColumnWidthConverter.DefaultPdfMinimumWidthCm - Tolerancia),
            "Con 12 columnas estrechas el mínimo de 1.2 cm sigue siendo factible.");
        Assert.IsTrue(cm.Sum() <= UsableCm + Tolerancia, "No debe desbordar el área útil.");
        Assert.AreEqual(UsableCm, cm.Sum(), Tolerancia);
    }

    [TestMethod]
    public void PxToCm_ListaVacia_DevuelveVacio()
    {
        double[] cm = ReportColumnWidthConverter.PxToCm(Array.Empty<int>(), UsableCm);

        Assert.AreEqual(0, cm.Length);
    }

    [TestMethod]
    public void PxToExcelWidth_ReproduceFactorLegacy()
    {
        Assert.AreEqual(10d, ReportColumnWidthConverter.PxToExcelWidth(70), Tolerancia);
        Assert.AreEqual(20d, ReportColumnWidthConverter.PxToExcelWidth(140), Tolerancia);
    }

    [TestMethod]
    public void PxToExcelWidth_AplicaMinimoDe4()
    {
        Assert.AreEqual(4d, ReportColumnWidthConverter.PxToExcelWidth(7), Tolerancia);
        Assert.AreEqual(4d, ReportColumnWidthConverter.PxToExcelWidth(0), Tolerancia);
    }

    [TestMethod]
    public void PxToExcelWidths_ConvierteCadaColumna()
    {
        double[] anchos = ReportColumnWidthConverter.PxToExcelWidths(new[] { 70, 7, 140 });

        CollectionAssert.AreEqual(new[] { 10d, 4d, 20d }, anchos);
    }

    [TestMethod]
    public void GetLetterUsableWidthCm_CoincideConElRendererLegacy()
    {
        Assert.AreEqual(UsableCm, ReportColumnWidthConverter.GetLetterUsableWidthCm(), Tolerancia);
        Assert.AreEqual(27.94 - 2.0, ReportColumnWidthConverter.GetLetterUsableWidthCm(true), Tolerancia);
    }
}
