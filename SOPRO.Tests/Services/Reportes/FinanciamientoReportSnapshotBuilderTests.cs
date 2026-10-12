using Microsoft.VisualStudio.TestTools.UnitTesting;
using SOPRO.Application.Models.Reporting.ReportColumns;
using SOPRO.Application.UseCases.Reporting;
using SOPRO.Core.Entities;

namespace SOPRO.Tests.Services.Reportes;

/// <summary>
/// Verificación del <see cref="FinanciamientoReportSnapshotBuilder"/>: snapshot
/// neutral de columnas del reporte de Financiamiento construido desde
/// <c>ColumnaFinanciamiento</c> como fuente, con defaults neutrales cuando no hay
/// configuración persistida.
///
/// Cubre visibilidad, orden, ancho, encabezado y formatos (incluido el rol
/// monetario de las columnas de costo/importe), el estilo neutral
/// <see cref="ReportTableStyle.LegacyCatalogo"/>, la reutilización de una sola
/// instancia de definiciones para los consumidores PDF/Excel, y huecos del mapper
/// (inferencia numérica por alineación derecha, alineación vertical 0/1/2, wrap,
/// colores nulos) ejercitados a través del builder real.
/// </summary>
[TestClass]
public class FinanciamientoReportSnapshotBuilderTests
{
    // ─────────────────────────── Helpers de datos ───────────────────────────

    private static ColumnaFinanciamiento Columna(
        string nombreInterno,
        string? nombre = null,
        int orden = 0,
        bool visible = true,
        int ancho = 100,
        AlineacionColumna alineacion = AlineacionColumna.Izquierda,
        int alineacionVertical = 1,
        bool wrap = false,
        string? formato = null,
        string? fuente = null,
        int tamano = 0,
        string? colorFuente = null,
        string? colorFondo = null,
        bool negrita = false,
        bool cursiva = false)
        => new()
        {
            NombreInterno = nombreInterno,
            Nombre = nombre!,
            Orden = orden,
            Visible = visible,
            AnchoColumna = ancho,
            Alineacion = alineacion,
            AlineacionVertical = alineacionVertical,
            WrapTexto = wrap,
            FormatoNumerico = formato!,
            NombreFuente = fuente!,
            TamanoFuente = tamano,
            ColorFuente = colorFuente!,
            ColorFondo = colorFondo!,
            Negrita = negrita,
            Cursiva = cursiva
        };

    private static readonly string[] IdentificadoresDefault =
    {
        "colPeriodo", "colInicio", "colFin", "colDias", "colCD", "colCI", "colEgresos",
        "colAnticipo", "colEstim", "colAmort", "colCobro", "colFlujoNeto", "colSaldo",
        "colTasa", "colInteres"
    };

    // ───────────────────────── Metadatos del snapshot ─────────────────────────

    [TestMethod]
    public void Build_ProduceSnapshotConTipoProyectoYTitulo()
    {
        var snapshot = new FinanciamientoReportSnapshotBuilder()
            .Build(5, "Análisis de Financiamiento", Array.Empty<ColumnaFinanciamiento>());

        Assert.AreEqual(FinanciamientoReportSnapshotBuilder.TipoReporte, snapshot.TipoReporte);
        Assert.AreEqual("Financiamiento", snapshot.TipoReporte);
        Assert.AreEqual(5, snapshot.ProyectoId);
        Assert.AreEqual("Análisis de Financiamiento", snapshot.Titulo);
        Assert.AreEqual(15, snapshot.Columnas.Count, "Sin configuración se usan los defaults del catálogo.");
    }

    [TestMethod]
    public void Build_TituloNulo_SeNormalizaAVacio()
    {
        var snapshot = new FinanciamientoReportSnapshotBuilder()
            .Build(1, null, Array.Empty<ColumnaFinanciamiento>());

        Assert.AreEqual(string.Empty, snapshot.Titulo);
    }

    [TestMethod]
    public void Build_PropagaLosDecimalesGlobalesDelProyecto()
    {
        var snapshot = new FinanciamientoReportSnapshotBuilder()
            .Build(1, "F", new[] { Columna("colCD", "CD $") },
                decimalesCantidad: 5, decimalesImporte: 3, decimalesPorcentaje: 4);

        Assert.AreEqual(5, snapshot.DecimalesCantidad);
        Assert.AreEqual(3, snapshot.DecimalesImporte);
        Assert.AreEqual(4, snapshot.DecimalesPorcentaje);
    }

    // ───────────────────────── Defaults neutrales ─────────────────────────

    [TestMethod]
    public void Build_SinColumnas_UsaDefaultsDelCatalogo()
    {
        var snapshot = new FinanciamientoReportSnapshotBuilder()
            .Build(1, "F", columnas: null);

        CollectionAssert.AreEqual(
            IdentificadoresDefault,
            snapshot.Columnas.Select(c => c.Identificador).ToArray());
    }

    [TestMethod]
    public void DefaultColumns_ExponeLasQuinceColumnasDelCatalogo()
    {
        var cols = FinanciamientoReportSnapshotBuilder.DefaultColumns();

        CollectionAssert.AreEqual(IdentificadoresDefault, cols.Select(c => c.Identificador).ToArray());

        var cd = cols.Single(c => c.Identificador == "colCD");
        Assert.IsTrue(cd.EsMoneda, "El costo directo es rol monetario (token C2).");
        Assert.IsTrue(cd.EsNumerica);

        var tasa = cols.Single(c => c.Identificador == "colTasa");
        Assert.IsFalse(tasa.EsMoneda, "La tasa no es monetaria.");
        Assert.IsFalse(tasa.EsNumerica);
        Assert.AreEqual(string.Empty, tasa.FormatoNumerico);
    }

    [TestMethod]
    public void Build_IgnoraElementosNulos()
    {
        var columnas = new ColumnaFinanciamiento[] { null!, Columna("colCD", "CD $") };

        var snapshot = new FinanciamientoReportSnapshotBuilder().Build(1, "F", columnas);

        Assert.AreEqual(1, snapshot.Columnas.Count);
        Assert.AreEqual("colCD", snapshot.Columnas.Single().Identificador);
    }

    // ───────────────── Visibilidad, orden, ancho y encabezado ─────────────────

    [TestMethod]
    public void Build_PropagaVisibilidadOrdenAnchoYEncabezadoTalCual()
    {
        var columnas = new[]
        {
            Columna("colEgresos", "Egreso $", orden: 2, visible: false, ancho: 105),
            Columna("colCD", "CD $", orden: 1, ancho: 95,
                alineacion: AlineacionColumna.Derecha, formato: "C2"),
            Columna("colPeriodo", "Período", orden: 0, ancho: 90)
        };

        var snapshot = new FinanciamientoReportSnapshotBuilder().Build(1, "F", columnas);

        CollectionAssert.AreEqual(
            new[] { "colPeriodo", "colCD", "colEgresos" },
            snapshot.Columnas.Select(c => c.Identificador).ToArray());

        var periodo = snapshot.Columnas[0];
        Assert.AreEqual("Período", periodo.Encabezado);
        Assert.IsTrue(periodo.Visible);
        Assert.AreEqual(0, periodo.Orden);
        Assert.AreEqual(90, periodo.Ancho);

        var egreso = snapshot.Columnas[2];
        Assert.AreEqual("Egreso $", egreso.Encabezado);
        Assert.IsFalse(egreso.Visible);
        Assert.AreEqual(2, egreso.Orden);
        Assert.AreEqual(105, egreso.Ancho);
    }

    // ───────────────────────── Estilo de tabla ─────────────────────────

    [TestMethod]
    public void Build_EstiloTabla_EsLegacyCatalogo()
    {
        var snapshot = new FinanciamientoReportSnapshotBuilder()
            .Build(1, "F", new[] { Columna("colPeriodo", "Período") });

        Assert.AreEqual(ReportTableStyle.LegacyCatalogo(), snapshot.EstiloTabla);
    }

    [TestMethod]
    public void Build_EncabezadoEsNeutralAunqueLaEntidadTengaEstiloContenidoPropio()
    {
        var columna = Columna(
            "colCD",
            "CD $",
            alineacion: AlineacionColumna.Derecha,
            fuente: "Consolas",
            tamano: 12,
            colorFuente: "#010203",
            colorFondo: "#040506",
            negrita: true);

        var snapshot = new FinanciamientoReportSnapshotBuilder().Build(1, "F", new[] { columna });

        var def = snapshot.Columnas.Single();

        Assert.AreEqual(ReportTableStyle.LegacyCatalogo().EstiloEncabezado, def.EstiloEncabezado);
        Assert.AreEqual("Consolas", def.EstiloContenido.Fuente);
        Assert.AreEqual(12f, def.EstiloContenido.Tamano);
        Assert.AreEqual("#010203", def.EstiloContenido.ColorFuente);
        Assert.AreEqual("#040506", def.EstiloContenido.ColorFondo);
        Assert.IsTrue(def.EstiloContenido.Negrita);
    }

    // ────────────── Misma lista fuente para PDF y Excel ──────────────

    [TestMethod]
    public void Build_UnSoloBuild_ExponeLaMismaInstanciaDeDefinicionesParaPdfYExcel()
    {
        var columnas = new[]
        {
            Columna("colPeriodo", "Período", orden: 0),
            Columna("colCD", "CD $", orden: 1, alineacion: AlineacionColumna.Derecha, formato: "C2")
        };

        var snapshot = new FinanciamientoReportSnapshotBuilder().Build(1, "F", columnas);

        var definicionesParaPdf = snapshot.Columnas;
        var definicionesParaExcel = snapshot.Columnas;

        Assert.AreSame(definicionesParaPdf, definicionesParaExcel);
        Assert.IsInstanceOfType(definicionesParaPdf, typeof(IReadOnlyList<ReportColumnDefinition>));
        CollectionAssert.AreEqual(
            definicionesParaPdf.Select(c => c.Identificador).ToArray(),
            definicionesParaExcel.Select(c => c.Identificador).ToArray());
    }

    // ───────────────────── Formatos y tipos ─────────────────────

    [TestMethod]
    public void Build_CostoDirectoEsMonetarioYColumnaTextoNoNumerica()
    {
        var columnas = new[]
        {
            Columna("colCD", "CD $", orden: 0,
                alineacion: AlineacionColumna.Derecha, formato: "C2"),
            Columna("colPeriodo", "Período", orden: 1, alineacion: AlineacionColumna.Centro)
        };

        var snapshot = new FinanciamientoReportSnapshotBuilder().Build(1, "F", columnas);

        var cd = snapshot.Columnas.Single(c => c.Identificador == "colCD");
        Assert.IsTrue(cd.EsNumerica);
        Assert.IsTrue(cd.EsMoneda, "El costo directo es rol monetario canónico.");
        Assert.AreEqual(ReportTextAlignment.Derecha, cd.Alineacion);

        var periodo = snapshot.Columnas.Single(c => c.Identificador == "colPeriodo");
        Assert.IsFalse(periodo.EsNumerica);
        Assert.IsFalse(periodo.EsMoneda);
    }

    [TestMethod]
    public void Build_FormatoVacioPeroAlineacionDerecha_InfiereNumerica()
    {
        var columna = Columna("colCD", "CD $", alineacion: AlineacionColumna.Derecha, formato: "");

        var snapshot = new FinanciamientoReportSnapshotBuilder().Build(1, "F", new[] { columna });

        var def = snapshot.Columnas.Single();
        Assert.IsTrue(def.EsNumerica);
        Assert.AreEqual("N2", def.FormatoNumerico);
        Assert.AreEqual(ReportTextAlignment.Derecha, def.Alineacion);
    }

    // ────────────── Exclusión de internas ──────────────

    [TestMethod]
    public void Build_OrdenaPorOrdenYExcluyeColumnasInternas()
    {
        var columnas = new[]
        {
            Columna("Tipo", "Tipo", orden: -1),
            Columna("colRelleno", "", orden: 99),
            Columna("colCD", "CD $", orden: 1),
            Columna("colPeriodo", "Período", orden: 0)
        };

        var snapshot = new FinanciamientoReportSnapshotBuilder().Build(1, "F", columnas);

        CollectionAssert.AreEqual(
            new[] { "colPeriodo", "colCD" },
            snapshot.Columnas.Select(c => c.Identificador).ToArray());
        Assert.IsFalse(snapshot.Columnas.Any(c => ReportColumnDefinitionMapper.EsColumnaInterna(c.Identificador)));
    }

    // ────────────── Wrap, alineación vertical y colores nulos ──────────────

    [TestMethod]
    public void Build_PropagaWrapYAlineacionVertical()
    {
        var columnas = new[]
        {
            Columna("A", "A", orden: 0, alineacionVertical: 0, wrap: true),
            Columna("B", "B", orden: 1, alineacionVertical: 1),
            Columna("C", "C", orden: 2, alineacionVertical: 2),
            Columna("D", "D", orden: 3, alineacionVertical: 9)
        };

        var snapshot = new FinanciamientoReportSnapshotBuilder().Build(1, "F", columnas);

        Assert.IsTrue(snapshot.Columnas[0].Wrap);
        Assert.AreEqual(ReportVerticalAlignment.Superior, snapshot.Columnas[0].AlineacionVertical);
        Assert.AreEqual(ReportVerticalAlignment.Medio, snapshot.Columnas[1].AlineacionVertical);
        Assert.AreEqual(ReportVerticalAlignment.Inferior, snapshot.Columnas[2].AlineacionVertical);
        Assert.AreEqual(ReportVerticalAlignment.Medio, snapshot.Columnas[3].AlineacionVertical);
    }

    [TestMethod]
    public void Build_ColoresContenidoNulos_UsanDefaultLegacyCatalogo()
    {
        var columna = Columna(
            "colCD",
            "CD $",
            fuente: null,
            tamano: 0,
            colorFuente: null,
            colorFondo: null);

        var snapshot = new FinanciamientoReportSnapshotBuilder().Build(1, "F", new[] { columna });

        Assert.AreEqual(ReportTableStyle.LegacyCatalogo().EstiloContenido, snapshot.Columnas.Single().EstiloContenido);
    }

    // ────────────── Inmutabilidad y robustez ──────────────

    [TestMethod]
    public void Build_ColumnasEsReadOnlyListYOrdenEstable()
    {
        var columnas = new[]
        {
            Columna("C", "C", orden: 2),
            Columna("A", "A", orden: 0),
            Columna("B", "B", orden: 1)
        };

        var snapshot = new FinanciamientoReportSnapshotBuilder().Build(1, "F", columnas);

        Assert.IsInstanceOfType(snapshot.Columnas, typeof(IReadOnlyList<ReportColumnDefinition>));

        var primera = snapshot.Columnas.Select(c => c.Identificador).ToArray();
        var segunda = snapshot.Columnas.Select(c => c.Identificador).ToArray();
        CollectionAssert.AreEqual(new[] { "A", "B", "C" }, primera);
        CollectionAssert.AreEqual(primera, segunda);
        Assert.IsTrue(snapshot.Columnas.All(c => c is not null));
    }
}
