using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SOPRO.Application.Models.Matrices;
using SOPRO.Application.Models.Reporting.ReportColumns;
using SOPRO.Application.Services.Reporting;
using SOPRO.Core.Entities;
using SOPRO.Data.Context;
using SOPRO.WinForms.Forms;
using SOPRO.WinForms.Helpers;
using SOPRO.WinForms.Undo;

namespace SOPRO.Tests.WinForms.Matrices;

/// <summary>
/// Paso 5.2: formato de celdas por SELECCIÓN en el grid de Matrices/APU.
/// Verifica el mapeo selección → direcciones ESTABLES (C:{Id} conceptos,
/// COMP:{Id} componentes, M:{Id} matrices), el filtrado de filas sin entidad /
/// columnas no formateables, la persistencia con <see cref="FormatoReporteService"/>
/// bajo el TipoReporte "APU" y el undo/redo como UNA sola acción compuesta.
/// Se usan grids fake / controles no mostrados para no depender de UI real.
/// </summary>
[TestClass]
public class FormatoCeldasMatricesTests
{
    private const string Tipo = "APU";
    private const string Vista = "";

    // ═════════════════════════════ Infraestructura ═════════════════════════════

    private static SOPROContext CrearContexto()
    {
        var dbPath = Path.Combine(Path.GetTempPath(), $"sopro_fmt_mat_{Guid.NewGuid():N}.db");
        var ctx = new SOPROContext(dbPath);
        ctx.Database.EnsureDeleted();
        ctx.Database.EnsureCreated();
        return ctx;
    }

    private static int AgregarProyecto(SOPROContext ctx)
    {
        var proyecto = new Proyecto
        {
            Nombre = "Proyecto formato matrices",
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

    private static ConceptoPresupuesto Concepto(int id) => new() { Id = id, EsAgrupador = false };
    private static ComponenteMatriz Componente(int id) => new() { Id = id };
    private static Matriz MatrizEntidad(int id) => new() { Id = id };

    // ═════════════════════════ Selección → direcciones ═════════════════════════

    [TestMethod]
    public void Concepto_GeneraClaveC()
    {
        using var grid = CrearGrid("ImporteTotal");
        var fila = AgregarFila(grid, Concepto(7));

        var direcciones = FormMatrices.ConstruirDireccionesSeleccionadas(grid, new[] { grid.Rows[fila].Cells[0] });

        Assert.AreEqual(1, direcciones.Count);
        Assert.AreEqual("C:7", direcciones[0].ClaveFila);
        Assert.AreEqual("ImporteTotal", direcciones[0].NombreColumna);
        Assert.AreEqual(Vista, direcciones[0].ClaveVista);
    }

    [TestMethod]
    public void Componente_GeneraClaveComp()
    {
        using var grid = CrearGrid("Descripcion");
        var fila = AgregarFila(grid, Componente(55));

        var direcciones = FormMatrices.ConstruirDireccionesSeleccionadas(grid, new[] { grid.Rows[fila].Cells[0] });

        Assert.AreEqual("COMP:55", direcciones[0].ClaveFila);
    }

    [TestMethod]
    public void MatrizDelCatalogo_GeneraClaveM()
    {
        using var grid = CrearGrid("Descripcion");
        var index = grid.Rows.Add();
        grid.Rows[index].Tag = new MatrixGridRowDisplay { MatrizId = 3, Source = MatrizEntidad(3) };

        var direcciones = FormMatrices.ConstruirDireccionesSeleccionadas(grid, new[] { grid.Rows[index].Cells[0] });

        Assert.AreEqual("M:3", direcciones[0].ClaveFila);
    }

    [TestMethod]
    public void RangoShift_GeneraUnaDireccionPorFila()
    {
        using var grid = CrearGrid("ImporteTotal");
        var i0 = AgregarFila(grid, Componente(1));
        var i1 = AgregarFila(grid, Componente(2));
        var i2 = AgregarFila(grid, Concepto(3));

        var direcciones = FormMatrices.ConstruirDireccionesSeleccionadas(grid, new[]
        {
            grid.Rows[i0].Cells[0],
            grid.Rows[i1].Cells[0],
            grid.Rows[i2].Cells[0],
        });

        CollectionAssert.AreEqual(new[] { "COMP:1", "COMP:2", "C:3" }, direcciones.Select(d => d.ClaveFila).ToArray());
    }

    [TestMethod]
    public void FilaSinEntidadYColumnaNoFormateable_SeIgnoran()
    {
        using var grid = CrearGrid("ImporteTotal");
        grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "colDummy" }); // sin Tag
        var conEntidad = AgregarFila(grid, Componente(42));
        var sinEntidad = AgregarFila(grid, null);

        var direcciones = FormMatrices.ConstruirDireccionesSeleccionadas(grid, new[]
        {
            grid.Rows[conEntidad].Cells[0],   // válida
            grid.Rows[conEntidad].Cells[1],   // columna no formateable
            grid.Rows[sinEntidad].Cells[0],   // fila sin entidad
        });

        Assert.AreEqual(1, direcciones.Count);
        Assert.AreEqual("COMP:42", direcciones[0].ClaveFila);
    }

    [TestMethod]
    public void MultiRangoCtrl_DeduplicaPorDireccionEstable()
    {
        using var grid = CrearGrid("ImporteTotal");
        var i0 = AgregarFila(grid, Componente(1));
        var i1 = AgregarFila(grid, Componente(2));
        var i2 = AgregarFila(grid, Componente(3));

        var fake = new GridSeleccionFake(grid, new[]
        {
            grid.Rows[i0].Cells[0],
            grid.Rows[i2].Cells[0],
            grid.Rows[i0].Cells[0],
            grid.Rows[i1].Cells[0],
        });
        var coordinador = new FormatoSeleccionCoordinator();
        coordinador.EstablecerGrid(fake);

        var cambios = coordinador.AplicarASeleccion(new ReportStyleOverlay(Negrita: true));

        Assert.AreEqual(DestinoFormatoGrid.Celdas, coordinador.Destino);
        Assert.AreEqual(3, cambios.Count, "Las direcciones repetidas se deduplican.");
        CollectionAssert.AreEquivalent(
            new[] { "COMP:1", "COMP:2", "COMP:3" },
            cambios.Select(c => c.Direccion.ClaveFila).ToArray());
    }

    // ═══════════════════ Persistencia + undo/reproducir pintado ═══════════════════

    [TestMethod]
    public void AplicarFormato_PersisteYSeRecargaBajoTipoApu()
    {
        using var ctx = CrearContexto();
        var proyectoId = AgregarProyecto(ctx);
        var service = new FormatoReporteService(ctx);
        var overlay = new ReportStyleOverlay(Negrita: true, ColorFondo: "#112233");

        var cambios = new List<CambioFormatoSeleccion>
        {
            new(new ReportCellAddress(Vista, ClaveFilaReporte.Componente(1), "ImporteTotal"), null, overlay),
            new(new ReportCellAddress(Vista, ClaveFilaReporte.Concepto(2), "Descripcion"), null, overlay),
        };

        service.AplicarCeldas(proyectoId, Tipo, FormMatrices.ConstruirLoteCambios(cambios, aplicarAntes: false));

        var recargado = new FormatoReporteService(ctx).CargarCeldas(proyectoId, Tipo);
        Assert.AreEqual(2, recargado.Count);
        var leido = recargado[new ReportCellAddress(Vista, "COMP:1", "ImporteTotal")];
        Assert.IsTrue(leido.Negrita);
        Assert.AreEqual("#112233", leido.ColorFondo);
    }

    [TestMethod]
    public void PipelineSeleccion_PersisteYPintaAlRecargar()
    {
        using var ctx = CrearContexto();
        var proyectoId = AgregarProyecto(ctx);
        var service = new FormatoReporteService(ctx);

        using var grid = CrearGrid("ImporteTotal");
        var i0 = AgregarFila(grid, Componente(1));
        var i1 = AgregarFila(grid, Componente(2));

        var fake = new GridSeleccionFake(grid, new[] { grid.Rows[i0].Cells[0], grid.Rows[i1].Cells[0] });
        var coordinador = new FormatoSeleccionCoordinator();
        coordinador.EstablecerGrid(fake);

        var overlay = new ReportStyleOverlay(Negrita: true, ColorFondo: "#0A0B0C");
        var cambios = coordinador.AplicarASeleccion(overlay);
        service.AplicarCeldas(proyectoId, Tipo, FormMatrices.ConstruirLoteCambios(cambios, aplicarAntes: false));

        var recargado = new FormatoReporteService(ctx).CargarCeldas(proyectoId, Tipo);
        var direccion = new ReportCellAddress(Vista, "COMP:1", "ImporteTotal");
        Assert.IsTrue(recargado.ContainsKey(direccion));

        var estilo = new DataGridViewCellStyle { BackColor = Color.Purple };
        FormPresupuesto.AplicarOverlayACellStyle(estilo, recargado[direccion]);

        Assert.AreEqual(Color.FromArgb(0x0A, 0x0B, 0x0C), estilo.BackColor);
        Assert.IsTrue(estilo.Font!.Bold);
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
                new ReportCellAddress(Vista, ClaveFilaReporte.Componente(i), "ImporteTotal"), null, overlay))
            .ToList();

        var manager = new UndoManager();
        var accion = new CompositeUndoableAction(
            "Formato de 10 celdas",
            cambios.Select(c => new UndoChange(c.Antes, c.Despues)).ToList(),
            _ => service.AplicarCeldas(proyectoId, Tipo, FormMatrices.ConstruirLoteCambios(cambios, aplicarAntes: true)),
            _ => service.AplicarCeldas(proyectoId, Tipo, FormMatrices.ConstruirLoteCambios(cambios, aplicarAntes: false)));

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
    }

    [TestMethod]
    public void EstiloVacioEnElLote_EliminaElOverride()
    {
        using var ctx = CrearContexto();
        var proyectoId = AgregarProyecto(ctx);
        var service = new FormatoReporteService(ctx);

        service.UpsertCelda(proyectoId, Tipo, Vista, "COMP:1", "ImporteTotal", new ReportStyleOverlay(ColorFondo: "#123456"));
        Assert.AreEqual(1, ctx.FormatosCeldaReporte.Count());

        var cambios = new[]
        {
            new CambioFormatoSeleccion(new ReportCellAddress(Vista, "COMP:1", "ImporteTotal"), null, new ReportStyleOverlay()),
        };
        service.AplicarCeldas(proyectoId, Tipo, FormMatrices.ConstruirLoteCambios(cambios, aplicarAntes: false));
        ctx.ChangeTracker.Clear();

        Assert.AreEqual(0, ctx.FormatosCeldaReporte.Count(), "Un overlay vacío elimina la fila de formato.");
    }

    // ═══════════════════ Precedencia de pintado (override > columna) ═══════════════════

    [TestMethod]
    public void AplicarOverlayACellStyle_GanaSobreElEstiloBaseDeColumna()
    {
        var estilo = new DataGridViewCellStyle { BackColor = Color.Purple, ForeColor = Color.White };

        FormPresupuesto.AplicarOverlayACellStyle(estilo, new ReportStyleOverlay(ColorFondo: "#112233", Negrita: true, Wrap: true));

        Assert.AreEqual(Color.FromArgb(0x11, 0x22, 0x33), estilo.BackColor);
        Assert.IsTrue(estilo.Font!.Bold);
        Assert.AreEqual(DataGridViewTriState.True, estilo.WrapMode);
    }

    // ═════════════════════════════════ Fakes ═════════════════════════════════

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
            => FormMatrices.ConstruirDireccionesSeleccionadas(_grid, _celdas);

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
}
