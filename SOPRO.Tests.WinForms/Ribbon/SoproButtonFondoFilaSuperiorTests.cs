using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SOPRO.WinForms.UI.Controls;

namespace SOPRO.Tests.WinForms.Ribbon;

/// <summary>
/// Regresión de composición del fondo en los layouts pintados a mano de
/// <see cref="SoproButton"/>: el relleno de fondo debe cubrir exactamente la
/// fila/columna del borde antes de activar el suavizado. Se renderiza el
/// <c>OnPaint</c> real sobre un bitmap sembrado con un color contrastante; no
/// es una captura de pantalla ni valida el resultado en un monitor.
/// </summary>
[STATestClass]
[DoNotParallelize] // System.Drawing/WinForms requieren un hilo STA dedicado.
public class SoproButtonFondoFilaSuperiorTests
{
    private static readonly Color Semilla = Color.FromArgb(255, 0, 255, 0);
    private static readonly Color Fondo = Color.FromArgb(255, 20, 30, 40);

    [TestMethod]
    public void Small_OnPaintCubreLasEsquinasSuperioresConElFondoExacto()
    {
        using BotonPintable boton = CrearBoton(SoproButtonLayout.Small, new Size(96, 28));

        using Bitmap render = Pintar(boton, aplicarRegion: false);

        Assert.AreEqual(
            Fondo.ToArgb(),
            render.GetPixel(0, 0).ToArgb(),
            "Small: la esquina superior izquierda (0,0) debe quedar exactamente en BackColor.");
        Assert.AreEqual(
            Fondo.ToArgb(),
            render.GetPixel(boton.Width - 1, 0).ToArgb(),
            "Small: la esquina superior derecha debe quedar exactamente en BackColor.");
        Assert.AreEqual(
            Fondo.ToArgb(),
            render.GetPixel(0, boton.Height - 1).ToArgb(),
            "Small: la esquina inferior izquierda debe quedar exactamente en BackColor.");
    }

    [TestMethod]
    public void Large_OnPaintRespetaRegionYCubreElTramoVisibleDeLaFilaSuperior()
    {
        using BotonPintable boton = CrearBoton(SoproButtonLayout.Large, new Size(96, 60));

        Assert.IsNotNull(
            boton.Region,
            "Precondición: el layout Large debe exponer una Region redondeada.");

        using Bitmap render = Pintar(boton, aplicarRegion: true);

        Assert.IsFalse(
            boton.Region!.IsVisible(0, 0),
            "Precondición: la esquina (0,0) del layout Large queda fuera de su Region redondeada.");
        Assert.AreEqual(
            Semilla.ToArgb(),
            render.GetPixel(0, 0).ToArgb(),
            "Large: el clip de la Region debe dejar intacta la esquina no visible.");

        int puntosVisibles = 0;
        for (int x = 0; x < boton.Width; x++)
        {
            if (!boton.Region.IsVisible(x, 0))
                continue;

            puntosVisibles++;
            Assert.AreEqual(
                Fondo.ToArgb(),
                render.GetPixel(x, 0).ToArgb(),
                $"Large: el punto visible ({x},0) de la fila superior debe quedar exactamente en BackColor.");
        }

        Assert.IsTrue(
            puntosVisibles > 0,
            "Large: la Region debe dejar visible al menos un punto de la fila superior.");
    }

    // ═════════════════════════════ Infraestructura ═════════════════════════════

    private static BotonPintable CrearBoton(SoproButtonLayout layout, Size tamano)
    {
        var boton = new BotonPintable
        {
            AutoSize = false,
            TabStop = false,
            UseVisualStyleBackColor = false,
            SoproLayout = layout,
            Size = tamano,
            Location = new Point(-32000, -32000),
            Text = string.Empty,
            BackColor = Fondo,
        };

        // Handle real para que la pintura resuelva el DPI y el puntero sin
        // depender de un formulario mostrado.
        _ = boton.Handle;
        return boton;
    }

    private static Bitmap Pintar(BotonPintable boton, bool aplicarRegion)
    {
        var bitmap = new Bitmap(boton.Width, boton.Height);
        using var graphics = Graphics.FromImage(bitmap);
        graphics.Clear(Semilla);

        if (aplicarRegion && boton.Region != null)
            graphics.SetClip(boton.Region, CombineMode.Replace);

        boton.Pintar(graphics, new Rectangle(Point.Empty, boton.ClientSize));
        return bitmap;
    }

    private sealed class BotonPintable : SoproButton
    {
        public void Pintar(Graphics graphics, Rectangle bounds)
            => OnPaint(new PaintEventArgs(graphics, bounds));
    }
}
