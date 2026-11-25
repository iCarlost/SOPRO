using System;
using System.Drawing;
using System.Linq;
using System.Threading;
using System.Windows.Forms;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SOPRO.WinForms.Controls;
using SOPRO.WinForms.Helpers;

namespace SOPRO.Tests.WinForms.Chrome;

/// <summary>
/// Contrato del host flotante compartido por todos los módulos (issue #171):
/// acople Arriba/Abajo, aire lógico escalado al DPI, índice z preservado sin
/// depender del orden de hospedado, recorte redondeado de la superficie y
/// ciclo de vida. No construye formularios de módulo ni requiere BD.
/// </summary>
[STATestClass]
[DoNotParallelize] // Los controles WinForms requieren un hilo STA dedicado.
public class ChromeFlotanteHostTests
{
    [TestMethod]
    public void Hospedar_Arriba_ConservaPadreIndiceZAcopleYAireLogico()
    {
        using var ventana = new Form { ClientSize = new Size(600, 300), BackColor = Color.White };
        var cuerpo = new Panel { Name = "cuerpo", Dock = DockStyle.Fill };
        var status = new Panel { Name = "status", Dock = DockStyle.Bottom, Height = 20 };
        var banda = new Panel { Name = "banda", Dock = DockStyle.Top, Height = 34 };
        var header = new Panel { Name = "header", Dock = DockStyle.Top, Height = 50 };
        ventana.Controls.AddRange(new Control[] { cuerpo, status, banda, header });

        ChromeFlotanteHost hostHeader = ChromeFlotanteHost.Hospedar(header, ventana, "headerHost", ventana.BackColor);
        ChromeFlotanteHost hostBanda = ChromeFlotanteHost.Hospedar(
            banda, ventana, "bandaHost", ventana.BackColor, insetSuperiorLogico: 0);

        Assert.AreSame(ventana, hostHeader.Parent, "El host queda en el contenedor de la superficie.");
        Assert.AreSame(hostHeader, header.Parent, "La superficie vive dentro de su host.");
        Assert.AreEqual("header", header.Name, "La superficie conserva su nombre.");

        // Orden de acople intacto: header > banda > status > cuerpo (índice z descendente).
        var controles = ventana.Controls;
        Assert.AreEqual(4, controles.Count, "El contenedor gana solo los dos hosts (reemplazan in-place a sus superficies).");
        Assert.AreEqual(controles.Count - 1, controles.GetChildIndex(hostHeader), "El header se acopla primero (arriba de todo).");
        Assert.IsTrue(
            controles.GetChildIndex(hostHeader) > controles.GetChildIndex(hostBanda)
            && controles.GetChildIndex(hostBanda) > controles.GetChildIndex(status)
            && controles.GetChildIndex(status) > controles.GetChildIndex(cuerpo),
            "El orden de acople original se preserva.");

        Assert.AreEqual(DockStyle.Top, hostHeader.Dock, "El host sigue acoplado arriba.");
        Assert.AreEqual(DockStyle.Top, header.Dock, "La superficie conserva su acople dentro del host.");

        int dpi = hostHeader.DeviceDpi;
        Assert.AreEqual(
            new Padding(
                ChromeFlotanteHost.EscalarPixeles(ChromeFlotanteHost.InsetHorizontalLogico, dpi),
                ChromeFlotanteHost.EscalarPixeles(ChromeFlotanteHost.InsetSuperiorLogico, dpi),
                ChromeFlotanteHost.EscalarPixeles(ChromeFlotanteHost.InsetHorizontalLogico, dpi),
                ChromeFlotanteHost.EscalarPixeles(ChromeFlotanteHost.SeparacionInferiorLogica, dpi)),
            hostHeader.Padding,
            "Aire del header: 10px a los lados, 8 al exterior y 6 hacia el interior.");

        Assert.AreEqual(
            new Padding(
                ChromeFlotanteHost.EscalarPixeles(ChromeFlotanteHost.InsetHorizontalLogico, dpi),
                0,
                ChromeFlotanteHost.EscalarPixeles(ChromeFlotanteHost.InsetHorizontalLogico, dpi),
                ChromeFlotanteHost.EscalarPixeles(ChromeFlotanteHost.SeparacionInferiorLogica, dpi)),
            hostBanda.Padding,
            "Aire de la banda pegada al header: sin aire exterior.");

        System.Windows.Forms.Application.DoEvents();
        foreach (var host in new[] { hostHeader, hostBanda })
        {
            Assert.AreEqual(host.Superficie.Height + host.Padding.Vertical, host.Height,
                $"{host.Name}: el alto del host es superficie + aire vertical.");
            Rectangle superficie = host.Superficie.Bounds;
            Assert.IsTrue(superficie.X >= host.Padding.Left && superficie.Y >= host.Padding.Top,
                $"{host.Name}: la superficie flota dentro del aire del host.");
        }
    }

    [TestMethod]
    public void Hospedar_Abajo_EspejaElAireYSeAcoplaAlBordeInferior()
    {
        using var ventana = new Form { ClientSize = new Size(600, 300), BackColor = Color.White };
        var cuerpo = new Panel { Name = "cuerpo", Dock = DockStyle.Fill };
        var acciones = new Panel { Name = "acciones", Dock = DockStyle.Bottom, Height = 44 };
        ventana.Controls.AddRange(new Control[] { cuerpo, acciones });

        ChromeFlotanteHost host = ChromeFlotanteHost.Hospedar(
            acciones, ventana, "accionesHost", ventana.BackColor, posicion: ChromeFlotantePosicion.Abajo);

        Assert.AreEqual(DockStyle.Bottom, host.Dock, "El host de la barra inferior se acopla abajo.");
        Assert.AreEqual(DockStyle.Bottom, host.Superficie.Dock, "La superficie conserva su acople inferior.");

        int dpi = host.DeviceDpi;
        Assert.AreEqual(
            new Padding(
                ChromeFlotanteHost.EscalarPixeles(ChromeFlotanteHost.InsetHorizontalLogico, dpi),
                ChromeFlotanteHost.EscalarPixeles(ChromeFlotanteHost.SeparacionInferiorLogica, dpi),
                ChromeFlotanteHost.EscalarPixeles(ChromeFlotanteHost.InsetHorizontalLogico, dpi),
                ChromeFlotanteHost.EscalarPixeles(ChromeFlotanteHost.InsetSuperiorLogico, dpi)),
            host.Padding,
            "Aire espejado: separación 6 hacia el interior y 8 hacia el borde exterior (abajo).");

        System.Windows.Forms.Application.DoEvents();
        Assert.AreEqual(host.Superficie.Height + host.Padding.Vertical, host.Height,
            "El alto del host es superficie + aire vertical.");
        Rectangle superficie = host.Superficie.Bounds;
        Assert.IsTrue(superficie.Bottom <= host.Height - host.Padding.Bottom + 1,
            "La superficie queda dentro del aire; abajo respira el borde exterior.");

        Assert.IsTrue(host.Padding.Bottom >= ChromeFlotanteHost.DesplazamientosSombra.Max(),
            "El aire exterior aloja las tres pasadas de sombra sin recortarlas.");
    }

    [TestMethod]
    public void Hospedar_OrdenIndependiente_LasLlamadasEnCualquierOrdenDanElMismoAcople()
    {
        using var ventanaA = new Form { ClientSize = new Size(600, 300) };
        using var ventanaB = new Form { ClientSize = new Size(600, 300) };
        foreach (Form ventana in new[] { ventanaA, ventanaB })
        {
            ventana.Controls.Add(new Panel { Name = "cuerpo", Dock = DockStyle.Fill });
            ventana.Controls.Add(new Panel { Name = "b3", Dock = DockStyle.Top, Height = 20 });
            ventana.Controls.Add(new Panel { Name = "b2", Dock = DockStyle.Top, Height = 20 });
            ventana.Controls.Add(new Panel { Name = "b1", Dock = DockStyle.Top, Height = 20 });
        }

        Control b1A = Buscar(ventanaA, "b1");
        Control b2A = Buscar(ventanaA, "b2");
        Control b3A = Buscar(ventanaA, "b3");
        Control b1B = Buscar(ventanaB, "b1");
        Control b2B = Buscar(ventanaB, "b2");
        Control b3B = Buscar(ventanaB, "b3");

        ChromeFlotanteHost.Hospedar(b1A, ventanaA, "b1Host", ventanaA.BackColor);
        ChromeFlotanteHost.Hospedar(b2A, ventanaA, "b2Host", ventanaA.BackColor, insetSuperiorLogico: 0);
        ChromeFlotanteHost.Hospedar(b3A, ventanaA, "b3Host", ventanaA.BackColor, insetSuperiorLogico: 0);

        ChromeFlotanteHost.Hospedar(b3B, ventanaB, "b3Host", ventanaB.BackColor, insetSuperiorLogico: 0);
        ChromeFlotanteHost.Hospedar(b1B, ventanaB, "b1Host", ventanaB.BackColor);
        ChromeFlotanteHost.Hospedar(b2B, ventanaB, "b2Host", ventanaB.BackColor, insetSuperiorLogico: 0);

        var indicesA = ventanaA.Controls.Cast<Control>().Select(c => (c.Name, ventanaA.Controls.GetChildIndex(c))).OrderBy(x => x.Item2);
        var indicesB = ventanaB.Controls.Cast<Control>().Select(c => (c.Name, ventanaB.Controls.GetChildIndex(c))).OrderBy(x => x.Item2);

        CollectionAssert.AreEqual(
            indicesA.Select(x => x.Name).ToList(),
            indicesB.Select(x => x.Name).ToList(),
            "Hospedar en distinto orden produce el mismo orden de acople.");
    }

    [TestMethod]
    public void Hospedar_RedondeaLaSuperficie_YFallaSiYaFueHospedada()
    {
        using var ventana = new Form { ClientSize = new Size(400, 200) };
        var superficie = new Panel { Name = "header", Dock = DockStyle.Top, Height = 40, BackColor = Color.FromArgb(51, 51, 76) };
        ventana.Controls.Add(superficie);

        ChromeFlotanteHost host = ChromeFlotanteHost.Hospedar(superficie, ventana, "headerHost", Color.White);

        Region? region = superficie.Region;
        Assert.IsNotNull(region, "Hospedar aplica el recorte redondeado del criterio.");
        Assert.IsFalse(region!.IsVisible(0, 0), "La esquina (0,0) queda fuera del Region.");
        Assert.IsTrue(region.IsVisible(superficie.Width / 2, superficie.Height / 2), "El centro sigue visible.");

        InvalidOperationException? error = Assert.ThrowsExactly<InvalidOperationException>(
            () => ChromeFlotanteHost.Hospedar(superficie, ventana, "headerHost2", Color.White),
            "Hospedar una superficie ya hospedada debe fallar de forma explícita.");
        Assert.IsTrue(error.Message.Contains("header"), "El error identifica a la superficie duplicada.");
    }

    [TestMethod]
    public void Host_Dispose_LiberaLaSuperficieYDejaDeEscucharCambios()
    {
        using var ventana = new Form { ClientSize = new Size(400, 200) };
        var superficie = new Panel { Name = "header", Dock = DockStyle.Top, Height = 40 };
        ventana.Controls.Add(superficie);
        ChromeFlotanteHost host = ChromeFlotanteHost.Hospedar(superficie, ventana, "headerHost", Color.White);
        System.Windows.Forms.Application.DoEvents();
        int altoHost = host.Height;
        Assert.AreEqual(40 + host.Padding.Vertical, altoHost);

        host.Dispose();

        Assert.IsTrue(host.IsDisposed, "El host se libera.");
        Assert.IsTrue(superficie.IsDisposed, "La superficie alojada se libera con el host.");
    }

    [TestMethod]
    public void MetricasLogicas_EscalarCoherenteAlDpi()
    {
        Assert.AreEqual(10, ChromeFlotanteHost.EscalarPixeles(ChromeFlotanteHost.InsetHorizontalLogico, 96));
        Assert.AreEqual(13, ChromeFlotanteHost.EscalarPixeles(ChromeFlotanteHost.InsetHorizontalLogico, 125));
        Assert.AreEqual(10, ChromeFlotanteHost.EscalarPixeles(ChromeFlotanteHost.InsetSuperiorLogico, 125));
        Assert.AreEqual(8, ChromeFlotanteHost.EscalarPixeles(ChromeFlotanteHost.SeparacionInferiorLogica, 125));
        Assert.AreEqual(18, ChromeFlotanteHost.RadioEscalado(SoproUiMetrics.SurfaceRadiusPx, 125));
        Assert.AreEqual(SoproUiMetrics.SurfaceRadiusPx, ChromeFlotanteHost.RadioEscalado(SoproUiMetrics.SurfaceRadiusPx, 96));
    }

    private static Control Buscar(Control raiz, string nombre)
    {
        Control? encontrado = raiz.Controls.Cast<Control>().FirstOrDefault(c => c.Name == nombre);
        if (encontrado != null) return encontrado;
        foreach (Control hijo in raiz.Controls)
        {
            try { return Buscar(hijo, nombre); }
            catch (InvalidOperationException) { }
        }
        throw new InvalidOperationException($"No se encontró el control '{nombre}'.");
    }
}
