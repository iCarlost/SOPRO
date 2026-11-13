using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SOPRO.Core.Entities;
using SOPRO.Data.Context;
using SOPRO.WinForms.Forms;

namespace SOPRO.Tests.WinForms.Ribbon;

/// <summary>
/// Contrato del overflow del ribbon rediseñado (issue #117):
///   1. Seis grupos físicos de izquierda a derecha:
///      Datos → Exportar → Fuente → Alineación → Colores → Celdas.
///   2. Los 18 comandos existentes aparecen exactamente una vez: visibles o en
///      el menú "⋯" (nunca duplicados ni perdidos).
///   3. El colapso es de derecha a izquierda: Datos y Exportar son los últimos
///      en ocultarse; un grupo se oculta completo y su caption/separadores
///      viajan con él.
///   4. Los selectores de Fuente y el indicador lblColumna siguen accesibles
///      cuando su grupo está oculto (operando sobre los controles reales).
///   5. Anchos estrechos no recortan ningún control visible y la actualización
///      es idempotente ante resizes y cambios de estado repetidos.
/// </summary>
[STATestClass]
[DoNotParallelize] // WinForms necesita un hilo STA dedicado.
public class RibbonOverflowTests
{
    private const string ClaveDatos = "Datos";
    private const string ClaveExportar = "Exportar";
    private const string ClaveFuente = "Fuente";
    private const string ClaveAlineacion = "Alineacion";
    private const string ClaveColores = "Colores";
    private const string ClaveCeldas = "Celdas";

    private static readonly string[] ClavesEsperadas =
    {
        ClaveDatos, ClaveExportar, ClaveFuente, ClaveAlineacion, ClaveColores, ClaveCeldas
    };

    private static readonly string[] ComandosDatos =
    {
        "btnRecalcularRibbon", "btnDepurarRibbon", "btnConsolidarInsumos", "btnBuscarRibbon"
    };

    private static readonly string[] ComandosExportar =
    {
        "btnExcelRibbon", "btnPdfRibbon"
    };

    private static readonly string[] ComandosAlineacion =
    {
        "btnAlinIzq", "btnAlinCen", "btnAlinDer", "btnAlinJus", "btnAlinMed", "btnAlinAba"
    };

    private static readonly string[] ComandosColores =
    {
        "btnColorTexto", "btnColorFondo"
    };

    private static readonly string[] ComandosCeldas =
    {
        "btnWrapRibbon", "btnAplicarATodas"
    };

    [TestMethod]
    public void Grupos_OrdenFisicoYRepartoExactoDeLos18Comandos()
    {
        ConFormulario(form =>
        {
            CollectionAssert.AreEqual(
                ClavesEsperadas,
                form.ObtenerClavesGruposRibbon().ToArray(),
                "El orden físico de los grupos debe ser Datos → Exportar → Fuente → Alineación → Colores → Celdas.");

            CollectionAssert.AreEqual(ComandosDatos, NombresDeComandos(form, ClaveDatos));
            CollectionAssert.AreEqual(ComandosExportar, NombresDeComandos(form, ClaveExportar));
            CollectionAssert.AreEqual(ComandosAlineacion, NombresDeComandos(form, ClaveAlineacion));
            CollectionAssert.AreEqual(ComandosColores, NombresDeComandos(form, ClaveColores));
            CollectionAssert.AreEqual(ComandosCeldas, NombresDeComandos(form, ClaveCeldas));

            CollectionAssert.AreEqual(
                new[] { "cboFuente", "nudTamano", "btnNegrita", "btnCursiva", "lblColumna" },
                form.ObtenerControlesGrupoRibbon(ClaveFuente).Select(control => control.Name).ToArray(),
                "Fuente agrupa los selectores, los toggles y el indicador de columna.");

            var comandos = ComandosRibbon(form);
            Assert.AreEqual(18, comandos.Count, "El ribbon conserva exactamente 18 comandos.");
            Assert.AreEqual(
                18,
                comandos.Select(comando => comando.Name).Distinct().Count(),
                "Ningún comando puede estar repetido en dos grupos.");
        });
    }

    [TestMethod]
    public void Overflow_ColapsaDeDerechaAIzquierdaYConservaDatosYExportar()
    {
        int[] bordes = { 120, 240, 360, 480, 600, 720 };

        Assert.AreEqual(6, FormProyecto.CalcularGruposVisiblesRibbon(bordes, 720));
        Assert.AreEqual(5, FormProyecto.CalcularGruposVisiblesRibbon(bordes, 719));
        Assert.AreEqual(4, FormProyecto.CalcularGruposVisiblesRibbon(bordes, 500));
        Assert.AreEqual(2, FormProyecto.CalcularGruposVisiblesRibbon(bordes, 250),
            "Con poco ancho sobreviven Datos y Exportar: son los de mayor prioridad.");
        Assert.AreEqual(1, FormProyecto.CalcularGruposVisiblesRibbon(bordes, 130));
        Assert.AreEqual(0, FormProyecto.CalcularGruposVisiblesRibbon(bordes, 119));

        int visiblesAnteriores = 0;
        for (int limite = 0; limite <= 800; limite += 10)
        {
            int visibles = FormProyecto.CalcularGruposVisiblesRibbon(bordes, limite);
            Assert.IsTrue(
                visibles >= visiblesAnteriores,
                $"Al ensanchar no pueden reaparecer menos grupos (límite {limite}).");
            visiblesAnteriores = visibles;
        }
    }

    [TestMethod]
    public void Overflow_AnchoMinimo_RepresentaCadaComandoExactamenteUnaVez()
    {
        ConFormulario(form =>
        {
            AplicarAncho(form, 40);

            var comandos = ComandosRibbon(form);
            Assert.IsTrue(
                comandos.All(form.EstaOcultoPorOverflowRibbon),
                "A 40 px el prefijo visible queda vacío y todo debe ir al menú de overflow.");

            var enMenu = ComandosEnMenu(form);
            Assert.AreEqual(18, enMenu.Count);
            Assert.AreEqual(
                18,
                enMenu.Select(comando => comando.Name).Distinct().Count(),
                "Cada comando debe aparecer una sola vez en el menú.");

            var representados = comandos
                .Where(comando => !form.EstaOcultoPorOverflowRibbon(comando))
                .Concat(enMenu)
                .Select(comando => comando.Name)
                .ToArray();

            CollectionAssert.AreEquivalent(
                comandos.Select(comando => comando.Name).ToArray(),
                representados,
                "La unión visible + menú debe cubrir los 18 comandos exactamente una vez.");
        });
    }

    [TestMethod]
    public void Overflow_AnchoAmplio_NoOcultaNadaNiMuestraElMenu()
    {
        ConFormulario(form =>
        {
            // Ancho de diseño por defecto (ventana maximizada): todo visible.
            form.ActualizarOverflowRibbon();
            Assert.IsFalse(
                ComandosRibbon(form).Any(form.EstaOcultoPorOverflowRibbon),
                "Con el ancho de diseño ningún grupo debe colapsarse.");
            Assert.AreEqual(0, Menu(form).Items.Count, "Sin overflow el menú debe quedar vacío.");

            AplicarAncho(form, 4000);

            Assert.IsFalse(
                ComandosRibbon(form).Any(form.EstaOcultoPorOverflowRibbon),
                "Con ancho de sobra ningún grupo debe colapsarse.");
            Assert.AreEqual(0, Menu(form).Items.Count, "Sin overflow el menú debe quedar vacío.");
        });
    }

    [TestMethod]
    public void Grupos_GeometriaEnOrdenFisicoSinSolaparse()
    {
        ConFormulario(form =>
        {
            Panel panel = LeerCampo<Panel>(form, "panelRibbon");
            int derechaAnterior = int.MinValue;

            foreach (string clave in form.ObtenerClavesGruposRibbon())
            {
                var controles = form.ObtenerControlesGrupoRibbon(clave);
                int izquierda = controles.Min(control => OrigenEnPanel(control, panel).X);
                int derecha = controles.Max(control => OrigenEnPanel(control, panel).X + control.Width);

                Assert.IsTrue(
                    izquierda > derechaAnterior,
                    $"El grupo {clave} debe quedar a la derecha del grupo anterior (izquierda {izquierda} vs borde {derechaAnterior}).");
                Assert.IsTrue(derecha > izquierda, $"El grupo {clave} debe tener ancho positivo.");

                derechaAnterior = derecha;
            }
        });
    }

    [TestMethod]
    public void Overflow_NingunControlVisibleQuedaRecortado()
    {
        ConFormulario(form =>
        {
            Panel panel = LeerCampo<Panel>(form, "panelRibbon");

            foreach (int ancho in AnchosRepresentativos())
            {
                AplicarAncho(form, ancho);

                foreach (var control in ControlesDeAccesoRibbon(form))
                {
                    if (form.EstaOcultoPorOverflowRibbon(control))
                        continue;

                    Point origen = OrigenEnPanel(control, panel);
                    Assert.IsTrue(origen.X >= 0, $"{control.Name} no debe salir por la izquierda a {ancho} px.");
                    Assert.IsTrue(
                        origen.X + control.Width <= panel.ClientSize.Width,
                        $"{control.Name} queda recortado a {ancho} px de ancho (borde {origen.X + control.Width}).");
                }

                Button mas = LeerCampo<Button>(form, "btnRibbonMas");
                Assert.IsTrue(
                    mas.Right <= Math.Max(panel.ClientSize.Width, 40),
                    $"El botón de overflow debe quedar dentro del panel a {ancho} px.");
            }
        });
    }

    [TestMethod]
    public void Overflow_ResizeRepetidoEsIdempotenteYConverge()
    {
        ConFormulario(form =>
        {
            int[] anchos = { 1400, 400, 900, 160, 40, 1400, 400, 900, 160, 40 };

            var primeraPasada = new List<string>();
            foreach (int ancho in anchos)
            {
                AplicarAncho(form, ancho);
                primeraPasada.Add(FirmaEstadoOverflow(form));
            }

            var segundaPasada = new List<string>();
            foreach (int ancho in anchos)
            {
                AplicarAncho(form, ancho);
                segundaPasada.Add(FirmaEstadoOverflow(form));
            }

            CollectionAssert.AreEqual(
                primeraPasada,
                segundaPasada,
                "Repetir la misma secuencia de resizes debe producir exactamente el mismo estado.");

            AplicarAncho(form, 4000);
            Assert.IsFalse(ComandosRibbon(form).Any(form.EstaOcultoPorOverflowRibbon));
            Assert.AreEqual(0, Menu(form).Items.Count);
        });
    }

    [TestMethod]
    public void Overflow_MenuReflejaEstadoHabilitadoSinDuplicarItems()
    {
        ConFormulario(form =>
        {
            AplicarAncho(form, BuscarAnchoConOverflowParcial(form));

            Button oculto = ComandosRibbon(form).First(form.EstaOcultoPorOverflowRibbon);
            oculto.Enabled = false;
            for (int i = 0; i < 3; i++)
                form.ActualizarOverflowRibbon();

            var itemOculto = Menu(form).Items
                .OfType<ToolStripMenuItem>()
                .Where(item => ReferenceEquals(item.Tag, oculto))
                .ToList();

            Assert.AreEqual(1, itemOculto.Count, "El comando oculto debe aparecer una sola vez.");
            Assert.IsFalse(itemOculto[0].Enabled, "El menú debe respetar el estado deshabilitado del comando.");

            var nombres = Menu(form).Items
                .OfType<ToolStripMenuItem>()
                .Where(item => item.Tag is Button)
                .Select(item => ((Button)item.Tag!).Name)
                .ToArray();

            CollectionAssert.AllItemsAreUnique(nombres, "No puede haber comandos duplicados en el menú.");
            Assert.AreEqual(nombres.Length, nombres.Distinct().Count());

            // Cambios contextuales repetidos con TODO el ribbon colapsado: el menú
            // refleja el estado real sin duplicar items ni perder los selectores.
            AplicarAncho(form, 40);

            var cboFuente = LeerCampo<ComboBox>(form, "cboFuente");
            Button negrita = LeerCampo<Button>(form, "btnNegrita");
            for (int i = 0; i < 3; i++)
            {
                cboFuente.Enabled = i % 2 == 0;
                negrita.Enabled = i % 2 != 0;
                form.ActualizarOverflowRibbon();
            }

            var menu = Menu(form);
            var itemFuente = menu.Items.Find("itemOverflowFuente", true)
                .OfType<ToolStripMenuItem>()
                .Single();
            var itemNegrita = menu.Items
                .OfType<ToolStripMenuItem>()
                .Single(item => ReferenceEquals(item.Tag, negrita));

            Assert.AreEqual(cboFuente.Enabled, itemFuente.Enabled,
                "El submenú de fuente debe reflejar el estado habilitado actual.");
            Assert.AreEqual(negrita.Enabled, itemNegrita.Enabled,
                "El item de un comando oculto debe reflejar su estado habilitado actual.");
            Assert.AreEqual(
                ComandosRibbon(form).Count(form.EstaOcultoPorOverflowRibbon),
                ComandosEnMenu(form).Count,
                "Los refrescos repetidos no pueden duplicar comandos en el menú.");
        });
    }

    [TestMethod]
    public void Overflow_FuenteOculta_SelectoresEIndicadorSiguenSiendoUtilizables()
    {
        ConFormulario(form =>
        {
            var cboFuente = LeerCampo<ComboBox>(form, "cboFuente");
            var nudTamano = LeerCampo<NumericUpDown>(form, "nudTamano");
            var lblColumna = LeerCampo<Label>(form, "lblColumna");

            cboFuente.Items.Clear();
            cboFuente.Items.AddRange(new object[] { "Segoe UI", "Arial", "Calibri" });
            cboFuente.SelectedIndex = 0;

            AplicarAncho(form, 40);

            var menu = Menu(form);
            var submenuFuente = menu.Items.Find("itemOverflowFuente", true)
                .OfType<ToolStripMenuItem>()
                .Single();
            var submenuTamano = menu.Items.Find("itemOverflowTamano", true)
                .OfType<ToolStripMenuItem>()
                .Single();
            var indicador = menu.Items.Find("itemOverflowColumna", true)
                .OfType<ToolStripMenuItem>()
                .Single();

            Assert.IsTrue(form.EstaOcultoPorOverflowRibbon(cboFuente));
            Assert.IsTrue(form.EstaOcultoPorOverflowRibbon(nudTamano));
            Assert.IsTrue(form.EstaOcultoPorOverflowRibbon(lblColumna));

            form.PoblarSubmenuFuenteRibbon(submenuFuente);
            Assert.AreEqual(cboFuente.Items.Count, submenuFuente.DropDownItems.Count,
                "El submenú debe ofrecer todas las fuentes del selector real.");

            var arial = submenuFuente.DropDownItems
                .OfType<ToolStripMenuItem>()
                .Single(item => item.Text == "Arial");
            arial.PerformClick();
            Assert.AreEqual("Arial", cboFuente.SelectedItem,
                "Elegir una fuente del submenú debe cambiar el control real del ribbon.");

            form.PoblarSubmenuTamanoRibbon(submenuTamano);
            var doce = submenuTamano.DropDownItems
                .OfType<ToolStripMenuItem>()
                .Single(item => item.Text == "12");
            doce.PerformClick();
            Assert.AreEqual(12m, nudTamano.Value,
                "Elegir un tamaño del submenú debe cambiar el control real del ribbon.");

            StringAssert.Contains(indicador.Text, lblColumna.Text,
                "El menú debe exponer el indicador de columna para que no se pierda.");
            Assert.IsFalse(indicador.Enabled, "El indicador es informativo, no un comando.");

            cboFuente.Enabled = false;
            form.PoblarSubmenuFuenteRibbon(submenuFuente);
            Assert.IsTrue(
                submenuFuente.DropDownItems.OfType<ToolStripMenuItem>().All(item => !item.Enabled),
                "El submenú debe respetar el estado habilitado del selector real.");
            cboFuente.Enabled = true;
        });
    }

    [TestMethod]
    public void Overflow_CaptionsYSeparadoresViajanConSuGrupo()
    {
        ConFormulario(form =>
        {
            Panel panel = LeerCampo<Panel>(form, "panelRibbon");

            var captionsPorGrupo = new Dictionary<string, string>
            {
                [ClaveDatos] = "lblGroupDatos",
                [ClaveExportar] = "lblGroupExportar",
                [ClaveFuente] = "lblGroupFuente",
                [ClaveAlineacion] = "lblGroupAlineacion",
                [ClaveColores] = "lblGroupColores",
                [ClaveCeldas] = "lblGroupCeldas",
            };

            foreach (var (clave, nombreCaption) in captionsPorGrupo)
            {
                Control caption = panel.Controls.Find(nombreCaption, false)
                    .SingleOrDefault()
                    ?? throw new AssertFailedException($"Falta el caption {nombreCaption} del grupo {clave}.");

                Assert.IsTrue(
                    form.ObtenerDecorGrupoRibbon(clave).Contains(caption),
                    $"{nombreCaption} debe viajar con el grupo {clave}.");
            }

            AplicarAncho(form, BuscarAnchoConOverflowParcial(form));

            foreach (string clave in form.ObtenerClavesGruposRibbon())
            {
                var controles = form.ObtenerControlesGrupoRibbon(clave);
                bool grupoOculto = controles.All(form.EstaOcultoPorOverflowRibbon);

                Assert.IsTrue(
                    controles.All(control => form.EstaOcultoPorOverflowRibbon(control) == grupoOculto),
                    $"El grupo {clave} debe ocultarse completo, sin controles huérfanos visibles.");

                foreach (Control decor in form.ObtenerDecorGrupoRibbon(clave))
                {
                    Assert.AreEqual(
                        grupoOculto,
                        form.EstaOcultoPorOverflowRibbon(decor),
                        $"El decor '{decor.Name}' debe viajar con el grupo {clave}.");
                }
            }

            Control captionCeldas = panel.Controls.Find("lblGroupCeldas", false).Single();
            Assert.IsTrue(
                form.EstaOcultoPorOverflowRibbon(captionCeldas),
                "En un ancho con overflow parcial el último grupo (Celdas) está colapsado: su caption también debe ocultarse.");

            AplicarAncho(form, 4000);
            Assert.IsFalse(
                form.EstaOcultoPorOverflowRibbon(captionCeldas),
                "Al ensanchar, el caption debe volver con su grupo.");
        });
    }

    [TestMethod]
    public void Overflow_OrdenDelMenuSigueElOrdenFisicoDeLosGrupos()
    {
        ConFormulario(form =>
        {
            AplicarAncho(form, 40);

            var esperado = ClavesEsperadas
                .SelectMany(clave => form.ObtenerControlesGrupoRibbon(clave))
                .OfType<Button>()
                .Select(comando => comando.Name)
                .ToArray();

            var enMenu = ComandosEnMenu(form).Select(comando => comando.Name).ToArray();

            CollectionAssert.AreEqual(
                esperado,
                enMenu,
                "El menú debe respetar el orden físico Datos → Exportar → Fuente → Alineación → Colores → Celdas.");
        });
    }

    [TestMethod]
    public void Overflow_ComandoDesdeMenuEjecutaElHandlerSinMostrarElBoton()
    {
        ConFormulario(form =>
        {
            AplicarAncho(form, 40);

            Button boton = LeerCampo<Button>(form, "btnNegrita");
            boton.Enabled = true;
            form.ActualizarOverflowRibbon();

            var item = ItemDeComando(form, boton);

            bool activoAntes = EstaActivo(boton);
            Point ubicacionAntes = boton.Location;
            bool visibleDuranteElClick = true;
            Point ubicacionDuranteElClick = Point.Empty;

            boton.Click += (_, __) =>
            {
                visibleDuranteElClick = boton.Visible;
                ubicacionDuranteElClick = boton.Location;
            };

            item.PerformClick();

            Assert.IsFalse(
                visibleDuranteElClick,
                "El botón no debe hacerse visible mientras se ejecuta el comando del overflow.");
            Assert.IsFalse(boton.Visible, "El botón debe seguir oculto después del comando.");
            Assert.AreEqual(
                ubicacionAntes,
                ubicacionDuranteElClick,
                "El botón no debe moverse fuera del ribbon para poder ejecutar el comando.");
            Assert.AreEqual(ubicacionAntes, boton.Location);
            Assert.AreNotEqual(
                activoAntes,
                EstaActivo(boton),
                "El handler original del comando debe ejecutarse al elegirlo en el menú.");
            Assert.IsTrue(form.EstaOcultoPorOverflowRibbon(boton));

            // El refresco diferido (BeginInvoke) puede reconstruir el menú fuera
            // del dispatch sin duplicar comandos ni volver a mostrar el botón.
            ProcesarMensajes();

            Assert.IsFalse(boton.Visible, "El botón debe seguir oculto tras el refresco diferido.");
            Assert.AreEqual(
                ComandosRibbon(form).Count(form.EstaOcultoPorOverflowRibbon),
                ComandosEnMenu(form).Count,
                "El refresco diferido no debe duplicar comandos en el menú.");
        }, mostrar: true);
    }

    [TestMethod]
    public void Overflow_MenuNoSeReconstruyeDuranteElClickYSeDifiereHastaCerrar()
    {
        ConFormulario(form =>
        {
            AplicarAncho(form, 40);

            Button boton = LeerCampo<Button>(form, "btnNegrita");
            var item = ItemDeComando(form, boton);
            var menu = Menu(form);
            int itemsAntes = menu.Items.Count;

            bool mismoItemDuranteElClick = false;
            int itemsDuranteElClick = -1;

            // Simula un comando que refresca el estado contextual del ribbon
            // mientras el menú todavía está despachando el Click del item.
            boton.Click += (_, __) =>
            {
                form.ActualizarOverflowRibbon();
                itemsDuranteElClick = menu.Items.Count;
                mismoItemDuranteElClick = menu.Items
                    .OfType<ToolStripMenuItem>()
                    .Any(candidato => ReferenceEquals(candidato, item));
            };

            item.PerformClick();

            Assert.IsTrue(
                mismoItemDuranteElClick,
                "El menú no puede reconstruirse dentro del Click del propio item.");
            Assert.AreEqual(
                itemsAntes,
                itemsDuranteElClick,
                "La colección de items debe permanecer intacta durante el dispatch.");

            Assert.IsTrue(
                menu.Items.OfType<ToolStripMenuItem>().Any(candidato => ReferenceEquals(candidato, item)),
                "La reconstrucción debe esperar al cierre del menú, no ejecutarse al terminar el handler.");

            form.NotificarMenuOverflowCerrado();

            Assert.IsFalse(
                menu.Items.OfType<ToolStripMenuItem>().Any(candidato => ReferenceEquals(candidato, item)),
                "Al cerrarse el menú debe reconstruirse con items nuevos.");

            var nombres = menu.Items
                .OfType<ToolStripMenuItem>()
                .Where(candidato => candidato.Tag is Button)
                .Select(candidato => ((Button)candidato.Tag!).Name)
                .ToArray();

            CollectionAssert.AllItemsAreUnique(nombres, "La reconstrucción diferida no debe duplicar comandos.");
            CollectionAssert.AreEquivalent(
                ClavesEsperadas.SelectMany(clave => form.ObtenerControlesGrupoRibbon(clave))
                    .OfType<Button>()
                    .Select(comando => comando.Name)
                    .ToArray(),
                nombres,
                "Tras el cierre deben seguir representados los 18 comandos exactamente una vez.");
        });
    }

    [TestMethod]
    public void Overflow_BotonSePegaAlUltimoGrupoVisibleConMargenSeguro()
    {
        ConFormulario(form =>
        {
            Panel panel = LeerCampo<Panel>(form, "panelRibbon");
            Button mas = LeerCampo<Button>(form, "btnRibbonMas");

            for (int ancho = 200; ancho <= 1100; ancho += 20)
            {
                AplicarAncho(form, ancho);
                AfirmarBotonOverflowAdyacente(form, panel, mas, ancho);
            }
        });
    }

    [TestMethod]
    public void Overflow_ResizeRealDelPanelMantieneElBotonPegadoAlGrupoVisible()
    {
        ConFormulario(form =>
        {
            Panel panel = LeerCampo<Panel>(form, "panelRibbon");
            Button mas = LeerCampo<Button>(form, "btnRibbonMas");

            // Sin llamar a ActualizarOverflowRibbon: el camino real es el evento
            // Resize del panel (y el posible reajuste por Anchor al hacer layout).
            foreach (int ancho in new[] { 420, 560, 700, 840, 380, 980 })
            {
                panel.Width = ancho;
                ProcesarMensajes();
                AfirmarBotonOverflowAdyacente(form, panel, mas, ancho);
            }
        }, mostrar: true);
    }

    // ═════════════════════════════ Infraestructura ═════════════════════════════

    private static IReadOnlyList<Button> ComandosRibbon(FormProyecto form)
        => ClavesEsperadas
            .SelectMany(clave => form.ObtenerControlesGrupoRibbon(clave))
            .OfType<Button>()
            .ToList();

    private static IReadOnlyList<Button> ComandosEnMenu(FormProyecto form)
        => Menu(form).Items
            .OfType<ToolStripMenuItem>()
            .Where(item => item.Tag is Button)
            .Select(item => (Button)item.Tag!)
            .ToList();

    private static IReadOnlyList<Control> ControlesDeAccesoRibbon(FormProyecto form)
        => ClavesEsperadas
            .SelectMany(clave => form.ObtenerControlesGrupoRibbon(clave))
            .Where(control => control is Button || control is ComboBox || control is NumericUpDown || control.Name == "lblColumna")
            .ToList();

    private static string[] NombresDeComandos(FormProyecto form, string clave)
        => form.ObtenerControlesGrupoRibbon(clave)
            .OfType<Button>()
            .Select(comando => comando.Name)
            .ToArray();

    private static void AplicarAncho(FormProyecto form, int ancho)
    {
        Panel panel = LeerCampo<Panel>(form, "panelRibbon");
        panel.Width = ancho;
        form.ActualizarOverflowRibbon();
    }

    private static ContextMenuStrip Menu(FormProyecto form)
        => LeerCampo<ContextMenuStrip>(form, "cmsRibbonOverflow");

    private static string FirmaEstadoOverflow(FormProyecto form)
    {
        string controles = string.Join(
            "|",
            ClavesEsperadas.SelectMany(clave => form.ObtenerControlesGrupoRibbon(clave))
                .Select(control => $"{control.Name}:{(form.EstaOcultoPorOverflowRibbon(control) ? "oculto" : "visible")}"));

        string menu = string.Join("|", Menu(form).Items
            .OfType<ToolStripMenuItem>()
            .Select(item => $"{item.Name}={item.Enabled}"));

        string decor = string.Join(
            "|",
            ClavesEsperadas.SelectMany(clave => form.ObtenerDecorGrupoRibbon(clave))
                .Select(control => $"{control.Name}:{(form.EstaOcultoPorOverflowRibbon(control) ? "oculto" : "visible")}"));

        Button mas = LeerCampo<Button>(form, "btnRibbonMas");
        return $"{controles}##{decor}##{menu}##mas:{mas.Enabled}";
    }

    private static IEnumerable<int> AnchosRepresentativos()
    {
        for (int ancho = 40; ancho <= 1400; ancho += 37)
            yield return ancho;
        yield return 720;
        yield return 900;
        yield return 1600;
    }

    /// <summary>
    /// Primer ancho que deja parte del ribbon visible y parte colapsada. Se busca
    /// dinámicamente para no acoplar las pruebas a la geometría exacta del lane
    /// de diseño.
    /// </summary>
    private static int BuscarAnchoConOverflowParcial(FormProyecto form)
    {
        for (int ancho = 60; ancho <= 4000; ancho += 10)
        {
            AplicarAncho(form, ancho);
            var comandos = ComandosRibbon(form);
            bool hayVisibles = comandos.Any(comando => !form.EstaOcultoPorOverflowRibbon(comando));
            bool hayOcultos = comandos.Any(form.EstaOcultoPorOverflowRibbon);

            if (hayVisibles && hayOcultos)
                return ancho;
        }

        throw new AssertFailedException("No se encontró un ancho que produzca overflow parcial.");
    }

    private static Point OrigenEnPanel(Control control, Panel panel)
    {
        Point origen = control.Location;
        Control? padre = control.Parent;
        while (padre != null && !ReferenceEquals(padre, panel))
        {
            origen.Offset(padre.Location);
            padre = padre.Parent;
        }

        return origen;
    }

    private static int BordeDerechoGrupo(FormProyecto form, Panel panel, string clave)
    {
        int borde = 0;
        foreach (Control control in form.ObtenerControlesGrupoRibbon(clave)
                     .Concat(form.ObtenerDecorGrupoRibbon(clave)))
        {
            borde = Math.Max(borde, OrigenEnPanel(control, panel).X + control.Width);
        }

        return borde;
    }

    private static void AfirmarBotonOverflowAdyacente(FormProyecto form, Panel panel, Button mas, int ancho)
    {
        var clavesVisibles = ClavesEsperadas
            .Where(clave => form.ObtenerControlesGrupoRibbon(clave)
                .Any(control => !form.EstaOcultoPorOverflowRibbon(control)))
            .ToList();

        if (clavesVisibles.Count == 0)
        {
            Assert.AreEqual(
                4,
                mas.Left,
                $"Sin grupos visibles el botón debe quedar en el margen izquierdo ({ancho} px).");
            return;
        }

        if (clavesVisibles.Count == ClavesEsperadas.Length)
            return; // Sin overflow el botón permanece oculto en el borde.

        int bordeVisible = clavesVisibles
            .Max(clave => BordeDerechoGrupo(form, panel, clave));
        int hueco = mas.Left - bordeVisible;

        Assert.IsTrue(
            hueco >= 4 && hueco <= 16,
            $"El botón debe quedar pegado al último grupo visible (hueco {hueco} px a {ancho} px).");
        Assert.IsTrue(
            mas.Right <= panel.ClientSize.Width,
            $"El botón no debe salirse del panel a {ancho} px.");
    }

    private static ToolStripMenuItem ItemDeComando(FormProyecto form, Button boton)
        => Menu(form).Items
            .OfType<ToolStripMenuItem>()
            .Single(item => ReferenceEquals(item.Tag, boton));

    private static bool EstaActivo(Button boton)
    {
        object? estado = boton.Tag;
        PropertyInfo? propiedad = estado?.GetType().GetProperty(
            "Active",
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

        return propiedad?.GetValue(estado) is bool activo && activo;
    }

    private static void ProcesarMensajes()
    {
        for (int i = 0; i < 5; i++)
            System.Windows.Forms.Application.DoEvents();
    }

    private static T LeerCampo<T>(FormProyecto form, string nombre) where T : class
    {
        FieldInfo? campo = form.GetType().GetField(
            nombre,
            BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);

        if (campo == null)
            throw new InvalidOperationException($"No existe el campo '{nombre}' en {form.GetType().Name}.");

        return campo.GetValue(form) as T
            ?? throw new InvalidOperationException($"El campo '{nombre}' de {form.GetType().Name} no está inicializado.");
    }

    private static void ConFormulario(Action<FormProyecto> prueba, bool mostrar = false)
    {
        string root = Path.Combine(Path.GetTempPath(), $"sopro_ribbon_overflow_{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        string dbPath = Path.Combine(root, "proyecto-temporal.db");

        SOPROContext? contexto = null;
        FormProyecto? form = null;
        try
        {
            contexto = new SOPROContext(dbPath);
            contexto.Database.EnsureCreated();

            form = new FormProyecto(contexto, new Proyecto
            {
                Id = 1,
                Nombre = "Proyecto de prueba de ribbon",
                Descripcion = string.Empty,
                Ubicacion = string.Empty,
                Convocante = string.Empty,
                Contratista = string.Empty,
                ApoderadoLegal = string.Empty,
            });

            if (mostrar)
            {
                form.Show();
                ProcesarMensajes();
            }

            prueba(form);
        }
        finally
        {
            form?.Dispose();
            contexto?.Dispose();
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();

            try
            {
                if (Directory.Exists(root))
                    Directory.Delete(root, true);
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
    }
}
