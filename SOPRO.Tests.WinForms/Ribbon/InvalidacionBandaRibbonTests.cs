using System;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Windows.Forms;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SOPRO.Core.Entities;
using SOPRO.Data.Context;
using SOPRO.WinForms.Forms;
using SOPRO.WinForms.UI.Controls;

namespace SOPRO.Tests.WinForms.Ribbon;

/// <summary>
/// Regresión del remedio del issue #186: tras las tormentas de repintado
/// (apertura de módulo, recálculo global) la banda del ribbon se repinta con
/// sus hijos (<c>InvalidarBandaRibbonDiferido</c>), porque los repintados
/// naturales de cada botón no cubren la primera fila y dejan filas residuo.
///
/// Valida la PROGRAMACIÓN (BeginInvoke → RepintarBandaRibbon) y las guardas
/// (disposed / sin manejador) sin depender del escritorio: el contador vive en
/// un derivado de <see cref="FormProyecto"/> y no observa renders reales.
/// </summary>
[STATestClass]
[DoNotParallelize]
public class InvalidacionBandaRibbonTests
{
    private sealed class FormularioDePrueba : FormProyecto
    {
        public int RepintadosBanda { get; private set; }

        public FormularioDePrueba(SOPROContext contexto, Proyecto proyecto)
            : base(contexto, proyecto) { }

        internal override void RepintarBandaRibbon()
        {
            RepintadosBanda++;
            base.RepintarBandaRibbon();
        }
    }

    [TestMethod]
    public void InvalidacionDiferida_ProgramaElRepintadoDeLaBanda()
    {
        using var escenario = CrearEscenario();
        escenario.Formulario.Show(); // BeginInvoke requiere manejador creado
        escenario.ProcesarMensajes();
        Assert.AreEqual(0, escenario.Formulario.RepintadosBanda,
            "Precondición: sin invalidación programada no debe haber repintados.");

        InvocarInvalidacionBandaRibbonDiferido(escenario.Formulario);
        Assert.AreEqual(0, escenario.Formulario.RepintadosBanda,
            "La invalidación debe quedar PROGRAMADA (BeginInvoke), no ejecutarse en línea.");

        escenario.ProcesarMensajes();
        Assert.AreEqual(1, escenario.Formulario.RepintadosBanda,
            "Tras bombear la cola debe ejecutarse exactamente un repintado de la banda.");
    }

    [TestMethod]
    public void CambioDeModulo_ProgramaElRepintadoDeLaBanda()
    {
        using var escenario = CrearEscenario();
        escenario.Formulario.Show();
        escenario.ProcesarMensajes();

        InvocarCambioSeleccionPestana(escenario.Formulario);
        escenario.ProcesarMensajes();

        Assert.IsTrue(escenario.Formulario.RepintadosBanda >= 1,
            "El cambio de módulo debe dejar programado (y ejecutado) el repintado de la banda.");
    }

    // NOTA (issue #186): el cableado del punto de llamada tras el RECALCULO no
    // se valida aquí: la ejecución real del recálculo en formularios fuera de
    // pantalla se cuelga (flujo asíncrono + contexto de BD), y sí quedó
    // validada en pantalla real con el harness automatizado (corridas con
    // VEREDICTO por botón).

    [TestMethod]
    public void ResiduoEncolado_LuegoDispose_NoRepintaNiLanza()
    {
        var escenario = CrearEscenario();
        escenario.Formulario.Show();
        escenario.ProcesarMensajes();

        // Encolar la invalidación y DISPONER el formulario antes de bombear:
        // el repintado pendiente no debe ejecutarse ni lanzar.
        InvocarInvalidacionBandaRibbonDiferido(escenario.Formulario);
        escenario.Formulario.Dispose();
        escenario.ProcesarMensajes();

        Assert.AreEqual(0, escenario.Formulario.RepintadosBanda,
            "Con el formulario dispuesto antes del bombeo no debe haber repintado.");
    }

    [TestMethod]
    public void InvalidacionDiferida_ToleraFormularioDispueto()
    {
        var escenario = CrearEscenario();
        escenario.Formulario.Show();
        escenario.ProcesarMensajes();
        escenario.Formulario.Dispose();

        // Tras Dispose el método debe salir sin excepción (guardas de estado).
        InvocarInvalidacionBandaRibbonDiferido(escenario.Formulario);
        escenario.ProcesarMensajes();
        Assert.AreEqual(0, escenario.Formulario.RepintadosBanda,
            "Un formulario dispuesto no debe repintar la banda.");
    }

    [TestMethod]
    public void InvalidacionDiferida_ToleraFormularioSinManejador()
    {
        using var escenario = CrearEscenario();

        // Sin Show(): sin handle creado; el método debe salir sin excepción
        // (BeginInvoke fallaría sin manejador y la guarda lo evita).
        InvocarInvalidacionBandaRibbonDiferido(escenario.Formulario);
        escenario.ProcesarMensajes();
        Assert.AreEqual(0, escenario.Formulario.RepintadosBanda,
            "Sin manejador no debe haber repintado programado.");
    }

    // ═══════════════════════ Infraestructura ═══════════════════════

    private static MethodInfo ResolverMetodo(Type tipo, string nombre)
    {
        // Los métodos privados viven en los partials de FormProyecto: hay que
        // recorrer la jerarquía (GetMethod no los ve desde tipos derivados).
        for (Type? t = tipo; t != null; t = t.BaseType)
        {
            var metodo = t.GetMethod(
                nombre,
                BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
            if (metodo != null) return metodo;
        }

        throw new InvalidOperationException($"No se resolvió {nombre} en {tipo.Name}.");
    }

    private static void InvocarInvalidacionBandaRibbonDiferido(FormProyecto form)
        => ResolverMetodo(form.GetType(), "InvalidarBandaRibbonDiferido").Invoke(form, Array.Empty<object>());

    private static void InvocarCambioSeleccionPestana(FormProyecto form)
        => ResolverMetodo(form.GetType(), "TabControl_SelectedIndexChanged")
            .Invoke(form, new object[] { form, EventArgs.Empty });

    private sealed class Escenario : IDisposable
    {
        public FormularioDePrueba Formulario { get; }

        public Escenario(FormularioDePrueba formulario)
        {
            Formulario = formulario;
        }

        public void ProcesarMensajes()
        {
            for (int i = 0; i < 5; i++)
                System.Windows.Forms.Application.DoEvents();
        }

        public void Dispose()
        {
            Formulario.Dispose();
        }
    }

    private static Escenario CrearEscenario()
    {
        string raiz = Path.Combine(Path.GetTempPath(), $"sopro_ribbon_invalidacion_{Guid.NewGuid():N}");
        Directory.CreateDirectory(raiz);
        string dbPath = Path.Combine(raiz, "proyecto-temporal.db");

        var contexto = new SOPROContext(dbPath);
        contexto.Database.EnsureCreated();

        var form = new FormularioDePrueba(contexto, new Proyecto
        {
            Id = 1,
            Nombre = "Proyecto de prueba de invalidación del ribbon",
            Descripcion = string.Empty,
            Ubicacion = string.Empty,
            Convocante = string.Empty,
            Contratista = string.Empty,
            ApoderadoLegal = string.Empty,
        })
        {
            // Fuera de pantalla: el Load aplica el tema y crea el ribbon; las
            // pruebas validan PROGRAMACIÓN, no renders (no dependen del monitor).
            WindowState = FormWindowState.Normal,
            StartPosition = FormStartPosition.Manual,
            Location = new Point(-4000, -4000),
            ClientSize = new Size(1600, 796),
            ShowInTaskbar = false,
        };

        return new Escenario(form);
    }
}
