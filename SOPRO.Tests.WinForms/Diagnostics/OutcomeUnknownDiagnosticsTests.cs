using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SOPRO.Application.Contracts;
using SOPRO.Application.Models.Presupuesto;
using SOPRO.Application.Models.Reporting.ReportColumns;
using SOPRO.Application.Services;
using SOPRO.Core.Entities;
using SOPRO.Data.Context;
using SOPRO.Data.Factories;
using SOPRO.WinForms.Diagnostics;
using SOPRO.WinForms.Forms;
using SOPRO.WinForms.Helpers;
using SOPRO.WinForms.Undo;

namespace SOPRO.Tests.WinForms.Diagnostics;

/// <summary>
/// Emisiones de diagnóstico de resultados INCIERTOS en las operaciones atómicas de
/// la UI: programa, pegado y formato de celdas. Sólo se aceptan operaciones fijas,
/// severidad Error, correlación válida y datos estructurales: nunca mensajes, rutas,
/// stack traces ni datos de negocio. Un fallo del sink no puede alterar el flujo.
/// Ejercita formularios REALES sobre bases temporales (nunca datos del usuario).
/// </summary>
[STATestClass]
[DoNotParallelize] // Construye formularios reales y fija el proyecto global de formato.
public class OutcomeUnknownDiagnosticsTests
{
    private const string ProductVersion = "1.19.2";
    private const string TipoProgramaObra = "ProgramaObra";
    private const string TipoPresupuesto = "Presupuesto";

    // ═════════════════ Programa de Obra: resultado incierto ═════════════════

    [TestMethod]
    public void ProgramaResultadoIncierto_EmiteUnaVezYBloqueaHastaLaRecargaExitosa()
    {
        var dbPath = CrearRuta("programa_unknown");
        SOPROContext? ctx = null;
        FormProgramaObra? form = null;
        try
        {
            ctx = CrearBasePrograma(dbPath, out var proyecto);
            GenerarPrograma(dbPath, proyecto.Id);

            var sink = new InMemoryDiagnosticsSink();
            var factory = new DisposeThrowingOnceFactory(dbPath);
            form = new FormProgramaObra(ctx, proyecto, factory, sink, ProductVersion);
            Mostrar(form);
            form.Notificador.Mostrar = (_, _) => { };

            LocalizarBoton(form, "btnSincronizar").Boton.PerformClick();

            Assert.IsTrue(form.ProgramaOperacionIncierta,
                "Precondición: el comando quedó en resultado incierto.");
            Assert.AreEqual(1, sink.Count, "Se emite exactamente un evento de resultado incierto.");
            var evento = sink.Snapshot()[0];
            ValidarEvento(evento, OutcomeUnknownDiagnostics.OperationProgramaObraResultadoIncierto,
                conExcepcion: false);

            // El bloqueo y la reconciliación se conservan: «Recargar» sigue siendo la salida.
            Assert.IsTrue(form.HistorialUndo.RequiresReconciliation);
            Assert.IsTrue(form.GridPrincipal.ReadOnly);
            Assert.IsFalse(LocalizarBoton(form, "btnSincronizar").Boton.Enabled);
            Assert.IsTrue(LocalizarBoton(form, "btnRecargar").Boton.Enabled);

            LocalizarBoton(form, "btnRecargar").Boton.PerformClick();

            Assert.IsFalse(form.ProgramaOperacionIncierta, "La recarga completa libera el bloqueo.");
            Assert.IsFalse(form.HistorialUndo.RequiresReconciliation);
            Assert.IsFalse(form.GridPrincipal.ReadOnly);
            Assert.AreEqual(1, sink.Count, "Una recarga exitosa no emite diagnóstico adicional.");
        }
        finally
        {
            form?.Dispose();
            ctx?.Dispose();
            Limpiar(dbPath);
        }
    }

    [TestMethod]
    public void ProgramaReconciliacionFallida_EmiteUnaVezSinDuplicarYMantieneElBloqueo()
    {
        var dbPath = CrearRuta("programa_recon_fail");
        SOPROContext? ctx = null;
        FormProgramaObra? form = null;
        try
        {
            ctx = CrearBasePrograma(dbPath, out var proyecto);
            GenerarPrograma(dbPath, proyecto.Id);

            var sink = new InMemoryDiagnosticsSink();
            var factory = new DisposeThrowingOnceFactory(dbPath);
            form = new FormProgramaObra(ctx, proyecto, factory, sink, ProductVersion);
            Mostrar(form);
            form.Notificador.Mostrar = (_, _) => { };

            LocalizarBoton(form, "btnSincronizar").Boton.PerformClick();
            Assert.IsTrue(form.ProgramaOperacionIncierta);
            Assert.AreEqual(1, sink.Count, "El resultado incierto emite su único evento.");

            // La lectura fresca de reconciliación falla; el cierre de la operación no
            // debe emitir un segundo evento del mismo fallo.
            ctx.Database.ExecuteSqlRaw("DROP TABLE \"DependenciasActividad\"");
            LocalizarBoton(form, "btnRecargar").Boton.PerformClick();

            Assert.IsTrue(form.ProgramaOperacionIncierta, "Sin lectura completa se conserva el bloqueo.");
            Assert.IsTrue(form.HistorialUndo.RequiresReconciliation);
            Assert.IsNotNull(form.UltimoErrorLecturaFresca);

            var reconciliaciones = sink.Snapshot()
                .Where(e => e.Operation == OutcomeUnknownDiagnostics.OperationProgramaObraReconciliacionFallida)
                .ToList();
            Assert.AreEqual(1, reconciliaciones.Count,
                "La reconciliación fallida se emite una sola vez (sin duplicar por el call site).");
            ValidarEvento(reconciliaciones[0],
                OutcomeUnknownDiagnostics.OperationProgramaObraReconciliacionFallida,
                conExcepcion: true);
            Assert.AreEqual(2, sink.Count,
                "Sólo hay un evento incierto y un evento de reconciliación fallida.");
        }
        finally
        {
            form?.Dispose();
            ctx?.Dispose();
            Limpiar(dbPath);
        }
    }

    // ═════════════════ Presupuesto: pegado ═════════════════

    [TestMethod]
    public void PegadoResultadoIncierto_EmiteUnaVezSoloEnLaRamaIncierta()
    {
        var root = CrearDirectorio("pegado_unknown");
        var dbPath = Path.Combine(root, "proyecto-temporal.soproproj");
        var proyectoGlobalPrevio = LeerProyectoGlobalFormato();
        SOPROContext? ctx = null;
        FormPresupuesto? form = null;
        try
        {
            ctx = CrearContextoPresupuesto(dbPath, out var escenario);

            var sink = new InMemoryDiagnosticsSink();
            form = new FormPresupuesto(
                ctx,
                escenario.Proyecto,
                new ProjectWorkspaceService(Path.Combine(root, "sopro"), Path.Combine(root, "local")),
                sink,
                ProductVersion,
                new DisposeThrowingOnceFactory(dbPath));

            var grid = form.GridPrincipal;
            int fila = BuscarFilaPorConcepto(grid, escenario.Concepto.Id);
            grid.CurrentCell = BuscarCelda(grid, fila, "Clave");

            var outcome = form.ProcesarPegadoPresupuesto(
                "APU-002\tDescripción pegada\tkg\t7",
                _ => true);

            Assert.AreEqual(BudgetPasteOutcomeKind.OutcomeUnknown, outcome.Kind, outcome.Message);
            Assert.AreEqual(BudgetOperationStatus.OutcomeUnknown, form.UltimoEstadoPegado);

            Assert.AreEqual(1, sink.Count, "Sólo se emite el evento del pegado incierto.");
            ValidarEvento(sink.Snapshot()[0],
                OutcomeUnknownDiagnostics.OperationPegadoResultadoIncierto,
                conExcepcion: false);
            Assert.IsFalse(sink.Snapshot().Any(e =>
                    e.Operation == OutcomeUnknownDiagnostics.OperationPegadoReconciliacionFallida),
                "Un resultado incierto no intenta ni reporta una recarga de reconciliación.");
        }
        finally
        {
            form?.Dispose();
            ctx?.Dispose();
            RestaurarProyectoGlobalFormato(proyectoGlobalPrevio);
            EliminarDirectorio(root);
        }
    }

    [TestMethod]
    public void PegadoReconciliacionFallida_EmiteUnaVezYBloqueaElSiguientePegado()
    {
        var root = CrearDirectorio("pegado_recon_fail");
        var dbPath = Path.Combine(root, "proyecto-temporal.soproproj");
        var proyectoGlobalPrevio = LeerProyectoGlobalFormato();
        SOPROContext? ctx = null;
        FormPresupuesto? form = null;
        try
        {
            ctx = CrearContextoPresupuesto(dbPath, out var escenario);

            var sink = new InMemoryDiagnosticsSink();
            form = new FormPresupuesto(
                ctx,
                escenario.Proyecto,
                new ProjectWorkspaceService(Path.Combine(root, "sopro"), Path.Combine(root, "local")),
                sink,
                ProductVersion,
                factory: null);

            var grid = form.GridPrincipal;
            int fila = BuscarFilaPorConcepto(grid, escenario.Concepto.Id);
            grid.CurrentCell = BuscarCelda(grid, fila, "Clave");

            // La vista falla tras aplicar la fila confirmada y la recarga desde la base
            // también falla: el commit ocurrió, la vista quedó desincronizada.
            form.PruebaFilasAplicadasAntesDeFallo = 1;
            form.PruebaForzarFalloDeRecarga = true;

            var outcome = form.ProcesarPegadoPresupuesto(
                "APU-002\tDescripción pegada\tkg\t7",
                _ => true);

            Assert.AreEqual(BudgetPasteOutcomeKind.CommittedViewNeedsReload, outcome.Kind, outcome.Message);

            Assert.AreEqual(1, sink.Count,
                "La recarga fallida emite una sola vez; no hay evento de pegado incierto.");
            var evento = sink.Snapshot()[0];
            ValidarEvento(evento, OutcomeUnknownDiagnostics.OperationPegadoReconciliacionFallida,
                conExcepcion: true);

            var reintento = form.ProcesarPegadoPresupuesto("APU-001", _ => true);
            Assert.AreEqual(BudgetPasteOutcomeKind.Rejected, reintento.Kind,
                "La vista desincronizada sigue bloqueando pegados posteriores.");
            Assert.AreEqual(1, sink.Count, "El pegado bloqueado no emite diagnóstico adicional.");
        }
        finally
        {
            form?.Dispose();
            ctx?.Dispose();
            RestaurarProyectoGlobalFormato(proyectoGlobalPrevio);
            EliminarDirectorio(root);
        }
    }

    [TestMethod]
    public void PegadoConflictoConocido_NoEmiteDiagnosticoDeResultadoIncierto()
    {
        var root = CrearDirectorio("pegado_conflict");
        var dbPath = Path.Combine(root, "proyecto-temporal.soproproj");
        var proyectoGlobalPrevio = LeerProyectoGlobalFormato();
        SOPROContext? ctx = null;
        FormPresupuesto? form = null;
        try
        {
            ctx = CrearContextoPresupuesto(dbPath, out var escenario);

            var sink = new InMemoryDiagnosticsSink();
            form = new FormPresupuesto(
                ctx,
                escenario.Proyecto,
                new ProjectWorkspaceService(Path.Combine(root, "sopro"), Path.Combine(root, "local")),
                sink,
                ProductVersion,
                factory: null);

            var grid = form.GridPrincipal;
            int fila = BuscarFilaPorConcepto(grid, escenario.Concepto.Id);
            grid.CurrentCell = BuscarCelda(grid, fila, "Clave");

            using (var ajeno = new SOPROContext(dbPath))
            {
                var concepto = ajeno.ConceptosPresupuesto.Single(c => c.Id == escenario.Concepto.Id);
                concepto.Descripcion = "Descripción cambiada en la base";
                ajeno.SaveChanges();
            }

            var outcome = form.ProcesarPegadoPresupuesto(
                "APU-002\tDescripción pegada\tkg\t7",
                _ => true);

            Assert.AreEqual(BudgetPasteOutcomeKind.Failed, outcome.Kind);
            Assert.AreEqual(BudgetOperationStatus.Conflict, form.UltimoEstadoPegado);
            Assert.AreEqual(0, sink.Count,
                "Un conflicto conocido no es un resultado incierto: no emite diagnóstico.");
        }
        finally
        {
            form?.Dispose();
            ctx?.Dispose();
            RestaurarProyectoGlobalFormato(proyectoGlobalPrevio);
            EliminarDirectorio(root);
        }
    }

    // ═════════════════ Formato de celdas ═════════════════

    [TestMethod]
    public void FormatoCeldasResultadoIncierto_EmiteAntesDeLaExcepcionYSuspendeElUndo()
    {
        var dbPath = CrearRuta("formato_unknown");
        SOPROContext? ctx = null;
        try
        {
            ctx = CrearContextoFormato(dbPath, out int proyectoId);
            var sink = new InMemoryDiagnosticsSink();
            var manager = new UndoManager();
            var comandos = new[]
            {
                new CambioFormatoCeldaCommand(
                    string.Empty,
                    "C:1",
                    "Importe",
                    ExpectedOverlay: null,
                    ProposedOverlay: new ReportStyleOverlay(ColorFondo: "#112233")),
            };

            var ex = Assert.ThrowsExactly<FormatoCeldasPersistenceException>(() =>
                FormatoCeldasPersistence.Aplicar(
                    dbPath,
                    proyectoId,
                    TipoPresupuesto,
                    comandos,
                    manager,
                    new DisposeThrowingFactory(dbPath),
                    sink,
                    ProductVersion));

            Assert.AreEqual(BudgetOperationStatus.OutcomeUnknown, ex.Status);
            Assert.IsTrue(manager.RequiresReconciliation,
                "El resultado incierto sigue suspendiendo el historial.");
            Assert.AreEqual(1, sink.Count);
            ValidarEvento(sink.Snapshot()[0],
                OutcomeUnknownDiagnostics.OperationFormatoCeldasResultadoIncierto,
                conExcepcion: false);
        }
        finally
        {
            ctx?.Dispose();
            Limpiar(dbPath);
        }
    }

    [TestMethod]
    public void FormatoCeldasConflictoConocido_NoEmiteDiagnosticoDeResultadoIncierto()
    {
        var dbPath = CrearRuta("formato_conflict");
        SOPROContext? ctx = null;
        try
        {
            ctx = CrearContextoFormato(dbPath, out int proyectoId);

            ctx.FormatosCeldaReporte.Add(new FormatoCeldaReporte
            {
                ProyectoId = proyectoId,
                TipoReporte = TipoPresupuesto,
                ClaveVista = string.Empty,
                ClaveFila = "C:1",
                NombreColumna = "Importe",
                ColorFondo = "#999999",
            });
            ctx.SaveChanges();

            var sink = new InMemoryDiagnosticsSink();
            var manager = new UndoManager();
            var comandos = new[]
            {
                new CambioFormatoCeldaCommand(
                    string.Empty,
                    "C:1",
                    "Importe",
                    ExpectedOverlay: null,
                    ProposedOverlay: new ReportStyleOverlay(ColorFondo: "#112233")),
            };

            var ex = Assert.ThrowsExactly<FormatoCeldasPersistenceException>(() =>
                FormatoCeldasPersistence.Aplicar(
                    dbPath,
                    proyectoId,
                    TipoPresupuesto,
                    comandos,
                    manager,
                    factory: null,
                    sink,
                    ProductVersion));

            Assert.AreEqual(BudgetOperationStatus.Conflict, ex.Status);
            Assert.AreEqual(0, sink.Count,
                "Un conflicto no emite diagnóstico de resultado incierto.");
        }
        finally
        {
            ctx?.Dispose();
            Limpiar(dbPath);
        }
    }

    [TestMethod]
    public void FormatoCeldasConSinkQueLanza_NoAlteraLaExcepcionOriginalNiElBloqueo()
    {
        var dbPath = CrearRuta("formato_sink_throws");
        SOPROContext? ctx = null;
        try
        {
            ctx = CrearContextoFormato(dbPath, out int proyectoId);
            var sink = new ThrowingDiagnosticsSink();
            var manager = new UndoManager();
            var comandos = new[]
            {
                new CambioFormatoCeldaCommand(
                    string.Empty,
                    "C:1",
                    "Importe",
                    ExpectedOverlay: null,
                    ProposedOverlay: new ReportStyleOverlay(ColorFondo: "#112233")),
            };

            var ex = Assert.ThrowsExactly<FormatoCeldasPersistenceException>(() =>
                FormatoCeldasPersistence.Aplicar(
                    dbPath,
                    proyectoId,
                    TipoPresupuesto,
                    comandos,
                    manager,
                    new DisposeThrowingFactory(dbPath),
                    sink,
                    ProductVersion));

            Assert.AreEqual(BudgetOperationStatus.OutcomeUnknown, ex.Status,
                "El fallo del sink no cambia la clasificación de la persistencia.");
            Assert.IsTrue(manager.RequiresReconciliation,
                "El bloqueo por resultado incierto se conserva aunque el sink falle.");
            Assert.AreEqual(1, sink.WriteAttempts);
            Assert.AreEqual(1, sink.FlushAttempts);
        }
        finally
        {
            ctx?.Dispose();
            Limpiar(dbPath);
        }
    }

    // ═════════════════ Validación de eventos ═════════════════

    private static void ValidarEvento(
        DiagnosticsEvent evento,
        string operacion,
        bool conExcepcion)
    {
        Assert.AreEqual(DiagnosticsSeverity.Error, evento.Severity);
        Assert.AreEqual(OutcomeUnknownDiagnostics.Source, evento.Source);
        Assert.AreEqual(operacion, evento.Operation);
        Assert.AreEqual(ProductVersion, evento.ProductVersion);
        Assert.IsTrue(DiagnosticsCorrelationId.IsValid(evento.CorrelationId),
            $"Correlación inválida: '{evento.CorrelationId}'.");
        Assert.AreEqual(TimeSpan.Zero, evento.TimestampUtc.Offset, "El instante debe ser UTC.");
        Assert.AreEqual(conExcepcion, evento.ExceptionType is not null);

        foreach (var texto in TextosDelEvento(evento))
        {
            Assert.IsFalse(
                texto.Contains("secreto", StringComparison.OrdinalIgnoreCase)
                    || texto.Contains("C:\\", StringComparison.OrdinalIgnoreCase)
                    || texto.Contains("ruta", StringComparison.OrdinalIgnoreCase)
                    || texto.Contains("usuario", StringComparison.OrdinalIgnoreCase),
                $"El evento no puede contener texto libre ni rutas: '{texto}'.");
        }
    }

    private static IEnumerable<string> TextosDelEvento(DiagnosticsEvent evento)
    {
        if (!string.IsNullOrEmpty(evento.Source)) yield return evento.Source;
        if (!string.IsNullOrEmpty(evento.Operation)) yield return evento.Operation;
        if (!string.IsNullOrEmpty(evento.ProductVersion)) yield return evento.ProductVersion;
        if (!string.IsNullOrEmpty(evento.CorrelationId)) yield return evento.CorrelationId;
        if (!string.IsNullOrEmpty(evento.ExceptionType)) yield return evento.ExceptionType!;
        if (!string.IsNullOrEmpty(evento.SchemaVersion)) yield return evento.SchemaVersion!;
        if (!string.IsNullOrEmpty(evento.SchemaStage)) yield return evento.SchemaStage!;
        foreach (var inner in evento.InnerExceptionTypes)
            yield return inner;
    }

    // ═════════════════ Infraestructura de Programa de Obra ═════════════════

    private static SOPROContext CrearBasePrograma(string dbPath, out Proyecto proyecto)
    {
        var ctx = new SOPROContext(dbPath);
        ctx.Database.EnsureDeleted();
        ctx.Database.EnsureCreated();

        proyecto = new Proyecto
        {
            Nombre = "Proyecto diagnóstico programa",
            Descripcion = string.Empty,
            Ubicacion = string.Empty,
            Convocante = string.Empty,
            Contratista = string.Empty,
            ApoderadoLegal = string.Empty,
            FechaInicio = new DateTime(2026, 1, 5),
            FechaTermino = new DateTime(2026, 2, 27),
            PlazoEjecucion = 40,
            DecimalesCantidad = 2,
            DecimalesImporte = 2,
            DecimalesPorcentaje = 4,
        };
        ctx.Proyectos.Add(proyecto);
        ctx.SaveChanges();

        ctx.ConceptosPresupuesto.AddRange(
            CrearConcepto(proyecto.Id, "C-01", 10m, 100m, 1),
            CrearConcepto(proyecto.Id, "C-02", 5m, 200m, 2));
        ctx.SaveChanges();

        return ctx;
    }

    private static int GenerarPrograma(string dbPath, int proyectoId)
    {
        var result = new ProgramacionSynchronizationService().GenerateProgram(
            new ProjectDbContextFactory(),
            dbPath,
            proyectoId,
            expectedActiveProgramaId: null,
            new DateTime(2026, 1, 5),
            TipoPeriodoPrograma.Semana);

        Assert.AreEqual(BudgetOperationStatus.Committed, result.Status, result.Diagnostic);
        Assert.IsNotNull(result.ProgramaObraId);
        return result.ProgramaObraId!.Value;
    }

    private static ConceptoPresupuesto CrearConcepto(
        int proyectoId,
        string clave,
        decimal cantidad,
        decimal precio,
        int orden)
        => new()
        {
            ProyectoId = proyectoId,
            Clave = clave,
            Descripcion = $"Concepto {clave}",
            Unidad = "m2",
            EsAgrupador = false,
            Nivel = 1,
            Orden = orden,
            Cantidad = cantidad,
            CostoDirectoUnitario = precio,
            CostoDirectoTotal = cantidad * precio,
            PrecioUnitario = precio,
            ImporteTotal = cantidad * precio,
        };

    private static void Mostrar(FormProgramaObra form)
    {
        form.Show();
        System.Windows.Forms.Application.DoEvents();
    }

    private static (ToolStrip Barra, ToolStripButton Boton) LocalizarBoton(
        FormProgramaObra form,
        string nombre)
    {
        var barra = form.Controls.Find("toolPrograma", searchAllChildren: true)
            .OfType<ToolStrip>()
            .SingleOrDefault();
        Assert.IsNotNull(barra, "Debe existir la barra 'toolPrograma' en el formulario.");

        var boton = barra!.Items.Find(nombre, searchAllChildren: false)
            .OfType<ToolStripButton>()
            .SingleOrDefault();
        Assert.IsNotNull(boton, $"La barra 'toolPrograma' debe contener el botón '{nombre}'.");

        return (barra, boton!);
    }

    // ═════════════════ Infraestructura de Presupuesto ═════════════════

    private sealed class EscenarioPresupuesto
    {
        public Proyecto Proyecto { get; init; } = null!;
        public ConceptoPresupuesto Concepto { get; init; } = null!;
    }

    private static SOPROContext CrearContextoPresupuesto(
        string dbPath,
        out EscenarioPresupuesto escenario)
    {
        var ctx = new SOPROContext(dbPath);
        ctx.Database.EnsureDeleted();
        ctx.Database.EnsureCreated();

        var proyecto = new Proyecto
        {
            Nombre = "Proyecto diagnóstico pegado",
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
            PorcentajeIVA = 0m,
            DecimalesCantidad = 2,
            DecimalesImporte = 2,
            DecimalesPorcentaje = 4,
            FechaCreacion = new DateTime(2026, 1, 1),
            FechaModificacion = new DateTime(2026, 1, 1),
        };
        ctx.Proyectos.Add(proyecto);
        ctx.SaveChanges();

        var matrizOrigen = new Matriz
        {
            ProyectoId = proyecto.Id,
            Clave = "APU-001",
            Descripcion = "Concreto simple",
            Unidad = "m3",
            Tipo = TipoMatriz.APU,
            CostoDirecto = 100m,
            Notas = string.Empty,
        };
        var matrizDestino = new Matriz
        {
            ProyectoId = proyecto.Id,
            Clave = "APU-002",
            Descripcion = "Acero de refuerzo",
            Unidad = "kg",
            Tipo = TipoMatriz.APU,
            CostoDirecto = 50m,
            Notas = string.Empty,
        };
        ctx.Matrices.AddRange(matrizOrigen, matrizDestino);
        ctx.SaveChanges();

        var concepto = new ConceptoPresupuesto
        {
            ProyectoId = proyecto.Id,
            Clave = "C-001",
            Descripcion = "Concreto simple en obra",
            EsAgrupador = false,
            Nivel = 1,
            Orden = 1,
            Unidad = "m3",
            ColumnasPersonalizadasJSON = string.Empty,
            Notas = string.Empty,
            Cantidad = 10m,
            MatrizId = matrizOrigen.Id,
            CostoDirectoUnitario = 100m,
            CostoDirectoTotal = 1000m,
            PrecioUnitario = 100m,
            ImporteTotal = 1000m,
            FechaModificacion = new DateTime(2026, 1, 1),
        };
        ctx.ConceptosPresupuesto.Add(concepto);
        ctx.SaveChanges();

        escenario = new EscenarioPresupuesto
        {
            Proyecto = proyecto,
            Concepto = concepto,
        };
        return ctx;
    }

    private static int BuscarFilaPorConcepto(DataGridView grid, int conceptoId)
    {
        for (int i = 0; i < grid.Rows.Count; i++)
        {
            if (grid.Rows[i].Tag is ConceptoPresupuesto concepto && concepto.Id == conceptoId)
                return i;
        }

        return -1;
    }

    private static DataGridViewCell? BuscarCelda(
        DataGridView grid,
        int rowIndex,
        string nombreInterno)
    {
        if (rowIndex < 0 || rowIndex >= grid.Rows.Count) return null;

        foreach (DataGridViewColumn col in grid.Columns)
        {
            if (col.Tag is ColumnaPersonalizada definicion
                && string.Equals(definicion.NombreInterno, nombreInterno, StringComparison.OrdinalIgnoreCase))
            {
                return grid.Rows[rowIndex].Cells[col.Index];
            }
        }

        return null;
    }

    // ═════════════════ Infraestructura de formato ═════════════════

    private static SOPROContext CrearContextoFormato(string dbPath, out int proyectoId)
    {
        var ctx = new SOPROContext(dbPath);
        ctx.Database.EnsureDeleted();
        ctx.Database.EnsureCreated();

        var proyecto = new Proyecto
        {
            Nombre = "Proyecto diagnóstico formato",
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

        proyectoId = proyecto.Id;
        return ctx;
    }

    // ═════════════════ Sinks y fábricas de prueba ═════════════════

    private sealed class DisposeThrowingFactory : IProjectDbContextFactory
    {
        private readonly string _dbPath;

        public DisposeThrowingFactory(string dbPath) => _dbPath = dbPath;

        public SOPROContext Create(string databasePath) => new DisposeThrowingContext(_dbPath);

        public Task<SOPROContext> CreateAsync(
            string databasePath,
            CancellationToken cancellationToken = default)
            => Task.FromResult(Create(databasePath));
    }

    private sealed class DisposeThrowingOnceFactory : IProjectDbContextFactory
    {
        private readonly string _dbPath;
        private int _disposeCount;

        public DisposeThrowingOnceFactory(string dbPath) => _dbPath = dbPath;

        public SOPROContext Create(string databasePath)
            => new DisposeThrowingContext(
                _dbPath,
                () => Interlocked.Increment(ref _disposeCount) == 2);

        public Task<SOPROContext> CreateAsync(
            string databasePath,
            CancellationToken cancellationToken = default)
            => Task.FromResult(Create(databasePath));
    }

    private sealed class DisposeThrowingContext : SOPROContext
    {
        private readonly Func<bool> _debeFallar;

        public DisposeThrowingContext(string dbPath, Func<bool>? debeFallar = null)
            : base(dbPath)
        {
            _debeFallar = debeFallar ?? (() => true);
        }

        public override void Dispose()
        {
            base.Dispose();
            if (_debeFallar())
                throw new InvalidOperationException(
                    "secreto C:\\ruta\\usuario.txt dispose_simulado");
        }
    }

    private sealed class ThrowingDiagnosticsSink : IDiagnosticsSink
    {
        public int WriteAttempts { get; private set; }

        public int FlushAttempts { get; private set; }

        public void Write(DiagnosticsEvent diagnosticsEvent)
        {
            WriteAttempts++;
            throw new InvalidOperationException("secreto C:\\ruta\\usuario.txt sink_write");
        }

        public void Flush()
        {
            FlushAttempts++;
            throw new InvalidOperationException("secreto C:\\ruta\\usuario.txt sink_flush");
        }
    }

    // ═════════════════ Estado global de FormatoHelper ═════════════════

    private static Proyecto? LeerProyectoGlobalFormato()
        => (Proyecto?)CampoProyectoGlobal().GetValue(null);

    private static void RestaurarProyectoGlobalFormato(Proyecto? previo)
        => CampoProyectoGlobal().SetValue(null, previo);

    private static System.Reflection.FieldInfo CampoProyectoGlobal()
        => typeof(FormatoHelper).GetField(
               "_proyectoActual",
               System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)
           ?? throw new InvalidOperationException(
               "No se encontró el campo FormatoHelper._proyectoActual.");

    // ═════════════════ Temporales ═════════════════

    private static string CrearRuta(string sufijo)
        => Path.Combine(
            Path.GetTempPath(),
            $"sopro_diag_{sufijo}_{Guid.NewGuid():N}.db");

    private static string CrearDirectorio(string sufijo)
    {
        var root = Path.Combine(
            Path.GetTempPath(),
            $"sopro_diag_{sufijo}_{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        return root;
    }

    private static void Limpiar(string dbPath)
    {
        foreach (var sufijo in new[] { string.Empty, "-wal", "-shm" })
        {
            var archivo = dbPath + sufijo;
            if (!File.Exists(archivo)) continue;
            try { File.Delete(archivo); }
            catch (IOException) { }
        }
    }

    private static void EliminarDirectorio(string root)
    {
        try
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }
}
