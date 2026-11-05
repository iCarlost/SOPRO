using System;
using System.IO;
using System.Reflection;
using System.Windows.Forms;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SOPRO.Core.Entities;
using SOPRO.Data.Context;
using SOPRO.WinForms.Forms;

namespace SOPRO.Tests.WinForms.Catalogos;

/// <summary>
/// Regresión: en las herramientas con unidad <c>%MO</c> el
/// <see cref="Herramienta.PrecioUnitario"/> es un porcentaje capturado tal cual
/// (3.00 = 3%). El editor lo trataba como fracción (0.03 = 3%), con máximo 1,
/// de modo que un registro persistido con 14.11 no se podía cargar ni editar.
/// </summary>
[STATestClass]
[DoNotParallelize] // Los forms de WinForms requieren un hilo STA dedicado.
public class FormEditarHerramientaPorcentajeMOTests
{
    [TestMethod]
    public void ConstructorConPorcentajeMO_MuestraPorcentajeDirectoConRangoOrdinario()
    {
        var herramienta = CrearPorcentajeMO("HER-001", 14.11m);

        ConFormularioExistente(herramienta, form =>
        {
            Assert.AreEqual(
                "%MO",
                LeerUnidad(form).Text,
                "El formulario debe conservar la unidad %MO del registro.");

            Assert.AreEqual(
                "Porcentaje (%):",
                LeerEtiquetaPrecio(form).Text,
                "La etiqueta debe indicar que el valor es un porcentaje.");

            Assert.AreEqual(
                14.11m,
                LeerPrecio(form).Value,
                "El porcentaje persistido se muestra tal cual (14.11 = 14.11%), sin convertirlo a fracción.");

            Assert.AreEqual(
                0m,
                LeerPrecio(form).Minimum,
                "El porcentaje debe iniciar en 0.");

            Assert.AreEqual(
                100m,
                LeerPrecio(form).Maximum,
                "El rango ordinario de captura del porcentaje debe ser 0..100.");

            Assert.AreEqual(
                0.01m,
                LeerPrecio(form).Increment,
                "El incremento de captura debe ser 0.01.");

            Assert.AreEqual(
                4,
                LeerPrecio(form).DecimalPlaces,
                "La captura del porcentaje debe permitir 4 decimales.");

            StringAssert.Contains(
                LeerHint(form).Text,
                "3.00 para 3%",
                "El texto de ayuda debe indicar que 3.00 equivale a 3%.");
        });
    }

    [TestMethod]
    public void ConstructorConPorcentajeMOHeredadoMayorACien_AmpliaMaximoYConservaValor()
    {
        var herramienta = CrearPorcentajeMO("HER-002", 140.5m);

        ConFormularioExistente(herramienta, form =>
        {
            Assert.AreEqual(
                140.5m,
                LeerPrecio(form).Value,
                "Un porcentaje heredado mayor a 100 debe conservarse sin recorte ni normalización.");

            Assert.IsTrue(
                LeerPrecio(form).Maximum >= 140.5m,
                "El máximo debe ampliarse para permitir mostrar el valor heredado.");
        });
    }

    [TestMethod]
    public void ConstructorConHerramientaNormal_MantieneRangoDePrecioUnitario()
    {
        var herramienta = new Herramienta
        {
            Clave = "HER-003",
            Descripcion = "Herramienta de prueba",
            Unidad = "pza",
            PrecioUnitario = 1234.56m
        };

        ConFormularioExistente(herramienta, form =>
        {
            Assert.AreEqual(
                "Precio Unitario:",
                LeerEtiquetaPrecio(form).Text,
                "Las herramientas normales conservan la etiqueta de precio unitario.");

            Assert.AreEqual(
                1234.56m,
                LeerPrecio(form).Value,
                "El precio unitario de una herramienta normal no debe alterarse.");

            Assert.AreEqual(
                999999m,
                LeerPrecio(form).Maximum,
                "Las herramientas normales conservan su rango de precio actual.");

            Assert.AreEqual(
                2,
                LeerPrecio(form).DecimalPlaces,
                "Las herramientas normales conservan 2 decimales.");
        });
    }

    // ═════════════════════════════ Infraestructura ═════════════════════════════

    private static Herramienta CrearPorcentajeMO(string clave, decimal porcentaje)
        => new Herramienta
        {
            Clave = clave,
            Descripcion = "Herramienta menor de prueba",
            Unidad = "%MO",
            PrecioUnitario = porcentaje
        };

    private static void ConFormularioExistente(
        Herramienta herramienta,
        Action<FormEditarHerramienta> verificar)
    {
        var root = Path.Combine(Path.GetTempPath(), $"sopro_herr_pct_{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        var dbPath = Path.Combine(root, "catalogo-temporal.db");

        SOPROContext? contexto = null;
        FormEditarHerramienta? form = null;
        try
        {
            contexto = new SOPROContext(dbPath);
            form = new FormEditarHerramienta(contexto, 0, herramienta);

            verificar(form);
        }
        finally
        {
            form?.Dispose();
            contexto?.Dispose();

            if (Directory.Exists(root))
                Directory.Delete(root, true);
        }
    }

    private static ComboBox LeerUnidad(FormEditarHerramienta form)
        => (ComboBox)LeerCampo(form, "cboUnidad");

    private static NumericUpDown LeerPrecio(FormEditarHerramienta form)
        => (NumericUpDown)LeerCampo(form, "nudPrecio");

    private static Label LeerEtiquetaPrecio(FormEditarHerramienta form)
        => (Label)LeerCampo(form, "lblPrecio");

    private static Label LeerHint(FormEditarHerramienta form)
        => (Label)LeerCampo(form, "lblHint");

    private static object LeerCampo(FormEditarHerramienta form, string nombre)
    {
        var campo = typeof(FormEditarHerramienta).GetField(
            nombre,
            BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException($"No existe el campo '{nombre}' en el formulario.");

        return campo.GetValue(form)
            ?? throw new InvalidOperationException($"El campo '{nombre}' no está inicializado.");
    }
}
