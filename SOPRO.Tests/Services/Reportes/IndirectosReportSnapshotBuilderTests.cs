using Microsoft.VisualStudio.TestTools.UnitTesting;
using SOPRO.Application.Models.Reporting.ReportColumns;
using SOPRO.Application.UseCases.Reporting;
using SOPRO.Core.Entities;

namespace SOPRO.Tests.Services.Reportes;

/// <summary>
/// Verificación del <see cref="IndirectosReportSnapshotBuilder"/>: snapshot
/// neutral de columnas del reporte de Cálculo de Indirectos construido desde
/// <c>ColumnaIndirectos</c> como fuente, con defaults neutrales cuando no hay
/// configuración persistida.
///
/// Cubre visibilidad, orden, ancho, encabezado y formatos (incluido el rol
/// monetario de Importe Mensual/Total y la duración textual), el estilo neutral
/// <see cref="ReportTableStyle.LegacyCatalogo"/>, la reutilización de una sola
/// instancia de definiciones para los consumidores PDF/Excel, y huecos del mapper
/// (inferencia numérica por alineación derecha, alineación vertical 0/1/2, wrap,
/// colores nulos) ejercitados a través del builder real.
/// </summary>
[TestClass]
public class IndirectosReportSnapshotBuilderTests
{
    // ─────────────────────────── Helpers de datos ───────────────────────────

    private static ColumnaIndirectos Columna(
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

    // ───────────────────────── Metadatos del snapshot ─────────────────────────

    [TestMethod]
    public void Build_ProduceSnapshotConTipoProyectoYTitulo()
    {
        var snapshot = new IndirectosReportSnapshotBuilder()
            .Build(5, "Análisis de Costos Indirectos", Array.Empty<ColumnaIndirectos>());

        Assert.AreEqual(IndirectosReportSnapshotBuilder.TipoReporte, snapshot.TipoReporte);
        Assert.AreEqual("Indirectos", snapshot.TipoReporte);
        Assert.AreEqual(5, snapshot.ProyectoId);
        Assert.AreEqual("Análisis de Costos Indirectos", snapshot.Titulo);
        Assert.AreEqual(4, snapshot.Columnas.Count, "Sin configuración se usan los defaults del catálogo.");
    }

    [TestMethod]
    public void Build_TituloNulo_SeNormalizaAVacio()
    {
        var snapshot = new IndirectosReportSnapshotBuilder()
            .Build(1, null, Array.Empty<ColumnaIndirectos>());

        Assert.AreEqual(string.Empty, snapshot.Titulo);
    }

    [TestMethod]
    public void Build_PropagaLosDecimalesGlobalesDelProyecto()
    {
        var snapshot = new IndirectosReportSnapshotBuilder()
            .Build(1, "I", new[] { Columna("Grupo", "Grupo") },
                decimalesCantidad: 3, decimalesImporte: 4, decimalesPorcentaje: 2);

        Assert.AreEqual(3, snapshot.DecimalesCantidad);
        Assert.AreEqual(4, snapshot.DecimalesImporte);
        Assert.AreEqual(2, snapshot.DecimalesPorcentaje);
    }

    // ───────────────────────── Defaults neutrales ─────────────────────────

    [TestMethod]
    public void Build_SinColumnas_UsaDefaultsDelCatalogo()
    {
        var snapshot = new IndirectosReportSnapshotBuilder()
            .Build(1, "I", columnas: null);

        CollectionAssert.AreEqual(
            new[] { "Grupo", "ImporteMensual", "Duracion", "ImporteTotal" },
            snapshot.Columnas.Select(c => c.Identificador).ToArray());
    }

    [TestMethod]
    public void DefaultColumns_ExponeLasCuatroColumnasDelCatalogo()
    {
        var cols = IndirectosReportSnapshotBuilder.DefaultColumns();

        CollectionAssert.AreEqual(
            new[] { "Grupo", "ImporteMensual", "Duracion", "ImporteTotal" },
            cols.Select(c => c.Identificador).ToArray());

        var mensual = cols.Single(c => c.Identificador == "ImporteMensual");
        Assert.IsTrue(mensual.EsMoneda, "Importe Mensual es rol monetario canónico.");
        Assert.IsTrue(mensual.EsNumerica);

        var total = cols.Single(c => c.Identificador == "ImporteTotal");
        Assert.IsTrue(total.EsMoneda, "Importe Total es rol monetario canónico.");
        Assert.IsTrue(total.EsNumerica);

        var duracion = cols.Single(c => c.Identificador == "Duracion");
        Assert.IsFalse(duracion.EsNumerica, "La duración es entero textual (sin formato ni alineación derecha).");

        Assert.IsFalse(cols.Single(c => c.Identificador == "Grupo").EsNumerica);
    }

    [TestMethod]
    public void Build_IgnoraElementosNulos()
    {
        var columnas = new ColumnaIndirectos[] { null!, Columna("Grupo", "Grupo") };

        var snapshot = new IndirectosReportSnapshotBuilder().Build(1, "I", columnas);

        Assert.AreEqual(1, snapshot.Columnas.Count);
        Assert.AreEqual("Grupo", snapshot.Columnas.Single().Identificador);
    }

    // ───────────────── Visibilidad, orden, ancho y encabezado ─────────────────

    [TestMethod]
    public void Build_PropagaVisibilidadOrdenAnchoYEncabezadoTalCual()
    {
        var columnas = new[]
        {
            Columna("ImporteTotal", "Importe Total", orden: 3, visible: false, ancho: 180),
            Columna("ImporteMensual", "Importe Mensual", orden: 1, ancho: 180,
                alineacion: AlineacionColumna.Derecha, formato: "N2"),
            Columna("Grupo", "Grupo / Concepto", orden: 0, ancho: 400)
        };

        var snapshot = new IndirectosReportSnapshotBuilder().Build(1, "I", columnas);

        CollectionAssert.AreEqual(
            new[] { "Grupo", "ImporteMensual", "ImporteTotal" },
            snapshot.Columnas.Select(c => c.Identificador).ToArray());

        var grupo = snapshot.Columnas[0];
        Assert.AreEqual("Grupo / Concepto", grupo.Encabezado);
        Assert.IsTrue(grupo.Visible);
        Assert.AreEqual(0, grupo.Orden);
        Assert.AreEqual(400, grupo.Ancho);

        var total = snapshot.Columnas[2];
        Assert.AreEqual("Importe Total", total.Encabezado);
        Assert.IsFalse(total.Visible);
        Assert.AreEqual(3, total.Orden);
        Assert.AreEqual(180, total.Ancho);
    }

    // ───────────────────────── Estilo de tabla ─────────────────────────

    [TestMethod]
    public void Build_EstiloTabla_EsLegacyCatalogo()
    {
        var snapshot = new IndirectosReportSnapshotBuilder()
            .Build(1, "I", new[] { Columna("Grupo", "Grupo") });

        Assert.AreEqual(ReportTableStyle.LegacyCatalogo(), snapshot.EstiloTabla);
    }

    [TestMethod]
    public void Build_EncabezadoEsNeutralAunqueLaEntidadTengaEstiloContenidoPropio()
    {
        var columna = Columna(
            "ImporteTotal",
            "Importe Total",
            alineacion: AlineacionColumna.Derecha,
            fuente: "Consolas",
            tamano: 12,
            colorFuente: "#010203",
            colorFondo: "#040506",
            negrita: true);

        var snapshot = new IndirectosReportSnapshotBuilder().Build(1, "I", new[] { columna });

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
            Columna("Grupo", "Grupo / Concepto", orden: 0),
            Columna("ImporteTotal", "Importe Total", orden: 1, alineacion: AlineacionColumna.Derecha, formato: "N2")
        };

        var snapshot = new IndirectosReportSnapshotBuilder().Build(1, "I", columnas);

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
    public void Build_ImportesSonMonetariosYDuracionNoEsNumerica()
    {
        var columnas = new[]
        {
            Columna("ImporteMensual", "Importe Mensual", orden: 0,
                alineacion: AlineacionColumna.Derecha, formato: "N2"),
            Columna("Duracion", "Duración", orden: 1, alineacion: AlineacionColumna.Centro)
        };

        var snapshot = new IndirectosReportSnapshotBuilder().Build(1, "I", columnas);

        var mensual = snapshot.Columnas.Single(c => c.Identificador == "ImporteMensual");
        Assert.IsTrue(mensual.EsNumerica);
        Assert.IsTrue(mensual.EsMoneda, "Importe Mensual es rol monetario canónico.");
        Assert.AreEqual(ReportTextAlignment.Derecha, mensual.Alineacion);

        var duracion = snapshot.Columnas.Single(c => c.Identificador == "Duracion");
        Assert.IsFalse(duracion.EsNumerica);
        Assert.IsFalse(duracion.EsMoneda);
        Assert.AreEqual(string.Empty, duracion.FormatoNumerico);
    }

    [TestMethod]
    public void Build_FormatoVacioPeroAlineacionDerecha_InfiereNumerica()
    {
        var columna = Columna("ImporteMensual", "Importe Mensual", alineacion: AlineacionColumna.Derecha, formato: "");

        var snapshot = new IndirectosReportSnapshotBuilder().Build(1, "I", new[] { columna });

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
            Columna("ImporteTotal", "Importe Total", orden: 1),
            Columna("Grupo", "Grupo", orden: 0)
        };

        var snapshot = new IndirectosReportSnapshotBuilder().Build(1, "I", columnas);

        CollectionAssert.AreEqual(
            new[] { "Grupo", "ImporteTotal" },
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

        var snapshot = new IndirectosReportSnapshotBuilder().Build(1, "I", columnas);

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
            "Grupo",
            "Grupo",
            fuente: null,
            tamano: 0,
            colorFuente: null,
            colorFondo: null);

        var snapshot = new IndirectosReportSnapshotBuilder().Build(1, "I", new[] { columna });

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

        var snapshot = new IndirectosReportSnapshotBuilder().Build(1, "I", columnas);

        Assert.IsInstanceOfType(snapshot.Columnas, typeof(IReadOnlyList<ReportColumnDefinition>));

        var primera = snapshot.Columnas.Select(c => c.Identificador).ToArray();
        var segunda = snapshot.Columnas.Select(c => c.Identificador).ToArray();
        CollectionAssert.AreEqual(new[] { "A", "B", "C" }, primera);
        CollectionAssert.AreEqual(primera, segunda);
        Assert.IsTrue(snapshot.Columnas.All(c => c is not null));
    }
}
