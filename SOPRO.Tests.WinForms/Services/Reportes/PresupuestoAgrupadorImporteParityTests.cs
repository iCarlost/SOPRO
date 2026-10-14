using Microsoft.VisualStudio.TestTools.UnitTesting;
using SOPRO.Application.Models.Presupuesto;
using SOPRO.Core.Entities;
using SOPRO.WinForms.Services;

namespace SOPRO.Tests.WinForms.Services.Reportes;

/// <summary>
/// Regresión: el PDF (<see cref="GeneradorPdfPresupuesto.ResolverValorConceptoPdf"/>)
/// imprimía el Importe de los agrupadores SIEMPRE vacío, mientras Excel
/// (<see cref="GeneradorExcelPresupuesto.ResolverValorConcepto"/>) imprimía el
/// <c>ImporteTotal</c> cuando era &gt; 0. Este test fija la paridad EXACTA entre
/// ambos medios para una fila agrupador con importe &gt; 0 y con importe = 0.
/// </summary>
[TestClass]
public class PresupuestoAgrupadorImporteParityTests
{
    private const string ColumnaImporte = "ImporteTotal";

    private static ConceptoPresupuesto Agrupador(decimal importeTotal)
        => new()
        {
            EsAgrupador = true,
            Descripcion = "Grupo",
            Clave = "G-1",
            ImporteTotal = importeTotal,
        };

    [TestMethod]
    public void AgrupadorConImportePositivo_PdfYExcel_ImprimenElMismoImporte()
    {
        var concepto = Agrupador(1234.56m);

        var pdf = GeneradorPdfPresupuesto.ResolverValorConceptoPdf(
            concepto, ColumnaImporte, consecutivo: 1, factorPU: 1m, rowDisplay: null!, indent: string.Empty);
        var excel = GeneradorExcelPresupuesto.ResolverValorConcepto(
            concepto, ColumnaImporte, consecutivo: 1, factorPU: 1m, rowDisplay: null!, indent: string.Empty);

        Assert.AreEqual(excel, pdf, "PDF y Excel deben resolver el mismo valor para la fila agrupador.");
        Assert.AreEqual(1234.56m, pdf, "El agrupador con importe > 0 muestra su ImporteTotal.");
    }

    [TestMethod]
    public void AgrupadorSinImporte_PdfYExcel_QuedanVacios()
    {
        // El alias legacy "Importe" debe comportarse igual que "ImporteTotal".
        var concepto = Agrupador(0m);

        var pdf = GeneradorPdfPresupuesto.ResolverValorConceptoPdf(
            concepto, "Importe", consecutivo: 1, factorPU: 1m, rowDisplay: null!, indent: string.Empty);
        var excel = GeneradorExcelPresupuesto.ResolverValorConcepto(
            concepto, "Importe", consecutivo: 1, factorPU: 1m, rowDisplay: null!, indent: string.Empty);

        Assert.AreEqual(excel, pdf, "PDF y Excel deben coincidir también con importe = 0.");
        Assert.AreEqual(string.Empty, pdf, "Un agrupador sin importe no imprime nada.");
    }
}
