using System;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Windows.Forms;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SOPRO.Core.Entities;
using SOPRO.Data.Context;
using SOPRO.WinForms.Forms;

namespace SOPRO.Tests.WinForms.Ribbon;

/// <summary>
/// Navegación lateral y sincronización del contexto del ribbon:
/// - un módulo nuevo sincroniza UNA vez, después de Show/layout/foco;
/// - una pestaña existente distinta sincroniza con la selección natural;
/// - un clic sobre la hoja ya activa no reejecuta la rutina ni repinta.
///
/// Las pruebas cuentan invocaciones mediante los seams internos y no observan
/// píxeles ni renders reales; el host se muestra fuera de pantalla.
/// </summary>
[STATestClass]
[DoNotParallelize]
public class SincronizacionContextoRibbonTests
{
    private const BindingFlags PrivadoDeInstancia =
        BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

    private sealed class FormularioDePrueba : FormProyecto
    {
        public int Sincronizaciones { get; private set; }

        public int RepintadosBanda { get; private set; }

        public FormularioDePrueba(SOPROContext contexto, Proyecto proyecto)
            : base(contexto, proyecto) { }

        internal override void SincronizarContextoRibbonActivo()
        {
            Sincronizaciones++;
            base.SincronizarContextoRibbonActivo();
        }

        internal override void RepintarBandaRibbon()
        {
            RepintadosBanda++;
            base.RepintarBandaRibbon();
        }

        public void ReiniciarContadores()
        {
            Sincronizaciones = 0;
            RepintadosBanda = 0;
        }
    }

    private sealed class ModuloEspia : Form
    {
        public int Layouts { get; private set; }

        /// <summary>
        /// Valor del contador de sincronizaciones la última vez que el módulo
        /// se dispuso. Permite comprobar que la sincronización final ocurre
        /// después de los layouts de la apertura.
        /// </summary>
        public int SincronizacionesEnLayout { get; private set; } = -1;

        [System.ComponentModel.DesignerSerializationVisibility(
            System.ComponentModel.DesignerSerializationVisibility.Hidden)]
        public Func<int>? ContadorSincronizaciones { get; set; }

        protected override void OnLayout(LayoutEventArgs levent)
        {
            base.OnLayout(levent);
            Layouts++;
            if (ContadorSincronizaciones != null)
                SincronizacionesEnLayout = ContadorSincronizaciones();
        }
    }

    [TestMethod]
    public void ModuloNuevo_SincronizaYRepintaUnaSolaVezDespuesDelLayout()
    {
        using Escenario escenario = CrearEscenario();
        escenario.Formulario.Show();
        escenario.ProcesarMensajes();

        // Un módulo previo garantiza que el alta concrete la selección explícita
        // de la pestaña (que es la que dispara SelectedIndexChanged antes de Show).
        InvocarAbrir(escenario.Formulario, "Módulo previo", new ModuloEspia(), "nodePrevio");
        escenario.ProcesarMensajes();
        escenario.Formulario.ReiniciarContadores();

        var modulo = new ModuloEspia
        {
            ContadorSincronizaciones = () => escenario.Formulario.Sincronizaciones
        };

        InvocarAbrir(escenario.Formulario, "Módulo de prueba", modulo, "nodeModuloPrueba");

        Assert.AreEqual(0, escenario.Formulario.Sincronizaciones,
            "Abrir no debe sincronizar en línea: la pestaña todavía no tiene Show/layout/foco finales.");
        Assert.AreEqual(0, escenario.Formulario.RepintadosBanda,
            "Tampoco debe programarse un repintado antes del cierre de la apertura.");

        escenario.ProcesarMensajes();

        Assert.AreEqual(1, escenario.Formulario.Sincronizaciones,
            "El alta de módulo debe sincronizar el contexto exactamente una vez.");
        Assert.AreEqual(1, escenario.Formulario.RepintadosBanda,
            "El alta debe dejar una única invalidación/repintado de la banda.");
        Assert.IsTrue(modulo.Layouts > 0, "La apertura debe haber ejecutado layouts del módulo.");
        Assert.AreEqual(0, modulo.SincronizacionesEnLayout,
            "La sincronización final debe ocurrir después de los layouts de la apertura.");
    }

    [TestMethod]
    public void PestanaExistenteDistinta_SincronizaUnaSolaVezConLaSeleccionNatural()
    {
        using Escenario escenario = CrearEscenario();
        escenario.Formulario.Show();
        escenario.ProcesarMensajes();

        InvocarAbrir(escenario.Formulario, "Módulo uno", new ModuloEspia(), "nodeUno");
        escenario.ProcesarMensajes();
        InvocarAbrir(escenario.Formulario, "Módulo dos", new ModuloEspia(), "nodeDos");
        escenario.ProcesarMensajes();
        escenario.Formulario.ReiniciarContadores();

        InvocarClickNodo(escenario.Formulario, "nodeUno");

        Assert.AreEqual(1, escenario.Formulario.Sincronizaciones,
            "Cambiar a una pestaña existente distinta debe sincronizar una sola vez (evento natural).");

        escenario.ProcesarMensajes();

        Assert.AreEqual(1, escenario.Formulario.Sincronizaciones,
            "La transferencia de foco diferida no debe volver a sincronizar.");
        Assert.AreEqual(1, escenario.Formulario.RepintadosBanda,
            "El cambio de módulo existente debe dejar una sola invalidación de la banda.");
    }

    [TestMethod]
    public void MismaPestana_SoloDevuelveFocoSinResincronizarNiRepintar()
    {
        using Escenario escenario = CrearEscenario();
        escenario.Formulario.Show();
        escenario.ProcesarMensajes();

        InvocarAbrir(escenario.Formulario, "Módulo uno", new ModuloEspia(), "nodeUno");
        escenario.ProcesarMensajes();
        escenario.Formulario.ReiniciarContadores();

        InvocarClickNodo(escenario.Formulario, "nodeUno");
        escenario.ProcesarMensajes();

        Assert.AreEqual(0, escenario.Formulario.Sincronizaciones,
            "Un clic sobre la hoja ya activa no debe ejecutar la sincronización del ribbon.");
        Assert.AreEqual(0, escenario.Formulario.RepintadosBanda,
            "Un clic sobre la hoja ya activa no debe invalidar ni repintar la banda.");
    }

    [TestMethod]
    public void EventoDePestana_MantieneUnaSolaSincronizacion()
    {
        using Escenario escenario = CrearEscenario();
        escenario.Formulario.Show();
        escenario.ProcesarMensajes();
        escenario.Formulario.ReiniciarContadores();

        InvocarCambioSeleccionPestana(escenario.Formulario);
        escenario.ProcesarMensajes();

        Assert.AreEqual(1, escenario.Formulario.Sincronizaciones,
            "El evento de pestaña debe seguir sincronizando el contexto (una vez).");
        Assert.AreEqual(1, escenario.Formulario.RepintadosBanda,
            "El evento de pestaña debe dejar una sola invalidación de la banda.");
    }

    // ═══════════════════════ Infraestructura ═══════════════════════

    private static void InvocarAbrir(FormProyecto formulario, string titulo, Form modulo, string nodeName)
        => ResolverMetodo(
                formulario.GetType(),
                "Abrir",
                typeof(string),
                typeof(Form),
                typeof(string))
            .Invoke(formulario, new object[] { titulo, modulo, nodeName });

    private static void InvocarClickNodo(FormProyecto formulario, string nodeName)
    {
        var padre = new TreeNode("Módulos");
        var nodo = new TreeNode("Nodo de prueba") { Name = nodeName };
        padre.Nodes.Add(nodo);

        var argumentos = new TreeNodeMouseClickEventArgs(nodo, MouseButtons.Left, 1, 0, 0);
        ResolverMetodo(
                formulario.GetType(),
                "treeMenu_NodeMouseClick",
                typeof(object),
                typeof(TreeNodeMouseClickEventArgs))
            .Invoke(formulario, new object[] { formulario, argumentos });
    }

    private static void InvocarCambioSeleccionPestana(FormProyecto formulario)
        => ResolverMetodo(formulario.GetType(), "TabControl_SelectedIndexChanged", typeof(object), typeof(EventArgs))
            .Invoke(formulario, new object[] { formulario, EventArgs.Empty });

    private static MethodInfo ResolverMetodo(Type tipo, string nombre, params Type[] parametros)
    {
        // Los métodos privados viven en los partials de FormProyecto: hay que
        // recorrer la jerarquía (GetMethod no los ve desde tipos derivados).
        for (Type? t = tipo; t != null; t = t.BaseType)
        {
            var metodo = t.GetMethod(nombre, PrivadoDeInstancia, null, parametros, null);
            if (metodo != null) return metodo;
        }

        throw new InvalidOperationException($"No se resolvió {nombre} en {tipo.Name}.");
    }

    private sealed class Escenario : IDisposable
    {
        public FormularioDePrueba Formulario { get; }

        public Escenario(FormularioDePrueba formulario)
        {
            Formulario = formulario;
        }

        public void ProcesarMensajes()
        {
            for (int i = 0; i < 8; i++)
                System.Windows.Forms.Application.DoEvents();
        }

        public void Dispose()
        {
            Formulario.Dispose();
        }
    }

    private static Escenario CrearEscenario()
    {
        string raiz = Path.Combine(Path.GetTempPath(), $"sopro_ribbon_sincronizacion_{Guid.NewGuid():N}");
        Directory.CreateDirectory(raiz);
        string dbPath = Path.Combine(raiz, "proyecto-temporal.db");

        var contexto = new SOPROContext(dbPath);
        contexto.Database.EnsureCreated();

        var form = new FormularioDePrueba(contexto, new Proyecto
        {
            Id = 1,
            Nombre = "Proyecto de prueba de sincronización del ribbon",
            Descripcion = string.Empty,
            Ubicacion = string.Empty,
            Convocante = string.Empty,
            Contratista = string.Empty,
            ApoderadoLegal = string.Empty,
        })
        {
            // Fuera de pantalla: se valida el conteo/orden de invocaciones, no
            // renders reales (no depende del monitor).
            WindowState = FormWindowState.Normal,
            StartPosition = FormStartPosition.Manual,
            Location = new Point(-4000, -4000),
            ClientSize = new Size(1600, 796),
            ShowInTaskbar = false,
        };

        return new Escenario(form);
    }
}
