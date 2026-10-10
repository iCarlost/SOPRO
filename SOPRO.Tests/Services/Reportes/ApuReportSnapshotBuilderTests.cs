using Microsoft.VisualStudio.TestTools.UnitTesting;
using SOPRO.Application.Models.Reporting.ReportColumns;
using SOPRO.Application.UseCases.Reporting;
using SOPRO.Core.Entities;

namespace SOPRO.Tests.Services.Reportes;

/// <summary>
/// Verificación del <see cref="ApuReportSnapshotBuilder"/>: snapshot neutral de
/// columnas del reporte de APU construido desde <c>ColumnaPersonalizada</c>
/// (reutilizando el núcleo de <see cref="ReportColumnDefinitionMapper"/> de
/// Presupuesto) con defaults en memoria cuando no hay configuración.
///
/// Cubre visibilidad, orden, ancho, encabezado y roles numéricos (Cantidad =
/// cantidad; Costo Unit. e Importe = monetarios), el estilo neutral
/// <see cref="ReportTableStyle.LegacyCatalogo"/>, la reutilización de una sola
/// instancia de definiciones para los consumidores PDF/Excel, y la conservación de
/// la columna visible <c>Tipo</c> excluyendo únicamente el relleno de grid.
/// </summary>
[TestClass]
public class ApuReportSnapshotBuilderTests
{
    // ─────────────────────────── Helpers de datos ───────────────────────────

    private static ColumnaPersonalizada Columna(
        string nombreInterno,
        string? nombre = null,
        int orden = 0,
        bool visible = true,
        int ancho = 100,
        TipoDatoColumna tipo = TipoDatoColumna.Texto,
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
            TipoDato = tipo,
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

    private static readonly string[] IdentificadoresApu =
        { "Tipo", "Clave", "Descripcion", "Unidad", "Cantidad", "PrecioUnitario", "ImporteTotal" };

    // ───────────────────────── Metadatos del snapshot ─────────────────────────

    [TestMethod]
    public void Build_ProduceSnapshotConTipoProyectoYTitulo()
    {
        var snapshot = new ApuReportSnapshotBuilder()
            .Build(5, "Análisis de Precios Unitarios", Array.Empty<ColumnaPersonalizada>());

        Assert.AreEqual(ApuReportSnapshotBuilder.TipoReporte, snapshot.TipoReporte);
        Assert.AreEqual("APU", snapshot.TipoReporte);
        Assert.AreEqual(5, snapshot.ProyectoId);
        Assert.AreEqual("Análisis de Precios Unitarios", snapshot.Titulo);
        Assert.AreEqual(7, snapshot.Columnas.Count, "Sin configuración se usan los defaults en memoria.");
    }

    [TestMethod]
    public void Build_TituloNulo_SeNormalizaAVacio()
    {
        var snapshot = new ApuReportSnapshotBuilder()
            .Build(1, null, Array.Empty<ColumnaPersonalizada>());

        Assert.AreEqual(string.Empty, snapshot.Titulo);
    }

    [TestMethod]
    public void Build_PropagaLosDecimalesGlobalesDelProyecto()
    {
        var snapshot = new ApuReportSnapshotBuilder()
            .Build(1, "APU", new[] { Columna("Clave", "Clave") },
                decimalesCantidad: 5, decimalesImporte: 3, decimalesPorcentaje: 4);

        Assert.AreEqual(5, snapshot.DecimalesCantidad);
        Assert.AreEqual(3, snapshot.DecimalesImporte);
        Assert.AreEqual(4, snapshot.DecimalesPorcentaje);
    }

    // ───────────────────────── Defaults en memoria ─────────────────────────

    [TestMethod]
    public void Build_SinColumnas_UsaDefaultsEnMemoria()
    {
        var snapshot = new ApuReportSnapshotBuilder().Build(1, "APU", columnas: null);

        CollectionAssert.AreEqual(IdentificadoresApu, snapshot.Columnas.Select(c => c.Identificador).ToArray());
    }

    [TestMethod]
    public void DefaultColumns_ExponeLasSieteColumnasDelApuConSusRoles()
    {
        var cols = ApuReportSnapshotBuilder.DefaultColumns();

        CollectionAssert.AreEqual(IdentificadoresApu, cols.Select(c => c.Identificador).ToArray());

        var cantidad = cols.Single(c => c.Identificador == "Cantidad");
        Assert.IsTrue(cantidad.EsNumerica);
        Assert.IsFalse(cantidad.EsMoneda, "La Cantidad es rol de cantidad, no monetario.");

        var costo = cols.Single(c => c.Identificador == "PrecioUnitario");
        Assert.IsTrue(costo.EsNumerica);
        Assert.IsTrue(costo.EsMoneda, "El Costo Unitario es rol monetario.");

        var importe = cols.Single(c => c.Identificador == "ImporteTotal");
        Assert.IsTrue(importe.EsMoneda, "El Importe es rol monetario.");

        Assert.IsFalse(cols.Single(c => c.Identificador == "Clave").EsNumerica);
        Assert.AreEqual("Costo Unit.", costo.Encabezado);
    }

    [TestMethod]
    public void Build_IgnoraElementosNulos()
    {
        var columnas = new ColumnaPersonalizada[] { null!, Columna("Clave", "Clave") };

        var snapshot = new ApuReportSnapshotBuilder().Build(1, "APU", columnas);

        Assert.AreEqual(1, snapshot.Columnas.Count);
        Assert.AreEqual("Clave", snapshot.Columnas.Single().Identificador);
    }

    // ───────────────── Visibilidad, orden, ancho y encabezado ─────────────────

    [TestMethod]
    public void Build_PropagaVisibilidadOrdenAnchoYEncabezadoTalCual()
    {
        var columnas = new[]
        {
            Columna("Descripcion", "Descripción del insumo", orden: 2, visible: false, ancho: 250, wrap: true),
            Columna("Cantidad", "Cantidad", orden: 1, ancho: 80,
                tipo: TipoDatoColumna.Numerico, alineacion: AlineacionColumna.Derecha, formato: "N2"),
            Columna("Clave", "Clave", orden: 0, ancho: 80, alineacion: AlineacionColumna.Centro)
        };

        var snapshot = new ApuReportSnapshotBuilder().Build(1, "APU", columnas);

        CollectionAssert.AreEqual(
            new[] { "Clave", "Cantidad", "Descripcion" },
            snapshot.Columnas.Select(c => c.Identificador).ToArray());

        var clave = snapshot.Columnas[0];
        Assert.AreEqual("Clave", clave.Encabezado);
        Assert.IsTrue(clave.Visible);
        Assert.AreEqual(0, clave.Orden);
        Assert.AreEqual(80, clave.Ancho);

        var descripcion = snapshot.Columnas[2];
        Assert.AreEqual("Descripción del insumo", descripcion.Encabezado);
        Assert.IsFalse(descripcion.Visible);
        Assert.AreEqual(250, descripcion.Ancho);
        Assert.IsTrue(descripcion.Wrap);
    }

    // ───────────────────────── Estilo de tabla ─────────────────────────

    [TestMethod]
    public void Build_EstiloTabla_EsLegacyCatalogo()
    {
        var snapshot = new ApuReportSnapshotBuilder()
            .Build(1, "APU", new[] { Columna("Clave", "Clave") });

        Assert.AreEqual(ReportTableStyle.LegacyCatalogo(), snapshot.EstiloTabla);
    }

    [TestMethod]
    public void Build_EncabezadoEsLegacyCatalogoAunqueLaEntidadTengaEstiloContenidoPropio()
    {
        var columna = Columna(
            "PrecioUnitario",
            "Costo Unit.",
            tipo: TipoDatoColumna.Moneda,
            alineacion: AlineacionColumna.Derecha,
            fuente: "Consolas",
            tamano: 12,
            colorFuente: "#010203",
            colorFondo: "#040506",
            negrita: true);

        var snapshot = new ApuReportSnapshotBuilder().Build(1, "APU", new[] { columna });

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
            Columna("Clave", "Clave", orden: 0),
            Columna("ImporteTotal", "Importe", orden: 1, tipo: TipoDatoColumna.Moneda, alineacion: AlineacionColumna.Derecha)
        };

        var snapshot = new ApuReportSnapshotBuilder().Build(1, "APU", columnas);

        var definicionesParaPdf = snapshot.Columnas;
        var definicionesParaExcel = snapshot.Columnas;

        Assert.AreSame(definicionesParaPdf, definicionesParaExcel);
        Assert.IsInstanceOfType(definicionesParaPdf, typeof(IReadOnlyList<ReportColumnDefinition>));
    }

    // ────────────── Columna Tipo visible y exclusión del relleno ──────────────

    [TestMethod]
    public void Build_MantieneLaColumnaTipoVisibleYExcluyeSoloElRelleno()
    {
        var columnas = new[]
        {
            Columna("Tipo", "Tipo", orden: 0, alineacion: AlineacionColumna.Centro),
            Columna("colRelleno", "", orden: 99),
            Columna("Clave", "Clave", orden: 1)
        };

        var snapshot = new ApuReportSnapshotBuilder().Build(1, "APU", columnas);

        CollectionAssert.AreEqual(
            new[] { "Tipo", "Clave" },
            snapshot.Columnas.Select(c => c.Identificador).ToArray(),
            "Tipo es una columna visible del APU; sólo el relleno de grid se excluye.");
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

        var snapshot = new ApuReportSnapshotBuilder().Build(1, "APU", columnas);

        Assert.IsTrue(snapshot.Columnas[0].Wrap);
        Assert.AreEqual(ReportVerticalAlignment.Superior, snapshot.Columnas[0].AlineacionVertical);
        Assert.AreEqual(ReportVerticalAlignment.Medio, snapshot.Columnas[1].AlineacionVertical);
        Assert.AreEqual(ReportVerticalAlignment.Inferior, snapshot.Columnas[2].AlineacionVertical);
        Assert.AreEqual(ReportVerticalAlignment.Medio, snapshot.Columnas[3].AlineacionVertical);
    }

    [TestMethod]
    public void Build_ColoresContenidoNulos_UsanDefaultLegacyCatalogo()
    {
        var columna = Columna("Clave", "Clave", fuente: null, tamano: 0, colorFuente: null, colorFondo: null);

        var snapshot = new ApuReportSnapshotBuilder().Build(1, "APU", new[] { columna });

        Assert.AreEqual(ReportTableStyle.LegacyCatalogo().EstiloContenido, snapshot.Columnas.Single().EstiloContenido);
    }

    [TestMethod]
    public void Build_ColumnasEsReadOnlyListYOrdenEstable()
    {
        var columnas = new[]
        {
            Columna("C", "C", orden: 2),
            Columna("A", "A", orden: 0),
            Columna("B", "B", orden: 1)
        };

        var snapshot = new ApuReportSnapshotBuilder().Build(1, "APU", columnas);

        Assert.IsInstanceOfType(snapshot.Columnas, typeof(IReadOnlyList<ReportColumnDefinition>));
        CollectionAssert.AreEqual(
            new[] { "A", "B", "C" },
            snapshot.Columnas.Select(c => c.Identificador).ToArray());
    }
}
