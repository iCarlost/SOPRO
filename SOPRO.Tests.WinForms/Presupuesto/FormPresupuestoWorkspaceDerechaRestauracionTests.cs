using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SOPRO.Application.Services;
using SOPRO.Core.Entities;
using SOPRO.Data.Context;
using SOPRO.WinForms.Controls;
using SOPRO.WinForms.Forms;
using SOPRO.WinForms.Helpers;

namespace SOPRO.Tests.WinForms.Presupuesto;

/// <summary>
/// Regresiones del issue #92 observadas en la app real:
/// 1) cambiar el workspace a Derecha debe PRESERVAR la edición de cabecera de
///    matriz (estado de edición, campos visibles, valores y radios) aun con
///    cambios pendientes, y nunca debe invocar el descarte del borrador;
/// 2) la preferencia de tamaño persistida no debe corromperse por eventos
///    internos de SplitContainer (DPI/relayout) y al reabrir el módulo el ancho
///    restaurado debe aproximarse a la preferencia guardada.
/// </summary>
[STATestClass]
[DoNotParallelize] // Construye el formulario real del módulo (controles WinForms y estado estático).
public class FormPresupuestoWorkspaceDerechaRestauracionTests
{
    private const string CampoProyectoGlobalFormato = "_proyectoActual";

    [TestInitialize]
    public void VerificarApartmentState()
    {
        Assert.AreEqual(ApartmentState.STA, Thread.CurrentThread.GetApartmentState(),
            "Las pruebas que crean FormPresupuesto deben ejecutarse en STA.");
    }

    // Un borrador de cabecera con valores reales sobrevive el cambio de posición:
    // la orientación opera sobre el MISMO contenedor y no resuelve la edición.
    // (El guard de descarte/autoguardado permanece en las rutas de cierre y
    // navegación del panel; aquí nada de eso corre.)
    [TestMethod]
    public void CambiarADerecha_ConCabeceraPendiente_ConservaEstadoValoresYRadios()
    {
        Proyecto? previo = null;
        FormPresupuesto? form = null;
        SOPROContext? ctx = null;
        DirectorioTemporal root = new DirectorioTemporal("ws_derecha_borrador");
        try
        {
            previo = LeerProyectoGlobalFormato();
            ctx = new SOPROContext(root.DbPath);
            ctx.Database.EnsureCreated();
            Proyecto escenario = CrearProyecto(ctx, "ws_derecha_borrador");

            form = CrearForm(ctx, escenario, root);
            form.Show();
            System.Windows.Forms.Application.DoEvents();

            form.ExpandirWorkspaceParaPrueba();
            System.Windows.Forms.Application.DoEvents();

            PanelMatricesEmbebido panel = form.PanelMatricesInterno!;
            FijarCampo(panel, "_modoCreacionMatriz", true);
            FijarCampo(panel, "_modoEdicionCabecera", true);
            Control panelDatos = LeerCampoInstancia<Control>(panel, "_panelDatos")
                ?? throw new InvalidOperationException("Debe existir el panel de datos de cabecera.");
            panelDatos.Visible = true;
            TextBox txtClave = LeerCampoInstancia<TextBox>(panel, "_txtClaveMatriz")
                ?? throw new InvalidOperationException("Debe existir la caja de clave del borrador.");
            txtClave.Text = "PRS-01";
            TextBox txtUnidad = LeerCampoInstancia<TextBox>(panel, "_txtUnidadMatriz")
                ?? throw new InvalidOperationException("Debe existir la caja de unidad del borrador.");
            txtUnidad.Text = "M2";
            RadioButton rbBasico = LeerCampoInstancia<RadioButton>(panel, "_rbTipoBasico")
                ?? throw new InvalidOperationException("Debe existir el radio Básico del borrador.");
            rbBasico.Checked = true;
            System.Windows.Forms.Application.DoEvents();

            Assert.IsTrue(panel.CabeceraDatosVisiblesParaPrueba,
                "Precondición: la edición de cabecera estaba visible en modo Abajo.");
            Assert.AreEqual("PRS-01", panel.ClaveCabeceraParaPrueba);
            Assert.AreEqual("M2", panel.UnidadCabeceraParaPrueba);
            Assert.AreEqual(TipoMatriz.Basico, panel.TipoCabeceraParaPrueba);

            bool invocoDescarte = false;
            Func<bool> descarteOriginal = PanelMatricesEmbebido.ConfirmarDescartarNuevaMatriz;
            PanelMatricesEmbebido.ConfirmarDescartarNuevaMatriz = () =>
            {
                invocoDescarte = true;
                return false;
            };
            try
            {
                InvocarPrivate(form, "CambiarPosicionWorkspace", PosicionWorkspace.Derecha);
                System.Windows.Forms.Application.DoEvents();
            }
            finally
            {
                PanelMatricesEmbebido.ConfirmarDescartarNuevaMatriz = descarteOriginal;
            }

            Assert.AreEqual(PosicionWorkspace.Derecha, form.PosicionWorkspaceActual,
                "El cambio de orientación debe aplicarse sin resolver la edición pendiente.");
            Assert.AreEqual(Orientation.Vertical, form.SplitWorkspace.Orientation);
            Assert.IsFalse(invocoDescarte,
                "El cambio de orientación nunca debe pedir descartar el borrador.");
            Assert.IsTrue(panel.CabeceraDatosVisiblesParaPrueba,
                "La vista de cabecera debe seguir visible tras el cambio a Derecha.");
            Assert.AreEqual("PRS-01", panel.ClaveCabeceraParaPrueba,
                "El valor de clave capturado debe sobrevivir al cambio de posición.");
            Assert.AreEqual("M2", panel.UnidadCabeceraParaPrueba,
                "El valor de unidad capturado debe sobrevivir al cambio de posición.");
            Assert.AreEqual(TipoMatriz.Basico, panel.TipoCabeceraParaPrueba,
                "La selección de tipo (radio) debe sobrevivir al cambio de posición.");
            Assert.IsTrue(LeerCampoValor<bool>(panel, "_modoCreacionMatriz"),
                "El estado de edición (creación de matriz) debe conservarse.");
            Assert.IsTrue(LeerCampoValor<bool>(panel, "_modoEdicionCabecera"),
                "El estado de edición de cabecera debe conservarse.");
        }
        finally
        {
            LiberarEscenario(previo, form, ctx, root);
        }
    }

    // WinForms emite SplitterMoved por causas internas (mantenimiento de layout y
    // escalamiento DPI en ScaleControl) SIN pasar por SplitterMoving: esos eventos
    // no representan una elección del usuario y NO deben alimentar ni el estado en
    // memoria ni el archivo (raíz del panel que reabre al ancho máximo).
    [TestMethod]
    public void SplitterMovedInterno_SinMovimientoDeUsuario_NoTocaPreferenciasNiArchivo()
    {
        Proyecto? previo = null;
        FormPresupuesto? form = null;
        SOPROContext? ctx = null;
        DirectorioTemporal root = new DirectorioTemporal("ws_split_interno");
        try
        {
            previo = LeerProyectoGlobalFormato();
            ctx = new SOPROContext(root.DbPath);
            ctx.Database.EnsureCreated();
            Proyecto escenario = CrearProyecto(ctx, "ws_split_interno");

            ProjectWorkspaceService workspace = new ProjectWorkspaceService(
                Path.Combine(root.Ruta, "sopro"), WorkspaceLocal(root));
            Directory.CreateDirectory(workspace.LocalDataFolder);
            string rutaEstado = RutaEstadoNueva(workspace, escenario.Id, ctx.DatabasePath);
            new PresupuestoWorkspaceEstado(PosicionWorkspace.Derecha, 380, 560).Guardar(rutaEstado);

            form = new FormPresupuesto(ctx, escenario, workspace);

            Assert.AreEqual(560, LeerCampo<int?>(form, "_workspacePanelWidth"));
            Assert.AreEqual(380, LeerCampo<int?>(form, "_workspacePanelHeight"));
            // El archivo queda con "basura" para verificar que el evento interno NO lo guarda.
            File.WriteAllText(rutaEstado, "editor-no-debe-guardar-aqui\n");

            form.SplitWorkspace.OnSplitterMoved(new SplitterEventArgs(0, 0, 0, 0));
            System.Windows.Forms.Application.DoEvents();

            Assert.AreEqual(560, LeerCampo<int?>(form, "_workspacePanelWidth"),
                "Un SplitterMoved interno no debe recalcular el ancho preferido.");
            Assert.AreEqual(380, LeerCampo<int?>(form, "_workspacePanelHeight"),
                "Un SplitterMoved interno no debe recalcular el alto preferido.");
            Assert.AreEqual("editor-no-debe-guardar-aqui\n", File.ReadAllText(rutaEstado),
                "Sin movimiento de usuario no debe reescribirse el archivo de estado.");
        }
        finally
        {
            LiberarEscenario(previo, form, ctx, root);
        }
    }

    // Un movimiento de separador iniciado por el usuario (SplitterMoving seguido de
    // SplitterMoved) SÍ persiste: reescribe el archivo con las preferencias vigentes.
    [TestMethod]
    public void SplitterMovimientoDeUsuario_ReescribeElArchivoConLasPreferencias()
    {
        Proyecto? previo = null;
        FormPresupuesto? form = null;
        SOPROContext? ctx = null;
        DirectorioTemporal root = new DirectorioTemporal("ws_split_usuario");
        try
        {
            previo = LeerProyectoGlobalFormato();
            ctx = new SOPROContext(root.DbPath);
            ctx.Database.EnsureCreated();
            Proyecto escenario = CrearProyecto(ctx, "ws_split_usuario");

            ProjectWorkspaceService workspace = new ProjectWorkspaceService(
                Path.Combine(root.Ruta, "sopro"), WorkspaceLocal(root));
            Directory.CreateDirectory(workspace.LocalDataFolder);
            string rutaEstado = RutaEstadoNueva(workspace, escenario.Id, ctx.DatabasePath);
            new PresupuestoWorkspaceEstado(PosicionWorkspace.Derecha, 380, 560).Guardar(rutaEstado);

            form = new FormPresupuesto(ctx, escenario, workspace);

            File.WriteAllText(rutaEstado, "editor-no-debe-guardar-aqui\n");

            SplitterCancelEventArgs moving = new SplitterCancelEventArgs(0, 0, 0, 0);
            form.SplitWorkspace.OnSplitterMoving(moving);
            form.SplitWorkspace.OnSplitterMoved(new SplitterEventArgs(0, 0, 0, 0));
            System.Windows.Forms.Application.DoEvents();

            PresupuestoWorkspaceEstado estado = PresupuestoWorkspaceEstado.Leer(rutaEstado);
            Assert.AreEqual(PosicionWorkspace.Derecha, estado.Posicion,
                "El movimiento de usuario reescribe el archivo con la posición vigente.");
            Assert.AreEqual(560, estado.Ancho,
                "El ancho preferido debe sobrevivir al guardar por movimiento de usuario.");
            Assert.AreEqual(380, estado.Alto,
                "El alto preferido debe sobrevivir al guardar por movimiento de usuario.");
        }
        finally
        {
            LiberarEscenario(previo, form, ctx, root);
        }
    }

    // Reabrir el módulo de Presupuesto (nueva instancia del form) debe restaurar el
    // ancho del panel ≈ al persistido, nunca consumir el máximo disponible. Ventana
    // forzada a tamaño conocido para que la preferencia 640 siempre quepa.
    [TestMethod]
    public void ReabrirModuloRestauraAnchoDerechaAproximadoAlPersistido()
    {
        Proyecto? previo = null;
        SOPROContext? ctx = null;
        DirectorioTemporal root = new DirectorioTemporal("ws_reabrir_ancho");
        try
        {
            previo = LeerProyectoGlobalFormato();
            ctx = new SOPROContext(root.DbPath);
            ctx.Database.EnsureCreated();
            Proyecto escenario = CrearProyecto(ctx, "ws_reabrir_ancho");

            ProjectWorkspaceService workspace = new ProjectWorkspaceService(
                Path.Combine(root.Ruta, "sopro"), WorkspaceLocal(root));
            Directory.CreateDirectory(workspace.LocalDataFolder);
            string rutaEstado = RutaEstadoNueva(workspace, escenario.Id, ctx.DatabasePath);
            new PresupuestoWorkspaceEstado(PosicionWorkspace.Derecha, 350, 640).Guardar(rutaEstado);

            // Sesión 1: abrir módulo, workspace en Derecha con la preferencia persistida.
            FormPresupuesto form1 = new FormPresupuesto(ctx, escenario, workspace);
            form1.WindowState = FormWindowState.Normal;
            form1.ClientSize = new Size(1200, 852);
            form1.Show();
            System.Windows.Forms.Application.DoEvents();

            SplitContainer split1 = form1.SplitWorkspace;
            Assert.IsTrue(split1.Width - split1.Panel1MinSize - split1.SplitterWidth >= 640,
                $"Precondición: con la ventana forzada a 1200 px la preferencia 640 cabe " +
                $"(split={split1.Width}, p1Min={split1.Panel1MinSize}, sep={split1.SplitterWidth}).");

            form1.ExpandirWorkspaceParaPrueba();
            System.Windows.Forms.Application.DoEvents();
            Assert.IsTrue(Math.Abs(split1.Panel2.Width - 640) <= 2,
                $"El panel restaurado en la sesión 1 debe medir ≈640px (medido={split1.Panel2.Width}).");

            PresupuestoWorkspaceEstado estado1 = PresupuestoWorkspaceEstado.Leer(rutaEstado);
            Assert.AreEqual(640, estado1.Ancho,
                "La preferencia persistida no debe corromperse durante la primera sesión.");
            Assert.AreEqual(PosicionWorkspace.Derecha, estado1.Posicion);

            form1.Close();
            form1.Dispose();
            System.Windows.Forms.Application.DoEvents();

            PresupuestoWorkspaceEstado estadoTrasCierre = PresupuestoWorkspaceEstado.Leer(rutaEstado);
            Assert.AreEqual(640, estadoTrasCierre.Ancho,
                "Al cerrar el módulo el ancho persistido debe seguir siendo el del usuario.");

            // Sesión 2: nueva instancia del mismo módulo (usa el estado persistido).
            FormPresupuesto form2 = new FormPresupuesto(ctx, escenario, workspace);
            form2.WindowState = FormWindowState.Normal;
            form2.ClientSize = new Size(1200, 852);
            form2.Show();
            System.Windows.Forms.Application.DoEvents();
            Assert.AreEqual(Orientation.Vertical, form2.SplitWorkspace.Orientation,
                "La posición Derecha persistida restaura el split vertical.");

            form2.ExpandirWorkspaceParaPrueba();
            System.Windows.Forms.Application.DoEvents();

            SplitContainer split2 = form2.SplitWorkspace;
            Assert.IsTrue(Math.Abs(split2.Panel2.Width - 640) <= 2,
                $"El ancho restaurado de Panel2 en el módulo reabierto debe ser ≈640, no el " +
                $"máximo disponible (medido={split2.Panel2.Width}, " +
                $"tope={split2.Width - split2.Panel1MinSize - split2.SplitterWidth}).");

            PresupuestoWorkspaceEstado estado2 = PresupuestoWorkspaceEstado.Leer(rutaEstado);
            Assert.AreEqual(640, estado2.Ancho,
                "El ancho persistido debe sobrevivir completo al ciclo de reapertura.");

            form2.Close();
            form2.Dispose();
            System.Windows.Forms.Application.DoEvents();
        }
        finally
        {
            ctx?.Dispose();
            RestaurarProyectoGlobalFormato(previo);
            root.Dispose();
        }
    }

    // La misma LocalDataFolder compartida no cruza estados entre bases distintas:
    // dos .soproproj diferentes con Id=1 reciben rutas y estado propios
    // (el nombre del archivo de estado es base + Id).
    [TestMethod]
    public void DosBasesDistintasConMismoId_TienenRutasYEstadoIndependientes()
    {
        Proyecto? previo = null;
        SOPROContext? ctxA = null;
        SOPROContext? ctxB = null;
        FormPresupuesto? formA1 = null;
        FormPresupuesto? formB1 = null;
        FormPresupuesto? formA2 = null;
        FormPresupuesto? formB2 = null;
        DirectorioTemporal root = new DirectorioTemporal("ws_estado_por_base");
        try
        {
            previo = LeerProyectoGlobalFormato();
            string rutaLocal = WorkspaceLocal(root);
            Directory.CreateDirectory(rutaLocal);
            ProjectWorkspaceService workspaceCompartido = new ProjectWorkspaceService(
                Path.Combine(root.Ruta, "sopro"), rutaLocal);

            ctxA = new SOPROContext(Path.Combine(root.Ruta, "presupuesto-a.soproproj"));
            ctxA.Database.EnsureCreated();
            ctxB = new SOPROContext(Path.Combine(root.Ruta, "presupuesto-b.soproproj"));
            ctxB.Database.EnsureCreated();
            Proyecto proyectoA = CrearProyecto(ctxA, "por_base_a");
            Proyecto proyectoB = CrearProyecto(ctxB, "por_base_b");
            Assert.AreEqual(1, proyectoA.Id, "Precondición: el primer proyecto de la base A es Id=1.");
            Assert.AreEqual(1, proyectoB.Id, "Precondición: el primer proyecto de la base B es Id=1.");

            formA1 = new FormPresupuesto(ctxA, proyectoA, workspaceCompartido);
            formB1 = new FormPresupuesto(ctxB, proyectoB, workspaceCompartido);

            Assert.AreNotEqual(formA1.WorkspacePanelStatePath, formB1.WorkspacePanelStatePath,
                "Bases distintas (aunque ambas con Id=1) no deben compartir archivo de estado.");
            StringAssert.StartsWith(formA1.WorkspacePanelStatePath, rutaLocal,
                "La ruta del estado sigue viviendo bajo la carpeta de datos locales del workspace.");
            StringAssert.StartsWith(formB1.WorkspacePanelStatePath, rutaLocal);
            Assert.AreEqual(Path.GetFileName(formB1.LegacyWorkspacePanelStatePath),
                $"presupuesto_workspace_{proyectoB.Id}.txt",
                "La ruta heredada sigue siendo la del formato anterior (solo Id).");

            FijarCampo(formA1, "_posicionWorkspace", PosicionWorkspace.Derecha);
            FijarCampo(formA1, "_workspacePanelWidth", 640);
            InvocarPrivate(formA1, "SaveWorkspacePanelState");

            FijarCampo(formB1, "_posicionWorkspace", PosicionWorkspace.Abajo);
            FijarCampo(formB1, "_workspacePanelHeight", 402);
            InvocarPrivate(formB1, "SaveWorkspacePanelState");

            // Reapertura con instancias nuevas por base: cada una restaura SU estado.
            formA2 = new FormPresupuesto(ctxA, proyectoA, workspaceCompartido);
            formB2 = new FormPresupuesto(ctxB, proyectoB, workspaceCompartido);

            Assert.AreEqual(PosicionWorkspace.Derecha, formA2.PosicionWorkspaceActual,
                "La base A restaura su posición Derecha sin cruzar la de la base B.");
            Assert.AreEqual(640, LeerCampo<int?>(formA2, "_workspacePanelWidth"),
                "La base A restaura su ancho preferido.");
            Assert.AreEqual(PosicionWorkspace.Abajo, formB2.PosicionWorkspaceActual,
                "La base B restaura su posición Abajo sin cruzar la de la base A.");
            Assert.AreEqual(402, LeerCampo<int?>(formB2, "_workspacePanelHeight"),
                "La base B restaura su alto preferido.");

            PresupuestoWorkspaceEstado estadoA = PresupuestoWorkspaceEstado.Leer(formA2.WorkspacePanelStatePath);
            Assert.AreEqual(PosicionWorkspace.Derecha, estadoA.Posicion);
            Assert.AreEqual(640, estadoA.Ancho);
            PresupuestoWorkspaceEstado estadoB = PresupuestoWorkspaceEstado.Leer(formB2.WorkspacePanelStatePath);
            Assert.AreEqual(PosicionWorkspace.Abajo, estadoB.Posicion);
            Assert.AreEqual(402, estadoB.Alto);
        }
        finally
        {
            formA1?.Dispose();
            formB1?.Dispose();
            formA2?.Dispose();
            formB2?.Dispose();
            ctxA?.Dispose();
            ctxB?.Dispose();
            RestaurarProyectoGlobalFormato(previo);
            root.Dispose();
        }
    }

    private static string WorkspaceLocal(DirectorioTemporal root) => Path.Combine(root.Ruta, "local");

    private static string RutaEstadoNueva(ProjectWorkspaceService workspace, int proyectoId, string? databasePath)
        => Path.Combine(workspace.LocalDataFolder,
            $"presupuesto_workspace_{proyectoId}_{FormPresupuesto.NombreArchivoEstadoPorDb(databasePath)}.txt");

    private static Proyecto CrearProyecto(SOPROContext ctx, string nombre)
    {
        Proyecto proyecto = new Proyecto
        {
            Nombre = $"Proyecto {nombre}",
            Descripcion = string.Empty,
            Ubicacion = string.Empty,
            Convocante = string.Empty,
            Contratista = string.Empty,
            ApoderadoLegal = string.Empty,
            FechaInicio = new DateTime(2026, 1, 1),
            FechaTermino = new DateTime(2026, 1, 31),
            PlazoEjecucion = 31,
            PorcentajeIndirectosCentral = 0m,
            PorcentajeIndirectosCampo = 0m,
            PorcentajeFinanciamiento = 0m,
            PorcentajeUtilidad = 0m,
            PorcentajeCargosAdicionales = 0m,
            PorcentajeIVA = 0m,
            DecimalesCantidad = 2,
            DecimalesImporte = 2,
            DecimalesPorcentaje = 4,
            FechaCreacion = new DateTime(2026, 1, 1),
            FechaModificacion = new DateTime(2026, 1, 1),
        };
        ctx.Proyectos.Add(proyecto);
        ctx.SaveChanges();
        return proyecto;
    }

    private static FormPresupuesto CrearForm(SOPROContext ctx, Proyecto proyecto, DirectorioTemporal root)
        => new FormPresupuesto(ctx, proyecto, new ProjectWorkspaceService(
            Path.Combine(root.Ruta, "sopro"), Path.Combine(root.Ruta, "local")));

    private static void LiberarEscenario(Proyecto? previo, FormPresupuesto? form, SOPROContext? ctx, DirectorioTemporal root)
    {
        form?.Dispose();
        ctx?.Dispose();
        RestaurarProyectoGlobalFormato(previo);
        root.Dispose();
    }

    private static void InvocarPrivate(FormPresupuesto form, string metodo, params object?[] args)
    {
        MethodInfo? mi = typeof(FormPresupuesto).GetMethod(metodo,
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(mi, $"Debe existir {metodo}.");
        mi!.Invoke(form, args);
        System.Windows.Forms.Application.DoEvents();
    }

    private static void FijarCampo(object instancia, string nombre, object? valor)
    {
        FieldInfo? campo = instancia.GetType().GetField(nombre, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(campo, $"Debe existir el campo {nombre} en {instancia.GetType().Name}.");
        campo!.SetValue(instancia, valor);
    }

    private static T? LeerCampoInstancia<T>(object instancia, string nombre)
        where T : class
        => (T?)instancia.GetType().GetField(nombre, BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(instancia);

    private static T LeerCampoValor<T>(object instancia, string nombre)
    {
        object? valor = instancia.GetType().GetField(nombre, BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(instancia);
        Assert.IsNotNull(valor, $"El campo {nombre} debe tener valor.");
        return (T)valor;
    }

    private static T? LeerCampo<T>(FormPresupuesto form, string campo)
        => (T?)typeof(FormPresupuesto).GetField(campo, BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(form);

    private static Proyecto? LeerProyectoGlobalFormato()
        => (Proyecto?)typeof(FormatoHelper).GetField(CampoProyectoGlobalFormato,
               BindingFlags.Static | BindingFlags.NonPublic)?.GetValue(null);

    private static void RestaurarProyectoGlobalFormato(Proyecto? previo)
        => typeof(FormatoHelper).GetField(CampoProyectoGlobalFormato,
               BindingFlags.Static | BindingFlags.NonPublic)
           ?.SetValue(null, previo);
}
