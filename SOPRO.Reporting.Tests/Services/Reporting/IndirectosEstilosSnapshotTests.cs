using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SOPRO.Application.Models.Reporting.ReportColumns;
using SOPRO.Application.Services;
using SOPRO.Application.Services.Reporting;
using SOPRO.Application.UseCases.Reporting;
using SOPRO.Core.Entities;
using SOPRO.Reporting.Formatting;

namespace SOPRO.Reporting.Tests.Services.Reporting;

/// <summary>
/// Paso 5.1 — contrato de claves estables y estilos NEUTROS del reporte de
/// Indirectos que consumen el grid, PDF y Excel.
///
/// Fija:
/// <list type="bullet">
///   <item>las claves estables de fila (<c>G:{Id}</c> grupo, <c>IC:{Id}</c>
///   concepto) y el rol de fila del grid (GRUPO→Grupo, CONCEPTO→Concepto);</item>
///   <item>que la vista por defecto coincide con la que persiste el grid
///   (<c>""</c>);</item>
///   <item>que un snapshot SIN overrides conserva <c>EstilosCelda</c>/<c>EstilosFila</c>
///   en <c>null</c> (retrocompatibilidad) y que, al combinar los defaults por rol,
///   el resolver aplica el gris de Grupo;</item>
///   <item>la precedencia override de celda &gt; rol &gt; columna.</item>
/// </list>
/// </summary>
[TestClass]
public class IndirectosEstilosSnapshotTests
{
    private const int Proyecto = 7;

    [TestMethod]
    public void ClaveFila_Grupo_UsaPrefijoG()
        => Assert.AreEqual("G:5", IndirectosReportSnapshotBuilder.ClaveFila(new GrupoIndirecto { Id = 5 }));

    [TestMethod]
    public void ClaveFila_Concepto_UsaPrefijoIC()
        => Assert.AreEqual("IC:9", IndirectosReportSnapshotBuilder.ClaveFila(new ConceptoIndirecto { Id = 9 }));

    [TestMethod]
    public void ClaveVista_EsLaVistaPorDefectoQuePersisteElGrid()
        => Assert.AreEqual(string.Empty, IndirectosReportSnapshotBuilder.ClaveVista);

    [TestMethod]
    public void RolFila_MapeaLosTiposDelGrid()
    {
        Assert.AreEqual(RolFilaReporte.Grupo, IndirectosReportSnapshotBuilder.RolFila("GRUPO"));
        Assert.AreEqual(RolFilaReporte.Concepto, IndirectosReportSnapshotBuilder.RolFila("Concepto"));
        Assert.IsNull(IndirectosReportSnapshotBuilder.RolFila("Otro"));
        Assert.IsNull(IndirectosReportSnapshotBuilder.RolFila(null));
    }

    [TestMethod]
    public void SnapshotSinOverrides_NoTieneEstilos()
    {
        var snapshot = IndirectosExportResolver.BuildSnapshot(Proyecto, "Indirectos", columnas: null);

        Assert.IsNull(snapshot.EstilosCelda);
        Assert.IsNull(snapshot.EstilosFila);
    }

    [TestMethod]
    public void Resolver_OverrideDeCelda_GanaSobreRolYColumna()
    {
        var snapshot = IndirectosExportResolver.BuildSnapshot(Proyecto, "Indirectos", columnas: null) with
        {
            EstilosCelda = new[]
            {
                new ReportCellStyleOverride(
                    new ReportCellAddress(IndirectosReportSnapshotBuilder.ClaveVista, "IC:9", "ImporteTotal"),
                    new ReportStyleOverlay(ColorFondo: "#FF00AA")),
            },
            EstilosFila = new[]
            {
                new ReportRowRoleStyle(RolFilaReporte.Concepto, new ReportStyleOverlay(ColorFondo: "#DDDDDD")),
            },
        };

        var resultado = ReportCellStyleResolver.Resolve(
            snapshot, IndirectosReportSnapshotBuilder.ClaveVista, "IC:9", "ImporteTotal", RolFilaReporte.Concepto);

        Assert.AreEqual("#FF00AA", resultado.ColorFondo, "La celda tiene la máxima precedencia.");
    }

    [TestMethod]
    public void CombinarDefaultsGrupo_AplicaElGrisAlResolver()
    {
        var snapshot = IndirectosExportResolver.BuildSnapshot(Proyecto, "Indirectos", columnas: null);
        var conDefaults = snapshot with
        {
            EstilosFila = EstilosFilaPredeterminados.CombinarIndirectos((IEnumerable<ReportRowRoleStyle>?)null),
        };

        var resultado = ReportCellStyleResolver.Resolve(
            conDefaults, IndirectosReportSnapshotBuilder.ClaveVista, "G:1", "ImporteTotal", RolFilaReporte.Grupo);

        Assert.AreEqual("#E6E6E6", resultado.ColorFondo);
        Assert.AreEqual((bool?)true, resultado.Negrita);
    }

    [TestMethod]
    public void Resolver_SnapshotSinOverrides_ConservaElEstiloDeColumna()
    {
        var snapshot = IndirectosExportResolver.BuildSnapshot(Proyecto, "Indirectos", columnas: null);
        var total = snapshot.Columnas.Single(c => c.Identificador == "ImporteTotal");

        var resultado = ReportCellStyleResolver.Resolve(
            snapshot, IndirectosReportSnapshotBuilder.ClaveVista, "G:1", "ImporteTotal", RolFilaReporte.Grupo);

        Assert.AreEqual(total.EstiloContenido.Fuente, resultado.Fuente, "Sin overrides se hereda la columna.");
        Assert.AreEqual(total.Alineacion, resultado.AlineacionHorizontal);
    }
}
