using Microsoft.VisualStudio.TestTools.UnitTesting;
using SOPRO.Application.Services;
using SOPRO.Core.Entities;
using SOPRO.WinForms.Controls;

namespace SOPRO.Tests.WinForms.Matrices;

/// <summary>
/// Regresión: <c>PanelMatricesEmbebido</c> mantenía una COPIA de la fórmula del
/// factor de P.U. que además comparaba el modo de cálculo de forma sensible a
/// mayúsculas (<c>== "SobreCD"</c>), divergiendo del servicio. Ahora
/// <see cref="PanelMatricesEmbebido.CalcularFactorPU"/> delega en
/// <see cref="BudgetPricingService.CalculateFactor"/> (única fuente de verdad);
/// este test fija que ambos dan EXACTAMENTE el mismo factor para los distintos
/// modos, incluidos "SobreCD"/"sobrecd"/"SobreCD " y la cascada.
/// </summary>
[TestClass]
public class PanelMatricesFactorPuParityTests
{
    private static Proyecto ProyectoConModo(string modo) => new()
    {
        PorcentajeIndirectosCentral = 10m,
        PorcentajeIndirectosCampo = 5m,
        PorcentajeFinanciamiento = 3m,
        PorcentajeUtilidad = 8m,
        PorcentajeCargosAdicionales = 2m,
        ModoCalculoPorcentajes = modo,
    };

    [DataTestMethod]
    [DataRow("SobreCD")]
    [DataRow("sobrecd")]
    [DataRow("SobreCD ")]
    [DataRow("Cascada")]
    [DataRow("Acumulables")]
    [DataRow("")]
    public void PanelYService_DanElMismoFactor(string modo)
    {
        var proyecto = ProyectoConModo(modo);

        var delPanel = PanelMatricesEmbebido.CalcularFactorPU(proyecto);
        var delServicio = BudgetPricingService.CalculateFactor(proyecto);

        Assert.AreEqual(delServicio, delPanel, $"modo='{modo}'");
    }

    [TestMethod]
    public void ModoSobreCD_EsInsensibleAMayusculas()
    {
        var sobreCd = PanelMatricesEmbebido.CalcularFactorPU(ProyectoConModo("SobreCD"));
        var sobreCdMinusculas = PanelMatricesEmbebido.CalcularFactorPU(ProyectoConModo("sobrecd"));

        Assert.AreEqual(sobreCd, sobreCdMinusculas);
        // SobreCD SUMA los porcentajes: 1 + (10+5+3+8+2)/100 = 1.28 (no los multiplica).
        Assert.AreEqual(1.28m, sobreCd);
    }
}
