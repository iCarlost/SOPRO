using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SOPRO.Tests.WinForms.TestInfrastructure;
using SOPRO.Application.DTOs.Programacion.Insumos;
using SOPRO.Application.Models.Reporting.ReportColumns;
using SOPRO.Application.Services.Reporting;
using SOPRO.Application.UseCases.Reporting;
using SOPRO.Core.Entities;
using SOPRO.Data.Context;
using SOPRO.Reporting.Formatting;
using SOPRO.WinForms.Forms;
using SOPRO.WinForms.Helpers;
using SOPRO.WinForms.Undo;

namespace SOPRO.Tests.WinForms.Programa;

/// <summary>
/// Paso 6.2: formato de celdas por SELECCIÓN en el grid del Programa de Insumos.
/// Verifica el mapeo selección → direcciones ESTABLES (<c>INS:{InsumoId}</c>) y,
/// sobre todo, que las columnas DINÁMICAS de período usan el identificador ESTABLE
/// del período (<c>per_</c>,<c>acu_</c>,<c>imp_</c>,<c>iacu_</c> + PeriodoId) y NO
/// el índice visual: el formato sobrevive a regenerar/reordenar los períodos.
/// </summary>
[TestClass]
public class FormatoCeldasProgramaInsumosTests
{
    private const string Tipo = "ProgramaInsumos";
    private const string Vista = "";

    // ═════════════════════════════ Infraestructura ═════════════════════════════

    private static SOPROContext CrearContexto()
    {
        var dbPath = Path.Combine(Path.GetTempPath(), $"sopro_fmt_pi_{Guid.NewGuid():N}.db");
        var ctx = new SOPROContext(dbPath);
        ctx.Database.EnsureDeleted();
        ctx.Database.EnsureCreated();
        return ctx;
    }

    private static int AgregarProyecto(SOPROContext ctx)
    {
        var proyecto = new SOPRO.Core.Entities.Proyecto
        {
            Nombre = "Proyecto formato programa de insumos",
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

    private static DataGridView CrearGrid(IEnumerable<int> periodos, string[]? columnasEstaticas = null)
    {
        var grid = new DataGridView { AllowUserToAddRows = false };
        foreach (var nombre in columnasEstaticas ?? new[]
                 {
                     ProgramaInsumosReportSnapshotBuilder.ColClave,
                     ProgramaInsumosReportSnapshotBuilder.ColDescripcion,
                     ProgramaInsumosReportSnapshotBuilder.ColTotal,
                 })
            grid.Columns.Add(new DataGridViewTextBoxColumn { Name = nombre });

        foreach (var periodoId in periodos)
        {
            grid.Columns.Add(new DataGridViewTextBoxColumn { Name = $"per_{periodoId}" });
            grid.Columns.Add(new DataGridViewTextBoxColumn { Name = $"acu_{periodoId}" });
            grid.Columns.Add(new DataGridViewTextBoxColumn { Name = $"imp_{periodoId}" });
            grid.Columns.Add(new DataGridViewTextBoxColumn { Name = $"iacu_{periodoId}" });
        }

        grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "colDummy", AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill });
        return grid;
    }

    private static int AgregarFila(DataGridView grid, int? insumoId)
    {
        var index = grid.Rows.Add();
        grid.Rows[index].Tag = insumoId;
        return index;
    }

    // ═══════════════════════ Claves estables y período ═══════════════════════

    [TestMethod]
    public void Insumo_GeneraDireccionEstableConClaveIns()
    {
        using var grid = CrearGrid(new[] { 501 });
        var fila = AgregarFila(grid, 5);

        var direcciones = FormProgramaInsumos.ConstruirDireccionesSeleccionadas(
            grid, new[] { grid.Rows[fila].Cells["per_501"] });

        Assert.AreEqual(1, direcciones.Count);
        Assert.AreEqual("INS:5", direcciones[0].ClaveFila);
        Assert.AreEqual("per_501", direcciones[0].NombreColumna);
        Assert.AreEqual(Vista, direcciones[0].ClaveVista);
    }

    [TestMethod]
    public void ColumnaDinamica_UsaIdentificadorEstableDelPeriodo_NoElIndice()
    {
        // La MISMA columna de período conserva su identificador aunque cambie el
        // orden visual (los períodos se regeneran): el formato no se pierde.
        using var gridA = CrearGrid(new[] { 501, 502 });
        using var gridB = CrearGrid(new[] { 502, 501 });

        var a = FormProgramaInsumos.ObtenerNombreColumnaFormato(gridA, gridA.Columns["imp_502"].Index);
        var b = FormProgramaInsumos.ObtenerNombreColumnaFormato(gridB, gridB.Columns["imp_502"].Index);

        Assert.AreEqual("imp_502", a);
        Assert.AreEqual("imp_502", b);
        Assert.AreEqual(a, b, "El identificador estable no depende del índice de la columna.");
    }

    [TestMethod]
    public void ColumnaDummy_YColumnaInexistente_NoSonFormateables()
    {
        using var grid = CrearGrid(new[] { 501 });

        Assert.IsNull(FormProgramaInsumos.ObtenerNombreColumnaFormato(grid, grid.Columns["colDummy"].Index));
        Assert.AreEqual(ProgramaInsumosReportSnapshotBuilder.ColClave,
            FormProgramaInsumos.ObtenerNombreColumnaFormato(grid, grid.Columns[ProgramaInsumosReportSnapshotBuilder.ColClave].Index));
    }

    [TestMethod]
    public void FilaSinInsumo_SeIgnora()
    {
        using var grid = CrearGrid(new[] { 501 });
        var conInsumo = AgregarFila(grid, 42);
        var sinInsumo = AgregarFila(grid, null);

        var direcciones = FormProgramaInsumos.ConstruirDireccionesSeleccionadas(grid, new[]
        {
            grid.Rows[conInsumo].Cells["per_501"],
            grid.Rows[sinInsumo].Cells["per_501"],
        });

        Assert.AreEqual(1, direcciones.Count);
        Assert.AreEqual("INS:42", direcciones[0].ClaveFila);
    }

    // ═══════════════════ Persistencia + undo ═══════════════════

    [TestMethod]
    public void AplicarFormato_PersisteYSeRecarga()
    {
        using var ctx = CrearContexto();
        var proyectoId = AgregarProyecto(ctx);
        var service = new FormatoReporteService(ctx);
        var overlay = new ReportStyleOverlay(Negrita: true, ColorFondo: "#112233");

        var cambios = new List<CambioFormatoSeleccion>
        {
            new(new ReportCellAddress(Vista, ClaveFilaReporte.Insumo(1), "per_501"), null, overlay),
            new(new ReportCellAddress(Vista, ClaveFilaReporte.Insumo(2), "imp_502"), null, overlay),
        };

        FormatoTestWriter.Aplicar(ctx, proyectoId, Tipo, FormProgramaInsumos.ConstruirLoteCambios(cambios, aplicarAntes: false));

        var recargado = new FormatoReporteService(ctx).CargarCeldas(proyectoId, Tipo);

        Assert.AreEqual(2, recargado.Count);
        var leido = recargado[new ReportCellAddress(Vista, "INS:1", "per_501")];
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

        var cambios = Enumerable.Range(1, 8)
            .Select(i => new CambioFormatoSeleccion(
                new ReportCellAddress(Vista, ClaveFilaReporte.Insumo(i), $"per_{500 + i}"), null, overlay))
            .ToList();

        var manager = new UndoManager();
        var accion = new CompositeUndoableAction(
            "Formato de 8 celdas",
            cambios.Select(c => new UndoChange(c.Antes, c.Despues)).ToList(),
            _ => FormatoTestWriter.Aplicar(ctx, proyectoId, Tipo, FormProgramaInsumos.ConstruirLoteCambios(cambios, aplicarAntes: true)),
            _ => FormatoTestWriter.Aplicar(ctx, proyectoId, Tipo, FormProgramaInsumos.ConstruirLoteCambios(cambios, aplicarAntes: false)));

        Assert.IsTrue(manager.Execute(accion));
        Assert.AreEqual(1, manager.UndoCount);
        Assert.AreEqual(8, ctx.FormatosCeldaReporte.Count(f => f.ProyectoId == proyectoId));

        Assert.IsTrue(manager.Undo());
        ctx.ChangeTracker.Clear();
        Assert.AreEqual(0, ctx.FormatosCeldaReporte.Count(f => f.ProyectoId == proyectoId), "Undo revierte TODO el lote.");

        Assert.IsTrue(manager.Redo());
        ctx.ChangeTracker.Clear();
        Assert.AreEqual(8, ctx.FormatosCeldaReporte.Count(f => f.ProyectoId == proyectoId), "Redo reaplica TODO el lote.");
    }

    /// <summary>
    /// Prueba específica del paso 6.2: al REGENERAR los períodos (nuevos Ids / distinto
    /// orden) un override guardado sobre el identificador estable del período sigue
    /// resolviendo, porque la clave no incluye la posición visual.
    /// </summary>
    [TestMethod]
    public void RegenerarPeriodos_ElFormatoSobrevivePorClaveEstable()
    {
        using var ctx = CrearContexto();
        var proyectoId = AgregarProyecto(ctx);
        var service = new FormatoReporteService(ctx);

        // 1) Programación inicial: guarda formato sobre el período 501.
        var direccion = new ReportCellAddress(Vista, ClaveFilaReporte.Insumo(7), "per_501");
        FormatoTestWriter.Aplicar(ctx, proyectoId, Tipo, new[]
        {
            new CambioFormatoCeldaCommand(
                Vista,
                "INS:7",
                "per_501",
                ExpectedOverlay: null,
                ProposedOverlay: new ReportStyleOverlay(ColorFondo: "#ABCDEF")),
        });

        // 2) Se regeneran los períodos: el grid se reconstruye con otro orden/columnas.
        using var gridRegenerado = CrearGrid(new[] { 502, 501 });

        // El identificador estable del período 501 es el mismo pese al orden distinto.
        var identificador = FormProgramaInsumos.ObtenerNombreColumnaFormato(
            gridRegenerado, gridRegenerado.Columns["per_501"].Index);
        Assert.AreEqual("per_501", identificador);

        // 3) El override persistido sigue resolviendo para la nueva selección.
        var recargado = new FormatoReporteService(ctx).CargarCeldas(proyectoId, Tipo);
        var reconstruida = new ReportCellAddress(Vista, ClaveFilaReporte.Insumo(7), identificador!);

        Assert.IsTrue(recargado.ContainsKey(reconstruida), "El formato sobrevive a regenerar los períodos.");
        Assert.AreEqual("#ABCDEF", recargado[reconstruida].ColorFondo);
        Assert.AreEqual(ClaveFilaReporte.Insumo(7), reconstruida.ClaveFila);
    }

    [TestMethod]
    public void EstiloVacioEnElLote_EliminaElOverride()
    {
        using var ctx = CrearContexto();
        var proyectoId = AgregarProyecto(ctx);
        var service = new FormatoReporteService(ctx);

        service.UpsertCelda(proyectoId, Tipo, Vista, "INS:1", "per_501", new ReportStyleOverlay(ColorFondo: "#123456"));
        Assert.AreEqual(1, ctx.FormatosCeldaReporte.Count());

        var cambios = new[]
        {
            new CambioFormatoSeleccion(new ReportCellAddress(Vista, "INS:1", "per_501"), new ReportStyleOverlay(ColorFondo: "#123456"), new ReportStyleOverlay()),
        };
        FormatoTestWriter.Aplicar(ctx, proyectoId, Tipo, FormProgramaInsumos.ConstruirLoteCambios(cambios, aplicarAntes: false));
        ctx.ChangeTracker.Clear();

        Assert.AreEqual(0, ctx.FormatosCeldaReporte.Count(), "Un overlay vacío elimina la fila de formato.");
    }

    // ═══════════════════ H4 — formato POR CELDA (no por fila) ═══════════════════

    [TestMethod]
    public void ConfigurarSeleccionPorCelda_FijaCellSelect()
    {
        using var grid = CrearGrid(new[] { 501 });

        FormProgramaInsumos.ConfigurarSeleccionPorCelda(grid);

        Assert.AreEqual(DataGridViewSelectionMode.CellSelect, grid.SelectionMode,
            "El grid debe seleccionar POR CELDAS; con FullRowSelect el ribbon pintaba el override en TODA la fila.");
        Assert.IsTrue(grid.MultiSelect, "Se conserva la selección de rango (Shift) / multi-rango (Ctrl).");
    }

    [TestMethod]
    public void SeleccionarUnaCelda_GeneraUnaSolaDireccionDeCelda()
    {
        using var grid = CrearGrid(new[] { 501 });
        FormProgramaInsumos.ConfigurarSeleccionPorCelda(grid);
        var fila = AgregarFila(grid, 7);

        grid.CurrentCell = grid.Rows[fila].Cells["per_501"];

        var direcciones = FormProgramaInsumos.ConstruirDireccionesSeleccionadas(
            grid, grid.SelectedCells.Cast<DataGridViewCell>());

        Assert.AreEqual(1, direcciones.Count,
            "Seleccionar UNA celda debe producir UNA sola dirección (no toda la fila).");
        Assert.AreEqual("INS:7", direcciones[0].ClaveFila);
        Assert.AreEqual("per_501", direcciones[0].NombreColumna);
    }

    [TestMethod]
    public void DosCeldasMismaFila_GeneranDireccionesIndependientesPorColumna()
    {
        using var grid = CrearGrid(new[] { 501 });
        FormProgramaInsumos.ConfigurarSeleccionPorCelda(grid);
        var fila = AgregarFila(grid, 7);

        grid.CurrentCell = grid.Rows[fila].Cells["per_501"];
        grid.Rows[fila].Cells["acu_501"].Selected = true;

        var direcciones = FormProgramaInsumos.ConstruirDireccionesSeleccionadas(
            grid, grid.SelectedCells.Cast<DataGridViewCell>());

        Assert.AreEqual(2, direcciones.Count);
        Assert.IsTrue(direcciones.All(d => d.ClaveFila == "INS:7"),
            "Mismo par fila: la identidad de cada celda debe diferir sólo por columna.");
        CollectionAssert.AreEquivalent(
            new[] { "per_501", "acu_501" },
            direcciones.Select(d => d.NombreColumna).ToArray());
    }

    [TestMethod]
    public void OverlayDeUnaCelda_SoloAfectaAEsaCeldaEnElSnapshot()
    {
        using var ctx = CrearContexto();
        var proyectoId = AgregarProyecto(ctx);
        var service = new FormatoReporteService(ctx);
        service.UpsertCelda(proyectoId, Tipo, Vista, "INS:7", "per_501",
            new ReportStyleOverlay(ColorFondo: "#FF00AA"));

        var snapshot = new ProgramaInsumosReportSnapshotBuilder()
            .Build(proyectoId, "Insumos", programa: null, vista: ProgramaInsumosVista.Mixto);
        snapshot = new ReportStyleSnapshotBuilder(service).Build(snapshot);

        var periodo = ReportCellStyleResolver.Resolve(
            snapshot, Vista, "INS:7", "per_501", RolFilaReporte.Concepto, false);
        var acumulado = ReportCellStyleResolver.Resolve(
            snapshot, Vista, "INS:7", "acu_501", RolFilaReporte.Concepto, false);

        Assert.AreEqual("#FF00AA", periodo.ColorFondo, "El override aplica a la celda destino.");
        Assert.AreNotEqual("#FF00AA", acumulado.ColorFondo,
            "Las demás celdas de la MISMA fila NO se contaminan con el override.");
    }

    [TestMethod]
    public void EncabezadoSeleccionado_DestinoEsColumna_NoAplicaPorCelda()
    {
        // Al seleccionar el encabezado no hay selección de celdas y el destino es la
        // COLUMNA (sin regresión). Se replica la semántica del grid de Programa.
        var grid = new GridFakeProgramaInsumos
        {
            TieneSeleccionCeldas = false,
            ColumnaSeleccionada = new ColumnaPersonalizada { Nombre = "Total" },
        };
        var coordinador = new FormatoSeleccionCoordinator();
        coordinador.EstablecerGrid(grid);

        Assert.AreEqual(DestinoFormatoGrid.Columna, coordinador.Destino);

        var cambios = coordinador.AplicarASeleccion(new ReportStyleOverlay(Negrita: true));

        Assert.AreEqual(0, cambios.Count, "El encabezado NO debe aplicar formato por celda.");
    }

    /// <summary>
    /// Grid FAKE con la semántica del Programa de Insumos: implementa los dos
    /// contratos (columna + selección de celdas) sin depender de UI real.
    /// </summary>
    private sealed class GridFakeProgramaInsumos : IGridFormato, IGridSeleccionFormato
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
}
