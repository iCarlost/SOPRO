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
using SOPRO.Application.Services.Reporting;
using SOPRO.Application.UseCases.Reporting;
using SOPRO.Core.Entities;
using SOPRO.Data.Context;
using SOPRO.WinForms.Services;

namespace SOPRO.Tests.WinForms.Services.Reportes;

/// <summary>
/// Paso 5.2 — estilos por celda/fila del APU que viajan por el SNAPSHOT NEUTRO
/// hasta PDF (MigraDoc) y Excel (ClosedXML).
///
/// Cubre, sobre el MISMO snapshot con overrides:
/// <list type="bullet">
///   <item>PDF: el SEAM <c>ConstruirDocumento</c> permite inspeccionar el shading
///   real de la celda del componente (<c>COMP:{Id}</c>) y del encabezado del
///   concepto (<c>C:{Id}</c>) ANTES de serializar;</item>
///   <item>Excel: el .xlsx generado se abre con ClosedXML y se lee el relleno/fuente
///   de la celda del componente;</item>
///   <item>retrocompatibilidad: sin overrides el render conserva el estilo legacy
///   (color de sección del componente);</item>
///   <item>guard: ningún generador de APU recibe ni accede a un DataGridView.</item>
/// </list>
/// </summary>
[TestClass]
public class GeneradorApuEstilosTests
{
    private const string TipoApu = "APU";

    // ─────────────────────────── Escenario ───────────────────────────

    private static SOPROContext CreateContext()
    {
        var dbPath = Path.Combine(Path.GetTempPath(), $"sopro_apu_estilos_{Guid.NewGuid():N}.db");
        var ctx = new SOPROContext(dbPath);
        ctx.Database.EnsureDeleted();
        ctx.Database.EnsureCreated();
        return ctx;
    }

    private sealed record Escenario(Proyecto Proyecto, Matriz Matriz, ConceptoPresupuesto Concepto, int ComponenteMaterialId);

    private static Escenario CrearEscenario(SOPROContext ctx)
    {
        var proyecto = new Proyecto
        {
            Nombre = "Proyecto APU estilos",
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

        var cemento = new Material
        {
            ProyectoId = proyecto.Id,
            Clave = "MAT-CEM",
            Descripcion = "Cemento gris",
            Unidad = "kg",
            PrecioUnitario = 60m,
            Notas = string.Empty,
        };
        ctx.Materiales.Add(cemento);
        ctx.SaveChanges();

        var matriz = new Matriz
        {
            ProyectoId = proyecto.Id,
            Clave = "APU-001",
            Descripcion = "Concreto simple",
            Unidad = "m3",
            Tipo = TipoMatriz.APU,
            Notas = string.Empty,
        };
        ctx.Matrices.Add(matriz);
        ctx.SaveChanges();

        var componente = new ComponenteMatriz
        {
            MatrizId = matriz.Id,
            TipoComponente = TipoComponenteMatriz.Material,
            MaterialId = cemento.Id,
            Material = cemento,
            Cantidad = 1m,
            Orden = 1,
            Notas = string.Empty,
        };
        ctx.ComponentesMatriz.Add(componente);
        matriz.Componentes.Add(componente);
        ctx.SaveChanges();

        var concepto = new ConceptoPresupuesto
        {
            ProyectoId = proyecto.Id,
            Clave = "C-001",
            Descripcion = "Concreto simple en obra",
            Unidad = "m3",
            Cantidad = 10m,
            MatrizId = matriz.Id,
            CostoDirectoUnitario = 60m,
            CostoDirectoTotal = 600m,
            PrecioUnitario = 60m,
            ImporteTotal = 600m,
            Nivel = 1,
            Orden = 1,
            EsAgrupador = false,
            ColumnasPersonalizadasJSON = string.Empty,
            Notas = string.Empty,
        };
        ctx.ConceptosPresupuesto.Add(concepto);
        ctx.SaveChanges();

        return new Escenario(proyecto, matriz, concepto, componente.Id);
    }

    private static ReportColumnSnapshot SnapshotApu(int proyectoId)
        => new ApuReportSnapshotBuilder().Build(proyectoId, "Análisis de Precios Unitarios", columnas: null);

    // ─────────────────────────── PDF (MigraDoc seam) ───────────────────────────

    [TestMethod]
    public void Pdf_OverrideDeComponente_LlegaAlShadingDeMigraDoc()
    {
        using var ctx = CreateContext();
        var esc = CrearEscenario(ctx);
        var svc = new ReporteService(ctx);
        var plantilla = svc.ObtenerPlantillaSoloLectura(esc.Proyecto.Id);

        var snapshot = SnapshotApu(esc.Proyecto.Id) with
        {
            EstilosCelda = new[]
            {
                new ReportCellStyleOverride(
                    new ReportCellAddress(ApuReportSnapshotBuilder.ClaveVista,
                        ApuReportSnapshotBuilder.ClaveFilaComponente(esc.ComponenteMaterialId), "Descripcion"),
                    new ReportStyleOverlay(ColorFondo: "#FF00AA")),
            },
        };

        var doc = new GeneradorPdfAPU(svc, ctx).ConstruirDocumento(esc.Proyecto, new List<ConceptoPresupuesto> { esc.Concepto }, plantilla, snapshot);

        Assert.IsTrue(ExisteCeldaConShading(doc, "#FF00AA"),
            "El fondo del override de la celda del componente (#FF00AA) debe llegar al shading de MigraDoc.");
    }

    [TestMethod]
    public void Pdf_OverrideDeEncabezadoDeConcepto_LlegaAlShading()
    {
        using var ctx = CreateContext();
        var esc = CrearEscenario(ctx);
        var svc = new ReporteService(ctx);
        var plantilla = svc.ObtenerPlantillaSoloLectura(esc.Proyecto.Id);

        var snapshot = SnapshotApu(esc.Proyecto.Id) with
        {
            EstilosCelda = new[]
            {
                new ReportCellStyleOverride(
                    new ReportCellAddress(ApuReportSnapshotBuilder.ClaveVista,
                        ApuReportSnapshotBuilder.ClaveFilaConcepto(esc.Concepto.Id), "Descripcion"),
                    new ReportStyleOverlay(ColorFondo: "#00AAFF")),
            },
        };

        var doc = new GeneradorPdfAPU(svc, ctx).ConstruirDocumento(esc.Proyecto, new List<ConceptoPresupuesto> { esc.Concepto }, plantilla, snapshot);

        Assert.IsTrue(ExisteCeldaConShading(doc, "#00AAFF"),
            "El fondo del override del encabezado del concepto (#00AAFF) debe llegar al shading.");
    }

    [TestMethod]
    public void Pdf_SinOverrides_ConservaElColorDeSeccionLegacy()
    {
        using var ctx = CreateContext();
        var esc = CrearEscenario(ctx);
        var svc = new ReporteService(ctx);
        var plantilla = svc.ObtenerPlantillaSoloLectura(esc.Proyecto.Id);

        var doc = new GeneradorPdfAPU(svc, ctx).ConstruirDocumento(esc.Proyecto, new List<ConceptoPresupuesto> { esc.Concepto }, plantilla, SnapshotApu(esc.Proyecto.Id));

        Assert.IsTrue(ExisteCeldaConShading(doc, "#E3F2FD"),
            "Sin overrides, la fila de material conserva su color de sección legacy (#E3F2FD).");
        Assert.IsFalse(ExisteCeldaConShading(doc, "#FF00AA"));
    }

    // ─────────────────────────── Excel (ClosedXML) ───────────────────────────

    [TestMethod]
    public void Excel_OverrideDeComponente_EscribeRellenoYFuente()
    {
        using var ctx = CreateContext();
        var esc = CrearEscenario(ctx);
        var svc = new ReporteService(ctx);
        var plantilla = svc.ObtenerPlantillaSoloLectura(esc.Proyecto.Id);

        var snapshot = SnapshotApu(esc.Proyecto.Id) with
        {
            EstilosCelda = new[]
            {
                new ReportCellStyleOverride(
                    new ReportCellAddress(ApuReportSnapshotBuilder.ClaveVista,
                        ApuReportSnapshotBuilder.ClaveFilaComponente(esc.ComponenteMaterialId), "Descripcion"),
                    new ReportStyleOverlay(ColorFondo: "#FF00AA", Negrita: true)),
            },
        };

        var ruta = Path.Combine(Path.GetTempPath(), $"sopro_apu_{Guid.NewGuid():N}.xlsx");
        try
        {
            new GeneradorExcelAPU(svc, ctx).Generar(esc.Proyecto, new List<ConceptoPresupuesto> { esc.Concepto }, plantilla, snapshot, ruta);
            Assert.IsTrue(File.Exists(ruta), "El generador debe producir el .xlsx.");

            using var wb = new XLWorkbook(ruta);
            var ws = wb.Worksheets.First(w => w.Name != "Índice");
            Assert.IsTrue(HayCeldaConFondo(wb, ws, "#FF00AA"),
                "El fondo del override del componente debe escribirse en el libro.");
            Assert.IsTrue(HayCeldaConNegrita(wb, ws),
                "La negrita del override debe escribirse en la celda del componente.");
        }
        finally
        {
            try { if (File.Exists(ruta)) File.Delete(ruta); } catch { /* best effort */ }
        }
    }

    // ─────────────────────────── Guard: render puro snapshot ───────────────────────────

    [TestMethod]
    public void GeneradoresApu_NoRecibenNiAccedenADataGridView()
    {
        var generadores = new[] { typeof(GeneradorPdfAPU), typeof(GeneradorExcelAPU) };
        var tipoControl = typeof(System.Windows.Forms.Control);

        const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic
            | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

        foreach (var generador in generadores)
        {
            foreach (var metodo in generador.GetMethods(flags))
                AssertNoEsControl(metodo.GetParameters().Select(p => p.ParameterType).Append(metodo.ReturnType), generador.Name, tipoControl);

            foreach (var campo in generador.GetFields(flags))
                AssertNoEsControl(new[] { campo.FieldType }, generador.Name, tipoControl);

            foreach (var propiedad in generador.GetProperties(flags))
                AssertNoEsControl(new[] { propiedad.PropertyType }, generador.Name, tipoControl);
        }

        foreach (var relativo in new[]
        {
            Path.Combine("SOPRO.WinForms", "Services", "GeneradorPdfAPU.cs"),
            Path.Combine("SOPRO.WinForms", "Services", "GeneradorExcelAPU.cs"),
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

    private static bool ExisteCeldaConShading(Document doc, string hex)
    {
        var esperado = MigraDoc.DocumentObjectModel.Color.Parse(hex);
        for (int s = 0; s < doc.Sections.Count; s++)
        {
            Section section = doc.Sections[s];
            foreach (var table in section.Elements.OfType<Table>())
            {
                foreach (Row row in table.Rows)
                {
                    foreach (Cell cell in row.Cells)
                    {
                        var color = cell.Shading.Color;
                        if (color.R == esperado.R && color.G == esperado.G && color.B == esperado.B)
                            return true;
                    }
                }
            }
        }
        return false;
    }

    private static bool HayCeldaConFondo(XLWorkbook wb, IXLWorksheet ws, string hex)
    {
        var esperado = XLColor.FromHtml(hex).Color.ToArgb();
        foreach (var cell in ws.CellsUsed())
        {
            if (cell.Style.Fill.BackgroundColor.Color.ToArgb() == esperado) return true;
        }
        return false;
    }

    private static bool HayCeldaConNegrita(XLWorkbook wb, IXLWorksheet ws)
    {
        foreach (var cell in ws.CellsUsed())
        {
            if (cell.Style.Font.Bold) return true;
        }
        return false;
    }
}
