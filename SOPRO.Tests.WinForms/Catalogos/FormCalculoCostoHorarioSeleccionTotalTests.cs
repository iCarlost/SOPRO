using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SOPRO.Core.Entities;
using SOPRO.Data.Context;
using SOPRO.WinForms.Forms;
using SOPRO.WinForms.Helpers;

namespace SOPRO.Tests.WinForms.Catalogos;

/// <summary>
/// Captura del cálculo de costo horario: los 19 NumericUpDown editables de las
/// pestañas Cargos Fijos, Consumos y Operación seleccionan todo su contenido al
/// recibir el foco mediante <see cref="SeleccionTotalNumericUpDown"/>. El panel
/// de Resumen es sólo de lectura y no participa en la captura.
/// </summary>
[STATestClass]
[DoNotParallelize] // Los forms de WinForms requieren un hilo STA dedicado.
public class FormCalculoCostoHorarioSeleccionTotalTests
{
    private static readonly string[] CamposCargosFijos =
    {
        "nudValorAdquisicion",
        "nudValorLlantas",
        "nudValorPiezasEsp",
        "nudFactorRescate",
        "nudVidaEconomica",
        "nudTasaInteres",
        "nudHorasAnio",
        "nudPrimaSeguro",
        "nudFactorManten",
    };

    private static readonly string[] CamposConsumos =
    {
        "nudCantCombustible",
        "nudPrecioCombustible",
        "nudCantAceite",
        "nudPrecioAceite",
        "nudNumLlantas",
        "nudVidaLlantas",
        "nudVidaPiezasEsp",
    };

    private static readonly string[] CamposOperacion =
    {
        "nudSalarioOperador",
        "nudFSR",
        "nudHorasTurno",
    };

    private static string[] TodosLosCamposDeCaptura =>
        CamposCargosFijos.Concat(CamposConsumos).Concat(CamposOperacion).ToArray();

    [TestMethod]
    public void Constructor_ConectaSeleccionTotalSoloALosDiecinueveControlesDeCaptura()
    {
        ConCalculadora(form =>
        {
            Assert.AreEqual(
                19,
                TodosLosCamposDeCaptura.Length,
                "El contrato de captura debe cubrir exactamente 19 NumericUpDown.");

            foreach (string campo in TodosLosCamposDeCaptura)
            {
                NumericUpDown control = LeerCampo<NumericUpDown>(form, campo);
                Assert.IsTrue(
                    TieneHelperDeSeleccionConfigurado(control),
                    $"El constructor debe conectar '{campo}' a SeleccionTotalNumericUpDown.");
            }

            Assert.AreEqual(
                19,
                ObtenerTodosLosNumericUpDown(form).Count,
                "El formulario no debe exponer controles NumericUpDown fuera de los 19 de captura.");
        });
    }

    [TestMethod]
    public void Resumen_PermaneceComoSoloLecturaSinControlesDeCaptura()
    {
        ConCalculadora(form =>
        {
            TabPage resumen = LeerCampo<TabPage>(form, "tabResumen");

            Assert.AreEqual(
                0,
                ObtenerTodosLosNumericUpDown(resumen).Count,
                "La pestaña Resumen no debe contener NumericUpDown de captura.");

            Assert.IsInstanceOfType(
                LeerCampo<Label>(form, "lblResTotalVal"),
                typeof(Label),
                "El total del resumen debe seguir siendo una etiqueta de sólo lectura.");
        });
    }

    [TestMethod]
    public void ControlesDeCaptura_SeleccionanTodoAlRecibirElFocoPorPestana()
    {
        ConCalculadora(form =>
        {
            form.Show();
            form.Activate();
            ProcesarMensajes();

            TabControl pestanas = LeerCampo<TabControl>(form, "tabControl");
            Button neutral = LeerCampo<Button>(form, "btnCancelar");

            VerificarSeleccionTotal(form, pestanas, neutral, "tabCargosFijos", CamposCargosFijos);
            VerificarSeleccionTotal(form, pestanas, neutral, "tabConsumos", CamposConsumos);
            VerificarSeleccionTotal(form, pestanas, neutral, "tabOperacion", CamposOperacion);
        });
    }

    private static void VerificarSeleccionTotal(
        FormCalculoCostoHorario form,
        TabControl pestanas,
        Button neutral,
        string campoPestana,
        IReadOnlyList<string> campos)
    {
        pestanas.SelectedTab = LeerCampo<TabPage>(form, campoPestana);
        ProcesarMensajes();

        foreach (string campo in campos)
        {
            NumericUpDown control = LeerCampo<NumericUpDown>(form, campo);
            TextBox edicion = ObtenerEdicion(control);

            Assert.IsFalse(
                string.IsNullOrEmpty(edicion.Text),
                $"'{campo}' debe mostrar un valor antes de probar la selección.");

            neutral.Focus();
            ProcesarMensajes();

            Assert.IsTrue(control.Focus(), $"No se pudo enfocar '{campo}'.");
            ProcesarMensajes();

            Assert.IsTrue(control.ContainsFocus, $"'{campo}' debe conservar el foco.");
            Assert.AreEqual(0, edicion.SelectionStart, $"'{campo}' debe seleccionar desde el inicio.");
            Assert.AreEqual(
                edicion.Text.Length,
                edicion.SelectionLength,
                $"'{campo}' debe seleccionar todo el texto mostrado.");
        }
    }

    private static List<NumericUpDown> ObtenerTodosLosNumericUpDown(Control raiz)
    {
        List<NumericUpDown> resultado = new List<NumericUpDown>();
        foreach (Control hijo in raiz.Controls)
        {
            if (hijo is NumericUpDown numeric)
                resultado.Add(numeric);
            resultado.AddRange(ObtenerTodosLosNumericUpDown(hijo));
        }

        return resultado;
    }

    private static TextBox ObtenerEdicion(NumericUpDown control)
    {
        TextBox? edicion = control.Controls.OfType<TextBox>().FirstOrDefault();
        return edicion ?? throw new InvalidOperationException($"El control '{control.Name}' no expone su TextBox interno.");
    }

    private static bool TieneHelperDeSeleccionConfigurado(NumericUpDown control)
    {
        FieldInfo? campoEstados = typeof(SeleccionTotalNumericUpDown).GetField(
            "_estados",
            BindingFlags.Static | BindingFlags.NonPublic);
        object? estados = campoEstados?.GetValue(null);
        if (estados == null)
            return false;

        MethodInfo? intentar = estados.GetType().GetMethod("TryGetValue");
        object?[] argumentos = { ObtenerEdicion(control), null };
        return intentar != null && (bool)intentar.Invoke(estados, argumentos)!;
    }

    private static void ProcesarMensajes()
    {
        for (int i = 0; i < 5; i++)
            System.Windows.Forms.Application.DoEvents();
    }

    private static T LeerCampo<T>(Form form, string nombre) where T : class
    {
        FieldInfo? campo = form.GetType().GetField(
            nombre,
            BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);

        if (campo == null)
            throw new InvalidOperationException($"No existe el campo '{nombre}' en {form.GetType().Name}.");

        return campo.GetValue(form) as T
            ?? throw new InvalidOperationException($"El campo '{nombre}' de {form.GetType().Name} no está inicializado.");
    }

    private static void ConCalculadora(Action<FormCalculoCostoHorario> verificar)
    {
        string root = Path.Combine(Path.GetTempPath(), $"sopro_calc_costo_{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        string dbPath = Path.Combine(root, "catalogo-temporal.db");

        SOPROContext? contexto = null;
        FormCalculoCostoHorario? form = null;
        try
        {
            contexto = new SOPROContext(dbPath);
            form = new FormCalculoCostoHorario(contexto, CrearMaquinaria());
            verificar(form);
        }
        finally
        {
            form?.Dispose();
            contexto?.Dispose();

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

    private static Maquinaria CrearMaquinaria() => new()
    {
        Clave = "MAQ-0097",
        Descripcion = "Maquinaria de prueba para captura de costo horario",
        PotenciaNominal = 150m,
        TipoCombustible = TipoCombustible.Diesel,
        ValorAdquisicion = 1_500_000m,
        ValorLlantas = 120_000m,
        ValorPiezasEspeciales = 45_000m,
        FactorRescate = 0.10m,
        VidaEconomica = 10_000m,
        TasaInteres = 21.24m,
        HorasEfectivasAnio = 1_600m,
        PrimaSeguro = 3m,
        FactorMantenimiento = 0.20m,
        CantidadCombustible = 18.5m,
        PrecioCombustible = 24.5m,
        CantidadAceite = 0.5m,
        PrecioAceite = 80m,
        NumeroLlantas = 4,
        VidaEconomicaLlantas = 5_000m,
        VidaPiezasEspeciales = 5_000m,
        SalarioOperador = 450m,
        FactorSalarioReal = 1.6543m,
        HorasEfectivasTurno = 8m,
        CostoHorario = 0m,
        EsCostoCalculado = false,
        Origen = OrigenInsumo.Proyecto,
        Notas = string.Empty,
    };
}
