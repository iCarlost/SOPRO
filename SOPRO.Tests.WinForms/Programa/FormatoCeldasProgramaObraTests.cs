using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SOPRO.Application.DTOs.Programacion;
using SOPRO.Application.Models.Reporting.ReportColumns;
using SOPRO.Application.Services.Reporting;
using SOPRO.Application.UseCases.Reporting;
using SOPRO.Data.Context;
using SOPRO.WinForms.Forms;
using SOPRO.WinForms.Helpers;
using SOPRO.WinForms.Undo;

namespace SOPRO.Tests.WinForms.Programa;

/// <summary>
/// Paso 6.2: formato de celdas por SELECCIÓN en el grid del Programa de Obra.
/// Verifica el mapeo selección → direcciones ESTABLES (<c>ACT:{Id}</c> actividades,
/// <c>RES:{Clave}</c> agrupadores), el filtrado de filas sin entidad / columnas no
/// formateables, la persistencia con <see cref="FormatoReporteService"/> y el
/// undo/redo como UNA sola acción compuesta. Mismo patrón que
/// <c>FormatoCeldasIndirectosTests</c>.
/// </summary>
[TestClass]
public class FormatoCeldasProgramaObraTests
{
    private const string Tipo = "ProgramaObra";
    private const string Vista = "";

    // ═════════════════════════════ Infraestructura ═════════════════════════════

    private static SOPROContext CrearContexto()
    {
        var dbPath = Path.Combine(Path.GetTempPath(), $"sopro_fmt_po_{Guid.NewGuid():N}.db");
        var ctx = new SOPROContext(dbPath);
        ctx.Database.EnsureDeleted();
        ctx.Database.EnsureCreated();
        return ctx;
    }

    private static int AgregarProyecto(SOPROContext ctx)
    {
        var proyecto = new SOPRO.Core.Entities.Proyecto
        {
            Nombre = "Proyecto formato programa de obra",
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

    private static readonly string[] ColumnasGrid =
    {
        ProgramaObraReportSnapshotBuilder.ColOrden, ProgramaObraReportSnapshotBuilder.ColClave,
        ProgramaObraReportSnapshotBuilder.ColDescripcion, ProgramaObraReportSnapshotBuilder.ColCantidad,
        ProgramaObraReportSnapshotBuilder.ColImporte,
    };

    private static DataGridView CrearGrid()
    {
        var grid = new DataGridView { AllowUserToAddRows = false };
        foreach (var nombre in ColumnasGrid)
            grid.Columns.Add(new DataGridViewTextBoxColumn { Name = nombre });
        grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "colDesconocida" });
        return grid;
    }

    private static int AgregarFila(DataGridView grid, ActivityGridRowDto? dto)
    {
        var index = grid.Rows.Add();
        grid.Rows[index].Tag = dto;
        return index;
    }

    private static ActivityGridRowDto Actividad(int id, string clave) => new()
    {
        Id = id, Clave = clave, Descripcion = $"Actividad {id}", EsResumen = false, Orden = id
    };

    private static ActivityGridRowDto Resumen(int id, string clave) => new()
    {
        Id = id, Clave = clave, Descripcion = $"Agrupador {clave}", EsResumen = true, Orden = id
    };

    // ═════════════════════════ Selección → direcciones ═════════════════════════

    [TestMethod]
    public void Actividad_GeneraDireccionEstableConClaveAct()
    {
        using var grid = CrearGrid();
        AgregarFila(grid, Resumen(1, "1"));
        var filaAct = AgregarFila(grid, Actividad(22, "1.1"));

        var direcciones = FormProgramaObra.ConstruirDireccionesSeleccionadas(
            grid, new[] { grid.Rows[filaAct].Cells[ProgramaObraReportSnapshotBuilder.ColImporte] });

        Assert.AreEqual(1, direcciones.Count);
        Assert.AreEqual("ACT:22", direcciones[0].ClaveFila);
        Assert.AreEqual(ProgramaObraReportSnapshotBuilder.ColImporte, direcciones[0].NombreColumna);
        Assert.AreEqual(Vista, direcciones[0].ClaveVista);
    }

    [TestMethod]
    public void Resumen_GeneraDireccionEstableConClaveResYLaClave()
    {
        using var grid = CrearGrid();
        var filaRes = AgregarFila(grid, Resumen(3, "2"));
        AgregarFila(grid, Actividad(22, "2.1"));

        var direcciones = FormProgramaObra.ConstruirDireccionesSeleccionadas(
            grid, new[] { grid.Rows[filaRes].Cells[ProgramaObraReportSnapshotBuilder.ColImporte] });

        Assert.AreEqual(1, direcciones.Count);
        Assert.AreEqual("RES:2", direcciones[0].ClaveFila);
    }

    [TestMethod]
    public void FilaSinEntidadYColumnaNoFormateable_SeIgnoran()
    {
        using var grid = CrearGrid();
        var conEntidad = AgregarFila(grid, Actividad(42, "1.1"));
        var sinEntidad = AgregarFila(grid, null);

        var direcciones = FormProgramaObra.ConstruirDireccionesSeleccionadas(grid, new[]
        {
            grid.Rows[conEntidad].Cells[ProgramaObraReportSnapshotBuilder.ColImporte], // válida
            grid.Rows[conEntidad].Cells["colDesconocida"],                             // columna no formateable
            grid.Rows[sinEntidad].Cells[ProgramaObraReportSnapshotBuilder.ColImporte], // fila sin entidad
        });

        Assert.AreEqual(1, direcciones.Count);
        Assert.AreEqual("ACT:42", direcciones[0].ClaveFila);
    }

    [TestMethod]
    public void ObtenerNombreColumnaFormato_MapeaColumnasFormateables()
    {
        using var grid = CrearGrid();

        Assert.AreEqual(ProgramaObraReportSnapshotBuilder.ColOrden,
            FormProgramaObra.ObtenerNombreColumnaFormato(grid, grid.Columns[ProgramaObraReportSnapshotBuilder.ColOrden].Index));
        Assert.AreEqual(ProgramaObraReportSnapshotBuilder.ColImporte,
            FormProgramaObra.ObtenerNombreColumnaFormato(grid, grid.Columns[ProgramaObraReportSnapshotBuilder.ColImporte].Index));
        Assert.IsNull(FormProgramaObra.ObtenerNombreColumnaFormato(grid, grid.Columns["colDesconocida"].Index));
    }

    [TestMethod]
    public void DireccionEstable_NoDependeDelIndiceVisualDeLaFila()
    {
        // El formato se indexa por Id (no por posición): regenerar/reordenar el grid
        // produce la MISMA dirección para la misma actividad.
        using var gridA = CrearGrid();
        AgregarFila(gridA, Resumen(1, "1"));
        var aAct = AgregarFila(gridA, Actividad(22, "1.1"));
        AgregarFila(gridA, Actividad(33, "1.2"));

        using var gridB = CrearGrid();
        AgregarFila(gridB, Actividad(33, "1.2"));
        AgregarFila(gridB, Resumen(1, "1"));
        var bAct = AgregarFila(gridB, Actividad(22, "1.1"));

        var a = FormProgramaObra.ConstruirDireccionesSeleccionadas(
            gridA, new[] { gridA.Rows[aAct].Cells[ProgramaObraReportSnapshotBuilder.ColImporte] });
        var b = FormProgramaObra.ConstruirDireccionesSeleccionadas(
            gridB, new[] { gridB.Rows[bAct].Cells[ProgramaObraReportSnapshotBuilder.ColImporte] });

        Assert.AreEqual(a[0], b[0]);
        Assert.AreEqual("ACT:22", a[0].ClaveFila);
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
            new(new ReportCellAddress(Vista, ClaveFilaReporte.Actividad(1), "colImporte"), null, overlay),
            new(new ReportCellAddress(Vista, ClaveFilaReporte.Resumen("2"), "colCantidad"), null, overlay),
        };

        service.AplicarCeldas(proyectoId, Tipo, FormProgramaObra.ConstruirLoteCambios(cambios, aplicarAntes: false));

        var recargado = new FormatoReporteService(ctx).CargarCeldas(proyectoId, Tipo);

        Assert.AreEqual(2, recargado.Count);
        var leido = recargado[new ReportCellAddress(Vista, "ACT:1", "colImporte")];
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
                new ReportCellAddress(Vista, ClaveFilaReporte.Actividad(i), "colImporte"), null, overlay))
            .ToList();

        var manager = new UndoManager();
        var accion = new CompositeUndoableAction(
            "Formato de 10 celdas",
            cambios.Select(c => new UndoChange(c.Antes, c.Despues)).ToList(),
            _ => service.AplicarCeldas(proyectoId, Tipo, FormProgramaObra.ConstruirLoteCambios(cambios, aplicarAntes: true)),
            _ => service.AplicarCeldas(proyectoId, Tipo, FormProgramaObra.ConstruirLoteCambios(cambios, aplicarAntes: false)));

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

        service.UpsertCelda(proyectoId, Tipo, Vista, "ACT:1", "colImporte", new ReportStyleOverlay(ColorFondo: "#123456"));
        Assert.AreEqual(1, ctx.FormatosCeldaReporte.Count());

        var cambios = new[]
        {
            new CambioFormatoSeleccion(new ReportCellAddress(Vista, "ACT:1", "colImporte"), null, new ReportStyleOverlay()),
        };
        service.AplicarCeldas(proyectoId, Tipo, FormProgramaObra.ConstruirLoteCambios(cambios, aplicarAntes: false));
        ctx.ChangeTracker.Clear();

        Assert.AreEqual(0, ctx.FormatosCeldaReporte.Count(), "Un overlay vacío elimina la fila de formato.");
    }

    [TestMethod]
    public void AplicarOverlayACellStyle_GanaSobreElEstiloBaseHardcodeado()
    {
        var estilo = new DataGridViewCellStyle
        {
            BackColor = System.Drawing.Color.Purple,
            ForeColor = System.Drawing.Color.White
        };

        FormPresupuesto.AplicarOverlayACellStyle(
            estilo, new ReportStyleOverlay(ColorFondo: "#112233", Negrita: true, Wrap: true));

        Assert.AreEqual(System.Drawing.Color.FromArgb(0x11, 0x22, 0x33), estilo.BackColor,
            "El override de celda gana sobre el color de rol/hardcodeado.");
        Assert.IsTrue(estilo.Font!.Bold);
        Assert.AreEqual(DataGridViewTriState.True, estilo.WrapMode);
    }
}
