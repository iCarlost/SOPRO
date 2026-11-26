using System;
using System.Drawing;
using System.Reflection;
using System.Windows.Forms;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SOPRO.WinForms.Helpers;
using SOPRO.WinForms.UI.Controls;

namespace SOPRO.Tests.WinForms.Ribbon;

/// <summary>
/// Invariantes de repintado de <see cref="SoproButton"/> (issue #186: rayas de
/// 1 px reportadas en Recalcular/Ajustar). El mecanismo exacto del reporte no
/// se reprodujo a demanda en pantalla; lo que estas pruebas fijan es la
/// propiedad que su corrección garantiza:
///
///   1. IDEMPOTENCIA: un repintado PARCIAL (franja invalidada por el sistema,
///      recortada de verdad con Graphics.SetClip) produce exactamente los
///      mismos píxeles que el repintado completo para el mismo estado visible.
///      Antes del fix, OnPaint leía Cursor.Position/Control.MouseButtons en el
///      momento de pintar y una franja podía discrepar de la cara circundante.
///   2. El estado hover/presionado sigue las TRANSICIONES relevantes (entrada,
///      salida, arrastre con captura, pérdida de captura, habilitado,
///      visibilidad, foco) sin depender del cursor físico: los tests corren
///      con el cursor real donde lo dejó el usuario y el estado se inyecta
///      disparando los métodos protegidos OnMouse*/On* por reflexión.
///
/// Todo se renderiza a bitmaps en memoria; nada depende del monitor. El botón
/// se parenta a un panel blanco (como la banda del ribbon) para que la
/// simulación de transparencia resuelva el mismo fondo que en producción.
/// </summary>
[STATestClass]
[DoNotParallelize]
public class SoproButtonRepaintParcialTests
{
    private static readonly Color FondoHoverEsperado = SoproTheme.RibbonClaro.Hover;
    private static readonly Color FondoPresionadoEsperado = SoproTheme.RibbonClaro.Presionado;
    private static readonly Color FondoBaseEsperado = Color.White;

    [TestMethod]
    public void MouseEnter_Simulado_PintaFondoHover_AunqueElCursorFisicoEsteLejos()
    {
        using var escenario = CrearEscenario();

        // El cursor físico NO está sobre el botón (fuera de pantalla). Con el
        // código anterior al fix, el pintado ignoraba este MouseEnter simulado
        // porque leía la posición real del cursor.
        InvocarProtectorado(escenario.Boton, "OnMouseEnter");
        ProcesarMensajes();

        using var bitmap = escenario.Boton.Renderizar();
        Assert.AreEqual(
            FondoHoverEsperado.ToArgb(),
            PixelDeFondo(bitmap).ToArgb(),
            "Tras OnMouseEnter el fondo debe ser el tinte de hover aunque el cursor físico esté lejos.");
    }

    [TestMethod]
    public void RepintadoParcial_CoincideConRepintadoCompleto_ParaElMismoEstado()
    {
        using var escenario = CrearEscenario();
        var boton = escenario.Boton;

        InvocarProtectorado(boton, "OnMouseEnter");
        ProcesarMensajes();
        using var renderHover = boton.Renderizar();

        InvocarProtectorado(boton, "OnMouseLeave");
        ProcesarMensajes();
        using var renderReposo = boton.Renderizar();

        // Franja central expuesta y repintada parcialmente en REPOSO: la
        // composición debe ser idéntica al render completo de reposo.
        Rectangle franja = FranjaCentral(boton);
        using (var compuesta = ComponerFranja(renderReposo, boton, franja, estadoHover: false))
            PixelesIguales(renderReposo, compuesta, "reposo");

        // Misma franja repintada parcialmente en HOVER sobre una cara obsoleta
        // de reposo: la franja debe coincidir con el render completo de hover
        // y el resto de la cara conserva el estado obsoleto (así se comporta
        // una superficie real entre repintados).
        using (var compuesta = ComponerFranja(renderReposo, boton, franja, estadoHover: true))
        {
            FranjasIguales(renderHover, compuesta, franja, "hover");
            Assert.AreNotEqual(
                renderReposo.GetPixel(5, franja.Y).ToArgb(),
                compuesta.GetPixel(5, franja.Y).ToArgb(),
                "La franja repintada con hover debe diferir de la cara obsoleta en reposo.");
        }
    }

    [TestMethod]
    public void RepintadoParcial_Tardio_EnReposo_NoDependeDelCursorFisico()
    {
        using var escenario = CrearEscenario();
        var boton = escenario.Boton;
        InvocarProtectorado(boton, "OnMouseLeave");
        ProcesarMensajes();
        using var reposo = boton.Renderizar();

        // La franja se repinta DESPUÉS del último evento: debe reflejar el
        // estado vigente (reposo) y no el cursor físico.
        Rectangle franja = FranjaCentral(boton);
        using var tardia = boton.Renderizar(franja);
        FranjasIguales(reposo, tardia, franja, "reposo tardío");
    }

    [TestMethod]
    public void ArrastreConCaptura_PresionadoSoloMientrasElPunteroEstaDentro()
    {
        using var escenario = CrearEscenario();
        var boton = escenario.Boton;
        InvocarProtectorado(boton, "OnMouseEnter");
        InvocarProtectorado(boton, "OnMouseDown", Ratones(MouseButtons.Left, 10, 10));

        using (var presionado = boton.Renderizar())
            Assert.AreEqual(
                FondoPresionadoEsperado.ToArgb(),
                PixelDeFondo(presionado).ToArgb(),
                "Con hover + botón izquierdo presionado el fondo debe ser el tinte presionado.");

        // Arrastre fuera del área con captura: sin aspecto presionado.
        InvocarProtectorado(boton, "OnMouseMove", Ratones(MouseButtons.Left, boton.Width + 40, -20));
        using (var fuera = boton.Renderizar())
            Assert.AreEqual(
                FondoBaseEsperado.ToArgb(),
                PixelDeFondo(fuera).ToArgb(),
                "Arrastrando fuera del botón no debe verse el tinte presionado.");

        // Regreso dentro con el botón aún presionado: aspecto presionado otra vez.
        InvocarProtectorado(boton, "OnMouseMove", Ratones(MouseButtons.Left, 10, 10));
        using (var regresado = boton.Renderizar())
            Assert.AreEqual(
                FondoPresionadoEsperado.ToArgb(),
                PixelDeFondo(regresado).ToArgb(),
                "Al regresar dentro con el botón aún presionado debe verse el tinte presionado.");

        // Soltar dentro: queda hover.
        InvocarProtectorado(boton, "OnMouseUp", Ratones(MouseButtons.Left, 10, 10));
        using var soltado = boton.Renderizar();
        Assert.AreEqual(
            FondoHoverEsperado.ToArgb(),
            PixelDeFondo(soltado).ToArgb(),
            "Al soltar dentro debe quedar el tinte de hover.");
    }

    [TestMethod]
    public void PerdidaDeCaptura_LimpiaElEstadoPresionado()
    {
        using var escenario = CrearEscenario();
        var boton = escenario.Boton;
        InvocarProtectorado(boton, "OnMouseEnter");
        InvocarProtectorado(boton, "OnMouseDown", Ratones(MouseButtons.Left, 10, 10));

        InvocarProtectorado(boton, "OnMouseCaptureChanged", EventArgs.Empty);
        using var liberado = boton.Renderizar();
        Assert.AreEqual(
            FondoHoverEsperado.ToArgb(),
            PixelDeFondo(liberado).ToArgb(),
            "Al perder la captura el botón debe volver al tinte de hover, no quedar presionado.");
        Assert.IsFalse(
            LeerCampoBool(boton, "_pressed"),
            "El estado interno presionado debe limpiarse al perder la captura.");
    }

    [TestMethod]
    public void Deshabilitar_LimpiaPresionado_YAlRehabilitarSincronizaElCursor()
    {
        using var escenario = CrearEscenario();
        var boton = escenario.Boton;
        InvocarProtectorado(boton, "OnMouseEnter");
        InvocarProtectorado(boton, "OnMouseDown", Ratones(MouseButtons.Left, 10, 10));

        boton.Enabled = false;
        ProcesarMensajes();
        Assert.IsFalse(LeerCampoBool(boton, "_pressed"), "Deshabilitar debe limpiar el estado presionado.");
        using (var deshabilitado = boton.Renderizar())
            Assert.AreEqual(
                FondoBaseEsperado.ToArgb(),
                PixelDeFondo(deshabilitado).ToArgb(),
                "Deshabilitado el fondo debe ser el base aunque el estado hover quedara marcado.");

        // Al rehabilitar, el estado hover se re-sincroniza con el cursor real
        // (lejos del botón fuera de pantalla): no debe quedar pegado en hover.
        boton.Enabled = true;
        ProcesarMensajes();
        Assert.IsFalse(
            LeerCampoBool(boton, "_pointerOver"),
            "Rehabilitar debe re-sincronizar el estado hover con la posición real del cursor.");
        using (var rehabilitado = boton.Renderizar())
            Assert.AreEqual(
                FondoBaseEsperado.ToArgb(),
                PixelDeFondo(rehabilitado).ToArgb(),
                "Rehabilitado lejos del cursor, el fondo debe ser el base.");
    }

    [TestMethod]
    public void OcultarYMostrar_CorrigeElEstadoSegunLaPosicionRealDelCursor()
    {
        using var escenario = CrearEscenario();
        var boton = escenario.Boton;
        InvocarProtectorado(boton, "OnMouseEnter");
        Assert.IsTrue(LeerCampoBool(boton, "_pointerOver"), "Precondición: hover simulado activo.");

        boton.Visible = false;
        boton.Visible = true;
        ProcesarMensajes();

        Assert.IsFalse(
            LeerCampoBool(boton, "_pointerOver"),
            "Al reaparecer, el estado hover debe re-sincronizarse con el cursor físico (lejos).");
        using var render = boton.Renderizar();
        Assert.AreEqual(
            FondoBaseEsperado.ToArgb(),
            PixelDeFondo(render).ToArgb(),
            "Tras reaparecer lejos del cursor el fondo debe ser el base.");
    }

    /// <summary>
    /// Transición de teclado (foco): el marco de foco convive con el estado
    /// seguido sin alterarlo. Sigue el patrón de los tests de foco del ribbon
    /// (formulario fuera de pantalla con guarda Inconclusive).
    /// </summary>
    [TestMethod]
    public void FocoTeclado_ConHover_ConservaElFondoDeHoverYDibujaMarco()
    {
        using var escenario = CrearEscenario(enfocable: true);
        var boton = (BotonPintableEnfocable)escenario.Boton;
        var otro = new TextBox { Location = new Point(20, 110), Width = 120 };
        escenario.Formulario.Controls.Add(otro);
        escenario.Formulario.Show();
        escenario.Formulario.Activate();
        ProcesarMensajes();

        boton.Focus();
        ProcesarMensajes();
        if (!boton.Focused)
            Assert.Inconclusive("El entorno no permitió dar el foco al botón; sin foco no hay marco que validar.");

        InvocarProtectorado(boton, "OnMouseEnter");
        ProcesarMensajes();

        using var conFocoYHover = boton.Renderizar();
        Assert.AreEqual(
            FondoHoverEsperado.ToArgb(),
            PixelDeFondo(conFocoYHover).ToArgb(),
            "El foco de teclado no debe alterar el fondo seguido (hover).");

        // Quitar el foco (si el entorno lo permite) para comparar el mismo
        // estado hover sin marco: la diferencia debe ser el marco de foco.
        otro.Focus();
        ProcesarMensajes();
        if (otro.Focused)
        {
            InvocarProtectorado(boton, "OnMouseEnter");
            using var hoverSinFoco = boton.Renderizar();
            Assert.AreEqual(
                FondoHoverEsperado.ToArgb(),
                PixelDeFondo(hoverSinFoco).ToArgb(),
                "Sin foco, el fondo hover debe conservarse.");
            Assert.IsTrue(
                Difieren(conFocoYHover, hoverSinFoco),
                "El marco de foco debe aparecer/desaparecer con la transición de foco.");
        }
        else
        {
            Assert.Inconclusive("El entorno no permitió alternar el foco; solo se validó el fondo.");
        }
    }

    // ═════════════════════════ Infraestructura ═════════════════════════

    private sealed class Escenario : IDisposable
    {
        public Form Formulario { get; }
        public BotonPintable Boton { get; }

        public Escenario(BotonPintable boton, Form formulario)
        {
            Boton = boton;
            Formulario = formulario;
        }

        public void Dispose()
        {
            Boton.Dispose();
            Formulario.Dispose();
        }
    }

    private static Escenario CrearEscenario(bool enfocable = false)
    {
        var formulario = new Form
        {
            ClientSize = new Size(320, 180),
            StartPosition = FormStartPosition.Manual,
            Location = new Point(-4000, -4000),
            ShowInTaskbar = false,
        };
        var banda = new Panel
        {
            Location = new Point(10, 10),
            Size = new Size(300, 60),
            BackColor = FondoBaseEsperado,
        };
        formulario.Controls.Add(banda);

        var boton = enfocable ? new BotonPintableEnfocable() : new BotonPintable();
        boton.Size = new Size(74, 44);
        boton.Text = "Recalcular";
        boton.ForeColor = SoproTheme.RibbonClaro.Acento;
        boton.BackColor = Color.Transparent;
        boton.FlatStyle = FlatStyle.Flat;
        boton.SoproLayout = SoproButtonLayout.Large;
        boton.SoproIcon = SoproIconType.Recalcular;
        boton.SoproIconSize = 20;
        boton.TabStop = false;
        boton.UseVisualStyleBackColor = false;
        boton.Location = new Point(10, 8);
        boton.FlatAppearance.BorderSize = 0;
        boton.FlatAppearance.MouseOverBackColor = FondoHoverEsperado;
        boton.FlatAppearance.MouseDownBackColor = FondoPresionadoEsperado;
        banda.Controls.Add(boton);
        formulario.Show(); // fuera de pantalla: Visible efectiva true, sin tapar el escritorio
        ProcesarMensajes();
        _ = boton.Handle;

        return new Escenario(boton, formulario);
    }

    /// <summary>Franja horizontal central de 1 px (exposición típica del sistema).</summary>
    private static Rectangle FranjaCentral(SoproButton boton)
        => new(0, boton.ClientSize.Height / 2, boton.ClientSize.Width, 1);

    /// <summary>
    /// Compone un repintado PARCIAL real: parte de una cara previa (posiblemente
    /// obsoleta), fija el estado indicado y pinta la franja con recorte
    /// efectivo del Graphics (solo puede escribir dentro de la franja).
    /// </summary>
    private static Bitmap ComponerFranja(Bitmap caraPrevia, SoproButton boton, Rectangle franja, bool estadoHover)
    {
        if (estadoHover)
            InvocarProtectorado(boton, "OnMouseEnter");
        else
            InvocarProtectorado(boton, "OnMouseLeave");

        var compuesta = (Bitmap)caraPrevia.Clone();
        using (Graphics graphics = Graphics.FromImage(compuesta))
        {
            graphics.SetClip(franja);
            ((BotonPintable)boton).PintarFondo(graphics, franja);
            ((BotonPintable)boton).PintarEn(graphics, franja);
        }
        return compuesta;
    }

    /// <summary>
    /// Dispara un método protegido de <see cref="Control"/> (p. ej.
    /// OnMouseEnter) para mover el estado seguido del botón sin tocar el
    /// cursor físico ni requerir monitor.
    /// </summary>
    private static void InvocarProtectorado(Control control, string metodo, EventArgs? argumentos = null)
    {
        EventArgs args = argumentos ?? EventArgs.Empty;
        MethodInfo? info = typeof(Control).GetMethod(
            metodo,
            BindingFlags.Instance | BindingFlags.NonPublic,
            null,
            new[] { args.GetType() },
            null);

        if (info == null)
            throw new InvalidOperationException($"No se resolvió Control.{metodo}({args.GetType().Name}).");

        info.Invoke(control, new[] { args });
    }

    private static MouseEventArgs Ratones(MouseButtons botones, int x, int y)
        => new(botones, 1, x, y, 0);

    /// <summary>
    /// Lee un campo privado declarado en la jerarquía (los campos de estado de
    /// <see cref="SoproButton"/> viven en la clase base y BindingFlags no los
    /// encuentra desde el tipo derivado sin recorrerla).
    /// </summary>
    private static bool LeerCampoBool(object objeto, string campo)
    {
        for (Type? tipo = objeto.GetType(); tipo != null; tipo = tipo.BaseType)
        {
            var info = tipo.GetField(campo, BindingFlags.Instance | BindingFlags.NonPublic);
            if (info != null)
                return (bool)info.GetValue(objeto)!;
        }

        throw new InvalidOperationException($"No existe el campo '{campo}' en la jerarquía de {objeto.GetType().Name}.");
    }

    /// <summary>Pixel de fondo puro del botón Large (esquina sin contenido).</summary>
    private static Color PixelDeFondo(Bitmap bitmap) => bitmap.GetPixel(5, 5);

    private static void FranjasIguales(Bitmap esperado, Bitmap real, Rectangle franja, string contexto)
    {
        for (int y = franja.Top; y < franja.Bottom; y++)
            for (int x = franja.Left; x < franja.Right; x++)
                Assert.AreEqual(
                    esperado.GetPixel(x, y).ToArgb(),
                    real.GetPixel(x, y).ToArgb(),
                    $"Repintado parcial ({contexto}): difiere en ({x}, {y}).");
    }

    private static void PixelesIguales(Bitmap esperado, Bitmap real, string contexto)
    {
        Assert.AreEqual(esperado.Size, real.Size, $"Tamaños distintos ({contexto}).");
        for (int y = 0; y < esperado.Height; y++)
            for (int x = 0; x < esperado.Width; x++)
                Assert.AreEqual(
                    esperado.GetPixel(x, y).ToArgb(),
                    real.GetPixel(x, y).ToArgb(),
                    $"Comparación completa ({contexto}): difiere en ({x}, {y}).");
    }

    private static bool Difieren(Bitmap primero, Bitmap segundo)
    {
        if (primero.Size != segundo.Size) return true;
        for (int y = 0; y < primero.Height; y++)
            for (int x = 0; x < primero.Width; x++)
                if (primero.GetPixel(x, y).ToArgb() != segundo.GetPixel(x, y).ToArgb())
                    return true;
        return false;
    }

    private static void ProcesarMensajes()
    {
        for (int i = 0; i < 3; i++)
            System.Windows.Forms.Application.DoEvents();
    }

    /// <summary>
    /// Subclase que expone el pintado protegido con recorte REAL del Graphics
    /// (mismo patrón que <c>SoproButtonRenderingTests.BotonSoproPintable</c>).
    /// Reproduce el flujo real de WM_PAINT de un control con DoubleBuffered:
    /// primero OnPaintBackground (la simulación de transparencia pinta el fondo
    /// del padre sobre el buffer) y después OnPaint.
    /// </summary>
    public class BotonPintable : SoproButton
    {
        public Bitmap Renderizar(Rectangle? clip = null)
        {
            _ = Handle;
            var area = clip ?? new Rectangle(Point.Empty, ClientSize);
            var bitmap = new Bitmap(ClientSize.Width, ClientSize.Height);
            using (Graphics graphics = Graphics.FromImage(bitmap))
            {
                // Recorte efectivo: como en un repintado parcial real, el
                // pintado solo puede escribir dentro de la franja sucia.
                graphics.SetClip(area);
                PintarFondo(graphics, area);
                PintarEn(graphics, area);
            }
            return bitmap;
        }

        public void PintarFondo(Graphics graphics, Rectangle area)
            => OnPaintBackground(new PaintEventArgs(graphics, area));

        public void PintarEn(Graphics graphics, Rectangle area)
            => OnPaint(new PaintEventArgs(graphics, area));
    }

    /// <summary>Fuerza <see cref="Control.ShowFocusCues"/> para validar el marco sin depender del sistema.</summary>
    public class BotonPintableEnfocable : BotonPintable
    {
        protected override bool ShowFocusCues => true;
    }
}
