using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SOPRO.WinForms.Forms;
using SOPRO.WinForms.Helpers;
using SOPRO.WinForms.UI.Controls;

namespace SOPRO.Tests.WinForms.Ribbon;

/// <summary>
/// Contrato COMPACTO del mockup SOPRO (seguimiento de #180). Es una extensión
/// parcial de <see cref="RibbonTemaYEstadosTests"/> para reutilizar sus
/// helpers de formulario STA, colores y geometría sin duplicarlos; los tests y
/// su semántica son los mismos que antes vivían en el archivo principal:
///
///   1. La banda usa iconos PEQUEÑOS (12..20 px lógicos) rasterizados a la
///      escala del DPI, no iconos grandes dentro de una banda de dos filas.
///   2. La banda no reserva alto que no pinta: sin aire muerto en sus bordes,
///      entre comandos y rótulos, ni dentro de cada comando.
///   3. Los chips de color se leen INLINE, sin ficha ni marco en reposo, sobre
///      la superficie de la banda; el color de muestra sigue siendo una marca
///      pequeña y visible, y los rótulos legibles en reposo, hover y
///      presionado (con estados de puntero distintos).
///   4. Rótulos, separadores y grupos comparten la misma banda vertical.
///   5. El overflow oculta grupos sin comprimir ni recortar ningún control.
///
/// Todo se evalúa sobre los controles reales del formulario, sin capturas ni
/// píxeles incidentales.
/// </summary>
public partial class RibbonTemaYEstadosTests
{
    // Compactez de la banda (mockup SOPRO): el aire vertical que puede
    // reservarse por encima/debajo del contenido, entre comandos y rótulos, y
    // alrededor de cada comando. Son píxeles LÓGICOS (se escalan por DPI en
    // cada aserción), no coordenadas del lane de diseño.
    private const int AireMaximoBandaPx = 6;
    private const int AireMaximoComandoPx = 12;

    // Desalineación admitida entre el inicio/fin de los grupos y entre los
    // centros de los controles de una misma fila.
    private const int ToleranciaAlineacionPx = 4;

    // Fracción máxima de píxeles oscuros en el pintado de un chip: el color de
    // muestra debe seguir siendo una MARCA (barra/cuadro) sobre la ficha clara,
    // no un relleno que convierta el control en un bloque de color.
    private const double ProporcionMaximaOscuraChip = 0.35;

    // Integración INLINE del mockup: en reposo el chip se funde con la banda
    // (misma superficie, sin ficha) y no dibuja un marco exterior. El límite de
    // 1.10 entre el borde pintado y la superficie equivale a "no hay filete
    // dibujado"; el de 1.50 admite un tinte de superficie apenas perceptible.
    private const double ContrasteMaximoFichaEnReposo = 1.50;
    private const double ContrasteMaximoMarcoExterior = 1.10;

    // La marca de muestra debe seguir viéndose dentro del chip (glifo, barra o
    // cuadro con su contorno) frente a la superficie en reposo.
    private const double ContrasteMinimoMarcaMuestra = 2.0;
    private const int PixelesMinimosMarcaMuestra = 8;

    /// <summary>
    /// Los comandos del ribbon deben llevar iconos PEQUEÑOS del sistema
    /// (<see cref="SoproUiMetrics.RibbonGlyphButtonIconSize"/> con holgura) y
    /// rasterizados al tamaño lógico escalado por DPI. Un icono de 30 px dentro
    /// de una banda de dos filas es el principal responsable del aspecto
    /// "pesado" que el mockup corrige.
    /// </summary>
    [TestMethod]
    public void Iconos_UsanTamanoLogicoCompactoYSeRasterizanALaEscalaDelDpi()
    {
        ConFormulario(form =>
        {
            HabilitarComandosYReaplicarTema(form);

            const int IconoMinimoLogico = 12;
            int iconoMaximoLogico = SoproUiMetrics.RibbonGlyphButtonIconSize + 4;
            var fallos = new List<string>();

            foreach (SoproButton boton in ComandosSopro(form))
            {
                // Los chips de color son fichas de muestra (glifo + barra), no
                // comandos con icono rasterizado.
                if (boton.SoproLayout is SoproButtonLayout.SampleLetter or SoproButtonLayout.SampleSwatch)
                    continue;

                if (!boton.SoproIcon.HasValue)
                {
                    fallos.Add($"{boton.Name}: comando sin SoproIcon asignado.");
                    continue;
                }

                int logico = boton.SoproIconSize;
                if (logico < IconoMinimoLogico || logico > iconoMaximoLogico)
                {
                    fallos.Add(
                        $"{boton.Name}: icono lógico {logico} px fuera del rango compacto "
                        + $"{IconoMinimoLogico}..{iconoMaximoLogico} (sistema: "
                        + $"{SoproUiMetrics.RibbonGlyphButtonIconSize} px).");
                }

                if (boton.Image is not Bitmap bitmap)
                {
                    fallos.Add($"{boton.Name}: no hay bitmap de icono que verificar.");
                    continue;
                }

                int esperado = Math.Max(
                    IconoMinimoLogico,
                    (int)Math.Round(logico * (boton.DeviceDpi / 96.0)));

                if (bitmap.Width != esperado || bitmap.Height != esperado)
                {
                    fallos.Add(
                        $"{boton.Name}: el bitmap {bitmap.Size} no corresponde al tamaño lógico {logico} px "
                        + $"x {boton.DeviceDpi / 96.0:0.##} de DPI (se espera {esperado}x{esperado}).");
                }
            }

            Assert.AreEqual(
                0,
                fallos.Count,
                "La banda compacta del mockup usa iconos pequeños y nítidos:" + Environment.NewLine
                + string.Join(Environment.NewLine, fallos));
        });
    }

    /// <summary>
    /// La banda no debe reservar alto que no pinte: ni aire muerto en sus
    /// bordes, ni un hueco entre los comandos y la fila de rótulos, ni comandos
    /// mucho más altos que su contenido (icono + texto). Un botón de 60 px para
    /// un contenido de ~50 px con un icono de 30 px es el "demasiado alto" que
    /// reporta el mockup.
    /// </summary>
    [TestMethod]
    public void Banda_Compacta_SinAireMuertoEntreComandosFilasYRotulos()
    {
        ConFormulario(form =>
        {
            Panel panel = LeerCampo<Panel>(form, "panelRibbon");
            HabilitarComandosYReaplicarTema(form);

            int aireMaximo = Escalar(panel, AireMaximoBandaPx);
            var comandos = ComandosRibbon(form).Where(comando => comando.Visible).ToList();
            var captions = CaptionsPorGrupo.Values
                .Select(nombre => CaptionDelPanel(panel, nombre))
                .Where(caption => caption.Visible)
                .ToList();

            Assert.IsTrue(
                comandos.Count >= 18 && captions.Count == CaptionsPorGrupo.Count,
                $"Precondición: en el ancho de diseño deben verse los 18 comandos y 6 rótulos "
                + $"(comandos {comandos.Count}, rótulos {captions.Count}).");

            int arriba = comandos.Min(comando => RectanguloEnPanel(comando, panel).Top);
            int abajo = comandos.Max(comando => RectanguloEnPanel(comando, panel).Bottom);
            int rotuloArriba = captions.Min(caption => RectanguloEnPanel(caption, panel).Top);
            int rotuloAbajo = captions.Max(caption => RectanguloEnPanel(caption, panel).Bottom);

            Assert.IsTrue(
                arriba <= aireMaximo,
                $"La banda no debe empezar con aire muerto: el primer comando empieza en y={arriba} "
                + $"(máximo {aireMaximo} px).");

            Assert.IsTrue(
                rotuloArriba >= abajo && rotuloArriba - abajo <= aireMaximo,
                $"Los rótulos deben ir inmediatamente bajo los comandos: comandos hasta y={abajo}, "
                + $"rótulos desde y={rotuloArriba} (hueco máximo {aireMaximo} px).");

            Assert.IsTrue(
                panel.ClientSize.Height - rotuloAbajo <= aireMaximo,
                $"La banda no debe terminar con aire muerto: los rótulos acaban en y={rotuloAbajo} "
                + $"de una banda de {panel.ClientSize.Height} px (máximo {aireMaximo} px de sobra).");

            var fallos = new List<string>();
            foreach (Button comando in comandos)
            {
                int contenido = AltoContenidoPintado(comando);
                int aire = Escalar(comando, AireMaximoComandoPx);
                if (comando.Height > contenido + aire)
                {
                    fallos.Add(
                        $"{comando.Name}: {comando.Height} px de alto para {contenido} px de contenido "
                        + $"(máximo {contenido + aire} px con {AireMaximoComandoPx} px lógicos de aire).");
                }
            }

            Assert.AreEqual(
                0,
                fallos.Count,
                "Los comandos no deben reservar un halo vertical que engorde la banda:" + Environment.NewLine
                + string.Join(Environment.NewLine, fallos));
        });
    }

    /// <summary>
    /// Los chips de color son acciones INLINE del mockup: en reposo se funden
    /// con la superficie de la banda (sin ficha ni marco exterior) y el color
    /// elegido se lee como una marca pequeña y visible (barra/cuadro/glifo).
    /// El rótulo conserva contraste en reposo, hover y presionado, y los tres
    /// estados de puntero se distinguen entre sí (feedback de interacción).
    /// </summary>
    [TestMethod]
    public void Chips_SeFundenConLaBandaEnReposoYConservanMuestraYRotuloLegible()
    {
        ConFormulario(form =>
        {
            Panel panel = LeerCampo<Panel>(form, "panelRibbon");
            (Color fondo, _) = FondoEfectivo(panel);
            HabilitarComandosYReaplicarTema(form);

            foreach (string nombre in ChipsDeColor)
            {
                SoproButton chip = LeerCampo<SoproButton>(form, nombre);

                Assert.IsTrue(
                    chip.SoproLayout is SoproButtonLayout.SampleLetter or SoproButtonLayout.SampleSwatch,
                    $"{nombre}: debe ser una ficha de muestra, no un comando genérico ({chip.SoproLayout}).");
                Assert.IsFalse(
                    string.IsNullOrWhiteSpace(chip.SoproCaption),
                    $"{nombre}: el chip de color necesita su rótulo (SoproCaption) para explicar la muestra.");

                // El reposo puede declararse opaco o transparente: lo que cuenta
                // es la superficie EFECTIVA sobre la que se pinta el chip.
                Color reposo = chip.BackColor.A == 255 ? chip.BackColor : FondoEfectivo(chip).Color;
                Color hover = chip.FlatAppearance.MouseOverBackColor;
                Color presionado = chip.FlatAppearance.MouseDownBackColor;

                Assert.AreEqual(255, (int)reposo.A, $"{nombre}: la superficie de reposo debe ser opaca ({Hex(reposo)}).");
                Assert.IsTrue(
                    Contraste(reposo, fondo) <= ContrasteMaximoFichaEnReposo,
                    $"{nombre}: en reposo el chip debe fundirse con la banda {Hex(fondo)} "
                    + $"(contraste {Contraste(reposo, fondo):0.00}:1, máximo {ContrasteMaximoFichaEnReposo:0.00}:1); "
                    + "no puede leerse como un bloque de color.");
                Assert.IsTrue(
                    chip.FlatAppearance.BorderSize <= 2,
                    $"{nombre}: el chip inline no puede llevar un marco grueso "
                    + $"(BorderSize {chip.FlatAppearance.BorderSize}).");

                // Sin caja en reposo, el aro de foco es la única señal de teclado:
                // debe conservarse el filete dual del ribbon.
                Assert.IsTrue(
                    chip.SoproDualFocusCue,
                    $"{nombre}: al no tener marco en reposo, el chip debe conservar el aro de foco dual "
                    + "(SoproDualFocusCue) para que el foco de teclado siga siendo visible.");

                Color? glifo = chip.SoproSampleGlyphColor;
                Assert.IsNotNull(
                    glifo,
                    $"{nombre}: debe fijar SoproSampleGlyphColor para que el glifo/rótulo se lea sobre la superficie de la banda.");

                foreach ((string estado, Color fondoEstado) in new[]
                         {
                             ("reposo", reposo),
                             ("hover", hover),
                             ("presionado", presionado),
                         })
                {
                    Assert.AreEqual(
                        255,
                        (int)fondoEstado.A,
                        $"{nombre}: el fondo de {estado} debe ser opaco ({Hex(fondoEstado)}).");
                    Assert.IsTrue(
                        Contraste(glifo!.Value, fondoEstado) >= ContrasteTexto,
                        $"{nombre}: el glifo/rótulo {Hex(glifo.Value)} debe alcanzar {ContrasteTexto:0.0}:1 sobre el "
                        + $"fondo de {estado} {Hex(fondoEstado)} (actual "
                        + $"{Contraste(glifo.Value, fondoEstado):0.00}:1).");
                }

                // Feedback de interacción: hover y presionado no pueden colapsar
                // en el mismo tinte ni en la superficie de reposo.
                AfirmarFondosDistintos(chip.Name, "reposo", reposo, "hover", hover);
                AfirmarFondosDistintos(chip.Name, "hover", hover, "presionado", presionado);

                using Bitmap pintado = Renderizar(chip);
                Color superficie = pintado.GetPixel(
                    Math.Min(2, pintado.Width - 1),
                    Math.Min(2, pintado.Height - 1));

                Assert.IsTrue(
                    Contraste(superficie, fondo) <= ContrasteMaximoFichaEnReposo,
                    $"{nombre}: la superficie PINTADA en reposo {Hex(superficie)} no se funde con la banda "
                    + $"{Hex(fondo)} (contraste {Contraste(superficie, fondo):0.00}:1).");

                (int marcados, double maximo) = MedirMarcoLateral(pintado, superficie);
                Assert.AreEqual(
                    0,
                    marcados,
                    $"{nombre}: en reposo no debe dibujar un marco exterior sobre la banda "
                    + $"({marcados} px de los bordes laterales contrastan {maximo:0.00}:1 con la superficie; "
                    + $"máximo {ContrasteMaximoMarcoExterior:0.00}:1).");

                Rectangle zonaMarca = ZonaMarcaMuestra(chip, pintado.Width);
                int pixelesMarca = ContarPixelesContrastados(pintado, zonaMarca, superficie, ContrasteMinimoMarcaMuestra);
                Assert.IsTrue(
                    pixelesMarca >= PixelesMinimosMarcaMuestra,
                    $"{nombre}: la marca de muestra debe seguir viéndose en {zonaMarca} "
                    + $"({pixelesMarca} px con >= {ContrasteMinimoMarcaMuestra:0.0}:1 sobre la superficie).");

                double proporcionOscura = ProporcionOscura(pintado);
                Assert.IsTrue(
                    proporcionOscura <= ProporcionMaximaOscuraChip,
                    $"{nombre}: el {proporcionOscura:P0} del chip se pinta oscuro; el color de muestra debe ser "
                    + $"una marca pequeña sobre la superficie clara (máximo {ProporcionMaximaOscuraChip:P0}).");
            }
        });
    }

    /// <summary>
    /// Alineación vertical del mockup: la fila de rótulos es única, los
    /// separadores recorren la franja de comandos (sin invadir los rótulos) y
    /// cada grupo alinea sus controles en como máximo dos filas dentro de una
    /// misma banda vertical (sin escalones entre grupos).
    /// </summary>
    [TestMethod]
    public void Filas_RotulosYSeparadores_CompartenLaMismaBandaVertical()
    {
        ConFormulario(form =>
        {
            Panel panel = LeerCampo<Panel>(form, "panelRibbon");
            int tolerancia = Escalar(panel, ToleranciaAlineacionPx);

            var captions = CaptionsPorGrupo.Values
                .Select(nombre => CaptionDelPanel(panel, nombre))
                .ToList();
            int rotuloTop = captions[0].Top;
            int rotuloAlto = captions[0].Height;

            foreach (Label caption in captions)
            {
                Assert.IsTrue(
                    Math.Abs(caption.Top - rotuloTop) <= tolerancia,
                    $"Los rótulos deben compartir la misma línea base: {caption.Name} está en y={caption.Top}, "
                    + $"la referencia en y={rotuloTop} (tolerancia {tolerancia} px).");
                Assert.IsTrue(
                    Math.Abs(caption.Height - rotuloAlto) <= tolerancia,
                    $"Los rótulos deben tener el mismo alto: {caption.Name} mide {caption.Height} px, "
                    + $"la referencia {rotuloAlto} px (tolerancia {tolerancia} px).");
            }

            if (!captions.Any(caption => caption.Visible))
                Assert.Inconclusive("El ancho actual oculta todos los rótulos; no se puede verificar la alineación.");

            var comandos = ComandosRibbon(form).Cast<Control>().ToList();
            int franjaTop = comandos.Min(comando => RectanguloEnPanel(comando, panel).Top);
            int franjaAbajo = comandos.Max(comando => RectanguloEnPanel(comando, panel).Bottom);

            foreach (Control separador in SeparadoresRibbon(panel))
            {
                Rectangle caja = RectanguloEnPanel(separador, panel);
                Assert.IsTrue(
                    Math.Abs(caja.Top - franjaTop) <= tolerancia,
                    $"{separador.Name}: el separador debe empezar con la franja de comandos "
                    + $"(y={caja.Top} vs {franjaTop}).");
                Assert.IsTrue(
                    Math.Abs(caja.Bottom - franjaAbajo) <= tolerancia,
                    $"{separador.Name}: el separador debe acabar con la franja de comandos "
                    + $"(y={caja.Bottom} vs {franjaAbajo}).");
                Assert.IsTrue(
                    caja.Bottom <= captions.Min(caption => RectanguloEnPanel(caption, panel).Top),
                    $"{separador.Name}: el separador no debe invadir la fila de rótulos (termina en y={caja.Bottom}).");
            }

            var envolventes = new Dictionary<string, (int Top, int Abajo)>();
            foreach (string clave in form.ObtenerClavesGruposRibbon())
            {
                var controles = form.ObtenerControlesGrupoRibbon(clave)
                    .Where(control => control.Visible)
                    .ToList();
                if (controles.Count == 0)
                    continue;

                var centros = controles
                    .Select(control =>
                    {
                        Rectangle caja = RectanguloEnPanel(control, panel);
                        return caja.Top + (caja.Height / 2.0);
                    })
                    .OrderBy(centro => centro)
                    .ToList();

                int filas = 1;
                for (int i = 1; i < centros.Count; i++)
                {
                    if (centros[i] - centros[i - 1] > tolerancia)
                        filas++;
                }

                Assert.IsTrue(
                    filas <= 2,
                    $"El grupo {clave} reparte sus controles en {filas} filas; la banda compacta usa como "
                    + $"máximo dos (centros: {string.Join(", ", centros.Select(centro => centro.ToString("0.0")))}, "
                    + $"tolerancia {tolerancia} px).");

                envolventes[clave] = (
                    controles.Min(control => RectanguloEnPanel(control, panel).Top),
                    controles.Max(control => RectanguloEnPanel(control, panel).Bottom));
            }

            if (envolventes.Count <= 1)
                Assert.Inconclusive("No hay suficientes grupos visibles para comparar sus bandas verticales.");

            int inicioComun = envolventes.Values.Min(envolvente => envolvente.Top);
            int finComun = envolventes.Values.Max(envolvente => envolvente.Abajo);

            foreach (var (clave, envolvente) in envolventes)
            {
                Assert.IsTrue(
                    Math.Abs(envolvente.Top - inicioComun) <= tolerancia,
                    $"El grupo {clave} no comparte el inicio vertical de la banda: empieza en y={envolvente.Top} "
                    + $"vs y={inicioComun} (tolerancia {tolerancia} px).");
                Assert.IsTrue(
                    Math.Abs(envolvente.Abajo - finComun) <= tolerancia,
                    $"El grupo {clave} no comparte el fin vertical de la banda: acaba en y={envolvente.Abajo} "
                    + $"vs y={finComun} (tolerancia {tolerancia} px).");
            }
        });
    }

    /// <summary>
    /// El overflow debe OCULTAR grupos completos sin recomprimir controles: los
    /// tamaños de comandos, rótulos y separadores se conservan en todos los
    /// anchos y vuelven intactos al ensanchar. Así el ribbon no "respira" ni
    /// cambia de aspecto (ni de jerarquía visual) al reducir la ventana.
    /// </summary>
    [TestMethod]
    public void Overflow_ConservaLosTamanosDeComandosRotulosYSeparadores()
    {
        ConFormulario(form =>
        {
            Panel panel = LeerCampo<Panel>(form, "panelRibbon");
            form.ActualizarOverflowRibbon();

            var referencia = ControlesDeLayoutRibbon(form, panel)
                .ToDictionary(control => control.Name, control => control.Size);

            Assert.IsTrue(
                referencia.Count >= 30,
                $"Precondición: deben vigilarse comandos, rótulos y separadores; hay {referencia.Count}.");

            int visiblesAnteriores = int.MaxValue;
            bool huboParcial = false;

            foreach (int ancho in new[] { 1280, 1024, 900, 720, 520, 360, 200, 40 })
            {
                AplicarAncho(form, ancho);

                foreach (Control control in ControlesDeLayoutRibbon(form, panel))
                {
                    Assert.AreEqual(
                        referencia[control.Name],
                        control.Size,
                        $"{control.Name} cambió de tamaño a {ancho} px: el overflow oculta grupos, no comprime "
                        + $"controles (era {referencia[control.Name]}, ahora {control.Size}).");
                }

                int visibles = ClavesEsperadas.Count(clave => form.ObtenerControlesGrupoRibbon(clave)
                    .Any(control => !form.EstaOcultoPorOverflowRibbon(control)));

                Assert.IsTrue(
                    visibles <= visiblesAnteriores,
                    $"Al reducir el ancho no pueden aparecer más grupos (a {ancho} px: {visibles} visibles, "
                    + $"antes {visiblesAnteriores}).");
                visiblesAnteriores = visibles;

                if (visibles > 0 && visibles < ClavesEsperadas.Length)
                    huboParcial = true;
            }

            Assert.IsTrue(
                huboParcial,
                "Los anchos elegidos deben ejercitar un overflow parcial para verificar que no comprime controles.");

            AplicarAncho(form, 4000);

            foreach (Control control in ControlesDeLayoutRibbon(form, panel))
            {
                Assert.AreEqual(
                    referencia[control.Name],
                    control.Size,
                    $"{control.Name} debe recuperar su tamaño original al ensanchar (era "
                    + $"{referencia[control.Name]}, es {control.Size}).");
            }

            Assert.IsFalse(
                ComandosRibbon(form).Any(form.EstaOcultoPorOverflowRibbon),
                "Con ancho de sobra deben volver los seis grupos completos.");
        });
    }

    private static IReadOnlyList<SoproButton> ComandosSopro(FormProyecto form)
        => ComandosRibbon(form).OfType<SoproButton>().ToList();

    /// <summary>
    /// Comandos, rótulos y separadores del ribbon (los que el overflow no debe
    /// recomprimir ni recortar).
    /// </summary>
    private static IReadOnlyList<Control> ControlesDeLayoutRibbon(FormProyecto form, Panel panel)
        => ClavesEsperadas
            .SelectMany(clave => form.ObtenerControlesGrupoRibbon(clave))
            .Concat(CaptionsPorGrupo.Values.Select(nombre => (Control)CaptionDelPanel(panel, nombre)))
            .Concat(SeparadoresRibbon(panel))
            .ToList();

    /// <summary>Escala un valor en píxeles lógicos al DPI real del control.</summary>
    private static int Escalar(Control control, int pixelesLogicos)
        => Math.Max(
            pixelesLogicos,
            (int)Math.Round(pixelesLogicos * (control.DeviceDpi > 0 ? control.DeviceDpi / 96.0 : 1.0)));

    /// <summary>
    /// Alto que el control pinta realmente (icono, texto, glifo y rótulo de
    /// muestra), sin el aire de layout. Es la referencia de compactez de la
    /// banda: el alto reservado no debe excederla más allá de
    /// <see cref="AireMaximoComandoPx"/>.
    /// </summary>
    private static int AltoContenidoPintado(Button comando)
    {
        int altoTexto = AltoTextoPintado(comando.Text, comando.Font);

        if (comando is not SoproButton sopro)
            return altoTexto;

        float escala = sopro.DeviceDpi > 0 ? sopro.DeviceDpi / 96f : 1f;
        int icono = (int)Math.Round(sopro.SoproIconSize * escala);
        int altoRotulo = AltoTextoPintado(sopro.SoproCaption, sopro.Font);

        switch (sopro.SoproLayout)
        {
            case SoproButtonLayout.Large:
                return icono + (altoTexto > 0 ? Math.Max(2, (int)Math.Round(4 * escala)) + altoTexto : 0);

            case SoproButtonLayout.SampleLetter:
                // Glifo + barra de muestra, o el rótulo si es más alto.
                return Math.Max(altoTexto + 1 + 4, altoRotulo);

            case SoproButtonLayout.SampleSwatch:
                // Cuadro de 12 px o el rótulo si es más alto.
                return Math.Max(12, altoRotulo);

            default:
                return Math.Max(sopro.SoproIcon.HasValue ? icono : 0, sopro.SoproShowText ? altoTexto : 0);
        }
    }

    private static int AltoTextoPintado(string? texto, Font fuente)
        => string.IsNullOrEmpty(texto)
            ? 0
            : TextRenderer.MeasureText(
                texto,
                fuente,
                new Size(int.MaxValue, int.MaxValue),
                TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix).Height;

    private static int AnchoTextoPintado(string? texto, Font fuente)
        => string.IsNullOrEmpty(texto)
            ? 0
            : TextRenderer.MeasureText(
                texto,
                fuente,
                new Size(int.MaxValue, int.MaxValue),
                TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix).Width;

    /// <summary>
    /// Cuántos píxeles de los bordes LATERALES del chip se separan de su
    /// superficie en reposo. Un chip inline no dibuja marco: ambos laterales
    /// deben ser la misma superficie de la banda.
    /// </summary>
    private static (int Marcados, double Maximo) MedirMarcoLateral(Bitmap pintado, Color superficie)
    {
        int marcados = 0;
        double maximo = 0;

        foreach (int x in new[] { 0, pintado.Width - 1 })
        {
            for (int y = 0; y < pintado.Height; y++)
            {
                double contraste = Contraste(pintado.GetPixel(x, y), superficie);
                maximo = Math.Max(maximo, contraste);
                if (contraste > ContrasteMaximoMarcoExterior)
                    marcados++;
            }
        }

        return (marcados, maximo);
    }

    private static int ContarPixelesContrastados(Bitmap pintado, Rectangle zona, Color fondo, double minimo)
    {
        int contados = 0;
        Rectangle limite = Rectangle.Intersect(zona, new Rectangle(Point.Empty, pintado.Size));

        for (int y = limite.Top; y < limite.Bottom; y++)
        {
            for (int x = limite.Left; x < limite.Right; x++)
            {
                if (Contraste(pintado.GetPixel(x, y), fondo) >= minimo)
                    contados++;
            }
        }

        return contados;
    }

    /// <summary>
    /// Franja donde PaintSample dibuja el glifo/cuadro de muestra: el bloque
    /// (marca + separación + rótulo) se centra en el control, así que la marca
    /// ocupa su ancho a la izquierda del bloque.
    /// </summary>
    private static Rectangle ZonaMarcaMuestra(SoproButton chip, int anchoControl)
    {
        int anchoMarca = chip.SoproLayout == SoproButtonLayout.SampleSwatch
            ? 12
            : Math.Max(10, AnchoTextoPintado(chip.Text, chip.Font));
        int anchoRotulo = AnchoTextoPintado(chip.SoproCaption, chip.Font);
        int total = anchoMarca + (anchoRotulo > 0 ? 6 + anchoRotulo : 0);
        int izquierda = Math.Max(0, (anchoControl - total) / 2);
        int ancho = Math.Max(1, Math.Min(anchoMarca, anchoControl - izquierda));

        return new Rectangle(izquierda, 0, ancho, chip.Height);
    }

    /// <summary>
    /// Fracción de píxeles oscuros del bitmap: mide si el control se pinta como
    /// un bloque de color (pesado) o como una marca sobre una ficha clara.
    /// </summary>
    private static double ProporcionOscura(Bitmap bitmap)
    {
        int oscuros = 0;
        for (int y = 0; y < bitmap.Height; y++)
        {
            for (int x = 0; x < bitmap.Width; x++)
            {
                if (LuminanciaRelativa(bitmap.GetPixel(x, y)) < 0.35)
                    oscuros++;
            }
        }

        return bitmap.Width * bitmap.Height == 0
            ? 0
            : oscuros / (double)(bitmap.Width * bitmap.Height);
    }
}
