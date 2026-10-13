using Microsoft.VisualStudio.TestTools.UnitTesting;
using SOPRO.Application.Models.Reporting.ReportColumns;
using SOPRO.Application.Services.Reporting;
using SOPRO.Application.UseCases.Reporting;
using SOPRO.Data.Context;
using SOPRO.Reporting.Formatting;

namespace SOPRO.Reporting.Tests.Services.Reporting;

/// <summary>
/// Paso 5.2 — estilos por celda/fila del APU viajando por el SNAPSHOT NEUTRO.
///
/// Fija el CONTRATO que consumen los generadores PDF/Excel de APU (en
/// SOPRO.WinForms, ejercidos en SOPRO.Tests.WinForms):
/// <list type="bullet">
///   <item>las claves estables de fila del APU (<c>C:{Id}</c> encabezado del
///   concepto, <c>COMP:{Id}</c> componentes, <c>PU:{Id}</c> precio unitario) y los
///   roles de fila;</item>
///   <item>el resolver de precedencia (celda &gt; rol &gt; columna) sobre un snapshot
///   construido por <see cref="ApuReportSnapshotBuilder"/>;</item>
///   <item>un snapshot SIN overrides conserva el estilo de columna/tabla
///   (retrocompatibilidad total);</item>
///   <item>aislamiento por <c>TipoReporte</c>: un override de Presupuesto NO aparece
///   en el snapshot de APU ni viceversa.</item>
/// </list>
/// </summary>
[TestClass]
public class ApuEstilosSnapshotTests
{
    private const int Proyecto = 21;
    private const string TipoApu = "APU";
    private const string TipoPresupuesto = "Presupuesto";

    private static ReportColumnSnapshot SnapshotApu()
        => new ApuReportSnapshotBuilder().Build(Proyecto, "Análisis de Precios Unitarios", columnas: null);

    // ─────────────────────────── Claves estables ───────────────────────────

    [TestMethod]
    public void ClaveFila_Concepto_ComponenteYPrecioUnitario()
    {
        Assert.AreEqual("C:101", ApuReportSnapshotBuilder.ClaveFilaConcepto(101));
        Assert.AreEqual("COMP:55", ApuReportSnapshotBuilder.ClaveFilaComponente(55));
        Assert.AreEqual("PU:101", ApuReportSnapshotBuilder.ClaveFilaPrecioUnitario(101));
    }

    [TestMethod]
    public void ClaveVista_EsLaVistaPorDefectoQuePersisteElGrid()
        => Assert.AreEqual(string.Empty, ApuReportSnapshotBuilder.ClaveVista);

    [TestMethod]
    public void RolFila_EncabezadoDeConceptoYPrecioUnitario()
    {
        Assert.AreEqual(RolFilaReporte.Concepto, ApuReportSnapshotBuilder.RolFilaConcepto);
        Assert.AreEqual(RolFilaReporte.Total, ApuReportSnapshotBuilder.RolFilaPrecioUnitario);
    }

    // ─────────────────────────── Resolver sobre el snapshot APU ───────────────────────────

    [TestMethod]
    public void Resolver_OverrideDeComponente_GanaSobreRolYColumna()
    {
        var celda = new ReportCellStyleOverride(
            new ReportCellAddress(ApuReportSnapshotBuilder.ClaveVista, "COMP:55", "ImporteTotal"),
            new ReportStyleOverlay(ColorFondo: "#FF00AA"));
        var rol = new ReportRowRoleStyle(RolFilaReporte.Concepto, new ReportStyleOverlay(ColorFondo: "#DDDDDD"));
        var snapshot = SnapshotApu() with { EstilosCelda = new[] { celda }, EstilosFila = new[] { rol } };

        var resultado = ReportCellStyleResolver.Resolve(
            snapshot, ApuReportSnapshotBuilder.ClaveVista, "COMP:55", "ImporteTotal", RolFilaReporte.Concepto);

        Assert.AreEqual("#FF00AA", resultado.ColorFondo, "La celda del componente tiene la máxima precedencia.");
    }

    [TestMethod]
    public void Resolver_OverrideDeEncabezadoDeConcepto_GanaSobreLaColumna()
    {
        var celda = new ReportCellStyleOverride(
            new ReportCellAddress(ApuReportSnapshotBuilder.ClaveVista, "C:101", "Descripcion"),
            new ReportStyleOverlay(ColorFondo: "#00AAFF", Negrita: true));
        var snapshot = SnapshotApu() with { EstilosCelda = new[] { celda } };

        var resultado = ReportCellStyleResolver.Resolve(
            snapshot, ApuReportSnapshotBuilder.ClaveVista, "C:101", "Descripcion", RolFilaReporte.Concepto);

        Assert.AreEqual("#00AAFF", resultado.ColorFondo);
        Assert.AreEqual((bool?)true, resultado.Negrita);
    }

    [TestMethod]
    public void Resolver_SnapshotSinOverrides_ConservaElEstiloDeColumna()
    {
        var snapshot = SnapshotApu();

        Assert.IsNull(snapshot.EstilosCelda, "Sin overrides persistidos, EstilosCelda queda null.");
        Assert.IsNull(snapshot.EstilosFila, "Sin estilos de fila, EstilosFila queda null.");

        var importe = snapshot.Columnas.Single(c => c.Identificador == "ImporteTotal");
        var resultado = ReportCellStyleResolver.Resolve(
            snapshot, ApuReportSnapshotBuilder.ClaveVista, "COMP:55", "ImporteTotal", null);

        Assert.AreEqual(importe.EstiloContenido.Fuente, resultado.Fuente);
        Assert.AreEqual(importe.EstiloContenido.ColorFondo, resultado.ColorFondo);
        Assert.AreEqual(importe.Alineacion, resultado.AlineacionHorizontal);
    }

    // ─────────────────────────── Integración con FormatoReporteService ───────────────────────────

    private static SOPROContext CrearContexto()
    {
        var dbPath = Path.Combine(Path.GetTempPath(), $"sopro_fmt_apu_{Guid.NewGuid():N}.db");
        var ctx = new SOPROContext(dbPath);
        ctx.Database.EnsureDeleted();
        ctx.Database.EnsureCreated();
        return ctx;
    }

    private static int AgregarProyecto(SOPROContext ctx)
    {
        var proyecto = new SOPRO.Core.Entities.Proyecto
        {
            Nombre = "Proyecto APU",
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
    public void ReportStyleSnapshotBuilder_PueblaEstilosApuConLasClavesDelGrid()
    {
        using var ctx = CrearContexto();
        var proyectoId = AgregarProyecto(ctx);
        var formatos = new FormatoReporteService(ctx);

        formatos.UpsertCelda(proyectoId, TipoApu, ApuReportSnapshotBuilder.ClaveVista,
            ApuReportSnapshotBuilder.ClaveFilaComponente(55), "ImporteTotal",
            new ReportStyleOverlay(ColorFondo: "#112233"));
        formatos.UpsertEstiloFila(proyectoId, TipoApu, ApuReportSnapshotBuilder.RolFilaPrecioUnitario,
            new ReportStyleOverlay(ColorFondo: "#2C3E50", Negrita: true));

        var baseSnapshot = SnapshotApu() with { ProyectoId = proyectoId };
        var snapshot = new ReportStyleSnapshotBuilder(formatos).Build(baseSnapshot);

        Assert.IsNotNull(snapshot.EstilosCelda);
        var celda = snapshot.EstilosCelda!.Single();
        Assert.AreEqual("COMP:55", celda.Direccion.ClaveFila);
        Assert.AreEqual("ImporteTotal", celda.Direccion.NombreColumna);
        Assert.AreEqual("#112233", celda.Estilo.ColorFondo);

        Assert.IsNotNull(snapshot.EstilosFila);
        Assert.AreEqual(RolFilaReporte.Total, snapshot.EstilosFila!.Single().RolFila);
    }

    [TestMethod]
    public void Aislamiento_PorTipoReporte_ApuNoVePresupuesto()
    {
        using var ctx = CrearContexto();
        var proyectoId = AgregarProyecto(ctx);
        var formatos = new FormatoReporteService(ctx);

        // Override de Presupuesto en la MISMA clave de fila/proyecto.
        formatos.UpsertCelda(proyectoId, TipoPresupuesto, ApuReportSnapshotBuilder.ClaveVista,
            ApuReportSnapshotBuilder.ClaveFilaComponente(55), "ImporteTotal",
            new ReportStyleOverlay(ColorFondo: "#AABBCC"));

        var snapshot = new ReportStyleSnapshotBuilder(formatos).Build(SnapshotApu() with { ProyectoId = proyectoId });

        Assert.IsNull(snapshot.EstilosCelda, "Un override de Presupuesto no debe aparecer en el snapshot de APU.");
    }

    [TestMethod]
    public void Aislamiento_PorTipoReporte_PresupuestoNoVeApu()
    {
        using var ctx = CrearContexto();
        var proyectoId = AgregarProyecto(ctx);
        var formatos = new FormatoReporteService(ctx);

        formatos.UpsertCelda(proyectoId, TipoApu, ApuReportSnapshotBuilder.ClaveVista, "C:7", "ImporteTotal",
            new ReportStyleOverlay(ColorFondo: "#123456"));

        var snapshotPresupuesto = new PresupuestoReportSnapshotBuilder()
            .Build(proyectoId, "P", Array.Empty<SOPRO.Core.Entities.ColumnaPersonalizada>());
        var snapshot = new ReportStyleSnapshotBuilder(formatos).Build(snapshotPresupuesto);

        Assert.IsNull(snapshot.EstilosCelda, "Un override de APU no debe aparecer en el snapshot de Presupuesto.");
    }
}
