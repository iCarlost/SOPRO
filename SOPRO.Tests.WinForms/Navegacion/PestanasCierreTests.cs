using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SOPRO.Core.Entities;
using SOPRO.Data.Context;
using SOPRO.WinForms.Forms;

namespace SOPRO.Tests.WinForms.Navegacion;

[STATestClass]
[DoNotParallelize] // Crea y muestra formularios WinForms reales.
public class PestanasCierreTests
{
    private const BindingFlags PrivadoDeInstancia = BindingFlags.Instance | BindingFlags.NonPublic;

    [TestMethod]
    public void CerrarPestanaIndividual_DescartaSoloLaPulsada_YConservaPestanasVecinas()
    {
        using HostPrueba host = new HostPrueba();
        FormCierreEspia formUno = host.Abrir("Módulo uno", "nodeUno");
        FormCierreEspia formDos = host.Abrir("Módulo dos", "nodeDos");
        FormCierreEspia formTres = host.Abrir("Módulo tres", "nodeTres");

        TabPage[] iniciales = host.Tabs;
        Assert.AreEqual(3, iniciales.Length);

        host.CerrarPestana(iniciales[1]);

        Assert.IsTrue(formDos.IsDisposed, "El formulario pulsado debe quedar descartado.");
        Assert.IsFalse(formUno.IsDisposed, "El formulario vecino anterior no debe descartarse.");
        Assert.IsFalse(formTres.IsDisposed, "El formulario vecino posterior no debe descartarse.");
        Assert.AreEqual(1, formDos.IntentosCierre, "El cierre individual debe intentarse una sola vez.");
        Assert.IsFalse(host.Contiene(iniciales[1]), "La pestaña pulsada debe salir del host.");

        TabPage[] restantes = host.Tabs;
        Assert.AreEqual(2, restantes.Length);
        Assert.AreSame(iniciales[0], restantes[0], "La pestaña vecina anterior debe conservar su instancia.");
        Assert.AreSame(iniciales[2], restantes[1], "La pestaña vecina posterior debe conservar su instancia.");
    }

    [TestMethod]
    public void CerrarPestanaIndividual_UltimaPestana_DescartaSuFormularioYConservaLasAnteriores()
    {
        using HostPrueba host = new HostPrueba();
        FormCierreEspia formUno = host.Abrir("Módulo uno", "nodeUno");
        FormCierreEspia formDos = host.Abrir("Módulo dos", "nodeDos");
        FormCierreEspia formTres = host.Abrir("Módulo tres", "nodeTres");

        TabPage[] iniciales = host.Tabs;
        host.CerrarPestana(iniciales[2]);

        Assert.IsTrue(formTres.IsDisposed, "La última pestaña también debe cerrarse.");
        Assert.IsFalse(formUno.IsDisposed);
        Assert.IsFalse(formDos.IsDisposed);

        TabPage[] restantes = host.Tabs;
        Assert.AreEqual(2, restantes.Length);
        Assert.AreSame(iniciales[0], restantes[0]);
        Assert.AreSame(iniciales[1], restantes[1]);
    }

    [TestMethod]
    public void CerrarOtrasPestanas_ConservaExactamenteLaPulsada()
    {
        using HostPrueba host = new HostPrueba();
        FormCierreEspia formUno = host.Abrir("Módulo uno", "nodeUno");
        FormCierreEspia formDos = host.Abrir("Módulo dos", "nodeDos");
        FormCierreEspia formTres = host.Abrir("Módulo tres", "nodeTres");

        TabPage[] iniciales = host.Tabs;
        host.CerrarOtrasPestanas(iniciales[1]);

        Assert.IsFalse(formDos.IsDisposed, "La pestaña pulsada no debe descartarse.");
        Assert.IsTrue(formUno.IsDisposed, "Las demás pestañas deben descartarse.");
        Assert.IsTrue(formTres.IsDisposed, "Las demás pestañas deben descartarse.");

        TabPage[] restantes = host.Tabs;
        Assert.AreEqual(1, restantes.Length);
        Assert.AreSame(iniciales[1], restantes[0], "Debe quedar exactamente la instancia pulsada.");
    }

    [TestMethod]
    public void CerrarOtrasPestanas_ConPestanaYaCerrada_NoCierraLasRestantes()
    {
        using HostPrueba host = new HostPrueba();
        FormCierreEspia formUno = host.Abrir("Módulo uno", "nodeUno");
        FormCierreEspia formDos = host.Abrir("Módulo dos", "nodeDos");
        FormCierreEspia formTres = host.Abrir("Módulo tres", "nodeTres");

        TabPage[] iniciales = host.Tabs;
        host.CerrarPestana(iniciales[1]);
        host.CerrarOtrasPestanas(iniciales[1]);

        Assert.AreEqual(2, host.Tabs.Length, "Una pestaña obsoleta no debe convertir 'Cerrar otras' en 'Cerrar todas'.");
        Assert.IsFalse(formUno.IsDisposed);
        Assert.IsFalse(formTres.IsDisposed);
        Assert.IsTrue(formDos.IsDisposed);
    }

    [TestMethod]
    public void CerrarTodasLasPestanas_DescartaTodosLosFormularios_YDejaCeroPestanas()
    {
        using HostPrueba host = new HostPrueba();
        FormCierreEspia formUno = host.Abrir("Módulo uno", "nodeUno");
        FormCierreEspia formDos = host.Abrir("Módulo dos", "nodeDos");
        FormCierreEspia formTres = host.Abrir("Módulo tres", "nodeTres");

        host.CerrarTodasLasPestanas();

        Assert.AreEqual(0, host.Tabs.Length, "El host debe poder quedar sin pestañas.");
        Assert.IsTrue(formUno.IsDisposed);
        Assert.IsTrue(formDos.IsDisposed);
        Assert.IsTrue(formTres.IsDisposed);
        Assert.AreEqual(1, formUno.IntentosCierre, "Cada formulario debe cerrarse una sola vez.");
        Assert.AreEqual(1, formDos.IntentosCierre, "Cada formulario debe cerrarse una sola vez.");
        Assert.AreEqual(1, formTres.IntentosCierre, "Cada formulario debe cerrarse una sola vez.");

        // Cero pestañas es un estado válido: repetir la operación no debe fallar.
        host.CerrarTodasLasPestanas();
        Assert.AreEqual(0, host.Tabs.Length);
    }

    [TestMethod]
    public void CierreMasivo_ConFormularioQueCancela_ConservaSuPestanaYNoRepiteElIntento()
    {
        using HostPrueba host = new HostPrueba();
        FormCierreEspia formUno = host.Abrir("Módulo uno", "nodeUno");
        FormCierreEspia formDos = host.Abrir("Módulo dos", "nodeDos");
        FormCierreEspia formTres = host.Abrir("Módulo tres", "nodeTres");
        formDos.CancelarCierre = true;

        TabPage[] iniciales = host.Tabs;
        host.CerrarTodasLasPestanas();

        Assert.IsTrue(formUno.IsDisposed, "Los formularios que aceptan el cierre deben descartarse.");
        Assert.IsTrue(formTres.IsDisposed, "Los formularios que aceptan el cierre deben descartarse.");
        Assert.IsFalse(formDos.IsDisposed, "Un formulario que cancela el cierre no debe descartarse.");

        TabPage[] restantes = host.Tabs;
        Assert.AreEqual(1, restantes.Length);
        Assert.AreSame(iniciales[1], restantes[0], "La pestaña que cancela debe conservarse por referencia.");
        Assert.AreEqual(1, formDos.IntentosCierre,
            "El cierre masivo debe operar sobre una instantánea: no reintenta ni entra en bucle.");
    }

    [TestMethod]
    public void CerrarPestanaIndividual_Cancelada_ConservaPestanaYFormulario()
    {
        using HostPrueba host = new HostPrueba();
        FormCierreEspia formUno = host.Abrir("Módulo uno", "nodeUno");
        FormCierreEspia formDos = host.Abrir("Módulo dos", "nodeDos");
        formDos.CancelarCierre = true;

        TabPage[] iniciales = host.Tabs;
        host.CerrarPestana(iniciales[1]);

        Assert.IsTrue(host.Contiene(iniciales[1]), "La pestaña debe permanecer si el cierre se cancela.");
        Assert.IsFalse(formDos.IsDisposed, "El formulario debe permanecer vivo si el cierre se cancela.");
        Assert.AreEqual(1, formDos.IntentosCierre);
        Assert.IsFalse(formUno.IsDisposed, "La otra pestaña no debe verse afectada.");
        Assert.AreEqual(2, host.Tabs.Length);
    }

    [TestMethod]
    public void CerrarFormularioEmbebido_QuitaSuPestanaPorReferencia()
    {
        using HostPrueba host = new HostPrueba();
        FormCierreEspia formUno = host.Abrir("Módulo uno", "nodeUno");
        FormCierreEspia formDos = host.Abrir("Módulo dos", "nodeDos");
        FormCierreEspia formTres = host.Abrir("Módulo tres", "nodeTres");

        TabPage[] iniciales = host.Tabs;
        formDos.Close();

        Assert.IsTrue(formDos.IsDisposed);
        Assert.IsFalse(host.Contiene(iniciales[1]), "FormClosed debe retirar exactamente la pestaña de su formulario.");
        Assert.IsFalse(formUno.IsDisposed);
        Assert.IsFalse(formTres.IsDisposed);

        TabPage[] restantes = host.Tabs;
        Assert.AreEqual(2, restantes.Length);
        Assert.AreSame(iniciales[0], restantes[0]);
        Assert.AreSame(iniciales[2], restantes[1]);
    }

    [TestMethod]
    public void CerrarFormularioEmbebido_ConNombreDuplicado_QuitaSoloLaPestanaDeEseFormulario()
    {
        using HostPrueba host = new HostPrueba();
        FormCierreEspia formUno = host.Abrir("Primer módulo", "nodeDuplicado");
        FormCierreEspia formDos = host.Abrir("Segundo módulo", "nodeDuplicado");

        TabPage[] iniciales = host.Tabs;
        formUno.Close();

        Assert.IsTrue(formUno.IsDisposed);
        Assert.IsFalse(host.Contiene(iniciales[0]),
            "Debe retirarse la pestaña del formulario cerrado, no otra con el mismo Name.");
        Assert.IsTrue(host.Contiene(iniciales[1]),
            "Una pestaña con el mismo Name no debe retirarse en lugar de la cerrada.");
        Assert.IsFalse(formDos.IsDisposed);
        Assert.AreEqual(1, host.Tabs.Length);
        Assert.AreSame(iniciales[1], host.Tabs[0]);
    }

    [TestMethod]
    public void CerrarPestanaSinFormulario_LaQuitaSinExcepcion()
    {
        using HostPrueba host = new HostPrueba();
        TabPage tab = new TabPage("Vacía");
        host.TabControl.TabPages.Add(tab);

        host.CerrarPestana(tab);

        Assert.IsFalse(host.Contiene(tab));
        Assert.AreEqual(0, host.Tabs.Length);
    }

    private sealed class HostPrueba : IDisposable
    {
        private readonly string _root;
        private readonly SOPROContext _context;
        private readonly FormProyecto _host;
        private readonly FieldInfo _campoTabControl;
        private readonly MethodInfo _abrir;
        private readonly MethodInfo _cerrarPestana;
        private readonly MethodInfo _cerrarOtrasPestanas;
        private readonly MethodInfo _cerrarTodasLasPestanas;

        public HostPrueba()
        {
            _root = Path.Combine(Path.GetTempPath(), $"sopro_pestanas_cierre_{Guid.NewGuid():N}");
            Directory.CreateDirectory(_root);
            string dbPath = Path.Combine(_root, "proyecto-temporal.soproproj");

            _context = new SOPROContext(dbPath);
            _host = new FormProyecto(_context, CrearProyecto());
            _ = _host.Handle; // Abrir usa BeginInvoke; el host necesita handle sin disparar Load.

            _campoTabControl = typeof(FormProyecto).GetField("tabControl", PrivadoDeInstancia)
                ?? throw new InvalidOperationException("No se encontró el campo tabControl de FormProyecto.");
            _abrir = ObtenerMetodo("Abrir", typeof(string), typeof(Form), typeof(string));
            _cerrarPestana = ObtenerMetodo("CerrarPestana", typeof(TabPage));
            _cerrarOtrasPestanas = ObtenerMetodo("CerrarOtrasPestanas", typeof(TabPage));
            _cerrarTodasLasPestanas = ObtenerMetodo("CerrarTodasLasPestanas");
        }

        public TabControl TabControl => (TabControl)(_campoTabControl.GetValue(_host)
            ?? throw new InvalidOperationException("El host no tiene tabControl."));

        public TabPage[] Tabs => TabControl.TabPages.Cast<TabPage>().ToArray();

        public bool Contiene(TabPage tab) => TabControl.TabPages.Contains(tab);

        public FormCierreEspia Abrir(string titulo, string nodeName)
        {
            FormCierreEspia form = new FormCierreEspia();
            Invocar(_abrir, titulo, form, nodeName);
            return form;
        }

        public void CerrarPestana(TabPage tab) => Invocar(_cerrarPestana, tab);

        public void CerrarOtrasPestanas(TabPage tab) => Invocar(_cerrarOtrasPestanas, tab);

        public void CerrarTodasLasPestanas() => Invocar(_cerrarTodasLasPestanas);

        public void Dispose()
        {
            _host.Dispose();
            _context.Dispose();
            EliminarDirectorio(_root);
        }

        private static Proyecto CrearProyecto() => new()
        {
            Nombre = "Proyecto cierre de pestañas",
            Descripcion = string.Empty,
            Ubicacion = string.Empty,
            Convocante = string.Empty,
            Contratista = string.Empty,
            ApoderadoLegal = string.Empty,
            FechaInicio = new DateTime(2026, 1, 1),
            FechaTermino = new DateTime(2026, 1, 31),
            PlazoEjecucion = 31,
            DecimalesCantidad = 2,
            DecimalesImporte = 2,
            DecimalesPorcentaje = 4,
            FechaCreacion = new DateTime(2026, 1, 1),
            FechaModificacion = new DateTime(2026, 1, 1),
        };

        private static MethodInfo ObtenerMetodo(string nombre, params Type[] parametros)
            => typeof(FormProyecto).GetMethod(nombre, PrivadoDeInstancia, null, parametros, null)
                ?? throw new InvalidOperationException($"No se encontró el método privado FormProyecto.{nombre}.");

        private void Invocar(MethodInfo metodo, params object?[] argumentos)
            => metodo.Invoke(_host, argumentos);

        private void Invocar(MethodInfo metodo)
            => metodo.Invoke(_host, null);

        private static void EliminarDirectorio(string root)
        {
            for (int intento = 0; intento < 5; intento++)
            {
                try
                {
                    Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
                    if (Directory.Exists(root))
                        Directory.Delete(root, recursive: true);
                    return;
                }
                catch (IOException) when (intento < 4)
                {
                    System.Threading.Thread.Sleep(200);
                }
                catch (UnauthorizedAccessException) when (intento < 4)
                {
                    System.Threading.Thread.Sleep(200);
                }
            }
        }
    }

    private sealed class FormCierreEspia : Form
    {
        public FormCierreEspia()
        {
            FormClosing += (s, e) =>
            {
                IntentosCierre++;
                if (CancelarCierre) e.Cancel = true;
            };
        }

        public bool CancelarCierre { get; set; }

        public int IntentosCierre { get; private set; }
    }
}
