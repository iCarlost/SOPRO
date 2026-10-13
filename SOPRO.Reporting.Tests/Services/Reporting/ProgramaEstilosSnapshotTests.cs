using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SOPRO.Application.Models.Reporting.ReportColumns;
using SOPRO.Application.Services.Reporting;
using SOPRO.Application.UseCases.Reporting;
using SOPRO.Reporting.Formatting;

namespace SOPRO.Reporting.Tests.Services.Reporting;

/// <summary>
/// Paso 6.2 — contrato de claves estables y estilos NEUTROS de los PROGRAMAS
/// (Obra e Insumos) que consumen el grid, PDF y Excel.
///
/// Fija: las claves estables de fila (<c>ACT:{Id}</c> actividad, <c>RES:{Clave}</c>
/// agrupador, <c>INS:{InsumoId}</c> insumo); que la vista por defecto coincide con
/// la que persiste el grid (<c>""</c>); que un snapshot SIN overrides conserva los
/// estilos en <c>null</c>; y la precedencia override de celda &gt; rol &gt; columna.
/// </summary>
[TestClass]
public class ProgramaEstilosSnapshotTests
{
    private const int Proyecto = 7;

    private static ReportColumnSnapshot SnapshotObra()
        => new ProgramaObraReportSnapshotBuilder().Build(Proyecto, "Obra", columnas: null);

    private static ReportColumnSnapshot SnapshotInsumos()
        => new ProgramaInsumosReportSnapshotBuilder().Build(Proyecto, "Insumos", programa: null, ProgramaInsumosVista.Mixto);

    // ─────────────────────────── Claves estables ───────────────────────────

    [TestMethod]
    public void ClaveFila_Obra_ActividadUsaPrefijoAct()
        => Assert.AreEqual("ACT:5", ProgramaObraReportSnapshotBuilder.ClaveFila(esResumen: false, actividadId: 5, clave: "1.2"));

    [TestMethod]
    public void ClaveFila_Obra_ResumenUsaPrefijoResYLaClave()
        => Assert.AreEqual("RES:1", ProgramaObraReportSnapshotBuilder.ClaveFila(esResumen: true, actividadId: 9, clave: "1"));

    [TestMethod]
    public void ClaveFila_Insumos_UsaPrefijoIns()
        => Assert.AreEqual("INS:7", ProgramaInsumosReportSnapshotBuilder.ClaveFila(7));

    [TestMethod]
    public void ClaveVista_EsLaVistaPorDefectoQuePersisteElGrid()
    {
        Assert.AreEqual(string.Empty, ProgramaObraReportSnapshotBuilder.ClaveVista);
        Assert.AreEqual(string.Empty, ProgramaInsumosReportSnapshotBuilder.ClaveVista);
    }

    [TestMethod]
    public void RolFila_AgrupadorEsResumenYActividadEsConcepto()
    {
        Assert.AreEqual(RolFilaReporte.Resumen, ProgramaObraReportSnapshotBuilder.RolFila(true));
        Assert.AreEqual(RolFilaReporte.Concepto, ProgramaObraReportSnapshotBuilder.RolFila(false));
        Assert.AreEqual(RolFilaReporte.Resumen, ProgramaInsumosReportSnapshotBuilder.RolFila(true));
        Assert.AreEqual(RolFilaReporte.Concepto, ProgramaInsumosReportSnapshotBuilder.RolFila(false));
    }

    // ─────────────────────────── Snapshot sin overrides ───────────────────────────

    [TestMethod]
    public void SnapshotSinOverrides_NoTieneEstilos()
    {
        var snapshot = SnapshotObra();

        Assert.IsNull(snapshot.EstilosCelda);
        Assert.IsNull(snapshot.EstilosFila);
        Assert.AreEqual("ProgramaObra", snapshot.TipoReporte);
    }

    // ─────────────────────────── Resolver ───────────────────────────

    [TestMethod]
    public void CombinarDefaultsResumen_AplicaElGrisAlResolver()
    {
        var snapshot = SnapshotObra() with
        {
            EstilosFila = EstilosFilaPredeterminados.CombinarProgramas((IEnumerable<ReportRowRoleStyle>?)null),
        };

        var resultado = ReportCellStyleResolver.Resolve(
            snapshot, ProgramaObraReportSnapshotBuilder.ClaveVista, "RES:1", "colImporte", RolFilaReporte.Resumen);

        Assert.AreEqual("#F0F0F0", resultado.ColorFondo);
        Assert.AreEqual((bool?)true, resultado.Negrita);
    }

    [TestMethod]
    public void Resolver_OverrideDeCelda_GanaSobreRolYColumna()
    {
        var snapshot = SnapshotObra() with
        {
            EstilosCelda = new[]
            {
                new ReportCellStyleOverride(
                    new ReportCellAddress(ProgramaObraReportSnapshotBuilder.ClaveVista, "ACT:2", "colImporte"),
                    new ReportStyleOverlay(ColorFondo: "#FF00AA")),
            },
            EstilosFila = new[]
            {
                new ReportRowRoleStyle(RolFilaReporte.Resumen, new ReportStyleOverlay(ColorFondo: "#DDDDDD")),
            },
        };

        var resultado = ReportCellStyleResolver.Resolve(
            snapshot, ProgramaObraReportSnapshotBuilder.ClaveVista, "ACT:2", "colImporte", RolFilaReporte.Concepto);

        Assert.AreEqual("#FF00AA", resultado.ColorFondo, "La celda tiene la máxima precedencia.");
    }

    [TestMethod]
    public void Resolver_SnapshotSinOverrides_ConservaElEstiloDeColumna()
    {
        var snapshot = SnapshotObra();
        var columna = snapshot.Columnas.Single(c => c.Identificador == "colImporte");

        var resultado = ReportCellStyleResolver.Resolve(
            snapshot, ProgramaObraReportSnapshotBuilder.ClaveVista, "ACT:2", "colImporte", RolFilaReporte.Concepto);

        Assert.AreEqual(columna.EstiloContenido.Fuente, resultado.Fuente, "Sin overrides se hereda la columna.");
        Assert.AreEqual(columna.Alineacion, resultado.AlineacionHorizontal);
    }

    [TestMethod]
    public void ResolverInsumos_OverrideDeCeldaGanaEnColumnaDinamicaDePeriodo()
    {
        // La columna dinámica usa el identificador ESTABLE del período (per_501),
        // no el índice visual: el formato sobrevive a regenerar/reordenar períodos.
        var snapshot = SnapshotInsumos() with
        {
            EstilosCelda = new[]
            {
                new ReportCellStyleOverride(
                    new ReportCellAddress(ProgramaInsumosReportSnapshotBuilder.ClaveVista, "INS:5", "per_501"),
                    new ReportStyleOverlay(ColorFondo: "#123456")),
            },
        };

        var resultado = ReportCellStyleResolver.Resolve(
            snapshot, ProgramaInsumosReportSnapshotBuilder.ClaveVista, "INS:5", "per_501", RolFilaReporte.Concepto);

        Assert.AreEqual("#123456", resultado.ColorFondo);
    }
}
