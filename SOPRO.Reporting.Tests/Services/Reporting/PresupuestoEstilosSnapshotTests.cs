using Microsoft.VisualStudio.TestTools.UnitTesting;
using SOPRO.Application.Models.Reporting.ReportColumns;
using SOPRO.Application.Services.Reporting;
using SOPRO.Application.UseCases.Reporting;
using SOPRO.Core.Entities;
using SOPRO.Reporting.Formatting;

namespace SOPRO.Reporting.Tests.Services.Reporting;

/// <summary>
/// Paso 4.2 — estilos por celda/fila de Presupuesto viajando por el SNAPSHOT NEUTRO.
///
/// Estos tests fijan el CONTRATO que consumen los generadores PDF/Excel (que viven
/// en SOPRO.WinForms y se ejercen en SOPRO.Tests.WinForms):
/// <list type="bullet">
///   <item>las claves estables de fila del Presupuesto (<c>C:{Id}</c> para conceptos,
///   <c>SUB:{Clave}</c> para agrupadores y subtotales, <c>TOTAL</c>/<c>IVA</c>) y los
///   roles de fila coinciden con los que persiste el grid;</item>
///   <item>el resolver de precedencia (celda &gt; rol &gt; columna &gt; banding &gt; tabla)
///   resuelve correctamente sobre un snapshot construido por el builder de Presupuesto;</item>
///   <item>un snapshot SIN overrides conserva el estilo de columna/tabla
///   (retrocompatibilidad total).</item>
/// </list>
/// </summary>
[TestClass]
public class PresupuestoEstilosSnapshotTests
{
    private const int Proyecto = 7;

    // ─────────────────────────── Fixtures ───────────────────────────

    private static ColumnaPersonalizada Columna(
        string nombreInterno,
        int orden,
        TipoDatoColumna tipo = TipoDatoColumna.Texto,
        string formato = "")
        => new()
        {
            ProyectoId = Proyecto,
            NombreInterno = nombreInterno,
            Nombre = nombreInterno,
            Orden = orden,
            AnchoColumna = 100,
            TipoDato = tipo,
            WrapTexto = false,
            FormatoNumerico = formato,
        };

    private static ReportColumnSnapshot SnapshotPresupuesto() =>
        new PresupuestoReportSnapshotBuilder().Build(
            proyectoId: Proyecto,
            titulo: "Presupuesto de prueba",
            columnas: new[]
            {
                Columna("Clave", 0),
                Columna("Descripcion", 1),
                Columna("Cantidad", 2, TipoDatoColumna.Numerico),
                Columna("ImporteTotal", 3, TipoDatoColumna.Moneda),
            });

    private static ConceptoPresupuesto Concepto(int id, string clave, bool agrupador = false, int nivel = 0, string notas = "")
        => new()
        {
            Id = id,
            ProyectoId = Proyecto,
            Clave = clave,
            Descripcion = "Concepto " + id,
            Unidad = "pza",
            Nivel = nivel,
            EsAgrupador = agrupador,
            Notas = notas,
        };

    // ───────── Claves estables de fila (paridad con el grid 4.1) ─────────

    [TestMethod]
    public void ClaveFila_ConceptoTerminal_UsaIdDeDominio()
    {
        Assert.AreEqual("C:101", PresupuestoReportSnapshotBuilder.ClaveFila(Concepto(101, "1")));
    }

    [TestMethod]
    public void ClaveFila_Agrupador_UsaClaveSinteticaSub()
    {
        // El grid persiste los agrupadores (Capítulo/Subcapítulo/Nivel) como SUB:{Clave}.
        Assert.AreEqual("SUB:1", PresupuestoReportSnapshotBuilder.ClaveFila(Concepto(1, "1", agrupador: true, nivel: 0)));
    }

    [TestMethod]
    public void ClaveFila_SubtotalSintetico_UsaClaveSinteticaSub()
    {
        var subtotal = Concepto(0, "1.2", agrupador: true, nivel: 1,
            notas: PresupuestoReportSnapshotBuilder.MarcaSubtotalSintetico);
        Assert.AreEqual("SUB:1.2", PresupuestoReportSnapshotBuilder.ClaveFila(subtotal));
    }

    [TestMethod]
    public void ClaveVista_EsLaVistaPorDefectoQuePersisteElGrid()
    {
        Assert.AreEqual(string.Empty, PresupuestoReportSnapshotBuilder.ClaveVista);
    }

    // ───────────── Rol funcional de fila ─────────────

    [TestMethod]
    public void RolFila_ConceptoTerminal_EsConcepto()
    {
        Assert.AreEqual(RolFilaReporte.Concepto, PresupuestoReportSnapshotBuilder.RolFila(Concepto(101, "1")));
    }

    [TestMethod]
    public void RolFila_Agrupadores_SeClasificanPorNivel()
    {
        Assert.AreEqual(RolFilaReporte.Capitulo, PresupuestoReportSnapshotBuilder.RolFila(Concepto(1, "1", agrupador: true, nivel: 0)));
        Assert.AreEqual(RolFilaReporte.Subcapitulo, PresupuestoReportSnapshotBuilder.RolFila(Concepto(2, "1.1", agrupador: true, nivel: 1)));
        Assert.AreEqual(RolFilaReporte.Nivel1, PresupuestoReportSnapshotBuilder.RolFila(Concepto(3, "1.1.1", agrupador: true, nivel: 2)));
    }

    [TestMethod]
    public void RolFila_SubtotalSintetico_EsSubtotal()
    {
        var subtotal = Concepto(0, "1", agrupador: true, notas: PresupuestoReportSnapshotBuilder.MarcaSubtotalSintetico);
        Assert.AreEqual(RolFilaReporte.Subtotal, PresupuestoReportSnapshotBuilder.RolFila(subtotal));
    }

    // ───────── Resolver sobre el snapshot de Presupuesto ─────────

    [TestMethod]
    public void Resolver_OverrideDeCelda_GanaSobreRolYColumna()
    {
        var celda = new ReportCellStyleOverride(
            new ReportCellAddress(PresupuestoReportSnapshotBuilder.ClaveVista, "C:101", "ImporteTotal"),
            new ReportStyleOverlay(ColorFondo: "#FF00AA"));
        var rol = new ReportRowRoleStyle(RolFilaReporte.Concepto, new ReportStyleOverlay(ColorFondo: "#DDDDDD"));
        var snapshot = SnapshotPresupuesto() with
        {
            EstilosCelda = new[] { celda },
            EstilosFila = new[] { rol },
        };

        var resultado = ReportCellStyleResolver.Resolve(
            snapshot, PresupuestoReportSnapshotBuilder.ClaveVista, "C:101", "ImporteTotal", RolFilaReporte.Concepto);

        Assert.AreEqual("#FF00AA", resultado.ColorFondo, "La celda tiene la máxima precedencia.");
    }

    [TestMethod]
    public void Resolver_RolSinCelda_GanaSobreColumna()
    {
        var rol = new ReportRowRoleStyle(RolFilaReporte.Total, new ReportStyleOverlay(ColorFondo: "#2C3E50", Negrita: true));
        var snapshot = SnapshotPresupuesto() with { EstilosFila = new[] { rol } };

        var resultado = ReportCellStyleResolver.Resolve(
            snapshot, PresupuestoReportSnapshotBuilder.ClaveVista, ClaveFilaReporte.Total, "ImporteTotal", RolFilaReporte.Total);

        Assert.AreEqual("#2C3E50", resultado.ColorFondo);
        Assert.AreEqual((bool?)true, resultado.Negrita);
    }

    [TestMethod]
    public void Resolver_SnapshotSinOverrides_ConservaElEstiloDeColumna()
    {
        var snapshot = SnapshotPresupuesto();

        Assert.IsNull(snapshot.EstilosCelda, "Sin overrides persistidos, EstilosCelda queda null.");
        Assert.IsNull(snapshot.EstilosFila, "Sin estilos de fila, EstilosFila queda null.");

        var importe = snapshot.Columnas.Single(c => c.Identificador == "ImporteTotal");
        var resultado = ReportCellStyleResolver.Resolve(
            snapshot, PresupuestoReportSnapshotBuilder.ClaveVista, ClaveFilaReporte.Concepto(101), "ImporteTotal", RolFilaReporte.Concepto);

        Assert.AreEqual(importe.EstiloContenido.Fuente, resultado.Fuente);
        Assert.AreEqual(importe.EstiloContenido.ColorFondo, resultado.ColorFondo);
        Assert.AreEqual(importe.Alineacion, resultado.AlineacionHorizontal);
    }

    [TestMethod]
    public void Resolver_OverrideDeCeldaSoloConFondo_HeredaElRestoDeLaColumna()
    {
        var celda = new ReportCellStyleOverride(
            new ReportCellAddress(PresupuestoReportSnapshotBuilder.ClaveVista, "C:101", "ImporteTotal"),
            new ReportStyleOverlay(ColorFondo: "#ABCDEF"));
        var snapshot = SnapshotPresupuesto() with { EstilosCelda = new[] { celda } };
        var importe = snapshot.Columnas.Single(c => c.Identificador == "ImporteTotal");

        var resultado = ReportCellStyleResolver.Resolve(
            snapshot, PresupuestoReportSnapshotBuilder.ClaveVista, "C:101", "ImporteTotal", RolFilaReporte.Concepto);

        Assert.AreEqual("#ABCDEF", resultado.ColorFondo);
        Assert.AreEqual(importe.EstiloContenido.Fuente, resultado.Fuente, "La fuente se hereda de la columna.");
        Assert.AreEqual(importe.FormatoNumerico, resultado.FormatoNumerico, "El formato numérico se hereda de la columna.");
    }
}
