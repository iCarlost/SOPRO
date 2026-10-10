using Microsoft.VisualStudio.TestTools.UnitTesting;
using SOPRO.Application.Models.Reporting.ReportColumns;
using SOPRO.Application.Services;
using SOPRO.Application.UseCases.Reporting;
using SOPRO.Core.Entities;

namespace SOPRO.Tests.Services.Reportes;

/// <summary>
/// Verificación del <see cref="ManoObraReportSnapshotBuilder"/> y de
/// <see cref="ManoObraCatalogExportResolver"/>: snapshot neutral de columnas del
/// Catálogo de Mano de Obra construido desde <c>ColumnaManoObra</c> como única
/// fuente, más la materialización neutral de valores (texto y numérico) sin tocar
/// grids.
///
/// Cubre visibilidad, orden, ancho, encabezado y formatos (monetarios C2 y factor
/// N4), el estilo neutral <see cref="ReportTableStyle.LegacyCatalogo"/>, la
/// reutilización de una sola instancia para PDF/Excel, el rol monetario derivado y
/// huecos del mapper (inferencia numérica por alineación derecha, alineación
/// vertical 0/1/2, wrap, colores nulos) ejercitados a través del builder real.
/// </summary>
[TestClass]
public class ManoObraReportSnapshotBuilderTests
{
    // ─────────────────────────── Helpers de datos ───────────────────────────

    private static ColumnaManoObra Columna(
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
        var snapshot = new ManoObraReportSnapshotBuilder()
            .Build(5, "Catálogo de Mano de Obra", Array.Empty<ColumnaManoObra>());

        Assert.AreEqual(ManoObraReportSnapshotBuilder.TipoReporte, snapshot.TipoReporte);
        Assert.AreEqual("ManoDeObra", snapshot.TipoReporte);
        Assert.AreEqual(5, snapshot.ProyectoId);
        Assert.AreEqual("Catálogo de Mano de Obra", snapshot.Titulo);
        Assert.AreEqual(0, snapshot.Columnas.Count);
    }

    [TestMethod]
    public void Build_TituloNulo_SeNormalizaAVacio()
    {
        var snapshot = new ManoObraReportSnapshotBuilder()
            .Build(1, null, Array.Empty<ColumnaManoObra>());

        Assert.AreEqual(string.Empty, snapshot.Titulo);
    }

    // ───────────────── Visibilidad, orden, ancho y encabezado ─────────────────

    [TestMethod]
    public void Build_PropagaVisibilidadOrdenAnchoYEncabezadoTalCual()
    {
        var columnas = new[]
        {
            Columna("Descripcion", "Descripción del trabajador", orden: 2, visible: false, ancho: 260),
            Columna("Unidad", "Unidad", orden: 1, ancho: 60),
            Columna("Clave", "Clave", orden: 0, ancho: 90)
        };

        var snapshot = new ManoObraReportSnapshotBuilder().Build(1, "M", columnas);

        CollectionAssert.AreEqual(
            new[] { "Clave", "Unidad", "Descripcion" },
            snapshot.Columnas.Select(c => c.Identificador).ToArray());

        var clave = snapshot.Columnas[0];
        Assert.AreEqual("Clave", clave.Encabezado);
        Assert.IsTrue(clave.Visible);
        Assert.AreEqual(0, clave.Orden);
        Assert.AreEqual(90, clave.Ancho);

        var descripcion = snapshot.Columnas[2];
        Assert.AreEqual("Descripción del trabajador", descripcion.Encabezado);
        Assert.IsFalse(descripcion.Visible);
        Assert.AreEqual(2, descripcion.Orden);
        Assert.AreEqual(260, descripcion.Ancho);
    }

    // ───────────────────────── Estilo de tabla ─────────────────────────

    [TestMethod]
    public void Build_EstiloTabla_EsLegacyCatalogo()
    {
        var snapshot = new ManoObraReportSnapshotBuilder()
            .Build(1, "M", new[] { Columna("Clave", "Clave") });

        Assert.AreEqual(ReportTableStyle.LegacyCatalogo(), snapshot.EstiloTabla);
    }

    [TestMethod]
    public void Build_EncabezadoEsNeutralAunqueLaEntidadTengaEstiloContenidoPropio()
    {
        var columna = Columna(
            "SalarioBase",
            "Salario Base",
            alineacion: AlineacionColumna.Derecha,
            fuente: "Consolas",
            tamano: 12,
            colorFuente: "#010203",
            colorFondo: "#040506",
            negrita: true);

        var snapshot = new ManoObraReportSnapshotBuilder().Build(1, "M", new[] { columna });

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
            Columna("SalarioBase", "Salario Base", orden: 1, alineacion: AlineacionColumna.Derecha, formato: "C2")
        };

        var snapshot = new ManoObraReportSnapshotBuilder().Build(1, "M", columnas);

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
    public void Build_PropagaFormatosYRolesDeManoDeObra()
    {
        var columnas = new[]
        {
            Columna("Clave", "Clave", orden: 0),
            Columna("SalarioBase", "Salario Base", orden: 1, alineacion: AlineacionColumna.Derecha, formato: "C2"),
            Columna("FactorSalarioReal", "FSR", orden: 2, alineacion: AlineacionColumna.Derecha, formato: "N4"),
            Columna("SalarioReal", "Salario Real", orden: 3, alineacion: AlineacionColumna.Derecha, formato: "C2")
        };

        var snapshot = new ManoObraReportSnapshotBuilder().Build(1, "M", columnas);

        var salarioBase = snapshot.Columnas.Single(c => c.Identificador == "SalarioBase");
        Assert.IsTrue(salarioBase.EsNumerica);
        Assert.IsTrue(salarioBase.EsMoneda, "SalarioBase es monetario por nombre canónico.");
        Assert.AreEqual("C2", salarioBase.FormatoNumerico);

        var fsr = snapshot.Columnas.Single(c => c.Identificador == "FactorSalarioReal");
        Assert.IsTrue(fsr.EsNumerica);
        Assert.IsFalse(fsr.EsMoneda, "El FSR (N4) NO es monetario.");
        Assert.AreEqual("N4", fsr.FormatoNumerico);

        var salarioReal = snapshot.Columnas.Single(c => c.Identificador == "SalarioReal");
        Assert.IsTrue(salarioReal.EsMoneda, "SalarioReal es monetario por nombre canónico.");

        var clave = snapshot.Columnas.Single(c => c.Identificador == "Clave");
        Assert.IsFalse(clave.EsNumerica);
        Assert.IsFalse(clave.EsMoneda);
        Assert.AreEqual(string.Empty, clave.FormatoNumerico, "Una columna de texto sin formato no debe recibir N2.");
    }

    [TestMethod]
    public void Build_FormatoVacioPeroAlineacionDerecha_InfiereNumerica()
    {
        var columna = Columna("SalarioBase", "Salario Base", alineacion: AlineacionColumna.Derecha, formato: "");

        var snapshot = new ManoObraReportSnapshotBuilder().Build(1, "M", new[] { columna });

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
            Columna("SalarioBase", "Salario Base", orden: 1),
            Columna("Clave", "Clave", orden: 0)
        };

        var snapshot = new ManoObraReportSnapshotBuilder().Build(1, "M", columnas);

        CollectionAssert.AreEqual(
            new[] { "Clave", "SalarioBase" },
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

        var snapshot = new ManoObraReportSnapshotBuilder().Build(1, "M", columnas);

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

        var snapshot = new ManoObraReportSnapshotBuilder().Build(1, "M", new[] { columna });

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

        var snapshot = new ManoObraReportSnapshotBuilder().Build(1, "M", columnas);

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
            new ManoObraReportSnapshotBuilder().Build(1, "M", columnas: null!));
    }

    [TestMethod]
    public void Build_IgnoraElementosNulos()
    {
        var columnas = new ColumnaManoObra[] { null!, Columna("Clave", "Clave") };

        var snapshot = new ManoObraReportSnapshotBuilder().Build(1, "M", columnas);

        Assert.AreEqual(1, snapshot.Columnas.Count);
        Assert.AreEqual("Clave", snapshot.Columnas.Single().Identificador);
    }

    // ────────────── Decimales del proyecto ──────────────

    [TestMethod]
    public void Build_SinDecimalesExplicitos_UsaDefaultsDelContrato()
    {
        var snapshot = new ManoObraReportSnapshotBuilder()
            .Build(1, "M", Array.Empty<ColumnaManoObra>());

        Assert.AreEqual(2, snapshot.DecimalesCantidad);
        Assert.AreEqual(2, snapshot.DecimalesImporte);
        Assert.AreEqual(4, snapshot.DecimalesPorcentaje);
    }

    [TestMethod]
    public void Build_PropagaDecimalesDelProyecto()
    {
        var snapshot = new ManoObraReportSnapshotBuilder()
            .Build(
                1,
                "M",
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
        var snapshot = ManoObraCatalogExportResolver.BuildSnapshot(1, "M", null);

        CollectionAssert.AreEqual(
            new[] { "Clave", "Descripcion", "Unidad", "SalarioBase", "FactorSalarioReal", "SalarioReal", "Origen" },
            snapshot.Columnas.Select(c => c.Identificador).ToArray());

        Assert.AreEqual(7, ManoObraCatalogExportResolver.DefaultColumns().Count);
    }

    [TestMethod]
    public void BuildSnapshot_ColumnasVacias_UsaDefaultsEnMemoria()
    {
        var snapshot = ManoObraCatalogExportResolver.BuildSnapshot(1, "M", Array.Empty<ColumnaManoObra>());

        Assert.AreEqual(7, snapshot.Columnas.Count);
    }

    [TestMethod]
    public void BuildSnapshot_UsaColumnasPersistidasCuandoExisten()
    {
        var columnas = new[] { Columna("Clave", "Clave", orden: 0), Columna("SalarioBase", "Salario Base", orden: 1, alineacion: AlineacionColumna.Derecha, formato: "C2") };

        var snapshot = ManoObraCatalogExportResolver.BuildSnapshot(1, "M", columnas);

        CollectionAssert.AreEqual(
            new[] { "Clave", "SalarioBase" },
            snapshot.Columnas.Select(c => c.Identificador).ToArray());
    }

    [TestMethod]
    public void BuildSnapshot_PropagaDecimalesDelProyecto()
    {
        var snapshot = ManoObraCatalogExportResolver.BuildSnapshot(
            1, "M", null, decimalesCantidad: 5, decimalesImporte: 1, decimalesPorcentaje: 6);

        Assert.AreEqual(5, snapshot.DecimalesCantidad);
        Assert.AreEqual(1, snapshot.DecimalesImporte);
        Assert.AreEqual(6, snapshot.DecimalesPorcentaje);
    }

    [TestMethod]
    public void ResolveValue_DevuelveTextoDeColumnasDeTexto()
    {
        var mano = ManoDeObraEjemplo();
        var snapshot = ManoObraCatalogExportResolver.BuildSnapshot(1, "M", null);

        Assert.AreEqual("MO-01", ManoObraCatalogExportResolver.ResolveValue(mano, ColDe(snapshot, "Clave")));
        Assert.AreEqual("Peón", ManoObraCatalogExportResolver.ResolveValue(mano, ColDe(snapshot, "Descripcion")));
        Assert.AreEqual("jor", ManoObraCatalogExportResolver.ResolveValue(mano, ColDe(snapshot, "Unidad")));
        Assert.AreEqual("Maestro", ManoObraCatalogExportResolver.ResolveValue(mano, ColDe(snapshot, "Origen")));
    }

    [TestMethod]
    public void ResolveValue_ColumnasNumericasNoDevuelvenTextoFormateado()
    {
        // El valor crudo de las numéricas lo entrega TryResolveNumber; ResolveValue
        // es sólo texto (el formato vive en el contrato neutral de Reporting).
        var mano = ManoDeObraEjemplo();
        var snapshot = ManoObraCatalogExportResolver.BuildSnapshot(1, "M", null);

        Assert.AreEqual(string.Empty, ManoObraCatalogExportResolver.ResolveValue(mano, ColDe(snapshot, "SalarioBase")));
        Assert.AreEqual(string.Empty, ManoObraCatalogExportResolver.ResolveValue(mano, ColDe(snapshot, "FactorSalarioReal")));
        Assert.AreEqual(string.Empty, ManoObraCatalogExportResolver.ResolveValue(mano, ColDe(snapshot, "SalarioReal")));
    }

    [TestMethod]
    public void TryResolveNumber_ResuelveSalariosYFactor()
    {
        var mano = ManoDeObraEjemplo();
        var snapshot = ManoObraCatalogExportResolver.BuildSnapshot(1, "M", null);

        Assert.IsTrue(ManoObraCatalogExportResolver.TryResolveNumber(mano, ColDe(snapshot, "SalarioBase"), out var sb));
        Assert.AreEqual(250.5m, sb);

        Assert.IsTrue(ManoObraCatalogExportResolver.TryResolveNumber(mano, ColDe(snapshot, "FactorSalarioReal"), out var fsr));
        Assert.AreEqual(1.6543m, fsr);

        Assert.IsTrue(ManoObraCatalogExportResolver.TryResolveNumber(mano, ColDe(snapshot, "SalarioReal"), out var sr));
        Assert.AreEqual(414.4m, sr);
    }

    [TestMethod]
    public void TryResolveNumber_ColumnasDeTextoOFueraDeCatalogo_DevuelveFalse()
    {
        var mano = ManoDeObraEjemplo();
        var snapshot = ManoObraCatalogExportResolver.BuildSnapshot(1, "M", null);
        var rendimiento = ReportColumnDefinitionMapper.MapearManoObra(
            Columna("Rendimiento", "Rendimiento", alineacion: AlineacionColumna.Derecha, formato: "N2"));

        Assert.IsFalse(ManoObraCatalogExportResolver.TryResolveNumber(mano, ColDe(snapshot, "Clave"), out _));
        Assert.IsFalse(ManoObraCatalogExportResolver.TryResolveNumber(mano, rendimiento, out _));
        Assert.AreEqual(string.Empty, ManoObraCatalogExportResolver.ResolveValue(mano, rendimiento));
    }

    [TestMethod]
    public void ResolveOrigin_MapeaMaestroYProyecto()
    {
        Assert.AreEqual("Maestro", ManoObraCatalogExportResolver.ResolveOrigin(
            new ManoDeObra { Origen = OrigenInsumo.Maestro }));
        Assert.AreEqual("Proyecto", ManoObraCatalogExportResolver.ResolveOrigin(
            new ManoDeObra { Origen = OrigenInsumo.Proyecto }));
    }

    [TestMethod]
    public void ResolveCellBackground_UsaBandeoYRespetaOverrideDeColumna()
    {
        var tabla = ReportTableStyle.LegacyCatalogo();
        var defaultCol = ReportColumnDefinitionMapper.MapearManoObra(Columna("Clave", "Clave"));
        var overrideCol = ReportColumnDefinitionMapper.MapearManoObra(Columna("Clave", "Clave", colorFondo: "#00FF00"));

        Assert.AreEqual("#F5F5F5", ManoObraCatalogExportResolver.ResolveCellBackground(defaultCol, tabla, esFilaAlterna: true));
        Assert.AreEqual("#FFFFFF", ManoObraCatalogExportResolver.ResolveCellBackground(defaultCol, tabla, esFilaAlterna: false));
        Assert.AreEqual("#00FF00", ManoObraCatalogExportResolver.ResolveCellBackground(overrideCol, tabla, esFilaAlterna: true));
    }

    // ────────────── Auxiliares ──────────────

    private static ManoDeObra ManoDeObraEjemplo() => new()
    {
        Clave = "MO-01",
        Descripcion = "Peón",
        Unidad = "jor",
        SalarioBase = 250.5m,
        FactorSalarioReal = 1.6543m,
        SalarioReal = 414.4m,
        Origen = OrigenInsumo.Maestro,
    };

    private static ReportColumnDefinition ColDe(ReportColumnSnapshot snapshot, string identificador)
        => snapshot.Columnas.Single(c => c.Identificador == identificador);
}
