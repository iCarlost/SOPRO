using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ClosedXML.Excel;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MigraDoc.DocumentObjectModel;
using MigraDoc.DocumentObjectModel.Tables;
using SOPRO.Application.Models.Reporting.ReportColumns;
using SOPRO.Application.Services;
using SOPRO.Application.Services.Reporting;
using SOPRO.Application.UseCases.Materials;
using SOPRO.Application.UseCases.Reporting;
using SOPRO.Core.Entities;
using SOPRO.Data.Context;
using SOPRO.WinForms.Services;

namespace SOPRO.Tests.WinForms.Services.Reportes;

/// <summary>
/// Paso 6.3 — el override de celda de los CATÁLOGOS viaja por el snapshot NEUTRO
/// hasta PDF (MigraDoc) y Excel (ClosedXML), con paridad con el grid. Se cubre la
/// ruta PDF del catálogo de Materiales (seam <c>ConstruirDocumento</c>) y la ruta
/// Excel del catálogo de Maquinaria (ClosedXML real).
/// </summary>
[TestClass]
public class GeneradorCatalogoEstilosTests
{
    private const int InsumoId = 1;
    private const string ColorOverride = "#FF00AA";

    private static SOPROContext CrearContexto()
    {
        var dbPath = Path.Combine(Path.GetTempPath(), $"sopro_cat_gen_{Guid.NewGuid():N}.db");
        var ctx = new SOPROContext(dbPath);
        ctx.Database.EnsureDeleted();
        ctx.Database.EnsureCreated();
        return ctx;
    }

    private static Proyecto CrearProyecto(SOPROContext ctx)
    {
        var proyecto = new Proyecto
        {
            Nombre = "Proyecto catálogo estilos",
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
        return proyecto;
    }

    // ─────────────────────────── PDF Materiales ───────────────────────────

    [TestMethod]
    public void PdfMateriales_CeldaConOverride_SerializaElShading()
    {
        using var ctx = CrearContexto();
        var proyecto = CrearProyecto(ctx);
        var svc = new ReporteService(ctx);
        var plantilla = svc.ObtenerPlantillaSoloLectura(proyecto.Id);

        var snapshot = MaterialCatalogExportResolver.BuildSnapshot(proyecto.Id, "Materiales", columnas: null) with
        {
            EstilosCelda = new[]
            {
                new ReportCellStyleOverride(
                    new ReportCellAddress(MaterialesReportSnapshotBuilder.ClaveVista, ClaveFilaReporte.Insumo(InsumoId), "PrecioUnitario"),
                    new ReportStyleOverlay(ColorFondo: ColorOverride, Negrita: true)),
            },
        };

        var materiales = new List<MaterialListItem>
        {
            new(InsumoId, "MAT-1", "Cemento", "kg", 12.5m, string.Empty, OrigenInsumo.Proyecto),
        };

        var doc = new GeneradorPdfCatalogoMateriales(svc).ConstruirDocumento(proyecto, materiales, plantilla, snapshot);

        var table = doc.Sections[0].Elements.OfType<Table>().First();
        int col = ColumnaPrecioUnitario(snapshot);

        var color = table.Rows[1].Cells[col].Shading.Color;
        Assert.IsTrue(color.R == 0xFF && color.G == 0x00 && color.B == 0xAA,
            "El fondo del override (#FF00AA) debe llegar al shading de MigraDoc.");
    }

    [TestMethod]
    public void PdfMateriales_SinOverrides_ConservaElFondoDeLaColumna()
    {
        using var ctx = CrearContexto();
        var proyecto = CrearProyecto(ctx);
        var svc = new ReporteService(ctx);
        var plantilla = svc.ObtenerPlantillaSoloLectura(proyecto.Id);

        var snapshot = MaterialCatalogExportResolver.BuildSnapshot(proyecto.Id, "Materiales", columnas: null);
        var materiales = new List<MaterialListItem>
        {
            new(InsumoId, "MAT-1", "Cemento", "kg", 12.5m, string.Empty, OrigenInsumo.Proyecto),
        };

        var doc = new GeneradorPdfCatalogoMateriales(svc).ConstruirDocumento(proyecto, materiales, plantilla, snapshot);

        var table = doc.Sections[0].Elements.OfType<Table>().First();
        int col = ColumnaPrecioUnitario(snapshot);

        var color = table.Rows[1].Cells[col].Shading.Color;
        Assert.IsTrue(color.R == 0xFF && color.G == 0xFF && color.B == 0xFF,
            "Sin overrides la celda conserva el fondo blanco de la columna (retrocompatibilidad).");
    }

    // ─────────────────────────── Excel Maquinaria ───────────────────────────

    [TestMethod]
    public void ExcelMaquinaria_CeldaConOverride_EscribeFuenteRelleno()
    {
        using var ctx = CrearContexto();
        var proyecto = CrearProyecto(ctx);
        var svc = new ReporteService(ctx);
        var plantilla = svc.ObtenerPlantillaSoloLectura(proyecto.Id);

        var snapshot = new CostoHorarioReportSnapshotBuilder().Build(proyecto.Id, "Maquinaria", columnas: null) with
        {
            EstilosCelda = new[]
            {
                new ReportCellStyleOverride(
                    new ReportCellAddress(CostoHorarioReportSnapshotBuilder.ClaveVista, ClaveFilaReporte.Insumo(InsumoId), "CostoHorario"),
                    new ReportStyleOverlay(
                        Fuente: "Arial",
                        TamanoFuente: 12f,
                        Negrita: true,
                        ColorFondo: ColorOverride,
                        AlineacionHorizontal: ReportTextAlignment.Centro)),
            },
        };

        var maquinas = new List<Maquinaria> { new() { Id = InsumoId, Clave = "MQ-1", Descripcion = "Retro" } };

        var ruta = Path.Combine(Path.GetTempPath(), $"sopro_cat_maq_{Guid.NewGuid():N}.xlsx");
        try
        {
            using (var wb = new XLWorkbook())
            {
                GeneradorExcelCostoHorario.Generar(wb, maquinas, proyecto, plantilla, snapshot, svc);
                wb.SaveAs(ruta);
            }

            using var wbLeido = new XLWorkbook(ruta);
            var ws = wbLeido.Worksheet(1);
            int col = ColumnaEncabezado(ws, "Costo Horario");
            var celda = ws.Cell(FilaDeTexto(ws, "MQ-1"), col);

            Assert.AreEqual(XLColor.FromHtml(ColorOverride).Color.ToArgb(),
                celda.Style.Fill.BackgroundColor.Color.ToArgb(), "El fondo del override debe escribirse.");
            Assert.AreEqual("Arial", celda.Style.Font.FontName);
            Assert.AreEqual(12d, celda.Style.Font.FontSize);
            Assert.IsTrue(celda.Style.Font.Bold);
            Assert.AreEqual(XLAlignmentHorizontalValues.Center, celda.Style.Alignment.Horizontal);
        }
        finally
        {
            try { if (File.Exists(ruta)) File.Delete(ruta); } catch { /* best effort */ }
        }
    }

    [TestMethod]
    public void ExcelMaquinaria_SinOverrides_ConservaElFormatoDeColumna()
    {
        using var ctx = CrearContexto();
        var proyecto = CrearProyecto(ctx);
        var svc = new ReporteService(ctx);
        var plantilla = svc.ObtenerPlantillaSoloLectura(proyecto.Id);
        var snapshot = new CostoHorarioReportSnapshotBuilder().Build(proyecto.Id, "Maquinaria", columnas: null);

        var maquinas = new List<Maquinaria> { new() { Id = InsumoId, Clave = "MQ-1", Descripcion = "Retro" } };

        var ruta = Path.Combine(Path.GetTempPath(), $"sopro_cat_maq_{Guid.NewGuid():N}.xlsx");
        try
        {
            using (var wb = new XLWorkbook())
            {
                GeneradorExcelCostoHorario.Generar(wb, maquinas, proyecto, plantilla, snapshot, svc);
                wb.SaveAs(ruta);
            }

            using var wbLeido = new XLWorkbook(ruta);
            var ws = wbLeido.Worksheet(1);
            int col = ColumnaEncabezado(ws, "Costo Horario");
            var celda = ws.Cell(FilaDeTexto(ws, "MQ-1"), col);

            // El formato numérico del importe lo conserva la columna (no se pisa con null).
            Assert.IsFalse(string.IsNullOrWhiteSpace(celda.Style.NumberFormat.Format),
                "Sin overrides la columna conserva su formato numérico de importe.");
        }
        finally
        {
            try { if (File.Exists(ruta)) File.Delete(ruta); } catch { /* best effort */ }
        }
    }

    // ─────────────────────────── Guard: render puro snapshot ───────────────────────────

    [TestMethod]
    public void GeneradoresCatalogos_NoRecibenNiAccedenADataGridView()
    {
        var generadores = new[]
        {
            typeof(GeneradorPdfCatalogoMateriales),
            typeof(GeneradorPdfCatalogoManoObra),
            typeof(GeneradorPdfCatalogoHerramientas),
            typeof(GeneradorPdfCostoHorario),
            typeof(GeneradorExcelCostoHorario),
        };
        var tipoControl = typeof(System.Windows.Forms.Control);

        const System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.Public
            | System.Reflection.BindingFlags.NonPublic
            | System.Reflection.BindingFlags.Instance
            | System.Reflection.BindingFlags.Static
            | System.Reflection.BindingFlags.DeclaredOnly;

        foreach (var generador in generadores)
        {
            foreach (var metodo in generador.GetMethods(flags))
                AssertNoEsControl(metodo.GetParameters().Select(p => p.ParameterType).Append(metodo.ReturnType), generador.Name, tipoControl);

            foreach (var campo in generador.GetFields(flags))
                AssertNoEsControl(new[] { campo.FieldType }, generador.Name, tipoControl);
        }

        foreach (var relativo in new[]
        {
            Path.Combine("SOPRO.WinForms", "Services", "GeneradorPdfCatalogoMateriales.cs"),
            Path.Combine("SOPRO.WinForms", "Services", "GeneradorPdfCatalogoManoObra.cs"),
            Path.Combine("SOPRO.WinForms", "Services", "GeneradorPdfCatalogoHerramientas.cs"),
            Path.Combine("SOPRO.WinForms", "Services", "GeneradorPdfCostoHorario.cs"),
            Path.Combine("SOPRO.WinForms", "Services", "GeneradorExcelCostoHorario.cs"),
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

    private static int ColumnaPrecioUnitario(ReportColumnSnapshot snapshot)
        => snapshot.Columnas.Where(c => c.Visible).OrderBy(c => c.Orden)
            .ToList().FindIndex(c => c.Identificador == "PrecioUnitario");

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
