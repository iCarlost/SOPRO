using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SOPRO.Application.Models.ExternalProjects;
using SOPRO.Application.Models.Presupuesto;
using SOPRO.Application.Services;
using SOPRO.Core.Entities;
using SOPRO.Data.Context;
using SOPRO.WinForms.Controls;
using SOPRO.WinForms.Forms;
using SOPRO.WinForms.Helpers;

namespace SOPRO.Tests.WinForms.Presupuesto;

/// <summary>
/// Flujo del selector APU embebido dentro del workspace del presupuesto:
/// F2 / doble clic en P.U. según el concepto tenga o no matriz asignada, y
/// edición de la matriz seleccionada en la lista (debe abrirse aun cuando no
/// coincide con la matriz asignada al concepto que abrió el selector; las
/// opciones externas siguen bloqueadas). Solo SQLite temporal.
/// </summary>
[STATestClass]
[DoNotParallelize]
public class SelectorApuFlujoPresupuestoTests
{
    private const string CampoProyectoGlobalFormato = "_proyectoActual";

    [TestMethod]
    public void Selector_EditBtn_SeleccionLocalDistintaAAsignada_HabilitaYEdita()
    {
        var previo = LeerProyectoGlobalFormato();
        using var root = new DirectorioTemporal("selector_edit_other");
        SOPROContext? ctx = null;
        FormPresupuesto? form = null;
        try
        {
            ctx = new SOPROContext(root.DbPath);
            ctx.Database.EnsureCreated();
            var escenario = CrearEscenario(ctx);
            form = CrearForm(ctx, escenario, root);

            form.Show();
            form.ExpandirWorkspaceParaPrueba();
            System.Windows.Forms.Application.DoEvents();
            int fila = BuscarFilaPorConcepto(form.GridPrincipal, escenario.Concepto.Id);

            AbrirSelectorEmbebido(form, fila, escenario.MatrizA.Id);

            var selector = LeerSelectorEmbebido(form)!;
            var inner = LeerInnerForm(selector)!;
            var innerDgv = LeerDgvMatrices(inner)!;
            Assert.AreEqual(3, innerDgv.Rows.Count, "El selector debe listar las matrizes APU del proyecto.");

            // El usuario selecciona una matriz DISTINTA a la asignada al concepto.
            SeleccionarFilaPorMatrixId(innerDgv, escenario.MatrizB.Id);
            System.Windows.Forms.Application.DoEvents();

            Assert.IsTrue(selector.BtnEditarItem.Enabled,
                $"CanEditSelectedMatrix={inner.CanEditSelectedMatrix} SelectedId={inner.WorkspaceSelectedMatrixId}. " +
                "Editar debe habilitarse con la matriz local seleccionada en la lista, sea o no la asignada al concepto.");

            // Editar la seleccionada: el host abre el panel embebido sobre esa matriz.
            selector.TriggerEditarMatriz();
            System.Windows.Forms.Application.DoEvents();
            var panel = form.PanelMatricesInterno!;
            Assert.IsTrue(panel.Visible, "Debe mostrarse el panel embebido.");
            Assert.IsNotNull(panel.MatrizVisibleEnPanel, "Debe cargar la matriz editada.");
            Assert.AreEqual(escenario.MatrizB.Id, panel.MatrizVisibleEnPanel.Id,
                "Editar debe abrir la matriz seleccionada en la lista, no otra.");
            Assert.IsNull(LeerSelectorEmbebido(form), "Al editar la superficie vuelve al panel.");
        }
        finally
        {
            form?.Dispose();
            ctx?.Dispose();
            RestaurarProyectoGlobalFormato(previo);
        }
    }

    [TestMethod]
    public void Selector_EditBtn_FilaExterna_SigueDeshabilitado()
    {
        var previo = LeerProyectoGlobalFormato();
        using var root = new DirectorioTemporal("selector_edit_external");
        SOPROContext? ctx = null;
        FormPresupuesto? form = null;
        try
        {
            ctx = new SOPROContext(root.DbPath);
            ctx.Database.EnsureCreated();
            var escenario = CrearEscenario(ctx);
            form = CrearForm(ctx, escenario, root);
            form.Show();
            form.ExpandirWorkspaceParaPrueba();
            System.Windows.Forms.Application.DoEvents();
            int fila = BuscarFilaPorConcepto(form.GridPrincipal, escenario.Concepto.Id);

            AbrirSelectorEmbebido(form, fila, null);
            var selector = LeerSelectorEmbebido(form)!;
            var inner = LeerInnerForm(selector)!;
            var innerDgv = LeerDgvMatrices(inner)!;

            // Opción externa (no pertenece al proyecto local): la protección debe mantenerse.
            int idx = innerDgv.Rows.Add(9999, "🌐 APU", "EXT-1", "Matriz externa", "m2",
                "$1.0000", "Externo", "Otro proyecto", string.Empty);
            innerDgv.Rows[idx].Tag = new ExternalProjectMatrixOption
            {
                MatrixId = 9999,
                ProjectName = "Otro proyecto",
                ProjectPath = Path.Combine(root.Ruta, "otro.soproproj"),
                Clave = "EXT-1",
                Descripcion = "Matriz externa",
                Unidad = "m2",
                CostoDirecto = 1m,
                Tipo = TipoMatriz.APU,
            };
            int visibleIdx = innerDgv.Columns.Cast<DataGridViewColumn>().First(c => c.Visible).Index;
            innerDgv.ClearSelection();
            innerDgv.Rows[idx].Selected = true;
            innerDgv.CurrentCell = innerDgv.Rows[idx].Cells[visibleIdx];
            System.Windows.Forms.Application.DoEvents();

            Assert.IsFalse(selector.BtnEditarItem.Enabled,
                "Una matriz externa (no del proyecto local) no debe abrir edición.");
        }
        finally
        {
            form?.Dispose();
            ctx?.Dispose();
            RestaurarProyectoGlobalFormato(previo);
        }
    }

    [TestMethod]
    public void F2_Pu_ConceptoSinMatriz_AbreSelector()
    {
        var previo = LeerProyectoGlobalFormato();
        using var root = new DirectorioTemporal("f2_sin_matriz");
        SOPROContext? ctx = null;
        FormPresupuesto? form = null;
        try
        {
            ctx = new SOPROContext(root.DbPath);
            ctx.Database.EnsureCreated();
            var escenario = CrearEscenario(ctx);
            form = CrearForm(ctx, escenario, root);
            form.Show();
            form.ExpandirWorkspaceParaPrueba();
            System.Windows.Forms.Application.DoEvents();
            var grid = form.GridPrincipal;
            int fila = BuscarFilaPorConcepto(grid, escenario.ConceptoSinMatriz.Id);

            FijarCeldaActualEnPu(grid, fila);
            DispararF2(form, grid);

            var selector = LeerSelectorEmbebido(form);
            Assert.IsNotNull(selector, "F2 en P.U. de concepto sin matriz debe abrir el selector APU.");
            Assert.IsTrue(selector.Visible, "El selector debe quedar visible.");
            var panel = form.PanelMatricesInterno!;
            Assert.IsFalse(panel.Visible, "El panel embebido no debe quedar a la vista al abrir el selector.");
        }
        finally
        {
            form?.Dispose();
            ctx?.Dispose();
            RestaurarProyectoGlobalFormato(previo);
        }
    }

    [TestMethod]
    public void F2_Pu_ConceptoConMatriz_AbrePanelSinSelector()
    {
        var previo = LeerProyectoGlobalFormato();
        using var root = new DirectorioTemporal("f2_con_matriz");
        SOPROContext? ctx = null;
        FormPresupuesto? form = null;
        try
        {
            ctx = new SOPROContext(root.DbPath);
            ctx.Database.EnsureCreated();
            var escenario = CrearEscenario(ctx);
            form = CrearForm(ctx, escenario, root);
            form.Show();
            form.ExpandirWorkspaceParaPrueba();
            System.Windows.Forms.Application.DoEvents();
            var grid = form.GridPrincipal;
            int fila = BuscarFilaPorConcepto(grid, escenario.Concepto.Id);

            FijarCeldaActualEnPu(grid, fila);
            DispararF2(form, grid);

            Assert.IsNull(LeerSelectorEmbebido(form),
                "Concepto con MatrizId debe abrir el panel directamente, sin selector APU.");
            var panel = form.PanelMatricesInterno!;
            Assert.IsTrue(panel.Visible, "Debe mostrarse el panel embebido.");
            Assert.IsNotNull(panel.MatrizVisibleEnPanel, "La matriz asignada debe cargar en el panel.");
            Assert.AreEqual(escenario.MatrizA.Id, panel.MatrizVisibleEnPanel.Id);
        }
        finally
        {
            form?.Dispose();
            ctx?.Dispose();
            RestaurarProyectoGlobalFormato(previo);
        }
    }

    [TestMethod]
    public void DobleClic_Pu_ConceptoConMatriz_AbrePanelSinSelector()
    {
        var previo = LeerProyectoGlobalFormato();
        using var root = new DirectorioTemporal("dblclick_con");
        SOPROContext? ctx = null;
        FormPresupuesto? form = null;
        try
        {
            ctx = new SOPROContext(root.DbPath);
            ctx.Database.EnsureCreated();
            var escenario = CrearEscenario(ctx);
            form = CrearForm(ctx, escenario, root);
            form.Show();
            form.ExpandirWorkspaceParaPrueba();
            System.Windows.Forms.Application.DoEvents();
            var grid = form.GridPrincipal;
            int fila = BuscarFilaPorConcepto(grid, escenario.Concepto.Id);

            var puCol = ObtenerColumnaPu(grid);
            Assert.IsNotNull(puCol, "El grid del presupuesto debe tener la columna P.U.");
            grid.CurrentCell = grid.Rows[fila].Cells[puCol!.Index];

            InvocarManejadorDobleClic(form, grid, puCol.Index, fila);

            Assert.IsNull(LeerSelectorEmbebido(form),
                "Doble clic sobre P.U. de concepto con MatrizId debe abrir el panel, sin selector.");
            var panel = form.PanelMatricesInterno!;
            Assert.IsTrue(panel.Visible, "Debe mostrarse el panel embebido.");
            Assert.IsNotNull(panel.MatrizVisibleEnPanel);
            Assert.AreEqual(escenario.MatrizA.Id, panel.MatrizVisibleEnPanel.Id);
        }
        finally
        {
            form?.Dispose();
            ctx?.Dispose();
            RestaurarProyectoGlobalFormato(previo);
        }
    }

    [TestMethod]
    public void DobleClic_Pu_ConceptoSinMatriz_AbreSelector()
    {
        var previo = LeerProyectoGlobalFormato();
        using var root = new DirectorioTemporal("dblclick_sin");
        SOPROContext? ctx = null;
        FormPresupuesto? form = null;
        try
        {
            ctx = new SOPROContext(root.DbPath);
            ctx.Database.EnsureCreated();
            var escenario = CrearEscenario(ctx);
            form = CrearForm(ctx, escenario, root);
            form.Show();
            form.ExpandirWorkspaceParaPrueba();
            System.Windows.Forms.Application.DoEvents();
            var grid = form.GridPrincipal;
            int fila = BuscarFilaPorConcepto(grid, escenario.ConceptoSinMatriz.Id);

            var puCol = ObtenerColumnaPu(grid);
            Assert.IsNotNull(puCol, "El grid del presupuesto debe tener la columna P.U.");
            grid.CurrentCell = grid.Rows[fila].Cells[puCol!.Index];

            InvocarManejadorDobleClic(form, grid, puCol.Index, fila);

            var selector = LeerSelectorEmbebido(form);
            Assert.IsNotNull(selector, "Doble clic sobre P.U. de concepto sin matriz debe abrir el selector.");
            Assert.IsTrue(selector.Visible);
        }
        finally
        {
            form?.Dispose();
            ctx?.Dispose();
            RestaurarProyectoGlobalFormato(previo);
        }
    }

    [TestMethod]
    public void F2Real_Pu_ConceptoConMatriz_AbrePanelSinSelector()
    {
        var previo = LeerProyectoGlobalFormato();
        using var root = new DirectorioTemporal("f2real_con");
        SOPROContext? ctx = null;
        FormPresupuesto? form = null;
        try
        {
            ctx = new SOPROContext(root.DbPath);
            ctx.Database.EnsureCreated();
            var escenario = CrearEscenario(ctx);
            form = CrearForm(ctx, escenario, root);
            form.Show();
            form.ExpandirWorkspaceParaPrueba();
            System.Windows.Forms.Application.DoEvents();
            var grid = form.GridPrincipal;
            int fila = BuscarFilaPorConcepto(grid, escenario.Concepto.Id);

            FijarCeldaActualEnPu(grid, fila);
            grid.Focus();
            form.Activate();
            System.Windows.Forms.Application.DoEvents();
            SendKeys.SendWait("{F2}");
            System.Windows.Forms.Application.DoEvents();

            Assert.IsNull(LeerSelectorEmbebido(form),
                "F2 real sobre P.U. de concepto con MatrizId debe abrir el panel, sin selector.");
            var panel = form.PanelMatricesInterno!;
            Assert.IsTrue(panel.Visible, "Debe mostrarse el panel embebido.");
            Assert.IsNotNull(panel.MatrizVisibleEnPanel);
            Assert.AreEqual(escenario.MatrizA.Id, panel.MatrizVisibleEnPanel.Id);
        }
        finally
        {
            form?.Dispose();
            ctx?.Dispose();
            RestaurarProyectoGlobalFormato(previo);
        }
    }

    [TestMethod]
    public void F2Real_Pu_ConceptoSinMatriz_AbreSelector()
    {
        var previo = LeerProyectoGlobalFormato();
        using var root = new DirectorioTemporal("f2real_sin");
        SOPROContext? ctx = null;
        FormPresupuesto? form = null;
        try
        {
            ctx = new SOPROContext(root.DbPath);
            ctx.Database.EnsureCreated();
            var escenario = CrearEscenario(ctx);
            form = CrearForm(ctx, escenario, root);
            form.Show();
            form.ExpandirWorkspaceParaPrueba();
            System.Windows.Forms.Application.DoEvents();
            var grid = form.GridPrincipal;
            int fila = BuscarFilaPorConcepto(grid, escenario.ConceptoSinMatriz.Id);

            FijarCeldaActualEnPu(grid, fila);
            grid.Focus();
            form.Activate();
            System.Windows.Forms.Application.DoEvents();
            SendKeys.SendWait("{F2}");
            System.Windows.Forms.Application.DoEvents();

            Assert.IsNotNull(LeerSelectorEmbebido(form),
                "F2 real sobre P.U. de concepto sin matriz debe abrir el selector APU.");
        }
        finally
        {
            form?.Dispose();
            ctx?.Dispose();
            RestaurarProyectoGlobalFormato(previo);
        }
    }


    [TestMethod]
    public void Selector_EditBtn_ConFiltroInicial_SeleccionLocalHabilitaYEdita()
    {
        var previo = LeerProyectoGlobalFormato();
        using var root = new DirectorioTemporal("selector_edit_filtro");
        SOPROContext? ctx = null;
        FormPresupuesto? form = null;
        try
        {
            ctx = new SOPROContext(root.DbPath);
            ctx.Database.EnsureCreated();
            var escenario = CrearEscenario(ctx);
            form = CrearForm(ctx, escenario, root);
            form.Show();
            form.ExpandirWorkspaceParaPrueba();
            System.Windows.Forms.Application.DoEvents();
            int fila = BuscarFilaPorConcepto(form.GridPrincipal, escenario.ConceptoSinMatriz.Id);

            // Ruta real del concepto sin matriz: filtro inicial = descripción.
            var mi = typeof(FormPresupuesto).GetMethod("MostrarSelectorApuEmbebido",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(mi);
            mi!.Invoke(form, new object?[] { fila, 1m, null, escenario.ConceptoSinMatriz.Descripcion });
            System.Windows.Forms.Application.DoEvents();

            var selector = LeerSelectorEmbebido(form)!;
            var inner = LeerInnerForm(selector)!;
            var innerDgv = LeerDgvMatrices(inner)!;
            Assert.IsTrue(innerDgv.Rows.Count > 0, "La búsqueda transversal debe listar coincidencias.");

            SeleccionarFilaPorMatrixId(innerDgv, escenario.MatrizB.Id);
            System.Windows.Forms.Application.DoEvents();

            Assert.IsTrue(selector.BtnEditarItem.Enabled,
                "Con una matriz local seleccionada en la búsqueda, Editar debe habilitarse.");
            selector.TriggerEditarMatriz();
            System.Windows.Forms.Application.DoEvents();
            var panel = form.PanelMatricesInterno!;
            Assert.IsTrue(panel.Visible);
            Assert.IsNotNull(panel.MatrizVisibleEnPanel);
            Assert.AreEqual(escenario.MatrizB.Id, panel.MatrizVisibleEnPanel.Id,
                "Editar debe abrir la matriz local seleccionada, no la del filtro ni otra.");
        }
        finally
        {
            form?.Dispose();
            ctx?.Dispose();
            RestaurarProyectoGlobalFormato(previo);
        }
    }

    [TestMethod]
    public void NuevaMatriz_DesdeSelector_ConCantidadCeroPersistida_UsaCantidadVisibleYAsigna()
    {
        var previo = LeerProyectoGlobalFormato();
        var avisoPrevio = FormPresupuesto.MostrarAvisoAsignacionMatriz;
        var avisos = new List<string>();
        FormPresupuesto.MostrarAvisoAsignacionMatriz = (_, mensaje, _) => avisos.Add(mensaje);

        using var root = new DirectorioTemporal("selector_nueva_cantidad");
        SOPROContext? ctx = null;
        FormPresupuesto? form = null;
        try
        {
            ctx = new SOPROContext(root.DbPath);
            ctx.Database.EnsureCreated();
            var escenario = CrearEscenario(ctx);
            form = CrearForm(ctx, escenario, root);
            form.Show();
            form.ExpandirWorkspaceParaPrueba();
            System.Windows.Forms.Application.DoEvents();

            var grid = form.GridPrincipal;
            int fila = BuscarFilaPorConcepto(grid, escenario.ConceptoCantidadCero.Id);
            Assert.IsTrue(fila >= 0, "La fila persistida con Cantidad 0 debe estar en el grid.");

            AbrirSelectorDesdeFila(form, fila);

            var selector = LeerSelectorEmbebido(form);
            Assert.IsNotNull(selector, "La fila sin matriz debe abrir el selector APU.");
            var inner = LeerInnerForm(selector!)!;
            var nud = LeerNudCantidad(inner)!;
            Assert.IsTrue(nud.Value > 0m,
                "La cantidad visible del selector debe ser positiva aunque la fila persistida tenga 0.");
            Assert.AreEqual(0.01m, selector!.CantidadParaNuevaMatriz,
                "Con cantidad persistida 0 el selector debe arrancar en su mínimo positivo (0.01).");

            // El usuario corrige la cantidad desde el resumen del selector.
            nud.Value = 3m;
            Assert.AreEqual(3m, selector.CantidadParaNuevaMatriz,
                "Nueva Matriz debe arrastrar la cantidad VISIBLE, no la leída al abrir la fila.");

            selector.TriggerNuevaMatriz();
            System.Windows.Forms.Application.DoEvents();

            Assert.IsNull(LeerSelectorEmbebido(form), "Al crear matriz la superficie pasa al panel.");
            var panel = form.PanelMatricesInterno!;
            Assert.IsTrue(panel.Visible, "El panel de captura debe quedar visible.");

            var matrizNueva = AgregarMatrizNueva(
                ctx, escenario.Proyecto.Id, "APU-N1", "Matriz nueva desde selector", "m2", 60m);

            LeerCallbackMatrizGuardada(panel)(matrizNueva);
            System.Windows.Forms.Application.DoEvents();

            Assert.AreEqual(0, avisos.Count,
                "La asignación no debe avisar de fallo: " + string.Join(" | ", avisos));
            Assert.AreEqual(BudgetOperationStatus.Committed, form.UltimoEstadoAsignacionMatriz);

            using (var verificacion = new SOPROContext(root.DbPath))
            {
                var persistido = verificacion.ConceptosPresupuesto
                    .AsNoTracking()
                    .Single(c => c.Id == escenario.ConceptoCantidadCero.Id);
                Assert.AreEqual(matrizNueva.Id, persistido.MatrizId, "La matriz debe quedar persistida.");
                Assert.AreEqual(3m, persistido.Cantidad, "Debe persistirse la cantidad editada en el selector.");
                Assert.AreEqual(60m, persistido.PrecioUnitario);
                Assert.AreEqual(180m, persistido.ImporteTotal);
            }

            Assert.AreEqual(matrizNueva.Id, escenario.ConceptoCantidadCero.MatrizId);
            Assert.AreEqual(3m, Convert.ToDecimal(BuscarCelda(grid, fila, "Cantidad")!.Value));
            StringAssert.Contains(
                BuscarCelda(grid, fila, "PrecioUnitario")!.Value?.ToString() ?? string.Empty, "60");
            StringAssert.Contains(
                BuscarCelda(grid, fila, "Importe")!.Value?.ToString() ?? string.Empty, "180");

            // F2 sobre P.U. ahora debe abrir el panel con la matriz recién asignada.
            FijarCeldaActualEnPu(grid, fila);
            DispararF2(form, grid);
            Assert.IsNull(LeerSelectorEmbebido(form));
            Assert.IsTrue(panel.Visible);
            Assert.IsNotNull(panel.MatrizVisibleEnPanel);
            Assert.AreEqual(matrizNueva.Id, panel.MatrizVisibleEnPanel.Id);
        }
        finally
        {
            FormPresupuesto.MostrarAvisoAsignacionMatriz = avisoPrevio;
            form?.Dispose();
            ctx?.Dispose();
            RestaurarProyectoGlobalFormato(previo);
        }
    }

    [TestMethod]
    public void NuevaMatriz_DesdePanel_ConCantidadCeroPersistida_UsaDefaultPositivoYAsigna()
    {
        var previo = LeerProyectoGlobalFormato();
        var avisoPrevio = FormPresupuesto.MostrarAvisoAsignacionMatriz;
        var avisos = new List<string>();
        FormPresupuesto.MostrarAvisoAsignacionMatriz = (_, mensaje, _) => avisos.Add(mensaje);

        using var root = new DirectorioTemporal("panel_nueva_cantidad");
        SOPROContext? ctx = null;
        FormPresupuesto? form = null;
        try
        {
            ctx = new SOPROContext(root.DbPath);
            ctx.Database.EnsureCreated();
            var escenario = CrearEscenario(ctx);
            form = CrearForm(ctx, escenario, root);
            form.Show();
            form.ExpandirWorkspaceParaPrueba();
            System.Windows.Forms.Application.DoEvents();

            var grid = form.GridPrincipal;
            var panel = form.PanelMatricesInterno!;
            int fila = BuscarFilaPorConcepto(grid, escenario.ConceptoCantidadCero.Id);
            Assert.IsTrue(fila >= 0);

            grid.CurrentCell = BuscarCelda(grid, fila, "Descripcion");
            panel.NotificarFilaCambiada(fila);

            InvocarIniciarCreacionMatrizDesdePanel(form);
            System.Windows.Forms.Application.DoEvents();
            Assert.IsTrue(panel.Visible, "La captura de matriz nueva debe mostrarse en el panel.");

            var matrizNueva = AgregarMatrizNueva(
                ctx, escenario.Proyecto.Id, "APU-N2", "Matriz nueva desde panel", "m2", 60m);

            LeerCallbackMatrizGuardada(panel)(matrizNueva);
            System.Windows.Forms.Application.DoEvents();

            Assert.AreEqual(0, avisos.Count,
                "La asignación no debe avisar de fallo: " + string.Join(" | ", avisos));
            Assert.AreEqual(BudgetOperationStatus.Committed, form.UltimoEstadoAsignacionMatriz);

            using (var verificacion = new SOPROContext(root.DbPath))
            {
                var persistido = verificacion.ConceptosPresupuesto
                    .AsNoTracking()
                    .Single(c => c.Id == escenario.ConceptoCantidadCero.Id);
                Assert.AreEqual(matrizNueva.Id, persistido.MatrizId);
                Assert.AreEqual(1m, persistido.Cantidad,
                    "Sin selector y con cantidad 0 debe usarse el default positivo 1.0.");
                Assert.AreEqual(60m, persistido.PrecioUnitario);
                Assert.AreEqual(60m, persistido.ImporteTotal);
            }

            Assert.AreEqual(1m, Convert.ToDecimal(BuscarCelda(grid, fila, "Cantidad")!.Value));

            FijarCeldaActualEnPu(grid, fila);
            DispararF2(form, grid);
            Assert.IsNull(LeerSelectorEmbebido(form));
            Assert.IsTrue(panel.Visible);
            Assert.IsNotNull(panel.MatrizVisibleEnPanel);
            Assert.AreEqual(matrizNueva.Id, panel.MatrizVisibleEnPanel.Id);
        }
        finally
        {
            FormPresupuesto.MostrarAvisoAsignacionMatriz = avisoPrevio;
            form?.Dispose();
            ctx?.Dispose();
            RestaurarProyectoGlobalFormato(previo);
        }
    }

    // ═════════════════════════════ Infraestructura ═════════════════════════════

    [TestMethod]
    public void Asignacion_MatrizNoApu_SeRechazaSinAsignarNiPintarBorrador()
    {
        var previo = LeerProyectoGlobalFormato();

        using var root = new DirectorioTemporal("asignacion_no_apu");
        SOPROContext? ctx = null;
        FormPresupuesto? form = null;
        try
        {
            ctx = new SOPROContext(root.DbPath);
            ctx.Database.EnsureCreated();
            var escenario = CrearEscenario(ctx);
            form = CrearForm(ctx, escenario, root);

            var matrizBasica = new Matriz
            {
                ProyectoId = escenario.Proyecto.Id,
                Clave = "BAS-01",
                Descripcion = "Matriz básica no asignable",
                Unidad = "m3",
                Tipo = TipoMatriz.Basico,
                CostoDirecto = 10m,
                Notas = string.Empty
            };
            ctx.Matrices.Add(matrizBasica);
            ctx.SaveChanges();

            var grid = form.GridPrincipal;
            int fila = BuscarFilaPorConcepto(grid, escenario.Concepto.Id);
            Assert.IsTrue(fila >= 0);

            decimal cantidadVisible = Convert.ToDecimal(BuscarCelda(grid, fila, "Cantidad")!.Value);

            Assert.IsFalse(form.TryApplySelectedMatrixAssignment(
                    fila, matrizBasica, 3m, preserveCurrentTexts: true),
                "Una matriz no APU no puede reportarse como asignada.");

            Assert.IsNull(form.UltimoEstadoAsignacionMatriz,
                "El rechazo ocurre antes de enviar el comando.");
            StringAssert.Contains(form.UltimoDiagnosticoAsignacionMatriz ?? string.Empty, "APU",
                "El diagnóstico debe explicar que solo se admite APU.");

            Assert.AreEqual(cantidadVisible,
                Convert.ToDecimal(BuscarCelda(grid, fila, "Cantidad")!.Value),
                "La fila conserva sus valores visibles originales.");

            var tag = (ConceptoPresupuesto)grid.Rows[fila].Tag!;
            Assert.AreEqual(escenario.MatrizA.Id, tag.MatrizId,
                "El Tag compartido no se muta en un rechazo.");

            using var verificacion = new SOPROContext(root.DbPath);
            Assert.AreEqual(escenario.MatrizA.Id,
                verificacion.ConceptosPresupuesto.AsNoTracking()
                    .Single(c => c.Id == escenario.Concepto.Id).MatrizId,
                "El rechazo no escribe ninguna asignación en la base.");
        }
        finally
        {
            form?.Dispose();
            ctx?.Dispose();
            RestaurarProyectoGlobalFormato(previo);
        }
    }

    [TestMethod]
    public void Asignacion_ComandoNoEnviadoPorFalloDeConstruccion_NoAplicaBorradorNiAfirmaExito()
    {
        var previo = LeerProyectoGlobalFormato();
        var avisoPrevio = FormPresupuesto.MostrarAvisoAsignacionMatriz;
        var avisos = new List<string>();
        FormPresupuesto.MostrarAvisoAsignacionMatriz = (_, mensaje, _) => avisos.Add(mensaje);

        using var root = new DirectorioTemporal("asignacion_not_sent");
        SOPROContext? ctx = null;
        FormPresupuesto? form = null;
        try
        {
            ctx = new SOPROContext(root.DbPath);
            ctx.Database.EnsureCreated();
            var escenario = CrearEscenario(ctx);
            form = CrearForm(ctx, escenario, root);

            var grid = form.GridPrincipal;
            int fila = BuscarFilaPorConcepto(grid, escenario.Concepto.Id);
            Assert.IsTrue(fila >= 0);

            decimal cantidadVisible = Convert.ToDecimal(BuscarCelda(grid, fila, "Cantidad")!.Value);
            string claveVisible = BuscarCelda(grid, fila, "Clave")!.Value?.ToString() ?? string.Empty;

            // Cantidad 0 hace que la construcción del comando lance: el intento no se
            // envía y NO puede pintarse como si se hubiera asignado.
            Assert.IsFalse(form.TryApplySelectedMatrixAssignment(
                    fila, escenario.MatrizB, 0m, preserveCurrentTexts: true),
                "Un comando que no se construye no se reporta como asignación aplicada.");

            Assert.AreEqual(BudgetOperationStatus.Failed, form.UltimoEstadoAsignacionMatriz,
                "El fallo local se clasifica como fallo, no como commit.");
            Assert.IsNotNull(form.UltimoDiagnosticoAsignacionMatriz);
            Assert.AreEqual(1, avisos.Count,
                "El usuario debe recibir un aviso inequívoco del intento no enviado.");

            Assert.AreEqual(claveVisible, BuscarCelda(grid, fila, "Clave")!.Value?.ToString(),
                "La clave visible original se conserva.");
            Assert.AreEqual(cantidadVisible,
                Convert.ToDecimal(BuscarCelda(grid, fila, "Cantidad")!.Value),
                "La cantidad visible original se conserva (no se pinta el borrador).");

            var tag = (ConceptoPresupuesto)grid.Rows[fila].Tag!;
            Assert.AreEqual(escenario.MatrizA.Id, tag.MatrizId);

            using var verificacion = new SOPROContext(root.DbPath);
            Assert.AreEqual(escenario.MatrizA.Id,
                verificacion.ConceptosPresupuesto.AsNoTracking()
                    .Single(c => c.Id == escenario.Concepto.Id).MatrizId,
                "Un intento no enviado no escribe en la base.");
        }
        finally
        {
            FormPresupuesto.MostrarAvisoAsignacionMatriz = avisoPrevio;
            form?.Dispose();
            ctx?.Dispose();
            RestaurarProyectoGlobalFormato(previo);
        }
    }

    [TestMethod]
    public void Asignacion_ApuConfirmada_AplicaValoresVisiblesYPersiste()
    {
        var previo = LeerProyectoGlobalFormato();

        using var root = new DirectorioTemporal("asignacion_ok");
        SOPROContext? ctx = null;
        FormPresupuesto? form = null;
        try
        {
            ctx = new SOPROContext(root.DbPath);
            ctx.Database.EnsureCreated();
            var escenario = CrearEscenario(ctx);
            form = CrearForm(ctx, escenario, root);

            var grid = form.GridPrincipal;
            int fila = BuscarFilaPorConcepto(grid, escenario.Concepto.Id);
            Assert.IsTrue(fila >= 0);

            Assert.IsTrue(form.TryApplySelectedMatrixAssignment(
                    fila, escenario.MatrizB, 4m, preserveCurrentTexts: true),
                "Una asignación APU confirmada sí se aplica.");

            Assert.AreEqual(BudgetOperationStatus.Committed, form.UltimoEstadoAsignacionMatriz);
            Assert.IsNull(form.UltimoDiagnosticoAsignacionMatriz);

            Assert.AreEqual(4m, Convert.ToDecimal(BuscarCelda(grid, fila, "Cantidad")!.Value),
                "La cantidad asignada queda visible.");

            var tag = (ConceptoPresupuesto)grid.Rows[fila].Tag!;
            Assert.AreEqual(escenario.MatrizB.Id, tag.MatrizId);
            Assert.AreEqual(4m, tag.Cantidad);

            using var verificacion = new SOPROContext(root.DbPath);
            var persistido = verificacion.ConceptosPresupuesto.AsNoTracking()
                .Single(c => c.Id == escenario.Concepto.Id);
            Assert.AreEqual(escenario.MatrizB.Id, persistido.MatrizId);
            Assert.AreEqual(4m, persistido.Cantidad);
        }
        finally
        {
            form?.Dispose();
            ctx?.Dispose();
            RestaurarProyectoGlobalFormato(previo);
        }
    }

    [TestMethod]
    public void NuevaMatriz_DesdePanel_TipoNoApu_NoAsignaNiAparentaExito()
    {
        var previo = LeerProyectoGlobalFormato();
        var avisoPrevio = FormPresupuesto.MostrarAvisoAsignacionMatriz;
        var avisos = new List<string>();
        FormPresupuesto.MostrarAvisoAsignacionMatriz = (_, mensaje, _) => avisos.Add(mensaje);

        using var root = new DirectorioTemporal("panel_nueva_no_apu");
        SOPROContext? ctx = null;
        FormPresupuesto? form = null;
        try
        {
            ctx = new SOPROContext(root.DbPath);
            ctx.Database.EnsureCreated();
            var escenario = CrearEscenario(ctx);
            form = CrearForm(ctx, escenario, root);
            form.Show();
            form.ExpandirWorkspaceParaPrueba();
            System.Windows.Forms.Application.DoEvents();

            var grid = form.GridPrincipal;
            var panel = form.PanelMatricesInterno!;
            int fila = BuscarFilaPorConcepto(grid, escenario.ConceptoSinMatriz.Id);
            Assert.IsTrue(fila >= 0);

            grid.CurrentCell = BuscarCelda(grid, fila, "Descripcion");
            panel.NotificarFilaCambiada(fila);

            InvocarIniciarCreacionMatrizDesdePanel(form);
            System.Windows.Forms.Application.DoEvents();
            Assert.IsTrue(panel.Visible, "La captura de matriz nueva debe mostrarse en el panel.");

            var matrizBasica = new Matriz
            {
                ProyectoId = escenario.Proyecto.Id,
                Clave = "BAS-PANEL",
                Descripcion = "Básico creado desde el panel",
                Unidad = "m3",
                Tipo = TipoMatriz.Basico,
                CostoDirecto = 20m,
                Notas = string.Empty
            };
            ctx.Matrices.Add(matrizBasica);
            ctx.SaveChanges();

            LeerCallbackMatrizGuardada(panel)(matrizBasica);
            System.Windows.Forms.Application.DoEvents();

            Assert.AreEqual(1, avisos.Count,
                "El intento con tipo no soportado debe avisarse: " + string.Join(" | ", avisos));
            Assert.IsNull(form.UltimoEstadoAsignacionMatriz,
                "La matriz no APU no debe llegar a la asignación.");

            using (var verificacion = new SOPROContext(root.DbPath))
            {
                Assert.IsNull(verificacion.ConceptosPresupuesto.AsNoTracking()
                        .Single(c => c.Id == escenario.ConceptoSinMatriz.Id).MatrizId,
                    "La matriz no APU no se asigna a la fila del presupuesto.");
            }

            Assert.IsNull((grid.Rows[fila].Tag as ConceptoPresupuesto)?.MatrizId,
                "La fila conserva su Tag sin matriz.");
        }
        finally
        {
            FormPresupuesto.MostrarAvisoAsignacionMatriz = avisoPrevio;
            form?.Dispose();
            ctx?.Dispose();
            RestaurarProyectoGlobalFormato(previo);
        }
    }

    private static FormPresupuesto CrearForm(SOPROContext ctx, Escenario escenario, DirectorioTemporal root)
        => new(ctx, escenario.Proyecto, new ProjectWorkspaceService(
            Path.Combine(root.Ruta, "sopro"), Path.Combine(root.Ruta, "local")));

    private static void AbrirSelectorEmbebido(FormPresupuesto form, int fila, int? matrizIdActual)
    {
        var mi = typeof(FormPresupuesto).GetMethod("MostrarSelectorApuEmbebido",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(mi, "Debe existir MostrarSelectorApuEmbebido.");
        mi!.Invoke(form, new object?[] { fila, 1m, matrizIdActual, null });
            System.Windows.Forms.Application.DoEvents();
    }

    private static void AbrirSelectorDesdeFila(FormPresupuesto form, int fila)
    {
        var mi = typeof(FormPresupuesto).GetMethod("OpenApuSelectorForRow",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(mi, "Debe existir OpenApuSelectorForRow.");
        mi!.Invoke(form, new object[] { fila });
        System.Windows.Forms.Application.DoEvents();
    }

    private static void InvocarIniciarCreacionMatrizDesdePanel(FormPresupuesto form)
    {
        var mi = typeof(FormPresupuesto).GetMethod("IniciarCreacionMatrizDesdePanel",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(mi, "Debe existir IniciarCreacionMatrizDesdePanel.");
        mi!.Invoke(form, null);
    }

    private static NumericUpDown? LeerNudCantidad(FormSeleccionarAPU inner)
        => (NumericUpDown?)inner.GetType()
            .GetField("nudCantidad", BindingFlags.Instance | BindingFlags.NonPublic)
            ?.GetValue(inner);

    private static Action<Matriz> LeerCallbackMatrizGuardada(PanelMatricesEmbebido panel)
        => (Action<Matriz>?)typeof(PanelMatricesEmbebido)
               .GetField("_onMatrixHeaderSaved", BindingFlags.Instance | BindingFlags.NonPublic)
               ?.GetValue(panel)
           ?? throw new InvalidOperationException(
               "El panel no expone el callback de matriz guardada tras BeginCreateMatrix.");

    private static Matriz AgregarMatrizNueva(
        SOPROContext ctx,
        int proyectoId,
        string clave,
        string descripcion,
        string unidad,
        decimal costoDirecto)
    {
        var matriz = new Matriz
        {
            ProyectoId = proyectoId,
            Clave = clave,
            Descripcion = descripcion,
            Unidad = unidad,
            Tipo = TipoMatriz.APU,
            CostoDirecto = costoDirecto,
            Notas = string.Empty,
        };
        ctx.Matrices.Add(matriz);
        ctx.SaveChanges();
        return matriz;
    }

    private static SelectorApuEmbebidoControl? LeerSelectorEmbebido(FormPresupuesto form)
        => (SelectorApuEmbebidoControl?)typeof(FormPresupuesto)
            .GetField("_selectorApuEmbebido", BindingFlags.Instance | BindingFlags.NonPublic)
            ?.GetValue(form);

    private static FormSeleccionarAPU? LeerInnerForm(SelectorApuEmbebidoControl selector)
        => (FormSeleccionarAPU?)typeof(SelectorApuEmbebidoControl)
            .GetField("_innerForm", BindingFlags.Instance | BindingFlags.NonPublic)
            ?.GetValue(selector);

    private static DataGridView? LeerDgvMatrices(FormSeleccionarAPU inner)
        => (DataGridView?)inner.GetType()
            .GetField("dgvMatrices", BindingFlags.Instance | BindingFlags.NonPublic)
            ?.GetValue(inner);

    private static void SeleccionarFilaPorMatrixId(DataGridView dgv, int matrizId)
    {
        foreach (DataGridViewRow row in dgv.Rows)
        {
            if (Convert.ToInt32(row.Cells["colId"].Value) != matrizId) continue;
            dgv.ClearSelection();
            row.Selected = true;
            var visibleCell = row.Cells.Cast<DataGridViewCell>()
                .FirstOrDefault(c => c.Visible && c.OwningColumn.Visible)
                ?? row.Cells[0];
            dgv.CurrentCell = visibleCell;
            return;
        }
        Assert.Fail($"No se encontró la fila de la matriz {matrizId} en el selector.");
    }

    private static void FijarCeldaActualEnPu(DataGridView grid, int fila)
    {
        var puCol = ObtenerColumnaPu(grid);
        Assert.IsNotNull(puCol, "El grid del presupuesto debe tener la columna P.U.");
        grid.CurrentCell = grid.Rows[fila].Cells[puCol!.Index];
    }

    private static DataGridViewCell? BuscarCelda(DataGridView grid, int rowIndex, string nombreInterno)
    {
        if (rowIndex < 0 || rowIndex >= grid.Rows.Count) return null;

        foreach (DataGridViewColumn col in grid.Columns)
        {
            if (col.Tag is ColumnaPersonalizada definicion
                && string.Equals(definicion.NombreInterno, nombreInterno, StringComparison.OrdinalIgnoreCase))
            {
                return grid.Rows[rowIndex].Cells[col.Index];
            }
        }

        return null;
    }

    private static void DispararF2(FormPresupuesto form, DataGridView grid)
    {
        var mi = typeof(FormPresupuesto).GetMethod("DgvPresupuesto_KeyDown",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(mi, "Debe existir el manejador KeyDown del grid.");
        var keyEventArgs = new KeyEventArgs(Keys.F2);
        mi!.Invoke(form, new object[] { grid, keyEventArgs });
            System.Windows.Forms.Application.DoEvents();
    }

    private static void InvocarManejadorDobleClic(FormPresupuesto form, DataGridView grid, int columna, int fila)
    {
        var mi = typeof(FormPresupuesto).GetMethod("DgvPresupuesto_CellDoubleClick",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(mi, "Debe existir el manejador de doble clic del grid.");
        mi!.Invoke(form, new object[] { grid, new DataGridViewCellEventArgs(columna, fila) });
            System.Windows.Forms.Application.DoEvents();
    }

    private static DataGridViewColumn? ObtenerColumnaPu(DataGridView grid)
    {
        foreach (DataGridViewColumn col in grid.Columns)
        {
            if (col.Tag is ColumnaPersonalizada def
                && string.Equals(def.NombreInterno, "PrecioUnitario", StringComparison.OrdinalIgnoreCase))
                return col;
        }
        return null;
    }

    private sealed class Escenario
    {
        public Proyecto Proyecto { get; init; } = null!;
        public Matriz MatrizA { get; init; } = null!;
        public Matriz MatrizB { get; init; } = null!;
        public Matriz MatrizC { get; init; } = null!;
        public ConceptoPresupuesto Agrupador { get; init; } = null!;
        public ConceptoPresupuesto Concepto { get; init; } = null!;
        public ConceptoPresupuesto ConceptoSinMatriz { get; init; } = null!;
        public ConceptoPresupuesto ConceptoCantidadCero { get; init; } = null!;
    }

    private static Escenario CrearEscenario(SOPROContext ctx)
    {
        var proyecto = new Proyecto
        {
            Nombre = "Proyecto flujo selector APU",
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

        var matrizA = new Matriz { ProyectoId = proyecto.Id, Clave = "APU-A", Descripcion = "Matriz A", Unidad = "m3", Tipo = TipoMatriz.APU, CostoDirecto = 100m, Notas = string.Empty };
        var matrizB = new Matriz { ProyectoId = proyecto.Id, Clave = "APU-B", Descripcion = "Matriz B", Unidad = "kg", Tipo = TipoMatriz.APU, CostoDirecto = 50m, Notas = string.Empty };
        var matrizC = new Matriz { ProyectoId = proyecto.Id, Clave = "APU-C", Descripcion = "Matriz C", Unidad = "m2", Tipo = TipoMatriz.APU, CostoDirecto = 25m, Notas = string.Empty };
        ctx.Matrices.AddRange(matrizA, matrizB, matrizC);
        ctx.SaveChanges();

        var agrupador = new ConceptoPresupuesto
        {
            ProyectoId = proyecto.Id,
            Clave = "1",
            Descripcion = "Capítulo 1",
            EsAgrupador = true,
            Nivel = 0,
            Orden = 0,
            Unidad = string.Empty,
            ColumnasPersonalizadasJSON = string.Empty,
            Notas = string.Empty,
            Cantidad = 0m,
            CostoDirectoUnitario = 0m,
            CostoDirectoTotal = 0m,
            PrecioUnitario = 0m,
            ImporteTotal = 0m,
            FechaModificacion = new DateTime(2026, 1, 1),
        };
        var concepto = new ConceptoPresupuesto
        {
            ProyectoId = proyecto.Id,
            Clave = "1.1",
            Descripcion = "Concreto predosificado",
            EsAgrupador = false,
            Nivel = 1,
            Orden = 1,
            Unidad = "m3",
            ColumnasPersonalizadasJSON = string.Empty,
            Notas = string.Empty,
            Cantidad = 10m,
            MatrizId = matrizA.Id,
            CostoDirectoUnitario = 100m,
            CostoDirectoTotal = 1000m,
            PrecioUnitario = 100m,
            ImporteTotal = 1000m,
            FechaModificacion = new DateTime(2026, 1, 1),
        };
        var conceptoSinMatriz = new ConceptoPresupuesto
        {
            ProyectoId = proyecto.Id,
            Clave = "1.2",
            Descripcion = "Concepto sin APU",
            EsAgrupador = false,
            Nivel = 1,
            Orden = 2,
            Unidad = "m2",
            ColumnasPersonalizadasJSON = string.Empty,
            Notas = string.Empty,
            Cantidad = 5m,
            MatrizId = null,
            CostoDirectoUnitario = 0m,
            CostoDirectoTotal = 0m,
            PrecioUnitario = 0m,
            ImporteTotal = 0m,
            FechaModificacion = new DateTime(2026, 1, 1),
        };
        var conceptoCantidadCero = new ConceptoPresupuesto
        {
            ProyectoId = proyecto.Id,
            Clave = "1.3",
            Descripcion = "Concepto con cantidad cero",
            EsAgrupador = false,
            Nivel = 1,
            Orden = 3,
            Unidad = "m2",
            ColumnasPersonalizadasJSON = string.Empty,
            Notas = string.Empty,
            Cantidad = 0m,
            MatrizId = null,
            CostoDirectoUnitario = 0m,
            CostoDirectoTotal = 0m,
            PrecioUnitario = 0m,
            ImporteTotal = 0m,
            FechaModificacion = new DateTime(2026, 1, 1),
        };
        ctx.ConceptosPresupuesto.AddRange(agrupador, concepto, conceptoSinMatriz, conceptoCantidadCero);
        ctx.SaveChanges();

        return new Escenario
        {
            Proyecto = proyecto,
            MatrizA = matrizA,
            MatrizB = matrizB,
            MatrizC = matrizC,
            Agrupador = agrupador,
            Concepto = concepto,
            ConceptoSinMatriz = conceptoSinMatriz,
            ConceptoCantidadCero = conceptoCantidadCero,
        };
    }

    private static int BuscarFilaPorConcepto(DataGridView grid, int conceptoId)
    {
        for (int i = 0; i < grid.Rows.Count; i++)
        {
            if (grid.Rows[i].Tag is ConceptoPresupuesto concepto && concepto.Id == conceptoId)
                return i;
        }
        return -1;
    }

    private static Proyecto? LeerProyectoGlobalFormato()
        => (Proyecto?)CampoProyectoGlobal().GetValue(null);

    private static void RestaurarProyectoGlobalFormato(Proyecto? previo)
    {
        if (previo is not null)
            CampoProyectoGlobal().SetValue(null, previo);
    }

    private static FieldInfo CampoProyectoGlobal()
        => typeof(FormatoHelper).GetField(CampoProyectoGlobalFormato,
               BindingFlags.Static | BindingFlags.NonPublic)
           ?? throw new InvalidOperationException(
               $"No se encontró el campo FormatoHelper.{CampoProyectoGlobalFormato}.");
}

internal sealed class DirectorioTemporal : IDisposable
{
    public string Ruta { get; }
    public string DbPath { get; }

    public DirectorioTemporal(string nombre)
    {
        Ruta = Path.Combine(Path.GetTempPath(), $"sopro_{nombre}_{Guid.NewGuid():N}");
        Directory.CreateDirectory(Ruta);
        DbPath = Path.Combine(Ruta, "proyecto-temporal.soproproj");
    }

    public void Dispose()
    {
        for (int intento = 0; intento < 5; intento++)
        {
            try
            {
                Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
                if (Directory.Exists(Ruta))
                    Directory.Delete(Ruta, recursive: true);
                return;
            }
            catch (IOException) when (intento < 4)
            {
                System.Threading.Thread.Sleep(200);
            }
        }
    }
}
