using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using ClosedXML.Excel;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MigraDoc.DocumentObjectModel;
using MigraDoc.DocumentObjectModel.Tables;
using SOPRO.Application.Models.Reporting.Fsr;
using SOPRO.Application.Models.Reporting.ReportColumns;
using SOPRO.Application.Services.Reporting;
using SOPRO.Application.UseCases.Reporting;
using SOPRO.Core.Entities;
using SOPRO.Data.Context;
using SOPRO.Reporting.Formatting;
using SOPRO.WinForms.Services;

namespace SOPRO.Tests.WinForms.Services.Reportes;

/// <summary>
/// Paso 6.4 — los generadores del FSR (AE-2A/AE-2C) ya NO tienen colores
/// hardcodeados: las bandas de sección/subsección y la fila de total provienen de
/// la tabla neutral <see cref="EstilosFilaPredeterminados.Fsr"/>. PDF (seam
/// <c>ConstruirDocumento</c>) y Excel (ClosedXML) convergen a los MISMOS colores;
/// las celdas numéricas conservan el formato del contrato neutral.
/// </summary>
[TestClass]
public class GeneradorFsrEstilosTests
{
    private static readonly string[] ColoresFsrRetirados =
    {
        "#33334C", "#4A4A6A", "#E8EAF6", "#1A237E", "#C5CAE9", // Excel
        "#D9E2F3", "#EDEDED", "#C00000",                       // PDF
    };

    private const string Parametros =
        """
        {
          "SalarioMinimo":"248.93", "Jornada":"0", "Semestre":"0", "Anio":"2026",
          "HorasJornada":"8", "SalarioNominal":"500", "DiasCalendario":"365",
          "DiasAguinaldo":"15", "DiasVacaciones":"12", "PrimaVacacional":"25",
          "DiasDescanso":"52", "DiasFestivos":"7", "PctGuarderias":"1",
          "PctRetiro":"2", "PctRiesgos":"4.58875", "PctINFONAVIT":"5", "PctNomina":"2.4"
        }
        """;

    private static SOPROContext CrearContexto()
    {
        var dbPath = Path.Combine(Path.GetTempPath(), $"sopro_fsr_estilos_{Guid.NewGuid():N}.db");
        var ctx = new SOPROContext(dbPath);
        ctx.Database.EnsureDeleted();
        ctx.Database.EnsureCreated();
        return ctx;
    }

    private static Proyecto CrearProyecto(SOPROContext ctx)
    {
        var proyecto = new Proyecto
        {
            Nombre = "Proyecto FSR estilos",
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
            ParametrosFSR = Parametros,
        };
        ctx.Proyectos.Add(proyecto);
        ctx.SaveChanges();
        return proyecto;
    }

    // ─────────────────────────── PDF (seam ConstruirDocumento) ───────────────────────────

    [TestMethod]
    public void Pdf_SeccionSubseccionYTotal_UsanLosDefaultsPorRol()
    {
        using var ctx = CrearContexto();
        var proyecto = CrearProyecto(ctx);
        var svc = new ReporteService(ctx);
        var snapshot = new FsrReportSnapshotBuilder().BuildAE2A(proyecto, "Tabla FSR");
        var filas = new List<FsrRow>
        {
            FsrRow.Seccion("DATOS BÁSICOS"),
            FsrRow.Subseccion("Para el cálculo de días pagados"),
            FsrRow.Numero("Días Calendario (DC)", "", "días", 365m),
        };

        var doc = new GeneradorPdfFSR(svc).ConstruirDocumento(
            proyecto, svc.ObtenerPlantillaSoloLectura(proyecto.Id), snapshot, filas, 1.5m);

        var tabla = TablaPrincipal(doc);

        var encabezado = EstilosFilaPredeterminados.Fsr(RolFilaReporte.Seccion).ColorFondo!;
        var subseccion = EstilosFilaPredeterminados.Fsr(RolFilaReporte.Referencia).ColorFondo!;
        var total = EstilosFilaPredeterminados.Fsr(RolFilaReporte.Total).ColorFondo!;

        AssertColor(tabla.Rows[1].Cells[0].Shading.Color, encabezado, "La banda de SECCIÓN usa el default del rol.");
        AssertColor(tabla.Rows[2].Cells[0].Shading.Color, subseccion, "La banda de SUBsección usa el default del rol.");

        var filaTotal = tabla.Rows[tabla.Rows.Count - 1];
        AssertColor(filaTotal.Cells[0].Shading.Color, total, "La fila de TOTAL usa el default del rol.");
    }

    // ─────────────────────────── Excel (ClosedXML) ───────────────────────────

    [TestMethod]
    public void Excel_AE2A_SeccionSubseccionYTotal_UsanLosDefaultsYConservanElFormatoNumerico()
    {
        using var ctx = CrearContexto();
        var proyecto = CrearProyecto(ctx);
        var svc = new ReporteService(ctx);
        var snapshot = new FsrReportSnapshotBuilder().BuildAE2A(proyecto, "Tabla FSR");
        var valorCol = snapshot.Columnas.Single(c => c.Identificador == FsrReportSnapshotBuilder.ColValor);
        var formatoEsperado = ReportColumnGridFormat.ResolveExcelFormat(valorCol, snapshot);

        using var wb = new XLWorkbook();
        GeneradorExcelFSR.GenerarAE2A(wb, proyecto, svc.ObtenerPlantillaSoloLectura(proyecto.Id), svc, snapshot);
        var ws = wb.Worksheet("Cálculo FSR");

        var seccion = FilaDeTexto(ws, "DATOS BASICOS");
        var subseccion = FilaDeTexto(ws, "Para el cálculo de días pagados");
        var total = FilaDeTexto(ws, "FACTOR DE SALARIO REAL");

        Assert.AreEqual(
            XLColor.FromHtml(EstilosFilaPredeterminados.Fsr(RolFilaReporte.Seccion).ColorFondo!).Color.ToArgb(),
            ws.Cell(seccion, 1).Style.Fill.BackgroundColor.Color.ToArgb(), "SECCIÓN → #4A4A6A del default por rol.");
        Assert.AreEqual(
            XLColor.FromHtml(EstilosFilaPredeterminados.Fsr(RolFilaReporte.Referencia).ColorFondo!).Color.ToArgb(),
            ws.Cell(subseccion, 1).Style.Fill.BackgroundColor.Color.ToArgb(), "SUBsección → #C5CAE9 del default por rol.");
        Assert.AreEqual(
            XLColor.FromHtml(EstilosFilaPredeterminados.Fsr(RolFilaReporte.Total).ColorFondo!).Color.ToArgb(),
            ws.Cell(total, 1).Style.Fill.BackgroundColor.Color.ToArgb(), "TOTAL → #1A237E del default por rol.");

        // Celdas numéricas: conservan el formato del contrato neutral (decimales de cantidad).
        var dato = FilaDeTexto(ws, "Días Calendario (DC)");
        Assert.AreEqual(formatoEsperado, ws.Cell(dato, 4).Style.NumberFormat.Format,
            "La columna Valor conserva el formato numérico del snapshot.");
    }

    [TestMethod]
    public void Excel_AE2C_Tabulador_GeneraSinColoresHardcodeados()
    {
        using var ctx = CrearContexto();
        var proyecto = CrearProyecto(ctx);
        var svc = new ReporteService(ctx);
        var snapshot = new FsrReportSnapshotBuilder().BuildAE2C(proyecto, "Tabulador FSR");
        var manoDeObra = new List<ManoDeObra>
        {
            new()
            {
                Clave = "MO-01", Descripcion = "Peón", Unidad = "jor",
                SalarioBase = 300m, FactorSalarioReal = 1.5m, SalarioReal = 450m,
            },
        };

        using var wb = new XLWorkbook();
        GeneradorExcelFSR.GenerarAE2C(wb, proyecto, manoDeObra, svc.ObtenerPlantillaSoloLectura(proyecto.Id), svc, snapshot);

        var ws = wb.Worksheet("Tabulador FSR");
        Assert.IsNotNull(ws, "El Tabulador FSR (AE-2C) debe generarse.");

        // La columna Salario Real conserva su formato monetario del contrato.
        var salarioRealCol = snapshot.Columnas.Single(c => c.Identificador == FsrReportSnapshotBuilder.ColSalarioReal);
        var formatoSalarioReal = ReportColumnGridFormat.ResolveExcelFormat(salarioRealCol, snapshot);
        int col = ColumnaEncabezado(ws, salarioRealCol.Encabezado);
        int filaDato = FilaDeTexto(ws, "MO-01");
        Assert.AreEqual(formatoSalarioReal, ws.Cell(filaDato, col).Style.NumberFormat.Format,
            "La columna Salario Real conserva el formato monetario del snapshot.");
    }

    // ─────────────────────────── Guardas ───────────────────────────

    [TestMethod]
    public void GeneradoresFsr_NoContienenLasConstantesDeColorRetiradas()
    {
        foreach (var relativo in new[]
        {
            Path.Combine("SOPRO.WinForms", "Services", "GeneradorPdfFSR.cs"),
            Path.Combine("SOPRO.WinForms", "Services", "GeneradorExcelFSR.cs"),
        })
        {
            var ruta = Path.Combine(RaizRepositorio(), relativo);
            Assert.IsTrue(File.Exists(ruta), $"No se localizó el generador: {ruta}");
            var fuente = File.ReadAllText(ruta);
            foreach (var color in ColoresFsrRetirados)
                Assert.IsFalse(fuente.Contains(color, StringComparison.OrdinalIgnoreCase),
                    $"{relativo} no debe contener la constante de color '{color}' (debe venir de los defaults por rol).");
        }
    }

    [TestMethod]
    public void GeneradoresFsr_NoRecibenNiAccedenADataGridView()
    {
        var generadores = new[] { typeof(GeneradorPdfFSR), typeof(GeneradorExcelFSR) };
        var tipoControl = typeof(System.Windows.Forms.Control);

        const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic
            | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

        foreach (var generador in generadores)
        {
            foreach (var metodo in generador.GetMethods(flags))
            {
                foreach (var tipo in metodo.GetParameters().Select(p => p.ParameterType).Append(metodo.ReturnType))
                    Assert.IsFalse(tipoControl.IsAssignableFrom(tipo),
                        $"{generador.Name} no debe exponer/recibir controles de UI ({tipo.Name}).");
            }
        }

        foreach (var relativo in new[]
        {
            Path.Combine("SOPRO.WinForms", "Services", "GeneradorPdfFSR.cs"),
            Path.Combine("SOPRO.WinForms", "Services", "GeneradorExcelFSR.cs"),
        })
        {
            var fuente = File.ReadAllText(Path.Combine(RaizRepositorio(), relativo));
            Assert.IsFalse(fuente.Contains("DataGridView", StringComparison.Ordinal),
                $"{relativo} no puede leer el DataGridView: el render es puro snapshot + dominio.");
        }
    }

    // ─────────────────────────── Helpers ───────────────────────────

    private static Table TablaPrincipal(Document doc)
        => doc.Sections[0].Elements.OfType<Table>().Skip(1).First();

    private static void AssertColor(MigraDoc.DocumentObjectModel.Color color, string hex, string mensaje)
    {
        var esperado = System.Drawing.ColorTranslator.FromHtml(hex);
        Assert.IsTrue(
            color.R == esperado.R && color.G == esperado.G && color.B == esperado.B,
            $"{mensaje} Se esperaba {hex}, se obtuvo {color.R:X2}{color.G:X2}{color.B:X2}.");
    }

    private static int FilaDeTexto(IXLWorksheet ws, string texto)
    {
        foreach (var celda in ws.CellsUsed())
            if (celda.GetString().Trim() == texto) return celda.Address.RowNumber;
        Assert.Fail($"No se encontró la fila con texto '{texto}'.");
        return -1;
    }

    private static int ColumnaEncabezado(IXLWorksheet ws, string texto)
    {
        foreach (var celda in ws.CellsUsed())
            if (celda.GetString().Trim() == texto) return celda.Address.ColumnNumber;
        Assert.Fail($"No se encontró el encabezado '{texto}'.");
        return -1;
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
}
