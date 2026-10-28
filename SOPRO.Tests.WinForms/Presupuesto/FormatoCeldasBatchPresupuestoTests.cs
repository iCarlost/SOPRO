using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SOPRO.Application.Models.Presupuesto;
using SOPRO.Application.Models.Reporting.ReportColumns;
using SOPRO.Application.Services.Reporting;
using SOPRO.Core.Entities;
using SOPRO.Data.Context;
using SOPRO.Data.Factories;
using SOPRO.Tests.WinForms.TestInfrastructure;
using SOPRO.WinForms.Forms;
using SOPRO.WinForms.Helpers;
using SOPRO.WinForms.Undo;

namespace SOPRO.Tests.WinForms.Presupuesto;

/// <summary>
/// Flujo de persistencia del formato por celda tal como lo ejecuta
/// <see cref="FormPresupuesto"/>: lote con base esperada, UNA acción compuesta de
/// undo, fallo revertido que conserva overlay y stack, y resultado incierto que
/// bloquea hasta una lectura fresca de overlays.
/// </summary>
[TestClass]
public class FormatoCeldasBatchPresupuestoTests
{
    private const string Tipo = "Presupuesto";
    private const string Vista = "";

    // ═════════════════════════════ Escenario ═════════════════════════════

    private static SOPROContext CrearContexto()
    {
        var dbPath = Path.Combine(Path.GetTempPath(), $"sopro_fmt_batch_{Guid.NewGuid():N}.db");
        var ctx = new SOPROContext(dbPath);
        ctx.Database.EnsureDeleted();
        ctx.Database.EnsureCreated();
        return ctx;
    }

    private static int AgregarProyecto(SOPROContext ctx)
    {
        var proyecto = new Proyecto
        {
            Nombre = "Proyecto formato lote",
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

    private static List<CambioFormatoSeleccion> Cambios(params (string fila, string columna, ReportStyleOverlay overlay)[] celdas)
        => celdas
            .Select(c => new CambioFormatoSeleccion(new ReportCellAddress(Vista, c.fila, c.columna), null, c.overlay))
            .ToList();

    // ═════════════ Èxito: do / undo / redo como UNA acción ═════════════

    [TestMethod]
    public void FlushExitoso_DoUndoRedo_EsUnaSolaAccionYRevierteTodoElLote()
    {
        using var ctx = CrearContexto();
        var proyectoId = AgregarProyecto(ctx);
        var harness = new FormatoPresupuestoHarness(ctx, proyectoId);
        var overlay = new ReportStyleOverlay(Negrita: true, ColorFondo: "#112233");

        var cambios = Cambios(
            ("C:1", "Importe", overlay),
            ("C:2", "Importe", overlay));

        harness.EjecutarLote(cambios);

        Assert.AreEqual(1, harness.Manager.UndoCount, "El lote completo es UNA sola acción.");
        Assert.AreEqual(2, ContarFilas(ctx, proyectoId));
        Assert.AreEqual(overlay, harness.Overlays[new ReportCellAddress(Vista, "C:1", "Importe")]);

        harness.Undo();

        Assert.AreEqual(0, ContarFilas(ctx, proyectoId), "Undo revierte TODO el lote.");
        Assert.IsFalse(harness.Overlays.ContainsKey(new ReportCellAddress(Vista, "C:1", "Importe")));
        Assert.AreEqual(1, harness.Manager.RedoCount);

        harness.Redo();

        Assert.AreEqual(2, ContarFilas(ctx, proyectoId), "Redo reaplica TODO el lote.");
        Assert.AreEqual(overlay, harness.Overlays[new ReportCellAddress(Vista, "C:1", "Importe")]);
        Assert.AreEqual(1, harness.Manager.UndoCount);
    }

    [TestMethod]
    public void FlushConBaseVieja_RevierteOverlayYNoApilaLaAccion()
    {
        using var ctx = CrearContexto();
        var proyectoId = AgregarProyecto(ctx);

        // La base cambió por otra vía tras cargar la vista (Antes = null en la vista).
        ctx.FormatosCeldaReporte.Add(new FormatoCeldaReporte
        {
            ProyectoId = proyectoId,
            TipoReporte = Tipo,
            ClaveVista = Vista,
            ClaveFila = "C:1",
            NombreColumna = "Importe",
            ColorFondo = "#999999",
        });
        ctx.SaveChanges();

        var harness = new FormatoPresupuestoHarness(ctx, proyectoId);
        var cambios = Cambios(("C:1", "Importe", new ReportStyleOverlay(ColorFondo: "#112233")));

        var ex = Assert.ThrowsExactly<FormatoCeldasPersistenceException>(() => harness.EjecutarLote(cambios));

        Assert.AreEqual(BudgetOperationStatus.Conflict, ex.Status);
        Assert.AreEqual(0, harness.Manager.UndoCount, "Un conflicto no apila la acción.");
        Assert.IsFalse(harness.Overlays.ContainsKey(new ReportCellAddress(Vista, "C:1", "Importe")),
            "La vista vuelve al estado anterior; no se repinta un éxito inexistente.");
        Assert.AreEqual("#999999", ctx.FormatosCeldaReporte.AsNoTracking().Single().ColorFondo);
    }

    // ═════════════ Resultado incierto: bloqueo hasta reconciliar ═════════════

    [TestMethod]
    public void ResultadoIncierto_BloqueaHastaRecargaFrescaYReconciliaAntesDeReintentar()
    {
        using var ctx = CrearContexto();
        var proyectoId = AgregarProyecto(ctx);
        var harness = new FormatoPresupuestoHarness(ctx, proyectoId, new DisposeThrowingFactory(ctx.DatabasePath));

        var overlay = new ReportStyleOverlay(ColorFondo: "#ABCDEF");
        var cambios = Cambios(("C:1", "Importe", overlay));

        var ex = Assert.ThrowsExactly<FormatoCeldasPersistenceException>(() => harness.EjecutarLote(cambios));

        Assert.AreEqual(BudgetOperationStatus.OutcomeUnknown, ex.Status);
        Assert.IsTrue(harness.Manager.RequiresReconciliation);
        Assert.AreEqual(0, harness.Manager.UndoCount, "La acción incierta no entra al stack.");
        Assert.IsFalse(harness.Manager.CanUndo);
        Assert.IsFalse(harness.Manager.CanRedo);
        Assert.AreEqual(overlay, harness.Overlays[new ReportCellAddress(Vista, "C:1", "Importe")],
            "Sin resultado confirmado no se revierte ni se repinta: la vista conserva su estado.");
        Assert.ThrowsExactly<InvalidOperationException>(() =>
            harness.Manager.Execute(new CompositeUndoableAction(
                "otra", new List<UndoChange> { new(null, overlay) }, _ => { }, _ => { })));

        // El commit sí ocurrió antes del fallo de cierre: la recarga fresca lo ve.
        Assert.AreEqual(1, ContarFilas(ctx, proyectoId));

        harness.RecargarOverlaysDesdeBase();

        Assert.IsFalse(harness.Manager.RequiresReconciliation, "La lectura fresca libera el bloqueo.");
        Assert.AreEqual(0, harness.Manager.UndoCount, "El historial obsoleto se descarta explícitamente.");
        Assert.AreEqual(0, harness.Manager.RedoCount);
        Assert.AreEqual(overlay, harness.Overlays[new ReportCellAddress(Vista, "C:1", "Importe")],
            "La caché se reemplaza con lo realmente persistido.");

        // Reintentar el comando viejo (base esperada null) ya no es válido: conflicto.
        Assert.ThrowsExactly<FormatoCeldasPersistenceException>(() => harness.EjecutarLote(cambios));

        // El reintento correcto parte de la base reconciliada.
        var reintento = new List<CambioFormatoSeleccion>
        {
            new(new ReportCellAddress(Vista, "C:1", "Importe"), overlay,
                new ReportStyleOverlay(ColorFondo: "#123456")),
        };
        harness.EjecutarLote(reintento);

        Assert.AreEqual("#123456", ctx.FormatosCeldaReporte.AsNoTracking().Single().ColorFondo);
        Assert.AreEqual(1, harness.Manager.UndoCount);
    }

    private static int ContarFilas(SOPROContext ctx, int proyectoId)
    {
        ctx.ChangeTracker.Clear();
        return ctx.FormatosCeldaReporte.AsNoTracking().Count(f => f.ProyectoId == proyectoId);
    }

    // ═════════════════════════════ Infraestructura ═════════════════════════════

    /// <summary>
    /// Réplica mínima del flujo de <c>FlushFormatoPendiente</c> de un formulario:
    /// overlay en memoria, acción compuesta, persistencia con el comando real y
    /// recarga fresca tras un resultado incierto.
    /// </summary>
    private sealed class FormatoPresupuestoHarness
    {
        private readonly SOPROContext _context;
        private readonly int _proyectoId;
        private readonly IProjectDbContextFactory? _factory;

        public FormatoPresupuestoHarness(
            SOPROContext context,
            int proyectoId,
            IProjectDbContextFactory? factory = null)
        {
            _context = context;
            _proyectoId = proyectoId;
            _factory = factory;
            Overlays = new Dictionary<ReportCellAddress, ReportStyleOverlay>(
                new FormatoReporteService(context).CargarCeldas(proyectoId, Tipo));
        }

        public Dictionary<ReportCellAddress, ReportStyleOverlay> Overlays { get; }

        public UndoManager Manager { get; } = new();

        public void EjecutarLote(IReadOnlyList<CambioFormatoSeleccion> cambios)
        {
            // El ribbon ya pintó el estado "después" en la caché antes del flush.
            foreach (var cambio in cambios)
                Establecer(cambio.Direccion, cambio.Despues);

            var accion = new CompositeUndoableAction(
                "Formato de lote",
                cambios.Select(c => new UndoChange(c.Antes, c.Despues)).ToList(),
                _ => Aplicar(cambios, aplicarAntes: true),
                _ => Aplicar(cambios, aplicarAntes: false));

            try
            {
                Manager.Execute(accion);
            }
            catch (FormatoCeldasPersistenceException ex)
            {
                // Mismo comportamiento que el formulario: un fallo confirmado
                // devuelve la vista al estado anterior; uno incierto NO se revierte.
                if (ex.Status != BudgetOperationStatus.OutcomeUnknown)
                    RestaurarAntes(cambios);
                throw;
            }
        }

        public void Undo()
        {
            try
            {
                Manager.Undo();
            }
            catch (FormatoCeldasPersistenceException)
            {
                throw;
            }
        }

        public void Redo() => Manager.Redo();

        public void RecargarOverlaysDesdeBase()
        {
            Overlays.Clear();
            foreach (var par in new FormatoReporteService(_context).CargarCeldas(_proyectoId, Tipo))
                Overlays[par.Key] = par.Value;

            FormatoCeldasPersistence.ReconciliarSiCorresponde(Manager);
        }

        private void Aplicar(IReadOnlyList<CambioFormatoSeleccion> cambios, bool aplicarAntes)
        {
            FormatoCeldasPersistence.Aplicar(
                _context.DatabasePath,
                _proyectoId,
                Tipo,
                FormPresupuesto.ConstruirLoteCambios(cambios, aplicarAntes),
                Manager,
                _factory);

            foreach (var cambio in cambios)
                Establecer(cambio.Direccion, aplicarAntes ? cambio.Antes : cambio.Despues);
        }

        private void RestaurarAntes(IReadOnlyList<CambioFormatoSeleccion> cambios)
        {
            foreach (var cambio in cambios)
                Establecer(cambio.Direccion, cambio.Antes);
        }

        private void Establecer(ReportCellAddress direccion, ReportStyleOverlay? overlay)
        {
            if (overlay is null) Overlays.Remove(direccion);
            else Overlays[direccion] = overlay;
        }
    }

    /// <summary>
    /// Simula el commit ya realizado seguido de un fallo al cerrar el contexto:
    /// el ejecutor debe clasificarlo como <see cref="BudgetOperationStatus.OutcomeUnknown"/>.
    /// </summary>
    private sealed class DisposeThrowingFactory : IProjectDbContextFactory
    {
        private readonly string _dbPath;
        private bool _failed;

        public DisposeThrowingFactory(string dbPath) => _dbPath = dbPath;

        public SOPROContext Create(string databasePath)
            => new DisposeThrowingContext(_dbPath, () =>
            {
                if (_failed) return false;
                _failed = true;
                return true;
            });

        public Task<SOPROContext> CreateAsync(string databasePath, CancellationToken cancellationToken = default)
            => Task.FromResult(Create(databasePath));
    }

    private sealed class DisposeThrowingContext : SOPROContext
    {
        private readonly Func<bool> _debeFallar;

        public DisposeThrowingContext(string dbPath, Func<bool> debeFallar)
            : base(dbPath)
        {
            _debeFallar = debeFallar;
        }

        public override void Dispose()
        {
            base.Dispose();
            if (_debeFallar()) throw new InvalidOperationException("dispose_simulado");
        }
    }
}
