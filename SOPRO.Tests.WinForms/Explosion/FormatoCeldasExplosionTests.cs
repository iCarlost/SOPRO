using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SOPRO.Application.Models.Explosion;
using SOPRO.Application.Models.Reporting.ReportColumns;
using SOPRO.Application.Services.Reporting;
using SOPRO.Core.Entities;
using SOPRO.Data.Context;
using SOPRO.WinForms.Forms;
using SOPRO.WinForms.Helpers;
using SOPRO.WinForms.Services;
using SOPRO.WinForms.Undo;

namespace SOPRO.Tests.WinForms.Explosion;

/// <summary>
/// Paso 6.1: formato de celdas por SELECCIÓN en el grid de Explosión de Insumos.
/// Verifica el mapeo selección → direcciones ESTABLES (claves <c>SEC:</c>,
/// <c>D:seccion:clave</c>, <c>T:seccion</c>, <c>TOTALGENERAL</c>, <c>REF:n</c>), el
/// pintado por ROL (defaults compartidos con PDF/Excel), el filtrado de filas sin
/// clave / columnas no formateables, la persistencia con
/// <see cref="FormatoReporteService"/> y el undo/redo como UNA sola acción
/// compuesta. Mismo patrón que <c>FormatoCeldasIndirectosTests</c>.
/// </summary>
[TestClass]
public class FormatoCeldasExplosionTests
{
    private const string Tipo = "Explosion";
    private const string Vista = "";

    // ═════════════════════════════ Infraestructura ═════════════════════════════

    private static DataGridView CrearGrid()
    {
        var grid = new DataGridView { AllowUserToAddRows = false };
        grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "colClave" });
        grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "colDescripcion" });
        grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "colUnidad" });
        grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "colCantidad" });
        grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "colPrecioUnitario" });
        grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "colImporte" });
        grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "colPorcentaje" });
        grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "colDummy" });
        return grid;
    }

    /// <summary>
    /// Construye las filas con el MISMO servicio que usa el form, de modo que los
    /// tags de identidad estable y el pintado por rol se ejerzan de verdad.
    /// </summary>
    private static void AgregarFilas(DataGridView grid)
    {
        var rows = new List<ExplosionRowDisplay>
        {
            new() { Kind = ExplosionRowKind.Encabezado, Descripcion = "MATERIALES" },
            new() { Kind = ExplosionRowKind.Detalle, Clave = "MAT-01", Descripcion = "Cemento" },
            new() { Kind = ExplosionRowKind.Total, Descripcion = "TOTAL MATERIALES" },
            new() { Kind = ExplosionRowKind.TotalGeneral, Descripcion = "TOTAL DEL REPORTE" },
            new() { Kind = ExplosionRowKind.Referencia, Descripcion = "Costo Directo (Presupuesto)" },
        };

        var service = new ExplosionGridRenderService();
        grid.Rows.AddRange(service.BuildRows(grid, rows));
    }

    private static SOPROContext CrearContexto()
    {
        var dbPath = Path.Combine(Path.GetTempPath(), $"sopro_fmt_expl_{Guid.NewGuid():N}.db");
        var ctx = new SOPROContext(dbPath);
        ctx.Database.EnsureDeleted();
        ctx.Database.EnsureCreated();
        return ctx;
    }

    private static int AgregarProyecto(SOPROContext ctx)
    {
        var proyecto = new Proyecto
        {
            Nombre = "Proyecto formato explosión",
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

    // ═════════════════════════ Identidad estable + mapeo ═════════════════════════

    [TestMethod]
    public void FilasGeneranClavesEstablesPorTipo()
    {
        using var grid = CrearGrid();
        AgregarFilas(grid);

        Assert.AreEqual("SEC:MATERIALES", FormExplosionInsumos.ObtenerClaveFilaFormato(grid, 0));
        Assert.AreEqual("D:MATERIALES:MAT-01", FormExplosionInsumos.ObtenerClaveFilaFormato(grid, 1));
        Assert.AreEqual("T:MATERIALES", FormExplosionInsumos.ObtenerClaveFilaFormato(grid, 2));
        Assert.AreEqual("TOTALGENERAL", FormExplosionInsumos.ObtenerClaveFilaFormato(grid, 3));
        Assert.AreEqual("REF:0", FormExplosionInsumos.ObtenerClaveFilaFormato(grid, 4));
    }

    [TestMethod]
    public void ObtenerNombreColumnaFormato_MapeaColumnasYAliasImporte()
    {
        using var grid = CrearGrid();

        Assert.AreEqual("Clave", FormExplosionInsumos.ObtenerNombreColumnaFormato(grid, grid.Columns["colClave"].Index));
        Assert.AreEqual("Descripcion", FormExplosionInsumos.ObtenerNombreColumnaFormato(grid, grid.Columns["colDescripcion"].Index));
        Assert.AreEqual("Unidad", FormExplosionInsumos.ObtenerNombreColumnaFormato(grid, grid.Columns["colUnidad"].Index));
        Assert.AreEqual("Cantidad", FormExplosionInsumos.ObtenerNombreColumnaFormato(grid, grid.Columns["colCantidad"].Index));
        Assert.AreEqual("PrecioUnitario", FormExplosionInsumos.ObtenerNombreColumnaFormato(grid, grid.Columns["colPrecioUnitario"].Index));
        Assert.AreEqual("ImporteTotal", FormExplosionInsumos.ObtenerNombreColumnaFormato(grid, grid.Columns["colImporte"].Index));
        Assert.AreEqual("Porcentaje", FormExplosionInsumos.ObtenerNombreColumnaFormato(grid, grid.Columns["colPorcentaje"].Index));
        Assert.IsNull(FormExplosionInsumos.ObtenerNombreColumnaFormato(grid, grid.Columns["colDummy"].Index));
    }

    [TestMethod]
    public void Detalle_GeneraDireccionEstableConClaveYColumna()
    {
        using var grid = CrearGrid();
        AgregarFilas(grid);

        var direcciones = FormExplosionInsumos.ConstruirDireccionesSeleccionadas(
            grid, new[] { grid.Rows[1].Cells["colImporte"] });

        Assert.AreEqual(1, direcciones.Count);
        Assert.AreEqual("D:MATERIALES:MAT-01", direcciones[0].ClaveFila);
        Assert.AreEqual("ImporteTotal", direcciones[0].NombreColumna);
        Assert.AreEqual(Vista, direcciones[0].ClaveVista);
    }

    [TestMethod]
    public void BandaDeSeccion_GeneraDireccionEstable()
    {
        using var grid = CrearGrid();
        AgregarFilas(grid);

        var direcciones = FormExplosionInsumos.ConstruirDireccionesSeleccionadas(
            grid, new[] { grid.Rows[0].Cells["colDescripcion"] });

        Assert.AreEqual(1, direcciones.Count);
        Assert.AreEqual("SEC:MATERIALES", direcciones[0].ClaveFila);
        Assert.AreEqual("Descripcion", direcciones[0].NombreColumna);
    }

    [TestMethod]
    public void FilaSinClaveYColumnaNoFormateable_SeIgnoran()
    {
        using var grid = CrearGrid();
        AgregarFilas(grid);

        // La columna de relleno no es formateable; se descarta.
        var direcciones = FormExplosionInsumos.ConstruirDireccionesSeleccionadas(grid, new[]
        {
            grid.Rows[1].Cells["colImporte"],   // válida
            grid.Rows[1].Cells["colDummy"],     // columna no formateable
        });

        Assert.AreEqual(1, direcciones.Count);
        Assert.AreEqual("D:MATERIALES:MAT-01", direcciones[0].ClaveFila);
    }

    // ═══════════════════════ Pintado por ROL de fila ═══════════════════════

    [TestMethod]
    public void BuildRows_AplicaColoresDeRolDelGrid()
    {
        using var grid = CrearGrid();
        AgregarFilas(grid);

        Assert.AreEqual(Color.FromArgb(0x46, 0x82, 0xB4), grid.Rows[0].DefaultCellStyle.BackColor, "Banda de sección.");
        Assert.AreEqual(Color.FromArgb(0xF0, 0xF0, 0xF0), grid.Rows[2].DefaultCellStyle.BackColor, "Total de sección.");
        Assert.AreEqual(Color.FromArgb(0x2D, 0x2D, 0x30), grid.Rows[3].DefaultCellStyle.BackColor, "Total general.");
        Assert.AreEqual(Color.FromArgb(0xF0, 0xF0, 0xF0), grid.Rows[4].DefaultCellStyle.BackColor, "Referencia.");
        Assert.AreEqual(Color.FromArgb(0x64, 0x64, 0x64), grid.Rows[4].DefaultCellStyle.ForeColor, "Texto de referencia.");
        Assert.AreEqual(Color.Empty, grid.Rows[1].DefaultCellStyle.BackColor, "El detalle hereda el color de la columna.");
    }

    [TestMethod]
    public void BuildRows_OverrideDeRol_USuarioGanaSobreElDefault()
    {
        using var grid = CrearGrid();
        var usuario = new Dictionary<string, ReportStyleOverlay>(StringComparer.OrdinalIgnoreCase)
        {
            [RolFilaReporte.Encabezado] = new ReportStyleOverlay(ColorFondo: "#010203"),
        };

        var service = new ExplosionGridRenderService();
        var rows = new List<ExplosionRowDisplay>
        {
            new() { Kind = ExplosionRowKind.Encabezado, Descripcion = "MATERIALES" },
        };
        grid.Rows.AddRange(service.BuildRows(grid, rows, usuario));

        Assert.AreEqual(Color.FromArgb(1, 2, 3), grid.Rows[0].DefaultCellStyle.BackColor,
            "El override del usuario gana sobre el default del rol.");
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
            new(new ReportCellAddress(Vista, "D:MATERIALES:MAT-01", "ImporteTotal"), null, overlay),
            new(new ReportCellAddress(Vista, "SEC:MATERIALES", "Descripcion"), null, overlay),
        };

        service.AplicarCeldas(proyectoId, Tipo, FormExplosionInsumos.ConstruirLoteCambios(cambios, aplicarAntes: false));

        var recargado = new FormatoReporteService(ctx).CargarCeldas(proyectoId, Tipo);

        Assert.AreEqual(2, recargado.Count);
        var leido = recargado[new ReportCellAddress(Vista, "D:MATERIALES:MAT-01", "ImporteTotal")];
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
                new ReportCellAddress(Vista, $"D:MATERIALES:MAT-{i:00}", "ImporteTotal"), null, overlay))
            .ToList();

        var manager = new UndoManager();
        var accion = new CompositeUndoableAction(
            "Formato de 10 celdas",
            cambios.Select(c => new UndoChange(c.Antes, c.Despues)).ToList(),
            _ => service.AplicarCeldas(proyectoId, Tipo, FormExplosionInsumos.ConstruirLoteCambios(cambios, aplicarAntes: true)),
            _ => service.AplicarCeldas(proyectoId, Tipo, FormExplosionInsumos.ConstruirLoteCambios(cambios, aplicarAntes: false)));

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

        service.UpsertCelda(proyectoId, Tipo, Vista, "D:MATERIALES:MAT-01", "ImporteTotal",
            new ReportStyleOverlay(ColorFondo: "#123456"));
        Assert.AreEqual(1, ctx.FormatosCeldaReporte.Count());

        var cambios = new[]
        {
            new CambioFormatoSeleccion(new ReportCellAddress(Vista, "D:MATERIALES:MAT-01", "ImporteTotal"), null, new ReportStyleOverlay()),
        };
        service.AplicarCeldas(proyectoId, Tipo, FormExplosionInsumos.ConstruirLoteCambios(cambios, aplicarAntes: false));
        ctx.ChangeTracker.Clear();

        Assert.AreEqual(0, ctx.FormatosCeldaReporte.Count(), "Un overlay vacío elimina la fila de formato.");
    }

    // ═══════════════════ Precedencia de pintado (override > default) ═══════════════════

    [TestMethod]
    public void AplicarOverlayACellStyle_GanaSobreElEstiloBaseDelRol()
    {
        var estilo = new DataGridViewCellStyle { BackColor = Color.FromArgb(0x46, 0x82, 0xB4) };

        FormPresupuesto.AplicarOverlayACellStyle(
            estilo, new ReportStyleOverlay(ColorFondo: "#112233", Negrita: true, Wrap: true));

        Assert.AreEqual(Color.FromArgb(0x11, 0x22, 0x33), estilo.BackColor,
            "El override de celda gana sobre el color de rol/hardcodeado.");
        Assert.IsTrue(estilo.Font!.Bold);
        Assert.AreEqual(DataGridViewTriState.True, estilo.WrapMode);
    }
}
