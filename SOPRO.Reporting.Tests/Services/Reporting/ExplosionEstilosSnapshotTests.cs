using System;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SOPRO.Application.Models.Explosion;
using SOPRO.Application.Models.Reporting.ReportColumns;
using SOPRO.Application.Services;
using SOPRO.Application.Services.Reporting;
using SOPRO.Application.UseCases.Reporting;
using SOPRO.Data.Context;
using SOPRO.Reporting.Formatting;

namespace SOPRO.Reporting.Tests.Services.Reporting;

/// <summary>
/// Paso 6.1 — contrato de claves estables y estilos NEUTROS del reporte de
/// Explosión de Insumos que consumen el grid, PDF y Excel.
///
/// Fija:
/// <list type="bullet">
///   <item>las claves estables de fila (banda <c>SEC:</c>, detalle <c>D:seccion:clave</c>,
///   total de sección <c>T:seccion</c>, <c>TOTALGENERAL</c> y referencia
///   <c>REF:n</c>) y el rol de fila de cada tipo de renglón;</item>
///   <item>que la vista por defecto coincide con la que persiste el grid (<c>""</c>);</item>
///   <item>que un snapshot SIN overrides conserva <c>EstilosCelda</c>/<c>EstilosFila</c>
///   en <c>null</c> (retrocompatibilidad) y que, al combinar los defaults por rol,
///   el resolver aplica el color de la banda de sección;</item>
///   <item>la precedencia override de celda &gt; rol &gt; columna;</item>
///   <item>el aislamiento por <c>TipoReporte</c> ("Explosion").</item>
/// </list>
/// </summary>
[TestClass]
public class ExplosionEstilosSnapshotTests
{
    private const int Proyecto = 42;
    private const string TipoExplosion = "Explosion";
    private const string TipoPresupuesto = "Presupuesto";

    // ─────────────────────────── Claves estables ───────────────────────────

    [TestMethod]
    public void ClaveVista_EsLaVistaPorDefectoQuePersisteElGrid()
        => Assert.AreEqual(string.Empty, ExplosionReportSnapshotBuilder.ClaveVista);

    [TestMethod]
    public void ClaveFila_SeccionDetalleTotalGeneralYReferencia()
    {
        Assert.AreEqual("SEC:MATERIALES", ExplosionReportSnapshotBuilder.ClaveFilaSeccion("Materiales"));
        Assert.AreEqual("D:MATERIALES:MAT-01", ExplosionReportSnapshotBuilder.ClaveFilaDetalle("Materiales", "MAT-01"));
        Assert.AreEqual("T:MATERIALES", ExplosionReportSnapshotBuilder.ClaveFilaTotalSeccion("Materiales"));
        Assert.AreEqual("TOTALGENERAL", ExplosionReportSnapshotBuilder.ClaveFilaTotalGeneral);
        Assert.AreEqual("REF:0", ExplosionReportSnapshotBuilder.ClaveFilaReferencia(0));
        Assert.AreEqual("REF:1", ExplosionReportSnapshotBuilder.ClaveFilaReferencia(1));
    }

    [TestMethod]
    public void RolFila_MapeaLosTiposDelGrid()
    {
        Assert.AreEqual(RolFilaReporte.Encabezado, ExplosionReportSnapshotBuilder.RolFila(ExplosionRowKind.Encabezado));
        Assert.AreEqual(RolFilaReporte.Total, ExplosionReportSnapshotBuilder.RolFila(ExplosionRowKind.Total));
        Assert.AreEqual(RolFilaReporte.TotalGeneral, ExplosionReportSnapshotBuilder.RolFila(ExplosionRowKind.TotalGeneral));
        Assert.AreEqual(RolFilaReporte.Referencia, ExplosionReportSnapshotBuilder.RolFila(ExplosionRowKind.Referencia));
        Assert.AreEqual(RolFilaReporte.Concepto, ExplosionReportSnapshotBuilder.RolFila(ExplosionRowKind.Detalle));
        Assert.IsNull(ExplosionReportSnapshotBuilder.RolFila(ExplosionRowKind.Vacia));
    }

    [TestMethod]
    public void TipoReporte_EsExplosion()
        => Assert.AreEqual("Explosion", ExplosionReportSnapshotBuilder.TipoReporte);

    // ─────────────────────────── Snapshot y resolver ───────────────────────────

    [TestMethod]
    public void SnapshotSinOverrides_NoTieneEstilos()
    {
        var snapshot = ExplosionExportResolver.BuildSnapshot(Proyecto, "Explosión", columnas: null);

        Assert.IsNull(snapshot.EstilosCelda);
        Assert.IsNull(snapshot.EstilosFila);
    }

    [TestMethod]
    public void CombinarDefaultsExplosion_AplicaElColorDeBandaAlResolver()
    {
        var snapshot = ExplosionExportResolver.BuildSnapshot(Proyecto, "Explosión", columnas: null);
        var conDefaults = snapshot with
        {
            EstilosFila = EstilosFilaPredeterminados.CombinarExplosion((System.Collections.Generic.IEnumerable<ReportRowRoleStyle>?)null),
        };

        var resultado = ReportCellStyleResolver.Resolve(
            conDefaults,
            ExplosionReportSnapshotBuilder.ClaveVista,
            ExplosionReportSnapshotBuilder.ClaveFilaSeccion("Materiales"),
            "Descripcion",
            RolFilaReporte.Encabezado);

        Assert.AreEqual("#4682B4", resultado.ColorFondo);
        Assert.AreEqual((bool?)true, resultado.Negrita);
    }

    [TestMethod]
    public void Resolver_OverrideDeCelda_GanaSobreRolYColumna()
    {
        var snapshot = ExplosionExportResolver.BuildSnapshot(Proyecto, "Explosión", columnas: null) with
        {
            EstilosCelda = new[]
            {
                new ReportCellStyleOverride(
                    new ReportCellAddress(ExplosionReportSnapshotBuilder.ClaveVista, "D:MATERIALES:MAT-01", "ImporteTotal"),
                    new ReportStyleOverlay(ColorFondo: "#FF00AA")),
            },
            EstilosFila = new[]
            {
                new ReportRowRoleStyle(RolFilaReporte.Concepto, new ReportStyleOverlay(ColorFondo: "#DDDDDD")),
            },
        };

        var resultado = ReportCellStyleResolver.Resolve(
            snapshot,
            ExplosionReportSnapshotBuilder.ClaveVista,
            "D:MATERIALES:MAT-01",
            "ImporteTotal",
            RolFilaReporte.Concepto);

        Assert.AreEqual("#FF00AA", resultado.ColorFondo, "La celda tiene la máxima precedencia.");
    }

    [TestMethod]
    public void Resolver_SnapshotSinOverrides_ConservaElEstiloDeColumna()
    {
        var snapshot = ExplosionExportResolver.BuildSnapshot(Proyecto, "Explosión", columnas: null);
        var importe = snapshot.Columnas.Single(c => c.Identificador == "ImporteTotal");

        var resultado = ReportCellStyleResolver.Resolve(
            snapshot,
            ExplosionReportSnapshotBuilder.ClaveVista,
            "D:MATERIALES:MAT-01",
            "ImporteTotal",
            RolFilaReporte.Concepto);

        Assert.AreEqual(importe.EstiloContenido.Fuente, resultado.Fuente);
        Assert.AreEqual(importe.EstiloContenido.ColorFondo, resultado.ColorFondo);
        Assert.AreEqual(importe.Alineacion, resultado.AlineacionHorizontal);
    }

    // ─────────────────── Integración con FormatoReporteService ───────────────────

    private static SOPROContext CrearContexto()
    {
        var dbPath = Path.Combine(Path.GetTempPath(), $"sopro_fmt_expl_{Guid.NewGuid():N}.db");
        var ctx = new SOPROContext(dbPath);
        ctx.Database.EnsureDeleted();
        ctx.Database.EnsureCreated();
        return ctx;
    }

    private static int AgregarProyecto(SOPROContext ctx)
    {
        var proyecto = new SOPRO.Core.Entities.Proyecto
        {
            Nombre = "Proyecto explosión",
            Descripcion = string.Empty,
            Ubicacion = string.Empty,
            Convocante = string.Empty,
            Contratista = string.Empty,
            ApoderadoLegal = string.Empty,
            FechaInicio = new DateTime(2026, 1, 1),
            FechaTermino = new DateTime(2026, 1, 31),
            PlazoEjecucion = 31,
            DecimalesCantidad = 2,
            DecimalesImporte = 2,
            DecimalesPorcentaje = 4,
        };
        ctx.Proyectos.Add(proyecto);
        ctx.SaveChanges();
        return proyecto.Id;
    }

    [TestMethod]
    public void ReportStyleSnapshotBuilder_PueblaEstilosExplosionConLasClavesDelGrid()
    {
        using var ctx = CrearContexto();
        var proyectoId = AgregarProyecto(ctx);
        var formatos = new FormatoReporteService(ctx);

        formatos.UpsertCelda(proyectoId, TipoExplosion, ExplosionReportSnapshotBuilder.ClaveVista,
            ExplosionReportSnapshotBuilder.ClaveFilaDetalle("Materiales", "MAT-01"), "ImporteTotal",
            new ReportStyleOverlay(ColorFondo: "#112233"));
        formatos.UpsertEstiloFila(proyectoId, TipoExplosion, RolFilaReporte.Encabezado,
            new ReportStyleOverlay(ColorFondo: "#2C3E50", Negrita: true));

        var baseSnapshot = ExplosionExportResolver.BuildSnapshot(proyectoId, "Explosión", columnas: null);
        var snapshot = ExplosionReportSnapshotBuilder.ConEstilos(baseSnapshot, formatos);

        Assert.IsNotNull(snapshot.EstilosCelda);
        var celda = snapshot.EstilosCelda!.Single();
        Assert.AreEqual("D:MATERIALES:MAT-01", celda.Direccion.ClaveFila);
        Assert.AreEqual("ImporteTotal", celda.Direccion.NombreColumna);
        Assert.AreEqual("#112233", celda.Estilo.ColorFondo);

        Assert.IsNotNull(snapshot.EstilosFila);
        Assert.AreEqual(RolFilaReporte.Encabezado, snapshot.EstilosFila!.Single().RolFila);
    }

    [TestMethod]
    public void Aislamiento_PorTipoReporte_ExplosionNoVePresupuesto()
    {
        using var ctx = CrearContexto();
        var proyectoId = AgregarProyecto(ctx);
        var formatos = new FormatoReporteService(ctx);

        // Mismo proyecto y misma clave de fila, pero TipoReporte distinto.
        formatos.UpsertCelda(proyectoId, TipoPresupuesto, ExplosionReportSnapshotBuilder.ClaveVista,
            "D:MATERIALES:MAT-01", "ImporteTotal",
            new ReportStyleOverlay(ColorFondo: "#AABBCC"));

        var snapshot = ExplosionReportSnapshotBuilder.ConEstilos(
            ExplosionExportResolver.BuildSnapshot(proyectoId, "Explosión", columnas: null), formatos);

        Assert.IsNull(snapshot.EstilosCelda, "Un override de Presupuesto no debe aparecer en el snapshot de Explosión.");
    }
}
