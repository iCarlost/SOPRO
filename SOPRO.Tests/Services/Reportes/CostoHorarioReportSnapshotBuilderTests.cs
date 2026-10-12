using Microsoft.VisualStudio.TestTools.UnitTesting;
using SOPRO.Application.Models.Reporting.ReportColumns;
using SOPRO.Application.UseCases.Reporting;
using SOPRO.Core.Entities;

namespace SOPRO.Tests.Services.Reportes;

/// <summary>
/// Verificación del <see cref="CostoHorarioReportSnapshotBuilder"/>: snapshot
/// neutral de columnas del catálogo de Maquinaria (Costo Horario) construido desde
/// <c>ColumnaMaquinaria</c> como fuente, con defaults neutrales cuando no hay
/// configuración persistida.
///
/// Cubre visibilidad, orden, ancho, encabezado y formatos (incluido el rol
/// monetario de Costo Horario), el estilo neutral
/// <see cref="ReportTableStyle.LegacyCatalogo"/>, la reutilización de una sola
/// instancia de definiciones para los consumidores PDF/Excel, y huecos del mapper
/// (inferencia numérica por alineación derecha, alineación vertical 0/1/2, wrap,
/// colores nulos) ejercitados a través del builder real.
/// </summary>
[TestClass]
public class CostoHorarioReportSnapshotBuilderTests
{
    // ─────────────────────────── Helpers de datos ───────────────────────────

    private static ColumnaMaquinaria Columna(
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
        var snapshot = new CostoHorarioReportSnapshotBuilder()
            .Build(5, "Catálogo de Maquinaria", Array.Empty<ColumnaMaquinaria>());

        Assert.AreEqual(CostoHorarioReportSnapshotBuilder.TipoReporte, snapshot.TipoReporte);
        Assert.AreEqual("Maquinaria", snapshot.TipoReporte);
        Assert.AreEqual(5, snapshot.ProyectoId);
        Assert.AreEqual("Catálogo de Maquinaria", snapshot.Titulo);
        Assert.AreEqual(7, snapshot.Columnas.Count, "Sin configuración se usan los defaults del catálogo.");
    }

    [TestMethod]
    public void Build_TituloNulo_SeNormalizaAVacio()
    {
        var snapshot = new CostoHorarioReportSnapshotBuilder()
            .Build(1, null, Array.Empty<ColumnaMaquinaria>());

        Assert.AreEqual(string.Empty, snapshot.Titulo);
    }

    [TestMethod]
    public void Build_PropagaLosDecimalesGlobalesDelProyecto()
    {
        var snapshot = new CostoHorarioReportSnapshotBuilder()
            .Build(1, "M", new[] { Columna("Clave", "Clave") },
                decimalesCantidad: 5, decimalesImporte: 3, decimalesPorcentaje: 4);

        Assert.AreEqual(5, snapshot.DecimalesCantidad, "La cantidad histórica de cinco decimales se propaga.");
        Assert.AreEqual(3, snapshot.DecimalesImporte);
        Assert.AreEqual(4, snapshot.DecimalesPorcentaje);
    }

    // ───────────────────────── Defaults neutrales ─────────────────────────

    [TestMethod]
    public void Build_SinColumnas_UsaDefaultsDelCatalogo()
    {
        var snapshot = new CostoHorarioReportSnapshotBuilder()
            .Build(1, "M", columnas: null);

        CollectionAssert.AreEqual(
            new[] { "Clave", "Descripcion", "PotenciaNominal", "Combustible", "CostoHorario", "TipoCosto", "Origen" },
            snapshot.Columnas.Select(c => c.Identificador).ToArray());
    }

    [TestMethod]
    public void DefaultColumns_ExponeLasSieteColumnasDelCatalogo()
    {
        var cols = CostoHorarioReportSnapshotBuilder.DefaultColumns();

        CollectionAssert.AreEqual(
            new[] { "Clave", "Descripcion", "PotenciaNominal", "Combustible", "CostoHorario", "TipoCosto", "Origen" },
            cols.Select(c => c.Identificador).ToArray());

        var costo = cols.Single(c => c.Identificador == "CostoHorario");
        Assert.IsTrue(costo.EsMoneda, "Costo Horario es rol monetario canónico.");
        Assert.IsTrue(costo.EsNumerica);

        var potencia = cols.Single(c => c.Identificador == "PotenciaNominal");
        Assert.IsTrue(potencia.EsNumerica);
        Assert.IsFalse(potencia.EsMoneda, "La potencia no es monetaria.");
        Assert.AreEqual("N2", potencia.FormatoNumerico);

        Assert.IsFalse(cols.Single(c => c.Identificador == "Clave").EsNumerica);
    }

    [TestMethod]
    public void Build_IgnoraElementosNulos()
    {
        var columnas = new ColumnaMaquinaria[] { null!, Columna("Clave", "Clave") };

        var snapshot = new CostoHorarioReportSnapshotBuilder().Build(1, "M", columnas);

        Assert.AreEqual(1, snapshot.Columnas.Count);
        Assert.AreEqual("Clave", snapshot.Columnas.Single().Identificador);
    }

    // ───────────────── Visibilidad, orden, ancho y encabezado ─────────────────

    [TestMethod]
    public void Build_PropagaVisibilidadOrdenAnchoYEncabezadoTalCual()
    {
        var columnas = new[]
        {
            Columna("Descripcion", "Descripción del equipo", orden: 2, visible: false, ancho: 320),
            Columna("PotenciaNominal", "Potencia (HP)", orden: 1, ancho: 100,
                alineacion: AlineacionColumna.Derecha, formato: "N2"),
            Columna("Clave", "Clave", orden: 0, ancho: 110)
        };

        var snapshot = new CostoHorarioReportSnapshotBuilder().Build(1, "M", columnas);

        CollectionAssert.AreEqual(
            new[] { "Clave", "PotenciaNominal", "Descripcion" },
            snapshot.Columnas.Select(c => c.Identificador).ToArray());

        var clave = snapshot.Columnas[0];
        Assert.AreEqual("Clave", clave.Encabezado);
        Assert.IsTrue(clave.Visible);
        Assert.AreEqual(0, clave.Orden);
        Assert.AreEqual(110, clave.Ancho);

        var descripcion = snapshot.Columnas[2];
        Assert.AreEqual("Descripción del equipo", descripcion.Encabezado);
        Assert.IsFalse(descripcion.Visible);
        Assert.AreEqual(2, descripcion.Orden);
        Assert.AreEqual(320, descripcion.Ancho);
    }

    // ───────────────────────── Estilo de tabla ─────────────────────────

    [TestMethod]
    public void Build_EstiloTabla_EsLegacyCatalogo()
    {
        var snapshot = new CostoHorarioReportSnapshotBuilder()
            .Build(1, "M", new[] { Columna("Clave", "Clave") });

        Assert.AreEqual(ReportTableStyle.LegacyCatalogo(), snapshot.EstiloTabla);
    }

    [TestMethod]
    public void Build_EncabezadoEsNeutralAunqueLaEntidadTengaEstiloContenidoPropio()
    {
        var columna = Columna(
            "CostoHorario",
            "Costo Horario",
            alineacion: AlineacionColumna.Derecha,
            fuente: "Consolas",
            tamano: 12,
            colorFuente: "#010203",
            colorFondo: "#040506",
            negrita: true);

        var snapshot = new CostoHorarioReportSnapshotBuilder().Build(1, "M", new[] { columna });

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
            Columna("CostoHorario", "Costo Horario", orden: 1, alineacion: AlineacionColumna.Derecha, formato: "#,##0.00")
        };

        var snapshot = new CostoHorarioReportSnapshotBuilder().Build(1, "M", columnas);

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
    public void Build_CostoHorarioEsMonetarioYPotenciaEsCantidad()
    {
        var columnas = new[]
        {
            Columna("CostoHorario", "Costo Horario", orden: 0,
                alineacion: AlineacionColumna.Derecha, formato: "#,##0.00"),
            Columna("PotenciaNominal", "Potencia (HP)", orden: 1,
                alineacion: AlineacionColumna.Derecha, formato: "N2")
        };

        var snapshot = new CostoHorarioReportSnapshotBuilder().Build(1, "M", columnas);

        var costo = snapshot.Columnas.Single(c => c.Identificador == "CostoHorario");
        Assert.IsTrue(costo.EsNumerica);
        Assert.IsTrue(costo.EsMoneda, "Costo Horario es rol monetario canónico.");
        Assert.AreEqual(ReportTextAlignment.Derecha, costo.Alineacion);

        var potencia = snapshot.Columnas.Single(c => c.Identificador == "PotenciaNominal");
        Assert.IsTrue(potencia.EsNumerica);
        Assert.IsFalse(potencia.EsMoneda);
        Assert.AreEqual("N2", potencia.FormatoNumerico);
    }

    [TestMethod]
    public void Build_FormatoVacioPeroAlineacionDerecha_InfiereNumerica()
    {
        var columna = Columna("PotenciaNominal", "Potencia (HP)", alineacion: AlineacionColumna.Derecha, formato: "");

        var snapshot = new CostoHorarioReportSnapshotBuilder().Build(1, "M", new[] { columna });

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
            Columna("CostoHorario", "Costo Horario", orden: 1),
            Columna("Clave", "Clave", orden: 0)
        };

        var snapshot = new CostoHorarioReportSnapshotBuilder().Build(1, "M", columnas);

        CollectionAssert.AreEqual(
            new[] { "Clave", "CostoHorario" },
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

        var snapshot = new CostoHorarioReportSnapshotBuilder().Build(1, "M", columnas);

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

        var snapshot = new CostoHorarioReportSnapshotBuilder().Build(1, "M", new[] { columna });

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

        var snapshot = new CostoHorarioReportSnapshotBuilder().Build(1, "M", columnas);

        Assert.IsInstanceOfType(snapshot.Columnas, typeof(IReadOnlyList<ReportColumnDefinition>));

        var primera = snapshot.Columnas.Select(c => c.Identificador).ToArray();
        var segunda = snapshot.Columnas.Select(c => c.Identificador).ToArray();
        CollectionAssert.AreEqual(new[] { "A", "B", "C" }, primera);
        CollectionAssert.AreEqual(primera, segunda);
        Assert.IsTrue(snapshot.Columnas.All(c => c is not null));
    }
}
