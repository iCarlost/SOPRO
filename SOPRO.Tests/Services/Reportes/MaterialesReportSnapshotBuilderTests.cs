using Microsoft.VisualStudio.TestTools.UnitTesting;
using SOPRO.Application.Models.Reporting.ReportColumns;
using SOPRO.Application.UseCases.Reporting;
using SOPRO.Core.Entities;

namespace SOPRO.Tests.Services.Reportes;

/// <summary>
/// Verificación del <see cref="MaterialesReportSnapshotBuilder"/>: snapshot neutral
/// de columnas del Catálogo de Materiales construido desde <c>ColumnaMaterial</c>
/// como única fuente.
///
/// Cubre visibilidad, orden, ancho, encabezado y formatos (incluido C4), el estilo
/// neutral <see cref="ReportTableStyle.LegacyMateriales"/>, la reutilización de una
/// sola instancia de definiciones para los consumidores PDF/Excel, y huecos del
/// mapper (inferencia numérica por alineación derecha, alineación vertical 0/1/2,
/// wrap, colores nulos) ejercitados a través del builder real.
/// </summary>
[TestClass]
public class MaterialesReportSnapshotBuilderTests
{
    // ─────────────────────────── Helpers de datos ───────────────────────────

    private static ColumnaMaterial Columna(
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
        var snapshot = new MaterialesReportSnapshotBuilder()
            .Build(5, "Catálogo de Materiales", Array.Empty<ColumnaMaterial>());

        Assert.AreEqual(MaterialesReportSnapshotBuilder.TipoReporte, snapshot.TipoReporte);
        Assert.AreEqual("Materiales", snapshot.TipoReporte);
        Assert.AreEqual(5, snapshot.ProyectoId);
        Assert.AreEqual("Catálogo de Materiales", snapshot.Titulo);
        Assert.AreEqual(0, snapshot.Columnas.Count);
    }

    [TestMethod]
    public void Build_TituloNulo_SeNormalizaAVacio()
    {
        var snapshot = new MaterialesReportSnapshotBuilder()
            .Build(1, null, Array.Empty<ColumnaMaterial>());

        Assert.AreEqual(string.Empty, snapshot.Titulo);
    }

    // ───────────────── Visibilidad, orden, ancho y encabezado ─────────────────

    [TestMethod]
    public void Build_PropagaVisibilidadOrdenAnchoYEncabezadoTalCual()
    {
        var columnas = new[]
        {
            Columna("Descripcion", "Descripción del material", orden: 2, visible: false, ancho: 260),
            Columna("Unidad", "Unidad", orden: 1, ancho: 60),
            Columna("Clave", "Clave", orden: 0, ancho: 90)
        };

        var snapshot = new MaterialesReportSnapshotBuilder().Build(1, "M", columnas);

        CollectionAssert.AreEqual(
            new[] { "Clave", "Unidad", "Descripcion" },
            snapshot.Columnas.Select(c => c.Identificador).ToArray());

        var clave = snapshot.Columnas[0];
        Assert.AreEqual("Clave", clave.Encabezado);
        Assert.IsTrue(clave.Visible);
        Assert.AreEqual(0, clave.Orden);
        Assert.AreEqual(90, clave.Ancho);

        var descripcion = snapshot.Columnas[2];
        Assert.AreEqual("Descripción del material", descripcion.Encabezado);
        Assert.IsFalse(descripcion.Visible);
        Assert.AreEqual(2, descripcion.Orden);
        Assert.AreEqual(260, descripcion.Ancho);
    }

    // ───────────────────────── Estilo de tabla ─────────────────────────

    [TestMethod]
    public void Build_EstiloTabla_EsLegacyMateriales()
    {
        var snapshot = new MaterialesReportSnapshotBuilder()
            .Build(1, "M", new[] { Columna("Clave", "Clave") });

        Assert.AreEqual(ReportTableStyle.LegacyMateriales(), snapshot.EstiloTabla);
    }

    [TestMethod]
    public void Build_EncabezadoEsNeutralAunqueLaEntidadTengaEstiloContenidoPropio()
    {
        var columna = Columna(
            "PrecioUnitario",
            "Precio Unitario",
            alineacion: AlineacionColumna.Derecha,
            fuente: "Consolas",
            tamano: 12,
            colorFuente: "#010203",
            colorFondo: "#040506",
            negrita: true);

        var snapshot = new MaterialesReportSnapshotBuilder().Build(1, "M", new[] { columna });

        var def = snapshot.Columnas.Single();

        // El encabezado siempre es el neutral de LegacyMateriales, no el estilo de la entidad.
        Assert.AreEqual(ReportTableStyle.LegacyMateriales().EstiloEncabezado, def.EstiloEncabezado);

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
            Columna("PrecioUnitario", "Precio Unitario", orden: 1, alineacion: AlineacionColumna.Derecha, formato: "C4")
        };

        var snapshot = new MaterialesReportSnapshotBuilder().Build(1, "M", columnas);

        // Un único Build materializa una sola lista; ambos consumidores leen la misma instancia.
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
    public void Build_PropagaFormatosIncluyendoC4()
    {
        var columnas = new[]
        {
            Columna("Clave", "Clave", orden: 0),
            Columna("PrecioUnitario", "Precio Unitario", orden: 1, alineacion: AlineacionColumna.Derecha, formato: "C4"),
            Columna("Cantidad", "Cantidad", orden: 2, alineacion: AlineacionColumna.Derecha, formato: "N2")
        };

        var snapshot = new MaterialesReportSnapshotBuilder().Build(1, "M", columnas);

        var precio = snapshot.Columnas.Single(c => c.Identificador == "PrecioUnitario");
        Assert.IsTrue(precio.EsNumerica);
        Assert.AreEqual("C4", precio.FormatoNumerico);
        Assert.AreEqual(ReportTextAlignment.Derecha, precio.Alineacion);

        Assert.AreEqual("N2", snapshot.Columnas.Single(c => c.Identificador == "Cantidad").FormatoNumerico);

        var clave = snapshot.Columnas.Single(c => c.Identificador == "Clave");
        Assert.IsFalse(clave.EsNumerica);
        Assert.AreEqual(string.Empty, clave.FormatoNumerico, "Una columna de texto sin formato no debe recibir N2.");
    }

    [TestMethod]
    public void Build_FormatoVacioPeroAlineacionDerecha_InfiereNumerica()
    {
        // Hueco del mapper: ColumnaMaterial no expone TipoDato; lo numérico se
        // infiere del formato persistido o de la alineación derecha.
        var columna = Columna("PrecioUnitario", "Precio Unitario", alineacion: AlineacionColumna.Derecha, formato: "");

        var snapshot = new MaterialesReportSnapshotBuilder().Build(1, "M", new[] { columna });

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
            Columna("PrecioUnitario", "Precio Unitario", orden: 1),
            Columna("Clave", "Clave", orden: 0)
        };

        var snapshot = new MaterialesReportSnapshotBuilder().Build(1, "M", columnas);

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

        var snapshot = new MaterialesReportSnapshotBuilder().Build(1, "M", columnas);

        Assert.IsTrue(snapshot.Columnas[0].Wrap);
        Assert.AreEqual(ReportVerticalAlignment.Superior, snapshot.Columnas[0].AlineacionVertical);
        Assert.AreEqual(ReportVerticalAlignment.Medio, snapshot.Columnas[1].AlineacionVertical);
        Assert.AreEqual(ReportVerticalAlignment.Inferior, snapshot.Columnas[2].AlineacionVertical);
        Assert.AreEqual(ReportVerticalAlignment.Medio, snapshot.Columnas[3].AlineacionVertical);
    }

    [TestMethod]
    public void Build_ColoresContenidoNulos_UsanDefaultLegacyMateriales()
    {
        var columna = Columna(
            "Clave",
            "Clave",
            fuente: null,
            tamano: 0,
            colorFuente: null,
            colorFondo: null);

        var snapshot = new MaterialesReportSnapshotBuilder().Build(1, "M", new[] { columna });

        Assert.AreEqual(ReportTableStyle.LegacyMateriales().EstiloContenido, snapshot.Columnas.Single().EstiloContenido);
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

        var snapshot = new MaterialesReportSnapshotBuilder().Build(1, "M", columnas);

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
            new MaterialesReportSnapshotBuilder().Build(1, "M", columnas: null!));
    }

    [TestMethod]
    public void Build_IgnoraElementosNulos()
    {
        var columnas = new ColumnaMaterial[] { null!, Columna("Clave", "Clave") };

        var snapshot = new MaterialesReportSnapshotBuilder().Build(1, "M", columnas);

        Assert.AreEqual(1, snapshot.Columnas.Count);
        Assert.AreEqual("Clave", snapshot.Columnas.Single().Identificador);
    }
}
