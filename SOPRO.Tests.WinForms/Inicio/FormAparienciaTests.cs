using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SOPRO.WinForms.Forms;
using SOPRO.WinForms.Helpers;
using SOPRO.WinForms.Services;
using SOPRO.WinForms.UI.Controls;

namespace SOPRO.Tests.WinForms.Inicio;

/// <summary>
/// Contrato del diálogo Apariencia (issue #203, fase 1): tres opciones reales
/// de RadioButton con mini-muestra de paleta, selección inicial tomada del
/// almacén persistido, aviso de aplicación al reiniciar y guardado que NUNCA
/// muta la paleta activa del proceso. El almacén apunta SIEMPRE a un
/// directorio temporal: no se lee ni escribe la preferencia real del usuario.
/// </summary>
[STATestClass]
[DoNotParallelize]
public class FormAparienciaTests
{
    private string? _directorioTemporal;

    private string DirectorioTemporal()
    {
        _directorioTemporal ??= Path.Combine(
            Path.GetTempPath(), "sopro-tests-apariencia-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_directorioTemporal);
        return _directorioTemporal;
    }

    [TestCleanup]
    public void Limpiar()
    {
        if (_directorioTemporal == null) return;
        try { Directory.Delete(_directorioTemporal, recursive: true); } catch { /* limpieza best-effort */ }
        _directorioTemporal = null;
    }

    [TestMethod]
    public void Dialogo_OfreceExactamenteLasTresOpcionesComoRadioButtonsReales()
    {
        using var dialogo = new FormApariencia(new SoproTemaStore(DirectorioTemporal()));

        var opciones = EnumerarControles(dialogo).OfType<RadioButton>().ToList();

        Assert.AreEqual(3, opciones.Count, "El diálogo ofrece exactamente tres presets.");
        CollectionAssert.AreEquivalent(
            new[] { "Claro azul (predeterminado)", "Oscuro grafito", "Claro verde" },
            opciones.Select(opcion => opcion.Text).ToArray());

        // Semántica accesible: las opciones viven en un GroupBox con el
        // rotulo del grupo y navegan por flechas de forma nativa.
        var grupo = opciones.Select(opcion => Ancestros(opcion).OfType<GroupBox>().FirstOrDefault())
            .OfType<GroupBox>()
            .Distinct()
            .Single();
        StringAssert.Contains(grupo.Text, "Tema de la interfaz");
        Assert.IsTrue(opciones.All(opcion => opcion.AccessibleName != null),
            "Cada opción tiene AccessibleName para lectores de pantalla.");
    }

    [TestMethod]
    public void Dialogo_CadaOpcionTieneMiniMuestraConLosColoresDeSuPaleta()
    {
        using var dialogo = new FormApariencia(new SoproTemaStore(DirectorioTemporal()));

        var muestras = EnumerarControles(dialogo).OfType<Panel>()
            .Where(panel => panel.Name != null && panel.Name.StartsWith("swatch", StringComparison.Ordinal))
            .ToList();

        Assert.HasCount(3, muestras, "Cada opción muestra su propia paleta en miniatura.");
        foreach (var muestra in muestras)
        {
            Assert.IsInstanceOfType(muestra.Tag, typeof(SoproTemaPaleta),
                "La mini-muestra se pinta con la paleta del PRESET (vía GetPalette), no con el tema activo.");
            var paleta = (SoproTemaPaleta)muestra.Tag;
            Assert.IsFalse(paleta.Surface.IsEmpty && paleta.Brand.IsEmpty && paleta.Ink.IsEmpty,
                "La muestra usa tokens superficie/acento/texto de la paleta.");
        }
    }

    [TestMethod]
    public void Dialogo_SeleccionaPorDefectoElTemaPredeterminadoSinPreferenciaGuardada()
    {
        using var dialogo = new FormApariencia(new SoproTemaStore(DirectorioTemporal()));

        Assert.AreEqual(SoproTemaPreset.ClaroAzul, dialogo.PresetSeleccionado,
            "Sin preferencia previa, la opción marcada es Claro azul (predeterminado).");
        Assert.AreEqual(SoproTemaPreset.ClaroAzul, dialogo.Store.Load(),
            "La carga inicial no escribe en el almacén.");
    }

    [TestMethod]
    public void Dialogo_SeleccionaLaOpcionGuardadaEnElAlmacen()
    {
        var directorio = DirectorioTemporal();
        using (var preparacion = new FormApariencia(new SoproTemaStore(directorio)))
        {
            preparacion.EnumerarOpciones().Single(opcion => opcion.Text == "Oscuro grafito").Checked = true;
            Mostrar(preparacion);
            preparacion.BotonPie("Guardar").PerformClick();
        }

        using (var dialogo = new FormApariencia(new SoproTemaStore(directorio)))
        {
            Assert.AreEqual(SoproTemaPreset.Grafito, dialogo.Store.Load(),
                "El almacén reporta la preferencia persistida.");
            var marcada = dialogo.EnumerarOpciones().Single(opcion => opcion.Checked);
            Assert.AreEqual("Oscuro grafito", marcada.Text,
                "La selección inicial refleja la elección guardada del inicio anterior.");
        }
    }

    [TestMethod]
    public void Dialogo_GuardarPersisteLaOpcionMarcadaYAvisaQueElCambioSeAplicaAlReiniciar()
    {
        var directorio = DirectorioTemporal();
        using var dialogo = new FormApariencia(new SoproTemaStore(directorio));

        var aviso = EnumerarControles(dialogo).OfType<Label>()
            .Single(etiqueta => etiqueta.Text == FormApariencia.AvisoReinicio);
        Assert.AreEqual("El tema guardado se aplicará al reiniciar SOPRO.", aviso.Text);

        dialogo.EnumerarOpciones().Single(opcion => opcion.Text == "Claro verde").Checked = true;
        Mostrar(dialogo);
        dialogo.BotonPie("Guardar").PerformClick();

        Assert.AreEqual(DialogResult.OK, dialogo.DialogResult, "Un guardado exitoso cierra el diálogo con éxito.");
        Assert.AreEqual(SoproTemaPreset.ClaroVerde, new SoproTemaStore(directorio).Load(),
            "La preferencia quedó persistida para el próximo inicio.");
    }

    [TestMethod]
    public void Dialogo_GuardarNoMutaLaPaletaActivaDelProceso()
    {
        using var dialogo = new FormApariencia(new SoproTemaStore(DirectorioTemporal()));

        var paletaAntes = SoproTheme.CurrentPalette;
        var idAntes = paletaAntes?.Id;

        // Seleccionar y guardar NO toca el tema activo: el diálogo nunca
        // llama a InitializePalette.
        dialogo.EnumerarOpciones().Single(opcion => opcion.Text == "Oscuro grafito").Checked = true;
        Mostrar(dialogo);
        dialogo.BotonPie("Guardar").PerformClick();

        Assert.IsTrue(ReferenceEquals(paletaAntes, SoproTheme.CurrentPalette),
            "CurrentPalette conserva la MISMA instancia tras seleccionar y guardar.");
        Assert.AreEqual(idAntes, SoproTheme.CurrentPalette?.Id);
    }

    [TestMethod]
    public void Dialogo_SiElGuardadoFallaMuestraErrorYNoCierraNiAnunciaExito()
    {
        // Directorio imposible de crear: debajo de un ARCHIVO existente.
        var archivo = Path.Combine(DirectorioTemporal(), "bloqueo");
        File.WriteAllText(archivo, string.Empty);
        var directorioInvalido = Path.Combine(archivo, "tema");

        using var dialogo = new FormApariencia(new SoproTemaStore(directorioInvalido));
        dialogo.EnumerarOpciones().Single(opcion => opcion.Text == "Claro verde").Checked = true;
        Mostrar(dialogo);
        dialogo.BotonPie("Guardar").PerformClick();

        Assert.AreNotEqual(DialogResult.OK, dialogo.DialogResult,
            "Un guardado fallido no marca el diálogo como exitoso.");
        Assert.AreEqual(FormApariencia.MensajeErrorGuardado, dialogo.EstadoGuardado.Text,
            "Se muestra un mensaje de error en el cuerpo del diálogo.");
        Assert.AreEqual(SoproTheme.Colors.Error, dialogo.EstadoGuardado.ForeColor,
            "El error usa el token de la paleta activa, no un color fijo local.");
        Assert.IsFalse(dialogo.IsDisposed, "El diálogo NO se cierra cuando falla el guardado.");

        // El mensaje completo permanece dentro de la tarjeta: se envuelve al
        // ancho disponible y crece en ALTO (regresión del recorte de un texto
        // de ~532px medidos en un contenido de ~410px).
        var contenido = (TableLayoutPanel)dialogo.EstadoGuardado.Parent!;
        var estado = dialogo.EstadoGuardado;

        Assert.AreEqual(Math.Max(1, contenido.ClientSize.Width - estado.Margin.Horizontal),
            estado.MaximumSize.Width,
            "El ancho máximo del rótulo es el ancho real del contenido de la tarjeta.");
        Assert.IsTrue(estado.Bounds.Right <= contenido.ClientSize.Width,
            $"El mensaje no se desborda horizontalmente (Right={estado.Bounds.Right}, contenido={contenido.ClientSize.Width}px).");
        Assert.IsTrue(estado.Bounds.Bottom <= contenido.ClientSize.Height,
            "El mensaje envuelto no se recorta por debajo del contenido.");

        int altoEnvueltoMedido = SoproDialogTheme.CalcularAltoEtiqueta(
            FormApariencia.MensajeErrorGuardado, estado.Font, estado.Bounds.Width);
        Assert.IsTrue(estado.Height >= altoEnvueltoMedido,
            $"El alto del rótulo ({estado.Height}px) debe contener el texto envuelto ({altoEnvueltoMedido}px).");
    }

    [TestMethod]
    public void MensajeDeError_SeEnvuelveAlAnchoDisponibleEnCualquierDpi()
    {
        // Regresión del bug de recorte: a 96 DPI el mensaje mide ~532px a una
        // línea, MÁS que los ~410px del contenido del diálogo de 460px. La
        // verificación es PURA (fuentes y anchos construidos, sin depender del
        // monitor real): el ancho disponible y el cuerpo del texto escalan
        // JUNTOS al cambiar el DPI, así que el conteo de líneas envueltas es
        // estable en 100% y 150%.
        using var fuente96 = new Font("Segoe UI", 9.5F, FontStyle.Regular, GraphicsUnit.Point);
        int anchoContenido96 = FormApariencia.AnchoCliente
            - 2 * SoproDialogTheme.MargenHorizontalContenido - 2;

        int altoUnaLinea96 = SoproDialogTheme.CalcularAltoEtiqueta(
            FormApariencia.MensajeErrorGuardado, fuente96, int.MaxValue);
        int altoEnvuelto96 = SoproDialogTheme.CalcularAltoEtiqueta(
            FormApariencia.MensajeErrorGuardado, fuente96, anchoContenido96);

        Assert.IsTrue(altoEnvuelto96 > altoUnaLinea96,
            $"A 96 DPI el mensaje DEBE envolver a más de una línea (una línea={altoUnaLinea96}px > contenido={anchoContenido96}px).");
        Assert.IsTrue(altoEnvuelto96 <= altoUnaLinea96 * 4,
            $"A 96 DPI el mensaje envuelto no crece sin límite (envuelto={altoEnvuelto96}px).");

        // 150% (≈144 DPI): mismo texto, ancho y fuente 1.5x.
        using var fuente150 = new Font("Segoe UI", 9.5F * 1.5F, FontStyle.Regular, GraphicsUnit.Point);
        int altoUnaLinea150 = SoproDialogTheme.CalcularAltoEtiqueta(
            FormApariencia.MensajeErrorGuardado, fuente150, int.MaxValue);
        int altoEnvuelto150 = SoproDialogTheme.CalcularAltoEtiqueta(
            FormApariencia.MensajeErrorGuardado, fuente150, anchoContenido96 * 3 / 2);

        Assert.IsTrue(altoEnvuelto150 > altoUnaLinea150, "A 150% el mensaje también se envuelve.");
        Assert.IsTrue(altoEnvuelto150 >= altoEnvuelto96 && altoEnvuelto150 <= altoEnvuelto96 * 2,
            $"El alto envuelto escala con el DPI (96={altoEnvuelto96}px, 150%={altoEnvuelto150}px).");

        // El diálogo conserva la holgura vertical para la segunda línea:
        // alto de cuerpo >= contenido envuelto a 96 + aviso + grupo sin recorte.
        Assert.IsTrue(FormApariencia.AltoCliente
            >= SoproDialogTheme.AltoEncabezado + SoproDialogTheme.AltoPie + altoEnvuelto96 * 4,
            "El alto fijo del diálogo deja espacio para el mensaje envuelto sin invadir encabezado ni pie.");
    }

    [TestMethod]
    public void Dialogo_UsaElCromoSoproDeDialogos()
    {
        using var dialogo = new FormApariencia(new SoproTemaStore(DirectorioTemporal()));

        Assert.AreEqual("Apariencia", dialogo.Text);
        Assert.AreEqual(FormBorderStyle.FixedDialog, dialogo.FormBorderStyle);
        Assert.AreEqual(SoproTheme.Colors.CenterBg, dialogo.BackColor);
        Assert.AreEqual(dialogo.BotonPie("Guardar"), dialogo.AcceptButton);
        Assert.AreEqual(dialogo.BotonPie("Cancelar"), dialogo.CancelButton);
    }

    // ---------------------------------------------------------------------

    /// <summary>
    /// Muestra el diálogo no-modal y bombea mensajes pendientes: el
    /// PerformClick requiere handles creados para disparar Click.
    /// </summary>
    private static void Mostrar(Form form)
    {
        form.Show();
        System.Windows.Forms.Application.DoEvents();
    }

    private static IEnumerable<Control> EnumerarControles(Control raiz)
    {
        foreach (Control control in raiz.Controls)
        {
            yield return control;
            foreach (var descendiente in EnumerarControles(control))
                yield return descendiente;
        }
    }

    private static IEnumerable<Control> Ancestros(Control control)
    {
        for (var actual = control.Parent; actual is not null; actual = actual.Parent)
            yield return actual;
    }
}

/// <summary>Atajos internos de navegación para las pruebas del diálogo.</summary>
internal static class FormAparienciaTestExtensions
{
    public static IEnumerable<RadioButton> EnumerarOpciones(this Form dialogo)
        => EnumerarRaiz(dialogo).OfType<RadioButton>();

    public static SoproButton BotonPie(this Form dialogo, string texto)
        => EnumerarRaiz(dialogo).OfType<SoproButton>().Single(boton => boton.Text == texto);

    private static IEnumerable<Control> EnumerarRaiz(Control raiz)
    {
        foreach (Control control in raiz.Controls)
        {
            yield return control;
            foreach (var descendiente in EnumerarRaiz(control))
                yield return descendiente;
        }
    }
}
