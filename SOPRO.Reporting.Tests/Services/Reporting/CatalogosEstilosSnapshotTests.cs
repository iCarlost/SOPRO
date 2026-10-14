using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SOPRO.Application.Models.Reporting.ReportColumns;
using SOPRO.Application.Services;
using SOPRO.Application.Services.Reporting;
using SOPRO.Application.UseCases.Reporting;
using SOPRO.Core.Entities;
using SOPRO.Data.Context;
using SOPRO.Reporting.Formatting;

namespace SOPRO.Reporting.Tests.Services.Reporting;

/// <summary>
/// Paso 6.3 — contrato neutral de los CATÁLOGOS (Materiales, Mano de Obra,
/// Herramientas, Maquinaria): claves estables <c>INS:{Id}</c>, vista comodín,
/// composición de estilos (<c>ConEstilos</c>) y la cascada del resolver
/// (override de celda &gt; estilo de rol &gt; columna). Garantiza además que SIN
/// overrides el formato por COLUMNA existente sigue resolviéndose intacto.
/// </summary>
[TestClass]
public class CatalogosEstilosSnapshotTests
{
    private const int Proyecto = 11;

    private static SOPROContext CrearContexto()
    {
        var dbPath = Path.Combine(Path.GetTempPath(), $"sopro_cat_snap_{Guid.NewGuid():N}.db");
        var ctx = new SOPROContext(dbPath);
        ctx.Database.EnsureDeleted();
        ctx.Database.EnsureCreated();
        return ctx;
    }

    private static int AgregarProyecto(SOPROContext ctx)
    {
        var proyecto = new Proyecto
        {
            Nombre = "Proyecto catálogos estilos",
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

    // ─────────────────────────── Claves estables ───────────────────────────

    [TestMethod]
    public void ClaveFila_Insumo_UsaPrefijoINS()
        => Assert.AreEqual("INS:42", ClaveFilaReporte.Insumo(42));

    [TestMethod]
    public void ClaveVista_EsLaVistaComodinEnLosCuatroCatalogos()
    {
        Assert.AreEqual(string.Empty, MaterialesReportSnapshotBuilder.ClaveVista);
        Assert.AreEqual(string.Empty, ManoObraReportSnapshotBuilder.ClaveVista);
        Assert.AreEqual(string.Empty, HerramientasReportSnapshotBuilder.ClaveVista);
        Assert.AreEqual(string.Empty, CostoHorarioReportSnapshotBuilder.ClaveVista);
    }

    // ─────────────────────────── Snapshots sin overrides ───────────────────────────

    [TestMethod]
    public void SnapshotsSinOverrides_NoTienenEstilos()
    {
        var material = MaterialCatalogExportResolver.BuildSnapshot(Proyecto, "Materiales", columnas: null);
        var manoObra = ManoObraCatalogExportResolver.BuildSnapshot(Proyecto, "Mano de Obra", columnas: null);
        var herramientas = HerramientasCatalogExportResolver.BuildSnapshot(Proyecto, "Herramientas", columnas: null);
        var maquinaria = new CostoHorarioReportSnapshotBuilder().Build(Proyecto, "Maquinaria", columnas: null);

        foreach (var snapshot in new[] { material, manoObra, herramientas, maquinaria })
        {
            Assert.IsNull(snapshot.EstilosCelda);
            Assert.IsNull(snapshot.EstilosFila);
        }
    }

    [TestMethod]
    public void TipoReporte_EsElEstablePorCatalogo()
    {
        Assert.AreEqual("Materiales", MaterialesReportSnapshotBuilder.TipoReporte);
        Assert.AreEqual("ManoDeObra", ManoObraReportSnapshotBuilder.TipoReporte);
        Assert.AreEqual("Herramientas", HerramientasReportSnapshotBuilder.TipoReporte);
        Assert.AreEqual("Maquinaria", CostoHorarioReportSnapshotBuilder.TipoReporte);
    }

    // ─────────────────────────── ConEstilos ───────────────────────────

    [TestMethod]
    public void ConEstilos_SinPersistencia_DejaLosEstilosNulos()
    {
        using var ctx = CrearContexto();
        var service = new FormatoReporteService(ctx);
        var snapshot = MaterialCatalogExportResolver.BuildSnapshot(Proyecto, "Materiales", columnas: null);

        var conEstilos = MaterialesReportSnapshotBuilder.ConEstilos(snapshot, service);

        Assert.IsNull(conEstilos.EstilosCelda);
        Assert.IsNull(conEstilos.EstilosFila);
    }

    [TestMethod]
    public void ConEstilos_CargaLosOverridesPersistidos()
    {
        using var ctx = CrearContexto();
        var proyectoId = AgregarProyecto(ctx);
        var service = new FormatoReporteService(ctx);
        service.UpsertCelda(proyectoId, "Herramientas", string.Empty, "INS:7", "PrecioUnitario",
            new ReportStyleOverlay(ColorFondo: "#ABCDEF"));

        var snapshot = HerramientasCatalogExportResolver.BuildSnapshot(proyectoId, "Herramientas", columnas: null);
        var conEstilos = HerramientasReportSnapshotBuilder.ConEstilos(snapshot, service);

        Assert.IsNotNull(conEstilos.EstilosCelda);
        Assert.AreEqual(1, conEstilos.EstilosCelda!.Count);
        var direccion = new ReportCellAddress(string.Empty, "INS:7", "PrecioUnitario");
        Assert.AreEqual("#ABCDEF", conEstilos.EstilosCelda.First(o => o.Direccion == direccion).Estilo.ColorFondo);
    }

    // ─────────────────────────── Precedencia del resolver ───────────────────────────

    [TestMethod]
    public void Resolver_SinOverrides_ConservaElFormatoDeColumna()
    {
        var snapshot = MaterialCatalogExportResolver.BuildSnapshot(Proyecto, "Materiales", columnas: null);
        var precio = snapshot.Columnas.Single(c => c.Identificador == "PrecioUnitario");

        var resultado = ReportCellStyleResolver.Resolve(
            snapshot, MaterialesReportSnapshotBuilder.ClaveVista, "INS:1", "PrecioUnitario");

        Assert.AreEqual(precio.EstiloContenido.Fuente, resultado.Fuente, "Sin overrides se hereda la columna.");
        Assert.AreEqual(precio.EstiloContenido.ColorFondo, resultado.ColorFondo);
        Assert.AreEqual(precio.Alineacion, resultado.AlineacionHorizontal);
        Assert.AreEqual(precio.FormatoNumerico, resultado.FormatoNumerico);
    }

    [TestMethod]
    public void Resolver_OverrideDeCelda_GanaSobreLaColumna()
    {
        var snapshot = MaterialCatalogExportResolver.BuildSnapshot(Proyecto, "Materiales", columnas: null) with
        {
            EstilosCelda = new[]
            {
                new ReportCellStyleOverride(
                    new ReportCellAddress(MaterialesReportSnapshotBuilder.ClaveVista, "INS:9", "PrecioUnitario"),
                    new ReportStyleOverlay(ColorFondo: "#FF00AA", Negrita: true)),
            },
        };

        var resultado = ReportCellStyleResolver.Resolve(
            snapshot, MaterialesReportSnapshotBuilder.ClaveVista, "INS:9", "PrecioUnitario");

        Assert.AreEqual("#FF00AA", resultado.ColorFondo, "La celda tiene la máxima precedencia.");
        Assert.AreEqual((bool?)true, resultado.Negrita);
    }

    // ─────────────────────────── OverrideEfectivo (delta) ───────────────────────────

    [TestMethod]
    public void OverrideEfectivo_SinEstilos_EsVacio()
    {
        var snapshot = ManoObraCatalogExportResolver.BuildSnapshot(Proyecto, "Mano de Obra", columnas: null);

        var efectivo = ReportCellOverlayResolver.OverrideEfectivo(
            snapshot, ReportCellOverlayResolver.SinEstilos(snapshot),
            ManoObraReportSnapshotBuilder.ClaveVista, "INS:1", "SalarioBase");

        Assert.IsNull(efectivo.Fuente);
        Assert.IsNull(efectivo.TamanoFuente);
        Assert.IsNull(efectivo.Negrita);
        Assert.IsNull(efectivo.Cursiva);
        Assert.IsNull(efectivo.ColorFuente);
        Assert.IsNull(efectivo.ColorFondo);
        Assert.IsNull(efectivo.AlineacionHorizontal);
        Assert.IsNull(efectivo.AlineacionVertical);
        Assert.IsNull(efectivo.Wrap);
        Assert.IsNull(efectivo.FormatoNumerico);
    }

    [TestMethod]
    public void OverrideEfectivo_ConOverrideDeCelda_AislaSoloLoQuePisa()
    {
        var snapshot = HerramientasCatalogExportResolver.BuildSnapshot(Proyecto, "Herramientas", columnas: null) with
        {
            EstilosCelda = new[]
            {
                new ReportCellStyleOverride(
                    new ReportCellAddress(HerramientasReportSnapshotBuilder.ClaveVista, "INS:3", "PrecioUnitario"),
                    new ReportStyleOverlay(ColorFondo: "#010203", Negrita: true, AlineacionHorizontal: ReportTextAlignment.Centro)),
            },
        };

        var efectivo = ReportCellOverlayResolver.OverrideEfectivo(
            snapshot, ReportCellOverlayResolver.SinEstilos(snapshot),
            HerramientasReportSnapshotBuilder.ClaveVista, "INS:3", "PrecioUnitario");

        Assert.AreEqual("#010203", efectivo.ColorFondo);
        Assert.AreEqual((bool?)true, efectivo.Negrita);
        Assert.AreEqual(ReportTextAlignment.Centro, efectivo.AlineacionHorizontal);
        // Propiedades no pisadas por el override: heredan la columna (delta nulo).
        Assert.IsNull(efectivo.Fuente);
        Assert.IsNull(efectivo.FormatoNumerico, "El formato numérico de importe lo conserva la columna.");
    }
}
