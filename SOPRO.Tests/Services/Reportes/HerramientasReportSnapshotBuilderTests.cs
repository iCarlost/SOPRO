using Microsoft.VisualStudio.TestTools.UnitTesting;
using SOPRO.Application.Models.Reporting.ReportColumns;
using SOPRO.Application.UseCases.Reporting;
using SOPRO.Core.Entities;

namespace SOPRO.Tests.Services.Reportes;

/// <summary>
/// Verificación del <see cref="HerramientasReportSnapshotBuilder"/>: snapshot neutral
/// de columnas del Catálogo de Herramientas construido desde <c>ColumnaHerramienta</c>
/// como única fuente.
///
/// Cubre visibilidad, orden, ancho, encabezado y formatos (incluido el Precio/
/// Porcentaje marcado como moneda), el estilo neutral
/// <see cref="ReportTableStyle.LegacyCatalogo"/>, la reutilización de una sola
/// instancia de definiciones para los consumidores PDF/Excel, y huecos del mapper
/// (inferencia numérica por alineación derecha, alineación vertical 0/1/2, wrap,
/// colores nulos) ejercitados a través del builder real.
/// </summary>
[TestClass]
public class HerramientasReportSnapshotBuilderTests
{
    // ─────────────────────────── Helpers de datos ───────────────────────────

    private static ColumnaHerramienta Columna(
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
        var snapshot = new HerramientasReportSnapshotBuilder()
            .Build(5, "Catálogo de Herramientas", Array.Empty<ColumnaHerramienta>());

        Assert.AreEqual(HerramientasReportSnapshotBuilder.TipoReporte, snapshot.TipoReporte);
        Assert.AreEqual("Herramientas", snapshot.TipoReporte);
        Assert.AreEqual(5, snapshot.ProyectoId);
        Assert.AreEqual("Catálogo de Herramientas", snapshot.Titulo);
        Assert.AreEqual(0, snapshot.Columnas.Count);
    }

    [TestMethod]
    public void Build_TituloNulo_SeNormalizaAVacio()
    {
        var snapshot = new HerramientasReportSnapshotBuilder()
            .Build(1, null, Array.Empty<ColumnaHerramienta>());

        Assert.AreEqual(string.Empty, snapshot.Titulo);
    }

    // ───────────────── Visibilidad, orden, ancho y encabezado ─────────────────

    [TestMethod]
    public void Build_PropagaVisibilidadOrdenAnchoYEncabezadoTalCual()
    {
        var columnas = new[]
        {
            Columna("Descripcion", "Descripción de la herramienta", orden: 2, visible: false, ancho: 300),
            Columna("Unidad", "Unidad", orden: 1, ancho: 80),
            Columna("Clave", "Clave", orden: 0, ancho: 110)
        };

        var snapshot = new HerramientasReportSnapshotBuilder().Build(1, "H", columnas);

        CollectionAssert.AreEqual(
            new[] { "Clave", "Unidad", "Descripcion" },
            snapshot.Columnas.Select(c => c.Identificador).ToArray());

        var clave = snapshot.Columnas[0];
        Assert.AreEqual("Clave", clave.Encabezado);
        Assert.IsTrue(clave.Visible);
        Assert.AreEqual(0, clave.Orden);
        Assert.AreEqual(110, clave.Ancho);

        var descripcion = snapshot.Columnas[2];
        Assert.AreEqual("Descripción de la herramienta", descripcion.Encabezado);
        Assert.IsFalse(descripcion.Visible);
        Assert.AreEqual(2, descripcion.Orden);
        Assert.AreEqual(300, descripcion.Ancho);
    }

    // ───────────────────────── Estilo de tabla ─────────────────────────

    [TestMethod]
    public void Build_EstiloTabla_EsLegacyCatalogo()
    {
        var snapshot = new HerramientasReportSnapshotBuilder()
            .Build(1, "H", new[] { Columna("Clave", "Clave") });

        Assert.AreEqual(ReportTableStyle.LegacyCatalogo(), snapshot.EstiloTabla);
        Assert.AreEqual("#4A4A6A", snapshot.EstiloTabla.EstiloEncabezado.ColorFondo);
        Assert.AreEqual("#F5F5F5", snapshot.EstiloTabla.FilaAlterna.ColorFondoAlterno);
        Assert.AreEqual("#DDDDDD", snapshot.EstiloTabla.Bordes.ColorHex);
    }

    [TestMethod]
    public void Build_EncabezadoEsNeutralAunqueLaEntidadTengaEstiloContenidoPropio()
    {
        var columna = Columna(
            "PrecioUnitario",
            "Precio/Porcentaje",
            alineacion: AlineacionColumna.Derecha,
            fuente: "Consolas",
            tamano: 12,
            colorFuente: "#010203",
            colorFondo: "#040506",
            negrita: true);

        var snapshot = new HerramientasReportSnapshotBuilder().Build(1, "H", new[] { columna });

        var def = snapshot.Columnas.Single();

        // El encabezado siempre es el neutral de LegacyCatalogo, no el estilo de la entidad.
        Assert.AreEqual(ReportTableStyle.LegacyCatalogo().EstiloEncabezado, def.EstiloEncabezado);

        // El estilo de contenido sí toma los valores de la entidad.
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
            Columna("PrecioUnitario", "Precio/Porcentaje", orden: 1, alineacion: AlineacionColumna.Derecha)
        };

        var snapshot = new HerramientasReportSnapshotBuilder().Build(1, "H", columnas);

        var definicionesParaPdf = snapshot.Columnas;
        var definicionesParaExcel = snapshot.Columnas;

        Assert.AreSame(definicionesParaPdf, definicionesParaExcel);
        Assert.IsInstanceOfType(definicionesParaPdf, typeof(IReadOnlyList<ReportColumnDefinition>));
        CollectionAssert.AreEqual(
            definicionesParaPdf.Select(c => c.Identificador).ToArray(),
            definicionesParaExcel.Select(c => c.Identificador).ToArray());
    }

    // ───────────────────── Formatos y roles numéricos ─────────────────────

    [TestMethod]
    public void Build_MarcaPrecioUnitarioComoMonedaYClaveComoTexto()
    {
        var columnas = new[]
        {
            Columna("Clave", "Clave", orden: 0, alineacion: AlineacionColumna.Centro),
            Columna("PrecioUnitario", "Precio/Porcentaje", orden: 1, alineacion: AlineacionColumna.Derecha)
        };

        var snapshot = new HerramientasReportSnapshotBuilder().Build(1, "H", columnas);

        var precio = snapshot.Columnas.Single(c => c.Identificador == "PrecioUnitario");
        Assert.IsTrue(precio.EsNumerica, "PrecioUnitario (alineación derecha) debe ser numérica.");
        Assert.IsTrue(precio.EsMoneda, "PrecioUnitario es rol monetario del catálogo (mapper común).");
        Assert.AreEqual("N2", precio.FormatoNumerico);

        var clave = snapshot.Columnas.Single(c => c.Identificador == "Clave");
        Assert.IsFalse(clave.EsNumerica);
        Assert.IsFalse(clave.EsMoneda);
        Assert.AreEqual(string.Empty, clave.FormatoNumerico, "Una columna de texto sin formato no debe recibir N2.");
    }

    [TestMethod]
    public void Build_FormatoVacioPeroAlineacionDerecha_InfiereNumerica()
    {
        // Hueco del mapper: ColumnaHerramienta no expone TipoDato; lo numérico se
        // infiere del formato persistido o de la alineación derecha.
        var columna = Columna("PrecioUnitario", "Precio/Porcentaje", alineacion: AlineacionColumna.Derecha, formato: "");

        var snapshot = new HerramientasReportSnapshotBuilder().Build(1, "H", new[] { columna });

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
            Columna("PrecioUnitario", "Precio/Porcentaje", orden: 1),
            Columna("Clave", "Clave", orden: 0)
        };

        var snapshot = new HerramientasReportSnapshotBuilder().Build(1, "H", columnas);

        CollectionAssert.AreEqual(
            new[] { "Clave", "PrecioUnitario" },
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

        var snapshot = new HerramientasReportSnapshotBuilder().Build(1, "H", columnas);

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
            "Clave",
            "Clave",
            fuente: null,
            tamano: 0,
            colorFuente: null,
            colorFondo: null);

        var snapshot = new HerramientasReportSnapshotBuilder().Build(1, "H", new[] { columna });

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

        var snapshot = new HerramientasReportSnapshotBuilder().Build(1, "H", columnas);

        Assert.IsInstanceOfType(snapshot.Columnas, typeof(IReadOnlyList<ReportColumnDefinition>));

        var primera = snapshot.Columnas.Select(c => c.Identificador).ToArray();
        var segunda = snapshot.Columnas.Select(c => c.Identificador).ToArray();
        CollectionAssert.AreEqual(new[] { "A", "B", "C" }, primera);
        CollectionAssert.AreEqual(primera, segunda);
        Assert.IsTrue(snapshot.Columnas.All(c => c is not null));
    }

    [TestMethod]
    public void Build_ColumnasNulas_LanzaArgumentNullException()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() =>
            new HerramientasReportSnapshotBuilder().Build(1, "H", columnas: null!));
    }

    [TestMethod]
    public void Build_IgnoraElementosNulos()
    {
        var columnas = new ColumnaHerramienta[] { null!, Columna("Clave", "Clave") };

        var snapshot = new HerramientasReportSnapshotBuilder().Build(1, "H", columnas);

        Assert.AreEqual(1, snapshot.Columnas.Count);
        Assert.AreEqual("Clave", snapshot.Columnas.Single().Identificador);
    }

    // ────────────── Decimales del proyecto y moneda (paridad grid) ──────────────

    [TestMethod]
    public void Build_SinDecimalesExplicitos_UsaDefaultsDelContrato()
    {
        var snapshot = new HerramientasReportSnapshotBuilder()
            .Build(1, "H", Array.Empty<ColumnaHerramienta>());

        Assert.AreEqual(2, snapshot.DecimalesCantidad);
        Assert.AreEqual(2, snapshot.DecimalesImporte);
        Assert.AreEqual(4, snapshot.DecimalesPorcentaje);
    }

    [TestMethod]
    public void Build_PropagaDecimalesDelProyecto()
    {
        var snapshot = new HerramientasReportSnapshotBuilder()
            .Build(
                1,
                "H",
                new[] { Columna("PrecioUnitario", "Precio/Porcentaje") },
                decimalesCantidad: 4,
                decimalesImporte: 2,
                decimalesPorcentaje: 3);

        Assert.AreEqual(4, snapshot.DecimalesCantidad);
        Assert.AreEqual(2, snapshot.DecimalesImporte);
        Assert.AreEqual(3, snapshot.DecimalesPorcentaje);
    }
}
