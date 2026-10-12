using Microsoft.VisualStudio.TestTools.UnitTesting;
using SOPRO.Application.Models.Reporting.ReportColumns;
using SOPRO.Application.Services.Reporting;
using SOPRO.Core.Entities;
using SOPRO.Data.Context;
using SOPRO.Tests.TestInfrastructure;

namespace SOPRO.Tests.Services.Reportes;

/// <summary>
/// Verificación del <see cref="FormatoReporteService"/>: upsert PARCIAL de overrides
/// de celda y de estilos semánticos por rol, borrado cuando el override queda
/// vacío, carga masiva filtrada por (ProyectoId, TipoReporte) y atomicidad de los
/// lotes multi-fila (rollback ante fallo intermedio).
/// </summary>
[TestClass]
public class FormatoReporteServiceTests
{
    private const string Tipo = "Presupuesto";

    // ─────────────────────────── Escenario ───────────────────────────

    private static Proyecto CrearProyecto(string nombre) => new()
    {
        Nombre = nombre,
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

    private static int AgregarProyecto(SOPROContext ctx, string nombre = "Proyecto")
    {
        var proyecto = CrearProyecto(nombre);
        ctx.Proyectos.Add(proyecto);
        ctx.SaveChanges();
        return proyecto.Id;
    }

    // ─────────────────────── Upsert parcial (celda) ───────────────────────

    [TestMethod]
    public void UpsertCelda_Parcial_NoPisaValoresPrevios()
    {
        using var ctx = TestDbFactory.CreateContext();
        var proyectoId = AgregarProyecto(ctx);
        var service = new FormatoReporteService(ctx);
        var fila = ClaveFilaReporte.Concepto(10);

        var primero = service.UpsertCelda(proyectoId, Tipo, string.Empty, fila, "ImporteTotal",
            new ReportStyleOverlay(Negrita: true, ColorFondo: "#112233"));

        Assert.IsNotNull(primero);
        Assert.AreEqual("#112233", primero!.ColorFondo);
        Assert.IsTrue(primero.Negrita);

        // Segundo upsert: sólo cambia ColorFondo; Negrita null = no tocar.
        var segundo = service.UpsertCelda(proyectoId, Tipo, string.Empty, fila, "ImporteTotal",
            new ReportStyleOverlay(ColorFondo: "#445566"));

        Assert.IsNotNull(segundo);
        Assert.AreEqual("#445566", segundo!.ColorFondo, "ColorFondo debe actualizarse.");
        Assert.IsTrue(segundo.Negrita, "Negrita null NO debe pisar el valor previo.");
        Assert.IsNull(segundo.Fuente, "Las propiedades no informadas siguen heredando (null).");
        Assert.AreEqual(1, ctx.FormatosCeldaReporte.Count(f => f.ProyectoId == proyectoId),
            "El upsert no debe duplicar la fila.");
    }

    [TestMethod]
    public void UpsertCelda_MapeaAlineacionesYLasRecupera()
    {
        using var ctx = TestDbFactory.CreateContext();
        var proyectoId = AgregarProyecto(ctx);
        var service = new FormatoReporteService(ctx);

        service.UpsertCelda(proyectoId, Tipo, string.Empty, "C:1", "ImporteTotal",
            new ReportStyleOverlay(
                AlineacionHorizontal: ReportTextAlignment.Derecha,
                AlineacionVertical: ReportVerticalAlignment.Medio,
                TamanoFuente: 11f,
                Wrap: true));

        var cargadas = service.CargarCeldas(proyectoId, Tipo);
        var overlay = cargadas[new ReportCellAddress(string.Empty, "C:1", "ImporteTotal")];

        Assert.AreEqual((ReportTextAlignment?)ReportTextAlignment.Derecha, overlay.AlineacionHorizontal);
        Assert.AreEqual((ReportVerticalAlignment?)ReportVerticalAlignment.Medio, overlay.AlineacionVertical);
        Assert.AreEqual(11f, overlay.TamanoFuente);
        Assert.IsTrue(overlay.Wrap);
    }

    // ─────────────────── Override vacío → borra la fila ───────────────────

    [TestMethod]
    public void UpsertCelda_OverlayVacio_BorraLaFilaExistente()
    {
        using var ctx = TestDbFactory.CreateContext();
        var proyectoId = AgregarProyecto(ctx);
        var service = new FormatoReporteService(ctx);

        service.UpsertCelda(proyectoId, Tipo, string.Empty, "C:5", "ImporteTotal",
            new ReportStyleOverlay(ColorFondo: "#000000"));
        Assert.AreEqual(1, ctx.FormatosCeldaReporte.Count());

        // Un overlay con TODAS las propiedades null deja la fila sin estilo → se borra.
        var resultado = service.UpsertCelda(proyectoId, Tipo, string.Empty, "C:5", "ImporteTotal",
            new ReportStyleOverlay());

        Assert.IsNull(resultado);
        Assert.AreEqual(0, ctx.FormatosCeldaReporte.Count(), "Un override vacío no debe persistirse.");
    }

    [TestMethod]
    public void UpsertCelda_OverlayVacioEnCeldaInexistente_NoCreaNada()
    {
        using var ctx = TestDbFactory.CreateContext();
        var proyectoId = AgregarProyecto(ctx);
        var service = new FormatoReporteService(ctx);

        var resultado = service.UpsertCelda(proyectoId, Tipo, string.Empty, "C:9", "Clave",
            new ReportStyleOverlay());

        Assert.IsNull(resultado);
        Assert.AreEqual(0, ctx.FormatosCeldaReporte.Count());
    }

    [TestMethod]
    public void EliminarCelda_QuitaElOverrideYDevuelveSiExistia()
    {
        using var ctx = TestDbFactory.CreateContext();
        var proyectoId = AgregarProyecto(ctx);
        var service = new FormatoReporteService(ctx);

        service.UpsertCelda(proyectoId, Tipo, string.Empty, "C:3", "Cantidad",
            new ReportStyleOverlay(Cursiva: true));

        Assert.IsTrue(service.EliminarCelda(proyectoId, Tipo, string.Empty, "C:3", "Cantidad"));
        Assert.IsFalse(service.EliminarCelda(proyectoId, Tipo, string.Empty, "C:3", "Cantidad"));
        Assert.AreEqual(0, ctx.FormatosCeldaReporte.Count());
    }

    // ─────────────────── Carga masiva filtrada (sin N+1) ───────────────────

    [TestMethod]
    public void CargarCeldas_SoloDevuelveElProyectoTipoPedido()
    {
        using var ctx = TestDbFactory.CreateContext();
        var proyectoA = AgregarProyecto(ctx, "A");
        var proyectoB = AgregarProyecto(ctx, "B");
        var service = new FormatoReporteService(ctx);

        // Mismo proyecto/tipo, dos vistas.
        service.UpsertCelda(proyectoA, Tipo, string.Empty, "C:1", "ImporteTotal",
            new ReportStyleOverlay(ColorFondo: "#AAAAAA"));
        service.UpsertCelda(proyectoA, Tipo, "VistaA", "C:1", "ImporteTotal",
            new ReportStyleOverlay(ColorFondo: "#BBBBBB"));
        // Otro tipo y otro proyecto: NO deben contaminar.
        service.UpsertCelda(proyectoA, "APU", string.Empty, "C:1", "ImporteTotal",
            new ReportStyleOverlay(ColorFondo: "#CCCCCC"));
        service.UpsertCelda(proyectoB, Tipo, string.Empty, "C:1", "ImporteTotal",
            new ReportStyleOverlay(ColorFondo: "#DDDDDD"));

        var celdas = service.CargarCeldas(proyectoA, Tipo);

        Assert.AreEqual(2, celdas.Count, "Sólo el proyecto y tipo pedidos (ambas vistas).");
        Assert.AreEqual("#AAAAAA", celdas[new ReportCellAddress(string.Empty, "C:1", "ImporteTotal")].ColorFondo);
        Assert.AreEqual("#BBBBBB", celdas[new ReportCellAddress("VistaA", "C:1", "ImporteTotal")].ColorFondo);
        Assert.IsFalse(celdas.ContainsKey(new ReportCellAddress(string.Empty, "C:1", "NoExiste")));
    }

    // ─────────────────── Estilos semánticos por rol ───────────────────

    [TestMethod]
    public void UpsertEstiloFila_Parcial_NoPisaValoresPreviosYFiltra()
    {
        using var ctx = TestDbFactory.CreateContext();
        var proyectoA = AgregarProyecto(ctx, "A");
        var proyectoB = AgregarProyecto(ctx, "B");
        var service = new FormatoReporteService(ctx);

        service.UpsertEstiloFila(proyectoA, Tipo, RolFilaReporte.Total,
            new ReportStyleOverlay(Negrita: true, ColorFondo: "#2C3E50"));
        // Segundo upsert: sólo ColorFuente; Negrita se conserva.
        service.UpsertEstiloFila(proyectoA, Tipo, RolFilaReporte.Total,
            new ReportStyleOverlay(ColorFuente: "#FFFFFF"));
        // Otro proyecto no contamina.
        service.UpsertEstiloFila(proyectoB, Tipo, RolFilaReporte.Total,
            new ReportStyleOverlay(ColorFondo: "#999999"));

        var estilos = service.CargarEstilosFila(proyectoA, Tipo);

        Assert.AreEqual(1, estilos.Count);
        Assert.IsTrue(estilos.ContainsKey(RolFilaReporte.Total));
        Assert.IsTrue(estilos[RolFilaReporte.Total].Negrita);
        Assert.AreEqual("#2C3E50", estilos[RolFilaReporte.Total].ColorFondo);
        Assert.AreEqual("#FFFFFF", estilos[RolFilaReporte.Total].ColorFuente);
    }

    [TestMethod]
    public void UpsertEstiloFila_OverlayVacio_BorraLaFila()
    {
        using var ctx = TestDbFactory.CreateContext();
        var proyectoId = AgregarProyecto(ctx);
        var service = new FormatoReporteService(ctx);

        service.UpsertEstiloFila(proyectoId, Tipo, RolFilaReporte.Subtotal,
            new ReportStyleOverlay(ColorFondo: "#DDDDDD"));
        Assert.AreEqual(1, ctx.ConfiguracionesEstiloFilaReporte.Count());

        var resultado = service.UpsertEstiloFila(proyectoId, Tipo, RolFilaReporte.Subtotal,
            new ReportStyleOverlay());

        Assert.IsNull(resultado);
        Assert.AreEqual(0, ctx.ConfiguracionesEstiloFilaReporte.Count(),
            "Un estilo de fila vacío no debe persistirse.");
    }

    // ─────────────────── Lote transaccional (rollback) ───────────────────

    [TestMethod]
    public void AplicarCeldas_FallaIntermedia_RevierteTodoElLote()
    {
        using var ctx = TestDbFactory.CreateContext();
        var proyectoId = AgregarProyecto(ctx);
        var service = new FormatoReporteService(ctx);

        var lote = new List<CambioFormatoCelda>();
        for (var i = 0; i < 10; i++)
        {
            lote.Add(new CambioFormatoCelda(
                string.Empty,
                ClaveFilaReporte.Concepto(i),
                "ImporteTotal",
                new ReportStyleOverlay(ColorFondo: "#FF0000")));
        }

        // El SEGUNDO cambio es inválido (clave de fila vacía) → debe fallar el lote.
        lote[1] = new CambioFormatoCelda(
            string.Empty,
            string.Empty,
            "ImporteTotal",
            new ReportStyleOverlay(ColorFondo: "#00FF00"));

        Assert.ThrowsExactly<ArgumentException>(() => service.AplicarCeldas(proyectoId, Tipo, lote));

        ctx.ChangeTracker.Clear();
        Assert.AreEqual(0, ctx.FormatosCeldaReporte.Count(f => f.ProyectoId == proyectoId),
            "El rollback debe revertir incluso los cambios ya aplicados del lote.");
    }

    [TestMethod]
    public void AplicarCeldas_LoteValido_PersisteTodos()
    {
        using var ctx = TestDbFactory.CreateContext();
        var proyectoId = AgregarProyecto(ctx);
        var service = new FormatoReporteService(ctx);

        var lote = Enumerable.Range(0, 5)
            .Select(i => new CambioFormatoCelda(
                string.Empty,
                ClaveFilaReporte.Concepto(i),
                "ImporteTotal",
                new ReportStyleOverlay(ColorFondo: "#010203")))
            .ToList();

        service.AplicarCeldas(proyectoId, Tipo, lote);

        Assert.AreEqual(5, ctx.FormatosCeldaReporte.Count(f => f.ProyectoId == proyectoId));
    }

    [TestMethod]
    public void AplicarCeldas_EstiloNull_EliminaLaCelda()
    {
        using var ctx = TestDbFactory.CreateContext();
        var proyectoId = AgregarProyecto(ctx);
        var service = new FormatoReporteService(ctx);

        service.UpsertCelda(proyectoId, Tipo, string.Empty, "C:1", "ImporteTotal",
            new ReportStyleOverlay(ColorFondo: "#123456"));

        service.AplicarCeldas(proyectoId, Tipo, new[]
        {
            new CambioFormatoCelda(string.Empty, "C:1", "ImporteTotal", Estilo: null),
        });

        Assert.AreEqual(0, ctx.FormatosCeldaReporte.Count());
    }

    [TestMethod]
    public void AplicarEstilosFila_FallaIntermedia_RevierteTodoElLote()
    {
        using var ctx = TestDbFactory.CreateContext();
        var proyectoId = AgregarProyecto(ctx);
        var service = new FormatoReporteService(ctx);

        var lote = new List<CambioEstiloFila>
        {
            new(RolFilaReporte.Total, new ReportStyleOverlay(ColorFondo: "#111111")),
            new(string.Empty, new ReportStyleOverlay(ColorFondo: "#222222")), // rol inválido
            new(RolFilaReporte.Subtotal, new ReportStyleOverlay(ColorFondo: "#333333")),
        };

        Assert.ThrowsExactly<ArgumentException>(() => service.AplicarEstilosFila(proyectoId, Tipo, lote));

        ctx.ChangeTracker.Clear();
        Assert.AreEqual(0, ctx.ConfiguracionesEstiloFilaReporte.Count(f => f.ProyectoId == proyectoId));
    }

    // ─────────────────── Validaciones de cabecera ───────────────────

    [TestMethod]
    public void Operaciones_ProyectoInvalido_Lanzan()
    {
        using var ctx = TestDbFactory.CreateContext();
        var service = new FormatoReporteService(ctx);

        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() =>
            service.CargarCeldas(0, Tipo));
        Assert.ThrowsExactly<ArgumentException>(() =>
            service.UpsertCelda(1, string.Empty, string.Empty, "C:1", "ImporteTotal",
                new ReportStyleOverlay(ColorFondo: "#000000")));
    }
}
