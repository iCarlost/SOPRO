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
/// Regresión: en las herramientas con unidad <c>%MO</c> el porcentaje real vive
/// en la cantidad de cada matriz, así que el editor no captura precio ni
/// porcentaje. El valor persistido se conserva tal cual y un registro heredado
/// (por ejemplo 14.11) se abre sin excepción ni recorte.
/// </summary>
[STATestClass]
[DoNotParallelize] // Los forms de WinForms requieren un hilo STA dedicado.
public class FormEditarHerramientaPorcentajeMOTests
{
    [TestMethod]
    public void ConstructorConPorcentajeMO_OcultaLaCapturaDePrecioYConservaElValorHeredado()
    {
        var herramienta = CrearPorcentajeMO("HER-001", 14.11m);

        ConFormularioExistente(herramienta, form =>
        {
            Assert.AreEqual(
                "%MO",
                LeerUnidad(form).Text,
                "El formulario debe conservar la unidad %MO del registro.");

            Assert.IsFalse(
                LeerEtiquetaPrecio(form).Visible,
                "La etiqueta de precio no aplica a una herramienta %MO.");

            Assert.IsFalse(
                LeerPrecio(form).Visible,
                "El control de precio no debe mostrarse para una herramienta %MO.");

            Assert.IsFalse(
                LeerHint(form).Visible,
                "La ayuda de captura no aplica cuando no se captura porcentaje.");

            Assert.AreEqual(
                14.11m,
                LeerPrecio(form).Value,
                "El valor heredado debe cargarse sin recorte aunque el control esté oculto.");

            Assert.AreEqual(
                100m,
                LeerPrecio(form).Maximum,
                "El rango de carga debe seguir siendo tolerante (0..100).");
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
    public void GuardarPorcentajeMO_ConservaElValorPersistidoSinPisarlo()
    {
        var herramienta = CrearPorcentajeMO("HER-003", 14.11m);

        ConFormularioExistente(herramienta, form =>
        {
            InvocarGuardado(form);

            Assert.AreEqual(
                "%MO",
                herramienta.Unidad,
                "La unidad debe mantenerse en %MO.");
            Assert.AreEqual(
                14.11m,
                herramienta.PrecioUnitario,
                "Guardar una herramienta %MO no debe pisar el valor persistido con el control oculto.");
        });
    }

    [TestMethod]
    public void CambiarUnidadAMonetaria_VuelveAMostrarLaCapturaDePrecio()
    {
        var herramienta = CrearPorcentajeMO("HER-004", 14.11m);

        ConFormularioExistente(herramienta, form =>
        {
            LeerUnidad(form).SelectedItem = "pza";

            Assert.IsTrue(
                LeerPrecio(form).Visible,
                "Al cambiar a una unidad monetaria el precio vuelve a capturarse.");
            Assert.IsTrue(
                LeerEtiquetaPrecio(form).Visible,
                "La etiqueta de precio unitario debe reaparecer.");
            Assert.AreEqual(
                "Precio Unitario:",
                LeerEtiquetaPrecio(form).Text,
                "La etiqueta debe volver al texto de precio unitario.");
        });
    }

    [TestMethod]
    public void ConstructorConHerramientaNormal_MantieneRangoYCapturaDePrecio()
    {
        var herramienta = new Herramienta
        {
            Clave = "HER-005",
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

            Assert.IsTrue(
                LeerPrecio(form).Visible,
                "Las herramientas normales conservan la captura de precio.");

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

    private static void InvocarGuardado(FormEditarHerramienta form)
    {
        var metodo = typeof(FormEditarHerramienta).GetMethod(
            "btnGuardar_Click",
            BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("No existe 'btnGuardar_Click' en el formulario.");

        metodo.Invoke(form, new object?[] { null, EventArgs.Empty });
    }

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
            contexto.Database.EnsureCreated();
            form = new FormEditarHerramienta(contexto, 0, herramienta);
            // Control.Visible solo refleja el estado real con el formulario mostrado.
            form.Show();

            verificar(form);
        }
        finally
        {
            if (form != null)
            {
                form.Close();
                form.Dispose();
            }
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
