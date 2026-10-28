using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SOPRO.Application.Models.Presupuesto;
using SOPRO.Application.Services;
using SOPRO.Application.Services.Reporting;
using SOPRO.Core.Entities;
using SOPRO.Data.Context;
using SOPRO.Data.Factories;
using SOPRO.Tests.TestInfrastructure;
using System.Data.Common;

namespace SOPRO.Tests.Services.Programacion;

/// <summary>
/// Atomicidad de las rutas de programación migradas al ejecutor único: borrado del
/// programa + formato en un solo SaveChanges, reconstrucción con varios guardados y un
/// solo commit, sincronización destructiva y generación atómica. Todo fallo posterior a
/// una escritura debe revertir el trabajo completo y clasificarse sin tragar excepciones.
/// </summary>
[TestClass]
public class ProgramacionSynchronizationAtomicityTests
{
    private const string TipoProgramaObra = EstilosFilaPredeterminados.TipoReporteProgramaObra;
    private const string TipoPresupuesto = EstilosFilaPredeterminados.TipoReportePresupuesto;

    // ═════════════════════════════ Borrado atómico ═════════════════════════════

    [TestMethod]
    public void DeleteProgram_EliminaGrafoCalendarioYFormatoEnUnSoloSaveChanges()
    {
        var dbPath = TestDbFactory.CreateTempDbPath();
        try
        {
            var escenario = CrearEscenario(dbPath, conPrograma: true, conFormatos: true, conCalendario: true);
            var probe = new ProbeState();
            var service = new ProgramacionSynchronizationService();

            var result = service.DeleteProgram(new ProbeFactory(probe), dbPath, escenario.ProyectoId, expectedActiveProgramaId: escenario.ProgramaId);

            Assert.AreEqual(BudgetOperationStatus.Committed, result.Status, result.Diagnostic);
            Assert.IsTrue(result.Success);
            Assert.IsFalse(result.SinCambios);
            Assert.IsNull(result.ProgramaObraId);
            Assert.AreEqual(1, probe.Saves, "El borrado completo debe usar un único SaveChanges.");
            Assert.AreEqual(1, probe.Inicios);
            Assert.AreEqual(1, probe.Commits);
            Assert.AreEqual(0, probe.Rollbacks);

            using var ctx = new SOPROContext(dbPath);
            AssertEstaInexistente(ctx, escenario);
            Assert.AreEqual(1, ctx.FormatosCeldaReporte.Count(f => f.Id == escenario.FormatoPresupuestoId),
                "El formato de otro tipo de reporte no debe tocarse.");
            Assert.IsTrue(ctx.ProgramasObra.Any(p => p.ProyectoId == escenario.OtroProyectoId),
                "El programa de otro proyecto no debe tocarse.");
        }
        finally
        {
            DeleteQuietly(dbPath);
        }
    }

    [TestMethod]
    public void DeleteProgram_FallaAlBorrarElGrafo_RevierteGrafoFormatoYCalendario()
    {
        var dbPath = TestDbFactory.CreateTempDbPath();
        try
        {
            var escenario = CrearEscenario(dbPath, conPrograma: true, conFormatos: true, conCalendario: true);
            CrearTrigger(dbPath, "CREATE TRIGGER trg_fail_programa BEFORE DELETE ON ProgramasObra " +
                                 "BEGIN SELECT RAISE(ABORT, 'fallo_simulado_programa'); END;");

            var probe = new ProbeState();
            var result = new ProgramacionSynchronizationService()
                .DeleteProgram(new ProbeFactory(probe), dbPath, escenario.ProyectoId, expectedActiveProgramaId: escenario.ProgramaId);

            Assert.AreEqual(BudgetOperationStatus.Failed, result.Status, result.Diagnostic);
            Assert.IsFalse(result.IsOutcomeUnknown);
            StringAssert.Contains(result.Diagnostic, "fallo_simulado_programa");
            Assert.AreEqual(0, probe.Commits);
            Assert.AreEqual(1, probe.Rollbacks);

            using var ctx = new SOPROContext(dbPath);
            AssertExisteElGrafo(ctx, escenario);
            Assert.IsTrue(ctx.FormatosCeldaReporte.Any(f => f.Id == escenario.FormatoProgramaIds[0]),
                "El formato del programa debe sobrevivir al rollback.");
        }
        finally
        {
            DeleteQuietly(dbPath);
        }
    }

    [TestMethod]
    public void DeleteProgram_FallaAlBorrarElFormato_RevierteGrafoYFormato()
    {
        var dbPath = TestDbFactory.CreateTempDbPath();
        try
        {
            var escenario = CrearEscenario(dbPath, conPrograma: true, conFormatos: true, conCalendario: false);
            CrearTrigger(dbPath, "CREATE TRIGGER trg_fail_formato BEFORE DELETE ON FormatosCeldaReporte " +
                                 "BEGIN SELECT RAISE(ABORT, 'fallo_simulado_formato'); END;");

            var probe = new ProbeState();
            var result = new ProgramacionSynchronizationService()
                .DeleteProgram(new ProbeFactory(probe), dbPath, escenario.ProyectoId, expectedActiveProgramaId: escenario.ProgramaId, includeCalendars: false);

            Assert.AreEqual(BudgetOperationStatus.Failed, result.Status, result.Diagnostic);
            StringAssert.Contains(result.Diagnostic, "fallo_simulado_formato");
            Assert.AreEqual(0, probe.Commits);
            Assert.AreEqual(1, probe.Rollbacks);

            using var ctx = new SOPROContext(dbPath);
            AssertExisteElGrafo(ctx, escenario);
            CollectionAssert.AreEquivalent(
                escenario.FormatoProgramaIds,
                ctx.FormatosCeldaReporte.Where(f => f.ProyectoId == escenario.ProyectoId && f.TipoReporte == TipoProgramaObra)
                    .Select(f => f.Id).ToList());
        }
        finally
        {
            DeleteQuietly(dbPath);
        }
    }

    [TestMethod]
    public void DeleteProgram_SinProgramaNiFormato_EsNoOpConfirmadoDentroDeLaTransaccionSinGuardar()
    {
        var dbPath = TestDbFactory.CreateTempDbPath();
        try
        {
            int proyectoId;
            using (var ctx = TestDbFactory.CreateContextAt(dbPath))
            {
                proyectoId = CrearProyecto(ctx, "Proyecto vacío");
                ctx.SaveChanges();
            }

            var probe = new ProbeState();
            var result = new ProgramacionSynchronizationService()
                .DeleteProgram(new ProbeFactory(probe), dbPath, proyectoId, expectedActiveProgramaId: null);

            Assert.AreEqual(BudgetOperationStatus.Committed, result.Status, result.Diagnostic);
            Assert.IsTrue(result.SinCambios);
            Assert.IsFalse(result.Success, "Un no-op no debe simular un cambio que dispare reset de formato.");
            Assert.IsNull(result.ProgramaObraId);
            Assert.AreEqual(0, probe.Saves);
            Assert.AreEqual(1, probe.Inicios, "El no-op se decide dentro de la transacción, no en el stage.");
            Assert.AreEqual(1, probe.Commits);
            Assert.AreEqual(0, probe.Rollbacks);
        }
        finally
        {
            DeleteQuietly(dbPath);
        }
    }

    [TestMethod]
    public void DeleteProgram_IncludeCalendarsFalse_ConservaCalendarioYExcepciones()
    {
        var dbPath = TestDbFactory.CreateTempDbPath();
        try
        {
            var escenario = CrearEscenario(dbPath, conPrograma: true, conFormatos: false, conCalendario: true);
            var probe = new ProbeState();

            var result = new ProgramacionSynchronizationService()
                .DeleteProgram(new ProbeFactory(probe), dbPath, escenario.ProyectoId, expectedActiveProgramaId: escenario.ProgramaId, includeCalendars: false);

            Assert.AreEqual(BudgetOperationStatus.Committed, result.Status, result.Diagnostic);
            Assert.AreEqual(1, probe.Saves);

            using var ctx = new SOPROContext(dbPath);
            Assert.IsFalse(ctx.ProgramasObra.Any(p => p.Id == escenario.ProgramaId));
            Assert.IsTrue(ctx.CalendariosLaborales.Any(c => c.Id == escenario.CalendarioId),
                "Sin includeCalendars el calendario del proyecto debe conservarse.");
            Assert.IsTrue(ctx.ExcepcionesCalendario.Any(e => e.Id == escenario.ExcepcionId));
        }
        finally
        {
            DeleteQuietly(dbPath);
        }
    }

    [TestMethod]
    public void DeleteProgram_SinProgramaPeroConFormato_EliminaElFormatoYNoEsNoOp()
    {
        var dbPath = TestDbFactory.CreateTempDbPath();
        try
        {
            int proyectoId;
            int formatoId;
            using (var ctx = TestDbFactory.CreateContextAt(dbPath))
            {
                proyectoId = CrearProyecto(ctx, "Proyecto sin programa");
                var formato = AgregarFormato(ctx, proyectoId, TipoProgramaObra, "FilaTotal");
                ctx.SaveChanges();
                formatoId = formato.Id;
            }

            var probe = new ProbeState();
            var result = new ProgramacionSynchronizationService()
                .DeleteProgram(new ProbeFactory(probe), dbPath, proyectoId, expectedActiveProgramaId: null);

            Assert.AreEqual(BudgetOperationStatus.Committed, result.Status, result.Diagnostic);
            Assert.IsFalse(result.SinCambios);
            Assert.IsTrue(result.Success);
            Assert.AreEqual(1, probe.Saves);

            using var ctxVerificacion = new SOPROContext(dbPath);
            Assert.IsFalse(ctxVerificacion.FormatosCeldaReporte.Any(f => f.Id == formatoId));
        }
        finally
        {
            DeleteQuietly(dbPath);
        }
    }

    [TestMethod]
    public void DeleteProgram_SinProgramaPeroConBaseEsperadaNoNula_DevuelveConflictAntesDelNoOp()
    {
        var dbPath = TestDbFactory.CreateTempDbPath();
        try
        {
            int proyectoId;
            using (var ctx = TestDbFactory.CreateContextAt(dbPath))
            {
                proyectoId = CrearProyecto(ctx, "Proyecto vacío con base esperada");
                ctx.SaveChanges();
            }

            var probe = new ProbeState();
            var result = new ProgramacionSynchronizationService().DeleteProgram(
                new ProbeFactory(probe),
                dbPath,
                proyectoId,
                expectedActiveProgramaId: 12345);

            Assert.AreEqual(BudgetOperationStatus.Conflict, result.Status, result.Diagnostic);
            Assert.IsFalse(result.SinCambios, "El conflicto no debe confundirse con un no-op.");
            Assert.IsFalse(result.Success);
            Assert.AreEqual(0, probe.Saves);
            Assert.AreEqual(1, probe.Rollbacks);
        }
        finally
        {
            DeleteQuietly(dbPath);
        }
    }

    // ═════════════════════════ Reconstrucción atómica ═════════════════════════

    [TestMethod]
    public void DeleteProgram_ProgramaActivoReemplazadoEnOtraVentana_DevuelveConflictSinTocarGrafoNiFormato()
    {
        var dbPath = TestDbFactory.CreateTempDbPath();
        try
        {
            var escenario = CrearEscenario(dbPath, conPrograma: true, conFormatos: true, conCalendario: true);
            var reemplazoId = ReemplazarProgramaActivo(dbPath, escenario.ProyectoId);

            var probe = new ProbeState();
            var result = new ProgramacionSynchronizationService().DeleteProgram(
                new ProbeFactory(probe),
                dbPath,
                escenario.ProyectoId,
                expectedActiveProgramaId: escenario.ProgramaId);

            Assert.AreEqual(BudgetOperationStatus.Conflict, result.Status, result.Diagnostic);
            Assert.IsFalse(result.IsOutcomeUnknown);
            Assert.IsFalse(result.Success);
            Assert.IsNull(result.ProgramaObraId);
            StringAssert.Contains(result.Diagnostic, "esperado");
            Assert.AreEqual(0, probe.Saves, "El conflicto no debe escribir.");
            Assert.AreEqual(0, probe.Commits);
            Assert.AreEqual(1, probe.Rollbacks);

            using var ctx = new SOPROContext(dbPath);
            AssertExisteElGrafo(ctx, escenario);
            Assert.IsTrue(ctx.FormatosCeldaReporte.Any(f => f.Id == escenario.FormatoProgramaIds[0]));
            Assert.IsTrue(ctx.ProgramasObra.Any(p => p.Id == reemplazoId && p.Activo));
        }
        finally
        {
            DeleteQuietly(dbPath);
        }
    }

    [TestMethod]
    public void DeleteProgram_DosProgramasActivos_DevuelveConflictSinMutar()
    {
        var dbPath = TestDbFactory.CreateTempDbPath();
        try
        {
            var escenario = CrearEscenario(dbPath, conPrograma: true, conFormatos: true, conCalendario: true);
            var segundoActivoId = ActivarProgramaAdicional(dbPath, escenario.ProyectoId);

            var probe = new ProbeState();
            var result = new ProgramacionSynchronizationService().DeleteProgram(
                new ProbeFactory(probe),
                dbPath,
                escenario.ProyectoId,
                expectedActiveProgramaId: escenario.ProgramaId);

            Assert.AreEqual(BudgetOperationStatus.Conflict, result.Status, result.Diagnostic);
            Assert.IsFalse(result.Success);
            Assert.IsNull(result.ProgramaObraId);
            StringAssert.Contains(result.Diagnostic, "esperado");
            Assert.AreEqual(0, probe.Saves, "Con más de un activo no se debe borrar nada.");
            Assert.AreEqual(0, probe.Commits);
            Assert.AreEqual(1, probe.Rollbacks);

            using var ctx = new SOPROContext(dbPath);
            AssertExisteElGrafo(ctx, escenario);
            Assert.IsTrue(ctx.FormatosCeldaReporte.Any(f => f.Id == escenario.FormatoProgramaIds[0]));
            Assert.IsTrue(ctx.ProgramasObra.Any(p => p.Id == escenario.ProgramaId && p.Activo));
            Assert.IsTrue(ctx.ProgramasObra.Any(p => p.Id == segundoActivoId && p.Activo),
                "El segundo programa activo tampoco debe tocarse.");
        }
        finally
        {
            DeleteQuietly(dbPath);
        }
    }

    [TestMethod]
    public void RebuildProgram_ProgramaActivoReemplazadoEnOtraVentana_DevuelveConflictSinMutacion()
    {
        var dbPath = TestDbFactory.CreateTempDbPath();
        try
        {
            var escenario = CrearEscenario(dbPath, conPrograma: true, conFormatos: true, conCalendario: true);
            var reemplazoId = ReemplazarProgramaActivo(dbPath, escenario.ProyectoId);

            var probe = new ProbeState();
            var result = new ProgramacionSynchronizationService().RebuildProgram(
                new ProbeFactory(probe),
                dbPath,
                escenario.ProyectoId,
                expectedActiveProgramaId: escenario.ProgramaId);

            Assert.AreEqual(BudgetOperationStatus.Conflict, result.Status, result.Diagnostic);
            Assert.IsFalse(result.Success);
            Assert.AreEqual(0, probe.Saves);
            Assert.AreEqual(1, probe.Rollbacks);

            using var ctx = new SOPROContext(dbPath);
            AssertExisteElGrafo(ctx, escenario);
            CollectionAssert.AreEquivalent(
                escenario.FormatoProgramaIds,
                ctx.FormatosCeldaReporte.Where(f => f.ProyectoId == escenario.ProyectoId && f.TipoReporte == TipoProgramaObra)
                    .Select(f => f.Id).ToList());
            Assert.IsTrue(ctx.ProgramasObra.Any(p => p.Id == reemplazoId && p.Activo));
        }
        finally
        {
            DeleteQuietly(dbPath);
        }
    }

    [TestMethod]
    public void SyncFromBudget_ProgramaActivoReemplazadoEnOtraVentana_DevuelveConflictSinMutacion()
    {
        var dbPath = TestDbFactory.CreateTempDbPath();
        try
        {
            var escenario = CrearEscenarioSync(dbPath);
            var reemplazoId = ReemplazarProgramaActivo(dbPath, escenario.ProyectoId);

            var probe = new ProbeState();
            var result = new ProgramacionSynchronizationService().SyncFromBudget(
                new ProbeFactory(probe),
                dbPath,
                escenario.ProyectoId,
                expectedActiveProgramaId: escenario.ProgramaId);

            Assert.AreEqual(BudgetOperationStatus.Conflict, result.Status, result.Diagnostic);
            Assert.IsNull(result.ProgramaObraId);
            Assert.AreEqual(0, probe.Saves);
            Assert.AreEqual(1, probe.Rollbacks);

            using var ctx = new SOPROContext(dbPath);
            Assert.IsTrue(ctx.ActividadesProgramadas.Any(a => a.Id == escenario.ActividadObsoletaId),
                "El conflicto no debe eliminar actividades.");
            Assert.IsTrue(ctx.ActividadesProgramadas.Any(a => a.Id == escenario.ActividadManualId));
            Assert.IsTrue(ctx.ProgramasObra.Any(p => p.Id == reemplazoId && p.Activo));
        }
        finally
        {
            DeleteQuietly(dbPath);
        }
    }

    [TestMethod]
    public void GenerateProgram_ProgramaActivoNoEsperado_DevuelveConflictSinExito()
    {
        var dbPath = TestDbFactory.CreateTempDbPath();
        try
        {
            var escenario = CrearEscenario(dbPath, conPrograma: true, conFormatos: false, conCalendario: false);

            var probe = new ProbeState();
            var result = new ProgramacionSynchronizationService().GenerateProgram(
                new ProbeFactory(probe),
                dbPath,
                escenario.ProyectoId,
                expectedActiveProgramaId: null,
                new DateTime(2026, 1, 5),
                TipoPeriodoPrograma.Semana);

            Assert.AreEqual(BudgetOperationStatus.Conflict, result.Status, result.Diagnostic);
            Assert.IsFalse(result.Success);
            Assert.IsNull(result.ProgramaObraId);
            Assert.AreEqual(0, probe.Saves, "No debe generar sobre una base activa distinta de la esperada.");
            Assert.AreEqual(0, probe.Commits);
            Assert.AreEqual(1, probe.Rollbacks);

            using var ctx = new SOPROContext(dbPath);
            Assert.IsTrue(ctx.ProgramasObra.Any(p => p.Id == escenario.ProgramaId && p.Activo));
        }
        finally
        {
            DeleteQuietly(dbPath);
        }
    }

    [TestMethod]
    public void GenerateProgram_ProgramaEsperadoYaExiste_EsNoOpConfirmadoSinEscribir()
    {
        var dbPath = TestDbFactory.CreateTempDbPath();
        try
        {
            var escenario = CrearEscenario(dbPath, conPrograma: true, conFormatos: false, conCalendario: true);
            int programasAntes;
            using (var ctx = new SOPROContext(dbPath))
                programasAntes = ctx.ProgramasObra.Count();

            var probe = new ProbeState();
            var result = new ProgramacionSynchronizationService().GenerateProgram(
                new ProbeFactory(probe),
                dbPath,
                escenario.ProyectoId,
                expectedActiveProgramaId: escenario.ProgramaId,
                new DateTime(2026, 1, 5),
                TipoPeriodoPrograma.Semana);

            Assert.AreEqual(BudgetOperationStatus.Committed, result.Status, result.Diagnostic);
            Assert.IsTrue(result.SinCambios);
            Assert.IsFalse(result.Success);
            Assert.AreEqual(0, probe.Saves);
            Assert.AreEqual(1, probe.Commits);

            using var ctxVerificacion = new SOPROContext(dbPath);
            Assert.AreEqual(programasAntes, ctxVerificacion.ProgramasObra.Count());
        }
        finally
        {
            DeleteQuietly(dbPath);
        }
    }

    [TestMethod]
    public void SyncFromBudget_Exitoso_ConservaIntactaLaActividadManualVinculadaAConcepto()
    {
        var dbPath = TestDbFactory.CreateTempDbPath();
        try
        {
            int proyectoId;
            int programaId;
            int conceptoId;
            int manualPadreId;
            int manualHijaId;

            using (var ctx = TestDbFactory.CreateContextAt(dbPath))
            {
                proyectoId = CrearProyecto(ctx, "Proyecto con manual vinculada");
                var concepto = CrearConcepto(ctx, proyectoId, "C-01", 10m, 100m, 1);
                ctx.SaveChanges();
                conceptoId = concepto.Id;

                var programa = new ProgramaObra
                {
                    ProyectoId = proyectoId,
                    Nombre = "Programa base",
                    FechaInicioPrograma = new DateTime(2026, 1, 5),
                    TipoPeriodo = TipoPeriodoPrograma.Semana,
                    DuracionPeriodoDias = 7,
                    Activo = true
                };
                ctx.ProgramasObra.Add(programa);
                ctx.SaveChanges();
                programaId = programa.Id;

                var manualPadre = new ActividadProgramada
                {
                    ProgramaObraId = programa.Id,
                    Descripcion = "Manual padre",
                    EsManual = true,
                    EsResumen = true,
                    Nivel = 0,
                    Orden = 1
                };
                ctx.ActividadesProgramadas.Add(manualPadre);
                ctx.SaveChanges();

                var manualHija = new ActividadProgramada
                {
                    ProgramaObraId = programa.Id,
                    ConceptoPresupuestoId = concepto.Id,
                    ActividadPadreId = manualPadre.Id,
                    Clave = "MAN-01",
                    Descripcion = "Manual vinculada a concepto",
                    Unidad = "ml",
                    EsManual = true,
                    EsResumen = false,
                    Nivel = 1,
                    Orden = 7,
                    CantidadTotal = 7m,
                    PrecioUnitario = 3m,
                    ImporteTotal = 21m,
                    FechaInicioProgramada = new DateTime(2026, 1, 5),
                    FechaFinProgramada = new DateTime(2026, 1, 6),
                    DuracionDiasHabiles = 2,
                    MetodoDistribucion = MetodoDistribucionActividad.Uniforme
                };
                ctx.ActividadesProgramadas.Add(manualHija);
                ctx.SaveChanges();

                manualPadreId = manualPadre.Id;
                manualHijaId = manualHija.Id;
            }

            var probe = new ProbeState();
            var result = new ProgramacionSynchronizationService().SyncFromBudget(
                new ProbeFactory(probe),
                dbPath,
                proyectoId,
                expectedActiveProgramaId: programaId);

            Assert.AreEqual(BudgetOperationStatus.Committed, result.Status, result.Diagnostic);
            Assert.IsTrue(result.Success);
            Assert.AreEqual(1, probe.Commits);
            Assert.AreEqual(0, probe.Rollbacks);

            using var ctxVerificacion = new SOPROContext(dbPath);
            var manual = ctxVerificacion.ActividadesProgramadas.AsNoTracking().Single(a => a.Id == manualHijaId);
            Assert.IsTrue(manual.EsManual);
            Assert.AreEqual("MAN-01", manual.Clave);
            Assert.AreEqual("Manual vinculada a concepto", manual.Descripcion);
            Assert.AreEqual("ml", manual.Unidad);
            Assert.AreEqual(7m, manual.CantidadTotal);
            Assert.AreEqual(3m, manual.PrecioUnitario);
            Assert.AreEqual(21m, manual.ImporteTotal);
            Assert.AreEqual(7, manual.Orden);
            Assert.AreEqual(1, manual.Nivel);
            Assert.AreEqual(manualPadreId, manual.ActividadPadreId,
                "La sincronización no debe reescribir el padre de una actividad manual.");
            Assert.IsTrue(ctxVerificacion.ActividadesProgramadas.Any(a => a.Id == manualPadreId));

            Assert.IsTrue(ctxVerificacion.ActividadesProgramadas.Any(a =>
                    a.ProgramaObraId == programaId && !a.EsManual && a.ConceptoPresupuestoId == conceptoId),
                "El concepto debe tener además su actividad generada (la manual no se reutiliza).");
        }
        finally
        {
            DeleteQuietly(dbPath);
        }
    }

    [TestMethod]
    public void RebuildProgram_UnSoloCommitAunqueLaGeneracionGuardePorTramosYConservaFormatoYCalendario()
    {
        var dbPath = TestDbFactory.CreateTempDbPath();
        try
        {
            var escenario = CrearEscenario(dbPath, conPrograma: true, conFormatos: true, conCalendario: true);
            AgregarConceptos(dbPath, escenario.ProyectoId);
            var probe = new ProbeState();

            var result = new ProgramacionSynchronizationService()
                .RebuildProgram(new ProbeFactory(probe), dbPath, escenario.ProyectoId, expectedActiveProgramaId: escenario.ProgramaId);

            Assert.AreEqual(BudgetOperationStatus.Committed, result.Status, result.Diagnostic);
            Assert.IsTrue(result.Success);
            Assert.IsNotNull(result.ProgramaObraId);
            Assert.AreNotEqual(escenario.ProgramaId, result.ProgramaObraId,
                "La reconstrucción debe generar un programa nuevo.");
            Assert.AreEqual(1, probe.Commits, "La generación puede guardar por tramos, pero el commit es uno.");
            Assert.AreEqual(0, probe.Rollbacks);
            Assert.IsTrue(probe.Saves > 1, "La generación usa varios SaveChanges internos.");

            using var ctx = new SOPROContext(dbPath);
            Assert.IsFalse(ctx.ProgramasObra.Any(p => p.Id == escenario.ProgramaId),
                "El grafo anterior debe desaparecer.");
            Assert.IsTrue(ctx.CalendariosLaborales.Any(c => c.Id == escenario.CalendarioId),
                "El calendario debe conservarse.");
            CollectionAssert.AreEquivalent(
                escenario.FormatoProgramaIds,
                ctx.FormatosCeldaReporte.Where(f => f.ProyectoId == escenario.ProyectoId && f.TipoReporte == TipoProgramaObra)
                    .Select(f => f.Id).ToList(),
                "El formato del programa debe conservarse.");
            Assert.IsTrue(ctx.ActividadesProgramadas.Any(a => a.ProgramaObraId == result.ProgramaObraId));
            Assert.IsTrue(ctx.PeriodosPrograma.Any(p => p.ProgramaObraId == result.ProgramaObraId));
        }
        finally
        {
            DeleteQuietly(dbPath);
        }
    }

    [TestMethod]
    public void RebuildProgram_FallaTardiaDeGeneracion_RevierteYConservaElProgramaAnteriorYSuFormato()
    {
        var dbPath = TestDbFactory.CreateTempDbPath();
        try
        {
            var escenario = CrearEscenario(dbPath, conPrograma: true, conFormatos: true, conCalendario: true);
            CrearTrigger(dbPath, "CREATE TRIGGER trg_fail_periodo BEFORE INSERT ON PeriodosPrograma " +
                                 "BEGIN SELECT RAISE(ABORT, 'fallo_simulado_periodo'); END;");

            var probe = new ProbeState();
            var result = new ProgramacionSynchronizationService()
                .RebuildProgram(new ProbeFactory(probe), dbPath, escenario.ProyectoId, expectedActiveProgramaId: escenario.ProgramaId);

            Assert.AreEqual(BudgetOperationStatus.Failed, result.Status, result.Diagnostic);
            Assert.IsNull(result.ProgramaObraId, "Un fallo revertido no expone Ids confirmados.");
            StringAssert.Contains(result.Diagnostic, "fallo_simulado_periodo");
            Assert.AreEqual(0, probe.Commits);
            Assert.AreEqual(1, probe.Rollbacks);

            using var ctx = new SOPROContext(dbPath);
            AssertExisteElGrafo(ctx, escenario);
            CollectionAssert.AreEquivalent(
                escenario.FormatoProgramaIds,
                ctx.FormatosCeldaReporte.Where(f => f.ProyectoId == escenario.ProyectoId && f.TipoReporte == TipoProgramaObra)
                    .Select(f => f.Id).ToList());
        }
        finally
        {
            DeleteQuietly(dbPath);
        }
    }

    // ═══════════════════════ Sincronización destructiva atómica ═══════════════════════

    [TestMethod]
    public void SyncFromBudget_SinPrograma_GeneraDentroDeLaMismaTransaccionYConfirmaUnaVez()
    {
        var dbPath = TestDbFactory.CreateTempDbPath();
        try
        {
            int proyectoId;
            using (var ctx = TestDbFactory.CreateContextAt(dbPath))
            {
                proyectoId = CrearProyecto(ctx, "Proyecto sin programa");
                CrearConcepto(ctx, proyectoId, "C-01", 10m, 100m, 1);
                CrearConcepto(ctx, proyectoId, "C-02", 2m, 50m, 2);
                ctx.SaveChanges();
            }

            var probe = new ProbeState();
            var result = new ProgramacionSynchronizationService()
                .SyncFromBudget(new ProbeFactory(probe), dbPath, proyectoId, expectedActiveProgramaId: null);

            Assert.AreEqual(BudgetOperationStatus.Committed, result.Status, result.Diagnostic);
            Assert.IsTrue(result.Success);
            Assert.IsNotNull(result.ProgramaObraId);
            Assert.AreEqual(1, probe.Commits);
            Assert.AreEqual(0, probe.Rollbacks);

            using var ctxVerificacion = new SOPROContext(dbPath);
            var programa = ctxVerificacion.ProgramasObra.Single(p => p.ProyectoId == proyectoId && p.Activo);
            Assert.AreEqual(2, ctxVerificacion.ActividadesProgramadas.Count(a => a.ProgramaObraId == programa.Id));
            Assert.IsTrue(ctxVerificacion.PeriodosPrograma.Any(p => p.ProgramaObraId == programa.Id));
        }
        finally
        {
            DeleteQuietly(dbPath);
        }
    }

    [TestMethod]
    public void SyncFromBudget_FallaTardia_RevierteSinDejarActividadesNiPeriodosParciales()
    {
        var dbPath = TestDbFactory.CreateTempDbPath();
        try
        {
            var escenario = CrearEscenarioSync(dbPath);
            CrearTrigger(dbPath, "CREATE TRIGGER trg_fail_periodo BEFORE INSERT ON PeriodosPrograma " +
                                 "BEGIN SELECT RAISE(ABORT, 'fallo_simulado_periodo'); END;");

            int periodosAntes;
            using (var ctx = new SOPROContext(dbPath))
            {
                periodosAntes = ctx.PeriodosPrograma.Count(p => p.ProgramaObraId == escenario.ProgramaId);
            }

            var probe = new ProbeState();
            var result = new ProgramacionSynchronizationService()
                .SyncFromBudget(new ProbeFactory(probe), dbPath, escenario.ProyectoId, expectedActiveProgramaId: escenario.ProgramaId);

            Assert.AreEqual(BudgetOperationStatus.Failed, result.Status, result.Diagnostic);
            Assert.IsNull(result.ProgramaObraId);
            StringAssert.Contains(result.Diagnostic, "fallo_simulado_periodo");
            Assert.AreEqual(0, probe.Commits);
            Assert.AreEqual(1, probe.Rollbacks);

            using var ctxVerificacion = new SOPROContext(dbPath);
            Assert.IsTrue(ctxVerificacion.ActividadesProgramadas.Any(a => a.Id == escenario.ActividadObsoletaId),
                "La eliminación de la actividad obsoleta debe revertirse.");
            Assert.IsTrue(ctxVerificacion.ActividadesProgramadas.Any(a => a.Id == escenario.ActividadManualId),
                "Las actividades manuales se preservan.");
            Assert.AreEqual(periodosAntes, ctxVerificacion.PeriodosPrograma.Count(p => p.ProgramaObraId == escenario.ProgramaId));
            Assert.IsTrue(ctxVerificacion.DistribucionesPeriodo.Any(d => d.Id == escenario.DistribucionId),
                "Las distribuciones previas deben sobrevivir al rollback.");
        }
        finally
        {
            DeleteQuietly(dbPath);
        }
    }

    // ═════════════════════════ Generación atómica ═════════════════════════

    [TestMethod]
    public void GenerateProgram_FallaTardia_RevierteProgramaActividadesPeriodosYCalendario()
    {
        var dbPath = TestDbFactory.CreateTempDbPath();
        try
        {
            int proyectoId;
            using (var ctx = TestDbFactory.CreateContextAt(dbPath))
            {
                proyectoId = CrearProyecto(ctx, "Proyecto generación atómica");
                CrearConcepto(ctx, proyectoId, "C-01", 10m, 100m, 1);
                CrearConcepto(ctx, proyectoId, "C-02", 2m, 50m, 2);
                ctx.SaveChanges();
            }

            CrearTrigger(dbPath, "CREATE TRIGGER trg_fail_periodo BEFORE INSERT ON PeriodosPrograma " +
                                 "BEGIN SELECT RAISE(ABORT, 'fallo_simulado_periodo'); END;");

            var probe = new ProbeState();
            var result = new ProgramacionSynchronizationService().GenerateProgram(
                new ProbeFactory(probe),
                dbPath,
                proyectoId,
                expectedActiveProgramaId: null,
                new DateTime(2026, 1, 5),
                TipoPeriodoPrograma.Semana);

            Assert.AreEqual(BudgetOperationStatus.Failed, result.Status, result.Diagnostic);
            Assert.IsNull(result.ProgramaObraId);
            StringAssert.Contains(result.Diagnostic, "fallo_simulado_periodo");
            Assert.AreEqual(0, probe.Commits);
            Assert.AreEqual(1, probe.Rollbacks);

            using var ctxVerificacion = new SOPROContext(dbPath);
            Assert.AreEqual(0, ctxVerificacion.ProgramasObra.Count(p => p.ProyectoId == proyectoId),
                "La generación parcial no debe dejar programa.");
            Assert.AreEqual(0, ctxVerificacion.ActividadesProgramadas.Count(),
                "La generación parcial no debe dejar actividades.");
            Assert.AreEqual(0, ctxVerificacion.PeriodosPrograma.Count());
            Assert.AreEqual(0, ctxVerificacion.CalendariosLaborales.Count(c => c.ProyectoId == proyectoId),
                "El calendario creado por la generación también debe revertirse.");
        }
        finally
        {
            DeleteQuietly(dbPath);
        }
    }

    [TestMethod]
    public void GenerateProgram_ConfirmaElProgramaCompletoConUnSoloCommit()
    {
        var dbPath = TestDbFactory.CreateTempDbPath();
        try
        {
            int proyectoId;
            using (var ctx = TestDbFactory.CreateContextAt(dbPath))
            {
                proyectoId = CrearProyecto(ctx, "Proyecto generación");
                CrearConcepto(ctx, proyectoId, "C-01", 10m, 100m, 1);
                ctx.SaveChanges();
            }

            var probe = new ProbeState();
            var result = new ProgramacionSynchronizationService().GenerateProgram(
                new ProbeFactory(probe),
                dbPath,
                proyectoId,
                expectedActiveProgramaId: null,
                new DateTime(2026, 1, 5),
                TipoPeriodoPrograma.Semana);

            Assert.AreEqual(BudgetOperationStatus.Committed, result.Status, result.Diagnostic);
            Assert.IsTrue(result.Success);
            Assert.IsNotNull(result.ProgramaObraId);
            Assert.AreEqual(1, probe.Commits);
            Assert.AreEqual(0, probe.Rollbacks);

            using var ctxVerificacion = new SOPROContext(dbPath);
            Assert.IsTrue(ctxVerificacion.ProgramasObra.Any(p => p.Id == result.ProgramaObraId));
            Assert.IsTrue(ctxVerificacion.PeriodosPrograma.Any(p => p.ProgramaObraId == result.ProgramaObraId));
        }
        finally
        {
            DeleteQuietly(dbPath);
        }
    }

    // ═════════════════════ Recálculo/regeneración atómica ═════════════════════

    [TestMethod]
    public void RecalculateProgram_RegeneraPeriodosYDistribucionesConUnSoloCommit()
    {
        var dbPath = TestDbFactory.CreateTempDbPath();
        try
        {
            var escenario = CrearEscenario(dbPath, conPrograma: true, conFormatos: false, conCalendario: true);
            var probe = new ProbeState();

            var result = new ProgramacionSynchronizationService().RecalculateProgram(
                new ProbeFactory(probe),
                dbPath,
                escenario.ProyectoId,
                expectedActiveProgramaId: escenario.ProgramaId,
                TipoPeriodoPrograma.Mes);

            Assert.AreEqual(BudgetOperationStatus.Committed, result.Status, result.Diagnostic);
            Assert.IsTrue(result.Success);
            Assert.AreEqual(escenario.ProgramaId, result.ProgramaObraId);
            Assert.AreEqual(1, probe.Commits, "El recálculo y la regeneración deben compartir un commit.");
            Assert.AreEqual(0, probe.Rollbacks);
            Assert.IsTrue(probe.Saves > 1, "El recálculo/regeneración usa varios SaveChanges internos.");

            using var ctx = new SOPROContext(dbPath);
            var programa = ctx.ProgramasObra.AsNoTracking().Single(p => p.Id == escenario.ProgramaId);
            Assert.AreEqual(TipoPeriodoPrograma.Mes, programa.TipoPeriodo);
            Assert.AreEqual(30, programa.DuracionPeriodoDias);
            Assert.IsFalse(ctx.PeriodosPrograma.Any(p => p.Id == escenario.PeriodoIds[0]),
                "Los periodos previos deben reemplazarse.");
            Assert.IsTrue(ctx.PeriodosPrograma.Any(p => p.ProgramaObraId == escenario.ProgramaId));
            Assert.IsFalse(ctx.DistribucionesPeriodo.Any(d => d.Id == escenario.DistribucionId),
                "Las distribuciones previas deben recalcularse.");
            Assert.IsTrue(ctx.DistribucionesPeriodo.Any(d =>
                    d.ActividadProgramadaId == escenario.ActividadIds[1]),
                "La actividad hoja debe quedar distribuida.");
        }
        finally
        {
            DeleteQuietly(dbPath);
        }
    }

    [TestMethod]
    public void RecalculateProgram_TipoPeriodoNulo_ConservaElTipoActualYRegenera()
    {
        var dbPath = TestDbFactory.CreateTempDbPath();
        try
        {
            var escenario = CrearEscenario(dbPath, conPrograma: true, conFormatos: false, conCalendario: true);
            var probe = new ProbeState();

            var result = new ProgramacionSynchronizationService().RecalculateProgram(
                new ProbeFactory(probe),
                dbPath,
                escenario.ProyectoId,
                expectedActiveProgramaId: escenario.ProgramaId,
                tipoPeriodo: null);

            Assert.AreEqual(BudgetOperationStatus.Committed, result.Status, result.Diagnostic);
            Assert.IsTrue(result.Success);
            Assert.AreEqual(1, probe.Commits);
            Assert.IsTrue(probe.Saves > 1);

            using var ctx = new SOPROContext(dbPath);
            var programa = ctx.ProgramasObra.AsNoTracking().Single(p => p.Id == escenario.ProgramaId);
            Assert.AreEqual(TipoPeriodoPrograma.Semana, programa.TipoPeriodo,
                "tipoPeriodo null debe conservar el modo actual.");
            Assert.AreEqual(7, programa.DuracionPeriodoDias);
            Assert.IsFalse(ctx.PeriodosPrograma.Any(p => p.Id == escenario.PeriodoIds[0]),
                "Los periodos deben regenerarse con el tipo vigente.");
            Assert.IsTrue(ctx.PeriodosPrograma.Any(p => p.ProgramaObraId == escenario.ProgramaId));
            Assert.IsTrue(ctx.DistribucionesPeriodo.Any(d => d.ActividadProgramadaId == escenario.ActividadIds[1]));
        }
        finally
        {
            DeleteQuietly(dbPath);
        }
    }

    [TestMethod]
    public void RecalculateProgram_FallaAlInsertarPeriodos_RevierteTodo()
    {
        var dbPath = TestDbFactory.CreateTempDbPath();
        try
        {
            var escenario = CrearEscenario(dbPath, conPrograma: true, conFormatos: false, conCalendario: true);
            CrearTrigger(dbPath, "CREATE TRIGGER trg_fail_periodo BEFORE INSERT ON PeriodosPrograma " +
                                 "BEGIN SELECT RAISE(ABORT, 'fallo_simulado_periodo'); END;");

            var probe = new ProbeState();
            var result = new ProgramacionSynchronizationService().RecalculateProgram(
                new ProbeFactory(probe),
                dbPath,
                escenario.ProyectoId,
                expectedActiveProgramaId: escenario.ProgramaId,
                TipoPeriodoPrograma.Quincena);

            Assert.AreEqual(BudgetOperationStatus.Failed, result.Status, result.Diagnostic);
            StringAssert.Contains(result.Diagnostic, "fallo_simulado_periodo");
            Assert.AreEqual(0, probe.Commits);
            Assert.AreEqual(1, probe.Rollbacks);

            using var ctx = new SOPROContext(dbPath);
            var programa = ctx.ProgramasObra.AsNoTracking().Single(p => p.Id == escenario.ProgramaId);
            Assert.AreEqual(TipoPeriodoPrograma.Semana, programa.TipoPeriodo,
                "El tipo de periodo debe revertirse al valor previo.");
            Assert.AreEqual(7, programa.DuracionPeriodoDias);
            Assert.IsTrue(ctx.PeriodosPrograma.Any(p => p.Id == escenario.PeriodoIds[0]));
            Assert.IsTrue(ctx.DistribucionesPeriodo.Any(d => d.Id == escenario.DistribucionId));
        }
        finally
        {
            DeleteQuietly(dbPath);
        }
    }

    [TestMethod]
    public void RecalculateProgram_FallaAlInsertarDistribuciones_ReviertePeriodosYDistribuciones()
    {
        var dbPath = TestDbFactory.CreateTempDbPath();
        try
        {
            var escenario = CrearEscenario(dbPath, conPrograma: true, conFormatos: false, conCalendario: true);
            CrearTrigger(dbPath, "CREATE TRIGGER trg_fail_distribucion BEFORE INSERT ON DistribucionesPeriodo " +
                                 "BEGIN SELECT RAISE(ABORT, 'fallo_simulado_distribucion'); END;");

            var probe = new ProbeState();
            var result = new ProgramacionSynchronizationService().RecalculateProgram(
                new ProbeFactory(probe),
                dbPath,
                escenario.ProyectoId,
                expectedActiveProgramaId: escenario.ProgramaId,
                TipoPeriodoPrograma.Mes);

            Assert.AreEqual(BudgetOperationStatus.Failed, result.Status, result.Diagnostic);
            StringAssert.Contains(result.Diagnostic, "fallo_simulado_distribucion");
            Assert.AreEqual(0, probe.Commits);
            Assert.AreEqual(1, probe.Rollbacks);

            using var ctx = new SOPROContext(dbPath);
            var programa = ctx.ProgramasObra.AsNoTracking().Single(p => p.Id == escenario.ProgramaId);
            Assert.AreEqual(TipoPeriodoPrograma.Semana, programa.TipoPeriodo);
            Assert.IsTrue(ctx.PeriodosPrograma.Any(p => p.Id == escenario.PeriodoIds[0]),
                "El reemplazo de periodos debe revertirse.");
            Assert.IsTrue(ctx.DistribucionesPeriodo.Any(d => d.Id == escenario.DistribucionId),
                "Las distribuciones previas deben sobrevivir al rollback.");
        }
        finally
        {
            DeleteQuietly(dbPath);
        }
    }

    [TestMethod]
    public void RecalculateProgram_ProgramaActivoReemplazado_DevuelveConflictSinMutacion()
    {
        var dbPath = TestDbFactory.CreateTempDbPath();
        try
        {
            var escenario = CrearEscenario(dbPath, conPrograma: true, conFormatos: false, conCalendario: true);
            var reemplazoId = ReemplazarProgramaActivo(dbPath, escenario.ProyectoId);

            var probe = new ProbeState();
            var result = new ProgramacionSynchronizationService().RecalculateProgram(
                new ProbeFactory(probe),
                dbPath,
                escenario.ProyectoId,
                expectedActiveProgramaId: escenario.ProgramaId,
                TipoPeriodoPrograma.Mes);

            Assert.AreEqual(BudgetOperationStatus.Conflict, result.Status, result.Diagnostic);
            Assert.AreEqual(0, probe.Saves);
            Assert.AreEqual(1, probe.Rollbacks);

            using var ctx = new SOPROContext(dbPath);
            AssertExisteElGrafo(ctx, escenario);
            Assert.IsTrue(ctx.ProgramasObra.Any(p => p.Id == reemplazoId && p.Activo));
        }
        finally
        {
            DeleteQuietly(dbPath);
        }
    }

    // ═════════════════════════════ Escenario ═════════════════════════════

    private sealed class Escenario
    {
        public int ProyectoId;
        public int OtroProyectoId;
        public int ProgramaId;
        public int CalendarioId;
        public int ActividadConceptoId;
        public int ActividadManualId;
        public int ActividadObsoletaId;
        public int PeriodoId;
        public int DependenciaId;
        public int DistribucionId;
        public int ExcepcionId;
        public List<int> ActividadIds = new();
        public List<int> PeriodoIds = new();
        public List<int> FormatoProgramaIds = new();
        public List<int> EstiloProgramaIds = new();
        public int FormatoPresupuestoId;
    }

    /// <summary>Proyecto con programa, actividades (incluida manual y obsoleta), periodos, distribuciones y formato.</summary>
    private static Escenario CrearEscenarioSync(string dbPath)
    {
        var escenario = CrearEscenario(dbPath, conPrograma: true, conFormatos: false, conCalendario: true);
        using var ctx = new SOPROContext(dbPath);

        var c1 = CrearConcepto(ctx, escenario.ProyectoId, "C-01", 10m, 100m, 1);
        ctx.SaveChanges();
        var programa = ctx.ProgramasObra.Single(p => p.Id == escenario.ProgramaId);

        var conceptoActividad = new ActividadProgramada
        {
            ProgramaObraId = programa.Id,
            ConceptoPresupuestoId = c1.Id,
            Clave = c1.Clave,
            Descripcion = c1.Descripcion,
            Unidad = "m2",
            EsManual = false,
            Nivel = 1,
            Orden = 1,
            CantidadTotal = 10m,
            CantidadProgramada = 10m,
            PrecioUnitario = 100m,
            ImporteTotal = 1000m,
            ImporteProgramado = 1000m,
            FechaInicioProgramada = new DateTime(2026, 1, 5),
            FechaFinProgramada = new DateTime(2026, 1, 9),
            DuracionDiasHabiles = 5,
            FrentesTrabajo = 1,
            MetodoDistribucion = MetodoDistribucionActividad.Uniforme
        };
        var manual = new ActividadProgramada
        {
            ProgramaObraId = programa.Id,
            Descripcion = "Actividad manual",
            EsManual = true,
            Nivel = 1,
            Orden = 2,
            FechaInicioProgramada = new DateTime(2026, 1, 5),
            FechaFinProgramada = new DateTime(2026, 1, 9),
            DuracionDiasHabiles = 5,
            MetodoDistribucion = MetodoDistribucionActividad.Uniforme
        };
        var obsoleta = new ActividadProgramada
        {
            ProgramaObraId = programa.Id,
            Descripcion = "Actividad obsoleta sin concepto",
            EsManual = false,
            Nivel = 1,
            Orden = 3,
            FechaInicioProgramada = new DateTime(2026, 1, 5),
            FechaFinProgramada = new DateTime(2026, 1, 9),
            DuracionDiasHabiles = 5,
            MetodoDistribucion = MetodoDistribucionActividad.Uniforme
        };
        ctx.ActividadesProgramadas.AddRange(conceptoActividad, manual, obsoleta);
        ctx.SaveChanges();

        escenario.ActividadConceptoId = conceptoActividad.Id;
        escenario.ActividadManualId = manual.Id;
        escenario.ActividadObsoletaId = obsoleta.Id;
        return escenario;
    }

    /// <summary>Escenario de borrado: grafo completo, formato ProgramaObra y un formato ajeno.</summary>
    private static Escenario CrearEscenario(
        string dbPath,
        bool conPrograma,
        bool conFormatos,
        bool conCalendario)
    {
        using var ctx = TestDbFactory.CreateContextAt(dbPath);
        var escenario = new Escenario
        {
            ProyectoId = CrearProyecto(ctx, "Proyecto principal"),
            OtroProyectoId = CrearProyecto(ctx, "Proyecto ajeno")
        };

        if (conCalendario)
        {
            var calendario = CrearCalendario(ctx, escenario.ProyectoId);
            ctx.SaveChanges();
            escenario.CalendarioId = calendario.Id;

            var excepcion = new ExcepcionCalendario
            {
                CalendarioLaboralId = calendario.Id,
                Fecha = new DateTime(2026, 1, 6),
                Descripcion = "Día inhábil",
                Tipo = TipoExcepcionCalendario.Inhabil
            };
            ctx.ExcepcionesCalendario.Add(excepcion);
            ctx.SaveChanges();
            escenario.ExcepcionId = excepcion.Id;
        }

        if (conPrograma)
        {
            var programa = new ProgramaObra
            {
                ProyectoId = escenario.ProyectoId,
                Nombre = "Programa a borrar",
                FechaInicioPrograma = new DateTime(2026, 1, 5),
                FechaFinPrograma = new DateTime(2026, 1, 16),
                TipoPeriodo = TipoPeriodoPrograma.Semana,
                DuracionPeriodoDias = 7,
                CalendarioLaboralId = conCalendario ? escenario.CalendarioId : null,
                Activo = true
            };
            ctx.ProgramasObra.Add(programa);
            ctx.SaveChanges();
            escenario.ProgramaId = programa.Id;

            var padre = new ActividadProgramada
            {
                ProgramaObraId = programa.Id,
                Descripcion = "Agrupador",
                EsResumen = true,
                Nivel = 0,
                Orden = 1,
                FechaInicioProgramada = new DateTime(2026, 1, 5),
                FechaFinProgramada = new DateTime(2026, 1, 9),
                DuracionDiasHabiles = 5
            };
            var hija = new ActividadProgramada
            {
                ProgramaObraId = programa.Id,
                Descripcion = "Concepto",
                EsResumen = false,
                EsManual = true,
                Nivel = 1,
                Orden = 2,
                CantidadTotal = 10m,
                PrecioUnitario = 100m,
                ImporteTotal = 1000m,
                FechaInicioProgramada = new DateTime(2026, 1, 5),
                FechaFinProgramada = new DateTime(2026, 1, 9),
                DuracionDiasHabiles = 5,
                MetodoDistribucion = MetodoDistribucionActividad.Uniforme
            };
            ctx.ActividadesProgramadas.AddRange(padre, hija);
            ctx.SaveChanges();
            hija.ActividadPadreId = padre.Id;

            var periodo = new PeriodoPrograma
            {
                ProgramaObraId = programa.Id,
                NumeroPeriodo = 1,
                Etiqueta = "Semana 01",
                FechaInicio = new DateTime(2026, 1, 5),
                FechaFin = new DateTime(2026, 1, 11)
            };
            ctx.PeriodosPrograma.Add(periodo);
            ctx.SaveChanges();

            var dependencia = new DependenciaActividad
            {
                ActividadOrigenId = padre.Id,
                ActividadDestinoId = hija.Id,
                TipoDependencia = TipoDependenciaActividad.FS
            };
            var distribucion = new DistribucionPeriodo
            {
                ActividadProgramadaId = hija.Id,
                PeriodoProgramaId = periodo.Id,
                CantidadProgramada = 10m,
                PorcentajeProgramado = 100m,
                PrecioUnitario = 100m,
                ImporteProgramado = 1000m
            };
            ctx.DependenciasActividad.Add(dependencia);
            ctx.DistribucionesPeriodo.Add(distribucion);
            ctx.SaveChanges();

            escenario.ActividadIds.AddRange(new[] { padre.Id, hija.Id });
            escenario.PeriodoIds.Add(periodo.Id);
            escenario.DependenciaId = dependencia.Id;
            escenario.DistribucionId = distribucion.Id;

            var programaAjeno = new ProgramaObra
            {
                ProyectoId = escenario.OtroProyectoId,
                Nombre = "Programa ajeno",
                FechaInicioPrograma = new DateTime(2026, 1, 5),
                TipoPeriodo = TipoPeriodoPrograma.Semana,
                Activo = true
            };
            ctx.ProgramasObra.Add(programaAjeno);
            ctx.SaveChanges();
        }

        if (conFormatos)
        {
            var formato1 = AgregarFormato(ctx, escenario.ProyectoId, TipoProgramaObra, "FilaConcepto");
            var formato2 = AgregarFormato(ctx, escenario.ProyectoId, TipoProgramaObra, "FilaTotal");
            var estilo = new ConfiguracionEstiloFilaReporte
            {
                ProyectoId = escenario.ProyectoId,
                TipoReporte = TipoProgramaObra,
                RolFila = "Total",
                Negrita = true
            };
            var formatoAjeno = AgregarFormato(ctx, escenario.ProyectoId, TipoPresupuesto, "FilaConcepto");
            ctx.ConfiguracionesEstiloFilaReporte.Add(estilo);
            ctx.SaveChanges();

            escenario.FormatoProgramaIds.AddRange(new[] { formato1.Id, formato2.Id });
            escenario.EstiloProgramaIds.Add(estilo.Id);
            escenario.FormatoPresupuestoId = formatoAjeno.Id;
        }

        return escenario;
    }

    private static int CrearProyecto(SOPROContext ctx, string nombre)
    {
        var proyecto = new Proyecto
        {
            Nombre = nombre,
            Descripcion = string.Empty,
            Ubicacion = string.Empty,
            Convocante = string.Empty,
            Contratista = string.Empty,
            ApoderadoLegal = string.Empty,
            FechaInicio = new DateTime(2026, 1, 5),
            FechaTermino = new DateTime(2026, 12, 31),
            PlazoEjecucion = 360
        };
        ctx.Proyectos.Add(proyecto);
        ctx.SaveChanges();
        return proyecto.Id;
    }

    private static ConceptoPresupuesto CrearConcepto(
        SOPROContext ctx,
        int proyectoId,
        string clave,
        decimal cantidad,
        decimal precio,
        int orden)
    {
        var concepto = new ConceptoPresupuesto
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
            ImporteTotal = cantidad * precio
        };
        ctx.ConceptosPresupuesto.Add(concepto);
        return concepto;
    }

    /// <summary>
    /// Simula que otra ventana reemplazó el programa base: desactiva los activos y crea
    /// uno nuevo activo. La llamada con el Id viejo como base esperada debe conflictuar.
    /// </summary>
    private static int ReemplazarProgramaActivo(string dbPath, int proyectoId)
    {
        using var ctx = new SOPROContext(dbPath);
        foreach (var activo in ctx.ProgramasObra.Where(p => p.ProyectoId == proyectoId && p.Activo))
            activo.Activo = false;

        var reemplazo = new ProgramaObra
        {
            ProyectoId = proyectoId,
            Nombre = "Programa reemplazado",
            FechaInicioPrograma = new DateTime(2026, 2, 2),
            TipoPeriodo = TipoPeriodoPrograma.Semana,
            DuracionPeriodoDias = 7,
            Activo = true
        };
        ctx.ProgramasObra.Add(reemplazo);
        ctx.SaveChanges();
        return reemplazo.Id;
    }

    /// <summary>Crea un segundo programa activo para el proyecto, sin desactivar el existente.</summary>
    private static int ActivarProgramaAdicional(string dbPath, int proyectoId)
    {
        using var ctx = new SOPROContext(dbPath);
        var adicional = new ProgramaObra
        {
            ProyectoId = proyectoId,
            Nombre = "Programa adicional activo",
            FechaInicioPrograma = new DateTime(2026, 3, 2),
            TipoPeriodo = TipoPeriodoPrograma.Semana,
            DuracionPeriodoDias = 7,
            Activo = true
        };
        ctx.ProgramasObra.Add(adicional);
        ctx.SaveChanges();
        return adicional.Id;
    }

    private static void AgregarConceptos(string dbPath, int proyectoId)
    {
        using var ctx = new SOPROContext(dbPath);
        CrearConcepto(ctx, proyectoId, "C-01", 10m, 100m, 1);
        CrearConcepto(ctx, proyectoId, "C-02", 2m, 50m, 2);
        ctx.SaveChanges();
    }

    private static CalendarioLaboral CrearCalendario(SOPROContext ctx, int proyectoId)
    {
        var calendario = new CalendarioLaboral
        {
            ProyectoId = proyectoId,
            Nombre = "Calendario General",
            Lunes = true,
            Martes = true,
            Miercoles = true,
            Jueves = true,
            Viernes = true,
            Activo = true
        };
        ctx.CalendariosLaborales.Add(calendario);
        return calendario;
    }

    private static FormatoCeldaReporte AgregarFormato(
        SOPROContext ctx,
        int proyectoId,
        string tipoReporte,
        string claveFila)
    {
        var formato = new FormatoCeldaReporte
        {
            ProyectoId = proyectoId,
            TipoReporte = tipoReporte,
            ClaveVista = string.Empty,
            ClaveFila = claveFila,
            NombreColumna = "ImporteTotal",
            ColorFondo = "#112233"
        };
        ctx.FormatosCeldaReporte.Add(formato);
        return formato;
    }

    private static void AssertEstaInexistente(SOPROContext ctx, Escenario escenario)
    {
        Assert.IsFalse(ctx.ProgramasObra.Any(p => p.Id == escenario.ProgramaId));
        Assert.AreEqual(0, ctx.ActividadesProgramadas.Count(a => escenario.ActividadIds.Contains(a.Id)));
        Assert.AreEqual(0, ctx.PeriodosPrograma.Count(p => escenario.PeriodoIds.Contains(p.Id)));
        Assert.IsFalse(ctx.DependenciasActividad.Any(d => d.Id == escenario.DependenciaId));
        Assert.IsFalse(ctx.DistribucionesPeriodo.Any(d => d.Id == escenario.DistribucionId));
        Assert.AreEqual(0, ctx.FormatosCeldaReporte.Count(f => escenario.FormatoProgramaIds.Contains(f.Id)));
        Assert.AreEqual(0, ctx.ConfiguracionesEstiloFilaReporte.Count(f => escenario.EstiloProgramaIds.Contains(f.Id)));

        if (escenario.CalendarioId != 0)
        {
            Assert.IsFalse(ctx.CalendariosLaborales.Any(c => c.Id == escenario.CalendarioId),
                "El borrado con calendarios debe eliminar el calendario del proyecto.");
            Assert.IsFalse(ctx.ExcepcionesCalendario.Any(e => e.Id == escenario.ExcepcionId));
        }
    }

    private static void AssertExisteElGrafo(SOPROContext ctx, Escenario escenario)
    {
        Assert.IsTrue(ctx.ProgramasObra.Any(p => p.Id == escenario.ProgramaId));
        Assert.AreEqual(escenario.ActividadIds.Count, ctx.ActividadesProgramadas.Count(a => escenario.ActividadIds.Contains(a.Id)));
        Assert.AreEqual(escenario.PeriodoIds.Count, ctx.PeriodosPrograma.Count(p => escenario.PeriodoIds.Contains(p.Id)));
        Assert.IsTrue(ctx.DependenciasActividad.Any(d => d.Id == escenario.DependenciaId));
        Assert.IsTrue(ctx.DistribucionesPeriodo.Any(d => d.Id == escenario.DistribucionId));

        if (escenario.CalendarioId != 0)
            Assert.IsTrue(ctx.CalendariosLaborales.Any(c => c.Id == escenario.CalendarioId));
    }

    private static void CrearTrigger(string dbPath, string sql)
    {
        using var ctx = new SOPROContext(dbPath);
        ctx.Database.ExecuteSqlRaw(sql);
    }

    private static void DeleteQuietly(string dbPath)
    {
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        for (var intento = 0; intento < 3; intento++)
        {
            try
            {
                if (File.Exists(dbPath)) File.Delete(dbPath);
                return;
            }
            catch (IOException)
            {
                Thread.Sleep(150);
            }
        }
    }

    // ═════════════════════════ Infraestructura de conteo ═════════════════════════

    private sealed class ProbeState
    {
        public int Saves;
        public int Inicios;
        public int Commits;
        public int Rollbacks;
    }

    private sealed class ProbeFactory : IProjectDbContextFactory
    {
        private readonly ProbeState _state;

        public ProbeFactory(ProbeState state) => _state = state;

        public SOPROContext Create(string databasePath) => new ProbeContext(databasePath, _state);

        public Task<SOPROContext> CreateAsync(string databasePath, CancellationToken cancellationToken = default)
            => Task.FromResult(Create(databasePath));
    }

    private sealed class ProbeContext : SOPROContext
    {
        private readonly ProbeState _state;
        private readonly ProbeInterceptor _interceptor;

        public ProbeContext(string dbPath, ProbeState state)
            : base(dbPath)
        {
            _state = state;
            _interceptor = new ProbeInterceptor(state);
        }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.AddInterceptors(_interceptor);
            base.OnConfiguring(optionsBuilder);
        }

        public override int SaveChanges()
        {
            _state.Saves++;
            return base.SaveChanges();
        }
    }

    private sealed class ProbeInterceptor : DbTransactionInterceptor
    {
        private readonly ProbeState _state;

        public ProbeInterceptor(ProbeState state) => _state = state;

        public override InterceptionResult<DbTransaction> TransactionStarting(
            DbConnection connection,
            TransactionStartingEventData eventData,
            InterceptionResult<DbTransaction> result)
        {
            _state.Inicios++;
            return base.TransactionStarting(connection, eventData, result);
        }

        public override void TransactionCommitted(DbTransaction transaction, TransactionEndEventData eventData)
        {
            _state.Commits++;
            base.TransactionCommitted(transaction, eventData);
        }

        public override void TransactionRolledBack(DbTransaction transaction, TransactionEndEventData eventData)
        {
            _state.Rollbacks++;
            base.TransactionRolledBack(transaction, eventData);
        }
    }
}
