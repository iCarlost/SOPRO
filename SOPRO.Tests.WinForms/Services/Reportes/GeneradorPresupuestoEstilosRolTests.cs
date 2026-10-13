using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using ClosedXML.Excel;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MigraDoc.DocumentObjectModel;
using MigraDoc.DocumentObjectModel.Tables;
using SOPRO.Application.Models.Reporting.ReportColumns;
using SOPRO.Application.Services.Reporting;
using SOPRO.Application.UseCases.Reporting;
using SOPRO.Core.Entities;
using SOPRO.Data.Context;
using SOPRO.WinForms.Forms;
using SOPRO.WinForms.Services;

namespace SOPRO.Tests.WinForms.Services.Reportes;

/// <summary>
/// Paso 4.3 — DEFAULT NEUTRAL de estilo por ROL de fila compartido por los TRES
/// renderers (grid, PDF, Excel). Las filas agrupador ya no llevan color
/// hardcodeado: lo toman de <see cref="EstilosFilaPredeterminados"/> (Capítulo
/// #800080, etc.) combinado con la config persistida del usuario, y el override de
/// celda del paso 4.1 sigue ganando.
///
/// Incluye el test de PARIDAD: mismo rol + misma config ⇒ el MISMO color resuelto
/// en grid, PDF y Excel.
/// </summary>
[TestClass]
public class GeneradorPresupuestoEstilosRolTests
{
    private const int CapituloId = 201;
    private const int ConceptoId = 202;
    private const string ColorCapituloDefault = "#800080";
    private const string ColorUsuario = "#010203";
    private const string ColorOverrideCelda = "#ABCDEF";

    // ─────────────────────────── Contexto / fixtures ───────────────────────────

    private static SOPROContext CreateContext()
    {
        var dbPath = Path.Combine(Path.GetTempPath(), $"sopro_rol_{Guid.NewGuid():N}.db");
        var ctx = new SOPROContext(dbPath);
        ctx.Database.EnsureDeleted();
        ctx.Database.EnsureCreated();
        return ctx;
    }

    private static Proyecto CrearProyecto(SOPROContext ctx)
    {
        var proyecto = new Proyecto
        {
            Nombre = "Proyecto rol",
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
            PorcentajeIVA = 16m,
            DecimalesCantidad = 2,
            DecimalesImporte = 2,
            DecimalesPorcentaje = 4,
        };
        ctx.Proyectos.Add(proyecto);
        ctx.SaveChanges();
        return proyecto;
    }

    private static ColumnaPersonalizada ColumnaP(string nombreInterno, string nombre, int orden, TipoDatoColumna tipo)
        => new()
        {
            ProyectoId = null,
            NombreInterno = nombreInterno,
            Nombre = nombre,
            Orden = orden,
            AnchoColumna = 100,
            TipoDato = tipo,
            WrapTexto = false,
            FormatoNumerico = string.Empty,
        };

    private static ReportColumnSnapshot SnapshotConColumnas(int proyectoId)
        => new PresupuestoReportSnapshotBuilder().Build(
            proyectoId,
            "Presupuesto de prueba",
            new[]
            {
                ColumnaP("Clave", "Clave", 0, TipoDatoColumna.Texto),
                ColumnaP("Descripcion", "Descripción", 1, TipoDatoColumna.Texto),
                ColumnaP("ImporteTotal", "Importe", 2, TipoDatoColumna.Moneda),
            });

    /// <summary>Jerarquía mínima: un Capítulo (Nivel 0) con un concepto terminal.</summary>
    private static List<ConceptoPresupuesto> CrearJerarquia(Proyecto proyecto) => new()
    {
        new ConceptoPresupuesto
        {
            Id = CapituloId,
            ProyectoId = proyecto.Id,
            Clave = "1",
            Descripcion = "Capítulo Uno",
            EsAgrupador = true,
            Nivel = 0,
            Orden = 0,
        },
        new ConceptoPresupuesto
        {
            Id = ConceptoId,
            ProyectoId = proyecto.Id,
            Clave = "1.1",
            Descripcion = "Concepto Uno",
            Unidad = "pza",
            Cantidad = 2m,
            CostoDirectoUnitario = 50m,
            PrecioUnitario = 50m,
            ImporteTotal = 100m,
            Orden = 1,
            Nivel = 1,
        },
    };

    private static ReportColumnSnapshot ConRolUsuario(ReportColumnSnapshot snapshot, string rol, ReportStyleOverlay overlay)
        => snapshot with { EstilosFila = new[] { new ReportRowRoleStyle(rol, overlay) } };

    private static ReportColumnSnapshot ConOverrideCelda(ReportColumnSnapshot snapshot, string claveFila, string columna, ReportStyleOverlay overlay)
        => snapshot with
        {
            EstilosCelda = new[] { new ReportCellStyleOverride(new ReportCellAddress(PresupuestoReportSnapshotBuilder.ClaveVista, claveFila, columna), overlay) },
        };

    // ─────────────────────────── PDF ───────────────────────────

    [TestMethod]
    public void Pdf_AgrupadorSinConfig_UsaElDefaultDelRol()
    {
        using var ctx = CreateContext();
        var proyecto = CrearProyecto(ctx);
        var svc = new ReporteService(ctx);
        var snapshot = SnapshotConColumnas(proyecto.Id);

        var doc = new GeneradorPdfPresupuesto(svc).ConstruirDocumento(proyecto, CrearJerarquia(proyecto), svc.ObtenerPlantillaSoloLectura(proyecto.Id), snapshot);

        Assert.AreEqual(ColorTranslator.FromHtml(ColorCapituloDefault).ToArgb(), ShadingCapítulo(doc, snapshot));
    }

    [TestMethod]
    public void Pdf_AgrupadorConConfigDelUsuario_UsaElDelUsuario()
    {
        using var ctx = CreateContext();
        var proyecto = CrearProyecto(ctx);
        var svc = new ReporteService(ctx);
        var snapshot = ConRolUsuario(SnapshotConColumnas(proyecto.Id), RolFilaReporte.Capitulo, new ReportStyleOverlay(ColorFondo: ColorUsuario));

        var doc = new GeneradorPdfPresupuesto(svc).ConstruirDocumento(proyecto, CrearJerarquia(proyecto), svc.ObtenerPlantillaSoloLectura(proyecto.Id), snapshot);

        Assert.AreEqual(ColorTranslator.FromHtml(ColorUsuario).ToArgb(), ShadingCapítulo(doc, snapshot));
    }

    [TestMethod]
    public void Pdf_OverrideDeCelda_GanaSobreElDefaultDelRol()
    {
        using var ctx = CreateContext();
        var proyecto = CrearProyecto(ctx);
        var svc = new ReporteService(ctx);
        var snapshot = ConOverrideCelda(
            SnapshotConColumnas(proyecto.Id),
            ClaveFilaReporte.Subtotal("1"),
            "ImporteTotal",
            new ReportStyleOverlay(ColorFondo: ColorOverrideCelda));

        var doc = new GeneradorPdfPresupuesto(svc).ConstruirDocumento(proyecto, CrearJerarquia(proyecto), svc.ObtenerPlantillaSoloLectura(proyecto.Id), snapshot);

        Assert.AreEqual(ColorTranslator.FromHtml(ColorOverrideCelda).ToArgb(), ShadingCapítulo(doc, snapshot));
    }

    // ─────────────────────────── Excel ───────────────────────────

    [TestMethod]
    public void Excel_AgrupadorSinConfig_UsaElDefaultDelRol()
    {
        using var ctx = CreateContext();
        var proyecto = CrearProyecto(ctx);
        var ruta = GenerarExcel(ctx, proyecto, SnapshotConColumnas(proyecto.Id));
        try
        {
            Assert.AreEqual(ColorTranslator.FromHtml(ColorCapituloDefault).ToArgb(), FondoCapítuloExcel(ruta));
        }
        finally { Borrar(ruta); }
    }

    [TestMethod]
    public void Excel_AgrupadorConConfigDelUsuario_UsaElDelUsuario()
    {
        using var ctx = CreateContext();
        var proyecto = CrearProyecto(ctx);
        var snapshot = ConRolUsuario(SnapshotConColumnas(proyecto.Id), RolFilaReporte.Capitulo, new ReportStyleOverlay(ColorFondo: ColorUsuario));
        var ruta = GenerarExcel(ctx, proyecto, snapshot);
        try
        {
            Assert.AreEqual(ColorTranslator.FromHtml(ColorUsuario).ToArgb(), FondoCapítuloExcel(ruta));
        }
        finally { Borrar(ruta); }
    }

    [TestMethod]
    public void Excel_OverrideDeCelda_GanaSobreElDefaultDelRol()
    {
        using var ctx = CreateContext();
        var proyecto = CrearProyecto(ctx);
        var snapshot = ConOverrideCelda(
            SnapshotConColumnas(proyecto.Id),
            ClaveFilaReporte.Subtotal("1"),
            "ImporteTotal",
            new ReportStyleOverlay(ColorFondo: ColorOverrideCelda));
        var ruta = GenerarExcel(ctx, proyecto, snapshot);
        try
        {
            Assert.AreEqual(ColorTranslator.FromHtml(ColorOverrideCelda).ToArgb(), FondoCapítuloExcel(ruta));
        }
        finally { Borrar(ruta); }
    }

    // ─────────────────────────── Paridad de los 3 renderers ───────────────────────────

    [TestMethod]
    public void Paridad_TresRenderers_MismoRolMismaConfig_MismoColor()
    {
        using var ctx = CreateContext();
        var proyecto = CrearProyecto(ctx);
        var svc = new ReporteService(ctx);
        var overlay = new ReportStyleOverlay(ColorFondo: ColorUsuario, Negrita: true);
        var snapshot = ConRolUsuario(SnapshotConColumnas(proyecto.Id), RolFilaReporte.Capitulo, overlay);
        int esperado = ColorTranslator.FromHtml(ColorUsuario).ToArgb();

        // GRID
        var estiloGrid = new System.Windows.Forms.DataGridViewCellStyle();
        var usuarioGrid = new Dictionary<string, ReportStyleOverlay>(StringComparer.OrdinalIgnoreCase)
        {
            [RolFilaReporte.Capitulo] = overlay,
        };
        FormPresupuesto.AplicarEstiloRolPresupuesto(estiloGrid, new System.Drawing.Font("Segoe UI", 9f), "Capitulo", usuarioGrid);
        Assert.AreEqual(esperado, estiloGrid.BackColor.ToArgb(), "grid");

        // PDF
        var doc = new GeneradorPdfPresupuesto(svc).ConstruirDocumento(proyecto, CrearJerarquia(proyecto), svc.ObtenerPlantillaSoloLectura(proyecto.Id), snapshot);
        Assert.AreEqual(esperado, ShadingCapítulo(doc, snapshot), "PDF");

        // EXCEL
        var ruta = GenerarExcel(ctx, proyecto, snapshot);
        try
        {
            Assert.AreEqual(esperado, FondoCapítuloExcel(ruta), "Excel");
        }
        finally { Borrar(ruta); }
    }

    // ─────────────────────────── Helpers ───────────────────────────

    private static string GenerarExcel(SOPROContext ctx, Proyecto proyecto, ReportColumnSnapshot snapshot)
    {
        var svc = new ReporteService(ctx);
        var ruta = Path.Combine(Path.GetTempPath(), $"sopro_rol_{Guid.NewGuid():N}.xlsx");
        new GeneradorExcelPresupuesto(svc).Generar(proyecto, CrearJerarquia(proyecto), svc.ObtenerPlantillaSoloLectura(proyecto.Id), snapshot, ruta, factorPU: 1m);
        return ruta;
    }

    private static void Borrar(string ruta)
    {
        try { if (File.Exists(ruta)) File.Delete(ruta); } catch { /* best effort */ }
    }

    /// <summary>Shading del Capítulo (primera fila de datos), en la columna Importe.</summary>
    private static int ShadingCapítulo(Document doc, ReportColumnSnapshot snapshot)
    {
        var table = TablaCuerpo(doc);
        int colImporte = ColumnaImporteEnTabla(snapshot);
        var color = table.Rows[1].Cells[colImporte].Shading.Color;
        return System.Drawing.Color.FromArgb((int)color.R, (int)color.G, (int)color.B).ToArgb();
    }

    private static int FondoCapítuloExcel(string ruta)
    {
        using var wb = new XLWorkbook(ruta);
        var ws = wb.Worksheet(1);
        var (filaTitulos, _, colImporte) = LocalizarTitulos(ws);
        var celda = ws.Cell(filaTitulos + 1, colImporte);
        return celda.Style.Fill.BackgroundColor.Color.ToArgb();
    }

    private static Table TablaCuerpo(Document doc)
        => doc.Sections[0].Elements.OfType<Table>().First();

    private static int ColumnaImporteEnTabla(ReportColumnSnapshot snapshot)
    {
        var cols = SOPRO.Reporting.Columns.ReportColumnDefaults.ConPrefijoNumeroPresupuesto(
            snapshot.Columnas.Where(c => c.Visible).OrderBy(c => c.Orden).ToList());
        return cols.FindIndex(c => c.Identificador == "ImporteTotal");
    }

    private static (int FilaTitulos, int ColClave, int ColImporte) LocalizarTitulos(IXLWorksheet ws)
    {
        IXLCell? celdaImporte = null;
        IXLCell? celdaClave = null;
        foreach (var celda in ws.CellsUsed())
        {
            var texto = celda.GetString();
            if (texto == "Importe") celdaImporte = celda;
            else if (texto == "Clave") celdaClave = celda;
        }

        Assert.IsNotNull(celdaImporte, "La hoja debe contener el encabezado 'Importe'.");
        Assert.IsNotNull(celdaClave, "La hoja debe contener el encabezado 'Clave'.");
        return (celdaImporte!.Address.RowNumber, celdaClave!.Address.ColumnNumber, celdaImporte.Address.ColumnNumber);
    }
}
