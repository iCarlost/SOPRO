using System;
using System.Drawing;
using System.Windows.Forms;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SOPRO.WinForms.Helpers;

namespace SOPRO.Tests.WinForms.Sidebar;

/// <summary>
/// Contrato de PINTADO del sidebar (issue #193), renderizado en bitmap sin
/// controles reales ni capturas de pantalla: las funciones de
/// <see cref="SidebarRenderer"/> son puras, así que basta un Graphics de
/// memoria. Se fijan los estados (reposo, hover, activo con/sin foco) y el
/// chevron de sección SIN fijar píxeles incidentales ni goldens frágiles.
/// </summary>
[TestClass]
public class SidebarRendererTests
{
    private const int AnchoFila = 260;

    private static SidebarEscala Metricas() => SidebarMetrics.Crear(96);

    private static Rectangle Fila() => new(0, 0, AnchoFila, 34);

    /// <summary>
    /// Punto de muestreo seguro: interior derecho de la píldora, más allá del
    /// texto (que arranca a la izquierda). Ahí solo llega el relleno.
    /// </summary>
    private static Point PuntoMuestreo(Rectangle pildora)
        => new(pildora.Right - 5, pildora.Top + pildora.Height / 2);

    private static Bitmap Renderizar(Action<Graphics> pintar)
    {
        var bitmap = new Bitmap(AnchoFila, 34);
        using (var graphics = Graphics.FromImage(bitmap))
            pintar(graphics);
        return bitmap;
    }

    private static void PintarHoja(Graphics graphics, bool seleccionada, bool conFoco, bool enHover, int nivel = 1)
    {
        SidebarRenderer.PintarFondo(graphics, Fila());
        SidebarRenderer.PintarHoja(
            graphics, Fila(), "📄 Hoja de Presupuesto",
            new Font("Segoe UI", 10F), Metricas(), nivel,
            seleccionada, conFoco, enHover, colapsado: false);
    }

    [TestMethod]
    public void HojaEnReposo_FondoPuroSinPildora()
    {
        using var bitmap = Renderizar(b => PintarHoja(b, seleccionada: false, conFoco: false, enHover: false));

        var pildora = SidebarRenderer.RectPildora(Fila(), Metricas(), 1, colapsado: false);
        var punto = PuntoMuestreo(pildora);
        Assert.AreEqual(SidebarPaleta.Fondo, bitmap.GetPixel(punto.X, punto.Y),
            "En reposo la hoja no debe pintar píldora: solo la superficie fría del lateral.");
    }

    [TestMethod]
    public void HojaEnHover_PintaTinteDiferenteDelReposo()
    {
        using var bitmap = Renderizar(b => PintarHoja(b, seleccionada: false, conFoco: false, enHover: true));

        var pildora = SidebarRenderer.RectPildora(Fila(), Metricas(), 1, colapsado: false);
        var punto = PuntoMuestreo(pildora);
        Color centro = bitmap.GetPixel(punto.X, punto.Y);
        Assert.AreEqual(SidebarPaleta.FondoHover, centro,
            "El hover debe diferenciarse del reposo con su tinte propio.");
        Assert.AreNotEqual(SidebarPaleta.Fondo, centro,
            "El hover no debe dejarse invisible sobre la superficie del lateral.");
    }

    [TestMethod]
    public void HojaActiva_PintaLaPildoraDelTinteAzul_TengaOFNoFocoElArbol()
    {
        // Activo sin foco (HideSelection=false: la selección sigue visible al
        // perder el foco) y activo con foco comparten el relleno.
        using var sinFoco = Renderizar(b => PintarHoja(b, seleccionada: true, conFoco: false, enHover: false));
        using var conFoco = Renderizar(b => PintarHoja(b, seleccionada: true, conFoco: true, enHover: false));

        var pildora = SidebarRenderer.RectPildora(Fila(), Metricas(), 1, colapsado: false);
        var punto = PuntoMuestreo(pildora);

        Assert.AreEqual(SidebarPaleta.PildoraActiva, sinFoco.GetPixel(punto.X, punto.Y),
            "El activo sin foco debe conservar la píldora del tinte azul.");
        Assert.AreEqual(SidebarPaleta.PildoraActiva, conFoco.GetPixel(punto.X, punto.Y),
            "El activo con foco debe conservar el mismo relleno.");
    }

    [TestMethod]
    public void HojaActiva_ElBordeDeAcentoSoloApareceConFoco()
    {
        using var sinFoco = Renderizar(b => PintarHoja(b, seleccionada: true, conFoco: false, enHover: false));
        using var conFoco = Renderizar(b => PintarHoja(b, seleccionada: true, conFoco: true, enHover: false));

        var pildora = SidebarRenderer.RectPildora(Fila(), Metricas(), 1, colapsado: false);
        Assert.IsTrue(BordeDelLadoIzquierdoDifiere(sinFoco, conFoco, pildora),
            "El foco de teclado debe ser perceptible: el borde de acento aparece solo con foco.");
    }

    private static bool BordeDelLadoIzquierdoDifiere(Bitmap sinFoco, Bitmap conFoco, Rectangle pildora)
    {
        int x = pildora.Left;
        for (int y = pildora.Top; y < pildora.Bottom; y++)
            if (sinFoco.GetPixel(x, y) != conFoco.GetPixel(x, y))
                return true;
        return false;
    }

    [TestMethod]
    public void HojasDeNivelesDistintos_LaPildoraSeIndenta()
    {
        var fila = Fila();
        var nivel1 = SidebarRenderer.RectPildora(fila, Metricas(), 1, colapsado: false);
        var nivel2 = SidebarRenderer.RectPildora(fila, Metricas(), 2, colapsado: false);

        Assert.IsTrue(nivel2.Left > nivel1.Left,
            "La indentación debe hacer visible la jerarquía entre niveles.");
        Assert.IsTrue(nivel2.Right <= fila.Right - Metricas().MargenLateral,
            "La píldora nunca debe invadir el margen derecho.");
    }

    [TestMethod]
    public void Seccion_ElTextoSeNormalizaAMayusculas()
    {
        Assert.AreEqual("REPORTES", SidebarRenderer.TextoDeSeccion("Reportes"));
        Assert.AreEqual("📊 PRESUPUESTO", SidebarRenderer.TextoDeSeccion("📊 Presupuesto"));
        Assert.AreEqual(string.Empty, SidebarRenderer.TextoDeSeccion(null));
    }

    [TestMethod]
    public void Seccion_ElChevronCambiaDeOrientacionSegunExpansion()
    {
        var fila = Fila();
        var m = Metricas();
        var zona = SidebarRenderer.RectChevron(fila, m);

        using var expandida = Renderizar(b => SidebarRenderer.PintarChevron(b, zona, expandida: true));
        using var colapsada = Renderizar(b => SidebarRenderer.PintarChevron(b, zona, expandida: false));

        Assert.IsTrue(ZonaDifiere(expandida, colapsada, zona),
            "El chevron debe orientarse según el estado de la sección: abajo al expandir, derecha al colapsar.");
    }

    [TestMethod]
    public void Seccion_ColapsadaDibujaChevronYExpandidaTambien_PeroSobreZonasDiferentes()
    {
        // El rótulo de sección completo (texto + chevron) debe pintar algo en
        // la zona del chevron tanto expandido como colapsado.
        var fila = Fila();
        var m = Metricas();
        var zona = SidebarRenderer.RectChevron(fila, m);
        var fuente = new Font("Segoe UI", 8.5F, FontStyle.Bold);

        using var expandida = Renderizar(b => SidebarRenderer.PintarSeccion(b, fila, "Reportes", fuente, m, expandida: true, colapsado: false));
        using var colapsada = Renderizar(b => SidebarRenderer.PintarSeccion(b, fila, "Reportes", fuente, m, expandida: false, colapsado: false));

        Assert.IsTrue(ZonaDifiere(expandida, colapsada, zona));
    }

    [TestMethod]
    public void SeccionEnModoColapsado_NoDibujaChevron()
    {
        var fila = Fila();
        var m = Metricas();
        var zona = SidebarRenderer.RectChevron(fila, m);
        var fuente = new Font("Segoe UI", 8.5F, FontStyle.Bold);

        using var bitmap = Renderizar(b => SidebarRenderer.PintarSeccion(b, fila, "📑", fuente, m, expandida: true, colapsado: true));

        Assert.IsFalse(ZonaDifiere(bitmap, RenderizarVaciaDeSeccion(fuente, m), zona),
            "En modo colapsado la sección queda como emoji centrado, sin chevron.");
    }

    private static Bitmap RenderizarVaciaDeSeccion(Font fuente, SidebarEscala m)
        => Renderizar(b => SidebarRenderer.PintarSeccion(b, Fila(), string.Empty, fuente, m, expandida: true, colapsado: true));

    private static bool ZonaDifiere(Bitmap a, Bitmap b, Rectangle zona)
    {
        for (int y = Math.Max(0, zona.Top); y < Math.Min(zona.Bottom, a.Height); y++)
            for (int x = Math.Max(0, zona.Left); x < Math.Min(zona.Right, a.Width); x++)
                if (a.GetPixel(x, y) != b.GetPixel(x, y))
                    return true;
        return false;
    }
}
