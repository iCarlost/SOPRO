using System;
using System.Drawing;
using System.Reflection;
using System.Windows.Forms;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SOPRO.WinForms.Helpers;
using SOPRO.WinForms.UI.Controls;

namespace SOPRO.Tests.WinForms.Ribbon;

/// <summary>
/// Regresión del issue #186 (rayas de 1 px en Recalcular/Ajustar): el pintado
/// de <see cref="SoproButton"/> debe ser IDEMPOTENTE — un repintado PARCIAL
/// (franja invalidada por el sistema) debe producir exactamente los mismos
/// píxeles que el repintado completo para el mismo estado visible.
///
/// El defecto: OnPaint leía Cursor.Position/Control.MouseButtons en el momento
/// del pintado; una franja repintada con el cursor encima usaba el tinte de
/// hover (#EAF2FF) sobre una cara pintada en reposo (#FFFFFF) — línea casi
/// imperceptible que el hover "borraba" y que podía reaparecer en otro botón.
///
/// Todo se renderiza a bitmaps en memoria con el propio OnPaint del control;
/// NO depende del monitor ni de la posición del cursor físico: el estado
/// hover/presionado se inyecta disparando los eventos protegidos
/// OnMouseEnter/OnMouseLeave/OnMouseDown/OnMouseCaptureChanged por reflexión.
/// </summary>
[STATestClass]
[DoNotParallelize]
public class SoproButtonRepaintParcialTests
{
    private static readonly Color FondoHoverEsperado = SoproTheme.RibbonClaro.Hover;
    private static readonly Color FondoPresionadoEsperado = SoproTheme.RibbonClaro.Presionado;

    [TestMethod]
    public void MouseEnter_Simulado_PintaFondoHover_AunqueElCursorFisicoEsteLejos()
    {
        using var boton = CrearBotonLarge();

        // El cursor físico NO está sobre el botón (los tests corren con el
        // escritorio real, pero el botón está fuera de pantalla y el cursor
        // donde lo dejó el usuario). Con el DEFECTO, el pintado ignoraba el
        // MouseEnter y usaba el reposo.
        InvocarProtectorado(boton, "OnMouseEnter");
        ProcesarMensajes();

        using var bitmap = Renderizar(boton);
        Assert.AreEqual(
            FondoHoverEsperado.ToArgb(),
            PixelDeFondo(bitmap).ToArgb(),
            "Tras OnMouseEnter el fondo debe ser el tinte de hover aunque el cursor físico esté lejos.");
    }

    [TestMethod]
    public void RepintadoParcial_ConsistenteConRepintadoCompleto_EnHover()
    {
        using var boton = CrearBotonLarge();

        // Cara completa pintada con hover (estado objetivo).
        InvocarProtectorado(boton, "OnMouseEnter");
        ProcesarMensajes();
        using var objetivo = Renderizar(boton);

        // Cara obsoleta en REPOSO: se limpia el estado y se vuelve a pintar.
        InvocarProtectorado(boton, "OnMouseLeave");
        ProcesarMensajes();
        using var obsoleta = Renderizar(boton);

        // La franja central del botón queda "sucia" (como la exposición de una
        // ventana tapadera) y se repinta PARCIALMENTE con el estado de hover.
        // Con el defecto la franja usaba hover mientras el resto seguía en
        // reposo; con la corrección el estado es seguido y la franja repintada
        // en reposo coincide con el render completo de reposo.
        int yFranja = boton.ClientSize.Height / 2;
        var franja = new Rectangle(0, yFranja, boton.ClientSize.Width, 1);
        using var reparada = Renderizar(boton, franja);
        InvocarProtectorado(boton, "OnMouseEnter");
        ProcesarMensajes();
        for (int x = 0; x < boton.ClientSize.Width; x++)
            Assert.AreEqual(
                obsoleta.GetPixel(x, yFranja).ToArgb(),
                reparada.GetPixel(x, yFranja).ToArgb(),
                $"La franja repintada parcialmente debe coincidir con el render completo del MISMO estado (x={x}).");

        // Y el estado objetivo sigue siendo alcanzable con un repintado completo.
        using var objetivo2 = Renderizar(boton);
        Assert.IsTrue(
            Iguales(objetivo, objetivo2),
            "El repintado completo debe ser determinista.");
    }

    [TestMethod]
    public void PerdidaDeCaptura_LimpiaElEstadoPresionado()
    {
        using var boton = CrearBotonLarge();
        InvocarProtectorado(boton, "OnMouseEnter");
        InvocarProtectorado(boton, "OnMouseDown", new MouseEventArgs(MouseButtons.Left, 1, 10, 10, 0));

        using var presionado = Renderizar(boton);
        Assert.AreEqual(
            FondoPresionadoEsperado.ToArgb(),
            PixelDeFondo(presionado).ToArgb(),
            "Con hover + botón izquierdo presionado el fondo debe ser el tinte presionado.");

        InvocarProtectorado(boton, "OnMouseCaptureChanged", EventArgs.Empty);
        using var liberado = Renderizar(boton);
        Assert.AreEqual(
            FondoHoverEsperado.ToArgb(),
            PixelDeFondo(liberado).ToArgb(),
            "Al perder la captura el botón debe volver al tinte de hover, no quedar presionado.");
    }

    [TestMethod]
    public void RepintadoParcial_EnReposo_ProduceReposo_AunqueElEstadoLuegoCambie()
    {
        // Escenario del reporte: la franja se repinta y DESPUÉS llega el
        // cambio de estado. La franja debe reflejar el estado vigente al
        // pintar, y el repintado completo posterior unifica la cara.
        using var boton = CrearBotonLarge();
        InvocarProtectorado(boton, "OnMouseLeave");
        ProcesarMensajes();
        using var reposo = Renderizar(boton);

        int yFranja = boton.ClientSize.Height / 2;
        using var franjaTardia = Renderizar(boton, new Rectangle(0, yFranja, boton.ClientSize.Width, 1));
        for (int x = 0; x < boton.ClientSize.Width; x++)
            Assert.AreEqual(
                reposo.GetPixel(x, yFranja).ToArgb(),
                franjaTardia.GetPixel(x, yFranja).ToArgb(),
                "Un repintado parcial en reposo no debe depender del cursor físico.");
    }

    // ═════════════════════════ Infraestructura ═════════════════════════

    private static BotonPintable CrearBotonLarge()
    {
        var boton = new BotonPintable
        {
            Size = new Size(74, 44),
            Text = "Recalcular",
            ForeColor = SoproTheme.RibbonClaro.Acento,
            BackColor = Color.Transparent,
            FlatStyle = FlatStyle.Flat,
            SoproLayout = SoproButtonLayout.Large,
            SoproIcon = SoproIconType.Recalcular,
            SoproIconSize = 20,
            TabStop = false,
            UseVisualStyleBackColor = false,
        };
        boton.FlatAppearance.BorderSize = 0;
        boton.FlatAppearance.MouseOverBackColor = FondoHoverEsperado;
        boton.FlatAppearance.MouseDownBackColor = FondoPresionadoEsperado;
        _ = boton.Handle;
        return boton;
    }

    /// <summary>
    /// Dispara un método protegido de <see cref="Control"/> (p. ej.
    /// OnMouseEnter) para mover el estado seguido del botón sin tocar el
    /// cursor físico ni requerir monitor.
    /// </summary>
    private static void InvocarProtectorado(Control control, string metodo, EventArgs? argumentos = null)
    {
        MethodInfo? info = typeof(Control).GetMethod(
            metodo,
            BindingFlags.Instance | BindingFlags.NonPublic,
            null,
            argumentos == null ? new[] { typeof(EventArgs) } : new[] { argumentos.GetType() },
            null);

        if (info == null)
            throw new InvalidOperationException($"No se resolvió Control.{metodo}.");

        info.Invoke(control, new[] { argumentos ?? EventArgs.Empty });
    }

    private static Bitmap Renderizar(BotonPintable boton, Rectangle? clip = null)
    {
        _ = boton.Handle;
        var area = clip ?? new Rectangle(Point.Empty, boton.ClientSize);
        var bitmap = new Bitmap(boton.ClientSize.Width, boton.ClientSize.Height);
        using (Graphics graphics = Graphics.FromImage(bitmap))
            boton.PintarEn(graphics, area);
        return bitmap;
    }

    /// <summary>
    /// Subclase que expone el pintado protegido (mismo patrón que
    /// <c>SoproButtonRenderingTests.BotonSoproPintable</c>).
    /// </summary>
    private sealed class BotonPintable : SoproButton
    {
        public void PintarEn(Graphics graphics, Rectangle area)
            => OnPaint(new PaintEventArgs(graphics, area));
    }

    /// <summary>Pixel de fondo puro del botón Large (esquina sin contenido).</summary>
    private static Color PixelDeFondo(Bitmap bitmap) => bitmap.GetPixel(5, 5);

    private static bool Iguales(Bitmap primero, Bitmap segundo)
    {
        if (primero.Size != segundo.Size) return false;
        for (int y = 0; y < primero.Height; y++)
            for (int x = 0; x < primero.Width; x++)
                if (primero.GetPixel(x, y).ToArgb() != segundo.GetPixel(x, y).ToArgb())
                    return false;
        return true;
    }

    private static void ProcesarMensajes()
    {
        for (int i = 0; i < 3; i++)
            System.Windows.Forms.Application.DoEvents();
    }
}
