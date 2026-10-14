using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SOPRO.Application.Models.Reporting.ReportColumns;
using SOPRO.Application.Services.Reporting;
using SOPRO.Application.UseCases.Reporting;
using SOPRO.Core.Entities;
using SOPRO.Data.Context;
using SOPRO.Reporting.Formatting;
using SOPRO.WinForms.Forms;
using SOPRO.WinForms.Helpers;
using SOPRO.WinForms.Undo;

namespace SOPRO.Tests.WinForms.Financiamiento;

/// <summary>
/// Paso 6.4 — formato por CELDA del grid de flujo de Financiamiento. Verifica el
/// mapeo selección → direcciones ESTABLES (<c>PER:{NumeroPeriodo}</c> × columna del
/// snapshot), el filtrado de filas sin entidad / columnas no formateables, la
/// persistencia con <see cref="FormatoReporteService"/> y el undo/redo como UNA
/// sola acción compuesta. Mismo patrón que Presupuesto/Indirectos.
/// </summary>
[TestClass]
public class FormatoCeldasFinanciamientoTests
{
    private const string Tipo = "Financiamiento";
    private const string Vista = "";

    private static SOPROContext CrearContexto()
    {
        var dbPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"sopro_fmt_fin_{Guid.NewGuid():N}.db");
        var ctx = new SOPROContext(dbPath);
        ctx.Database.EnsureDeleted();
        ctx.Database.EnsureCreated();
        return ctx;
    }

    private static int AgregarProyecto(SOPROContext ctx)
    {
        var proyecto = new Proyecto
        {
            Nombre = "Proyecto formato financiamiento",
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
        };
        ctx.Proyectos.Add(proyecto);
        ctx.SaveChanges();
        return proyecto.Id;
    }

    private static DataGridView CrearGrid()
    {
        var grid = new DataGridView { AllowUserToAddRows = false };
        foreach (var nombre in new[]
        {
            "colPeriodo", "colInicio", "colFin", "colDias", "colCD", "colCI", "colEgresos",
            "colAnticipo", "colEstim", "colAmort", "colCobroNeto", "colFlujoNeto",
            "colSaldo", "colTasa", "colInteres", "colDummy",
        })
        {
            grid.Columns.Add(new DataGridViewTextBoxColumn { Name = nombre });
        }
        return grid;
    }

    private static int AgregarFila(DataGridView grid, int? numeroPeriodo)
    {
        var index = grid.Rows.Add();
        grid.Rows[index].Tag = numeroPeriodo.HasValue
            ? new FilaFlujoCajaFinanciamiento { NumeroPeriodo = numeroPeriodo.Value }
            : null;
        return index;
    }

    // ═════════════════════════ Selección → direcciones ═════════════════════════

    [TestMethod]
    public void Periodo_GeneraDireccionEstableConClavePER()
    {
        using var grid = CrearGrid();
        var fila = AgregarFila(grid, 3);

        var direcciones = FormFinanciamiento.ConstruirDireccionesSeleccionadas(
            grid, new[] { grid.Rows[fila].Cells["colCD"] });

        Assert.AreEqual(1, direcciones.Count);
        Assert.AreEqual("PER:3", direcciones[0].ClaveFila);
        Assert.AreEqual("colCD", direcciones[0].NombreColumna);
        Assert.AreEqual(Vista, direcciones[0].ClaveVista);
        Assert.AreEqual("PER:3", FormFinanciamiento.ClaveFilaFormato(3));
    }

    [TestMethod]
    public void ColCobroNeto_MapeaAColCobroDelContrato()
    {
        using var grid = CrearGrid();
        Assert.AreEqual("colCobro", FormFinanciamiento.ObtenerNombreColumnaFormato(grid, grid.Columns["colCobroNeto"].Index));
    }

    [TestMethod]
    public void ColDummyYFilaSinEntidad_SeIgnoran()
    {
        using var grid = CrearGrid();
        var conEntidad = AgregarFila(grid, 7);
        var sinEntidad = AgregarFila(grid, null);

        var direcciones = FormFinanciamiento.ConstruirDireccionesSeleccionadas(grid, new[]
        {
            grid.Rows[conEntidad].Cells["colCD"],        // válida
            grid.Rows[conEntidad].Cells["colDummy"],     // columna no formateable
            grid.Rows[sinEntidad].Cells["colCD"],        // fila sin entidad
        });

        Assert.AreEqual(1, direcciones.Count);
        Assert.AreEqual("PER:7", direcciones[0].ClaveFila);
        Assert.AreEqual("colCD", direcciones[0].NombreColumna);
    }

    [TestMethod]
    public void ObtenerNombreColumnaFormato_MapeaColumnasFormateables()
    {
        using var grid = CrearGrid();
        Assert.AreEqual("colPeriodo", FormFinanciamiento.ObtenerNombreColumnaFormato(grid, grid.Columns["colPeriodo"].Index));
        Assert.AreEqual("colEgresos", FormFinanciamiento.ObtenerNombreColumnaFormato(grid, grid.Columns["colEgresos"].Index));
        Assert.IsNull(FormFinanciamiento.ObtenerNombreColumnaFormato(grid, grid.Columns["colDummy"].Index));
    }

    // ═══════════════════ Persistencia + undo/reproducir pintado ═══════════════════

    [TestMethod]
    public void AplicarFormato_PersisteYSeRecarga()
    {
        using var ctx = CrearContexto();
        var proyectoId = AgregarProyecto(ctx);
        var service = new FormatoReporteService(ctx);
        var overlay = new ReportStyleOverlay(Negrita: true, ColorFondo: "#112233");

        var cambios = new List<CambioFormatoSeleccion>
        {
            new(new ReportCellAddress(Vista, FormFinanciamiento.ClaveFilaFormato(1), "colCD"), null, overlay),
            new(new ReportCellAddress(Vista, FormFinanciamiento.ClaveFilaFormato(2), "colEgresos"), null, overlay),
        };

        service.AplicarCeldas(proyectoId, Tipo, FormFinanciamiento.ConstruirLoteCambios(cambios, aplicarAntes: false));

        var recargado = new FormatoReporteService(ctx).CargarCeldas(proyectoId, Tipo);

        Assert.AreEqual(2, recargado.Count);
        var leido = recargado[new ReportCellAddress(Vista, "PER:1", "colCD")];
        Assert.IsTrue(leido.Negrita);
        Assert.AreEqual("#112233", leido.ColorFondo);
    }

    [TestMethod]
    public void UndoRedo_FormatoDeRango_EsUnaSolaAccion()
    {
        using var ctx = CrearContexto();
        var proyectoId = AgregarProyecto(ctx);
        var service = new FormatoReporteService(ctx);
        var overlay = new ReportStyleOverlay(ColorFondo: "#FF0000");

        var cambios = Enumerable.Range(1, 10)
            .Select(i => new CambioFormatoSeleccion(
                new ReportCellAddress(Vista, FormFinanciamiento.ClaveFilaFormato(i), "colSaldo"), null, overlay))
            .ToList();

        var manager = new UndoManager();
        var accion = new CompositeUndoableAction(
            "Formato de 10 celdas",
            cambios.Select(c => new UndoChange(c.Antes, c.Despues)).ToList(),
            _ => service.AplicarCeldas(proyectoId, Tipo, FormFinanciamiento.ConstruirLoteCambios(cambios, aplicarAntes: true)),
            _ => service.AplicarCeldas(proyectoId, Tipo, FormFinanciamiento.ConstruirLoteCambios(cambios, aplicarAntes: false)));

        Assert.IsTrue(manager.Execute(accion));
        Assert.AreEqual(1, manager.UndoCount, "El rango de 10 celdas es UNA sola acción.");
        Assert.AreEqual(10, ctx.FormatosCeldaReporte.Count(f => f.ProyectoId == proyectoId));

        Assert.IsTrue(manager.Undo());
        ctx.ChangeTracker.Clear();
        Assert.AreEqual(0, ctx.FormatosCeldaReporte.Count(f => f.ProyectoId == proyectoId), "Undo revierte TODO el lote.");

        Assert.IsTrue(manager.Redo());
        ctx.ChangeTracker.Clear();
        Assert.AreEqual(10, ctx.FormatosCeldaReporte.Count(f => f.ProyectoId == proyectoId), "Redo reaplica TODO el lote.");
        Assert.AreEqual(1, manager.UndoCount);
        Assert.AreEqual(0, manager.RedoCount);
    }

    [TestMethod]
    public void EstiloVacioEnElLote_EliminaElOverride()
    {
        using var ctx = CrearContexto();
        var proyectoId = AgregarProyecto(ctx);
        var service = new FormatoReporteService(ctx);

        service.UpsertCelda(proyectoId, Tipo, Vista, "PER:1", "colCD", new ReportStyleOverlay(ColorFondo: "#123456"));
        Assert.AreEqual(1, ctx.FormatosCeldaReporte.Count());

        var cambios = new[]
        {
            new CambioFormatoSeleccion(new ReportCellAddress(Vista, "PER:1", "colCD"), null, new ReportStyleOverlay()),
        };
        service.AplicarCeldas(proyectoId, Tipo, FormFinanciamiento.ConstruirLoteCambios(cambios, aplicarAntes: false));
        ctx.ChangeTracker.Clear();

        Assert.AreEqual(0, ctx.FormatosCeldaReporte.Count(), "Un overlay vacío elimina la fila de formato.");
    }

    // ═══════════════════ H6b — selección POR CELDA (no por fila) ═══════════════════

    [TestMethod]
    public void ConfigurarSeleccionPorCelda_FijaCellSelect()
    {
        using var grid = CrearGrid();

        FormFinanciamiento.ConfigurarSeleccionPorCelda(grid);

        Assert.AreEqual(DataGridViewSelectionMode.CellSelect, grid.SelectionMode,
            "El grid debe seleccionar POR CELDAS; con FullRowSelect el ribbon pintaba el override en TODA la fila.");
        Assert.IsTrue(grid.MultiSelect, "Se conserva la selección de rango (Shift) / multi-rango (Ctrl).");
    }

    [TestMethod]
    public void SeleccionarUnaCelda_GeneraUnaSolaDireccion()
    {
        using var grid = CrearGrid();
        FormFinanciamiento.ConfigurarSeleccionPorCelda(grid);
        var fila = AgregarFila(grid, 3);

        grid.CurrentCell = grid.Rows[fila].Cells["colCD"];

        var direcciones = FormFinanciamiento.ConstruirDireccionesSeleccionadas(
            grid, grid.SelectedCells.Cast<DataGridViewCell>());

        Assert.AreEqual(1, direcciones.Count,
            "Seleccionar UNA celda debe producir UNA sola dirección (no toda la fila).");
        Assert.AreEqual("PER:3", direcciones[0].ClaveFila);
        Assert.AreEqual("colCD", direcciones[0].NombreColumna);
    }

    [TestMethod]
    public void DosCeldasMismaFila_GeneranDireccionesIndependientesPorColumna()
    {
        using var grid = CrearGrid();
        FormFinanciamiento.ConfigurarSeleccionPorCelda(grid);
        var fila = AgregarFila(grid, 4);

        grid.CurrentCell = grid.Rows[fila].Cells["colCD"];
        grid.Rows[fila].Cells["colEgresos"].Selected = true;

        var direcciones = FormFinanciamiento.ConstruirDireccionesSeleccionadas(
            grid, grid.SelectedCells.Cast<DataGridViewCell>());

        Assert.AreEqual(2, direcciones.Count);
        Assert.IsTrue(direcciones.All(d => d.ClaveFila == "PER:4"),
            "Mismo período: la identidad de cada celda difiere sólo por columna.");
        CollectionAssert.AreEquivalent(
            new[] { "colCD", "colEgresos" },
            direcciones.Select(d => d.NombreColumna).ToArray());
    }

    [TestMethod]
    public void OverlayDeUnaCelda_SoloAfectaAEsaCeldaEnElSnapshot()
    {
        using var ctx = CrearContexto();
        var proyectoId = AgregarProyecto(ctx);
        var service = new FormatoReporteService(ctx);
        service.UpsertCelda(proyectoId, Tipo, Vista, "PER:3", "colCD",
            new ReportStyleOverlay(ColorFondo: "#FF00AA"));

        var baseSnapshot = new FinanciamientoReportSnapshotBuilder().Build(proyectoId, "Flujo de caja", columnas: null);
        var snapshot = new ReportStyleSnapshotBuilder(service).Build(baseSnapshot);

        var objetivo = ReportCellStyleResolver.Resolve(
            snapshot, Vista, "PER:3", "colCD", RolFilaReporte.Concepto);
        var otraColumna = ReportCellStyleResolver.Resolve(
            snapshot, Vista, "PER:3", "colEgresos", RolFilaReporte.Concepto);

        Assert.AreEqual("#FF00AA", objetivo.ColorFondo, "El override aplica a la celda destino.");
        Assert.AreNotEqual("#FF00AA", otraColumna.ColorFondo,
            "Las demás celdas de la MISMA fila NO se contaminan con el override.");
    }

    [TestMethod]
    public void FilasSeleccionadas_DesdeCelda_DevuelveLaFilaDelPeriodo()
    {
        using var grid = CrearGrid();
        FormFinanciamiento.ConfigurarSeleccionPorCelda(grid);
        var fila = AgregarFila(grid, 7);

        grid.Rows[fila].Cells["colCD"].Selected = true;

        var filas = SeleccionGridHelper.FilasSeleccionadas(grid);

        Assert.AreEqual(1, filas.Count);
        Assert.AreEqual(fila, filas[0].Index);
    }

    // ═════════════════════════════════ Helpers ═════════════════════════════════
}
