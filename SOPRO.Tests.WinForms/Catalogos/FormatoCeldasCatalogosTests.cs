using System;
using System.Collections.Generic;
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

    private const string MaterialesTipo = "Materiales";
}
