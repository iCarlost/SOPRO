using Microsoft.VisualStudio.TestTools.UnitTesting;
using SOPRO.Application.Models.Reporting.ReportColumns;
using SOPRO.Application.Services.Reporting;
using SOPRO.Application.UseCases.Reporting;
using SOPRO.Core.Entities;
using SOPRO.Data.Context;
using SOPRO.Tests.TestInfrastructure;

namespace SOPRO.Tests.Services.Reportes;

/// <summary>
/// Verificación de <see cref="ReportStyleSnapshotBuilder"/>: integra los overrides
/// persistidos (por celda y por rol de fila) en un <see cref="ReportColumnSnapshot"/>
/// ya construido, sin tocar columnas ni estilos de tabla.
///
/// Los valores se validan como contrato NEUTRAL (<see cref="ReportStyleOverlay"/>),
/// que es exactamente lo que consume <c>ReportCellStyleResolver</c> (probado en su
/// propio proyecto, SOPRO.Reporting.Tests, que no es referenciable desde aquí por
/// diferencia de target framework).
/// </summary>
[TestClass]
public class ReportStyleSnapshotBuilderTests
{
    private const string Tipo = "Presupuesto";

    private static Proyecto CrearProyecto() => new()
    {
        Nombre = "Proyecto",
        Descripcion = string.Empty,
        Ubicacion = string.Empty,
        Convocante = string.Empty,
        Contratista = string.Empty,
        ApoderadoLegal = string.Empty,
        FechaInicio = new DateTime(2026, 1, 1),
        FechaTermino = new DateTime(2026, 1, 31),
        PlazoEjecucion = 31,
        PorcentajeIndirectosCentral = 0m,
        PorcentajeIndirectosCampo = 0m,
        PorcentajeFinanciamiento = 0m,
        PorcentajeUtilidad = 0m,
        PorcentajeCargosAdicionales = 0m,
        DecimalesCantidad = 2,
        DecimalesImporte = 2,
        DecimalesPorcentaje = 4,
    };

    private static int AgregarProyecto(SOPROContext ctx)
    {
        var proyecto = CrearProyecto();
        ctx.Proyectos.Add(proyecto);
        ctx.SaveChanges();
        return proyecto.Id;
    }

    private static ReportColumnSnapshot SnapshotBase(int proyectoId, string tipo) =>
        new PresupuestoReportSnapshotBuilder().Build(proyectoId, "P", Array.Empty<ColumnaPersonalizada>())
        with
        {
            TipoReporte = tipo,
        };

    // ─────────────────── Sin overrides ───────────────────

    [TestMethod]
    public void Build_SinOverrides_DejaEstilosEnNullYConservaElSnapshot()
    {
        using var ctx = TestDbFactory.CreateContext();
        var proyectoId = AgregarProyecto(ctx);
        var builder = new ReportStyleSnapshotBuilder(new FormatoReporteService(ctx));

        var baseSnapshot = SnapshotBase(proyectoId, Tipo);
        var snapshot = builder.Build(baseSnapshot);

        Assert.IsNull(snapshot.EstilosCelda, "Sin overrides persistidos la colección queda null.");
        Assert.IsNull(snapshot.EstilosFila, "Sin estilos de fila la colección queda null.");

        // La integración no altera el resto del snapshot.
        Assert.AreEqual(baseSnapshot.TipoReporte, snapshot.TipoReporte);
        Assert.AreEqual(baseSnapshot.ProyectoId, snapshot.ProyectoId);
        Assert.AreEqual(baseSnapshot.Titulo, snapshot.Titulo);
        Assert.AreEqual(baseSnapshot.Columnas.Count, snapshot.Columnas.Count);
        Assert.AreEqual(baseSnapshot.EstiloTabla, snapshot.EstiloTabla);
    }

    // ─────────────────── Con overrides ───────────────────

    [TestMethod]
    public void Build_ConOverrides_PueblaEstilosCeldaYEstilosFila()
    {
        using var ctx = TestDbFactory.CreateContext();
        var proyectoId = AgregarProyecto(ctx);
        var formatos = new FormatoReporteService(ctx);

        formatos.UpsertCelda(proyectoId, Tipo, string.Empty, ClaveFilaReporte.Concepto(1), "ImporteTotal",
            new ReportStyleOverlay(ColorFondo: "#FF0000", Negrita: true));
        formatos.UpsertCelda(proyectoId, Tipo, "VistaA", ClaveFilaReporte.Concepto(1), "ImporteTotal",
            new ReportStyleOverlay(ColorFondo: "#00FF00"));
        formatos.UpsertEstiloFila(proyectoId, Tipo, RolFilaReporte.Total,
            new ReportStyleOverlay(ColorFondo: "#2C3E50", Fuente: "Arial"));

        var snapshot = new ReportStyleSnapshotBuilder(formatos).Build(SnapshotBase(proyectoId, Tipo));

        Assert.IsNotNull(snapshot.EstilosCelda);
        Assert.AreEqual(2, snapshot.EstilosCelda!.Count);

        var celdaDefault = snapshot.EstilosCelda.Single(c => c.Direccion.ClaveVista.Length == 0);
        Assert.AreEqual("C:1", celdaDefault.Direccion.ClaveFila);
        Assert.AreEqual("ImporteTotal", celdaDefault.Direccion.NombreColumna);
        Assert.AreEqual("#FF0000", celdaDefault.Estilo.ColorFondo);
        Assert.IsTrue(celdaDefault.Estilo.Negrita);

        var celdaVista = snapshot.EstilosCelda.Single(c => c.Direccion.ClaveVista == "VistaA");
        Assert.AreEqual("#00FF00", celdaVista.Estilo.ColorFondo);

        Assert.IsNotNull(snapshot.EstilosFila);
        Assert.AreEqual(1, snapshot.EstilosFila!.Count);
        var rol = snapshot.EstilosFila.Single();
        Assert.AreEqual(RolFilaReporte.Total, rol.RolFila);
        Assert.AreEqual("#2C3E50", rol.Estilo.ColorFondo);
        Assert.AreEqual("Arial", rol.Estilo.Fuente);
        Assert.IsNull(rol.Estilo.Negrita, "Las propiedades no informadas siguen heredando.");
    }

    [TestMethod]
    public void Build_SoloDevuelveEstilosDelTipoDeReporteDelSnapshot()
    {
        using var ctx = TestDbFactory.CreateContext();
        var proyectoId = AgregarProyecto(ctx);
        var formatos = new FormatoReporteService(ctx);

        // Overrides de otro tipo de reporte del MISMO proyecto.
        formatos.UpsertCelda(proyectoId, "APU", string.Empty, ClaveFilaReporte.Componente(1), "ImporteTotal",
            new ReportStyleOverlay(ColorFondo: "#123456"));
        formatos.UpsertEstiloFila(proyectoId, "APU", RolFilaReporte.Total,
            new ReportStyleOverlay(ColorFondo: "#654321"));

        var snapshot = new ReportStyleSnapshotBuilder(formatos).Build(SnapshotBase(proyectoId, Tipo));

        Assert.IsNull(snapshot.EstilosCelda, "Un override de APU no debe aparecer en Presupuesto.");
        Assert.IsNull(snapshot.EstilosFila, "Un estilo de APU no debe aparecer en Presupuesto.");
    }

    [TestMethod]
    public void Build_SnapshotNulo_LanzaArgumentNullException()
    {
        using var ctx = TestDbFactory.CreateContext();
        var builder = new ReportStyleSnapshotBuilder(new FormatoReporteService(ctx));

        Assert.ThrowsExactly<ArgumentNullException>(() => builder.Build(null!));
    }
}
