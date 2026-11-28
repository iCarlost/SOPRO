using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SOPRO.WinForms.Forms;
using SOPRO.WinForms.Helpers;

namespace SOPRO.Tests.WinForms.Diagnostics;

/// <summary>
/// Pruebas del diálogo fatal sin abrirlo de forma modal: se instancia, se
/// inspeccionan textos y botones y se disparan los clics por reflexión sobre
/// <see cref="Control.OnClick"/>. No se llama a <c>Application.Exit</c>.
/// </summary>
[STATestClass]
[DoNotParallelize] // Formularios WinForms sobre un hilo STA dedicado.
public class FormDiagnosticoFatalTests
{
    private const string CodigoPrueba = "ABCD2345";
    private const string CarpetaPrueba = @"C:\Users\sintetico\AppData\Local\SOPRO\logs";

    private static readonly Action<string> AbrirCarpetaSinEfecto = _ => { };

    [TestMethod]
    public void Dialogo_ExponeCorrelacionCarpetaYTextosEnEspanol()
    {
        using var dialogo = new FormDiagnosticoFatal(CodigoPrueba, CarpetaPrueba, AbrirCarpetaSinEfecto);

        Assert.AreEqual("Error grave", dialogo.TextoTitulo);
        Assert.AreEqual("Error grave", dialogo.Text);
        Assert.AreEqual(CodigoPrueba, dialogo.CodigoReferencia);
        Assert.AreEqual(CarpetaPrueba, dialogo.CarpetaRegistros);
        Assert.AreEqual("Código de referencia", dialogo.TextoCaptionCodigo);
        Assert.AreEqual("Registros locales", dialogo.TextoCaptionRegistros);
        StringAssert.Contains(dialogo.MensajePrincipal, "problema grave");
        StringAssert.Contains(dialogo.MensajePrincipal, "debe cerrarse");
        StringAssert.Contains(dialogo.MensajeAyuda, "registros locales");
    }

    [TestMethod]
    public void Dialogo_PrimerPlanoDelHeaderYAccionPrimariaUsanRolesSemanticos()
    {
        using var dialogo = new FormDiagnosticoFatal(CodigoPrueba, CarpetaPrueba, AbrirCarpetaSinEfecto);

        // El cromo del diálogo fatal debe consumir los ROLES de primer plano
        // (no Surface como texto): en el preset Grafito Surface es oscuro y el
        // rótulo/acción quedarían ilegibles si se volviera a usar como color de
        // texto. Con la paleta por omisión ambos pares coinciden, así que la
        // prueba queda anclada al token y se vuelve discriminante en cualquier
        // otro preset activo.
        var titulo = ObtenerControles<Label>(dialogo)
            .Single(etiqueta => ReferenceEquals(etiqueta.Font, SoproTheme.Fonts.DialogTitle));
        Assert.AreEqual(SoproTheme.Colors.OnHeader, titulo.ForeColor,
            "El título del encabezado debe usar el rol OnHeader.");

        var cerrar = ObtenerBoton(dialogo, "Cerrar aplicación");
        Assert.AreEqual(SoproTheme.Colors.OnBrand, cerrar.ForeColor,
            "La acción primaria debe usar el rol OnBrand.");
        Assert.AreEqual(SoproTheme.Colors.RibbonActive, cerrar.BackColor);
        Assert.AreEqual(SoproTheme.Colors.BrandHover, cerrar.FlatAppearance.MouseOverBackColor,
            "El hover de la acción primaria debe usar el relleno de marca, no un color de texto.");
        Assert.AreEqual(SoproTheme.Colors.RibbonDown, cerrar.FlatAppearance.MouseDownBackColor);
    }

    [TestMethod]
    public void Dialogo_TieneSoloAbrirYCerrarYSinBotonContinuar()
    {
        using var dialogo = new FormDiagnosticoFatal(CodigoPrueba, CarpetaPrueba, AbrirCarpetaSinEfecto);

        var botones = ObtenerBotones(dialogo).ToArray();

        Assert.AreEqual(2, botones.Length, "El diálogo fatal solo debe ofrecer abrir registros y cerrar.");
        Assert.IsTrue(
            botones.Any(boton => boton.Text == "Abrir carpeta de registros"),
            "Falta el botón para abrir la carpeta de registros.");
        var cerrar = botones.Single(boton => boton.Text == "Cerrar aplicación");

        Assert.IsFalse(
            botones.Any(boton => boton.Text.Contains("Continuar", StringComparison.OrdinalIgnoreCase)),
            "El diálogo fatal no debe ofrecer la opción Continuar.");
        Assert.IsFalse(
            botones.Any(boton => boton.Text.Contains("Reintentar", StringComparison.OrdinalIgnoreCase)),
            "El diálogo fatal no debe ofrecer reintentar con estado desconocido.");

        Assert.AreSame(cerrar, dialogo.AcceptButton, "Enter debe cerrar el diálogo.");
        Assert.AreSame(cerrar, dialogo.CancelButton, "Esc debe cerrar el diálogo.");
    }

    [TestMethod]
    public void AbrirCarpeta_DisparaElDelegadoInyectadoConLaCarpeta()
    {
        string? rutaAbierta = null;
        using var dialogo = new FormDiagnosticoFatal(CodigoPrueba, CarpetaPrueba, ruta => rutaAbierta = ruta);

        DispararClic(ObtenerBoton(dialogo, "Abrir carpeta de registros"));

        Assert.AreEqual(CarpetaPrueba, rutaAbierta);
    }

    [TestMethod]
    public void AbrirCarpeta_SinCarpetaNoInvocaElDelegado()
    {
        int llamadas = 0;
        using var dialogo = new FormDiagnosticoFatal(CodigoPrueba, string.Empty, _ => llamadas++);

        DispararClic(ObtenerBoton(dialogo, "Abrir carpeta de registros"));

        Assert.AreEqual(0, llamadas);
    }

    [TestMethod]
    public void Cerrar_CierraElDialogoSinTerminarElProceso()
    {
        var dialogo = new FormDiagnosticoFatal(CodigoPrueba, CarpetaPrueba, AbrirCarpetaSinEfecto);

        try
        {
            DispararClic(ObtenerBoton(dialogo, "Cerrar aplicación"));

            Assert.IsTrue(
                dialogo.IsDisposed,
                "El botón Cerrar debe cerrar el diálogo localmente, sin Application.Exit.");
        }
        finally
        {
            if (!dialogo.IsDisposed)
            {
                dialogo.Dispose();
            }
        }
    }

    private static Button ObtenerBoton(FormDiagnosticoFatal dialogo, string texto)
    {
        return ObtenerBotones(dialogo).Single(boton => boton.Text == texto);
    }

    private static IEnumerable<Button> ObtenerBotones(Control raiz)
    {
        foreach (Control hijo in raiz.Controls)
        {
            if (hijo is Button boton)
            {
                yield return boton;
            }

            foreach (var anidado in ObtenerBotones(hijo))
            {
                yield return anidado;
            }
        }
    }

    private static IEnumerable<T> ObtenerControles<T>(Control raiz) where T : Control
    {
        foreach (Control hijo in raiz.Controls)
        {
            if (hijo is T control)
            {
                yield return control;
            }

            foreach (var anidado in ObtenerControles<T>(hijo))
            {
                yield return anidado;
            }
        }
    }

    private static void DispararClic(Button boton)
    {
        var onClick = typeof(Control).GetMethod(
            "OnClick",
            BindingFlags.NonPublic | BindingFlags.Instance);

        Assert.IsNotNull(onClick, "Control.OnClick no está disponible para disparar el clic.");
        onClick.Invoke(boton, new object[] { EventArgs.Empty });
    }
}
