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
/// Regresión: el constructor de <see cref="FormEditarMaquinaria"/> cargaba los
/// datos del registro existente con <c>CargarDatos()</c> y después los pisaba
/// con costo 0 y modo Manual, de modo que al guardar se perdía el costo
/// calculado. Los registros existentes deben conservar el costo y el flag
/// "calculado" tal como están persistidos.
/// </summary>
[STATestClass]
[DoNotParallelize] // Los forms de WinForms requieren un hilo STA dedicado.
public class FormEditarMaquinariaCostoPreservadoTests
{
    [TestMethod]
    public void ConstructorConRegistroCalculado_ConservaCostoYFlagCalculado()
    {
        var maquinaria = new Maquinaria
        {
            Clave = "MAQ-001",
            Descripcion = "Retroexcavadora de prueba",
            PotenciaNominal = 120m,
            TipoCombustible = TipoCombustible.Diesel,
            CostoHorario = 1234.5678m,
            EsCostoCalculado = true,
            Origen = OrigenInsumo.Maestro,
            Notas = "Notas de prueba",
            FechaCalculoCosto = new DateTime(2026, 1, 2, 3, 4, 0)
        };

        ConFormularioExistente(maquinaria, form =>
        {
            Assert.AreEqual(
                maquinaria.CostoHorario,
                LeerCostoHorario(form).Value,
                "El constructor debe conservar el costo horario cargado del registro existente.");

            Assert.IsTrue(
                LeerCostoCalculado(form).Checked,
                "El constructor debe conservar el modo 'Calculado' del registro existente.");

            Assert.IsFalse(
                LeerCostoManual(form).Checked,
                "El constructor no debe forzar el modo 'Manual' sobre un registro calculado.");

            Assert.IsFalse(
                LeerCostoHorario(form).Enabled,
                "En modo calculado el costo horario no debe quedar editable.");

            Assert.IsTrue(
                LeerBotonCalcular(form).Enabled,
                "En modo calculado el botón 'Calcular' debe quedar disponible.");
        });
    }

    [TestMethod]
    public void ConstructorConRegistroManual_ConservaCostoYModoManual()
    {
        var maquinaria = new Maquinaria
        {
            Clave = "MAQ-002",
            Descripcion = "Compactadora de prueba",
            PotenciaNominal = 45m,
            TipoCombustible = TipoCombustible.Gasolina,
            CostoHorario = 87.6543m,
            EsCostoCalculado = false,
            Origen = OrigenInsumo.Proyecto,
            Notas = string.Empty
        };

        ConFormularioExistente(maquinaria, form =>
        {
            Assert.AreEqual(
                maquinaria.CostoHorario,
                LeerCostoHorario(form).Value,
                "El constructor debe conservar el costo capturado manualmente.");

            Assert.IsTrue(
                LeerCostoManual(form).Checked,
                "El constructor debe conservar el modo 'Manual' del registro existente.");

            Assert.IsFalse(
                LeerCostoCalculado(form).Checked,
                "El constructor no debe marcar el registro manual como calculado.");

            Assert.IsTrue(
                LeerCostoHorario(form).Enabled,
                "En modo manual el costo horario debe quedar editable.");
        });
    }

    // ═════════════════════════════ Infraestructura ═════════════════════════════

    private static void ConFormularioExistente(
        Maquinaria maquinaria,
        Action<FormEditarMaquinaria> verificar)
    {
        var root = Path.Combine(Path.GetTempPath(), $"sopro_maq_costo_{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        var dbPath = Path.Combine(root, "catalogo-temporal.db");

        SOPROContext? contexto = null;
        FormEditarMaquinaria? form = null;
        try
        {
            contexto = new SOPROContext(dbPath);
            // ProyectoId nulo fuerza el ctor sin origen proyecto; el registro ya existe.
            form = new FormEditarMaquinaria(contexto, null, maquinaria);

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

    private static NumericUpDown LeerCostoHorario(FormEditarMaquinaria form)
        => (NumericUpDown)LeerCampo(form, "nudCostoHorario");

    private static RadioButton LeerCostoCalculado(FormEditarMaquinaria form)
        => (RadioButton)LeerCampo(form, "rbCostoCalculado");

    private static RadioButton LeerCostoManual(FormEditarMaquinaria form)
        => (RadioButton)LeerCampo(form, "rbCostoManual");

    private static Button LeerBotonCalcular(FormEditarMaquinaria form)
        => (Button)LeerCampo(form, "btnCalcular");

    private static object LeerCampo(FormEditarMaquinaria form, string nombre)
    {
        var campo = typeof(FormEditarMaquinaria).GetField(
            nombre,
            BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException($"No existe el campo '{nombre}' en el formulario.");

        return campo.GetValue(form)
            ?? throw new InvalidOperationException($"El campo '{nombre}' no está inicializado.");
    }
}
