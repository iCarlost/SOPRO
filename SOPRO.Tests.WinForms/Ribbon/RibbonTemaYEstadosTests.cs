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
using SOPRO.WinForms.Helpers;
using SOPRO.WinForms.UI.Controls;

namespace SOPRO.Tests.WinForms.Ribbon;

/// <summary>
/// Contrato visual del ribbon modernizado (issue #180). A diferencia de
/// <see cref="RibbonOverflowTests"/> (grupos, overflow y comandos) aquí se
/// fijan SOLO las propiedades que deben sobrevivir a cualquier ajuste de la
/// paleta o de la geometría:
///
///   1. La banda se apoya en una superficie CLARA (mockup SOPRO), no en el
///      slate oscuro del chrome.
///   2. Textos, captions e iconos alcanzan contraste AA sobre esa superficie
///      (4.5:1 texto, 3:1 elementos no textuales), los selectores siguen
///      legibles, los chips de color conservan un pozo de muestra definido y
///      el glifo de la ficha de color de texto se lee en reposo.
///   3. Los seis grupos se distinguen con captions propias (texto distinto,
///      centradas bajo su grupo, sin pisar comandos) y separadores verticales
///      discretos pero visibles.
///   4. Reposo, hover, presionado y seleccionado se distinguen entre sí; el
///      estado deshabilitado (exento de WCAG) se distingue del habilitado y
///      sigue siendo perceptible.
///   5. El foco de teclado dibuja un marco visible en el botón del ribbon.
///   6. El orden de tabulación acompaña al orden físico de los grupos.
///   7. En anchos representativos no hay recortes ni solapes de captions ni
///      separadores.
///
/// Todo se evalúa sobre los controles reales del formulario (sin capturas de
/// pantalla y sin fijar píxeles incidentales): se comparan colores efectivos,
/// rectángulos y el resultado del propio pintado del control.
/// </summary>
[STATestClass]
[DoNotParallelize] // WinForms/System.Drawing requieren un hilo STA dedicado.
public class RibbonTemaYEstadosTests
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

    /// <summary>Caption que rotula cada grupo (contrato de #117 que #180 decora).</summary>
    private static readonly Dictionary<string, string> CaptionsPorGrupo = new()
    {
        [ClaveDatos] = "lblGroupDatos",
        [ClaveExportar] = "lblGroupExportar",
        [ClaveFuente] = "lblGroupFuente",
        [ClaveAlineacion] = "lblGroupAlineacion",
        [ClaveColores] = "lblGroupColores",
        [ClaveCeldas] = "lblGroupCeldas",
    };

    private static readonly string[] ChipsDeColor = { "btnColorTexto", "btnColorFondo" };

    // Contraste mínimo de texto (WCAG AA) y de elementos no textuales.
    private const double ContrasteTexto = 4.5;
    private const double ContrasteNoTextual = 3.0;

    // Una superficie clara del mockup: blanco, slate-50/100 o un tinte de marca
    // suave. El slate-800 del chrome actual (luminancia ~0.02) queda fuera.
    private const double LuminanciaMinimaSuperficieClara = 0.70;

    // Un borde/separador "discreto" puede ser sutil, pero no invisible.
    private const double ContrasteMinimoSeparador = 1.10;

    // Dos tintes suaves de estado pueden ser sutiles, pero deben notarse: si dos
    // estados comparten el mismo fondo el control no da feedback de interacción.
    private const double ContrasteMinimoEntreEstados = 1.05;

    [TestMethod]
    public void Superficie_BaseClaraYOpacaQueSostieneLaBandaDelRibbon()
    {
        ConFormulario(form =>
        {
            Panel panel = LeerCampo<Panel>(form, "panelRibbon");
            (Color fondo, string origen) = FondoEfectivo(panel);

            Assert.AreEqual(
                255,
                (int)fondo.A,
                $"La superficie del ribbon debe ser opaca (origen: {origen}, color {Hex(fondo)}).");

            double luminancia = LuminanciaRelativa(fondo);
            Assert.IsTrue(
                luminancia >= LuminanciaMinimaSuperficieClara,
                $"El ribbon debe descansar en una superficie clara (issue #180): el fondo efectivo es {Hex(fondo)} "
                + $"desde {origen}, con luminancia {luminancia:0.00} (se espera >= {LuminanciaMinimaSuperficieClara:0.00}).");
        });
    }

    [TestMethod]
    public void TextosCaptionsEIconos_AlcanzanContrasteSuficienteSobreLaSuperficie()
    {
        ConFormulario(form =>
        {
            Panel panel = LeerCampo<Panel>(form, "panelRibbon");
            (Color fondo, string origen) = FondoEfectivo(panel);
            var fallos = new List<string>();

            // Sin módulo activo el ribbon nace con el bloque de formato apagado
            // (los controles inactivos están exentos de contraste WCAG). Para
            // validar el aspecto HABILITADO se encienden los comandos y se
            // re-aplica el tema por el mismo camino que usa el ribbon al
            // refrescar su estado contextual.
            HabilitarComandosYReaplicarTema(form);

            foreach (Button boton in ComandosRibbon(form))
            {
                // Los chips de color pintan su glifo de muestra con el color
                // elegido; su superficie tiene su propio contrato (abajo).
                if (ChipsDeColor.Contains(boton.Name))
                    continue;

                Assert.IsTrue(boton.Enabled, $"Precondición: {boton.Name} debe quedar habilitado en este escenario.");

                bool llevaTexto = !string.IsNullOrWhiteSpace(boton.Text);
                VerificarContraste(
                    fallos,
                    boton.Name,
                    llevaTexto ? "texto" : "icono",
                    boton.ForeColor,
                    fondo,
                    llevaTexto ? ContrasteTexto : ContrasteNoTextual);
            }

            foreach (var (clave, nombreCaption) in CaptionsPorGrupo)
            {
                Label caption = CaptionDelPanel(panel, nombreCaption);
                double contraste = Contraste(caption.ForeColor, fondo);
                if (contraste < ContrasteTexto)
                {
                    fallos.Add(
                        $"{nombreCaption} ({clave}): caption {Hex(caption.ForeColor)} sobre {Hex(fondo)} = "
                        + $"{contraste:0.00}:1 (mínimo {ContrasteTexto:0.0}:1)");
                }
            }

            Label indicador = LeerCampo<Label>(form, "lblColumna");
            double contrasteIndicador = Contraste(indicador.ForeColor, fondo);
            if (contrasteIndicador < ContrasteTexto)
            {
                fallos.Add(
                    $"lblColumna: indicador {Hex(indicador.ForeColor)} sobre {Hex(fondo)} = "
                    + $"{contrasteIndicador:0.00}:1 (mínimo {ContrasteTexto:0.0}:1)");
            }

            Assert.AreEqual(
                0,
                fallos.Count,
                $"La superficie efectiva del ribbon es {Hex(fondo)} (desde {origen}). Colores sin contraste suficiente:"
                + Environment.NewLine + string.Join(Environment.NewLine, fallos));
        });
    }

    [TestMethod]
    public void SelectoresYChips_SeIntegranConLaSuperficieYConservanSuLegibilidad()
    {
        ConFormulario(form =>
        {
            Panel panel = LeerCampo<Panel>(form, "panelRibbon");
            (Color fondo, _) = FondoEfectivo(panel);

            var fuente = LeerCampo<ComboBox>(form, "cboFuente");
            Assert.AreEqual(255, (int)fuente.BackColor.A, "El selector de fuente debe tener fondo opaco propio.");
            Assert.IsTrue(
                Contraste(fuente.ForeColor, fuente.BackColor) >= ContrasteTexto,
                $"cboFuente debe seguir legible sobre su propio fondo: {Hex(fuente.ForeColor)} sobre "
                + $"{Hex(fuente.BackColor)} = {Contraste(fuente.ForeColor, fuente.BackColor):0.00}:1.");

            var tamano = LeerCampo<NumericUpDown>(form, "nudTamano");
            Control editorTamano = tamano.Controls.Count > 0 ? tamano.Controls[0] : tamano;
            Assert.AreEqual(255, (int)editorTamano.BackColor.A, "El campo de edición del tamaño debe tener fondo opaco.");
            Assert.IsTrue(
                Contraste(tamano.ForeColor, editorTamano.BackColor) >= ContrasteTexto,
                $"nudTamano debe seguir legible sobre su propio fondo: {Hex(tamano.ForeColor)} sobre "
                + $"{Hex(editorTamano.BackColor)} = {Contraste(tamano.ForeColor, editorTamano.BackColor):0.00}:1.");

            // Los chips de color son "pozos" de muestra: si su superficie no se
            // funde con la banda, necesita un filete visible que los defina como
            // control y no como un hueco.
            foreach (string nombre in ChipsDeColor)
            {
                SoproButton chip = LeerCampo<SoproButton>(form, nombre);
                Assert.AreEqual(255, (int)chip.BackColor.A, $"{nombre}: la superficie del chip debe ser opaca.");

                if (Contraste(chip.BackColor, fondo) <= 1.5)
                    continue;

                Assert.IsTrue(
                    chip.FlatAppearance.BorderSize >= 1,
                    $"{nombre}: un pozo de muestra que contrasta con la banda ({Hex(chip.BackColor)} vs "
                    + $"{Hex(fondo)}) necesita filete (BorderSize {chip.FlatAppearance.BorderSize}).");
                Assert.IsTrue(
                    Contraste(chip.FlatAppearance.BorderColor, fondo) >= ContrasteMinimoSeparador,
                    $"{nombre}: el filete del pozo debe verse sobre la banda "
                    + $"({Hex(chip.FlatAppearance.BorderColor)} vs {Hex(fondo)}).");
            }
        });
    }

    [TestMethod]
    public void Chips_ElGlifoDeColorDeTextoEsLegibleEnReposoHoverYPresionado()
    {
        ConFormulario(form =>
        {
            SoproButton chip = LeerCampo<SoproButton>(form, "btnColorTexto");

            // Estado habilitado real (sin módulo activo el bloque de formato
            // nace apagado y el glifo se pinta con el token deshabilitado).
            HabilitarComandosYReaplicarTema(form);
            Assert.IsTrue(chip.Enabled, "Precondición: el chip de color de texto debe quedar habilitado.");

            Color? glifo = chip.SoproSampleGlyphColor;
            Assert.IsNotNull(
                glifo,
                $"{chip.Name}: debe configurar SoproSampleGlyphColor; el tono histórico claro del chrome oscuro "
                + "es ilegible sobre la ficha clara del ribbon (fix del glifo en reposo).");

            // Fondos que SoproButton.ResolveBackground usa para cada estado. Los
            // tres deben ser opacos y explícitos: un tinte vacío/transparente
            // haría que el estado cayera al fondo de reposo (sin feedback).
            Color reposo = chip.BackColor.A == 255 ? chip.BackColor : FondoEfectivo(chip).Color;
            Color hover = chip.FlatAppearance.MouseOverBackColor;
            Color presionado = chip.FlatAppearance.MouseDownBackColor;

            Assert.AreEqual(255, (int)reposo.A, $"{chip.Name}: el fondo de reposo debe ser opaco ({Hex(reposo)}).");
            Assert.AreEqual(255, (int)hover.A, $"{chip.Name}: el fondo de hover debe ser opaco ({Hex(hover)}).");
            Assert.AreEqual(
                255,
                (int)presionado.A,
                $"{chip.Name}: el fondo de presionado debe ser opaco ({Hex(presionado)}).");

            Assert.IsTrue(
                LuminanciaRelativa(reposo) >= LuminanciaMinimaSuperficieClara,
                $"Precondición: la ficha en reposo debe ser clara ({Hex(reposo)}, luminancia "
                + $"{LuminanciaRelativa(reposo):0.00}); si dejó de serlo, revisar este contrato.");

            // El glifo y el rótulo de muestra se pintan con el MISMO color en los
            // tres estados (solo cambia el fondo), así que la legibilidad se exige
            // también al pasar el ratón y al presionar, no solo en reposo.
            foreach ((string estado, Color fondoEstado) in new[]
                     {
                         ("reposo", reposo),
                         ("hover", hover),
                         ("presionado", presionado),
                     })
            {
                Assert.IsTrue(
                    Contraste(glifo!.Value, fondoEstado) >= ContrasteTexto,
                    $"{chip.Name}: el glifo/rotulo de muestra {Hex(glifo.Value)} debe alcanzar {ContrasteTexto:0.0}:1 "
                    + $"sobre el fondo de {estado} {Hex(fondoEstado)} (actual "
                    + $"{Contraste(glifo.Value, fondoEstado):0.00}:1).");
            }

            // Los tres estados siguen siendo distinguibles entre sí: un chip que
            // no cambia de fondo no da feedback de interacción.
            AfirmarFondosDistintos(chip.Name, "reposo", reposo, "hover", hover);
            AfirmarFondosDistintos(chip.Name, "hover", hover, "presionado", presionado);
            AfirmarFondosDistintos(chip.Name, "reposo", reposo, "presionado", presionado);
        });
    }

    [TestMethod]
    public void Grupos_CadaCaptionEsDistintaYSeCentraBajoSuGrupo()
    {
        ConFormulario(form =>
        {
            Panel panel = LeerCampo<Panel>(form, "panelRibbon");
            var textos = new List<string>();

            foreach (var (clave, nombreCaption) in CaptionsPorGrupo)
            {
                Label caption = CaptionDelPanel(panel, nombreCaption);
                Assert.IsFalse(
                    string.IsNullOrWhiteSpace(caption.Text),
                    $"{nombreCaption} debe rotular el grupo {clave}.");
                textos.Add(caption.Text.Trim());

                var controles = form.ObtenerControlesGrupoRibbon(clave)
                    .Where(control => control.Visible)
                    .ToList();
                Assert.IsTrue(controles.Count > 0, $"El grupo {clave} debe tener controles visibles en el ancho de diseño.");

                int izquierda = controles.Min(control => RectanguloEnPanel(control, panel).Left);
                int derecha = controles.Max(control => RectanguloEnPanel(control, panel).Right);
                Rectangle cajaCaption = RectanguloEnPanel(caption, panel);
                int centro = cajaCaption.Left + (cajaCaption.Width / 2);

                Assert.IsTrue(
                    centro >= izquierda - 4 && centro <= derecha + 4,
                    $"{nombreCaption} debe centrarse bajo su grupo {clave} (centro {centro} px; grupo {izquierda}..{derecha}).");
                Assert.IsTrue(
                    cajaCaption.Left >= 0 && cajaCaption.Right <= panel.ClientSize.Width
                    && cajaCaption.Bottom <= panel.ClientSize.Height,
                    $"{nombreCaption} ({cajaCaption}) debe quedar dentro de la banda {panel.ClientSize}.");

                foreach (Control control in controles)
                {
                    Assert.IsFalse(
                        cajaCaption.IntersectsWith(RectanguloEnPanel(control, panel)),
                        $"{nombreCaption} no debe pisar el comando {control.Name} del grupo {clave}.");
                }
            }

            Assert.AreEqual(
                CaptionsPorGrupo.Count,
                textos.Select(texto => texto.ToUpperInvariant()).Distinct().Count(),
                "Las seis captions deben ser textos distintos para que los grupos se distingan: "
                + string.Join(" / ", textos));
        });
    }

    [TestMethod]
    public void Separadores_SonHairlinesVisiblesYNoPisanComandos()
    {
        ConFormulario(form =>
        {
            Panel panel = LeerCampo<Panel>(form, "panelRibbon");
            (Color fondo, _) = FondoEfectivo(panel);
            var separadores = SeparadoresRibbon(panel);
            var controles = ControlesVisiblesRibbon(form, panel)
                .Concat(CaptionsPorGrupo.Values.Select(nombre => (Control)CaptionDelPanel(panel, nombre)))
                .Where(control => control.Visible)
                .ToList();

            Assert.IsTrue(
                separadores.Count >= 5,
                $"Se requieren separadores sutiles entre los seis grupos; se encontraron {separadores.Count} "
                + "controles verticales delgados en la banda.");

            Assert.AreEqual(
                separadores.Count,
                separadores.Select(separador => separador.Left).Distinct().Count(),
                "Cada separador debe ocupar una columna propia de la banda.");

            foreach (Control separador in separadores)
            {
                Assert.IsTrue(
                    separador.Width >= 1 && separador.Width <= 3 && separador.Height >= 24
                    && separador.Height >= separador.Width * 4,
                    $"{separador.Name} debe ser una hairline vertical ({separador.Size}).");

                Assert.AreEqual(255, (int)separador.BackColor.A, $"{separador.Name} debe tener color opaco.");
                Assert.AreNotEqual(
                    fondo.ToArgb(),
                    separador.BackColor.ToArgb(),
                    $"{separador.Name} debe distinguirse de la superficie {Hex(fondo)}.");
                Assert.IsTrue(
                    Contraste(separador.BackColor, fondo) >= ContrasteMinimoSeparador,
                    $"{separador.Name}: {Hex(separador.BackColor)} sobre {Hex(fondo)} = "
                    + $"{Contraste(separador.BackColor, fondo):0.00}:1 (mínimo {ContrasteMinimoSeparador:0.00}:1).");

                Rectangle caja = RectanguloEnPanel(separador, panel);
                Assert.IsTrue(
                    caja.Left >= 0 && caja.Right <= panel.ClientSize.Width && caja.Bottom <= panel.ClientSize.Height,
                    $"{separador.Name} ({caja}) debe quedar dentro de la banda {panel.ClientSize}.");

                foreach (Control control in controles)
                {
                    if (ReferenceEquals(control, separador))
                        continue;

                    Assert.IsFalse(
                        caja.IntersectsWith(RectanguloEnPanel(control, panel)),
                        $"{separador.Name} no debe invadir {control.Name}.");
                }
            }
        });
    }

    [TestMethod]
    public void Estados_ReposoHoverYPresionadoSeDistinguenEntreSi()
    {
        ConFormulario(form =>
        {
            Panel panel = LeerCampo<Panel>(form, "panelRibbon");
            (Color fondo, _) = FondoEfectivo(panel);

            foreach (Button boton in ComandosRibbon(form))
            {
                Assert.IsFalse(
                    boton.UseVisualStyleBackColor,
                    $"{boton.Name}: el ribbon pinta sus propios estados (UseVisualStyleBackColor debe ser false).");

                Color hover = boton.FlatAppearance.MouseOverBackColor;
                Color presionado = boton.FlatAppearance.MouseDownBackColor;

                Assert.AreEqual(255, (int)hover.A, $"{boton.Name}: el color de hover debe ser opaco.");
                Assert.AreEqual(255, (int)presionado.A, $"{boton.Name}: el color de presionado debe ser opaco.");
                Assert.IsTrue(
                    Contraste(hover, fondo) >= ContrasteMinimoSeparador,
                    $"{boton.Name}: el hover {Hex(hover)} debe percibirse sobre la superficie {Hex(fondo)}.");
                Assert.IsTrue(
                    Contraste(presionado, fondo) >= ContrasteMinimoSeparador,
                    $"{boton.Name}: el presionado {Hex(presionado)} debe percibirse sobre la superficie {Hex(fondo)}.");
                Assert.AreNotEqual(
                    hover.ToArgb(),
                    presionado.ToArgb(),
                    $"{boton.Name}: hover y presionado deben ser estados distintos.");
            }
        });
    }

    [TestMethod]
    public void Estados_Deshabilitado_SeDistingueDelHabilitadoYSigueSiendoPerceptible()
    {
        ConFormulario(form =>
        {
            Panel panel = LeerCampo<Panel>(form, "panelRibbon");
            (Color fondo, _) = FondoEfectivo(panel);
            SoproButton boton = LeerCampo<SoproButton>(form, "btnDepurarRibbon");

            HabilitarComandosYReaplicarTema(form);
            Assert.IsTrue(boton.Enabled, "Precondición: Depurar debe quedar habilitado en este escenario.");
            Assert.IsNotNull(boton.Image, "Precondición: el comando tiene icono rasterizado.");
            Assert.IsTrue(
                Contraste(boton.ForeColor, fondo) >= ContrasteNoTextual,
                "Precondición: el color habilitado contrasta con la banda.");

            int firmaHabilitado = FirmaIcono((Bitmap)boton.Image!);
            Color colorHabilitado = boton.ForeColor;

            // Deshabilitar + refrescar el tema: es el camino real (el estado
            // deshabilitado se pinta con el token apagado, no con el acento).
            boton.Enabled = false;
            AplicarTemaRibbon(form);

            Assert.IsNotNull(boton.Image, "El icono debe seguir presente deshabilitado.");
            Assert.AreNotEqual(
                firmaHabilitado,
                FirmaIcono((Bitmap)boton.Image!),
                $"{boton.Name}: el icono debe re-tintarse con {Hex(boton.SoproDisabledIconColor)} al deshabilitarse.");
            Assert.AreNotEqual(
                colorHabilitado.ToArgb(),
                boton.ForeColor.ToArgb(),
                $"{boton.Name}: el primer plano deshabilitado debe diferir del habilitado.");

            // WCAG exime a los componentes inactivos del contraste mínimo; aun
            // así el estado deshabilitado debe seguir viéndose (no desaparecer).
            const double ContrasteMinimoDeshabilitado = 2.0;
            Assert.IsTrue(
                Contraste(boton.ForeColor, fondo) >= ContrasteMinimoDeshabilitado,
                $"{boton.Name}: el primer plano deshabilitado {Hex(boton.ForeColor)} debe seguir perceptible sobre "
                + $"{Hex(fondo)} (actual {Contraste(boton.ForeColor, fondo):0.00}:1).");
            Assert.IsTrue(
                Contraste(boton.SoproDisabledIconColor, fondo) >= ContrasteMinimoDeshabilitado,
                $"{boton.Name}: el icono deshabilitado {Hex(boton.SoproDisabledIconColor)} debe seguir perceptible "
                + $"sobre {Hex(fondo)} (actual {Contraste(boton.SoproDisabledIconColor, fondo):0.00}:1).");

            boton.Enabled = true;
            AplicarTemaRibbon(form);
            Assert.AreEqual(
                firmaHabilitado,
                FirmaIcono((Bitmap)boton.Image!),
                $"{boton.Name}: al rehabilitar debe volver el icono del color habilitado.");
        });
    }

    [TestMethod]
    public void Estados_Seleccionado_SeDistingueDelReposoYConservaSuTextoLegible()
    {
        ConFormulario(form =>
        {
            Panel panel = LeerCampo<Panel>(form, "panelRibbon");
            (Color fondo, _) = FondoEfectivo(panel);
            Button boton = LeerCampo<Button>(form, "btnNegrita");

            HabilitarComandosYReaplicarTema(form);

            Color reposoPrimerPlano = boton.ForeColor;
            Color reposoFondo = boton.BackColor;
            Assert.IsTrue(
                Contraste(reposoPrimerPlano, fondo) >= ContrasteTexto,
                $"{boton.Name}: en reposo el primer plano debe ser legible sobre la banda.");

            // Marca el toggle como seleccionado por el MISMO estado que usa el
            // ribbon (Tag con RibbonButtonState.Active) y refresca el tema.
            MarcarBotonSeleccionado(boton, activo: true);
            AplicarTemaRibbon(form);

            Assert.AreNotEqual(
                reposoFondo.ToArgb(),
                boton.BackColor.ToArgb(),
                $"{boton.Name}: el comando seleccionado debe pintar una superficie propia, distinta del reposo.");
            Assert.AreEqual(255, (int)boton.BackColor.A, $"{boton.Name}: el fondo seleccionado debe ser opaco.");
            Assert.IsTrue(
                Contraste(boton.BackColor, fondo) >= ContrasteNoTextual,
                $"{boton.Name}: el fondo seleccionado {Hex(boton.BackColor)} debe distinguirse de la banda "
                + $"{Hex(fondo)} (actual {Contraste(boton.BackColor, fondo):0.00}:1).");
            Assert.IsTrue(
                Contraste(boton.ForeColor, boton.BackColor) >= ContrasteTexto,
                $"{boton.Name}: el texto del comando seleccionado debe ser legible sobre su fondo "
                + $"({Hex(boton.ForeColor)} sobre {Hex(boton.BackColor)} = "
                + $"{Contraste(boton.ForeColor, boton.BackColor):0.00}:1).");

            // Estados de puntero del comando SELECCIONADO: el ribbon oscurece el
            // azul activo, y el primer plano (texto/icono en blanco) debe seguir
            // legible sobre cada tinte; hover y presionado no pueden colapsar en
            // el mismo color (el comando no daría feedback de interacción).
            Color hoverActivo = boton.FlatAppearance.MouseOverBackColor;
            Color presionadoActivo = boton.FlatAppearance.MouseDownBackColor;

            Assert.AreEqual(
                255,
                (int)hoverActivo.A,
                $"{boton.Name}: el hover del comando seleccionado debe ser opaco ({Hex(hoverActivo)}).");
            Assert.AreEqual(
                255,
                (int)presionadoActivo.A,
                $"{boton.Name}: el presionado del comando seleccionado debe ser opaco ({Hex(presionadoActivo)}).");

            foreach ((string estado, Color fondoEstado) in new[]
                     {
                         ("hover", hoverActivo),
                         ("presionado", presionadoActivo),
                     })
            {
                Assert.IsTrue(
                    Contraste(boton.ForeColor, fondoEstado) >= ContrasteTexto,
                    $"{boton.Name}: el primer plano {Hex(boton.ForeColor)} del comando seleccionado debe alcanzar "
                    + $"{ContrasteTexto:0.0}:1 sobre el fondo de {estado} {Hex(fondoEstado)} (actual "
                    + $"{Contraste(boton.ForeColor, fondoEstado):0.00}:1).");
            }

            AfirmarFondosDistintos(boton.Name, "hover activo", hoverActivo, "presionado activo", presionadoActivo);

            MarcarBotonSeleccionado(boton, activo: false);
            AplicarTemaRibbon(form);
            Assert.AreEqual(
                reposoPrimerPlano.ToArgb(),
                boton.ForeColor.ToArgb(),
                $"{boton.Name}: al deseleccionar debe volver el primer plano del reposo.");
        });
    }

    [TestMethod]
    public void Foco_DibujaUnMarcoVisibleEnElBotonDelRibbon()
    {
        using var form = new Form
        {
            ClientSize = new Size(320, 180),
            StartPosition = FormStartPosition.Manual,
            Location = new Point(-4000, -4000),
            ShowInTaskbar = false,
        };

        using var boton = new BotonSoproEnfocable
        {
            SoproLayout = SoproButtonLayout.Large,
            SoproIcon = SoproIconType.Recalcular,
            SoproIconSize = 30,
            SoproShowText = true,
            Text = "Recalcular",
            Location = new Point(20, 20),
            Size = new Size(96, 60),
            ForeColor = Color.White,
            BackColor = SoproTheme.Colors.HeaderDark,
        };
        var otro = new TextBox { Location = new Point(20, 110), Width = 120 };
        form.Controls.Add(boton);
        form.Controls.Add(otro);

        form.Show();
        form.Activate();
        ProcesarMensajes();

        otro.Focus();
        ProcesarMensajes();
        if (!otro.Focused)
            Assert.Inconclusive("El entorno no permitió tomar el foco para comparar el repintado sin foco.");

        using Bitmap sinFoco = DibujarBoton(boton);

        Assert.IsTrue(boton.Focus(), "El botón del ribbon debe poder recibir el foco por teclado.");
        ProcesarMensajes();
        if (!boton.Focused)
            Assert.Inconclusive("El botón no conservó el foco en este entorno; no se puede comparar el marco.");

        using Bitmap conFoco = DibujarBoton(boton);
        (int diferentes, Rectangle caja) = Diferencias(sinFoco, conFoco);

        Assert.IsTrue(
            diferentes > 0,
            "El foco debe ser visible: el repintado con foco no puede ser idéntico al de reposo (issue #180).");

        Assert.IsTrue(
            caja.Left <= 4 && caja.Top <= 4
            && caja.Right >= boton.Width - 5 && caja.Bottom >= boton.Height - 5,
            $"El foco debe dibujar un marco junto al borde interior; las diferencias ocupan {caja} "
            + $"en un botón de {boton.Size}.");

        Assert.AreEqual(
            sinFoco.GetPixel(boton.Width / 2, boton.Height / 2).ToArgb(),
            conFoco.GetPixel(boton.Width / 2, boton.Height / 2).ToArgb(),
            "El foco debe ser un marco, no un relleno que tape el contenido del botón.");
    }

    /// <summary>
    /// Regresión del aro de foco sobre un comando REAL del ribbon en estado
    /// seleccionado/activo (fondo azul de marca): el aro histórico (BrandLt)
    /// caía a ~1.8:1 sobre ese azul. Se renderiza el propio control del
    /// formulario con foco de teclado y se exige que el aro aporte píxeles con
    /// contraste no textual (>= 3:1) contra el azul activo.
    /// </summary>
    [TestMethod]
    public void Foco_ComandoActivoDelRibbon_MantieneElAroVisibleSobreElAzul()
    {
        ConFormulario(form =>
        {
            SoproButton boton = LeerCampo<SoproButton>(form, "btnWrapRibbon");

            HabilitarComandosYReaplicarTema(form);
            MarcarBotonSeleccionado(boton, activo: true);
            AplicarTemaRibbon(form);

            Color azulActivo = boton.BackColor;
            Assert.AreEqual(
                255,
                (int)azulActivo.A,
                $"Precondición: el comando activo debe tener un fondo opaco ({Hex(azulActivo)}).");
            Assert.IsTrue(
                Contraste(azulActivo, Color.White) >= ContrasteTexto,
                $"Precondición: el azul activo {Hex(azulActivo)} debe ser oscuro (texto claro legible).");

            form.Activate();
            boton.Focus();
            ProcesarMensajes();
            if (!boton.Focused)
                Assert.Inconclusive($"El entorno no permitió enfocar {boton.Name}; no se puede renderizar el foco.");

            // ShowFocusCues depende de la preferencia de teclado del sistema:
            // se fija el estado interno de WinForms para comparar el mismo
            // repintado con y sin aro (sin tocar el control ni su producción).
            if (!FijarCuesDeFoco(boton, visibles: false))
                Assert.Inconclusive("No se pudo controlar ShowFocusCues en este runtime; no se puede aislar el aro.");

            using Bitmap sinAro = Renderizar(boton);

            FijarCuesDeFoco(boton, visibles: true);
            using Bitmap conAro = Renderizar(boton);

            (int diferentes, Rectangle caja) = Diferencias(sinAro, conAro);
            Assert.IsTrue(
                diferentes >= 8,
                $"{boton.Name}: el comando activo debe dibujar un aro de foco visible ({diferentes} px distintos).");
            Assert.IsTrue(
                caja.Left <= 4 && caja.Top <= 4
                && caja.Right >= boton.Width - 5 && caja.Bottom >= boton.Height - 5,
                $"{boton.Name}: el aro debe recorrer el borde interior; las diferencias ocupan {caja} "
                + $"en un botón de {boton.Size}.");

            int pixelesVisibles = ContarPixelesConContraste(conAro, sinAro, azulActivo, ContrasteNoTextual);
            Assert.IsTrue(
                pixelesVisibles >= 8,
                $"{boton.Name}: sobre el azul activo {Hex(azulActivo)} el aro debe aportar píxeles con "
                + $">= {ContrasteNoTextual:0.0}:1 (encontrados {pixelesVisibles}; el aro azul simple histórico "
                + $"caía a ~1.8:1).");

            MarcarBotonSeleccionado(boton, activo: false);
            AplicarTemaRibbon(form);
            FijarCuesDeFoco(boton, visibles: false);
        });
    }

    [TestMethod]
    public void Teclado_ElOrdenDeTabulacionSigueElOrdenFisicoDeLosGrupos()
    {
        ConFormulario(form =>
        {
            var secuencia = new List<(string Grupo, Control Control)>();

            foreach (string clave in form.ObtenerClavesGruposRibbon())
            {
                foreach (Control control in form.ObtenerControlesGrupoRibbon(clave))
                {
                    if (control.TabStop)
                        secuencia.Add((clave, control));
                }
            }

            Assert.IsTrue(
                secuencia.Count >= 20,
                $"Todos los comandos y selectores deben ser alcanzables por teclado; hay {secuencia.Count} controles con TabStop.");

            for (int i = 1; i < secuencia.Count; i++)
            {
                (string grupoAnterior, Control anterior) = secuencia[i - 1];
                (string grupoActual, Control actual) = secuencia[i];

                Assert.IsTrue(
                    anterior.TabIndex < actual.TabIndex,
                    $"El tabulador debe seguir Datos → Exportar → Fuente → Alineación → Colores → Celdas: "
                    + $"{anterior.Name} ({grupoAnterior}, TabIndex {anterior.TabIndex}) queda antes de "
                    + $"{actual.Name} ({grupoActual}, TabIndex {actual.TabIndex}).");
            }
        });
    }

    [TestMethod]
    public void AnchosRepresentativos_CaptionsYSeparadoresNoSeRecortanNiSeSolapan()
    {
        ConFormulario(form =>
        {
            Panel panel = LeerCampo<Panel>(form, "panelRibbon");
            int captionsVerificadas = 0;
            int separadoresVerificados = 0;

            foreach (int ancho in new[] { 240, 360, 520, 720, 960, 1200, 1600 })
            {
                AplicarAncho(form, ancho);

                var captions = CaptionsPorGrupo
                    .Select(par => (Grupo: par.Key, Caption: CaptionDelPanel(panel, par.Value)))
                    .Where(par => par.Caption.Visible)
                    .ToList();
                var separadores = SeparadoresRibbon(panel).Where(separador => separador.Visible).ToList();
                var comandos = ControlesVisiblesRibbon(form, panel).ToList();
                captionsVerificadas += captions.Count;
                separadoresVerificados += separadores.Count;

                foreach (var (grupo, caption) in captions)
                {
                    Rectangle caja = RectanguloEnPanel(caption, panel);
                    Assert.IsTrue(
                        caja.Left >= 0 && caja.Right <= panel.ClientSize.Width
                        && caja.Top >= 0 && caja.Bottom <= panel.ClientSize.Height,
                        $"{caption.Name} del grupo {grupo} se recorta a {ancho} px (caja {caja}, banda {panel.ClientSize}).");

                    foreach (Control comando in comandos)
                    {
                        Assert.IsFalse(
                            caja.IntersectsWith(RectanguloEnPanel(comando, panel)),
                            $"{caption.Name} se superpone con {comando.Name} a {ancho} px.");
                    }

                    foreach (var (otroGrupo, otro) in captions)
                    {
                        if (ReferenceEquals(otro, caption) || string.CompareOrdinal(grupo, otroGrupo) >= 0)
                            continue;

                        Assert.IsFalse(
                            caja.IntersectsWith(RectanguloEnPanel(otro, panel)),
                            $"Las captions {caption.Name} y {otro.Name} se solapan a {ancho} px.");
                    }
                }

                foreach (Control separador in separadores)
                {
                    Rectangle caja = RectanguloEnPanel(separador, panel);
                    Assert.IsTrue(
                        caja.Left >= 0 && caja.Right <= panel.ClientSize.Width && caja.Bottom <= panel.ClientSize.Height,
                        $"{separador.Name} se recorta a {ancho} px (caja {caja}, banda {panel.ClientSize}).");

                    foreach (Control comando in comandos)
                    {
                        Assert.IsFalse(
                            caja.IntersectsWith(RectanguloEnPanel(comando, panel)),
                            $"{separador.Name} se superpone con {comando.Name} a {ancho} px.");
                    }
                }

                // El orden visual de captions y grupos visibles sigue siendo el
                // orden semántico (prefijo desde Datos hacia la derecha).
                int derechaAnterior = int.MinValue;
                foreach (string clave in ClavesEsperadas)
                {
                    var visibles = form.ObtenerControlesGrupoRibbon(clave)
                        .Where(control => control.Visible)
                        .ToList();
                    if (visibles.Count == 0)
                        continue;

                    int izquierda = visibles.Min(control => RectanguloEnPanel(control, panel).Left);
                    int derecha = visibles.Max(control => RectanguloEnPanel(control, panel).Right);

                    Assert.IsTrue(
                        izquierda >= derechaAnterior,
                        $"A {ancho} px el grupo {clave} debe quedar a la derecha del grupo anterior "
                        + $"(izquierda {izquierda} vs borde {derechaAnterior}).");
                    derechaAnterior = derecha;
                }
            }

            // Guarda anti-vacuidad: los anchos elegidos deben ejercitar de
            // verdad captions y separadores visibles (parcialmente colapsados).
            Assert.IsTrue(
                captionsVerificadas >= 10,
                $"Los anchos representativos deben dejar captions visibles para verificar; se revisaron {captionsVerificadas}.");
            Assert.IsTrue(
                separadoresVerificados >= 5,
                $"Los anchos representativos deben dejar separadores visibles para verificar; se revisaron {separadoresVerificados}.");
        });
    }

    [TestMethod]
    public void SelectorDeFuente_ElAltoDeFilaCubreElTextoEnEscalas125Y150()
    {
        // Proxy determinista del escalado por DPI: AutoScaleMode.Font lleva la
        // fuente base de 9 pt a 11.25 pt (125 %) y 13.5 pt (150 %); el alto del
        // item owner-draw debe cubrir siempre el texto medido.
        foreach (float puntos in new[] { 9F * 1.25F, 9F * 1.5F })
        {
            using var fuente = new Font("Segoe UI", puntos, FontStyle.Regular, GraphicsUnit.Point);
            int altoTexto = TextRenderer.MeasureText("Mg", fuente).Height;
            int alto = SoproTheme.CalcularItemHeightComboRibbon(altoTexto);

            Assert.IsTrue(
                alto >= altoTexto + 2,
                $"A {puntos:0.##} pt el alto de fila ({alto} px) debe cubrir el texto ({altoTexto} px) con holgura.");
            Assert.IsTrue(alto >= 20, $"A {puntos:0.##} pt el alto de fila base ({alto} px) no puede bajar de 20 px.");
        }
    }

    // ═════════════════════════════ Infraestructura ═════════════════════════════

    private static IReadOnlyList<Button> ComandosRibbon(FormProyecto form)
        => ClavesEsperadas
            .SelectMany(clave => form.ObtenerControlesGrupoRibbon(clave))
            .OfType<Button>()
            .ToList();

    private static IEnumerable<Control> ControlesVisiblesRibbon(FormProyecto form, Panel panel)
        => ClavesEsperadas
            .SelectMany(clave => form.ObtenerControlesGrupoRibbon(clave))
            .Where(control => control.Visible
                && (control is Button || control is ComboBox || control is NumericUpDown
                    || control.Name == "lblColumna"));

    private static List<Control> SeparadoresRibbon(Panel panel)
        => panel.Controls
            .Cast<Control>()
            .Where(control => control.Width >= 1 && control.Width <= 3
                && control.Height >= 24 && control.Height >= control.Width * 4)
            .ToList();

    private static Label CaptionDelPanel(Panel panel, string nombre)
        => panel.Controls.Find(nombre, false).FirstOrDefault() as Label
            ?? throw new AssertFailedException($"Falta la caption '{nombre}' en la banda del ribbon.");

    private static void AplicarAncho(FormProyecto form, int ancho)
    {
        Panel panel = LeerCampo<Panel>(form, "panelRibbon");
        panel.Width = ancho;
        form.ActualizarOverflowRibbon();
    }

    /// <summary>
    /// Enciende todos los comandos de los seis grupos y re-aplica el tema del
    /// ribbon: así se evalúa el aspecto HABILITADO (sin módulo activo el ribbon
    /// nace con el bloque de formato apagado).
    /// </summary>
    private static void HabilitarComandosYReaplicarTema(FormProyecto form)
    {
        foreach (Control control in ClavesEsperadas.SelectMany(clave => form.ObtenerControlesGrupoRibbon(clave)))
            control.Enabled = true;

        AplicarTemaRibbon(form);
    }

    /// <summary>
    /// Reconstruye el <c>RibbonButtonState</c> del botón (registro privado del
    /// formulario) con el flag <c>Active</c> indicado, preservando icono,
    /// tamaño y opciones. Es el mismo estado con el que el ribbon decide el
    /// pintado del comando seleccionado.
    /// </summary>
    private static void MarcarBotonSeleccionado(Button boton, bool activo)
    {
        object? estado = boton.Tag;
        if (estado == null)
            throw new AssertFailedException($"{boton.Name} no expone un RibbonButtonState en Tag.");

        Type tipo = estado.GetType();
        PropertyInfo? icono = tipo.GetProperty("IconType");
        PropertyInfo? tamano = tipo.GetProperty("IconSize");
        PropertyInfo? opciones = tipo.GetProperty("Options");
        ConstructorInfo? constructor = tipo
            .GetConstructors(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .SingleOrDefault(ctor => ctor.GetParameters().Length == 4);

        if (constructor == null)
            throw new AssertFailedException($"El estado '{tipo.Name}' de {boton.Name} no tiene el constructor esperado.");

        boton.Tag = constructor.Invoke(new object?[]
        {
            icono?.GetValue(estado),
            activo,
            tamano?.GetValue(estado) ?? 16,
            opciones?.GetValue(estado),
        });
    }

    /// <summary>
    /// Re-aplica el tema del ribbon por el mismo método que usan los caminos
    /// reales de refresco (cambio de pestaña, estado contextual, desactivación).
    /// </summary>
    private static void AplicarTemaRibbon(FormProyecto form)
    {
        MethodInfo? metodo = typeof(FormProyecto).GetMethod(
            "AplicarTemaCompletoRibbon",
            BindingFlags.Instance | BindingFlags.NonPublic);

        if (metodo == null)
        {
            throw new AssertFailedException(
                "No se encontró FormProyecto.AplicarTemaCompletoRibbon; el contrato de estados del ribbon cambió.");
        }

        try
        {
            metodo.Invoke(form, null);
        }
        catch (TargetInvocationException ex) when (ex.InnerException != null)
        {
            throw new AssertFailedException(
                $"AplicarTemaCompletoRibbon falló: {ex.InnerException.GetType().Name}: {ex.InnerException.Message}",
                ex.InnerException);
        }
    }

    private static void VerificarContraste(
        List<string> fallos,
        string control,
        string rol,
        Color color,
        Color fondo,
        double minimo = ContrasteTexto)
    {
        double contraste = Contraste(color, fondo);
        if (contraste < minimo)
            fallos.Add($"{control}: {rol} {Hex(color)} sobre {Hex(fondo)} = {contraste:0.00}:1 (mínimo {minimo:0.0}:1)");
    }

    /// <summary>
    /// Dos fondos de estado deben ser colores distintos y percibirse como un
    /// cambio (tinte suave admitido: umbral bajo, pero mayor que "idéntico").
    /// </summary>
    private static void AfirmarFondosDistintos(
        string control,
        string estadoA,
        Color fondoA,
        string estadoB,
        Color fondoB)
    {
        Assert.AreNotEqual(
            fondoA.ToArgb(),
            fondoB.ToArgb(),
            $"{control}: {estadoA} y {estadoB} no pueden compartir el mismo fondo ({Hex(fondoA)}).");

        Assert.IsTrue(
            Contraste(fondoA, fondoB) >= ContrasteMinimoEntreEstados,
            $"{control}: los fondos de {estadoA} ({Hex(fondoA)}) y {estadoB} ({Hex(fondoB)}) deben distinguirse "
            + $"(contraste {Contraste(fondoA, fondoB):0.00}:1, mínimo {ContrasteMinimoEntreEstados:0.00}:1).");
    }

    /// <summary>
    /// Color con el que realmente se pinta la banda: el primer ancestro con
    /// BackColor opaco (panelRibbon suele ser Transparent sobre el header).
    /// </summary>
    private static (Color Color, string Origen) FondoEfectivo(Control control)
    {
        Control? actual = control;
        while (actual != null)
        {
            Color color = actual.BackColor;
            if (color.A == 255)
                return (color, actual.Name.Length > 0 ? actual.Name : actual.GetType().Name);

            actual = actual.Parent;
        }

        throw new AssertFailedException(
            $"No se pudo resolver un fondo opaco para '{control.Name}'; la banda no tiene superficie propia.");
    }

    private static Rectangle RectanguloEnPanel(Control control, Panel panel)
    {
        Point origen = control.Location;
        Control? padre = control.Parent;
        while (padre != null && !ReferenceEquals(padre, panel))
        {
            origen.Offset(padre.Location);
            padre = padre.Parent;
        }

        return new Rectangle(origen, control.Size);
    }

    private static int FirmaIcono(Bitmap bitmap)
    {
        int firma = 17;
        for (int y = 0; y < bitmap.Height; y += 2)
        {
            for (int x = 0; x < bitmap.Width; x += 2)
                firma = unchecked((firma * 31) + bitmap.GetPixel(x, y).ToArgb());
        }

        return firma;
    }

    private static Bitmap DibujarBoton(BotonSoproEnfocable boton)
    {
        _ = boton.Handle;

        var bitmap = new Bitmap(boton.Width, boton.Height);
        using (Graphics graphics = Graphics.FromImage(bitmap))
            boton.PintarEn(graphics);

        return bitmap;
    }

    /// <summary>
    /// Renderiza un control REAL por su propia ruta de pintado (<c>OnPaint</c>),
    /// incluyendo los manejadores de <c>Paint</c> que dibujan el aro de foco.
    /// </summary>
    private static Bitmap Renderizar(Control control)
    {
        _ = control.Handle;

        MethodInfo? onPaint = control.GetType().GetMethod(
            "OnPaint", BindingFlags.Instance | BindingFlags.NonPublic);
        if (onPaint == null)
            throw new AssertFailedException($"No se pudo resolver OnPaint de {control.GetType().Name}.");

        var bitmap = new Bitmap(control.Width, control.Height);
        using (Graphics graphics = Graphics.FromImage(bitmap))
        {
            onPaint.Invoke(
                control,
                new object[] { new PaintEventArgs(graphics, new Rectangle(Point.Empty, control.ClientSize)) });
        }

        return bitmap;
    }

    /// <summary>
    /// Fija el estado de "cues" de teclado del control (interno de WinForms:
    /// <c>Control._uiCuesState</c>) para poder comparar el repintado con y sin
    /// aro de foco sin depender de la preferencia del sistema. Devuelve false
    /// si el runtime no expone ese estado (la prueba queda inconclusa).
    /// </summary>
    private static bool FijarCuesDeFoco(Control control, bool visibles)
    {
        FieldInfo? campo = typeof(Control).GetField(
            "_uiCuesState", BindingFlags.Instance | BindingFlags.NonPublic);
        if (campo == null || !campo.FieldType.IsEnum)
            return false;

        string nombre = visibles ? "FocusShow" : "FocusHidden";
        if (!Enum.GetNames(campo.FieldType).Contains(nombre))
            return false;

        campo.SetValue(control, Enum.Parse(campo.FieldType, nombre));
        return true;
    }

    /// <summary>
    /// Cuenta los píxeles que cambian entre dos renders y cuyo color nuevo
    /// alcanza el contraste pedido contra el fondo indicado (p. ej. el azul del
    /// comando activo). Aísla el aro de foco sin fijar su color exacto.
    /// </summary>
    private static int ContarPixelesConContraste(Bitmap conAro, Bitmap sinAro, Color fondo, double minimo)
    {
        int contados = 0;
        for (int y = 0; y < conAro.Height; y++)
        {
            for (int x = 0; x < conAro.Width; x++)
            {
                Color nuevo = conAro.GetPixel(x, y);
                if (nuevo.ToArgb() == sinAro.GetPixel(x, y).ToArgb())
                    continue;

                if (Contraste(nuevo, fondo) >= minimo)
                    contados++;
            }
        }

        return contados;
    }

    private static (int Diferentes, Rectangle Caja) Diferencias(Bitmap anterior, Bitmap actual)
    {
        int diferentes = 0;
        int minimoX = int.MaxValue, minimoY = int.MaxValue, maximoX = int.MinValue, maximoY = int.MinValue;

        for (int y = 0; y < anterior.Height; y++)
        {
            for (int x = 0; x < anterior.Width; x++)
            {
                if (anterior.GetPixel(x, y).ToArgb() == actual.GetPixel(x, y).ToArgb())
                    continue;

                diferentes++;
                minimoX = Math.Min(minimoX, x);
                minimoY = Math.Min(minimoY, y);
                maximoX = Math.Max(maximoX, x);
                maximoY = Math.Max(maximoY, y);
            }
        }

        if (diferentes == 0)
            return (0, Rectangle.Empty);

        return (diferentes, Rectangle.FromLTRB(minimoX, minimoY, maximoX + 1, maximoY + 1));
    }

    private static double LuminanciaRelativa(Color color)
    {
        static double Linealizar(byte componente)
        {
            double canal = componente / 255.0;
            return canal <= 0.04045 ? canal / 12.92 : Math.Pow((canal + 0.055) / 1.055, 2.4);
        }

        return (0.2126 * Linealizar(color.R))
            + (0.7152 * Linealizar(color.G))
            + (0.0722 * Linealizar(color.B));
    }

    private static double Contraste(Color primero, Color segundo)
    {
        double a = LuminanciaRelativa(primero);
        double b = LuminanciaRelativa(segundo);
        double claro = Math.Max(a, b);
        double oscuro = Math.Min(a, b);
        return (claro + 0.05) / (oscuro + 0.05);
    }

    private static string Hex(Color color) => $"#{color.R:X2}{color.G:X2}{color.B:X2}";

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

    /// <summary>
    /// Botón que fuerza <see cref="Control.ShowFocusCues"/> para poder validar
    /// el marco de foco sin depender de la preferencia de teclado del sistema.
    /// </summary>
    private sealed class BotonSoproEnfocable : SoproButton
    {
        protected override bool ShowFocusCues => true;

        public void PintarEn(Graphics graphics)
            => OnPaint(new PaintEventArgs(graphics, new Rectangle(Point.Empty, ClientSize)));
    }

    private static void ConFormulario(Action<FormProyecto> prueba)
    {
        string root = Path.Combine(Path.GetTempPath(), $"sopro_ribbon_tema_{Guid.NewGuid():N}");
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
                Nombre = "Proyecto de prueba del tema del ribbon",
                Descripcion = string.Empty,
                Ubicacion = string.Empty,
                Convocante = string.Empty,
                Contratista = string.Empty,
                ApoderadoLegal = string.Empty,
            })
            {
                // Fuera de pantalla para no interferir con el escritorio; el
                // Load real aplica el tema (SoproTheme + InicializarRibbon).
                WindowState = FormWindowState.Normal,
                StartPosition = FormStartPosition.Manual,
                Location = new Point(-4000, -4000),
                ShowInTaskbar = false,
            };

            form.Show();
            ProcesarMensajes();

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
