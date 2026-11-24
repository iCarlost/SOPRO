using System;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SOPRO.Application.Services;
using SOPRO.Core.Entities;
using SOPRO.Data.Context;
using SOPRO.WinForms.Forms;
using SOPRO.WinForms.Helpers;

namespace SOPRO.Tests.WinForms.Presupuesto;

/// <summary>
/// Contrato de medidas de los chips de porcentajes del header del presupuesto.
/// El tamaño del chip debe salir de la MISMA medición (fuentes y flags) que usa
/// el pintado, y el relleno lógico debe escalarse al DPI vigente sin doble
/// escalado. Todo el contrato es verificable a 96 DPI (CI) porque compara la
/// medida con la fórmula, no con píxeles de un equipo concreto.
/// </summary>
[STATestClass]
[DoNotParallelize] // Construye el formulario real del módulo (controles WinForms y estado estático).
public class ChipsPorcentajesDpiTests
{
    private const string CampoProyectoGlobalFormato = "_proyectoActual";

    [TestInitialize]
    public void VerificarApartmentState()
    {
        Assert.AreEqual(ApartmentState.STA, Thread.CurrentThread.GetApartmentState(),
            "Las pruebas que crean FormPresupuesto deben ejecutarse en STA.");
    }

    [TestMethod]
    public void Chips_Formulario_TamanoCoincideConMedicion_YLaFilaCabeEnElHeader()
    {
        string root = Path.Combine(Path.GetTempPath(), $"sopro_chips_dpi_{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        Proyecto? previo = null;
        FormPresupuesto? form = null;
        SOPROContext? ctx = null;
        try
        {
            previo = LeerProyectoGlobalFormato();
            ctx = new SOPROContext(Path.Combine(root, "proyecto-temporal.soproproj"));
            ctx.Database.EnsureCreated();
            Proyecto escenario = CrearProyecto(ctx);

            form = new FormPresupuesto(ctx, escenario, new ProjectWorkspaceService(
                Path.Combine(root, "sopro"), Path.Combine(root, "local")));
            form.Show();
            System.Windows.Forms.Application.DoEvents();

            Control panelTop = BuscarPorNombre(form, "panelTop");
            Control panelChips = BuscarPorNombre(form, "panelChipsPorcentajes");

            Assert.IsTrue(panelChips.Controls.Count > 0,
                "El escenario debe producir al menos un chip de porcentaje.");
            Assert.IsTrue(panelChips.Bottom <= panelTop.Height,
                $"La fila de chips (Bottom={panelChips.Bottom}, Y={panelChips.Top}, H={panelChips.Height}) debe caber en panelTop (Height={panelTop.Height}) al DPI real. " +
                $"dpiForm={form.DeviceDpi}.");

            Control? previoChip = null;
            foreach (Control control in panelChips.Controls)
            {
                Assert.IsInstanceOfType<FormPresupuesto.ChipPorcentaje>(control, "La fila solo debe contener chips.");
                var chip = (FormPresupuesto.ChipPorcentaje)control;
                var rellenoLogico = FormPresupuesto.ChipPorcentaje.RellenoLogico;
                int dpi = chip.DeviceDpi;
                Size contenido = chip.MedirContenidoActual();

                Assert.AreEqual(chip.LogicalToDeviceUnits(rellenoLogico.Left), chip.Padding.Left,
                    $"Relleno izquierdo debe ser el lógico escalado al DPI vigente ({dpi}).");
                Assert.AreEqual(chip.LogicalToDeviceUnits(rellenoLogico.Top), chip.Padding.Top,
                    $"Relleno superior debe ser el lógico escalado al DPI vigente ({dpi}).");
                Assert.AreEqual(chip.LogicalToDeviceUnits(rellenoLogico.Right), chip.Padding.Right,
                    $"Relleno derecho debe ser el lógico escalado al DPI vigente ({dpi}).");
                Assert.AreEqual(chip.LogicalToDeviceUnits(rellenoLogico.Bottom), chip.Padding.Bottom,
                    $"Relleno inferior debe ser el lógico escalado al DPI vigente ({dpi}).");

                Assert.AreEqual(contenido.Width + chip.Padding.Horizontal, chip.Width,
                    $"Ancho del chip debe ser contenido medido + relleno horizontal al DPI {dpi}. " +
                    $"contenido={contenido.Width}, W={chip.Width}, padding={chip.Padding.Horizontal}.");
                Assert.AreEqual(contenido.Height + chip.Padding.Vertical, chip.Height,
                    $"Alto del chip debe ser contenido medido + relleno vertical al DPI {dpi}. " +
                    $"contenido={contenido.Height}, H={chip.Height}, padding={chip.Padding.Vertical}.");

                int yTexto = (chip.Height - contenido.Height) / 2;
                Assert.IsTrue(yTexto >= 1 && yTexto + contenido.Height <= chip.Height - 1,
                    $"El texto (y={yTexto}..{yTexto + contenido.Height}) no debe tocar el borde de la píldora (H={chip.Height}) al DPI {dpi}.");

                if (previoChip != null)
                {
                    Assert.IsTrue(chip.Left >= previoChip.Right + previoChip.Margin.Right - 1,
                        $"Los chips no deben solaparse (previo.Right={previoChip.Right}, margin={previoChip.Margin.Right}, Left={chip.Left}).");
                }
                previoChip = chip;
            }
        }
        finally
        {
            form?.Dispose();
            ctx?.Dispose();
            RestaurarProyectoGlobalFormato(previo);
            LimpiarDirectorio(root);
        }
    }

    [TestMethod]
    public void Chips_Formulario_Ancho560_NombreLargo_ElProyectoCedeSinSolape()
    {
        string root = Path.Combine(Path.GetTempPath(), $"sopro_chips_560_{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        Proyecto? previo = null;
        FormPresupuesto? form = null;
        SOPROContext? ctx = null;
        try
        {
            previo = LeerProyectoGlobalFormato();
            ctx = new SOPROContext(Path.Combine(root, "proyecto-temporal.soproproj"));
            ctx.Database.EnsureCreated();
            Proyecto escenario = CrearProyecto(ctx);
            escenario.Nombre = "Proyecto de ampliación de la red de agua potable y alcantarillado sanitario de la zona noroeste 2026";
            ctx.SaveChanges();

            form = new FormPresupuesto(ctx, escenario, new ProjectWorkspaceService(
                Path.Combine(root, "sopro"), Path.Combine(root, "local")));
            form.WindowState = FormWindowState.Normal;
            form.ClientSize = new Size(560, 700);
            form.Show();
            System.Windows.Forms.Application.DoEvents();

            Control panelTop = BuscarPorNombre(form, "panelTop");
            Control panelChips = BuscarPorNombre(form, "panelChipsPorcentajes");
            var lblProyecto = (Label)BuscarPorNombre(form, "lblProyecto");

            Assert.IsTrue(lblProyecto.AutoEllipsis && !lblProyecto.AutoSize,
                "El nombre del proyecto debe truncarse con elipsis y ancho controlado por código.");

            // Ancho estrecho (560, como las pruebas de workspace): la fila de
            // chips (anclada a la derecha) comprime el nombre, nunca al revés.
            AssertarFranjaInferiorSinSolape(panelTop, panelChips, lblProyecto);

            // Al volver a un ancho amplio el nombre recupera espacio de forma
            // determinista (recalculado tras cada layout/resize/DPI).
            form.ClientSize = new Size(1400, form.ClientSize.Height);
            System.Windows.Forms.Application.DoEvents();

            AssertarFranjaInferiorSinSolape(panelTop, panelChips, lblProyecto);
        }
        finally
        {
            form?.Dispose();
            ctx?.Dispose();
            RestaurarProyectoGlobalFormato(previo);
            LimpiarDirectorio(root);
        }
    }

    private static void AssertarFranjaInferiorSinSolape(Control panelTop, Control panelChips, Label lblProyecto)
    {
        Assert.IsTrue(panelChips.Right <= panelTop.Width,
            $"La fila de chips (Right={panelChips.Right}) debe caber en panelTop (Width={panelTop.Width}).");
        Assert.IsTrue(panelChips.Bottom <= panelTop.Height,
            $"La fila de chips (Bottom={panelChips.Bottom}) debe caber en panelTop (Height={panelTop.Height}).");
        Assert.IsTrue(panelChips.Width > 0 && panelChips.Controls.Count > 0,
            "La fila de chips no debe quedar vacía ni colapsada al estrechar la ventana.");

        foreach (Control chip in panelChips.Controls)
        {
            Assert.IsTrue(chip.Right <= panelTop.Width && chip.Bottom <= panelTop.Height,
                $"El chip '{chip.Text}' (Bounds={chip.Bounds}) debe seguir visible dentro de panelTop.");
        }

        int separacion = panelTop.LogicalToDeviceUnits(FormPresupuesto.SeparacionProyectoChipsLogica);
        int anchoEsperado = Math.Max(0,
            panelChips.Left - separacion - lblProyecto.Margin.Right - lblProyecto.Left);
        Assert.AreEqual(anchoEsperado, lblProyecto.Width,
            $"El ancho del nombre del proyecto debe salir del borde izquierdo real de la fila de chips " +
            $"(chips.Left={panelChips.Left}, label.Left={lblProyecto.Left}, separación={separacion}).");

        Assert.IsTrue(lblProyecto.Right <= panelChips.Left,
            $"El nombre del proyecto (Right={lblProyecto.Right}) no debe invadir la fila de chips (Left={panelChips.Left}).");
    }

    [TestMethod]
    public void Chip_TamanoPreferido_EsMedicionMasRelleno_YelLayoutLoRespeta()
    {
        using var host = new Form();
        using var panel = new FlowLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
        };
        var chips = new[]
        {
            new FormPresupuesto.ChipPorcentaje("Indirectos CD", "10 %"),
            new FormPresupuesto.ChipPorcentaje("Utilidad", "15 %"),
            new FormPresupuesto.ChipPorcentaje("Falta configurar los porcentajes del proyecto"),
        };
        try
        {
            foreach (var chip in chips)
                panel.Controls.Add(chip);
            host.Controls.Add(panel);
            host.Show();
            System.Windows.Forms.Application.DoEvents();

            foreach (var chip in chips)
            {
                Size contenido = chip.MedirContenidoActual();
                Size preferido = chip.GetPreferredSize(Size.Empty);

                Assert.AreEqual(contenido.Width + chip.Padding.Horizontal, preferido.Width,
                    "El ancho preferido debe ser la medición canónica más el relleno horizontal.");
                Assert.AreEqual(contenido.Height + chip.Padding.Vertical, preferido.Height,
                    "El alto preferido debe ser la medición canónica más el relleno vertical.");

                Assert.AreEqual(preferido, chip.Size,
                    $"El layout debe dar al chip su tamaño preferido (real={chip.Size}, preferido={preferido}).");

                Assert.IsTrue(chip.Width >= chip.Padding.Left + contenido.Width + chip.Padding.Right,
                    "El contenido medido debe caber dentro del chip sin cruzar el relleno.");
            }
        }
        finally
        {
            foreach (var chip in chips)
                chip.Dispose();
        }
    }

    [TestMethod]
    public void Chip_Advertencia_SinValor_SeMideComoUnSoloTexto()
    {
        const string texto = "Falta configurar los porcentajes del proyecto";
        using var chip = new FormPresupuesto.ChipPorcentaje(texto);
        Size contenido = FormPresupuesto.ChipPorcentaje.MedirContenido(texto, string.Empty);
        Assert.AreEqual(contenido, chip.MedirContenidoActual(),
            "El chip de advertencia se mide como un único texto sin valor.");
        Assert.IsTrue(contenido.Height > 0 && contenido.Width > texto.Length,
            "La medición del texto de advertencia debe ser razonable.");
    }

    [TestMethod]
    public void Chip_Relleno_ReaplicadoEsIdempotente()
    {
        using var chip = new FormPresupuesto.ChipPorcentaje("Financiamiento", "3 %");
        Padding trasConstruccion = chip.Padding;
        chip.AplicarEscalaDpi();
        Assert.AreEqual(trasConstruccion, chip.Padding,
            "Reaplicar la escala al mismo DPI no debe alterar el relleno (sin doble escalado).");
        chip.AplicarEscalaDpi();
        Assert.AreEqual(trasConstruccion, chip.Padding,
            "Una segunda reaplicación tampoco debe alterar el relleno.");
    }

    // El comportamiento del relleno en un DPI real distinto de 96 (125% en
    // laptops) se valida con una sonda en un proceso real a 125%, no aquí:
    // el testhost de pruebas corre siempre a 96 DPI y Control.DeviceDpi no es
    // simulable por reflexión en .NET 10 (vive en la ventana/awareness del
    // proceso, no en un campo del control).

    private static Control BuscarPorNombre(Control raiz, string nombre)
        => BuscarPorNombreOpcional(raiz, nombre)
           ?? throw new InvalidOperationException(
               $"No se encontró el control {nombre}. árbolDesdeRaiz=[{VolcarArbol(raiz)}]");

    private static Control? BuscarPorNombreOpcional(Control raiz, string nombre)
    {
        if (string.Equals(raiz.Name, nombre, StringComparison.Ordinal))
            return raiz;

        foreach (Control hijo in raiz.Controls)
        {
            Control? encontrado = BuscarPorNombreOpcional(hijo, nombre);
            if (encontrado != null) return encontrado;
        }

        return null;
    }

    private static Proyecto CrearProyecto(SOPROContext ctx)
    {
        Proyecto proyecto = new Proyecto
        {
            Nombre = "Proyecto chips DPI",
            Descripcion = string.Empty,
            Ubicacion = string.Empty,
            Convocante = string.Empty,
            Contratista = string.Empty,
            ApoderadoLegal = string.Empty,
            FechaInicio = new DateTime(2026, 1, 1),
            FechaTermino = new DateTime(2026, 1, 31),
            PlazoEjecucion = 31,
            PorcentajeIndirectosCentral = 10m,
            PorcentajeIndirectosCampo = 5m,
            PorcentajeFinanciamiento = 3m,
            PorcentajeUtilidad = 15m,
            PorcentajeCargosAdicionales = 2m,
            PorcentajeIVA = 16m,
            DecimalesCantidad = 2,
            DecimalesImporte = 2,
            DecimalesPorcentaje = 4,
            FechaCreacion = new DateTime(2026, 1, 1),
            FechaModificacion = new DateTime(2026, 1, 1),
        };
        ctx.Proyectos.Add(proyecto);
        ctx.SaveChanges();
        return proyecto;
    }

    private static Proyecto? LeerProyectoGlobalFormato()
        => (Proyecto?)typeof(FormatoHelper).GetField(CampoProyectoGlobalFormato,
               BindingFlags.Static | BindingFlags.NonPublic)?.GetValue(null);

    private static void RestaurarProyectoGlobalFormato(Proyecto? previo)
        => typeof(FormatoHelper).GetField(CampoProyectoGlobalFormato,
               BindingFlags.Static | BindingFlags.NonPublic)
           ?.SetValue(null, previo);

    private static string VolcarArbol(Control raiz)
    {
        var nombres = new List<string>();
        void Visitar(Control c, int nivel)
        {
            if (nivel > 2) return;
            nombres.Add($"{new string(' ', nivel * 2)}{c.Name}({c.GetType().Name})");
            foreach (Control h in c.Controls) Visitar(h, nivel + 1);
        }
        Visitar(raiz, 0);
        return string.Join(" | ", nombres);
    }

    private static void LimpiarDirectorio(string ruta)
    {
        for (int intento = 0; intento < 5; intento++)
        {
            try
            {
                Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
                if (Directory.Exists(ruta))
                    Directory.Delete(ruta, recursive: true);
                return;
            }
            catch (IOException) when (intento < 4)
            {
                Thread.Sleep(200);
            }
        }
    }
}
