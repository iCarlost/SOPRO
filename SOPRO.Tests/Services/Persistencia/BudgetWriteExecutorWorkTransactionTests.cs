using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SOPRO.Application.Models.Presupuesto;
using SOPRO.Application.Services;
using SOPRO.Core.Entities;
using SOPRO.Data.Context;
using SOPRO.Data.Factories;
using SOPRO.Tests.TestInfrastructure;
using System.Data;
using System.Data.Common;

namespace SOPRO.Tests.Services.Persistencia;

/// <summary>
/// Contrato del ejecutor único (<see cref="BudgetWriteExecutor"/>) para operaciones de
/// trabajo que necesitan varios SaveChanges internos bajo una sola transacción, y
/// regresión del contrato de presupuesto de <c>Execute</c> (una sola escritura).
/// </summary>
[TestClass]
public class BudgetWriteExecutorWorkTransactionTests
{
    private const string ProyectoBase = "proyecto_base";

    // ─────────────────── Work transaction: camino confirmado ───────────────────

    [TestMethod]
    public void ExecuteWorkTransaction_DosSaveChanges_ConfirmaUnaSolaTransaccionYDevuelveInstantanea()
    {
        var dbPath = TestDbFactory.CreateTempDbPath();
        try
        {
            CrearProyectoBase(dbPath);

            var probe = new TransactionProbeInterceptor();
            using var contexto = new ProbeContext(dbPath, probe);

            var execution = BudgetWriteExecutor.ExecuteWorkTransaction(
                new FixedContextFactory(contexto),
                dbPath,
                context =>
                {
                    context.Proyectos.Add(NuevoProyecto("trabajo_uno"));
                    context.SaveChanges();
                    context.Proyectos.Add(NuevoProyecto("trabajo_dos"));
                    context.SaveChanges();

                    return BudgetWriteStep<WorkSnapshot>.CommitAfterSave(
                        () => new WorkSnapshot(context.Proyectos.Count(p => p.Nombre.StartsWith("trabajo_"))),
                        () => Array.Empty<int>());
                });

            Assert.AreEqual(BudgetOperationStatus.Committed, execution.Result.Status, execution.Diagnostic);
            Assert.IsTrue(execution.Result.IsCommitted);
            Assert.IsNull(execution.Diagnostic);

            Assert.IsNotNull(execution.Confirmed, "Un resultado confirmado debe exponer su instantánea.");
            Assert.AreEqual(2, execution.Confirmed!.ProjectCount);

            Assert.AreEqual(2, contexto.SaveChangesCalls, "El callback de trabajo controla los SaveChanges.");
            Assert.AreEqual(1, probe.TransaccionesIniciadas, "Los dos guardados deben vivir en UNA transacción.");
            Assert.AreEqual(1, probe.Commits, "Debe existir un solo commit.");
            Assert.AreEqual(0, probe.Rollbacks);

            using var verificacion = new SOPROContext(dbPath);
            Assert.AreEqual(3, verificacion.Proyectos.AsNoTracking().Count(),
                "La base debe conservar el proyecto base y los dos proyectos del trabajo.");
        }
        finally
        {
            DeleteQuietly(dbPath);
        }
    }

    [TestMethod]
    public void ExecuteWorkTransaction_RechazoDespuesDeEscribir_RevierteYConservaLaClasificacion()
    {
        var dbPath = TestDbFactory.CreateTempDbPath();
        try
        {
            CrearProyectoBase(dbPath);

            var probe = new TransactionProbeInterceptor();
            using var contexto = new ProbeContext(dbPath, probe);

            var execution = BudgetWriteExecutor.ExecuteWorkTransaction(
                new FixedContextFactory(contexto),
                dbPath,
                context =>
                {
                    context.Proyectos.Add(NuevoProyecto("trabajo_rechazado"));
                    context.SaveChanges();
                    return BudgetWriteStep<WorkSnapshot>.Abort(
                        BudgetOperationResult.Conflict("conflicto_simulado"), "rechazo_posterior");
                });

            Assert.AreEqual(BudgetOperationStatus.Conflict, execution.Result.Status, execution.Diagnostic);
            Assert.IsNull(execution.Confirmed);
            StringAssert.Contains(execution.Diagnostic, "rechazo_posterior");

            Assert.AreEqual(1, contexto.SaveChangesCalls);
            Assert.AreEqual(1, probe.Rollbacks, "Un rechazo posterior a la escritura debe revertir.");
            Assert.AreEqual(0, probe.Commits);

            using var verificacion = new SOPROContext(dbPath);
            Assert.AreEqual(1, verificacion.Proyectos.AsNoTracking().Count(),
                "La escritura rechazada no debe quedar en la base.");
        }
        finally
        {
            DeleteQuietly(dbPath);
        }
    }

    // ─────────────────── Work transaction: fallos y estado incierto ───────────────────

    [TestMethod]
    public void ExecuteWorkTransaction_FallaDespuesDelPrimerSave_RevierteLaBaseCompleta()
    {
        var dbPath = TestDbFactory.CreateTempDbPath();
        try
        {
            CrearProyectoBase(dbPath);

            var probe = new TransactionProbeInterceptor();
            using var contexto = new ProbeContext(dbPath, probe);

            var execution = BudgetWriteExecutor.ExecuteWorkTransaction<WorkSnapshot>(
                new FixedContextFactory(contexto),
                dbPath,
                context =>
                {
                    context.Proyectos.Add(NuevoProyecto("trabajo_parcial"));
                    context.SaveChanges();
                    throw new InvalidOperationException("fallo_despues_del_primer_save");
                });

            Assert.AreEqual(BudgetOperationStatus.Failed, execution.Result.Status, execution.Diagnostic);
            Assert.IsFalse(execution.Result.IsCommitted);
            Assert.IsNull(execution.Confirmed, "Un fallo revertido no expone instantánea confirmada.");
            StringAssert.Contains(execution.Diagnostic, "fallo_despues_del_primer_save");

            Assert.AreEqual(1, contexto.SaveChangesCalls);
            Assert.AreEqual(1, probe.Rollbacks);
            Assert.AreEqual(0, probe.Commits);

            using var verificacion = new SOPROContext(dbPath);
            Assert.AreEqual(1, verificacion.Proyectos.AsNoTracking().Count(),
                "La primera escritura tampoco debe sobrevivir al fallo del callback.");
        }
        finally
        {
            DeleteQuietly(dbPath);
        }
    }

    [TestMethod]
    public void ExecuteWorkTransaction_RollbackFalla_DevuelveOutcomeUnknownSinConfirmar()
    {
        var dbPath = TestDbFactory.CreateTempDbPath();
        try
        {
            CrearProyectoBase(dbPath);

            var probe = new TransactionProbeInterceptor { FailRollback = true };
            using var contexto = new ProbeContext(dbPath, probe);

            var execution = BudgetWriteExecutor.ExecuteWorkTransaction<WorkSnapshot>(
                new FixedContextFactory(contexto),
                dbPath,
                context =>
                {
                    context.Proyectos.Add(NuevoProyecto("trabajo_con_rollback_incierto"));
                    context.SaveChanges();
                    throw new InvalidOperationException("fallo_tras_escritura");
                });

            Assert.AreEqual(BudgetOperationStatus.OutcomeUnknown, execution.Result.Status, execution.Diagnostic);
            Assert.IsFalse(execution.Result.IsCommitted);
            Assert.IsNull(execution.Confirmed,
                "Sin reversión confirmada no se puede afirmar ni éxito ni instantánea.");
            StringAssert.Contains(execution.Diagnostic, "rollback_simulado");

            Assert.AreEqual(1, probe.IntentosDeRollback);
            Assert.AreEqual(0, probe.Commits);
        }
        finally
        {
            DeleteQuietly(dbPath);
        }
    }

    [TestMethod]
    public void ExecuteWorkTransaction_CommitIncierto_DevuelveOutcomeUnknownSinConfirmar()
    {
        var dbPath = TestDbFactory.CreateTempDbPath();
        try
        {
            CrearProyectoBase(dbPath);

            var probe = new TransactionProbeInterceptor { FailCommit = true };
            using var contexto = new ProbeContext(dbPath, probe);

            var execution = BudgetWriteExecutor.ExecuteWorkTransaction(
                new FixedContextFactory(contexto),
                dbPath,
                context =>
                {
                    context.Proyectos.Add(NuevoProyecto("trabajo_commit_incierto"));
                    context.SaveChanges();
                    return BudgetWriteStep<WorkSnapshot>.Commit(
                        new WorkSnapshot(1), Array.Empty<int>());
                });

            Assert.AreEqual(BudgetOperationStatus.OutcomeUnknown, execution.Result.Status, execution.Diagnostic);
            Assert.IsFalse(execution.Result.IsCommitted, "Un commit incierto no puede reportarse confirmado.");
            Assert.IsNull(execution.Confirmed);
            StringAssert.Contains(execution.Diagnostic, "commit_simulado");
        }
        finally
        {
            DeleteQuietly(dbPath);
        }
    }

    [TestMethod]
    public void ExecuteWorkTransaction_DisposeIncierto_DevuelveOutcomeUnknownSinConfirmar()
    {
        var dbPath = TestDbFactory.CreateTempDbPath();
        try
        {
            CrearProyectoBase(dbPath);

            var probe = new TransactionProbeInterceptor();
            using var contexto = new ProbeContext(dbPath, probe) { ThrowOnDispose = true };

            var execution = BudgetWriteExecutor.ExecuteWorkTransaction(
                new FixedContextFactory(contexto),
                dbPath,
                context =>
                {
                    context.Proyectos.Add(NuevoProyecto("trabajo_dispose_incierto"));
                    context.SaveChanges();
                    return BudgetWriteStep<WorkSnapshot>.Commit(
                        new WorkSnapshot(1), Array.Empty<int>());
                });

            Assert.AreEqual(BudgetOperationStatus.OutcomeUnknown, execution.Result.Status, execution.Diagnostic);
            Assert.IsFalse(execution.Result.IsCommitted, "Un cierre no verificable no puede reportarse confirmado.");
            Assert.IsNull(execution.Confirmed);
            StringAssert.Contains(execution.Diagnostic, "dispose_simulado");
        }
        finally
        {
            DeleteQuietly(dbPath);
        }
    }

    [TestMethod]
    public void ExecuteWorkTransaction_RollbackConfirmadoConDisposeDeTransaccionIncierto_DevuelveOutcomeUnknown()
    {
        var dbPath = TestDbFactory.CreateTempDbPath();
        try
        {
            CrearProyectoBase(dbPath);

            var probe = new TransactionProbeInterceptor { FailTransactionDispose = true };
            using var contexto = new ProbeContext(dbPath, probe);

            // El proveedor SQLite exige su propio tipo de transacción al ejecutar comandos,
            // así que la transacción instrumentada solo puede cubrir el ciclo abrir/cerrar:
            // el callback rechaza sin escribir y el fallo ocurre al cerrar la transacción.
            var execution = BudgetWriteExecutor.ExecuteWorkTransaction<WorkSnapshot>(
                new FixedContextFactory(contexto),
                dbPath,
                _ => BudgetWriteStep<WorkSnapshot>.Abort(
                    BudgetOperationResult.Conflict("conflicto_simulado"), "rechazo_posterior"));

            Assert.AreEqual(BudgetOperationStatus.OutcomeUnknown, execution.Result.Status, execution.Diagnostic);
            Assert.IsFalse(execution.Result.IsCommitted,
                "Un cierre de transacción no verificable no puede reportarse confirmado.");
            Assert.IsNull(execution.Confirmed,
                "Sin cierre verificable no se expone instantánea ni se afirma el rechazo original.");
            StringAssert.Contains(execution.Diagnostic, "rechazo_posterior",
                "El diagnóstico original debe preservarse junto al de limpieza.");
            StringAssert.Contains(execution.Diagnostic, "dispose_transaccion_simulado");

            Assert.AreEqual(1, probe.Rollbacks, "La reversión sí se confirmó antes del cierre fallido.");
            Assert.AreEqual(1, probe.IntentosDeDisposeDeTransaccion);
            Assert.AreEqual(0, probe.Commits);

            using var verificacion = new SOPROContext(dbPath);
            Assert.AreEqual(1, verificacion.Proyectos.AsNoTracking().Count(),
                "La reversión confirmada no debe dejar escrituras en la base.");
        }
        finally
        {
            DeleteQuietly(dbPath);
        }
    }

    [TestMethod]
    public void ExecuteWorkTransaction_RollbackConfirmadoConDisposeDeContextoIncierto_DevuelveOutcomeUnknown()
    {
        var dbPath = TestDbFactory.CreateTempDbPath();
        try
        {
            CrearProyectoBase(dbPath);

            var probe = new TransactionProbeInterceptor();
            using var contexto = new ProbeContext(dbPath, probe) { ThrowOnDispose = true };

            var execution = BudgetWriteExecutor.ExecuteWorkTransaction<WorkSnapshot>(
                new FixedContextFactory(contexto),
                dbPath,
                context =>
                {
                    context.Proyectos.Add(NuevoProyecto("trabajo_cierre_contexto_incierto"));
                    context.SaveChanges();
                    throw new InvalidOperationException("fallo_tras_escritura");
                });

            Assert.AreEqual(BudgetOperationStatus.OutcomeUnknown, execution.Result.Status, execution.Diagnostic);
            Assert.IsFalse(execution.Result.IsCommitted,
                "Un cierre de contexto no verificable no puede reportarse confirmado.");
            Assert.IsNull(execution.Confirmed,
                "Sin cierre verificable no se expone instantánea ni se afirma el fallo revertido.");
            StringAssert.Contains(execution.Diagnostic, "fallo_tras_escritura",
                "El diagnóstico original debe preservarse junto al de limpieza.");
            StringAssert.Contains(execution.Diagnostic, "dispose_simulado");

            Assert.AreEqual(1, probe.Rollbacks, "La reversión sí se confirmó antes del cierre fallido.");
            Assert.AreEqual(0, probe.Commits);

            using var verificacion = new SOPROContext(dbPath);
            Assert.AreEqual(1, verificacion.Proyectos.AsNoTracking().Count(),
                "La reversión confirmada no debe dejar la escritura fallida en la base.");
        }
        finally
        {
            DeleteQuietly(dbPath);
        }
    }

    [TestMethod]
    public void ExecuteWorkTransaction_RollbackConfirmadoConAmbosCierresInciertos_DevuelveOutcomeUnknownConAmbosDiagnosticos()
    {
        var dbPath = TestDbFactory.CreateTempDbPath();
        try
        {
            CrearProyectoBase(dbPath);

            var probe = new TransactionProbeInterceptor { FailTransactionDispose = true };
            using var contexto = new ProbeContext(dbPath, probe) { ThrowOnDispose = true };

            var execution = BudgetWriteExecutor.ExecuteWorkTransaction<WorkSnapshot>(
                new FixedContextFactory(contexto),
                dbPath,
                _ => throw new InvalidOperationException("fallo_tras_escritura"));

            Assert.AreEqual(BudgetOperationStatus.OutcomeUnknown, execution.Result.Status, execution.Diagnostic);
            Assert.IsNull(execution.Confirmed);
            StringAssert.Contains(execution.Diagnostic, "fallo_tras_escritura");
            StringAssert.Contains(execution.Diagnostic, "transaccion:");
            StringAssert.Contains(execution.Diagnostic, "contexto:");
            StringAssert.Contains(execution.Diagnostic, "dispose_transaccion_simulado");
            StringAssert.Contains(execution.Diagnostic, "dispose_simulado");

            Assert.AreEqual(1, probe.Rollbacks, "La reversión sí se confirmó antes de los cierres fallidos.");
        }
        finally
        {
            DeleteQuietly(dbPath);
        }
    }

    [TestMethod]
    public void ExecuteWorkTransaction_RollbackConfirmadoConCierresOk_ConservaLaClasificacionOriginal()
    {
        var dbPath = TestDbFactory.CreateTempDbPath();
        try
        {
            CrearProyectoBase(dbPath);

            var probe = new TransactionProbeInterceptor();
            using var contexto = new ProbeContext(dbPath, probe);

            var execution = BudgetWriteExecutor.ExecuteWorkTransaction<WorkSnapshot>(
                new FixedContextFactory(contexto),
                dbPath,
                context =>
                {
                    context.Proyectos.Add(NuevoProyecto("trabajo_cierre_ok"));
                    context.SaveChanges();
                    return BudgetWriteStep<WorkSnapshot>.Abort(
                        BudgetOperationResult.Failed("sin_cambios"), "rechazo_posterior");
                });

            Assert.AreEqual(BudgetOperationStatus.Failed, execution.Result.Status, execution.Diagnostic);
            Assert.IsNull(execution.Confirmed);
            Assert.AreEqual(1, probe.Rollbacks);
            Assert.AreEqual(0, probe.Commits);
            StringAssert.Contains(execution.Diagnostic, "rechazo_posterior");
            Assert.IsFalse(execution.Diagnostic!.Contains("cleanup:") && execution.Diagnostic.Contains("transaccion:"),
                "Con cierres verificables no debe reportarse limpieza fallida.");
        }
        finally
        {
            DeleteQuietly(dbPath);
        }
    }

    // ─────────────────── Work transaction: sin cambios ───────────────────

    [TestMethod]
    public void ExecuteWorkTransaction_PasoNoChanges_NoAbreTransaccionNiGuarda()
    {
        var dbPath = TestDbFactory.CreateTempDbPath();
        try
        {
            CrearProyectoBase(dbPath);

            var probe = new TransactionProbeInterceptor();
            using var contexto = new ProbeContext(dbPath, probe);
            var trabajoInvocado = false;

            var execution = BudgetWriteExecutor.ExecuteWorkTransaction(
                new FixedContextFactory(contexto),
                dbPath,
                stage: _ => BudgetWriteStep<WorkSnapshot>.NoChanges(new WorkSnapshot(0)),
                work: _ =>
                {
                    trabajoInvocado = true;
                    return BudgetWriteStep<WorkSnapshot>.Commit(new WorkSnapshot(0), Array.Empty<int>());
                });

            Assert.AreEqual(BudgetOperationStatus.Committed, execution.Result.Status, execution.Diagnostic);
            Assert.IsNotNull(execution.Confirmed);
            Assert.IsFalse(trabajoInvocado, "Sin cambios no debe ejecutarse el trabajo.");
            Assert.AreEqual(0, contexto.SaveChangesCalls);
            Assert.AreEqual(0, probe.TransaccionesIniciadas,
                "Un paso sin cambios no debe abrir una transacción innecesaria.");
        }
        finally
        {
            DeleteQuietly(dbPath);
        }
    }

    // ─────────────────── Execute: regresión del contrato de presupuesto ───────────────────

    [TestMethod]
    public void Execute_CaminoConfirmado_MantieneUnSoloSaveChangesYUnSoloCommit()
    {
        var dbPath = TestDbFactory.CreateTempDbPath();
        try
        {
            CrearProyectoBase(dbPath);

            var probe = new TransactionProbeInterceptor();
            using var contexto = new ProbeContext(dbPath, probe);

            var execution = BudgetWriteExecutor.Execute(
                new FixedContextFactory(contexto),
                dbPath,
                useTransaction: true,
                context =>
                {
                    var proyecto = context.Proyectos.Single(p => p.Nombre == ProyectoBase);
                    proyecto.Descripcion = "descripcion_confirmada";
                    return BudgetWriteStep<WorkSnapshot>.Commit(new WorkSnapshot(1), Array.Empty<int>());
                });

            Assert.AreEqual(BudgetOperationStatus.Committed, execution.Result.Status, execution.Diagnostic);
            Assert.IsNull(execution.Diagnostic);
            Assert.IsNotNull(execution.Confirmed);

            Assert.AreEqual(1, contexto.SaveChangesCalls, "El contrato de presupuesto conserva un único SaveChanges.");
            Assert.AreEqual(1, probe.Commits);
            Assert.AreEqual(0, probe.Rollbacks);

            using var verificacion = new SOPROContext(dbPath);
            Assert.AreEqual("descripcion_confirmada",
                verificacion.Proyectos.AsNoTracking().Single(p => p.Nombre == ProyectoBase).Descripcion);
        }
        finally
        {
            DeleteQuietly(dbPath);
        }
    }

    [TestMethod]
    public void Execute_SaveChangesFalla_RevierteYClasificaFailed()
    {
        var dbPath = TestDbFactory.CreateTempDbPath();
        try
        {
            CrearProyectoBase(dbPath);

            var probe = new TransactionProbeInterceptor();
            using var contexto = new ProbeContext(dbPath, probe) { ThrowOnSave = true };

            var execution = BudgetWriteExecutor.Execute(
                new FixedContextFactory(contexto),
                dbPath,
                useTransaction: true,
                context =>
                {
                    var proyecto = context.Proyectos.Single(p => p.Nombre == ProyectoBase);
                    proyecto.Descripcion = "no_debe_persistir";
                    return BudgetWriteStep<WorkSnapshot>.Commit(new WorkSnapshot(1), Array.Empty<int>());
                });

            Assert.AreEqual(BudgetOperationStatus.Failed, execution.Result.Status, execution.Diagnostic);
            Assert.IsNull(execution.Confirmed);
            StringAssert.Contains(execution.Diagnostic, "save_simulado");
            Assert.AreEqual(1, probe.Rollbacks);
            Assert.AreEqual(0, probe.Commits);

            using var verificacion = new SOPROContext(dbPath);
            Assert.AreEqual(string.Empty,
                verificacion.Proyectos.AsNoTracking().Single(p => p.Nombre == ProyectoBase).Descripcion);
        }
        finally
        {
            DeleteQuietly(dbPath);
        }
    }

    [TestMethod]
    public void Execute_RollbackFalla_DevuelveOutcomeUnknown()
    {
        var dbPath = TestDbFactory.CreateTempDbPath();
        try
        {
            CrearProyectoBase(dbPath);

            var probe = new TransactionProbeInterceptor { FailRollback = true };
            using var contexto = new ProbeContext(dbPath, probe) { ThrowOnSave = true };

            var execution = BudgetWriteExecutor.Execute(
                new FixedContextFactory(contexto),
                dbPath,
                useTransaction: true,
                context =>
                {
                    var proyecto = context.Proyectos.Single(p => p.Nombre == ProyectoBase);
                    proyecto.Descripcion = "no_debe_persistir";
                    return BudgetWriteStep<WorkSnapshot>.Commit(new WorkSnapshot(1), Array.Empty<int>());
                });

            Assert.AreEqual(BudgetOperationStatus.OutcomeUnknown, execution.Result.Status, execution.Diagnostic);
            Assert.IsNull(execution.Confirmed);
            StringAssert.Contains(execution.Diagnostic, "rollback_simulado");
            Assert.AreEqual(1, probe.IntentosDeRollback);
        }
        finally
        {
            DeleteQuietly(dbPath);
        }
    }

    [TestMethod]
    public void Execute_PasoNoChanges_NoLlamaSaveChanges()
    {
        var dbPath = TestDbFactory.CreateTempDbPath();
        try
        {
            CrearProyectoBase(dbPath);

            var probe = new TransactionProbeInterceptor();
            using var contexto = new ProbeContext(dbPath, probe);

            var execution = BudgetWriteExecutor.Execute(
                new FixedContextFactory(contexto),
                dbPath,
                useTransaction: true,
                _ => BudgetWriteStep<WorkSnapshot>.NoChanges(new WorkSnapshot(1)));

            Assert.AreEqual(BudgetOperationStatus.Committed, execution.Result.Status, execution.Diagnostic);
            Assert.IsNotNull(execution.Confirmed);
            Assert.AreEqual(0, contexto.SaveChangesCalls,
                "El contrato vigente de Execute no llama SaveChanges cuando el paso es NoChanges.");
        }
        finally
        {
            DeleteQuietly(dbPath);
        }
    }

    // ─────────────────── Infraestructura ───────────────────

    private static void CrearProyectoBase(string dbPath)
    {
        using var context = TestDbFactory.CreateContextAt(dbPath);
        context.Proyectos.Add(NuevoProyecto(ProyectoBase));
        context.SaveChanges();
    }

    private static Proyecto NuevoProyecto(string nombre) => new()
    {
        Nombre = nombre,
        Descripcion = string.Empty,
        Ubicacion = string.Empty,
        Convocante = string.Empty,
        Contratista = string.Empty,
        ApoderadoLegal = string.Empty,
        FechaInicio = new DateTime(2026, 1, 1),
        FechaTermino = new DateTime(2026, 12, 31),
        PlazoEjecucion = 360,
    };

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

    private sealed class WorkSnapshot
    {
        public WorkSnapshot(int projectCount) => ProjectCount = projectCount;

        public int ProjectCount { get; }
    }

    private sealed class FixedContextFactory : IProjectDbContextFactory
    {
        private readonly SOPROContext _context;

        public FixedContextFactory(SOPROContext context) => _context = context;

        public SOPROContext Create(string databasePath) => _context;

        public Task<SOPROContext> CreateAsync(string databasePath, CancellationToken cancellationToken = default)
            => Task.FromResult(_context);
    }

    private sealed class ProbeContext : SOPROContext
    {
        private readonly TransactionProbeInterceptor _probe;
        private bool _disposeFalló;

        public ProbeContext(string dbPath, TransactionProbeInterceptor probe)
            : base(dbPath)
        {
            _probe = probe;
        }

        public int SaveChangesCalls { get; private set; }

        public bool ThrowOnSave { get; set; }

        public bool ThrowOnDispose { get; set; }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.AddInterceptors(_probe);
            base.OnConfiguring(optionsBuilder);
        }

        public override int SaveChanges()
        {
            SaveChangesCalls++;
            if (ThrowOnSave)
                throw new InvalidOperationException("save_simulado");

            return base.SaveChanges();
        }

        public override void Dispose()
        {
            base.Dispose();

            if (ThrowOnDispose && !_disposeFalló)
            {
                _disposeFalló = true;
                throw new InvalidOperationException("dispose_simulado");
            }
        }
    }

    /// <summary>
    /// Cuenta aperturas, commits y reversiones reales de transacción y permite simular
    /// fallos en el commit o en el rollback (punto exacto donde el ejecutor debe dudar).
    /// </summary>
    private sealed class TransactionProbeInterceptor : DbTransactionInterceptor
    {
        public int TransaccionesIniciadas { get; private set; }

        public int Commits { get; private set; }

        public int Rollbacks { get; private set; }

        public int IntentosDeRollback { get; private set; }

        public int IntentosDeDisposeDeTransaccion { get; private set; }

        public bool FailCommit { get; set; }

        public bool FailRollback { get; set; }

        /// <summary>Simula un cierre no verificable de la transacción de trabajo.</summary>
        public bool FailTransactionDispose { get; set; }

        public override InterceptionResult<DbTransaction> TransactionStarting(
            DbConnection connection,
            TransactionStartingEventData eventData,
            InterceptionResult<DbTransaction> result)
        {
            TransaccionesIniciadas++;

            if (FailTransactionDispose)
            {
                return InterceptionResult<DbTransaction>.SuppressWithResult(
                    new DisposeFailingDbTransaction(
                        connection.BeginTransaction(),
                        () => IntentosDeDisposeDeTransaccion++));
            }

            return base.TransactionStarting(connection, eventData, result);
        }

        public override InterceptionResult TransactionCommitting(
            DbTransaction transaction,
            TransactionEventData eventData,
            InterceptionResult result)
        {
            if (FailCommit)
                throw new InvalidOperationException("commit_simulado");

            return base.TransactionCommitting(transaction, eventData, result);
        }

        public override void TransactionCommitted(DbTransaction transaction, TransactionEndEventData eventData)
        {
            Commits++;
            base.TransactionCommitted(transaction, eventData);
        }

        public override InterceptionResult TransactionRollingBack(
            DbTransaction transaction,
            TransactionEventData eventData,
            InterceptionResult result)
        {
            IntentosDeRollback++;
            if (FailRollback)
                throw new InvalidOperationException("rollback_simulado");

            return base.TransactionRollingBack(transaction, eventData, result);
        }

        public override void TransactionRolledBack(DbTransaction transaction, TransactionEndEventData eventData)
        {
            Rollbacks++;
            base.TransactionRolledBack(transaction, eventData);
        }
    }

    /// <summary>
    /// Envuelve la transacción real para poder simular un <c>Dispose</c> que falla
    /// (cierre no verificable del recurso transaccional).
    /// </summary>
    private sealed class DisposeFailingDbTransaction : DbTransaction
    {
        private readonly DbTransaction _inner;
        private readonly Action _onDispose;

        public DisposeFailingDbTransaction(DbTransaction inner, Action onDispose)
        {
            _inner = inner;
            _onDispose = onDispose;
        }

        public override IsolationLevel IsolationLevel => _inner.IsolationLevel;

        protected override DbConnection? DbConnection => _inner.Connection;

        public override void Commit() => _inner.Commit();

        public override void Rollback() => _inner.Rollback();

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _onDispose();
                _inner.Dispose();
                throw new InvalidOperationException("dispose_transaccion_simulado");
            }

            base.Dispose(disposing);
        }
    }
}
