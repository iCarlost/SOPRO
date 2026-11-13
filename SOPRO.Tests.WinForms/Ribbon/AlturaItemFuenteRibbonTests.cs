using System.Drawing;
using System.Windows.Forms;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SOPRO.WinForms.Helpers;

namespace SOPRO.Tests.WinForms.Ribbon;

/// <summary>
/// Regresión del alto de fila del desplegable de fuente del ribbon (issue #117).
/// Contrato acordado con el lane de diseño:
/// <c>SoproTheme.CalcularItemHeightComboRibbon(int)</c> devuelve un alto en px
/// que cubre el texto medido (con holgura) y una base mínima legible, y crece
/// con la fuente. La medición se hace con <see cref="TextRenderer"/> en el mismo
/// hilo STA, sin depender de la captura de un monitor real.
/// </summary>
[STATestClass]
[DoNotParallelize] // TextRenderer/WinForms requieren un hilo STA dedicado.
public class AlturaItemFuenteRibbonTests
{
    private const string TextoReferencia = "Mg";

    [TestMethod]
    public void CalcularAlturaItemFuenteRibbon_9pt_CubreElTextoYLaBaseMinima()
    {
        using var fuente = new Font("Segoe UI", 9F, FontStyle.Regular, GraphicsUnit.Point);

        int altoTexto = TextRenderer.MeasureText(TextoReferencia, fuente).Height;
        int alto = SoproTheme.CalcularItemHeightComboRibbon(altoTexto);

        Assert.IsTrue(
            alto >= altoTexto + 2,
            $"El alto de fila ({alto} px) debe cubrir el texto de 9 pt ({altoTexto} px) con holgura.");
        Assert.IsTrue(
            alto >= 20,
            $"El alto de fila base ({alto} px) no puede quedar por debajo de 20 px.");
    }

    [TestMethod]
    public void CalcularAlturaItemFuenteRibbon_18pt_EsMasAltoQue9ptYCubreElTexto()
    {
        using var fuente9 = new Font("Segoe UI", 9F, FontStyle.Regular, GraphicsUnit.Point);
        using var fuente18 = new Font("Segoe UI", 18F, FontStyle.Regular, GraphicsUnit.Point);

        int alto9 = SoproTheme.CalcularItemHeightComboRibbon(TextRenderer.MeasureText(TextoReferencia, fuente9).Height);
        int alto18 = SoproTheme.CalcularItemHeightComboRibbon(TextRenderer.MeasureText(TextoReferencia, fuente18).Height);
        int altoTexto18 = TextRenderer.MeasureText(TextoReferencia, fuente18).Height;

        Assert.IsTrue(
            alto18 >= altoTexto18 + 2,
            $"El alto de fila ({alto18} px) debe cubrir el texto de 18 pt ({altoTexto18} px) con holgura.");
        Assert.IsTrue(
            alto18 >= 20,
            $"El alto de fila de 18 pt ({alto18} px) no puede quedar por debajo de 20 px.");
        Assert.IsTrue(
            alto18 > alto9,
            $"Una fuente mayor debe producir filas más altas (9 pt={alto9} px, 18 pt={alto18} px).");
    }
}
