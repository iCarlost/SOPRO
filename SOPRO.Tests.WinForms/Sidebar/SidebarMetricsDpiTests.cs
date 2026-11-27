using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SOPRO.WinForms.Helpers;

namespace SOPRO.Tests.WinForms.Sidebar;

/// <summary>
/// Contrato de geometría del sidebar (issue #193): las métricas son puras y
/// se escalan con UNA regla (techo de px*dpi/96) desde valores base a 96 DPI.
/// Se fija la fórmula exacta en cada DPI objetivo y las invariantes que hacen
/// usable el lateral (píldora dentro de la fila, jerarquía por sangría,
/// chevron y botón de colapso proporcionados). No depende de UI ni de DPI del
/// equipo: <see cref="SidebarMetrics.Crear"/> recibe el DPI explícito.
/// </summary>
[TestClass]
public class SidebarMetricsDpiTests
{
    private static readonly int[] DpisObjetivo = { 96, 120, 144, 192 };

    [TestMethod]
    public void Escalado_UsaTechoDePixelesVecinosA96()
    {
        Assert.AreEqual(34, SidebarMetrics.Escalar(34, 96));
        Assert.AreEqual(43, SidebarMetrics.Escalar(34, 120));  // 42.5 → 43
        Assert.AreEqual(51, SidebarMetrics.Escalar(34, 144));  // 51 exacto
        Assert.AreEqual(68, SidebarMetrics.Escalar(34, 192));
    }

    [TestMethod]
    public void Escalado_DpiInvalido_CaeA96()
    {
        Assert.AreEqual(34, SidebarMetrics.Escalar(34, 0));
        Assert.AreEqual(34, SidebarMetrics.Escalar(34, -120));
    }

    [TestMethod]
    public void Alturas_CrecenConElDpi_YLaFilaColapsadaEsMasCompacta()
    {
        int altoFilaPrevio = 0;
        foreach (int dpi in DpisObjetivo)
        {
            var m = SidebarMetrics.Crear(dpi);

            Assert.IsTrue(m.AltoFila > altoFilaPrevio,
                $"El alto de fila debe crecer monótonamente con el DPI (dpi {dpi}).");
            Assert.IsTrue(m.AltoFilaColapsada < m.AltoFila,
                $"El modo colapsado debe seguir siendo más compacto que el expandido (dpi {dpi}).");
            Assert.IsTrue(m.AltoHeader >= m.LadoBotonColapso + 2 * m.MargenBotonColapso,
                $"El header debe contener al botón de colapso con aire (dpi {dpi}).");

            altoFilaPrevio = m.AltoFila;
        }
    }

    [TestMethod]
    public void Pildora_CabeInsertadaEnSuFila_EnTodoDpi()
    {
        foreach (int dpi in DpisObjetivo)
        {
            var m = SidebarMetrics.Crear(dpi);

            Assert.IsTrue(m.AcolchadoPildoraVertical > 0, $"La píldora debe quedar insertada (dpi {dpi}).");
            Assert.AreEqual(m.AltoFila - 2 * m.AcolchadoPildoraVertical,
                SidebarRenderer.RectPildora(new Rectangle(0, 0, 400, m.AltoFila), m, 1, colapsado: false).Height,
                $"La píldora debe respetar el acolchado vertical simétrico (dpi {dpi}).");
            Assert.IsTrue(2 * m.RadioPildora <= m.AltoFila - 2 * m.AcolchadoPildoraVertical,
                $"El radio debe caber en la píldora sin deformarse (dpi {dpi}).");
        }
    }

    [TestMethod]
    public void Jerarquia_LaSangriaDeHojasCrecePorNivel_YArrancaEnElMargen()
    {
        foreach (int dpi in DpisObjetivo)
        {
            var m = SidebarMetrics.Crear(dpi);

            Assert.AreEqual(m.MargenLateral, m.SangriaHoja(1),
                $"La primera generación de hojas arranca en el margen lateral (dpi {dpi}).");
            Assert.IsTrue(m.SangriaHoja(2) > m.SangriaHoja(1),
                $"El segundo nivel debe indentarse más que el primero (dpi {dpi}).");
            Assert.IsTrue(m.SangriaHoja(3) > m.SangriaHoja(2),
                $"La jerarquía debe seguir creciendo por nivel (dpi {dpi}).");
        }
    }

    [TestMethod]
    public void ChevronYBotonColapso_MantienenProporcionEnTodoDpi()
    {
        foreach (int dpi in DpisObjetivo)
        {
            var m = SidebarMetrics.Crear(dpi);

            Assert.IsTrue(m.AnchoChevron >= m.AltoChevron,
                $"El chevron debe ser al menos tan ancho como alto (dpi {dpi}).");
            Assert.IsTrue(m.LadoBotonColapso < m.AltoHeader,
                $"El botón de colapso debe ser pequeño dentro del header (dpi {dpi}).");
            Assert.IsTrue(m.MargenBotonColapso > 0,
                $"El botón no debe pegarse al borde (dpi {dpi}).");
        }
    }

    [TestMethod]
    public void PildoraColapsada_QuedaCuadradaYCentrada()
    {
        foreach (int dpi in DpisObjetivo)
        {
            var m = SidebarMetrics.Crear(dpi);
            var fila = new Rectangle(0, 0, 4 * m.AltoFilaColapsada, m.AltoFilaColapsada);
            var pildora = SidebarRenderer.RectPildora(fila, m, 1, colapsado: true);

            Assert.AreEqual(m.AltoFilaColapsada - 2 * m.AcolchadoPildoraVertical, pildora.Width,
                $"En modo colapsado la píldora es un cuadrado para el emoji (dpi {dpi}).");
            Assert.AreEqual((fila.Width - pildora.Width) / 2, pildora.Left - fila.Left,
                $"La píldora colapsada debe centrarse horizontalmente (dpi {dpi}).");
        }
    }
}
