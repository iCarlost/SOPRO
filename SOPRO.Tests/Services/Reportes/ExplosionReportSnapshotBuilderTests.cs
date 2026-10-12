using Microsoft.VisualStudio.TestTools.UnitTesting;
using SOPRO.Application.Models.Reporting.ReportColumns;
using SOPRO.Application.Services;
using SOPRO.Application.UseCases.Reporting;
using SOPRO.Core.Entities;

namespace SOPRO.Tests.Services.Reportes;

/// <summary>
/// Verificación del <see cref="ExplosionReportSnapshotBuilder"/> y de
/// <see cref="ExplosionExportResolver"/>: snapshot neutral de columnas del reporte
/// de Explosión de Insumos construido desde <c>ColumnaExplosion</c> como única
/// fuente, más la materialización neutral de valores (texto y numérico) sin tocar
/// grids.
///
/// Cubre visibilidad, orden, ancho, encabezado y formatos (cantidad N4, precio
/// unitario C4, importe C2 y porcentaje P2), el estilo neutral
/// <see cref="ReportTableStyle.LegacyCatalogo"/>, la reutilización de una sola
/// instancia para PDF/Excel, el rol monetario derivado, la normalización del alias
/// <c>Importe</c>→<c>ImporteTotal</c> y los huecos del mapper (inferencia numérica
/// por alineación derecha, alineación vertical 0/1/2, wrap, colores nulos)
/// ejercitados a través del builder real.
/// </summary>
[TestClass]
public class ExplosionReportSnapshotBuilderTests
{
    // ─────────────────────────── Helpers de datos ───────────────────────────

    private static ColumnaExplosion Columna(
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
        var snapshot = new ExplosionReportSnapshotBuilder()
            .Build(5, "Explosión de Insumos", Array.Empty<ColumnaExplosion>());

        Assert.AreEqual(ExplosionReportSnapshotBuilder.TipoReporte, snapshot.TipoReporte);
        Assert.AreEqual("Explosion", snapshot.TipoReporte);
        Assert.AreEqual(5, snapshot.ProyectoId);
        Assert.AreEqual("Explosión de Insumos", snapshot.Titulo);
        Assert.AreEqual(0, snapshot.Columnas.Count);
    }

    [TestMethod]
    public void Build_TituloNulo_SeNormalizaAVacio()
    {
        var snapshot = new ExplosionReportSnapshotBuilder()
            .Build(1, null, Array.Empty<ColumnaExplosion>());

        Assert.AreEqual(string.Empty, snapshot.Titulo);
    }

    // ───────────────── Visibilidad, orden, ancho y encabezado ─────────────────

    [TestMethod]
    public void Build_PropagaVisibilidadOrdenAnchoYEncabezadoTalCual()
    {
        var columnas = new[]
        {
            Columna("Descripcion", "Descripción del insumo", orden: 2, visible: false, ancho: 350),
            Columna("Unidad", "Unidad", orden: 1, ancho: 70),
            Columna("Clave", "Clave", orden: 0, ancho: 80)
        };

        var snapshot = new ExplosionReportSnapshotBuilder().Build(1, "E", columnas);

        CollectionAssert.AreEqual(
            new[] { "Clave", "Unidad", "Descripcion" },
            snapshot.Columnas.Select(c => c.Identificador).ToArray());

        var clave = snapshot.Columnas[0];
        Assert.AreEqual("Clave", clave.Encabezado);
        Assert.IsTrue(clave.Visible);
        Assert.AreEqual(0, clave.Orden);
        Assert.AreEqual(80, clave.Ancho);

        var descripcion = snapshot.Columnas[2];
        Assert.AreEqual("Descripción del insumo", descripcion.Encabezado);
        Assert.IsFalse(descripcion.Visible);
        Assert.AreEqual(2, descripcion.Orden);
        Assert.AreEqual(350, descripcion.Ancho);
    }

    // ───────────────────────── Estilo de tabla ─────────────────────────

    [TestMethod]
    public void Build_EstiloTabla_EsLegacyCatalogo()
    {
        var snapshot = new ExplosionReportSnapshotBuilder()
            .Build(1, "E", new[] { Columna("Clave", "Clave") });

        Assert.AreEqual(ReportTableStyle.LegacyCatalogo(), snapshot.EstiloTabla);
    }

    [TestMethod]
    public void Build_EncabezadoEsNeutralAunqueLaEntidadTengaEstiloContenidoPropio()
    {
        var columna = Columna(
            "PrecioUnitario",
            "P.U.",
            alineacion: AlineacionColumna.Derecha,
            formato: "C4",
            fuente: "Consolas",
            tamano: 12,
            colorFuente: "#010203",
            colorFondo: "#040506",
            negrita: true);

        var snapshot = new ExplosionReportSnapshotBuilder().Build(1, "E", new[] { columna });

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
            Columna("Importe", "Importe", orden: 1, alineacion: AlineacionColumna.Derecha, formato: "C2")
        };

        var snapshot = new ExplosionReportSnapshotBuilder().Build(1, "E", columnas);

        var definicionesParaPdf = snapshot.Columnas;
        var definicionesParaExcel = snapshot.Columnas;

        Assert.AreSame(definicionesParaPdf, definicionesParaExcel);
        Assert.IsInstanceOfType(definicionesParaPdf, typeof(IReadOnlyList<ReportColumnDefinition>));
        CollectionAssert.AreEqual(
            definicionesParaPdf.Select(c => c.Identificador).ToArray(),
            definicionesParaExcel.Select(c => c.Identificador).ToArray());
    }

    // ───────────────────── Formatos y roles ─────────────────────

    [TestMethod]
    public void Build_PropagaFormatosYRolesDeExplosion()
    {
        var columnas = new[]
        {
            Columna("Clave", "Clave", orden: 0),
            Columna("Cantidad", "Cantidad", orden: 1, alineacion: AlineacionColumna.Derecha, formato: "N4"),
            Columna("PrecioUnitario", "P.U.", orden: 2, alineacion: AlineacionColumna.Derecha, formato: "C4"),
            Columna("Importe", "Importe", orden: 3, alineacion: AlineacionColumna.Derecha, formato: "C2"),
            Columna("Porcentaje", "%", orden: 4, alineacion: AlineacionColumna.Derecha, formato: "P2")
        };

        var snapshot = new ExplosionReportSnapshotBuilder().Build(1, "E", columnas);

        var cantidad = snapshot.Columnas.Single(c => c.Identificador == "Cantidad");
        Assert.IsTrue(cantidad.EsNumerica);
        Assert.IsFalse(cantidad.EsMoneda, "La Cantidad (N4) NO es monetaria.");
        Assert.AreEqual("N4", cantidad.FormatoNumerico);

        var pu = snapshot.Columnas.Single(c => c.Identificador == "PrecioUnitario");
        Assert.IsTrue(pu.EsMoneda, "El Precio Unitario es monetario por nombre canónico.");
        Assert.AreEqual("C4", pu.FormatoNumerico);

        var importe = snapshot.Columnas.Single(c => c.Identificador == "ImporteTotal");
        Assert.IsTrue(importe.EsMoneda, "El Importe es monetario por nombre canónico.");
        Assert.AreEqual("C2", importe.FormatoNumerico);

        var porcentaje = snapshot.Columnas.Single(c => c.Identificador == "Porcentaje");
        Assert.IsTrue(porcentaje.EsNumerica);
        Assert.IsFalse(porcentaje.EsMoneda, "El Porcentaje (P2) NO es monetario.");
        Assert.AreEqual("P2", porcentaje.FormatoNumerico);

        var clave = snapshot.Columnas.Single(c => c.Identificador == "Clave");
        Assert.IsFalse(clave.EsNumerica);
        Assert.IsFalse(clave.EsMoneda);
        Assert.AreEqual(string.Empty, clave.FormatoNumerico, "Una columna de texto sin formato no debe recibir N2.");
    }

    [TestMethod]
    public void Build_NormalizaAliasImporteAImporteTotal()
    {
        var snapshot = new ExplosionReportSnapshotBuilder()
            .Build(1, "E", new[] { Columna("Importe", "Importe", alineacion: AlineacionColumna.Derecha, formato: "C2") });

        Assert.AreEqual("ImporteTotal", snapshot.Columnas.Single().Identificador);
    }

    [TestMethod]
    public void Build_FormatoVacioPeroAlineacionDerecha_InfiereNumerica()
    {
        var columna = Columna("Cantidad", "Cantidad", alineacion: AlineacionColumna.Derecha, formato: "");

        var snapshot = new ExplosionReportSnapshotBuilder().Build(1, "E", new[] { columna });

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
            Columna("Importe", "Importe", orden: 1),
            Columna("Clave", "Clave", orden: 0)
        };

        var snapshot = new ExplosionReportSnapshotBuilder().Build(1, "E", columnas);

        CollectionAssert.AreEqual(
            new[] { "Clave", "ImporteTotal" },
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

        var snapshot = new ExplosionReportSnapshotBuilder().Build(1, "E", columnas);

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

        var snapshot = new ExplosionReportSnapshotBuilder().Build(1, "E", new[] { columna });

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

        var snapshot = new ExplosionReportSnapshotBuilder().Build(1, "E", columnas);

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
            new ExplosionReportSnapshotBuilder().Build(1, "E", columnas: null!));
    }

    [TestMethod]
    public void Build_IgnoraElementosNulos()
    {
        var columnas = new ColumnaExplosion[] { null!, Columna("Clave", "Clave") };

        var snapshot = new ExplosionReportSnapshotBuilder().Build(1, "E", columnas);

        Assert.AreEqual(1, snapshot.Columnas.Count);
        Assert.AreEqual("Clave", snapshot.Columnas.Single().Identificador);
    }

    // ────────────── Decimales del proyecto ──────────────

    [TestMethod]
    public void Build_SinDecimalesExplicitos_UsaDefaultsDelContrato()
    {
        var snapshot = new ExplosionReportSnapshotBuilder()
            .Build(1, "E", Array.Empty<ColumnaExplosion>());

        Assert.AreEqual(2, snapshot.DecimalesCantidad);
        Assert.AreEqual(2, snapshot.DecimalesImporte);
        Assert.AreEqual(4, snapshot.DecimalesPorcentaje);
    }

    [TestMethod]
    public void Build_PropagaDecimalesDelProyecto()
    {
        var snapshot = new ExplosionReportSnapshotBuilder()
            .Build(
                1,
                "E",
                new[] { Columna("Clave", "Clave") },
                decimalesCantidad: 4,
                decimalesImporte: 2,
                decimalesPorcentaje: 3);

        Assert.AreEqual(4, snapshot.DecimalesCantidad);
        Assert.AreEqual(2, snapshot.DecimalesImporte);
        Assert.AreEqual(3, snapshot.DecimalesPorcentaje);
    }

    // ══════════════════════ Resolver de exportación ══════════════════════

    [TestMethod]
    public void BuildSnapshot_SinColumnas_UsaDefaultsEnMemoria()
    {
        var snapshot = ExplosionExportResolver.BuildSnapshot(1, "E", null);

        CollectionAssert.AreEqual(
            new[] { "Clave", "Descripcion", "Unidad", "Cantidad", "PrecioUnitario", "ImporteTotal", "Porcentaje" },
            snapshot.Columnas.Select(c => c.Identificador).ToArray());

        Assert.AreEqual(7, ExplosionExportResolver.DefaultColumns().Count);
    }

    [TestMethod]
    public void BuildSnapshot_ColumnasVacias_UsaDefaultsEnMemoria()
    {
        var snapshot = ExplosionExportResolver.BuildSnapshot(1, "E", Array.Empty<ColumnaExplosion>());

        Assert.AreEqual(7, snapshot.Columnas.Count);
    }

    [TestMethod]
    public void BuildSnapshot_UsaColumnasPersistidasCuandoExisten()
    {
        var columnas = new[]
        {
            Columna("Clave", "Clave", orden: 0),
            Columna("Importe", "Importe", orden: 1, alineacion: AlineacionColumna.Derecha, formato: "C2")
        };

        var snapshot = ExplosionExportResolver.BuildSnapshot(1, "E", columnas);

        CollectionAssert.AreEqual(
            new[] { "Clave", "ImporteTotal" },
            snapshot.Columnas.Select(c => c.Identificador).ToArray());
    }

    [TestMethod]
    public void BuildSnapshot_PropagaDecimalesDelProyecto()
    {
        var snapshot = ExplosionExportResolver.BuildSnapshot(
            1, "E", null, decimalesCantidad: 5, decimalesImporte: 1, decimalesPorcentaje: 6);

        Assert.AreEqual(5, snapshot.DecimalesCantidad);
        Assert.AreEqual(1, snapshot.DecimalesImporte);
        Assert.AreEqual(6, snapshot.DecimalesPorcentaje);
    }

    [TestMethod]
    public void ResolveValue_DevuelveTextoDeColumnasDeTexto()
    {
        var row = Fila();
        var snapshot = ExplosionExportResolver.BuildSnapshot(1, "E", null);

        Assert.AreEqual("MAT-01", ExplosionExportResolver.ResolveValue(row, ColDe(snapshot, "Clave")));
        Assert.AreEqual("Cemento", ExplosionExportResolver.ResolveValue(row, ColDe(snapshot, "Descripcion")));
        Assert.AreEqual("ton", ExplosionExportResolver.ResolveValue(row, ColDe(snapshot, "Unidad")));
    }

    [TestMethod]
    public void ResolveValue_InsumoPorcentual_MuestraGuionEnCantidadYPrecio()
    {
        var snapshot = ExplosionExportResolver.BuildSnapshot(1, "E", null);
        var porcentual = new ExplosionReportRow("MO", "%MO", "jor", 0m, 0m, 100m, 0.1m, EsPorcentual: true);

        Assert.AreEqual(ExplosionExportResolver.GuionInsumo,
            ExplosionExportResolver.ResolveValue(porcentual, ColDe(snapshot, "Cantidad")));
        Assert.AreEqual(ExplosionExportResolver.GuionInsumo,
            ExplosionExportResolver.ResolveValue(porcentual, ColDe(snapshot, "PrecioUnitario")));
    }

    [TestMethod]
    public void TryResolveNumber_ResuelveCantidadPrecioImporteYPorcentaje()
    {
        var row = Fila();
        var snapshot = ExplosionExportResolver.BuildSnapshot(1, "E", null);

        Assert.IsTrue(ExplosionExportResolver.TryResolveNumber(row, ColDe(snapshot, "Cantidad"), out var cantidad));
        Assert.AreEqual(10.5m, cantidad);

        Assert.IsTrue(ExplosionExportResolver.TryResolveNumber(row, ColDe(snapshot, "PrecioUnitario"), out var pu));
        Assert.AreEqual(20.25m, pu);

        Assert.IsTrue(ExplosionExportResolver.TryResolveNumber(row, ColDe(snapshot, "ImporteTotal"), out var importe));
        Assert.AreEqual(212.625m, importe);

        Assert.IsTrue(ExplosionExportResolver.TryResolveNumber(row, ColDe(snapshot, "Porcentaje"), out var pct));
        Assert.AreEqual(0.5m, pct);
    }

    [TestMethod]
    public void TryResolveNumber_InsumoPorcentual_DevuelveFalseParaCantidadYPrecio()
    {
        var snapshot = ExplosionExportResolver.BuildSnapshot(1, "E", null);
        var porcentual = new ExplosionReportRow("MO", "%MO", "jor", 0m, 0m, 100m, 0.1m, EsPorcentual: true);

        Assert.IsFalse(ExplosionExportResolver.TryResolveNumber(porcentual, ColDe(snapshot, "Cantidad"), out _));
        Assert.IsFalse(ExplosionExportResolver.TryResolveNumber(porcentual, ColDe(snapshot, "PrecioUnitario"), out _));

        // El importe y el porcentaje sí son numéricos aunque el insumo sea %MO.
        Assert.IsTrue(ExplosionExportResolver.TryResolveNumber(porcentual, ColDe(snapshot, "ImporteTotal"), out var importe));
        Assert.AreEqual(100m, importe);
        Assert.IsTrue(ExplosionExportResolver.TryResolveNumber(porcentual, ColDe(snapshot, "Porcentaje"), out var pct));
        Assert.AreEqual(0.1m, pct);
    }

    [TestMethod]
    public void TryResolveNumber_ColumnasDeTextoYFueraDeCatalogo_DevuelveFalse()
    {
        var row = Fila();
        var snapshot = ExplosionExportResolver.BuildSnapshot(1, "E", null);
        var rendimiento = ReportColumnDefinitionMapper.MapearExplosion(
            Columna("Rendimiento", "Rendimiento", alineacion: AlineacionColumna.Derecha, formato: "N2"));

        Assert.IsFalse(ExplosionExportResolver.TryResolveNumber(row, ColDe(snapshot, "Clave"), out _));
        Assert.IsFalse(ExplosionExportResolver.TryResolveNumber(row, rendimiento, out _));
        Assert.AreEqual(string.Empty, ExplosionExportResolver.ResolveValue(row, rendimiento));
    }

    [TestMethod]
    public void ResolveCellBackground_UsaBandeoYRespetaOverrideDeColumna()
    {
        var tabla = ReportTableStyle.LegacyCatalogo();
        var defaultCol = ReportColumnDefinitionMapper.MapearExplosion(Columna("Clave", "Clave"));
        var overrideCol = ReportColumnDefinitionMapper.MapearExplosion(Columna("Clave", "Clave", colorFondo: "#00FF00"));

        Assert.AreEqual("#F5F5F5", ExplosionExportResolver.ResolveCellBackground(defaultCol, tabla, esFilaAlterna: true));
        Assert.AreEqual("#FFFFFF", ExplosionExportResolver.ResolveCellBackground(defaultCol, tabla, esFilaAlterna: false));
        Assert.AreEqual("#00FF00", ExplosionExportResolver.ResolveCellBackground(overrideCol, tabla, esFilaAlterna: true));
    }

    // ────────────── Auxiliares ──────────────

    private static ExplosionReportRow Fila()
        => new("MAT-01", "Cemento", "ton", 10.5m, 20.25m, 212.625m, 0.5m, EsPorcentual: false);

    private static ReportColumnDefinition ColDe(ReportColumnSnapshot snapshot, string identificador)
        => snapshot.Columnas.Single(c => c.Identificador == identificador);
}
