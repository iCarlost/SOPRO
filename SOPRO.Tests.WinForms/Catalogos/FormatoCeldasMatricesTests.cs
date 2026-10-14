using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SOPRO.Application.Models.Matrices;
using SOPRO.Application.Models.Reporting.ReportColumns;
using SOPRO.Application.Services.Reporting;
using SOPRO.Application.UseCases.Reporting;
using SOPRO.Core.Entities;
using SOPRO.Data.Context;
using SOPRO.Reporting.Formatting;
using SOPRO.WinForms.Forms;
using SOPRO.WinForms.Helpers;

namespace SOPRO.Tests.WinForms.Catalogos;

/// <summary>
/// H6b — selección POR CELDA (tipo Excel) en el Catálogo de Matrices. El grid se
/// diseñó con <c>FullRowSelect</c>, donde un clic sobre UNA celda seleccionaba
/// TODA la fila y el ribbon aplicaba el override a todas sus columnas. Se fija
/// <see cref="DataGridViewSelectionMode.CellSelect"/> (+MultiSelect) y se
/// preservan las operaciones por FILA con
/// <see cref="SeleccionGridHelper.FilasSeleccionadas"/>.
/// </summary>
[TestClass]
public class FormatoCeldasMatricesTests
{
    private const string Tipo = "APU";
    private const string Vista = "";

    // ═════════════════════════════ Infraestructura ═════════════════════════════

    private static SOPROContext CrearContexto()
    {
        var dbPath = Path.Combine(Path.GetTempPath(), $"sopro_fmt_mat_cell_{Guid.NewGuid():N}.db");
        var ctx = new SOPROContext(dbPath);
        ctx.Database.EnsureDeleted();
        ctx.Database.EnsureCreated();
        return ctx;
    }

    private static int AgregarProyecto(SOPROContext ctx)
    {
        var proyecto = new Proyecto
        {
            Nombre = "Proyecto formato matrices por celda",
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

    private static DataGridView CrearGrid(params string[] columnasFormateables)
    {
        var grid = new DataGridView { AllowUserToAddRows = false };
        foreach (var nombre in columnasFormateables)
        {
            grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = nombre,
                Tag = new ColumnaMatriz { Nombre = nombre, NombreInterno = nombre },
            });
        }
        return grid;
    }

    private static int AgregarFila(DataGridView grid, object? entidad)
    {
        var index = grid.Rows.Add();
        grid.Rows[index].Tag = entidad;
        return index;
    }

    // ═══════════════════════ Selección por celda (H6b) ═══════════════════════

    [TestMethod]
    public void ConfigurarSeleccionPorCelda_FijaCellSelect()
    {
        using var grid = CrearGrid("Descripcion");

        FormMatrices.ConfigurarSeleccionPorCelda(grid);

        Assert.AreEqual(DataGridViewSelectionMode.CellSelect, grid.SelectionMode,
            "El grid debe seleccionar POR CELDAS; con FullRowSelect el ribbon pintaba el override en TODA la fila.");
        Assert.IsTrue(grid.MultiSelect, "Se conserva la selección de rango (Shift) / multi-rango (Ctrl).");
    }

    [TestMethod]
    public void SeleccionarUnaCelda_GeneraUnaSolaDireccion()
    {
        using var grid = CrearGrid("Descripcion", "ImporteTotal");
        FormMatrices.ConfigurarSeleccionPorCelda(grid);
        var index = grid.Rows.Add();
        grid.Rows[index].Tag = new MatrixGridRowDisplay { MatrizId = 3, Source = new Matriz { Id = 3 } };

        grid.CurrentCell = grid.Rows[index].Cells["ImporteTotal"];

        var direcciones = FormMatrices.ConstruirDireccionesSeleccionadas(
            grid, grid.SelectedCells.Cast<DataGridViewCell>());

        Assert.AreEqual(1, direcciones.Count,
            "Seleccionar UNA celda debe producir UNA sola dirección (no toda la fila).");
        Assert.AreEqual("M:3", direcciones[0].ClaveFila);
        Assert.AreEqual("ImporteTotal", direcciones[0].NombreColumna);
    }

    [TestMethod]
    public void DosCeldasMismaFila_GeneranDireccionesIndependientesPorColumna()
    {
        using var grid = CrearGrid("Descripcion", "ImporteTotal");
        FormMatrices.ConfigurarSeleccionPorCelda(grid);
        var index = grid.Rows.Add();
        grid.Rows[index].Tag = new Matriz { Id = 5 };

        grid.CurrentCell = grid.Rows[index].Cells["Descripcion"];
        grid.Rows[index].Cells["ImporteTotal"].Selected = true;

        var direcciones = FormMatrices.ConstruirDireccionesSeleccionadas(
            grid, grid.SelectedCells.Cast<DataGridViewCell>());

        Assert.AreEqual(2, direcciones.Count);
        Assert.IsTrue(direcciones.All(d => d.ClaveFila == "M:5"),
            "Mismo par fila: la identidad de cada celda debe diferir sólo por columna.");
        CollectionAssert.AreEquivalent(
            new[] { "Descripcion", "ImporteTotal" },
            direcciones.Select(d => d.NombreColumna).ToArray());
    }

    // ═════════════ Overlay de una celda: NO se derrama (snapshot real) ═════════════

    [TestMethod]
    public void OverlayDeUnaCelda_SoloAfectaAEsaCeldaEnElSnapshot()
    {
        using var ctx = CrearContexto();
        var proyectoId = AgregarProyecto(ctx);
        var service = new FormatoReporteService(ctx);
        service.UpsertCelda(proyectoId, Tipo, Vista, "COMP:22", "ImporteTotal",
            new ReportStyleOverlay(ColorFondo: "#FF00AA"));

        var baseSnapshot = new ApuReportSnapshotBuilder().Build(proyectoId, "Catálogo de matrices", columnas: null);
        var snapshot = new ReportStyleSnapshotBuilder(service).Build(baseSnapshot);

        var objetivo = ReportCellStyleResolver.Resolve(
            snapshot, Vista, "COMP:22", "ImporteTotal", RolFilaReporte.Concepto);
        var otraColumna = ReportCellStyleResolver.Resolve(
            snapshot, Vista, "COMP:22", "Descripcion", RolFilaReporte.Concepto);

        Assert.AreEqual("#FF00AA", objetivo.ColorFondo, "El override aplica a la celda destino.");
        Assert.AreNotEqual("#FF00AA", otraColumna.ColorFondo,
            "Las demás celdas de la MISMA fila NO se contaminan con el override.");
    }

    // ═══════════════════ Operaciones por FILA preservadas (H6b) ═══════════════════

    [TestMethod]
    public void EncabezadoSeleccionado_DestinoEsColumna_NoAplicaPorCelda()
    {
        var grid = new GridFakeMatrices
        {
            TieneSeleccionCeldas = false,
            ColumnaSeleccionada = new ColumnaPersonalizada { Nombre = "Importe" },
        };
        var coordinador = new FormatoSeleccionCoordinator();
        coordinador.EstablecerGrid(grid);

        Assert.AreEqual(DestinoFormatoGrid.Columna, coordinador.Destino);

        var cambios = coordinador.AplicarASeleccion(new ReportStyleOverlay(Negrita: true));

        Assert.AreEqual(0, cambios.Count, "El encabezado NO debe aplicar formato por celda.");
    }

    /// <summary>
    /// Grid FAKE con la semántica de Matrices: implementa los dos contratos
    /// (columna + selección de celdas) sin depender de UI real.
    /// </summary>
    private sealed class GridFakeMatrices : IGridFormato, IGridSeleccionFormato
    {
        public DataGridView GridPrincipal => null!;
        public ColumnaPersonalizada? ColumnaSeleccionada { get; set; }
        public bool TieneSeleccionCeldas { get; set; }
        public List<ReportCellAddress> Direcciones { get; } = new();
        public Dictionary<ReportCellAddress, ReportStyleOverlay> Overlays { get; } = new();

        public void AplicarFormato(ColumnaPersonalizada fmt) { }
        public void AplicarFormatoGlobal(ColumnaPersonalizada fmt) { }
        public bool GenerarReporteExcel() => false;

#pragma warning disable CS0067 // Evento del contrato; no se dispara en las pruebas.
        public event EventHandler? ColumnaSeleccionadaCambiada;
#pragma warning restore CS0067

        public IReadOnlyList<ReportCellAddress> ObtenerDireccionesSeleccionadas() => Direcciones;
        public bool EsCeldaFormateable(ReportCellAddress direccion) => true;
        public ReportStyleOverlay? ObtenerOverlayActual(ReportCellAddress direccion)
            => Overlays.TryGetValue(direccion, out var overlay) ? overlay : null;
        public void AplicarOverlayCelda(ReportCellAddress direccion, ReportStyleOverlay overlay)
            => Overlays[direccion] = overlay;
    }

    // ═════════════════════ SeleccionGridHelper.FilasSeleccionadas ═════════════════════

    [TestMethod]
    public void FilasSeleccionadas_DesdeCeldasEnDosFilas_DevuelveDosFilas()
    {
        using var grid = CrearGrid("Descripcion");
        FormMatrices.ConfigurarSeleccionPorCelda(grid);
        var r0 = grid.Rows.Add();
        var r1 = grid.Rows.Add();

        grid.Rows[r0].Cells[0].Selected = true;
        grid.Rows[r1].Cells[0].Selected = true;

        var filas = SeleccionGridHelper.FilasSeleccionadas(grid);

        Assert.AreEqual(2, filas.Count, "Dos celdas en dos filas distintas ⇒ dos filas de operación.");
        Assert.AreEqual(r0, filas[0].Index);
        Assert.AreEqual(r1, filas[1].Index);
    }

    [TestMethod]
    public void FilasSeleccionadas_DesdeFilasCompletas_DevuelveEsasFilas()
    {
        // Grid ENLAZADO: a diferencia del grid manual, una fila seleccionada por
        // completo puebla SelectedRows (la fuente que usan las operaciones por fila).
        using var grid = new DataGridView { AutoGenerateColumns = false, AllowUserToAddRows = false };
        grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "col", DataPropertyName = "Valor" });
        grid.BindingContext = new BindingContext();
        grid.DataSource = new[] { new { Valor = 1 }, new { Valor = 2 } }.ToList();

        grid.Rows[1].Selected = true;
        grid.Rows[0].Selected = true;

        Assert.AreEqual(2, grid.SelectedRows.Count, "Las filas completas pueblan SelectedRows.");
        var filas = SeleccionGridHelper.FilasSeleccionadas(grid);

        CollectionAssert.AreEqual(new[] { 0, 1 }, filas.Select(f => f.Index).ToArray());
    }

    [TestMethod]
    public void FilasSeleccionadas_DeduplicaYOrdena()
    {
        using var grid = CrearGrid("Descripcion");
        FormMatrices.ConfigurarSeleccionPorCelda(grid);
        var r0 = grid.Rows.Add();
        grid.Rows.Add();
        var r2 = grid.Rows.Add();

        foreach (DataGridViewCell celda in grid.Rows[r0].Cells)
            celda.Selected = true;                  // fila completa
        grid.Rows[r2].Cells[0].Selected = true;     // celda en otra fila
        grid.Rows[r2].Cells[0].Selected = true;     // repetida (Ctrl)

        var filas = SeleccionGridHelper.FilasSeleccionadas(grid);

        CollectionAssert.AreEqual(new[] { r0, r2 }, filas.Select(f => f.Index).ToArray(),
            "El helper deduplica (celda + fila) y ordena ascendente por índice de fila.");
    }
}
