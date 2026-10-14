using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using ClosedXML.Excel;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MigraDoc.DocumentObjectModel;
using MigraDoc.DocumentObjectModel.Tables;
using SOPRO.Application.Models.Reporting.ReportColumns;
using SOPRO.Application.Services;
using SOPRO.Application.Services.Reporting;
using SOPRO.Application.UseCases.Reporting;
using SOPRO.Core.Entities;
using SOPRO.Data.Context;
using SOPRO.WinForms.Forms;
using SOPRO.WinForms.Services;

namespace SOPRO.Tests.WinForms.Services.Reportes;

/// <summary>
/// Paso 5.1 — estilos por celda/fila de Indirectos que viajan por el SNAPSHOT
/// NEUTRO hasta PDF (MigraDoc) y Excel (ClosedXML), con paridad con el grid.
///
/// Cubre, sobre el MISMO snapshot con overrides:
/// <list type="bullet">
///   <item>PDF: el SEAM <c>ConstruirDocumento</c> permite inspeccionar el shading
///   real de la celda antes de serializar;</item>
///   <item>Excel: el .xlsx se abre con ClosedXML y se lee fuente, relleno y
///   alineación de la celda afectada;</item>
///   <item>rol Grupo: el default neutral (#E6E6E6 + negrita) se aplica en grid,
///   PDF y Excel (convergencia del gris, antes PDF/Excel usaban #E8E8E8);</item>
///   <item>precedencia: override de celda &gt; rol del usuario &gt; default &gt; columna;</item>
///   <item>guard: ningún generador de Indirectos recibe ni accede a un
///   <c>DataGridView</c>.</item>
/// </list>
/// </summary>
[TestClass]
public class GeneradorIndirectosEstilosTests
{
    private const int GrupoId = 201;
    private const int ConceptoId = 101;
    private const string ColorGrupoDefault = "#E6E6E6";
    private const string ColorUsuario = "#010203";
    private const string ColorOverrideCelda = "#FF00AA";

    // ─────────────────────────── Contexto / fixtures ───────────────────────────

    private static SOPROContext CreateContext()
    {
        var dbPath = Path.Combine(Path.GetTempPath(), $"sopro_ind_estilos_{Guid.NewGuid():N}.db");
        var ctx = new SOPROContext(dbPath);
        ctx.Database.EnsureDeleted();
        ctx.Database.EnsureCreated();
        return ctx;
    }

    private static Proyecto CrearProyecto(SOPROContext ctx)
    {
        var proyecto = new Proyecto
        {
            Nombre = "Proyecto indirectos estilos",
            Descripcion = string.Empty,
            Ubicacion = string.Empty,
            Convocante = string.Empty,
            Contratista = string.Empty,
            ApoderadoLegal = string.Empty,
            FechaInicio = new DateTime(2026, 1, 1),
            FechaTermino = new DateTime(2026, 1, 31),
            PlazoEjecucion = 31,
            PorcentajeIVA = 16m,
            DecimalesCantidad = 2,
            DecimalesImporte = 2,
            DecimalesPorcentaje = 4,
        };
        ctx.Proyectos.Add(proyecto);
        ctx.SaveChanges();
        return proyecto;
    }

    private static ReportColumnSnapshot SnapshotBase(int proyectoId)
        => IndirectosExportResolver.BuildSnapshot(proyectoId, "Análisis de Costos Indirectos", columnas: null);

    private static ReportColumnSnapshot SnapshotConOverrideCelda(int proyectoId)
        => SnapshotBase(proyectoId) with
        {
            EstilosCelda = new[]
            {
                new ReportCellStyleOverride(
                    new ReportCellAddress(IndirectosReportSnapshotBuilder.ClaveVista, ClaveFilaReporte.ConceptoIndirecto(ConceptoId), "ImporteTotal"),
                    new ReportStyleOverlay(
                        Fuente: "Arial",
                        TamanoFuente: 12f,
                        Negrita: true,
                        Cursiva: true,
                        ColorFuente: "#112233",
                        ColorFondo: ColorOverrideCelda,
                        AlineacionHorizontal: ReportTextAlignment.Centro)),
            },
        };

    private static ReportColumnSnapshot SnapshotConRolUsuario(int proyectoId, string rol, ReportStyleOverlay overlay)
        => SnapshotBase(proyectoId) with { EstilosFila = new[] { new ReportRowRoleStyle(rol, overlay) } };

    private static List<GrupoIndirecto> CrearGrupos()
    {
        var grupo = new GrupoIndirecto
        {
            Id = GrupoId,
            Nombre = "Honorarios",
            Tipo = TipoIndirecto.OficinaCentral,
            Orden = 1,
            Conceptos = new List<ConceptoIndirecto>
            {
                new()
                {
                    Id = ConceptoId,
                    Concepto = "Gerente",
                    Tipo = TipoIndirecto.OficinaCentral,
                    ImporteMensual = 1000m,
                    DuracionMeses = 12,
                    Orden = 1,
                    Activo = true,
                },
            },
        };
        return new List<GrupoIndirecto> { grupo };
    }

    private static ConfiguracionIndirectos CrearConfig() => new()
    {
        TotalOficinaCentralAnual = 12000m,
        TotalCampo = 0m,
        PorcentajeOficinaCentral = 1.5m,
        PorcentajeCampo = 0m,
        CostoDirectoObra = 100000m,
        VolumenAnualObra = 800000m,
    };

    // ─────────────────────────── PDF (MigraDoc seam) ───────────────────────────

    [TestMethod]
    public void Pdf_CeldaConOverride_SerializaElShadingYElRolGrupoAplicaAlResto()
    {
        using var ctx = CreateContext();
        var proyecto = CrearProyecto(ctx);
        var svc = new ReporteService(ctx);
        var plantilla = svc.ObtenerPlantillaSoloLectura(proyecto.Id);
        var snapshot = SnapshotConOverrideCelda(proyecto.Id);

        var doc = new GeneradorPdfIndirectos(svc).ConstruirDocumento(
            proyecto, CrearGrupos(), new List<GrupoIndirecto>(), CrearConfig(), plantilla, snapshot);

        var table = TablaColumnas(doc);
        int colImporte = ColumnaImporteTotal(snapshot);

        // Fila del grupo: default del rol Grupo (#E6E6E6).
        var celdaGrupo = table.Rows[FilaGrupo].Cells[colImporte].Shading.Color;
        Assert.IsTrue(celdaGrupo.R == 0xE6 && celdaGrupo.G == 0xE6 && celdaGrupo.B == 0xE6,
            "El default del rol Grupo (#E6E6E6) debe llegar al shading de MigraDoc.");

        // Fila del concepto: el override de celda GANA (#FF00AA).
        var celdaConcepto = table.Rows[FilaConcepto].Cells[colImporte].Shading.Color;
        Assert.IsTrue(celdaConcepto.R == 0xFF && celdaConcepto.G == 0x00 && celdaConcepto.B == 0xAA,
            "El fondo del override (#FF00AA) debe llegar al shading de MigraDoc.");
    }

    [TestMethod]
    public void Pdf_SinOverrides_AplicaElGrisDelRolGrupoYConservaElConcepto()
    {
        using var ctx = CreateContext();
        var proyecto = CrearProyecto(ctx);
        var svc = new ReporteService(ctx);
        var plantilla = svc.ObtenerPlantillaSoloLectura(proyecto.Id);
        var snapshot = SnapshotBase(proyecto.Id);

        var doc = new GeneradorPdfIndirectos(svc).ConstruirDocumento(
            proyecto, CrearGrupos(), new List<GrupoIndirecto>(), CrearConfig(), plantilla, snapshot);

        var table = TablaColumnas(doc);
        int colImporte = ColumnaImporteTotal(snapshot);

        var celdaGrupo = table.Rows[FilaGrupo].Cells[colImporte].Shading.Color;
        Assert.IsTrue(celdaGrupo.R == 0xE6 && celdaGrupo.G == 0xE6 && celdaGrupo.B == 0xE6,
            "Sin overrides, el grupo usa el gris neutral #E6E6E6 (antes #E8E8E8).");

        // El concepto conserva el fondo blanco de su columna.
        var celdaConcepto = table.Rows[FilaConcepto].Cells[colImporte].Shading.Color;
        Assert.IsTrue(celdaConcepto.R == 0xFF && celdaConcepto.G == 0xFF && celdaConcepto.B == 0xFF,
            "Sin overrides, el concepto conserva el fondo blanco de la columna.");
    }

    [TestMethod]
    public void Pdf_RolGrupoConConfigDelUsuario_GanaElUsuario()
    {
        using var ctx = CreateContext();
        var proyecto = CrearProyecto(ctx);
        var svc = new ReporteService(ctx);
        var snapshot = SnapshotConRolUsuario(proyecto.Id, RolFilaReporte.Grupo, new ReportStyleOverlay(ColorFondo: ColorUsuario));

        var doc = new GeneradorPdfIndirectos(svc).ConstruirDocumento(
            proyecto, CrearGrupos(), new List<GrupoIndirecto>(), CrearConfig(),
            svc.ObtenerPlantillaSoloLectura(proyecto.Id), snapshot);

        var color = TablaColumnas(doc).Rows[FilaGrupo].Cells[ColumnaImporteTotal(snapshot)].Shading.Color;
        Assert.IsTrue(color.R == 0x01 && color.G == 0x02 && color.B == 0x03, "El color del usuario debe ganar al default.");
    }

    // ─────────────────────────── Excel (ClosedXML) ───────────────────────────

    [TestMethod]
    public void Excel_CeldaConOverride_EscribeFuenteRellenoYAlineacion()
    {
        using var ctx = CreateContext();
        var proyecto = CrearProyecto(ctx);
        var svc = new ReporteService(ctx);
        var snapshot = SnapshotConOverrideCelda(proyecto.Id);

        var ruta = Path.Combine(Path.GetTempPath(), $"sopro_ind_{Guid.NewGuid():N}.xlsx");
        try
        {
            new GeneradorExcelIndirectos(svc).Generar(proyecto, CrearGrupos(), new List<GrupoIndirecto>(),
                CrearConfig(), svc.ObtenerPlantillaSoloLectura(proyecto.Id), snapshot, ruta);
            Assert.IsTrue(File.Exists(ruta), "El generador debe producir el .xlsx.");

            using var wb = new XLWorkbook(ruta);
            var ws = wb.Worksheet(1);
            int colImporte = ColumnaEncabezado(ws, "Importe Total $");

            var celdaConcepto = ws.Cell(FilaDeTexto(ws, "Gerente"), colImporte);
            Assert.AreEqual(XLColor.FromHtml(ColorOverrideCelda).Color.ToArgb(), celdaConcepto.Style.Fill.BackgroundColor.Color.ToArgb(),
                "El fondo del override debe escribirse en la celda.");
            Assert.AreEqual("Arial", celdaConcepto.Style.Font.FontName, "La fuente del override debe escribirse.");
            Assert.AreEqual(12d, celdaConcepto.Style.Font.FontSize, "El tamaño del override debe escribirse.");
            Assert.IsTrue(celdaConcepto.Style.Font.Bold);
            Assert.IsTrue(celdaConcepto.Style.Font.Italic);
            Assert.AreEqual(XLColor.FromHtml("#112233").Color.ToArgb(), celdaConcepto.Style.Font.FontColor.Color.ToArgb());
            Assert.AreEqual(XLAlignmentHorizontalValues.Center, celdaConcepto.Style.Alignment.Horizontal);
        }
        finally
        {
            try { if (File.Exists(ruta)) File.Delete(ruta); } catch { /* best effort */ }
        }
    }

    [TestMethod]
    public void Excel_SinOverrides_RolGrupoUsaElGrisNeutral()
    {
        using var ctx = CreateContext();
        var proyecto = CrearProyecto(ctx);
        var svc = new ReporteService(ctx);
        var snapshot = SnapshotBase(proyecto.Id);

        var ruta = Path.Combine(Path.GetTempPath(), $"sopro_ind_{Guid.NewGuid():N}.xlsx");
        try
        {
            new GeneradorExcelIndirectos(svc).Generar(proyecto, CrearGrupos(), new List<GrupoIndirecto>(),
                CrearConfig(), svc.ObtenerPlantillaSoloLectura(proyecto.Id), snapshot, ruta);

            using var wb = new XLWorkbook(ruta);
            var ws = wb.Worksheet(1);
            int colImporte = ColumnaEncabezado(ws, "Importe Total $");

            var celdaGrupo = ws.Cell(FilaDeTexto(ws, "Honorarios"), colImporte);
            Assert.AreEqual(XLColor.FromHtml(ColorGrupoDefault).Color.ToArgb(), celdaGrupo.Style.Fill.BackgroundColor.Color.ToArgb(),
                "El rol Grupo debe duplicar el gris neutral #E6E6E6 en Excel.");
            Assert.IsTrue(celdaGrupo.Style.Font.Bold, "El grupo va en negrita.");
        }
        finally
        {
            try { if (File.Exists(ruta)) File.Delete(ruta); } catch { /* best effort */ }
        }
    }

    // ─────────────────────────── Paridad de los 3 renderers ───────────────────────────

    [TestMethod]
    public void Paridad_RolGrupo_MismoColorEnGridPdfYExcel()
    {
        using var ctx = CreateContext();
        var proyecto = CrearProyecto(ctx);
        var svc = new ReporteService(ctx);
        var snapshot = SnapshotBase(proyecto.Id);
        int esperado = System.Drawing.ColorTranslator.FromHtml(ColorGrupoDefault).ToArgb();

        // GRID
        var estiloGrid = new System.Windows.Forms.DataGridViewCellStyle();
        FormIndirectos.AplicarEstiloRolIndirectos(estiloGrid, new System.Drawing.Font("Segoe UI", 9f), "GRUPO", null);
        Assert.AreEqual(esperado, estiloGrid.BackColor.ToArgb(), "grid");

        // PDF
        var doc = new GeneradorPdfIndirectos(svc).ConstruirDocumento(
            proyecto, CrearGrupos(), new List<GrupoIndirecto>(), CrearConfig(),
            svc.ObtenerPlantillaSoloLectura(proyecto.Id), snapshot);
        var colorPdf = TablaColumnas(doc).Rows[FilaGrupo].Cells[ColumnaImporteTotal(snapshot)].Shading.Color;
        Assert.AreEqual(esperado, System.Drawing.Color.FromArgb((int)colorPdf.R, (int)colorPdf.G, (int)colorPdf.B).ToArgb(), "PDF");

        // EXCEL
        var ruta = Path.Combine(Path.GetTempPath(), $"sopro_ind_{Guid.NewGuid():N}.xlsx");
        try
        {
            new GeneradorExcelIndirectos(svc).Generar(proyecto, CrearGrupos(), new List<GrupoIndirecto>(),
                CrearConfig(), svc.ObtenerPlantillaSoloLectura(proyecto.Id), snapshot, ruta);
            using var wb = new XLWorkbook(ruta);
            var ws = wb.Worksheet(1);
            int colImporte = ColumnaEncabezado(ws, "Importe Total $");
            var colorExcel = ws.Cell(FilaDeTexto(ws, "Honorarios"), colImporte).Style.Fill.BackgroundColor.Color.ToArgb();
            Assert.AreEqual(esperado, colorExcel, "Excel");
        }
        finally
        {
            try { if (File.Exists(ruta)) File.Delete(ruta); } catch { /* best effort */ }
        }
    }

    // ─────────────────────────── Guard: render puro snapshot ───────────────────────────

    [TestMethod]
    public void GeneradoresIndirectos_NoRecibenNiAccedenADataGridView()
    {
        var generadores = new[] { typeof(GeneradorPdfIndirectos), typeof(GeneradorExcelIndirectos) };
        var tipoControl = typeof(System.Windows.Forms.Control);

        const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic
            | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

        foreach (var generador in generadores)
        {
            foreach (var metodo in generador.GetMethods(flags))
                AssertNoEsControl(metodo.GetParameters().Select(p => p.ParameterType).Append(metodo.ReturnType), generador.Name, tipoControl);

            foreach (var campo in generador.GetFields(flags))
                AssertNoEsControl(new[] { campo.FieldType }, generador.Name, tipoControl);
        }

        foreach (var relativo in new[]
        {
            Path.Combine("SOPRO.WinForms", "Services", "GeneradorPdfIndirectos.cs"),
            Path.Combine("SOPRO.WinForms", "Services", "GeneradorExcelIndirectos.cs"),
        })
        {
            var ruta = Path.Combine(RaizRepositorio(), relativo);
            Assert.IsTrue(File.Exists(ruta), $"No se localizó el generador para el guard: {ruta}");
            var fuente = File.ReadAllText(ruta);
            Assert.IsFalse(fuente.Contains("DataGridView", StringComparison.Ordinal),
                $"{relativo} no puede leer el DataGridView: el render es puro snapshot + dominio.");
        }
    }

    private static void AssertNoEsControl(IEnumerable<Type> tipos, string generador, Type tipoControl)
    {
        foreach (var tipo in tipos)
            Assert.IsFalse(tipoControl.IsAssignableFrom(tipo),
                $"{generador} no debe exponer/recibir controles de UI ({tipo.Name}).");
    }

    private static string RaizRepositorio()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "SOPRO.sln")))
                return dir.FullName;
            dir = dir.Parent;
        }
        throw new InvalidOperationException("No se localizó la raíz del repositorio (SOPRO.sln).");
    }

    // ─────────────────────────── Helpers ───────────────────────────

    private const int FilaGrupo = 2;     // título de columna (0), sección (1), grupo (2)
    private const int FilaConcepto = 3;  // concepto (3)

    private static Table TablaColumnas(Document doc)
        => doc.Sections[0].Elements.OfType<Table>().Skip(1).First();

    private static int ColumnaImporteTotal(ReportColumnSnapshot snapshot)
        => snapshot.Columnas.Where(c => c.Visible).OrderBy(c => c.Orden)
            .ToList().FindIndex(c => c.Identificador == "ImporteTotal");

    private static int ColumnaEncabezado(IXLWorksheet ws, string texto)
    {
        foreach (var celda in ws.CellsUsed())
            if (celda.GetString() == texto) return celda.Address.ColumnNumber;
        Assert.Fail($"No se encontró el encabezado '{texto}'.");
        return -1;
    }

    private static int FilaDeTexto(IXLWorksheet ws, string texto)
    {
        foreach (var celda in ws.CellsUsed())
            if (celda.GetString().Trim() == texto) return celda.Address.RowNumber;
        Assert.Fail($"No se encontró la fila con texto '{texto}'.");
        return -1;
    }
}
