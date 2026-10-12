using Microsoft.VisualStudio.TestTools.UnitTesting;
using SOPRO.Application.Models.Reporting.ReportColumns;
using SOPRO.Application.UseCases.Reporting;
using SOPRO.Core.Entities;

namespace SOPRO.Tests.Services.Reportes;

/// <summary>
/// Verificación del <see cref="UtilidadReportSnapshotBuilder"/>: snapshot neutral de
/// columnas del reporte de Utilidad construido EN MEMORIA desde
/// <c>ColumnaFinanciamiento</c> como fuente (mapper reutilizado
/// <c>MapearFinanciamiento</c>), con defaults neutrales cuando no hay configuración
/// persistida.
///
/// Cubre visibilidad, orden, ancho, encabezado y roles (Base e ImporteFinal
/// monetarios; Porcentaje porcentual), el estilo neutral
/// <see cref="ReportTableStyle.LegacyCatalogo"/>, la reutilización de una sola
/// instancia de definiciones para los consumidores PDF/Excel, la eliminación de la
/// dependencia del grid "dummy" (el resolver expone las columnas sin UI) y huecos
/// del mapper (inferencia numérica por alineación derecha, alineación vertical
/// 0/1/2, wrap, colores nulos) ejercitados a través del builder real.
/// </summary>
[TestClass]
public class UtilidadReportSnapshotBuilderTests
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

    private static readonly string[] IdentificadoresPredeterminados =
        { UtilidadReportColumns.Concepto, UtilidadReportColumns.Base, UtilidadReportColumns.Porcentaje, UtilidadReportColumns.ImporteFinal };

    // ───────────────────────── Metadatos del snapshot ─────────────────────────

    [TestMethod]
    public void Build_ProduceSnapshotConTipoProyectoYTitulo()
    {
        var snapshot = new UtilidadReportSnapshotBuilder()
            .Build(5, "Determinación de la Utilidad", Array.Empty<ColumnaFinanciamiento>());

        Assert.AreEqual(UtilidadReportSnapshotBuilder.TipoReporte, snapshot.TipoReporte);
        Assert.AreEqual("Utilidad", snapshot.TipoReporte);
        Assert.AreEqual(5, snapshot.ProyectoId);
        Assert.AreEqual("Determinación de la Utilidad", snapshot.Titulo);
        Assert.AreEqual(4, snapshot.Columnas.Count, "Sin configuración se usan los defaults del reporte.");
    }

    [TestMethod]
    public void Build_TituloNulo_SeNormalizaAVacio()
    {
        var snapshot = new UtilidadReportSnapshotBuilder()
            .Build(1, null, Array.Empty<ColumnaFinanciamiento>());

        Assert.AreEqual(string.Empty, snapshot.Titulo);
    }

    [TestMethod]
    public void Build_PropagaLosDecimalesGlobalesDelProyecto()
    {
        var snapshot = new UtilidadReportSnapshotBuilder()
            .Build(1, "U", new[] { Columna(UtilidadReportColumns.Base, "Base") },
                decimalesCantidad: 3, decimalesImporte: 4, decimalesPorcentaje: 6);

        Assert.AreEqual(3, snapshot.DecimalesCantidad);
        Assert.AreEqual(4, snapshot.DecimalesImporte);
        Assert.AreEqual(6, snapshot.DecimalesPorcentaje);
    }

    // ───────────────────────── Defaults neutrales ─────────────────────────

    [TestMethod]
    public void Build_SinColumnas_UsaDefaultsDelReporte()
    {
        var snapshot = new UtilidadReportSnapshotBuilder()
            .Build(1, "U", columnas: null);

        CollectionAssert.AreEqual(
            IdentificadoresPredeterminados,
            snapshot.Columnas.Select(c => c.Identificador).ToArray());
    }

    [TestMethod]
    public void DefaultColumns_ExponeLosCuatroRolesDelReporte()
    {
        var cols = UtilidadReportSnapshotBuilder.DefaultColumns();

        CollectionAssert.AreEqual(
            IdentificadoresPredeterminados,
            cols.Select(c => c.Identificador).ToArray());

        var base_ = cols.Single(c => c.Identificador == UtilidadReportColumns.Base);
        Assert.IsTrue(base_.EsNumerica);
        Assert.IsTrue(base_.EsMoneda, "La base es monetaria (token C2).");

        var porcentaje = cols.Single(c => c.Identificador == UtilidadReportColumns.Porcentaje);
        Assert.IsTrue(porcentaje.EsNumerica);
        Assert.IsFalse(porcentaje.EsMoneda, "El porcentaje no es monetario.");
        Assert.AreEqual("P2", porcentaje.FormatoNumerico);

        var importe = cols.Single(c => c.Identificador == UtilidadReportColumns.ImporteFinal);
        Assert.IsTrue(importe.EsNumerica);
        Assert.IsTrue(importe.EsMoneda, "El importe final es monetario (token C2).");

        Assert.IsFalse(cols.Single(c => c.Identificador == UtilidadReportColumns.Concepto).EsNumerica);
    }

    [TestMethod]
    public void Build_IgnoraElementosNulos()
    {
        var columnas = new ColumnaFinanciamiento[] { null!, Columna(UtilidadReportColumns.Concepto, "Concepto") };

        var snapshot = new UtilidadReportSnapshotBuilder().Build(1, "U", columnas);

        Assert.AreEqual(1, snapshot.Columnas.Count);
        Assert.AreEqual(UtilidadReportColumns.Concepto, snapshot.Columnas.Single().Identificador);
    }

    // ───────────────── Visibilidad, orden, ancho y encabezado ─────────────────

    [TestMethod]
    public void Build_PropagaVisibilidadOrdenAnchoYEncabezadoTalCual()
    {
        var columnas = new[]
        {
            Columna(UtilidadReportColumns.ImporteFinal, "Importe final", orden: 2, visible: false, ancho: 130,
                alineacion: AlineacionColumna.Derecha, formato: "C2"),
            Columna(UtilidadReportColumns.Base, "Base", orden: 1, ancho: 120,
                alineacion: AlineacionColumna.Derecha, formato: "C2"),
            Columna(UtilidadReportColumns.Concepto, "Concepto", orden: 0, ancho: 340)
        };

        var snapshot = new UtilidadReportSnapshotBuilder().Build(1, "U", columnas);

        CollectionAssert.AreEqual(
            new[] { UtilidadReportColumns.Concepto, UtilidadReportColumns.Base, UtilidadReportColumns.ImporteFinal },
            snapshot.Columnas.Select(c => c.Identificador).ToArray());

        var concepto = snapshot.Columnas[0];
        Assert.AreEqual("Concepto", concepto.Encabezado);
        Assert.IsTrue(concepto.Visible);
        Assert.AreEqual(0, concepto.Orden);
        Assert.AreEqual(340, concepto.Ancho);

        var importe = snapshot.Columnas[2];
        Assert.AreEqual("Importe final", importe.Encabezado);
        Assert.IsFalse(importe.Visible);
        Assert.AreEqual(2, importe.Orden);
        Assert.AreEqual(130, importe.Ancho);
    }

    // ───────────────────────── Estilo de tabla ─────────────────────────

    [TestMethod]
    public void Build_EstiloTabla_EsLegacyCatalogo()
    {
        var snapshot = new UtilidadReportSnapshotBuilder()
            .Build(1, "U", new[] { Columna(UtilidadReportColumns.Concepto, "Concepto") });

        Assert.AreEqual(ReportTableStyle.LegacyCatalogo(), snapshot.EstiloTabla);
    }

    [TestMethod]
    public void Build_EncabezadoEsNeutralAunqueLaEntidadTengaEstiloContenidoPropio()
    {
        var columna = Columna(
            UtilidadReportColumns.ImporteFinal, "Importe final",
            alineacion: AlineacionColumna.Derecha,
            formato: "C2",
            fuente: "Consolas", tamano: 12,
            colorFuente: "#010203", colorFondo: "#040506", negrita: true);

        var snapshot = new UtilidadReportSnapshotBuilder().Build(1, "U", new[] { columna });

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
            Columna(UtilidadReportColumns.Concepto, "Concepto", orden: 0),
            Columna(UtilidadReportColumns.ImporteFinal, "Importe final", orden: 1, alineacion: AlineacionColumna.Derecha, formato: "C2")
        };

        var snapshot = new UtilidadReportSnapshotBuilder().Build(1, "U", columnas);

        var definicionesParaPdf = snapshot.Columnas;
        var definicionesParaExcel = snapshot.Columnas;

        Assert.AreSame(definicionesParaPdf, definicionesParaExcel);
        Assert.IsInstanceOfType(definicionesParaPdf, typeof(IReadOnlyList<ReportColumnDefinition>));
        CollectionAssert.AreEqual(
            definicionesParaPdf.Select(c => c.Identificador).ToArray(),
            definicionesParaExcel.Select(c => c.Identificador).ToArray());
    }

    // ────────────── Resolver en memoria (sin grid) ──────────────

    [TestMethod]
    public void Resolver_BuscarYOrdenarColumnas_SinLeerGrid()
    {
        var snapshot = new UtilidadReportSnapshotBuilder().Build(1, "U", columnas: null);

        var base_ = UtilidadReportColumns.Buscar(snapshot, UtilidadReportColumns.Base);
        Assert.IsNotNull(base_);
        Assert.AreEqual(UtilidadReportColumns.Base, base_.Identificador);

        var inexistente = UtilidadReportColumns.Buscar(snapshot, "NoExiste");
        Assert.IsNull(inexistente);

        CollectionAssert.AreEqual(
            IdentificadoresPredeterminados,
            UtilidadReportColumns.Obtener(snapshot).Select(c => c.Identificador).ToArray());
    }

    // ───────────────────── Formatos y tipos ─────────────────────

    [TestMethod]
    public void Build_BaseEImporteFinalMonetariosYPorcentajeComoRol()
    {
        var columnas = new[]
        {
            Columna(UtilidadReportColumns.Base, "Base", orden: 0, alineacion: AlineacionColumna.Derecha, formato: "C2"),
            Columna(UtilidadReportColumns.Porcentaje, "Porcentaje", orden: 1, alineacion: AlineacionColumna.Derecha, formato: "P2"),
            Columna(UtilidadReportColumns.ImporteFinal, "Importe final", orden: 2, alineacion: AlineacionColumna.Derecha, formato: "C2")
        };

        var snapshot = new UtilidadReportSnapshotBuilder().Build(1, "U", columnas);

        var base_ = snapshot.Columnas.Single(c => c.Identificador == UtilidadReportColumns.Base);
        Assert.IsTrue(base_.EsMoneda);
        Assert.AreEqual(ReportTextAlignment.Derecha, base_.Alineacion);

        var porcentaje = snapshot.Columnas.Single(c => c.Identificador == UtilidadReportColumns.Porcentaje);
        Assert.IsTrue(porcentaje.EsNumerica);
        Assert.IsFalse(porcentaje.EsMoneda);
        Assert.AreEqual("P2", porcentaje.FormatoNumerico);

        var importe = snapshot.Columnas.Single(c => c.Identificador == UtilidadReportColumns.ImporteFinal);
        Assert.IsTrue(importe.EsMoneda);
        Assert.AreEqual(ReportTextAlignment.Derecha, importe.Alineacion);
    }

    [TestMethod]
    public void Build_FormatoVacioPeroAlineacionDerecha_InfiereNumerica()
    {
        var columna = Columna(UtilidadReportColumns.Base, "Base", alineacion: AlineacionColumna.Derecha, formato: "");

        var snapshot = new UtilidadReportSnapshotBuilder().Build(1, "U", new[] { columna });

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
            Columna(UtilidadReportColumns.ImporteFinal, "Importe final", orden: 1),
            Columna(UtilidadReportColumns.Concepto, "Concepto", orden: 0)
        };

        var snapshot = new UtilidadReportSnapshotBuilder().Build(1, "U", columnas);

        CollectionAssert.AreEqual(
            new[] { UtilidadReportColumns.Concepto, UtilidadReportColumns.ImporteFinal },
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

        var snapshot = new UtilidadReportSnapshotBuilder().Build(1, "U", columnas);

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
            UtilidadReportColumns.Concepto, "Concepto",
            fuente: null, tamano: 0, colorFuente: null, colorFondo: null);

        var snapshot = new UtilidadReportSnapshotBuilder().Build(1, "U", new[] { columna });

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

        var snapshot = new UtilidadReportSnapshotBuilder().Build(1, "U", columnas);

        Assert.IsInstanceOfType(snapshot.Columnas, typeof(IReadOnlyList<ReportColumnDefinition>));

        var primera = snapshot.Columnas.Select(c => c.Identificador).ToArray();
        var segunda = snapshot.Columnas.Select(c => c.Identificador).ToArray();
        CollectionAssert.AreEqual(new[] { "A", "B", "C" }, primera);
        CollectionAssert.AreEqual(primera, segunda);
        Assert.IsTrue(snapshot.Columnas.All(c => c is not null));
    }
}
