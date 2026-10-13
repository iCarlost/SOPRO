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
/// Paso 4.2 — estilos por celda/fila de Presupuesto que viajan por el SNAPSHOT
/// NEUTRO hasta PDF (MigraDoc) y Excel (ClosedXML).
///
/// Cubre, sobre el MISMO snapshot con overrides:
/// <list type="bullet">
///   <item>PDF: el SEAM <c>ConstruirDocumento</c> permite inspeccionar el shading
///   real de la celda ANTES de serializar (el color del override llega al documento);</item>
///   <item>Excel: el .xlsx generado se abre con ClosedXML y se lee la fuente, el
///   relleno y la alineación de la celda afectada;</item>
///   <item>precedencia: el override de celda gana sobre el estilo de rol, que gana
///   sobre la columna;</item>
///   <item>guard: ningún generador de Presupuesto recibe ni accede a un
///   <c>DataGridView</c> (el render es puro snapshot + dominio).</item>
/// </list>
///
/// Vive en SOPRO.Tests.WinForms porque es el ÚNICO proyecto que referencia
/// SOPRO.WinForms (donde residen los generadores); SOPRO.Reporting.Tests se
/// mantiene neutral por diseño (no referencia WinForms).
/// </summary>
[TestClass]
public class GeneradorPresupuestoEstilosTests
{
    private const int ConceptoUnoId = 101;
    private const int ConceptoDosId = 102;

    // ─────────────────────────── Contexto / fixtures ───────────────────────────

    private static SOPROContext CreateContext()
    {
        var dbPath = Path.Combine(Path.GetTempPath(), $"sopro_formato_{Guid.NewGuid():N}.db");
        var ctx = new SOPROContext(dbPath);
        ctx.Database.EnsureDeleted();
        ctx.Database.EnsureCreated();
        return ctx;
    }

    private static Proyecto CrearProyecto(SOPROContext ctx)
    {
        var proyecto = new Proyecto
        {
            Nombre = "Proyecto estilos",
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
                ColumnaP("Cantidad", "Cantidad", 2, TipoDatoColumna.Numerico),
                ColumnaP("ImporteTotal", "Importe", 3, TipoDatoColumna.Moneda),
            });

    private static List<ConceptoPresupuesto> CrearConceptos(Proyecto proyecto) => new()
    {
        new ConceptoPresupuesto
        {
            Id = ConceptoUnoId,
            ProyectoId = proyecto.Id,
            Clave = "C-101",
            Descripcion = "Concepto Uno",
            Unidad = "pza",
            Cantidad = 2m,
            CostoDirectoUnitario = 50m,
            PrecioUnitario = 50m,
            ImporteTotal = 100m,
            Orden = 0,
            Nivel = 1,
        },
        new ConceptoPresupuesto
        {
            Id = ConceptoDosId,
            ProyectoId = proyecto.Id,
            Clave = "C-102",
            Descripcion = "Concepto Dos",
            Unidad = "pza",
            Cantidad = 4m,
            CostoDirectoUnitario = 25m,
            PrecioUnitario = 25m,
            ImporteTotal = 100m,
            Orden = 1,
            Nivel = 1,
        },
    };

    /// <summary>
    /// Snapshot con overrides: la celda (vista "", <c>C:101</c>, Importe) fija
    /// fuente/color de texto/color de fondo/negrita/cursiva/tamaño/alineación, y el
    /// rol <c>Concepto</c> fija un fondo distinto (que la celda debe ganar).
    /// </summary>
    private static ReportColumnSnapshot SnapshotConOverrides(int proyectoId)
    {
        var celda = new ReportCellStyleOverride(
            new ReportCellAddress(PresupuestoReportSnapshotBuilder.ClaveVista, "C:101", "ImporteTotal"),
            new ReportStyleOverlay(
                Fuente: "Arial",
                TamanoFuente: 12f,
                Negrita: true,
                Cursiva: true,
                ColorFuente: "#112233",
                ColorFondo: "#FF00AA",
                AlineacionHorizontal: ReportTextAlignment.Centro));

        var rolConcepto = new ReportRowRoleStyle(
            RolFilaReporte.Concepto,
            new ReportStyleOverlay(ColorFondo: "#EEEEEE"));

        return SnapshotConColumnas(proyectoId) with
        {
            EstilosCelda = new[] { celda },
            EstilosFila = new[] { rolConcepto },
        };
    }

    // ─────────────────────────── PDF (MigraDoc seam) ───────────────────────────

    [TestMethod]
    public void Pdf_CeldaConFondoOverride_SerializaElShadingDelOverrideYElRolAplicaAlResto()
    {
        using var ctx = CreateContext();
        var proyecto = CrearProyecto(ctx);
        var svc = new ReporteService(ctx);
        var plantilla = svc.ObtenerPlantillaSoloLectura(proyecto.Id);
        var snapshot = SnapshotConOverrides(proyecto.Id);
        var conceptos = CrearConceptos(proyecto);

        var doc = new GeneradorPdfPresupuesto(svc).ConstruirDocumento(proyecto, conceptos, plantilla, snapshot);

        var table = TablaCuerpo(doc);
        int colImporte = ColumnaImporteEnTabla(snapshot);

        // Fila del primer concepto: el override de celda GANA (#FF00AA).
        var celdaUno = table.Rows[1].Cells[colImporte];
        var colorUno = celdaUno.Shading.Color;
        Assert.IsTrue(colorUno.R == 0xFF && colorUno.G == 0x00 && colorUno.B == 0xAA,
            "El fondo del override (#FF00AA) debe llegar al shading de MigraDoc.");

        // Fila del segundo concepto: sin override de celda, gana el FONDO DEL ROL.
        var celdaDos = table.Rows[2].Cells[colImporte];
        var colorDos = celdaDos.Shading.Color;
        Assert.IsTrue(colorDos.R == 0xEE && colorDos.G == 0xEE && colorDos.B == 0xEE,
            "Sin override de celda, el fondo del rol (#EEEEEE) debe aplicarse.");
    }

    [TestMethod]
    public void Pdf_SinOverrides_NoAplicaNingunColorDeOverride()
    {
        using var ctx = CreateContext();
        var proyecto = CrearProyecto(ctx);
        var svc = new ReporteService(ctx);
        var plantilla = svc.ObtenerPlantillaSoloLectura(proyecto.Id);
        var snapshot = SnapshotConColumnas(proyecto.Id); // SIN estilos
        var conceptos = CrearConceptos(proyecto);

        var doc = new GeneradorPdfPresupuesto(svc).ConstruirDocumento(proyecto, conceptos, plantilla, snapshot);

        var table = TablaCuerpo(doc);
        int colImporte = ColumnaImporteEnTabla(snapshot);
        var celdaUno = table.Rows[1].Cells[colImporte];

        // Retrocompatibilidad: sin overrides se conserva el fondo de la columna (#FFFFFF).
        var color = celdaUno.Shading.Color;
        Assert.IsTrue(color.R == 0xFF && color.G == 0xFF && color.B == 0xFF,
            "Sin overrides se conserva el fondo blanco de la columna (#FFFFFF).");
    }

    // ─────────────────────────── Excel (ClosedXML) ───────────────────────────

    [TestMethod]
    public void Excel_CeldaConOverride_EscribeFuenteRellenoYAlineacionEnElLibro()
    {
        using var ctx = CreateContext();
        var proyecto = CrearProyecto(ctx);
        var svc = new ReporteService(ctx);
        var plantilla = svc.ObtenerPlantillaSoloLectura(proyecto.Id);
        var snapshot = SnapshotConOverrides(proyecto.Id);
        var conceptos = CrearConceptos(proyecto);

        var ruta = Path.Combine(Path.GetTempPath(), $"sopro_presupuesto_{Guid.NewGuid():N}.xlsx");
        try
        {
            new GeneradorExcelPresupuesto(svc).Generar(proyecto, conceptos, plantilla, snapshot, ruta, factorPU: 1m);
            Assert.IsTrue(File.Exists(ruta), "El generador debe producir el .xlsx.");

            using var wb = new XLWorkbook(ruta);
            var ws = wb.Worksheet(1);

            var (filaTitulos, colClave, colImporte) = LocalizarTitulos(ws);
            Assert.IsTrue(colImporte > 0, "Debe localizarse la columna 'Importe'.");

            var celdaUno = ws.Cell(filaTitulos + 1, colImporte);
            Assert.AreEqual(XLColor.FromHtml("#FF00AA").Color.ToArgb(), celdaUno.Style.Fill.BackgroundColor.Color.ToArgb(),
                "El fondo del override debe escribirse en la celda.");
            Assert.AreEqual("Arial", celdaUno.Style.Font.FontName, "La fuente del override debe escribirse.");
            Assert.AreEqual(12d, celdaUno.Style.Font.FontSize, "El tamaño del override debe escribirse.");
            Assert.IsTrue(celdaUno.Style.Font.Bold, "La negrita del override debe escribirse.");
            Assert.IsTrue(celdaUno.Style.Font.Italic, "La cursiva del override debe escribirse.");
            Assert.AreEqual(XLColor.FromHtml("#112233").Color.ToArgb(), celdaUno.Style.Font.FontColor.Color.ToArgb(),
                "El color de texto del override debe escribirse.");
            Assert.AreEqual(XLAlignmentHorizontalValues.Center, celdaUno.Style.Alignment.Horizontal,
                "La alineación del override debe escribirse.");

            // Segundo concepto: sin override de celda, gana el fondo del ROL.
            Assert.IsTrue(colClave > 0);
            var celdaDos = ws.Cell(filaTitulos + 2, colImporte);
            Assert.AreEqual(XLColor.FromHtml("#EEEEEE").Color.ToArgb(), celdaDos.Style.Fill.BackgroundColor.Color.ToArgb(),
                "El fondo del rol debe aplicarse a la fila sin override de celda.");
        }
        finally
        {
            try { if (File.Exists(ruta)) File.Delete(ruta); } catch { /* best effort */ }
        }
    }

    [TestMethod]
    public void Excel_SinOverrides_ConservaElEstiloDeColumna()
    {
        using var ctx = CreateContext();
        var proyecto = CrearProyecto(ctx);
        var svc = new ReporteService(ctx);
        var plantilla = svc.ObtenerPlantillaSoloLectura(proyecto.Id);
        var snapshot = SnapshotConColumnas(proyecto.Id); // SIN estilos
        var conceptos = CrearConceptos(proyecto);

        var ruta = Path.Combine(Path.GetTempPath(), $"sopro_presupuesto_{Guid.NewGuid():N}.xlsx");
        try
        {
            new GeneradorExcelPresupuesto(svc).Generar(proyecto, conceptos, plantilla, snapshot, ruta, factorPU: 1m);

            using var wb = new XLWorkbook(ruta);
            var ws = wb.Worksheet(1);
            var (filaTitulos, _, colImporte) = LocalizarTitulos(ws);
            var celdaUno = ws.Cell(filaTitulos + 1, colImporte);

            int blanco = XLColor.FromHtml("#FFFFFF").Color.ToArgb();
            Assert.AreEqual(blanco, celdaUno.Style.Fill.BackgroundColor.Color.ToArgb(),
                "Sin overrides, la celda conserva el fondo blanco de la columna.");
            Assert.AreEqual("Segoe UI", celdaUno.Style.Font.FontName, "Sin overrides, la fuente es la de la columna.");
            Assert.IsFalse(celdaUno.Style.Font.Bold, "Sin overrides no hay negrita forzada para un concepto terminal.");
        }
        finally
        {
            try { if (File.Exists(ruta)) File.Delete(ruta); } catch { /* best effort */ }
        }
    }

    // ─────────────────────────── Guard: render puro snapshot ───────────────────────────

    [TestMethod]
    public void GeneradoresPresupuesto_NoRecibenNiAccedenADataGridView()
    {
        var generadores = new[]
        {
            typeof(GeneradorPdfPresupuesto),
            typeof(GeneradorExcelPresupuesto),
        };

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

        // cinturón y tirantes: el código fuente no debe mencionar DataGridView.
        foreach (var relativo in new[]
        {
            Path.Combine("SOPRO.WinForms", "Services", "GeneradorPdfPresupuesto.cs"),
            Path.Combine("SOPRO.WinForms", "Services", "GeneradorExcelPresupuesto.cs"),
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
        {
            Assert.IsFalse(tipoControl.IsAssignableFrom(tipo),
                $"{generador} no debe exponer/recibir controles de UI ({tipo.Name}).");
        }
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

    /// <summary>Primera tabla del cuerpo de la sección (la generada por el reporte).</summary>
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
        Assert.AreEqual(celdaImporte!.Address.RowNumber, celdaClave!.Address.RowNumber,
            "Los encabezados deben estar en la misma fila.");
        return (celdaImporte.Address.RowNumber, celdaClave.Address.ColumnNumber, celdaImporte.Address.ColumnNumber);
    }
}

