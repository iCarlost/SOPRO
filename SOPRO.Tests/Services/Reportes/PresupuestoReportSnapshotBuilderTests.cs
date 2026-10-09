using Microsoft.VisualStudio.TestTools.UnitTesting;
using SOPRO.Application.Models.Reporting.ReportColumns;
using SOPRO.Application.UseCases.Reporting;
using SOPRO.Core.Entities;

namespace SOPRO.Tests.Services.Reportes;

/// <summary>
/// Verificación del <see cref="PresupuestoReportSnapshotBuilder"/>: snapshot neutral
/// de columnas del reporte de Presupuesto construido desde configuración persistida
/// pura (<c>ColumnaPersonalizada</c> primaria + overlay de encabezado
/// <c>ConfigColumnaReporte</c>).
///
/// Complementa a <c>ReportColumnDefinitionMapperTests</c> (que cubre la precedencia
/// a nivel de mapper) validando el contrato del snapshot: metadatos, orden estable,
/// exclusión de columnas internas, alias Importe→ImporteTotal, <c>EstiloTabla</c> y
/// huecos del mapper (alineación vertical 0/1/2, wrap, colores nulos) ejercitados a
/// través del builder real.
/// </summary>
[TestClass]
public class PresupuestoReportSnapshotBuilderTests
{
    // ─────────────────────────── Helpers de datos ───────────────────────────

    private static ColumnaPersonalizada Columna(
        string nombreInterno,
        string? nombre = null,
        int orden = 0,
        bool visible = true,
        int ancho = 100,
        TipoDatoColumna tipoDato = TipoDatoColumna.Texto,
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
            TipoDato = tipoDato,
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

    private static ConfigColumnaReporte Overlay(
        string nombreInterno,
        string? fuente = null,
        float tamano = 0f,
        bool negrita = true,
        bool cursiva = false,
        string? colorFondo = null,
        string? colorTexto = null,
        string? encabezado = null)
        => new()
        {
            NombreInterno = nombreInterno,
            Encabezado = encabezado!,
            EncFuente = fuente!,
            EncTamaño = tamano,
            EncNegrita = negrita,
            EncCursiva = cursiva,
            EncColorFondo = colorFondo!,
            EncColorTexto = colorTexto!
        };

    // ───────────────────────── Metadatos del snapshot ─────────────────────────

    [TestMethod]
    public void Build_ProduceSnapshotConTipoProyectoYTitulo()
    {
        var snapshot = new PresupuestoReportSnapshotBuilder()
            .Build(42, "Presupuesto 2026", Array.Empty<ColumnaPersonalizada>());

        Assert.AreEqual(PresupuestoReportSnapshotBuilder.TipoReporte, snapshot.TipoReporte);
        Assert.AreEqual("Presupuesto", snapshot.TipoReporte);
        Assert.AreEqual(42, snapshot.ProyectoId);
        Assert.AreEqual("Presupuesto 2026", snapshot.Titulo);
        Assert.AreEqual(0, snapshot.Columnas.Count);
    }

    [TestMethod]
    public void Build_TituloNulo_SeNormalizaAVacio()
    {
        var snapshot = new PresupuestoReportSnapshotBuilder()
            .Build(1, null, Array.Empty<ColumnaPersonalizada>());

        Assert.AreEqual(string.Empty, snapshot.Titulo);
    }

    // ───────────────── Visibilidad, orden, ancho y encabezado ─────────────────

    [TestMethod]
    public void Build_PropagaVisibilidadOrdenAnchoYEncabezadoTalCual()
    {
        var columnas = new[]
        {
            Columna("Descripcion", "Descripción de la partida", orden: 2, visible: false, ancho: 250),
            Columna("Unidad", "Unidad", orden: 1, ancho: 60),
            Columna("Clave", "Clave", orden: 0, ancho: 80)
        };

        var snapshot = new PresupuestoReportSnapshotBuilder().Build(1, "P", columnas);

        // Orden ascendente por Orden, aunque la entrada esté barajada.
        CollectionAssert.AreEqual(
            new[] { "Clave", "Unidad", "Descripcion" },
            snapshot.Columnas.Select(c => c.Identificador).ToArray());

        var clave = snapshot.Columnas[0];
        Assert.AreEqual("Clave", clave.Encabezado);
        Assert.IsTrue(clave.Visible);
        Assert.AreEqual(0, clave.Orden);
        Assert.AreEqual(80, clave.Ancho);

        var descripcion = snapshot.Columnas[2];
        Assert.AreEqual("Descripción de la partida", descripcion.Encabezado);
        Assert.IsFalse(descripcion.Visible, "La visibilidad false de la entidad debe propagarse.");
        Assert.AreEqual(2, descripcion.Orden);
        Assert.AreEqual(250, descripcion.Ancho);
    }

    // ────────────────── Precedencia/blending del overlay ──────────────────

    [TestMethod]
    public void Build_OverlayAplicaSoloEstiloEncabezadoCuandoIdentificadorCoincide()
    {
        var columna = Columna(
            "PrecioUnitario",
            "Precio Unitario",
            orden: 0,
            fuente: "Consolas",
            tamano: 12,
            colorFuente: "#010203",
            colorFondo: "#040506",
            negrita: true,
            cursiva: true);

        // El identificador coincide ignorando mayúsculas; el Encabezado del overlay
        // NO debe usarse (la entidad es primaria).
        var overlay = Overlay(
            "preciounitario",
            fuente: "Arial",
            tamano: 8f,
            negrita: false,
            cursiva: true,
            colorFondo: "#101010",
            colorTexto: "#F0F0F0",
            encabezado: "ENCABEZADO IGNORADO");

        var snapshot = new PresupuestoReportSnapshotBuilder()
            .Build(1, "P", new[] { columna }, new[] { overlay });

        var def = snapshot.Columnas.Single();

        // Entidad primaria.
        Assert.AreEqual("PrecioUnitario", def.Identificador);
        Assert.AreEqual("Precio Unitario", def.Encabezado, "El encabezado lo aporta la entidad, no el overlay.");
        Assert.AreEqual("Consolas", def.EstiloContenido.Fuente);
        Assert.AreEqual(12f, def.EstiloContenido.Tamano);
        Assert.AreEqual("#010203", def.EstiloContenido.ColorFuente);
        Assert.AreEqual("#040506", def.EstiloContenido.ColorFondo);
        Assert.IsTrue(def.EstiloContenido.Negrita);
        Assert.IsTrue(def.EstiloContenido.Cursiva);

        // Overlay: sólo el estilo del encabezado.
        Assert.AreEqual("Arial", def.EstiloEncabezado.Fuente);
        Assert.AreEqual(8f, def.EstiloEncabezado.Tamano);
        Assert.IsFalse(def.EstiloEncabezado.Negrita);
        Assert.IsTrue(def.EstiloEncabezado.Cursiva);
        Assert.AreEqual("#F0F0F0", def.EstiloEncabezado.ColorFuente);
        Assert.AreEqual("#101010", def.EstiloEncabezado.ColorFondo);
    }

    [TestMethod]
    public void Build_ColumnasSinOverlayQuedanConEncabezadoPorDefecto()
    {
        var columnas = new[]
        {
            Columna("PrecioUnitario", "Precio Unitario", orden: 0),
            Columna("Unidad", "Unidad", orden: 1)
        };

        var overlay = Overlay("PrecioUnitario", fuente: "Arial", colorFondo: "#111111", colorTexto: "#222222");

        var snapshot = new PresupuestoReportSnapshotBuilder()
            .Build(1, "P", columnas, new[] { overlay });

        var conOverlay = snapshot.Columnas.Single(c => c.Identificador == "PrecioUnitario");
        var sinOverlay = snapshot.Columnas.Single(c => c.Identificador == "Unidad");

        var encabezadoDefault = ReportTableStyle.LegacyPresupuesto().EstiloEncabezado;

        Assert.AreEqual("Arial", conOverlay.EstiloEncabezado.Fuente);
        Assert.AreEqual(
            encabezadoDefault,
            sinOverlay.EstiloEncabezado,
            "Una columna sin overlay debe conservar el encabezado por defecto LegacyPresupuesto.");
    }

    [TestMethod]
    public void Build_OverlayConIdentificadorDistinto_NoAplicaEstilo()
    {
        var columna = Columna("Clave", "Clave");
        var overlay = Overlay("OtraColumna", fuente: "Arial", colorFondo: "#111111", colorTexto: "#222222");

        var snapshot = new PresupuestoReportSnapshotBuilder()
            .Build(1, "P", new[] { columna }, new[] { overlay });

        var def = snapshot.Columnas.Single();
        Assert.AreEqual(ReportTableStyle.LegacyPresupuesto().EstiloEncabezado, def.EstiloEncabezado);
    }

    [TestMethod]
    public void Build_OverlayConColoresFuenteNulos_UsaDefaultLegacyEncabezado()
    {
        // Hueco del mapper: colores de encabezado nulos -> fallback al default.
        var columna = Columna("Clave", "Clave");
        var overlay = Overlay("Clave", fuente: null, tamano: 0f, negrita: true, cursiva: false, colorFondo: null, colorTexto: null);

        var snapshot = new PresupuestoReportSnapshotBuilder()
            .Build(1, "P", new[] { columna }, new[] { overlay });

        var def = snapshot.Columnas.Single();
        var esperado = ReportTableStyle.LegacyPresupuesto().EstiloEncabezado;

        Assert.AreEqual(esperado, def.EstiloEncabezado);
        Assert.AreEqual("#FFFFFF", def.EstiloEncabezado.ColorFuente);
        Assert.AreEqual("#1565C0", def.EstiloEncabezado.ColorFondo);
    }

    [TestMethod]
    public void Build_ColoresContenidoNulos_UsanDefaultLegacyContenido()
    {
        // Hueco del mapper: fuente/tamaño/colores de contenido nulos -> fallback.
        var columna = Columna(
            "Clave",
            "Clave",
            fuente: null,
            tamano: 0,
            colorFuente: null,
            colorFondo: null);

        var snapshot = new PresupuestoReportSnapshotBuilder().Build(1, "P", new[] { columna });

        Assert.AreEqual(ReportTableStyle.LegacyPresupuesto().EstiloContenido, snapshot.Columnas.Single().EstiloContenido);
    }

    // ───────────────────────── Estilo de tabla ─────────────────────────

    [TestMethod]
    public void Build_EstiloTabla_EsLegacyPresupuesto()
    {
        var snapshot = new PresupuestoReportSnapshotBuilder()
            .Build(1, "P", new[] { Columna("Clave", "Clave") });

        Assert.AreEqual(ReportTableStyle.LegacyPresupuesto(), snapshot.EstiloTabla);
    }

    // ────────────── Exclusión de internas y alias Importe ──────────────

    [TestMethod]
    public void Build_ExcluyeColumnasInternasYNormalizaAliasImporteTotal()
    {
        var columnas = new[]
        {
            Columna("Tipo", "Tipo", orden: -1),
            Columna("colRelleno", "", orden: 99),
            Columna("Importe", "Importe", orden: 1, tipoDato: TipoDatoColumna.Moneda),
            Columna("Clave", "Clave", orden: 0)
        };

        var snapshot = new PresupuestoReportSnapshotBuilder().Build(1, "P", columnas);

        CollectionAssert.AreEqual(
            new[] { "Clave", "ImporteTotal" },
            snapshot.Columnas.Select(c => c.Identificador).ToArray());
        Assert.IsFalse(snapshot.Columnas.Any(c => ReportColumnDefinitionMapper.EsColumnaInterna(c.Identificador)));
    }

    // ────────────── Wrap, alineaciones y formato numérico ──────────────

    [TestMethod]
    public void Build_PropagaWrapAlineacionesYFormatoNumerico()
    {
        var columnas = new[]
        {
            Columna("ImporteTotal", "Importe", orden: 0, tipoDato: TipoDatoColumna.Moneda,
                alineacion: AlineacionColumna.Izquierda, wrap: true, formato: "C2"),
            Columna("Unidad", "Unidad", orden: 1, alineacion: AlineacionColumna.Justificado),
            Columna("Descripcion", "Descripción", orden: 2, alineacion: AlineacionColumna.Centro)
        };

        var snapshot = new PresupuestoReportSnapshotBuilder().Build(1, "P", columnas);

        var importe = snapshot.Columnas.Single(c => c.Identificador == "ImporteTotal");
        Assert.IsTrue(importe.EsNumerica);
        Assert.AreEqual(ReportTextAlignment.Derecha, importe.Alineacion, "Numérica con alineación por defecto cae a Derecha.");
        Assert.IsTrue(importe.Wrap);
        Assert.AreEqual("C2", importe.FormatoNumerico);

        var unidad = snapshot.Columnas.Single(c => c.Identificador == "Unidad");
        Assert.IsFalse(unidad.EsNumerica);
        Assert.AreEqual(ReportTextAlignment.Justificado, unidad.Alineacion, "Justificado se conserva.");
        Assert.AreEqual(string.Empty, unidad.FormatoNumerico);

        var descripcion = snapshot.Columnas.Single(c => c.Identificador == "Descripcion");
        Assert.AreEqual(ReportTextAlignment.Centro, descripcion.Alineacion, "Centro se conserva.");
    }

    [TestMethod]
    public void Build_FormatoNumericoVacioEnNumerica_SeNormalizaAN2()
    {
        var columna = Columna("Cantidad", "Cantidad", tipoDato: TipoDatoColumna.Numerico, formato: "");

        var snapshot = new PresupuestoReportSnapshotBuilder().Build(1, "P", new[] { columna });

        Assert.AreEqual("N2", snapshot.Columnas.Single().FormatoNumerico);
    }

    [TestMethod]
    public void Build_AlineacionVertical_Mapea0_1_2_YFueraDeRango()
    {
        // Hueco del mapper: 0=Superior, 1=Medio, 2=Inferior, resto=Medio.
        var columnas = new[]
        {
            Columna("A", "A", orden: 0, alineacionVertical: 0),
            Columna("B", "B", orden: 1, alineacionVertical: 1),
            Columna("C", "C", orden: 2, alineacionVertical: 2),
            Columna("D", "D", orden: 3, alineacionVertical: 9)
        };

        var snapshot = new PresupuestoReportSnapshotBuilder().Build(1, "P", columnas);

        Assert.AreEqual(ReportVerticalAlignment.Superior, snapshot.Columnas[0].AlineacionVertical);
        Assert.AreEqual(ReportVerticalAlignment.Medio, snapshot.Columnas[1].AlineacionVertical);
        Assert.AreEqual(ReportVerticalAlignment.Inferior, snapshot.Columnas[2].AlineacionVertical);
        Assert.AreEqual(ReportVerticalAlignment.Medio, snapshot.Columnas[3].AlineacionVertical);
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

        var snapshot = new PresupuestoReportSnapshotBuilder().Build(1, "P", columnas);

        Assert.IsInstanceOfType(snapshot.Columnas, typeof(IReadOnlyList<ReportColumnDefinition>));

        // Enumerar dos veces no cambia el orden ni expone huecos.
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
            new PresupuestoReportSnapshotBuilder().Build(1, "P", columnas: null!));
    }

    [TestMethod]
    public void Build_IgnoraElementosNulos()
    {
        var columnas = new ColumnaPersonalizada[] { null!, Columna("Clave", "Clave") };

        var snapshot = new PresupuestoReportSnapshotBuilder().Build(1, "P", columnas);

        Assert.AreEqual(1, snapshot.Columnas.Count);
        Assert.AreEqual("Clave", snapshot.Columnas.Single().Identificador);
    }
}
