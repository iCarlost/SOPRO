using System;
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
/// Posición del workspace (Abajo / Derecha) sobre el form real: preferencia
/// local, herencia del formato de un entero, cambio de orientación vivo que
/// reutiliza el mismo split/host/panel, guard de cabecera y geometría viable.
/// </summary>
[STATestClass]
[DoNotParallelize] // Construye el formulario real del módulo (controles WinForms y estado estático).
public class FormPresupuestoWorkspacePosicionTests
{
    private const string CampoProyectoGlobalFormato = "_proyectoActual";

    private const string ToggleAbajoCerrado = "\U0001F4D0 Matrices \u25BC";
    private const string ToggleAbajoAbierto = "\U0001F4D0 Matrices \u25B2";
    private const string ToggleDerechaCerrado = "\U0001F4D0 Matrices \u25C0";
    private const string ToggleDerechaAbierto = "\U0001F4D0 Matrices \u25B6";

    [TestInitialize]
    public void VerificarApartmentState()
    {
        Assert.AreEqual(ApartmentState.STA, Thread.CurrentThread.GetApartmentState(),
            "Las pruebas que crean FormPresupuesto deben ejecutarse en STA.");
    }

    // Sin estado guardado rige el default Abajo; el constructor solo lee y no crea el archivo.
    [TestMethod]
    public void SinEstadoGuardado_ElWorkspaceIniciaEnAbajoConSplitHorizontal()
    {
        Proyecto? previo = null;
        FormPresupuesto? form = null;
        SOPROContext? ctx = null;
        DirectorioTemporal root = new DirectorioTemporal("ws_pos_default");
        try
        {
            previo = LeerProyectoGlobalFormato();
            ctx = new SOPROContext(root.DbPath);
            ctx.Database.EnsureCreated();
            Proyecto escenario = CrearProyecto(ctx, "ws_pos_default");

            form = CrearForm(ctx, escenario, root);

            Assert.AreEqual(PosicionWorkspace.Abajo, form.PosicionWorkspaceActual);
            Assert.AreEqual(Orientation.Horizontal, form.SplitWorkspace.Orientation,
                "El modo Abajo parte el área arriba/abajo: presupuesto en Panel1, workspace en Panel2.");
            Assert.AreEqual(ToggleAbajoCerrado, form.TextoBotonToggleMatrices);
            Assert.IsFalse(File.Exists(form.WorkspacePanelStatePath),
                "El constructor solo lee la preferencia; no debe crear el archivo por sí solo.");
        }
        finally
        {
            form?.Dispose();
            LiberarEscenario(previo, ctx, root);
        }
    }

    // Herencia de compatibilidad: el archivo histórico (solo Id) de un entero es el
    // ALTO del modo Abajo. Se lee una única vez y se MIGRA a la nueva ruta base+Id
    // sin borrar el archivo heredado (otra app o proyecto puede seguir leyéndolo).
    [TestMethod]
    public void EstadoHeredadoDeUnEntero_SeLeeComoAltoConAbajoYMigraALaNuevaRuta()
    {
        Proyecto? previo = null;
        FormPresupuesto? form = null;
        SOPROContext? ctx = null;
        DirectorioTemporal root = new DirectorioTemporal("ws_pos_legacy");
        try
        {
            previo = LeerProyectoGlobalFormato();
            ctx = new SOPROContext(root.DbPath);
            ctx.Database.EnsureCreated();
            Proyecto escenario = CrearProyecto(ctx, "ws_pos_legacy");

            ProjectWorkspaceService workspace = new ProjectWorkspaceService(
                Path.Combine(root.Ruta, "sopro"), Path.Combine(root.Ruta, "local"));
            Directory.CreateDirectory(workspace.LocalDataFolder);
            string rutaLegado = Path.Combine(workspace.LocalDataFolder, $"presupuesto_workspace_{escenario.Id}.txt");
            File.WriteAllText(rutaLegado, "402");

            form = new FormPresupuesto(ctx, escenario, workspace);

            Assert.AreEqual(PosicionWorkspace.Abajo, form.PosicionWorkspaceActual);
            Assert.AreEqual(Orientation.Horizontal, form.SplitWorkspace.Orientation);
            Assert.AreEqual(402, LeerCampo<int?>(form, "_workspacePanelHeight"),
                "El entero heredado es la altura preferida del workspace en modo Abajo.");
            Assert.AreEqual(0, LeerCampo<int?>(form, "_workspacePanelWidth"),
                "El formato heredado no guarda ancho: queda sin preferencia.");

            string rutaNueva = Path.Combine(workspace.LocalDataFolder,
                $"presupuesto_workspace_{escenario.Id}_{FormPresupuesto.NombreArchivoEstadoPorDb(ctx.DatabasePath)}.txt");
            Assert.IsTrue(File.Exists(rutaNueva),
                "El estado heredado se importa (migra) a la nueva ruta base+Id.");
            PresupuestoWorkspaceEstado migrado = PresupuestoWorkspaceEstado.Leer(rutaNueva);
            Assert.AreEqual(PosicionWorkspace.Abajo, migrado.Posicion,
                "El contenido heredado migra como Abajo/altura.");
            Assert.AreEqual(402, migrado.Alto);
            Assert.AreEqual(0, migrado.Ancho);
            Assert.IsTrue(File.Exists(rutaLegado),
                "El archivo heredado no se borra: otra app o proyecto puede seguir usándolo.");
        }
        finally
        {
            form?.Dispose();
            LiberarEscenario(previo, ctx, root);
        }
    }

    // En ambas orientaciones el grid vive en Panel1 y el workspace en Panel2: la
    // jerarquía de controles no se espeja ni se re-hospeda.
    [TestMethod]
    public void EstadoCompletoEnDerecha_RestauraOrientacionYAmbasDimensiones()
    {
        Proyecto? previo = null;
        FormPresupuesto? form = null;
        SOPROContext? ctx = null;
        DirectorioTemporal root = new DirectorioTemporal("ws_pos_right");
        try
        {
            previo = LeerProyectoGlobalFormato();
            ctx = new SOPROContext(root.DbPath);
            ctx.Database.EnsureCreated();
            Proyecto escenario = CrearProyecto(ctx, "ws_pos_right");

            ProjectWorkspaceService workspace = new ProjectWorkspaceService(
                Path.Combine(root.Ruta, "sopro"), Path.Combine(root.Ruta, "local"));
            Directory.CreateDirectory(workspace.LocalDataFolder);
            new PresupuestoWorkspaceEstado(PosicionWorkspace.Derecha, 350, 640).Guardar(
                Path.Combine(workspace.LocalDataFolder, $"presupuesto_workspace_{escenario.Id}.txt"));

            form = new FormPresupuesto(ctx, escenario, workspace);

            Assert.AreEqual(PosicionWorkspace.Derecha, form.PosicionWorkspaceActual);
            Assert.AreEqual(Orientation.Vertical, form.SplitWorkspace.Orientation,
                "Derecha parte el área izquierda/derecha: el panel ancla al borde derecho y crece a la izquierda.");
            Assert.AreSame(form.SplitWorkspace.Panel1, form.GridPrincipal!.Parent);
            Assert.AreSame(form.SplitWorkspace.Panel2, form.PanelMatricesInterno!.Parent!.Parent!.Parent,
                "El workspace debe seguir en Panel2 (grid - host - panelMatrices - Panel2) sin espejar controles.");
            Assert.AreEqual(ToggleDerechaCerrado, form.TextoBotonToggleMatrices);
            Assert.AreEqual("Posición: Derecha", form.TextoBotonPosicion);
            Assert.AreEqual(640, LeerCampo<int?>(form, "_workspacePanelWidth"),
                "El ancho guardado es la preferencia del modo Derecha.");
            Assert.AreEqual(350, LeerCampo<int?>(form, "_workspacePanelHeight"),
                "El alto guardado se conserva para retornos a Abajo.");
        }
        finally
        {
            form?.Dispose();
            LiberarEscenario(previo, ctx, root);
        }
    }

    // La posición se persiste aunque el workspace esté colapsado (el alto/ancho solo si expandido).
    [TestMethod]
    public void GuardarEstadoDesdeFormReal_EscribePosicionAltoYAnchoEnElEstado()
    {
        Proyecto? previo = null;
        FormPresupuesto? form = null;
        SOPROContext? ctx = null;
        DirectorioTemporal root = new DirectorioTemporal("ws_pos_roundtrip");
        try
        {
            previo = LeerProyectoGlobalFormato();
            ctx = new SOPROContext(root.DbPath);
            ctx.Database.EnsureCreated();
            Proyecto escenario = CrearProyecto(ctx, "ws_pos_roundtrip");

            form = CrearForm(ctx, escenario, root);
            Assert.IsFalse(File.Exists(form.WorkspacePanelStatePath));

            GuardarCampo(form, "_posicionWorkspace", PosicionWorkspace.Derecha);
            GuardarCampo(form, "_workspacePanelHeight", 380);
            GuardarCampo(form, "_workspacePanelWidth", 560);
            InvocarPrivate(form, "SaveWorkspacePanelState");

            PresupuestoWorkspaceEstado estado = PresupuestoWorkspaceEstado.Leer(form.WorkspacePanelStatePath);
            Assert.AreEqual(PosicionWorkspace.Derecha, estado.Posicion);
            Assert.AreEqual(380, estado.Alto);
            Assert.AreEqual(560, estado.Ancho);
        }
        finally
        {
            form?.Dispose();
            LiberarEscenario(previo, ctx, root);
        }
    }

    [TestMethod]
    public void CambiarPosicion_IdaYVuelta_ReutilizaHostYNoDuplicaElPanel()
    {
        Proyecto? previo = null;
        FormPresupuesto? form = null;
        SOPROContext? ctx = null;
        DirectorioTemporal root = new DirectorioTemporal("ws_pos_switch");
        try
        {
            previo = LeerProyectoGlobalFormato();
            ctx = new SOPROContext(root.DbPath);
            ctx.Database.EnsureCreated();
            Proyecto escenario = CrearProyecto(ctx, "ws_pos_switch");

            form = CrearForm(ctx, escenario, root);
            form.Show();
            System.Windows.Forms.Application.DoEvents();

            form.ExpandirWorkspaceParaPrueba();
            System.Windows.Forms.Application.DoEvents();
            Assert.AreEqual(Orientation.Horizontal, form.SplitWorkspace.Orientation);
            Assert.AreEqual(ToggleAbajoAbierto, form.TextoBotonToggleMatrices);

            Panel host = (Panel)form.PanelMatricesInterno!.Parent!;

            InvocarPrivate(form, "CambiarPosicionWorkspace", PosicionWorkspace.Derecha);
            System.Windows.Forms.Application.DoEvents();

            Assert.AreEqual(PosicionWorkspace.Derecha, form.PosicionWorkspaceActual);
            Assert.AreEqual(Orientation.Vertical, form.SplitWorkspace.Orientation);
            Assert.AreSame(host, form.PanelMatricesInterno!.Parent,
                "El cambio de posición reutiliza el panelMatricesHost original.");
            Assert.AreSame(form.SplitWorkspace.Panel2, form.PanelMatricesInterno!.Parent!.Parent!.Parent,
                "El workspace debe permanecer en Panel2 (derecha) tras el cambio.");
            Assert.AreEqual(1, host.Controls.OfType<PanelMatricesEmbebido>().Count(),
                "Nunca debe duplicarse el panel embebido dentro del host.");
            Assert.AreEqual(ToggleDerechaAbierto, form.TextoBotonToggleMatrices);
            Assert.AreEqual("Posición: Derecha", form.TextoBotonPosicion);
            Assert.IsFalse(form.SplitWorkspace.Panel2Collapsed, "El workspace sigue expandido si lo estaba.");
            Assert.AreEqual(MinimoDegradadoDerecha(form), form.SplitWorkspace.Panel2MinSize,
                "El piso de degradación se recalcula DPI-escalado para la dimensión de ancho.");

            InvocarPrivate(form, "CambiarPosicionWorkspace", PosicionWorkspace.Abajo);
            System.Windows.Forms.Application.DoEvents();

            Assert.AreEqual(PosicionWorkspace.Abajo, form.PosicionWorkspaceActual);
            Assert.AreEqual(Orientation.Horizontal, form.SplitWorkspace.Orientation);
            Assert.AreSame(host, form.PanelMatricesInterno!.Parent);
            Assert.AreEqual(1, host.Controls.OfType<PanelMatricesEmbebido>().Count());
            Assert.AreEqual(ToggleAbajoAbierto, form.TextoBotonToggleMatrices);
            Assert.AreEqual("Posición: Abajo", form.TextoBotonPosicion);

            PresupuestoWorkspaceEstado estado = PresupuestoWorkspaceEstado.Leer(form.WorkspacePanelStatePath);
            Assert.AreEqual(PosicionWorkspace.Abajo, estado.Posicion,
                "El cambio persiste la posición de vuelta a Abajo.");
        }
        finally
        {
            form?.Dispose();
            LiberarEscenario(previo, ctx, root);
        }
    }

    // Con el workspace oculto también aplica la orientación y persiste la posición.
    [TestMethod]
    public void CambiarPosicion_ConWorkspaceColapsado_ConservaElColapsoYPersiste()
    {
        Proyecto? previo = null;
        FormPresupuesto? form = null;
        SOPROContext? ctx = null;
        DirectorioTemporal root = new DirectorioTemporal("ws_pos_collapsed");
        try
        {
            previo = LeerProyectoGlobalFormato();
            ctx = new SOPROContext(root.DbPath);
            ctx.Database.EnsureCreated();
            Proyecto escenario = CrearProyecto(ctx, "ws_pos_collapsed");

            form = CrearForm(ctx, escenario, root);
            form.Show();
            System.Windows.Forms.Application.DoEvents();
            Assert.IsTrue(form.SplitWorkspace.Panel2Collapsed, "El constructor abre colapsado.");

            InvocarPrivate(form, "CambiarPosicionWorkspace", PosicionWorkspace.Derecha);
            System.Windows.Forms.Application.DoEvents();

            Assert.AreEqual(PosicionWorkspace.Derecha, form.PosicionWorkspaceActual);
            Assert.AreEqual(Orientation.Vertical, form.SplitWorkspace.Orientation);
            Assert.IsTrue(form.SplitWorkspace.Panel2Collapsed,
                "El estado colapsado debe sobrevivir al cambio de posición.");
            Assert.AreEqual(ToggleDerechaCerrado, form.TextoBotonToggleMatrices);

            PresupuestoWorkspaceEstado estado = PresupuestoWorkspaceEstado.Leer(form.WorkspacePanelStatePath);
            Assert.AreEqual(PosicionWorkspace.Derecha, estado.Posicion,
                "La posición se guarda aunque el workspace esté oculto.");

            form.ExpandirWorkspaceParaPrueba();
            System.Windows.Forms.Application.DoEvents();
            Assert.IsFalse(form.SplitWorkspace.Panel2Collapsed);
            Assert.AreEqual(MinimoDegradadoDerecha(form), form.SplitWorkspace.Panel2MinSize,
                "Al reabrir en Derecha rige el piso de ancho degradado.");
        }
        finally
        {
            form?.Dispose();
            LiberarEscenario(previo, ctx, root);
        }
    }

    // Ventana determinística donde Derecha no cabe (ancho < mínimo combinado de
    // Derecha) pero Abajo sí: el guard del cambio de orientación sigue abortando
    // SIN excepción y el form queda consistente y expandido en Abajo.
    [TestMethod]
    public void CambiarPosicion_ConVentanaEstrechaNoCabeDerecha_NoLanzaYQuedaConsistente()
    {
        Proyecto? previo = null;
        FormPresupuesto? form = null;
        SOPROContext? ctx = null;
        DirectorioTemporal root = new DirectorioTemporal("ws_pos_small");
        try
        {
            previo = LeerProyectoGlobalFormato();
            ctx = new SOPROContext(root.DbPath);
            ctx.Database.EnsureCreated();
            Proyecto escenario = CrearProyecto(ctx, "ws_pos_small");

            form = CrearForm(ctx, escenario, root);
            form.WindowState = FormWindowState.Normal;
            form.ClientSize = new System.Drawing.Size(560, 700);
            form.Show();
            System.Windows.Forms.Application.DoEvents();
            form.ExpandirWorkspaceParaPrueba();
            System.Windows.Forms.Application.DoEvents();

            SplitContainer split = form.SplitWorkspace;
            int minimoCombinadoDerecha = split.Panel1MinSize + MinimoDegradadoDerecha(form) + split.SplitterWidth;

            Assert.AreEqual(Orientation.Horizontal, form.SplitWorkspace.Orientation,
                "Precondición: ventana 560x700 abre el workspace en Abajo.");
            Assert.IsTrue(split.Width < minimoCombinadoDerecha,
                $"Precondición determinística: Derecha no cabe en 560 px de ancho " +
                $"(split={split.Width}, mínimo combinado={minimoCombinadoDerecha}).");

            InvocarPrivate(form, "CambiarPosicionWorkspace", PosicionWorkspace.Derecha);

            Assert.AreEqual(PosicionWorkspace.Abajo, form.PosicionWorkspaceActual,
                "Derecha no cabe: el cambio de posición debe abortar a mitad determinística.");
            Assert.AreEqual(Orientation.Horizontal, form.SplitWorkspace.Orientation,
                "La orientación previa debe sobrevivir a un cambio imposible.");
            Assert.IsFalse(form.SplitWorkspace.Panel2Collapsed,
                "El workspace estaba expandido: un aborto no debe colapsarlo.");
        }
        finally
        {
            form?.Dispose();
            LiberarEscenario(previo, ctx, root);
        }
    }

    // Ventana determinística donde Derecha no cabe Y Abajo tampoco (sin superficie
    // viable en ninguna orientación): la apertura deja el workspace colapsado,
    // conserva la posición Derecha y la preferencia persistida sin tocar el archivo.
    [TestMethod]
    public void AbrirWorkspace_SinSuperficieEnAmbasOrientaciones_QuedaColapsadoYConservaPreferencia()
    {
        Proyecto? previo = null;
        FormPresupuesto? form = null;
        SOPROContext? ctx = null;
        DirectorioTemporal root = new DirectorioTemporal("ws_pos_imposible");
        try
        {
            previo = LeerProyectoGlobalFormato();
            ctx = new SOPROContext(root.DbPath);
            ctx.Database.EnsureCreated();
            Proyecto escenario = CrearProyecto(ctx, "ws_pos_imposible");

            ProjectWorkspaceService workspace = new ProjectWorkspaceService(
                Path.Combine(root.Ruta, "sopro"), WorkspaceLocal(root));
            Directory.CreateDirectory(workspace.LocalDataFolder);
            string rutaEstado = Path.Combine(workspace.LocalDataFolder, $"presupuesto_workspace_{escenario.Id}_{FormPresupuesto.NombreArchivoEstadoPorDb(ctx.DatabasePath)}.txt");
            new PresupuestoWorkspaceEstado(PosicionWorkspace.Derecha, 350, 640).Guardar(rutaEstado);

            form = new FormPresupuesto(ctx, escenario, workspace);
            form.WindowState = FormWindowState.Normal;
            form.ClientSize = new System.Drawing.Size(560, 420);
            form.Show();
            System.Windows.Forms.Application.DoEvents();
            Assert.AreEqual(Orientation.Vertical, form.SplitWorkspace.Orientation,
                "La posición Derecha persistida restaura el split vertical antes de abrir.");

            SplitContainer split = form.SplitWorkspace;
            int minimoCombinadoDerecha = split.Panel1MinSize + MinimoDegradadoDerecha(form) + split.SplitterWidth;
            int minimoCombinadoAbajo = split.Panel1MinSize + MinimoDegradadoAbajo(form) + split.SplitterWidth;

            Assert.IsTrue(split.Width < minimoCombinadoDerecha,
                $"Precondición: Derecha no cabe en 560 px de ancho (split={split.Width}, " +
                $"mínimo combinado={minimoCombinadoDerecha}).");
            Assert.IsTrue(split.Height < minimoCombinadoAbajo,
                $"Precondición: Abajo no cabe en una ventana de 420 px de alto " +
                $"(split={split.Height}, mínimo combinado={minimoCombinadoAbajo}).");

            form.ExpandirWorkspaceParaPrueba();
            System.Windows.Forms.Application.DoEvents();

            Assert.IsTrue(form.SplitWorkspace.Panel2Collapsed,
                "Ni Derecha ni Abajo cabe: el workspace permanece colapsado.");
            Assert.AreEqual(PosicionWorkspace.Derecha, form.PosicionWorkspaceActual,
                "Sin superficie viable no se cambia la posición.");
            Assert.AreEqual(ToggleDerechaCerrado, form.TextoBotonToggleMatrices,
                "El botón refleja que el workspace sigue oculto en Derecha.");

            PresupuestoWorkspaceEstado estado = PresupuestoWorkspaceEstado.Leer(rutaEstado);
            Assert.AreEqual(PosicionWorkspace.Derecha, estado.Posicion,
                "La preferencia de posición no debe corromperse.");
            Assert.AreEqual(640, estado.Ancho,
                "La preferencia de ancho debe sobrevivir completa.");
            Assert.AreEqual(350, estado.Alto,
                "La preferencia de alto debe sobrevivir completa.");
        }
        finally
        {
            form?.Dispose();
            LiberarEscenario(previo, ctx, root);
        }
    }

    // Ventana determinística donde Derecha no cabe pero Abajo sí (ventana baja, no
    // estrecha): la apertura degrada a Abajo, abre con la política (alto objetivo por
    // mínimo funcional/proporción) y persiste la caída solo tras el éxito; nada
    // fuerza mínimos de 1px y el piso Panel2MinSize queda reafirmado.
    [TestMethod]
    public void AbrirWorkspace_DerechaNoCabeYAbajoCabe_CaeAbajoYAbreConPolitica()
    {
        Proyecto? previo = null;
        FormPresupuesto? form = null;
        SOPROContext? ctx = null;
        DirectorioTemporal root = new DirectorioTemporal("ws_pos_fallback");
        try
        {
            previo = LeerProyectoGlobalFormato();
            ctx = new SOPROContext(root.DbPath);
            ctx.Database.EnsureCreated();
            Proyecto escenario = CrearProyecto(ctx, "ws_pos_fallback");

            ProjectWorkspaceService workspace = new ProjectWorkspaceService(
                Path.Combine(root.Ruta, "sopro"), WorkspaceLocal(root));
            Directory.CreateDirectory(workspace.LocalDataFolder);
            string rutaEstado = Path.Combine(workspace.LocalDataFolder, $"presupuesto_workspace_{escenario.Id}_{FormPresupuesto.NombreArchivoEstadoPorDb(ctx.DatabasePath)}.txt");
            new PresupuestoWorkspaceEstado(PosicionWorkspace.Derecha, 0, 900).Guardar(rutaEstado);

            form = new FormPresupuesto(ctx, escenario, workspace);
            form.WindowState = FormWindowState.Normal;
            form.ClientSize = new System.Drawing.Size(560, 700);
            form.Show();
            System.Windows.Forms.Application.DoEvents();
            Assert.AreEqual(Orientation.Vertical, form.SplitWorkspace.Orientation,
                "La posición Derecha persistida restaura el split vertical.");

            SplitContainer split = form.SplitWorkspace;
            int minimoCombinadoDerecha = split.Panel1MinSize + MinimoDegradadoDerecha(form) + split.SplitterWidth;
            int minimoCombinadoAbajo = split.Panel1MinSize + MinimoDegradadoAbajo(form) + split.SplitterWidth;
            Assert.IsTrue(split.Width < minimoCombinadoDerecha,
                $"Precondición: Derecha no cabe en 560 px de ancho (split={split.Width}, " +
                $"mínimo combinado={minimoCombinadoDerecha}).");
            Assert.IsTrue(split.Height >= minimoCombinadoAbajo,
                $"Precondición: Abajo sí cabe en una ventana de 700 px de alto " +
                $"(split={split.Height}, mínimo combinado={minimoCombinadoAbajo}).");

            form.ExpandirWorkspaceParaPrueba();
            System.Windows.Forms.Application.DoEvents();

            Assert.AreEqual(PosicionWorkspace.Abajo, form.PosicionWorkspaceActual,
                "Derecha no cabe y Abajo sí: la apertura degrada a Abajo.");
            Assert.AreEqual(Orientation.Horizontal, form.SplitWorkspace.Orientation);
            Assert.IsFalse(form.SplitWorkspace.Panel2Collapsed,
                "El workspace pedido debe abrirse (en Abajo) pese a la ventana estrecha.");
            Assert.AreEqual(ToggleAbajoAbierto, form.TextoBotonToggleMatrices);
            Assert.AreEqual("Posición: Abajo", form.TextoBotonPosicion);

            // Política de alto sin preferencia (Alto=0): mínimo funcional escalado con
            // crecimiento proporcional sobre el alto real disponible del split.
            int factorAltura = 42; // FactorAlturaWorkspace = 0.42
            int objetivoEsperado = Math.Max(
                MinimoFuncionalAbajo(form),
                (int)(split.Height * factorAltura / 100));
            Assert.AreEqual(objetivoEsperado, split.Panel2.Height,
                $"El alto del workspace abierto debe ser el objetivo de la política " +
                $"(minimoFuncional={MinimoFuncionalAbajo(form)}, proporcion={split.Height}x0.42).");
            Assert.AreEqual(MinimoDegradadoAbajo(form), form.SplitWorkspace.Panel2MinSize,
                "El piso de degradación vuelve al mínimo escalado de Abajo; nunca 1px residual.");

            PresupuestoWorkspaceEstado estado = PresupuestoWorkspaceEstado.Leer(rutaEstado);
            Assert.AreEqual(PosicionWorkspace.Abajo, estado.Posicion,
                "La degradación a Abajo se persiste por proyecto.");
            Assert.AreEqual(900, estado.Ancho,
                "La preferencia de ancho no se pierde al degradar (sirve si vuelve a Derecha).");
            Assert.AreEqual(1, form.PanelMatricesInterno!.Parent!.Parent!.Parent!.Controls.Count,
                "El panel de matrices debe seguir albergado una única vez (panelMatrices contiene host).");
        }
        finally
        {
            form?.Dispose();
            LiberarEscenario(previo, ctx, root);
        }
    }

    // La orientación NO resuelve la edición de cabecera: opera el mismo contenedor.
    // Un borrador con valores pendientes sobrevive el cambio y no se pide descartar.
    [TestMethod]
    public void CambiarPosicion_ConBorradorPendiente_ConservaElBorradorYNoDescarta()
    {
        Proyecto? previo = null;
        FormPresupuesto? form = null;
        SOPROContext? ctx = null;
        DirectorioTemporal root = new DirectorioTemporal("ws_pos_guard");
        try
        {
            previo = LeerProyectoGlobalFormato();
            ctx = new SOPROContext(root.DbPath);
            ctx.Database.EnsureCreated();
            Proyecto escenario = CrearProyecto(ctx, "ws_pos_guard");

            form = CrearForm(ctx, escenario, root);
            form.Show();
            System.Windows.Forms.Application.DoEvents();
            form.ExpandirWorkspaceParaPrueba();
            System.Windows.Forms.Application.DoEvents();

            PanelMatricesEmbebido panel = form.PanelMatricesInterno!;
            Func<bool> confirmacionOriginal = PanelMatricesEmbebido.ConfirmarDescartarNuevaMatriz;
            bool invocoDescarte = false;
            PanelMatricesEmbebido.ConfirmarDescartarNuevaMatriz = () =>
            {
                invocoDescarte = true;
                return false;
            };
            try
            {
                FijarCampo(panel, "_modoCreacionMatriz", true);
                FijarCampo(panel, "_modoEdicionCabecera", true);
                Control panelDatos = LeerCampoInstancia<Control>(panel, "_panelDatos")
                    ?? throw new InvalidOperationException("Debe existir el panel de datos de cabecera.");
                panelDatos.Visible = true;
                TextBox txtClave = LeerCampoInstancia<TextBox>(panel, "_txtClaveMatriz")
                    ?? throw new InvalidOperationException("Debe existir la caja de clave del borrador.");
                txtClave.Text = "BORRADOR-POSICION";
                System.Windows.Forms.Application.DoEvents();

                InvocarPrivate(form, "CambiarPosicionWorkspace", PosicionWorkspace.Derecha);
                System.Windows.Forms.Application.DoEvents();

                Assert.AreEqual(PosicionWorkspace.Derecha, form.PosicionWorkspaceActual,
                    "La orientación cambia sin resolver la edición pendiente.");
                Assert.AreEqual(Orientation.Vertical, form.SplitWorkspace.Orientation);
                Assert.IsFalse(invocoDescarte,
                    "El cambio de orientación nunca debe pedir descartar el borrador.");
                Assert.AreEqual("BORRADOR-POSICION", panel.ClaveCabeceraParaPrueba,
                    "El borrador pendiente debe sobrevivir al cambio de posición.");
                Assert.IsTrue(panel.CabeceraDatosVisiblesParaPrueba,
                    "La vista de cabecera debe seguir visible tras el cambio.");
            }
            finally
            {
                PanelMatricesEmbebido.ConfirmarDescartarNuevaMatriz = confirmacionOriginal;
            }
        }
        finally
        {
            form?.Dispose();
            LiberarEscenario(previo, ctx, root);
        }
    }

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

    private static void LiberarEscenario(Proyecto? previo, SOPROContext? ctx, DirectorioTemporal root)
    {
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

    /// <summary>Lee la constante DPI-escalada del piso degradado del modo Derecha (sin hardcodear 320).</summary>
    private static int MinimoDegradadoDerecha(FormPresupuesto form)
    {
        FieldInfo campo = typeof(FormPresupuesto).GetField("AnchoMinimoDegradadoApu",
                BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("Debe existir AnchoMinimoDegradadoApu.");
        MethodInfo? escala = typeof(FormPresupuesto).GetMethod("EscalarPixelesDiseno",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(escala);
        return (int)escala.Invoke(form, new object[] { (int)campo.GetValue(form)! })!;
    }

    /// <summary>Lee la constante DPI-escalada del piso degradado del modo Abajo (sin hardcodear 150).</summary>
    private static int MinimoDegradadoAbajo(FormPresupuesto form)
    {
        FieldInfo campo = typeof(FormPresupuesto).GetField("AltoMinimoDegradadoApu",
                BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("Debe existir AltoMinimoDegradadoApu.");
        MethodInfo? escala = typeof(FormPresupuesto).GetMethod("EscalarPixelesDiseno",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(escala);
        return (int)escala.Invoke(form, new object[] { (int)campo.GetValue(form)! })!;
    }

    /// <summary>Lee la constante DPI-escalada del alto mínimo funcional del modo Abajo (sin hardcodear 250).</summary>
    private static int MinimoFuncionalAbajo(FormPresupuesto form)
    {
        FieldInfo campo = typeof(FormPresupuesto).GetField("AltoMinimoFuncionalApu",
                BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("Debe existir AltoMinimoFuncionalApu.");
        MethodInfo? escala = typeof(FormPresupuesto).GetMethod("EscalarPixelesDiseno",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(escala);
        return (int)escala.Invoke(form, new object[] { (int)campo.GetValue(form)! })!;
    }

    /// <summary>Carpeta de datos locales del workspace temporal inyectado en las pruebas.</summary>
    private static string WorkspaceLocal(DirectorioTemporal root) => Path.Combine(root.Ruta, "local");

    private static T? LeerCampoInstancia<T>(object instancia, string nombre)
        where T : class
        => (T?)instancia.GetType().GetField(nombre, BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(instancia);

    private static void FijarCampo(object instancia, string nombre, object? valor)
    {
        FieldInfo? campo = instancia.GetType().GetField(nombre, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(campo, $"Debe existir el campo {nombre} en {instancia.GetType().Name}.");
        campo!.SetValue(instancia, valor);
    }

    private static T? LeerCampo<T>(FormPresupuesto form, string campo)
        => (T?)typeof(FormPresupuesto).GetField(campo, BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(form);

    private static void GuardarCampo(FormPresupuesto form, string campo, object valor)
    {
        FieldInfo? f = typeof(FormPresupuesto).GetField(campo, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(f, $"Debe existir el campo {campo}.");
        f!.SetValue(form, valor);
    }

    private static Proyecto? LeerProyectoGlobalFormato()
        => (Proyecto?)typeof(FormatoHelper).GetField(CampoProyectoGlobalFormato,
               BindingFlags.Static | BindingFlags.NonPublic)?.GetValue(null);

    private static void RestaurarProyectoGlobalFormato(Proyecto? previo)
        => typeof(FormatoHelper).GetField(CampoProyectoGlobalFormato,
               BindingFlags.Static | BindingFlags.NonPublic)
           ?.SetValue(null, previo);
}
