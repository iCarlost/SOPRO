using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SOPRO.Tests.WinForms.TestInfrastructure;
using SOPRO.Application.Models.Reporting.ReportColumns;
using SOPRO.Application.Services.Reporting;
using SOPRO.Core.Entities;
using SOPRO.Data.Context;
using SOPRO.WinForms.Forms;
using SOPRO.WinForms.Helpers;
using SOPRO.WinForms.Undo;

namespace SOPRO.Tests.WinForms.Indirectos;

/// <summary>
/// Paso 5.1: formato de celdas por SELECCIÓN en los grids de Indirectos (Oficina
/// Central y Campo). Verifica el mapeo selección → direcciones ESTABLES
/// (<c>G:{Id}</c> grupos, <c>IC:{Id}</c> conceptos), el filtrado de filas sin
/// entidad / columnas no formateables, la persistencia con
/// <see cref="FormatoReporteService"/> y el undo/redo como UNA sola acción
/// compuesta. Mismo patrón que <c>FormatoCeldasPresupuestoTests</c>.
/// </summary>
[TestClass]
public class FormatoCeldasIndirectosTests
{
    private const string Tipo = "Indirectos";
    private const string Vista = ""; // vista por defecto / comodín de ReportCellAddress

    // ═════════════════════════════ Infraestructura ═════════════════════════════

    private static SOPROContext CrearContexto()
    {
        var dbPath = Path.Combine(Path.GetTempPath(), $"sopro_fmt_ind_{Guid.NewGuid():N}.db");
        var ctx = new SOPROContext(dbPath);
        ctx.Database.EnsureDeleted();
        ctx.Database.EnsureCreated();
        return ctx;
    }

    private static int AgregarProyecto(SOPROContext ctx)
    {
        var proyecto = new Proyecto
        {
            Nombre = "Proyecto formato indirectos",
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
        grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "colGrupo" });
        grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "colImporteMensual" });
        grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "colDuracion" });
        grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "colImporteTotal" });
        grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "colId", Visible = false });
        grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "colTipo", Visible = false });
        return grid;
    }

    private static int AgregarFila(DataGridView grid, string? tipo, int? id)
    {
        var index = grid.Rows.Add();
        grid.Rows[index].Cells["colTipo"].Value = tipo;
        grid.Rows[index].Cells["colId"].Value = id;
        return index;
    }

    // ═════════════════════════ Selección → direcciones ═════════════════════════

    [TestMethod]
    public void Concepto_GeneraDireccionEstableConClaveIC()
    {
        using var grid = CrearGrid();
        var fila = AgregarFila(grid, "CONCEPTO", 7);

        var direcciones = FormIndirectos.ConstruirDireccionesSeleccionadas(
            grid, new[] { grid.Rows[fila].Cells["colImporteMensual"] });

        Assert.AreEqual(1, direcciones.Count);
        Assert.AreEqual("IC:7", direcciones[0].ClaveFila);
        Assert.AreEqual("ImporteMensual", direcciones[0].NombreColumna);
        Assert.AreEqual(Vista, direcciones[0].ClaveVista);
    }

    [TestMethod]
    public void Grupo_GeneraDireccionEstableConClaveG()
    {
        using var grid = CrearGrid();
        var fila = AgregarFila(grid, "GRUPO", 3);

        var direcciones = FormIndirectos.ConstruirDireccionesSeleccionadas(
            grid, new[] { grid.Rows[fila].Cells["colImporteTotal"] });

        Assert.AreEqual(1, direcciones.Count);
        Assert.AreEqual("G:3", direcciones[0].ClaveFila);
        Assert.AreEqual("ImporteTotal", direcciones[0].NombreColumna);
    }

    [TestMethod]
    public void RangoContiguoShift_GeneraUnaDireccionPorFila()
    {
        using var grid = CrearGrid();
        var g = AgregarFila(grid, "GRUPO", 1);
        var c1 = AgregarFila(grid, "CONCEPTO", 2);
        var c2 = AgregarFila(grid, "CONCEPTO", 3);

        var direcciones = FormIndirectos.ConstruirDireccionesSeleccionadas(grid, new[]
        {
            grid.Rows[g].Cells["colImporteMensual"],
            grid.Rows[c1].Cells["colImporteMensual"],
            grid.Rows[c2].Cells["colImporteMensual"],
        });

        CollectionAssert.AreEqual(
            new[] { "G:1", "IC:2", "IC:3" },
            direcciones.Select(d => d.ClaveFila).ToArray());
    }

    [TestMethod]
    public void FilaSinEntidadYColumnaNoExportable_SeIgnoran()
    {
        using var grid = CrearGrid();
        var conEntidad = AgregarFila(grid, "CONCEPTO", 42);
        var sinEntidad = AgregarFila(grid, null, null);

        var direcciones = FormIndirectos.ConstruirDireccionesSeleccionadas(grid, new[]
        {
            grid.Rows[conEntidad].Cells["colImporteMensual"], // válida
            grid.Rows[conEntidad].Cells["colId"],             // columna no formateable
            grid.Rows[sinEntidad].Cells["colImporteMensual"], // fila sin entidad
        });

        Assert.AreEqual(1, direcciones.Count);
        Assert.AreEqual("IC:42", direcciones[0].ClaveFila);
        Assert.AreEqual("ImporteMensual", direcciones[0].NombreColumna);
    }

    [TestMethod]
    public void ObtenerNombreColumnaFormato_MapeaColumnasFormateables()
    {
        using var grid = CrearGrid();

        Assert.AreEqual("Grupo", FormIndirectos.ObtenerNombreColumnaFormato(grid, grid.Columns["colGrupo"].Index));
        Assert.AreEqual("ImporteMensual", FormIndirectos.ObtenerNombreColumnaFormato(grid, grid.Columns["colImporteMensual"].Index));
        Assert.AreEqual("Duracion", FormIndirectos.ObtenerNombreColumnaFormato(grid, grid.Columns["colDuracion"].Index));
        Assert.AreEqual("ImporteTotal", FormIndirectos.ObtenerNombreColumnaFormato(grid, grid.Columns["colImporteTotal"].Index));
        Assert.IsNull(FormIndirectos.ObtenerNombreColumnaFormato(grid, grid.Columns["colId"].Index));
        Assert.IsNull(FormIndirectos.ObtenerNombreColumnaFormato(grid, grid.Columns["colTipo"].Index));
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
            new(new ReportCellAddress(Vista, ClaveFilaReporte.ConceptoIndirecto(1), "ImporteMensual"), null, overlay),
            new(new ReportCellAddress(Vista, ClaveFilaReporte.GrupoIndirecto(2), "ImporteTotal"), null, overlay),
        };

        FormatoTestWriter.Aplicar(ctx, proyectoId, Tipo, FormIndirectos.ConstruirLoteCambios(cambios, aplicarAntes: false));

        var recargado = new FormatoReporteService(ctx).CargarCeldas(proyectoId, Tipo);

        Assert.AreEqual(2, recargado.Count);
        var leido = recargado[new ReportCellAddress(Vista, "IC:1", "ImporteMensual")];
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
                new ReportCellAddress(Vista, ClaveFilaReporte.ConceptoIndirecto(i), "ImporteMensual"), null, overlay))
            .ToList();

        var manager = new UndoManager();
        var accion = new CompositeUndoableAction(
            "Formato de 10 celdas",
            cambios.Select(c => new UndoChange(c.Antes, c.Despues)).ToList(),
            _ => FormatoTestWriter.Aplicar(ctx, proyectoId, Tipo, FormIndirectos.ConstruirLoteCambios(cambios, aplicarAntes: true)),
            _ => FormatoTestWriter.Aplicar(ctx, proyectoId, Tipo, FormIndirectos.ConstruirLoteCambios(cambios, aplicarAntes: false)));

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
        Assert.AreEqual(1, manager.UndoCount);
        Assert.AreEqual(0, manager.RedoCount);
    }

    [TestMethod]
    public void EstiloVacioEnElLote_EliminaElOverride()
    {
        using var ctx = CrearContexto();
        var proyectoId = AgregarProyecto(ctx);
        var service = new FormatoReporteService(ctx);

        service.UpsertCelda(proyectoId, Tipo, Vista, "IC:1", "ImporteMensual",
            new ReportStyleOverlay(ColorFondo: "#123456"));
        Assert.AreEqual(1, ctx.FormatosCeldaReporte.Count());

        var cambios = new[]
        {
            new CambioFormatoSeleccion(new ReportCellAddress(Vista, "IC:1", "ImporteMensual"), new ReportStyleOverlay(ColorFondo: "#123456"), new ReportStyleOverlay()),
        };
        FormatoTestWriter.Aplicar(ctx, proyectoId, Tipo, FormIndirectos.ConstruirLoteCambios(cambios, aplicarAntes: false));
        ctx.ChangeTracker.Clear();

        Assert.AreEqual(0, ctx.FormatosCeldaReporte.Count(), "Un overlay vacío elimina la fila de formato.");
    }

    // ═══════════════════ Precedencia de pintado (override > default) ═══════════════════

    [TestMethod]
    public void AplicarOverlayACellStyle_GanaSobreElEstiloBaseHardcodeado()
    {
        var estilo = new DataGridViewCellStyle { BackColor = Color.Purple, ForeColor = Color.White };

        FormPresupuesto.AplicarOverlayACellStyle(
            estilo, new ReportStyleOverlay(ColorFondo: "#112233", Negrita: true, Wrap: true));

        Assert.AreEqual(Color.FromArgb(0x11, 0x22, 0x33), estilo.BackColor,
            "El override de celda gana sobre el color de rol/hardcodeado.");
        Assert.IsTrue(estilo.Font!.Bold);
        Assert.AreEqual(DataGridViewTriState.True, estilo.WrapMode);
    }

    // ═════════════════════════════════ Fakes ═════════════════════════════════

    /// <summary>
    /// Grid de prueba que implementa <see cref="IGridSeleccionFormato"/> sobre el
    /// mapeo REAL de <see cref="FormIndirectos.ConstruirDireccionesSeleccionadas"/>
    /// y guarda los overlays en memoria, para ejercitar el coordinador y el
    /// pipeline de persistencia sin UI real.
    /// </summary>
    private sealed class GridSeleccionFake : IGridFormato, IGridSeleccionFormato
    {
        private readonly DataGridView _grid;
        private readonly IReadOnlyList<DataGridViewCell> _celdas;
        private readonly Dictionary<ReportCellAddress, ReportStyleOverlay> _overlays = new();

        public GridSeleccionFake(DataGridView grid, IReadOnlyList<DataGridViewCell> celdas)
        {
            _grid = grid;
            _celdas = celdas;
        }

        public bool TieneSeleccionCeldas => _celdas.Count > 0;

        public IReadOnlyList<ReportCellAddress> ObtenerDireccionesSeleccionadas()
            => FormIndirectos.ConstruirDireccionesSeleccionadas(_grid, _celdas);

        public bool EsCeldaFormateable(ReportCellAddress direccion) => true;

        public ReportStyleOverlay? ObtenerOverlayActual(ReportCellAddress direccion)
            => _overlays.TryGetValue(direccion, out var overlay) ? overlay : null;

        public void AplicarOverlayCelda(ReportCellAddress direccion, ReportStyleOverlay overlay)
            => _overlays[direccion] = overlay;

        public DataGridView GridPrincipal => _grid;

        public ColumnaPersonalizada ColumnaSeleccionada => null!;

        public void AplicarFormato(ColumnaPersonalizada fmt) { }

        public void AplicarFormatoGlobal(ColumnaPersonalizada fmt) { }

        public event EventHandler? ColumnaSeleccionadaCambiada { add { } remove { } }

        public bool GenerarReporteExcel() => false;
    }

    [TestMethod]
    public void MultiRangoCtrl_DeduplicaPorDireccionEstable()
    {
        using var grid = CrearGrid();
        var c1 = AgregarFila(grid, "CONCEPTO", 1);
        var c2 = AgregarFila(grid, "CONCEPTO", 2);
        var g = AgregarFila(grid, "GRUPO", 3);

        var fake = new GridSeleccionFake(grid, new[]
        {
            grid.Rows[c1].Cells["colImporteMensual"],
            grid.Rows[g].Cells["colImporteTotal"],
            grid.Rows[c1].Cells["colImporteMensual"],
            grid.Rows[c2].Cells["colImporteMensual"],
        });
        var coordinador = new FormatoSeleccionCoordinator();
        coordinador.EstablecerGrid(fake);

        var cambios = coordinador.AplicarASeleccion(new ReportStyleOverlay(Negrita: true));

        Assert.AreEqual(DestinoFormatoGrid.Celdas, coordinador.Destino);
        Assert.AreEqual(3, cambios.Count, "Las direcciones repetidas se deduplican.");
        CollectionAssert.AreEqual(
            new[] { "IC:1", "G:3", "IC:2" },
            cambios.Select(c => c.Direccion.ClaveFila).ToArray());
    }
}
