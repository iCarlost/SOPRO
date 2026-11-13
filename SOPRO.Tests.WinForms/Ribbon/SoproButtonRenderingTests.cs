using System;
using System.Drawing;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SOPRO.WinForms.Helpers;
using SOPRO.WinForms.UI.Controls;

namespace SOPRO.Tests.WinForms.Ribbon;

/// <summary>
/// Regresiones de pintado de <see cref="SoproButton"/> introducidas por el
/// rediseño del ribbon (issue #117). Todo se renderiza a un bitmap en memoria
/// llamando al propio pintado del control; NO son capturas de pantalla ni
/// validan píxeles de un monitor real.
/// </summary>
[STATestClass]
[DoNotParallelize] // System.Drawing/WinForms requieren un hilo STA dedicado.
public class SoproButtonRenderingTests
{
    private static readonly Color ColorMuestra = Color.FromArgb(0x00, 0xC8, 0xFF);
    private static readonly Color FondoRibbon = Color.FromArgb(0x1E, 0x29, 0x3B);

    [TestMethod]
    public void SampleLetter_DibujaBarraSolidaDeCuatroFilasDentroDelCliente()
    {
        // Color brillante por encima del umbral IsDark (luminancia 0.40): así la
        // barra NO lleva filete de encuadre y sus píxeles son todos del color
        // exacto de muestra. Evita confundir el marco con la barra.
        Assert.IsTrue(
            Luminancia(ColorMuestra) > 0.40,
            "El color de muestra de la prueba debe superar el umbral IsDark.");

        using var boton = CrearBoton();
        using var fuente = new Font("Segoe UI", 9F, FontStyle.Regular, GraphicsUnit.Point);
        boton.Font = fuente;
        boton.SoproLayout = SoproButtonLayout.SampleLetter;
        boton.Size = new Size(74, 26);
        boton.Text = "A";
        boton.SoproCaption = string.Empty;
        boton.ForeColor = ColorMuestra;
        boton.BackColor = FondoRibbon;

        var cliente = new Rectangle(Point.Empty, boton.ClientSize);

        // 1) El render normal (OnPaint con AntiAlias) debe contener la barra:
        //    GDI+ suaviza las filas de borde, por lo que el núcleo exacto mide
        //    al menos 3 filas sólidas y queda íntegramente dentro del cliente.
        using Bitmap render = Dibujar(boton);
        Rectangle? nucleo = BuscarBloqueSolido(render, ColorMuestra, anchoMinimo: 6, alto: 3);
        Assert.IsNotNull(
            nucleo,
            "El render real del botón SampleLetter debe contener la barra de muestra.");
        Assert.IsTrue(
            cliente.Contains(nucleo!.Value),
            $"La barra {nucleo.Value} debe quedar completamente dentro del cliente {cliente}.");

        // 2) Geometría de la barra: el mismo método de pintado sin suavizado
        //    permite verificar que mide exactamente 4 filas y que tampoco se
        //    sale del área del cliente (el defecto era quedar pegada al borde).
        using Bitmap geometria = DibujarRecto(boton);
        Rectangle? barra = BuscarBloqueSolido(geometria, ColorMuestra, anchoMinimo: 6, alto: 4);
        Assert.IsNotNull(
            barra,
            "La barra de muestra debe ser un bloque sólido de exactamente 4 filas.");
        Assert.AreEqual(4, barra!.Value.Height, "La barra de muestra tiene exactamente 4 filas.");
        Assert.IsTrue(
            cliente.Contains(barra.Value),
            $"La barra {barra.Value} debe quedar completamente dentro del cliente {cliente}.");
        Assert.IsTrue(
            barra.Value.Top >= 1 && barra.Value.Bottom <= cliente.Bottom - 1,
            $"La barra {barra.Value} no debe tocar el marco superior/inferior de {cliente}.");
    }

    [TestMethod]
    public void IconoGrande_EscalaElBitmapConElDpiDelControl()
    {
        using var boton = CrearBoton();
        boton.SoproLayout = SoproButtonLayout.Large;
        boton.SoproIcon = SoproIconType.Recalcular;
        boton.SoproIconSize = 30;
        boton.Size = new Size(72, 60);
        boton.Text = "Recalcular";
        boton.ForeColor = Color.White;

        // HandleCreated re-rasteriza el icono con el DPI efectivo del control.
        _ = boton.Handle;

        int esperado = Math.Max(12, (int)Math.Round(30 * (boton.DeviceDpi / 96f)));

        Assert.AreEqual(30, boton.SoproIconSize, "El tamaño lógico del icono no debe cambiar con el DPI.");
        Assert.IsNotNull(boton.Image, "El icono grande debe rasterizarse al crear el handle.");
        Assert.AreEqual(
            esperado,
            boton.Image!.Width,
            $"A {boton.DeviceDpi} DPI el bitmap debe medir round(30 * {boton.DeviceDpi} / 96) px de ancho.");
        Assert.AreEqual(esperado, boton.Image.Height, "El bitmap del icono debe ser cuadrado.");

        if (boton.DeviceDpi == 96)
            Assert.AreEqual(30, boton.Image.Width, "A 96 DPI el icono lógico de 30 px se dibuja a 30 px.");
    }

    [TestMethod]
    public void IconosGrandes_UsanBordesSuavizados()
    {
        foreach (SoproIconType tipo in new[]
        {
            SoproIconType.Recalcular,
            SoproIconType.Depurar,
            SoproIconType.Consolidar,
            SoproIconType.Buscar
        })
        {
            using Image icono = SoproIconProvider.GetIcon(tipo, Color.White, 30);
            int pixelesDeBordeSuavizado = 0;

            for (int y = 0; y < icono.Height; y++)
            {
                for (int x = 0; x < icono.Width; x++)
                {
                    byte alpha = ((Bitmap)icono).GetPixel(x, y).A;
                    if (alpha > 0 && alpha < 255)
                        pixelesDeBordeSuavizado++;
                }
            }

            Assert.IsTrue(
                pixelesDeBordeSuavizado > 0,
                $"El icono {tipo} debe usar suavizado de bordes a 30 px.");
        }
    }

    [TestMethod]
    public void SampleSwatch_DibujaParcheDe12x12DentroDelBoton()
    {
        using var boton = CrearBoton();
        boton.SoproLayout = SoproButtonLayout.SampleSwatch;
        boton.Size = new Size(74, 26);
        boton.Text = string.Empty;
        boton.SoproCaption = string.Empty;
        boton.ForeColor = ColorMuestra;
        boton.BackColor = FondoRibbon;

        using Bitmap bitmap = Dibujar(boton);
        var cliente = new Rectangle(Point.Empty, boton.ClientSize);

        // Sin caption, PaintSample centra un bloque fijo de 12x12.
        int x = (boton.ClientSize.Width - 12) / 2;
        int y = (boton.ClientSize.Height - 12) / 2;
        var parche = new Rectangle(x, y, 12, 12);

        Assert.IsTrue(cliente.Contains(parche), $"La muestra {parche} debe caber en el cliente {cliente}.");

        // El borde de 1 px del swatch ocupa el perímetro; el interior debe ser
        // del color de muestra.
        for (int dx = 2; dx <= 9; dx++)
        {
            for (int dy = 2; dy <= 9; dy++)
            {
                Assert.AreEqual(
                    ColorMuestra.ToArgb(),
                    bitmap.GetPixel(parche.X + dx, parche.Y + dy).ToArgb(),
                    $"El parche de muestra debe ser sólido en ({parche.X + dx}, {parche.Y + dy}).");
            }
        }
    }

    // ═════════════════════════════ Infraestructura ═════════════════════════════

    private static BotonSoproPintable CrearBoton()
    {
        return new BotonSoproPintable
        {
            AutoSize = false,
            TabStop = false,
            UseVisualStyleBackColor = false,
        };
    }

    private static Bitmap Dibujar(BotonSoproPintable boton)
    {
        _ = boton.Handle;

        var bitmap = new Bitmap(boton.Width, boton.Height);
        using (Graphics graphics = Graphics.FromImage(bitmap))
            boton.PintarEn(graphics, new Rectangle(Point.Empty, boton.ClientSize));

        return bitmap;
    }

    private sealed class BotonSoproPintable : SoproButton
    {
        private static readonly System.Reflection.MethodInfo? PintarMuestraMethod =
            typeof(SoproButton).GetMethod(
                "PaintSample",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);

        public void PintarEn(Graphics graphics, Rectangle bounds)
            => OnPaint(new PaintEventArgs(graphics, bounds));

        public void PintarMuestra(Graphics graphics, Rectangle bounds)
        {
            if (PintarMuestraMethod == null)
                throw new InvalidOperationException("No se pudo resolver SoproButton.PaintSample.");

            PintarMuestraMethod.Invoke(this, new object[] { graphics, bounds });
        }
    }

    private static Bitmap DibujarRecto(BotonSoproPintable boton)
    {
        var bitmap = new Bitmap(boton.Width, boton.Height);
        using Graphics graphics = Graphics.FromImage(bitmap);
        graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.None;
        boton.PintarMuestra(graphics, new Rectangle(Point.Empty, boton.ClientSize));
        return bitmap;
    }

    /// <summary>
    /// Busca un rectángulo de <paramref name="alto"/> filas contiguas del color
    /// exacto indicado, con al menos <paramref name="anchoMinimo"/> px de ancho
    /// en cada fila y el mismo rango horizontal en todas ellas.
    /// </summary>
    private static Rectangle? BuscarBloqueSolido(Bitmap bitmap, Color color, int anchoMinimo, int alto)
    {
        int argb = color.ToArgb();

        for (int y = 0; y + alto <= bitmap.Height; y++)
        {
            for (int x = 0; x < bitmap.Width; x++)
            {
                if (bitmap.GetPixel(x, y).ToArgb() != argb)
                    continue;

                int ancho = 0;
                while (x + ancho < bitmap.Width && bitmap.GetPixel(x + ancho, y).ToArgb() == argb)
                    ancho++;

                if (ancho < anchoMinimo)
                    continue;

                bool todasLasFilas = true;
                for (int dy = 1; dy < alto && todasLasFilas; dy++)
                {
                    for (int dx = 0; dx < ancho; dx++)
                    {
                        if (bitmap.GetPixel(x + dx, y + dy).ToArgb() != argb)
                        {
                            todasLasFilas = false;
                            break;
                        }
                    }
                }

                if (todasLasFilas)
                    return new Rectangle(x, y, ancho, alto);
            }
        }

        return null;
    }

    private static double Luminancia(Color color)
        => (0.299 * color.R + 0.587 * color.G + 0.114 * color.B) / 255.0;
}
