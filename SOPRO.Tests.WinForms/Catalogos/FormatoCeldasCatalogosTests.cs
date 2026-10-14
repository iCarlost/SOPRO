using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SOPRO.Application.Models.Reporting.ReportColumns;
using SOPRO.Application.Services.Reporting;
using SOPRO.Application.UseCases.Materials;
using SOPRO.Core.Entities;
using SOPRO.Data.Context;
using SOPRO.WinForms.Forms;
using SOPRO.WinForms.Helpers;
using SOPRO.WinForms.Undo;

namespace SOPRO.Tests.WinForms.Catalogos;

/// <summary>
/// Paso 6.3 — formato de celdas por SELECCIÓN en los grids de los catálogos
/// (Materiales, Mano de Obra, Herramientas, Maquinaria). Verifica el mapeo
/// selección → direcciones ESTABLES (<c>INS:{Id}</c> + identificador neutral de
/// columna), el filtrado de columnas no formateables, la persistencia por lote y
/// el undo/redo como UNA sola acción compuesta. Mismo patrón que
/// <c>FormatoCeldasIndirectosTests</c>.
/// </summary>
[TestClass]
public class FormatoCeldasCatalogosTests
{
    private const string Vista = "";

    // ═════════════════════════════ Infraestructura ═════════════════════════════

    private static SOPROContext CrearContexto()
    {
        var dbPath = Path.Combine(Path.GetTempPath(), $"sopro_fmt_cat_{Guid.NewGuid():N}.db");
        var ctx = new SOPROContext(dbPath);
        ctx.Database.EnsureDeleted();
        ctx.Database.EnsureCreated();
        return ctx;
    }

    private static int AgregarProyecto(SOPROContext ctx)
    {
        var proyecto = new Proyecto
        {
            Nombre = "Proyecto formato catálogos",
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

    private static DataGridView CrearGrid<T>(DataGridViewTextBoxColumn[] columnas, IReadOnlyList<T> filas)
    {
        var grid = new DataGridView { AutoGenerateColumns = false, AllowUserToAddRows = false };
        foreach (var columna in columnas)
            grid.Columns.Add(columna);
        // Sin un BindingContext el grid headless no materializa las filas del DataSource
        // (DataBoundItem quedaría null). Con él, el binding es idéntico al de la UI.
        grid.BindingContext = new BindingContext();
        grid.DataSource = filas.ToList();
        return grid;
    }

    private static DataGridViewTextBoxColumn Col(string nombre, string dataProperty, object? tag)
        => new() { Name = nombre, DataPropertyName = dataProperty, Tag = tag };

    // ═════════════════════════ Materiales ═════════════════════════

    [TestMethod]
    public void Materiales_MapeaFilaYColumnaAEstables()
    {
        using var grid = CrearGrid(
            new[]
            {
                Col("col_Clave", "Clave", new ColumnaMaterial { NombreInterno = "Clave" }),
                Col("col_PrecioUnitario", "PrecioUnitario", new ColumnaMaterial { NombreInterno = "PrecioUnitario" }),
                Col("colDummy", null, null),
            },
            new[] { new MaterialListItem(7, "MAT-1", "Cemento", "kg", 12.5m, "", OrigenInsumo.Proyecto) });

        Assert.AreEqual("INS:7", FormCatalogoMateriales.ObtenerClaveFilaFormato(grid, 0));
        Assert.AreEqual("PrecioUnitario",
            FormCatalogoMateriales.ObtenerNombreColumnaFormato(grid, grid.Columns["col_PrecioUnitario"].Index));
        Assert.IsNull(FormCatalogoMateriales.ObtenerNombreColumnaFormato(grid, grid.Columns["colDummy"].Index));

        var direcciones = FormCatalogoMateriales.ConstruirDireccionesSeleccionadas(
            grid, new[] { grid.Rows[0].Cells["col_PrecioUnitario"] });

        Assert.AreEqual(1, direcciones.Count);
        Assert.AreEqual("INS:7", direcciones[0].ClaveFila);
        Assert.AreEqual("PrecioUnitario", direcciones[0].NombreColumna);
        Assert.AreEqual(Vista, direcciones[0].ClaveVista);
    }

    [TestMethod]
    public void Materiales_ColumnaNoFormateable_SeIgnora()
    {
        using var grid = CrearGrid(
            new[]
            {
                Col("col_Clave", "Clave", new ColumnaMaterial { NombreInterno = "Clave" }),
                Col("colDummy", null, null),
            },
            new[] { new MaterialListItem(3, "MAT-3", "Arena", "m3", 5m, "", OrigenInsumo.Proyecto) });

        var direcciones = FormCatalogoMateriales.ConstruirDireccionesSeleccionadas(
            grid, new[] { grid.Rows[0].Cells["colDummy"], grid.Rows[0].Cells["col_Clave"] });

        Assert.AreEqual(1, direcciones.Count);
        Assert.AreEqual("Clave", direcciones[0].NombreColumna);
    }

    // ═════════════════════════ Mano de Obra ═════════════════════════

    [TestMethod]
    public void ManoObra_MapeaFilaYColumnaAEstables()
    {
        using var grid = CrearGrid(
            new[]
            {
                Col("col_Clave", "Clave", new ColumnaManoObra { NombreInterno = "Clave" }),
                Col("col_SalarioBase", "SalarioBase", new ColumnaManoObra { NombreInterno = "SalarioBase" }),
            },
            new[] { new ManoDeObra { Id = 21, Clave = "MO-1", Descripcion = "Peón" } });

        Assert.AreEqual("INS:21", FormCatalogoManoObra.ObtenerClaveFilaFormato(grid, 0));

        var direcciones = FormCatalogoManoObra.ConstruirDireccionesSeleccionadas(
            grid, new[] { grid.Rows[0].Cells["col_SalarioBase"] });

        Assert.AreEqual(1, direcciones.Count);
        Assert.AreEqual("INS:21", direcciones[0].ClaveFila);
        Assert.AreEqual("SalarioBase", direcciones[0].NombreColumna);
    }

    // ═════════════════════════ Herramientas ═════════════════════════

    [TestMethod]
    public void Herramientas_MapeaFilaYColumnaAEstables()
    {
        using var grid = CrearGrid(
            new[]
            {
                Col("col_Clave", "Clave", new ColumnaHerramienta { NombreInterno = "Clave" }),
                Col("col_PrecioUnitario", "PrecioUnitario", new ColumnaHerramienta { NombreInterno = "PrecioUnitario" }),
            },
            new[] { new Herramienta { Id = 33, Clave = "H-1", Descripcion = "Pala" } });

        Assert.AreEqual("INS:33", FormCatalogoHerramientas.ObtenerClaveFilaFormato(grid, 0));

        var direcciones = FormCatalogoHerramientas.ConstruirDireccionesSeleccionadas(
            grid, new[] { grid.Rows[0].Cells["col_PrecioUnitario"] });

        Assert.AreEqual(1, direcciones.Count);
        Assert.AreEqual("INS:33", direcciones[0].ClaveFila);
        Assert.AreEqual("PrecioUnitario", direcciones[0].NombreColumna);
    }

    // ═════════════════════════ Maquinaria ═════════════════════════

    [TestMethod]
    public void Maquinaria_MapeaFilaYColumnaAEstables()
    {
        using var grid = CrearGrid(
            new[]
            {
                Col("col_Clave", "Clave", new ColumnaMaquinaria { NombreInterno = "Clave" }),
                Col("col_CostoHorario", "CostoHorario", new ColumnaMaquinaria { NombreInterno = "CostoHorario" }),
            },
            new[] { new Maquinaria { Id = 44, Clave = "MQ-1", Descripcion = "Retro" } });

        Assert.AreEqual("INS:44", FormCatalogoMaquinaria.ObtenerClaveFilaFormato(grid, 0));

        var direcciones = FormCatalogoMaquinaria.ConstruirDireccionesSeleccionadas(
            grid, new[] { grid.Rows[0].Cells["col_CostoHorario"] });

        Assert.AreEqual(1, direcciones.Count);
        Assert.AreEqual("INS:44", direcciones[0].ClaveFila);
        Assert.AreEqual("CostoHorario", direcciones[0].NombreColumna);
    }

    // ═══════════════════ Persistencia + undo/reproducir pintado ═══════════════════

    [TestMethod]
    public void Persistencia_Materiales_PersisteYSeRecarga()
    {
        using var ctx = CrearContexto();
        var proyectoId = AgregarProyecto(ctx);
        var service = new FormatoReporteService(ctx);
        var overlay = new ReportStyleOverlay(Negrita: true, ColorFondo: "#112233");

        var cambios = new List<CambioFormatoSeleccion>
        {
            new(new ReportCellAddress(Vista, ClaveFilaReporte.Insumo(1), "PrecioUnitario"), null, overlay),
            new(new ReportCellAddress(Vista, ClaveFilaReporte.Insumo(2), "Descripcion"), null, overlay),
        };

        service.AplicarCeldas(proyectoId, MaterialesTipo,
            FormCatalogoMateriales.ConstruirLoteCambios(cambios, aplicarAntes: false));

        var recargado = new FormatoReporteService(ctx).CargarCeldas(proyectoId, MaterialesTipo);

        Assert.AreEqual(2, recargado.Count);
        var leido = recargado[new ReportCellAddress(Vista, "INS:1", "PrecioUnitario")];
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
                new ReportCellAddress(Vista, ClaveFilaReporte.Insumo(i), "PrecioUnitario"), null, overlay))
            .ToList();

        var manager = new UndoManager();
        var accion = new CompositeUndoableAction(
            "Formato de 10 celdas",
            cambios.Select(c => new UndoChange(c.Antes, c.Despues)).ToList(),
            _ => service.AplicarCeldas(proyectoId, MaterialesTipo, FormCatalogoMateriales.ConstruirLoteCambios(cambios, aplicarAntes: true)),
            _ => service.AplicarCeldas(proyectoId, MaterialesTipo, FormCatalogoMateriales.ConstruirLoteCambios(cambios, aplicarAntes: false)));

        Assert.IsTrue(manager.Execute(accion));
        Assert.AreEqual(1, manager.UndoCount, "El rango de 10 celdas es UNA sola acción.");
        Assert.AreEqual(10, ctx.FormatosCeldaReporte.Count(f => f.ProyectoId == proyectoId));

        Assert.IsTrue(manager.Undo());
        ctx.ChangeTracker.Clear();
        Assert.AreEqual(0, ctx.FormatosCeldaReporte.Count(f => f.ProyectoId == proyectoId),
            "Undo revierte TODO el lote.");

        Assert.IsTrue(manager.Redo());
        ctx.ChangeTracker.Clear();
        Assert.AreEqual(10, ctx.FormatosCeldaReporte.Count(f => f.ProyectoId == proyectoId),
            "Redo reaplica TODO el lote.");
    }

    [TestMethod]
    public void EstiloVacioEnElLote_EliminaElOverride()
    {
        using var ctx = CrearContexto();
        var proyectoId = AgregarProyecto(ctx);
        var service = new FormatoReporteService(ctx);

        service.UpsertCelda(proyectoId, MaterialesTipo, Vista, "INS:1", "PrecioUnitario",
            new ReportStyleOverlay(ColorFondo: "#123456"));
        Assert.AreEqual(1, ctx.FormatosCeldaReporte.Count());

        var cambios = new[]
        {
            new CambioFormatoSeleccion(new ReportCellAddress(Vista, "INS:1", "PrecioUnitario"), null, new ReportStyleOverlay()),
        };
        service.AplicarCeldas(proyectoId, MaterialesTipo,
            FormCatalogoMateriales.ConstruirLoteCambios(cambios, aplicarAntes: false));
        ctx.ChangeTracker.Clear();

        Assert.AreEqual(0, ctx.FormatosCeldaReporte.Count(), "Un overlay vacío elimina la fila de formato.");
    }

    [TestMethod]
    public void AislamientoPorTipoReporte_NoSeMezclanLosCatalogos()
    {
        using var ctx = CrearContexto();
        var proyectoId = AgregarProyecto(ctx);
        var service = new FormatoReporteService(ctx);

        service.UpsertCelda(proyectoId, "Materiales", Vista, "INS:1", "PrecioUnitario",
            new ReportStyleOverlay(ColorFondo: "#000001"));
        service.UpsertCelda(proyectoId, "Herramientas", Vista, "INS:1", "PrecioUnitario",
            new ReportStyleOverlay(ColorFondo: "#000002"));

        Assert.AreEqual(1, service.CargarCeldas(proyectoId, "Materiales").Count);
        Assert.AreEqual(1, service.CargarCeldas(proyectoId, "Herramientas").Count);
        Assert.AreEqual("#000001",
            service.CargarCeldas(proyectoId, "Materiales")[new ReportCellAddress(Vista, "INS:1", "PrecioUnitario")].ColorFondo);
    }

    // ═══════════════════ H6a: formato por CELDA (no por fila) ═══════════════════

    /// <summary>
    /// H6a — los 4 grids de catálogo quedan en selección POR CELDA (tipo Excel) con
    /// multi-selección, de modo que un clic sobre una celda NO selecciona toda la fila.
    /// </summary>
    [TestMethod]
    public void GridsDeCatalogo_QuedanEnCellSelectConMultiSelect()
    {
        using var ctx = CrearContexto();

        using (var form = new FormCatalogoMateriales(ctx))
        {
            Assert.AreEqual(DataGridViewSelectionMode.CellSelect, form.GridPrincipal.SelectionMode, "Materiales");
            Assert.IsTrue(form.GridPrincipal.MultiSelect, "Materiales MultiSelect");
        }

        using (var form = new FormCatalogoManoObra(ctx))
        {
            Assert.AreEqual(DataGridViewSelectionMode.CellSelect, form.GridPrincipal.SelectionMode, "Mano de Obra");
            Assert.IsTrue(form.GridPrincipal.MultiSelect, "Mano de Obra MultiSelect");
        }

        using (var form = new FormCatalogoHerramientas(ctx))
        {
            Assert.AreEqual(DataGridViewSelectionMode.CellSelect, form.GridPrincipal.SelectionMode, "Herramientas");
            Assert.IsTrue(form.GridPrincipal.MultiSelect, "Herramientas MultiSelect");
        }

        using (var form = new FormCatalogoMaquinaria(ctx))
        {
            Assert.AreEqual(DataGridViewSelectionMode.CellSelect, form.GridPrincipal.SelectionMode, "Maquinaria");
            Assert.IsTrue(form.GridPrincipal.MultiSelect, "Maquinaria MultiSelect");
        }
    }

    [TestMethod]
    public void Helper_ConfigurarSeleccionPorCelda_FijaCellSelectYMultiSelect()
    {
        using var grid = new DataGridView { AllowUserToAddRows = false };
        grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        grid.MultiSelect = false;

        SeleccionGridHelper.ConfigurarSeleccionPorCelda(grid);

        Assert.AreEqual(DataGridViewSelectionMode.CellSelect, grid.SelectionMode);
        Assert.IsTrue(grid.MultiSelect);
    }

    /// <summary>
    /// Dos celdas de la MISMA fila generan direcciones independientes por columna
    /// (antes, con FullRowSelect, se generaba una dirección por cada columna de la
    /// fila y el formato se derramaba a toda la fila).
    /// </summary>
    [TestMethod]
    public void Materiales_DosCeldasDeLaMismaFila_DireccionesIndependientesPorColumna()
    {
        using var grid = CrearGrid(
            new[]
            {
                Col("col_Clave", "Clave", new ColumnaMaterial { NombreInterno = "Clave" }),
                Col("col_PrecioUnitario", "PrecioUnitario", new ColumnaMaterial { NombreInterno = "PrecioUnitario" }),
            },
            new[] { new MaterialListItem(7, "MAT-1", "Cemento", "kg", 12.5m, "", OrigenInsumo.Proyecto) });

        var direcciones = FormCatalogoMateriales.ConstruirDireccionesSeleccionadas(
            grid, new[] { grid.Rows[0].Cells["col_Clave"], grid.Rows[0].Cells["col_PrecioUnitario"] });

        Assert.AreEqual(2, direcciones.Count);
        CollectionAssert.AreEquivalent(
            new[] { "Clave", "PrecioUnitario" },
            direcciones.Select(d => d.NombreColumna).ToArray());
        Assert.IsTrue(direcciones.All(d => d.ClaveFila == "INS:7"));
    }

    /// <summary>
    /// <see cref="SeleccionGridHelper.FilasSeleccionadas"/> resuelve las filas
    /// distintas desde <c>SelectedCells</c> (selección por celda) y las ordena por
    /// índice; un clic por celda NO pobla <c>SelectedRows</c>.
    /// </summary>
    [TestMethod]
    public void FilasSeleccionadas_ConCeldasEnDosFilas_DeduplicaYOrdena()
    {
        using var grid = CrearGrid(
            new[] { Col("col_Clave", "Clave", new ColumnaMaterial { NombreInterno = "Clave" }) },
            new[]
            {
                new MaterialListItem(1, "MAT-1", "A", "kg", 1m, "", OrigenInsumo.Proyecto),
                new MaterialListItem(2, "MAT-2", "B", "kg", 2m, "", OrigenInsumo.Proyecto),
                new MaterialListItem(3, "MAT-3", "C", "kg", 3m, "", OrigenInsumo.Proyecto),
            });

        grid.SelectionMode = DataGridViewSelectionMode.CellSelect;
        grid.ClearSelection();
        grid.Rows[2].Cells["col_Clave"].Selected = true;
        grid.Rows[0].Cells["col_Clave"].Selected = true;

        var filas = SeleccionGridHelper.FilasSeleccionadas(grid);

        CollectionAssert.AreEqual(new[] { 0, 2 }, filas.Select(f => f.Index).ToArray());
        Assert.AreEqual(0, grid.SelectedRows.Count, "La selección por celdas no puebla SelectedRows.");
        Assert.AreEqual(2, grid.SelectedCells.Count);
    }

    /// <summary>
    /// Deduplica celdas de la MISMA fila (varias columnas ⇒ una sola fila) y
    /// conserva el orden ascendente con una segunda fila.
    /// </summary>
    [TestMethod]
    public void FilasSeleccionadas_DeduplicaCeldasDeLaMismaFila()
    {
        using var grid = CrearGrid(
            new[]
            {
                Col("col_Clave", "Clave", new ColumnaMaterial { NombreInterno = "Clave" }),
                Col("col_PrecioUnitario", "PrecioUnitario", new ColumnaMaterial { NombreInterno = "PrecioUnitario" }),
            },
            new[]
            {
                new MaterialListItem(1, "MAT-1", "A", "kg", 1m, "", OrigenInsumo.Proyecto),
                new MaterialListItem(2, "MAT-2", "B", "kg", 2m, "", OrigenInsumo.Proyecto),
            });

        grid.SelectionMode = DataGridViewSelectionMode.CellSelect;
        grid.ClearSelection();
        grid.Rows[1].Cells["col_Clave"].Selected = true;
        grid.Rows[0].Cells["col_Clave"].Selected = true;
        grid.Rows[0].Cells["col_PrecioUnitario"].Selected = true;

        var filas = SeleccionGridHelper.FilasSeleccionadas(grid);

        Assert.AreEqual(3, grid.SelectedCells.Count);
        CollectionAssert.AreEqual(new[] { 0, 1 }, filas.Select(f => f.Index).ToArray(),
            "Dos celdas de la fila 0 y una de la fila 1 ⇒ dos filas distintas ordenadas.");
    }

    /// <summary>
    /// Cuando la selección puebla <c>SelectedRows</c> (fila completa: clic en
    /// encabezado en CellSelect, o <c>FullRowSelect</c>), el helper devuelve esas
    /// filas ordenadas.
    /// </summary>
    [TestMethod]
    public void FilasSeleccionadas_ConSelectedRows_DevuelveEsasFilas()
    {
        using var grid = CrearGrid(
            new[] { Col("col_Clave", "Clave", new ColumnaMaterial { NombreInterno = "Clave" }) },
            new[]
            {
                new MaterialListItem(1, "MAT-1", "A", "kg", 1m, "", OrigenInsumo.Proyecto),
                new MaterialListItem(2, "MAT-2", "B", "kg", 2m, "", OrigenInsumo.Proyecto),
                new MaterialListItem(3, "MAT-3", "C", "kg", 3m, "", OrigenInsumo.Proyecto),
            });

        grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        grid.ClearSelection();
        grid.Rows[2].Selected = true;
        grid.Rows[0].Selected = true;

        var filas = SeleccionGridHelper.FilasSeleccionadas(grid);

        Assert.AreEqual(2, grid.SelectedRows.Count);
        CollectionAssert.AreEqual(new[] { 0, 2 }, filas.Select(f => f.Index).ToArray());
    }

    /// <summary>
    /// Snapshot REAL: un overlay aplicado a UNA celda se pinta SÓLO en esa celda y
    /// NO se derrama a otra columna de la misma fila (regresión H6a).
    /// </summary>
    [TestMethod]
    public void OverlayDeUnaCelda_NoSeDerramaAOtrasColumnas()
    {
        using var ctx = CrearContexto();
        var proyectoId = AgregarProyecto(ctx);

        using var grid = CrearGrid(
            new[]
            {
                Col("col_Clave", "Clave", new ColumnaMaterial { NombreInterno = "Clave" }),
                Col("col_PrecioUnitario", "PrecioUnitario", new ColumnaMaterial { NombreInterno = "PrecioUnitario" }),
            },
            new[] { new MaterialListItem(7, "MAT-1", "Cemento", "kg", 12.5m, "", OrigenInsumo.Proyecto) });

        grid.SelectionMode = DataGridViewSelectionMode.CellSelect;
        grid.MultiSelect = true;

        var formato = new CatalogoFormatoCeldas(grid, ctx, proyectoId, MaterialesTipo, Vista);
        formato.Cargar();

        grid.ClearSelection();
        grid.Rows[0].Cells["col_PrecioUnitario"].Selected = true;
        grid.CurrentCell = grid.Rows[0].Cells["col_PrecioUnitario"];

        var direcciones = formato.ObtenerDireccionesSeleccionadas();
        Assert.AreEqual(1, direcciones.Count, "Una celda seleccionada ⇒ una sola dirección.");
        Assert.AreEqual("PrecioUnitario", direcciones[0].NombreColumna);

        formato.AplicarOverlayCelda(direcciones[0], new ReportStyleOverlay(ColorFondo: "#112233", Negrita: true));

        var estiloPrecio = new DataGridViewCellStyle();
        formato.AplicarOverride(new DataGridViewCellFormattingEventArgs(
            grid.Columns["col_PrecioUnitario"].Index, 0, value: null, desiredType: null, estiloPrecio));
        Assert.AreEqual(Color.FromArgb(0x11, 0x22, 0x33), estiloPrecio.BackColor, "La celda formateada recibe el overlay.");
        Assert.IsTrue(estiloPrecio.Font!.Bold);

        var estiloClave = new DataGridViewCellStyle();
        formato.AplicarOverride(new DataGridViewCellFormattingEventArgs(
            grid.Columns["col_Clave"].Index, 0, value: null, desiredType: null, estiloClave));
        Assert.AreNotEqual(Color.FromArgb(0x11, 0x22, 0x33), estiloClave.BackColor,
            "El overlay de una celda NO se derrama a otra columna de la misma fila.");
    }

    /// <summary>
    /// Las operaciones por FILA (consolidación) siguen resolviendo la selección a
    /// través del helper: dos celdas en filas distintas habilitan la consolidación
    /// aunque <c>SelectedRows</c> quede vacío bajo CellSelect.
    /// </summary>
    [TestMethod]
    public void Materiales_ConsolidacionResuelveFilasDesdeSeleccionDeCeldas()
    {
        using var ctx = CrearContexto();
        var proyectoId = AgregarProyecto(ctx);

        using var form = new FormCatalogoMateriales(ctx, proyectoId);
        var grid = form.GridPrincipal;
        grid.BindingContext = new BindingContext();
        grid.DataSource = new List<MaterialListItem>
        {
            new(1, "MAT-1", "Cemento", "kg", 10m, "", OrigenInsumo.Proyecto),
            new(2, "MAT-2", "Arena", "m3", 20m, "", OrigenInsumo.Proyecto),
        };

        grid.ClearSelection();
        grid.Rows[0].Cells[0].Selected = true;
        grid.Rows[1].Cells[0].Selected = true;

        Assert.AreEqual(0, grid.SelectedRows.Count, "CellSelect por celdas no puebla SelectedRows.");
        Assert.IsTrue(form.ConsolidacionDisponible, "Dos filas distintas (por celdas) habilitan consolidación.");

        var seleccion = form.ObtenerSeleccionConsolidable();

        Assert.AreEqual(2, seleccion.Count);
        CollectionAssert.AreEquivalent(new[] { 1, 2 }, seleccion.Select(s => s.Id).ToArray());
    }

    /// <summary>
    /// Sin regresión: cuando la última interacción fue sobre el ENCABEZADO de una
    /// columna, el catálogo no reporta selección de CELDAS (el ribbon mantiene el
    /// destino Columna), aunque existan celdas seleccionadas en el grid.
    /// </summary>
    [TestMethod]
    public void Catalogo_SeleccionPorEncabezado_NoReportaSeleccionDeCeldas()
    {
        using var ctx = CrearContexto();
        var proyectoId = AgregarProyecto(ctx);

        using var grid = CrearGrid(
            new[] { Col("col_Clave", "Clave", new ColumnaMaterial { NombreInterno = "Clave" }) },
            new[] { new MaterialListItem(7, "MAT-1", "A", "kg", 1m, "", OrigenInsumo.Proyecto) });

        var formato = new CatalogoFormatoCeldas(grid, ctx, proyectoId, MaterialesTipo, Vista);
        formato.Cargar();

        grid.Rows[0].Cells["col_Clave"].Selected = true;
        formato.SeleccionPorEncabezado = true;

        Assert.IsFalse(formato.TieneSeleccionCeldas, "El encabezado ⇒ destino Columna, no celdas.");
        Assert.AreEqual(0, formato.ObtenerDireccionesSeleccionadas().Count);
    }

    private const string MaterialesTipo = "Materiales";
}
