using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SOPRO.Application.Models.Presupuesto;
using SOPRO.Application.Services;
using SOPRO.Core.Entities;
using SOPRO.Data.Context;
using SOPRO.Data.Factories;
using SOPRO.Tests.TestInfrastructure;

namespace SOPRO.Tests.Services.Persistencia;

/// <summary>
/// Contrato del comando único de edición de la cabecera de una matriz YA
/// persistida (ADR-0001): base persistida exacta, normalización solo de la
/// propuesta, validaciones canónicas, clave duplicada en contexto fresco, guarda
/// del tipo cuando hay referencias y un solo commit. No usa bases reales.
/// </summary>
[TestClass]
public class BudgetMatrixHeaderEditPersistenceTests
{
    [TestMethod]
    public void ExecuteMatrixHeaderEditCommand_Exito_NormalizaPropuestaYConfirmaSnapshot()
    {
        var dbPath = TestDbFactory.CreateTempDbPath();
        try
        {
            int proyectoId;
            int matrizId;
            DateTime fechaAnterior;

            using (var context = TestDbFactory.CreateContextAt(dbPath))
            {
                var proyecto = AddProject(context, "Proyecto cabecera");
                proyectoId = proyecto.Id;
                var matriz = AddMatrix(context, proyectoId, "APU-001", "Concreto simple", "m3", TipoMatriz.APU, 150m);
                matrizId = matriz.Id;
                fechaAnterior = matriz.FechaModificacion;
            }

            var command = CreateCommand(
                dbPath,
                proyectoId,
                matrizId,
                proposedClave: "  apu-002  ",
                proposedDescripcion: "  Concreto reforzado  ",
                proposedUnidad: " m2 ",
                proposedTipo: TipoMatriz.APU);

            var result = BudgetPersistenceService.ExecuteMatrixHeaderEditCommand(
                new ProjectDbContextFactory(), dbPath, command);

            Assert.AreEqual(BudgetOperationStatus.Committed, result.Status, result.Message);
            Assert.IsNull(result.Diagnostic);
            Assert.IsNotNull(result.Confirmed);

            var snapshot = result.Confirmed!;
            Assert.AreEqual(proyectoId, snapshot.ProjectId);
            Assert.AreEqual(matrizId, snapshot.MatrixId);
            Assert.AreEqual("APU-002", snapshot.Clave);
            Assert.AreEqual("Concreto reforzado", snapshot.Descripcion);
            Assert.AreEqual("m2", snapshot.Unidad);
            Assert.AreEqual(TipoMatriz.APU, snapshot.Tipo);
            Assert.IsTrue(snapshot.FechaModificacion > fechaAnterior);

            using (var verificacion = new SOPROContext(dbPath))
            {
                var matriz = verificacion.Matrices.AsNoTracking().Single(m => m.Id == matrizId);
                Assert.AreEqual("APU-002", matriz.Clave);
                Assert.AreEqual("Concreto reforzado", matriz.Descripcion);
                Assert.AreEqual("m2", matriz.Unidad);
                Assert.AreEqual(TipoMatriz.APU, matriz.Tipo);
                Assert.AreEqual(150m, matriz.CostoDirecto, "La cabecera no altera el costo directo.");
                Assert.AreEqual(snapshot.FechaModificacion, matriz.FechaModificacion);
            }
        }
        finally
        {
            DeleteQuietly(dbPath);
        }
    }

    [TestMethod]
    public void ExecuteMatrixHeaderEditCommand_BaseEsperadaDistintaPorCampo_DevuelveConflictoSinEscribir()
    {
        var dbPath = TestDbFactory.CreateTempDbPath();
        try
        {
            int proyectoId;
            int matrizId;

            using (var context = TestDbFactory.CreateContextAt(dbPath))
            {
                var proyecto = AddProject(context, "Proyecto conflicto base");
                proyectoId = proyecto.Id;
                matrizId = AddMatrix(context, proyectoId, "APU-001", "Concreto simple", "m3", TipoMatriz.APU, 100m).Id;
            }

            var baseReal = ReadHeaderBase(dbPath, matrizId);
            var variantes = new[]
            {
                new BudgetMatrixHeaderBase(matrizId, proyectoId, baseReal.Clave + "X", baseReal.Descripcion, baseReal.Unidad, baseReal.Tipo),
                new BudgetMatrixHeaderBase(matrizId, proyectoId, baseReal.Clave, baseReal.Descripcion + "X", baseReal.Unidad, baseReal.Tipo),
                new BudgetMatrixHeaderBase(matrizId, proyectoId, baseReal.Clave, baseReal.Descripcion, baseReal.Unidad + "X", baseReal.Tipo),
                new BudgetMatrixHeaderBase(matrizId, proyectoId, baseReal.Clave, baseReal.Descripcion, baseReal.Unidad, TipoMatriz.Cuadrilla),
            };

            foreach (var variante in variantes)
            {
                var command = new BudgetMatrixHeaderEditCommand(
                    proyectoId, variante, "APU-999", "Descripción propuesta", "m2", TipoMatriz.APU);

                var result = BudgetPersistenceService.ExecuteMatrixHeaderEditCommand(
                    new ProjectDbContextFactory(), dbPath, command);

                Assert.AreEqual(BudgetOperationStatus.Conflict, result.Status, result.Message);
                Assert.IsNull(result.Confirmed);
            }

            using var verificacion = new SOPROContext(dbPath);
            var matriz = verificacion.Matrices.AsNoTracking().Single(m => m.Id == matrizId);
            Assert.AreEqual("APU-001", matriz.Clave);
            Assert.AreEqual("Concreto simple", matriz.Descripcion);
            Assert.AreEqual("m3", matriz.Unidad);
            Assert.AreEqual(TipoMatriz.APU, matriz.Tipo);
        }
        finally
        {
            DeleteQuietly(dbPath);
        }
    }

    [TestMethod]
    public void ExecuteMatrixHeaderEditCommand_OtroProyectoOIngresoInexistente_DevuelveConflictoSinEscribir()
    {
        var dbPath = TestDbFactory.CreateTempDbPath();
        try
        {
            int proyectoAId;
            int proyectoBId;
            int matrizAId;

            using (var context = TestDbFactory.CreateContextAt(dbPath))
            {
                proyectoAId = AddProject(context, "Proyecto A").Id;
                proyectoBId = AddProject(context, "Proyecto B").Id;
                matrizAId = AddMatrix(context, proyectoAId, "APU-001", "Concreto", "m3", TipoMatriz.APU, 100m).Id;
            }

            var baseA = ReadHeaderBase(dbPath, matrizAId);

            var otroProyecto = new BudgetMatrixHeaderEditCommand(
                proyectoBId,
                new BudgetMatrixHeaderBase(matrizAId, proyectoBId, baseA.Clave, baseA.Descripcion, baseA.Unidad, baseA.Tipo),
                "APU-002",
                "Otra descripción",
                "m2",
                TipoMatriz.APU);

            var resultOtroProyecto = BudgetPersistenceService.ExecuteMatrixHeaderEditCommand(
                new ProjectDbContextFactory(), dbPath, otroProyecto);

            Assert.AreEqual(BudgetOperationStatus.Conflict, resultOtroProyecto.Status, resultOtroProyecto.Message);

            var inexistente = new BudgetMatrixHeaderEditCommand(
                proyectoAId,
                new BudgetMatrixHeaderBase(999_999, proyectoAId, "APU-X", "Inexistente", "m2", TipoMatriz.APU),
                "APU-002",
                "Otra descripción",
                "m2",
                TipoMatriz.APU);

            var resultInexistente = BudgetPersistenceService.ExecuteMatrixHeaderEditCommand(
                new ProjectDbContextFactory(), dbPath, inexistente);

            Assert.AreEqual(BudgetOperationStatus.Conflict, resultInexistente.Status, resultInexistente.Message);

            using var verificacion = new SOPROContext(dbPath);
            var matriz = verificacion.Matrices.AsNoTracking().Single(m => m.Id == matrizAId);
            Assert.AreEqual("APU-001", matriz.Clave);
        }
        finally
        {
            DeleteQuietly(dbPath);
        }
    }

    [TestMethod]
    public void ExecuteMatrixHeaderEditCommand_Validaciones_RechazaSinEscribir()
    {
        var dbPath = TestDbFactory.CreateTempDbPath();
        try
        {
            int proyectoId;
            int matrizId;

            using (var context = TestDbFactory.CreateContextAt(dbPath))
            {
                proyectoId = AddProject(context, "Proyecto validaciones").Id;
                matrizId = AddMatrix(context, proyectoId, "APU-001", "Concreto", "m3", TipoMatriz.APU, 100m).Id;
            }

            var baseReal = ReadHeaderBase(dbPath, matrizId);
            var invalidos = new[]
            {
                new BudgetMatrixHeaderEditCommand(proyectoId, baseReal, "   ", "Descripción", "m2", TipoMatriz.APU),
                new BudgetMatrixHeaderEditCommand(proyectoId, baseReal, "APU-002", "   ", "m2", TipoMatriz.APU),
                new BudgetMatrixHeaderEditCommand(proyectoId, baseReal, "APU-002", "Descripción", "   ", TipoMatriz.APU),
                new BudgetMatrixHeaderEditCommand(proyectoId, baseReal, new string('A', 51), "Descripción", "m2", TipoMatriz.APU),
                new BudgetMatrixHeaderEditCommand(proyectoId, baseReal, "APU-002", new string('D', 2001), "m2", TipoMatriz.APU),
                new BudgetMatrixHeaderEditCommand(proyectoId, baseReal, "APU-002", "Descripción", new string('U', 21), TipoMatriz.APU),
                new BudgetMatrixHeaderEditCommand(proyectoId, baseReal, "APU-002", "Descripción", "m2", (TipoMatriz)99),
            };

            foreach (var command in invalidos)
            {
                var result = BudgetPersistenceService.ExecuteMatrixHeaderEditCommand(
                    new ProjectDbContextFactory(), dbPath, command);

                Assert.AreEqual(BudgetOperationStatus.Failed, result.Status,
                    $"Propuesta inválida aceptada: {command.ProposedClave}/{command.ProposedDescripcion?.Length}/{command.ProposedUnidad?.Length}/{command.ProposedTipo}");
                Assert.IsNull(result.Confirmed);
            }

            using var verificacion = new SOPROContext(dbPath);
            Assert.AreEqual("APU-001", verificacion.Matrices.AsNoTracking().Single(m => m.Id == matrizId).Clave);
        }
        finally
        {
            DeleteQuietly(dbPath);
        }
    }

    [TestMethod]
    public void ExecuteMatrixHeaderEditCommand_ClaveDuplicadaNormalizadaContraCasingCanonico_DevuelveConflicto()
    {
        var dbPath = TestDbFactory.CreateTempDbPath();
        try
        {
            int proyectoId;
            int matrizAId;
            int matrizBId;

            using (var context = TestDbFactory.CreateContextAt(dbPath))
            {
                proyectoId = AddProject(context, "Proyecto duplicado").Id;
                matrizAId = AddMatrix(context, proyectoId, "ABC", "Matriz A", "m3", TipoMatriz.APU, 10m).Id;
                matrizBId = AddMatrix(context, proyectoId, "XYZ", "Matriz B", "m3", TipoMatriz.APU, 20m).Id;
            }

            var baseB = ReadHeaderBase(dbPath, matrizBId);
            var command = new BudgetMatrixHeaderEditCommand(
                proyectoId, baseB, "  aBc  ", "Matriz B editada", "m2", TipoMatriz.APU);

            var result = BudgetPersistenceService.ExecuteMatrixHeaderEditCommand(
                new ProjectDbContextFactory(), dbPath, command);

            Assert.AreEqual(BudgetOperationStatus.Conflict, result.Status, result.Message);
            Assert.IsNull(result.Confirmed);

            using var verificacion = new SOPROContext(dbPath);
            Assert.AreEqual("ABC", verificacion.Matrices.AsNoTracking().Single(m => m.Id == matrizAId).Clave);
            Assert.AreEqual("XYZ", verificacion.Matrices.AsNoTracking().Single(m => m.Id == matrizBId).Clave);
        }
        finally
        {
            DeleteQuietly(dbPath);
        }
    }

    [TestMethod]
    public void ExecuteMatrixHeaderEditCommand_CasingLegacyDistinto_NoPrometeUnicidadNormalizada()
    {
        var dbPath = TestDbFactory.CreateTempDbPath();
        try
        {
            int proyectoId;
            int matrizAId;
            int matrizBId;

            using (var context = TestDbFactory.CreateContextAt(dbPath))
            {
                proyectoId = AddProject(context, "Proyecto casing legacy").Id;
                matrizAId = AddMatrix(context, proyectoId, "abc", "Matriz legacy", "m3", TipoMatriz.APU, 10m).Id;
                matrizBId = AddMatrix(context, proyectoId, "XYZ", "Matriz B", "m3", TipoMatriz.APU, 20m).Id;
            }

            var baseB = ReadHeaderBase(dbPath, matrizBId);
            var command = new BudgetMatrixHeaderEditCommand(
                proyectoId, baseB, "ABC", "Matriz B editada", "m2", TipoMatriz.APU);

            var result = BudgetPersistenceService.ExecuteMatrixHeaderEditCommand(
                new ProjectDbContextFactory(), dbPath, command);

            // La igualdad de la base y del índice único es exacta: una clave legacy
            // con otro casing no se declara duplicada. No se promete unicidad normalizada.
            Assert.AreEqual(BudgetOperationStatus.Committed, result.Status, result.Message);

            using var verificacion = new SOPROContext(dbPath);
            Assert.AreEqual("abc", verificacion.Matrices.AsNoTracking().Single(m => m.Id == matrizAId).Clave);
            Assert.AreEqual("ABC", verificacion.Matrices.AsNoTracking().Single(m => m.Id == matrizBId).Clave);
        }
        finally
        {
            DeleteQuietly(dbPath);
        }
    }

    [TestMethod]
    public void ExecuteMatrixHeaderEditCommand_IndiceUnicoYTrigger_RevierteYElAjenoSeGuardaSolo()
    {
        var dbPath = TestDbFactory.CreateTempDbPath();
        try
        {
            int proyectoId;
            int matrizAId;
            int matrizBId;

            using (var context = TestDbFactory.CreateContextAt(dbPath))
            {
                proyectoId = AddProject(context, "Proyecto integridad").Id;
                matrizAId = AddMatrix(context, proyectoId, "APU-001", "Matriz A", "m3", TipoMatriz.APU, 10m).Id;
                matrizBId = AddMatrix(context, proyectoId, "APU-002", "Matriz B", "m3", TipoMatriz.APU, 20m).Id;
            }

            // Integridad del índice único: una actualización cruda que duplica la
            // clave exacta se aborta y deja ambas filas intactas.
            using (var admin = new SOPROContext(dbPath))
            {
                Assert.ThrowsExactly<Microsoft.Data.Sqlite.SqliteException>(() =>
                    admin.Database.ExecuteSqlRaw(
                        "UPDATE Matrices SET Clave = 'APU-001' WHERE Id = {0};", matrizBId));
            }

            using (var verificacion = new SOPROContext(dbPath))
            {
                Assert.AreEqual("APU-001", verificacion.Matrices.AsNoTracking().Single(m => m.Id == matrizAId).Clave);
                Assert.AreEqual("APU-002", verificacion.Matrices.AsNoTracking().Single(m => m.Id == matrizBId).Clave);
            }

            var command = CreateCommand(
                dbPath, proyectoId, matrizBId,
                proposedClave: "APU-003",
                proposedDescripcion: "Matriz B editada",
                proposedUnidad: "m2",
                proposedTipo: TipoMatriz.APU);

            using (var admin = new SOPROContext(dbPath))
            {
                admin.Database.ExecuteSqlRaw(
                    "CREATE TRIGGER trg_fail_matriz BEFORE UPDATE ON Matrices " +
                    "BEGIN SELECT RAISE(ABORT, 'fallo_simulado_matriz'); END;");
            }

            var fallido = BudgetPersistenceService.ExecuteMatrixHeaderEditCommand(
                new ProjectDbContextFactory(), dbPath, command);

            Assert.AreEqual(BudgetOperationStatus.Failed, fallido.Status, fallido.Message);
            Assert.IsNull(fallido.Confirmed);
            StringAssert.Contains(fallido.Diagnostic!, "fallo_simulado_matriz");

            using (var admin = new SOPROContext(dbPath))
            {
                admin.Database.ExecuteSqlRaw("DROP TRIGGER trg_fail_matriz;");
            }

            // Otro SaveChanges ajeno confirma solo su trabajo: la cabecera sigue con
            // los valores previos porque el comando no tocó el contexto compartido.
            using (var ajeno = new SOPROContext(dbPath))
            {
                var proyecto = ajeno.Proyectos.Single(p => p.Id == proyectoId);
                proyecto.Nombre = "Proyecto ajeno modificado";
                ajeno.SaveChanges();
            }

            using var verificacionFinal = new SOPROContext(dbPath);
            var matrizFinal = verificacionFinal.Matrices.AsNoTracking().Single(m => m.Id == matrizBId);
            Assert.AreEqual("APU-002", matrizFinal.Clave);
            Assert.AreEqual("Matriz B", matrizFinal.Descripcion);
            Assert.AreEqual("Proyecto ajeno modificado",
                verificacionFinal.Proyectos.AsNoTracking().Single(p => p.Id == proyectoId).Nombre);
        }
        finally
        {
            DeleteQuietly(dbPath);
        }
    }

    [TestMethod]
    public void ExecuteMatrixHeaderEditCommand_TipoReferenciadoPorConcepto_RechazaSinEscribir()
    {
        var dbPath = TestDbFactory.CreateTempDbPath();
        try
        {
            int proyectoId;
            int matrizId;

            using (var context = TestDbFactory.CreateContextAt(dbPath))
            {
                proyectoId = AddProject(context, "Proyecto tipo concepto").Id;
                matrizId = AddMatrix(context, proyectoId, "APU-001", "Matriz usada", "m3", TipoMatriz.APU, 100m).Id;
                AddConcept(context, proyectoId, "C-001", matrizId);
                context.SaveChanges();
            }

            var baseReal = ReadHeaderBase(dbPath, matrizId);
            var command = new BudgetMatrixHeaderEditCommand(
                proyectoId, baseReal, baseReal.Clave, "Matriz usada", "m3", TipoMatriz.Basico);

            var result = BudgetPersistenceService.ExecuteMatrixHeaderEditCommand(
                new ProjectDbContextFactory(), dbPath, command);

            Assert.AreEqual(BudgetOperationStatus.Failed, result.Status, result.Message);
            Assert.IsNull(result.Confirmed);

            using var verificacion = new SOPROContext(dbPath);
            Assert.AreEqual(TipoMatriz.APU, verificacion.Matrices.AsNoTracking().Single(m => m.Id == matrizId).Tipo);
        }
        finally
        {
            DeleteQuietly(dbPath);
        }
    }

    [TestMethod]
    public void ExecuteMatrixHeaderEditCommand_TipoReferenciadoComoAuxiliar_RechazaSinEscribir()
    {
        var dbPath = TestDbFactory.CreateTempDbPath();
        try
        {
            int proyectoId;
            int matrizId;

            using (var context = TestDbFactory.CreateContextAt(dbPath))
            {
                proyectoId = AddProject(context, "Proyecto tipo auxiliar").Id;
                matrizId = AddMatrix(context, proyectoId, "BAS-001", "Básico referenciado", "m3", TipoMatriz.Basico, 50m).Id;
                var padre = AddMatrix(context, proyectoId, "APU-001", "Padre", "m3", TipoMatriz.APU, 50m);
                context.ComponentesMatriz.Add(new ComponenteMatriz
                {
                    MatrizId = padre.Id,
                    TipoComponente = TipoComponenteMatriz.Auxiliar,
                    AuxiliarId = matrizId,
                    Cantidad = 1m,
                    Importe = 50m,
                    Orden = 1,
                    Notas = string.Empty,
                });
                context.SaveChanges();
            }

            var baseReal = ReadHeaderBase(dbPath, matrizId);
            var command = new BudgetMatrixHeaderEditCommand(
                proyectoId, baseReal, baseReal.Clave, "Básico referenciado", "m3", TipoMatriz.Cuadrilla);

            var result = BudgetPersistenceService.ExecuteMatrixHeaderEditCommand(
                new ProjectDbContextFactory(), dbPath, command);

            Assert.AreEqual(BudgetOperationStatus.Failed, result.Status, result.Message);
            Assert.IsNull(result.Confirmed);

            using var verificacion = new SOPROContext(dbPath);
            Assert.AreEqual(TipoMatriz.Basico, verificacion.Matrices.AsNoTracking().Single(m => m.Id == matrizId).Tipo);
        }
        finally
        {
            DeleteQuietly(dbPath);
        }
    }

    [TestMethod]
    public void ExecuteMatrixHeaderEditCommand_TipoNoReferenciado_PermiteElCambio()
    {
        var dbPath = TestDbFactory.CreateTempDbPath();
        try
        {
            int proyectoId;
            int matrizId;

            using (var context = TestDbFactory.CreateContextAt(dbPath))
            {
                proyectoId = AddProject(context, "Proyecto tipo libre").Id;
                matrizId = AddMatrix(context, proyectoId, "APU-LIBRE", "Sin referencias", "m3", TipoMatriz.APU, 100m).Id;
            }

            var baseReal = ReadHeaderBase(dbPath, matrizId);
            var command = new BudgetMatrixHeaderEditCommand(
                proyectoId, baseReal, baseReal.Clave, baseReal.Descripcion, baseReal.Unidad, TipoMatriz.Basico);

            var result = BudgetPersistenceService.ExecuteMatrixHeaderEditCommand(
                new ProjectDbContextFactory(), dbPath, command);

            Assert.AreEqual(BudgetOperationStatus.Committed, result.Status, result.Message);
            Assert.AreEqual(TipoMatriz.Basico, result.Confirmed!.Tipo);
            Assert.AreEqual(proyectoId, result.Confirmed.ProjectId);

            using var verificacion = new SOPROContext(dbPath);
            var matriz = verificacion.Matrices.AsNoTracking().Single(m => m.Id == matrizId);
            Assert.AreEqual(TipoMatriz.Basico, matriz.Tipo);
            Assert.AreEqual(100m, matriz.CostoDirecto);
        }
        finally
        {
            DeleteQuietly(dbPath);
        }
    }

    [TestMethod]
    public void ExecuteMatrixHeaderEditCommand_CommitConDisposeQueFalla_DevuelveOutcomeUnknownYNoDuplica()
    {
        var dbPath = TestDbFactory.CreateTempDbPath();
        try
        {
            int proyectoId;
            int matrizId;

            using (var context = TestDbFactory.CreateContextAt(dbPath))
            {
                proyectoId = AddProject(context, "Proyecto resultado incierto").Id;
                matrizId = AddMatrix(context, proyectoId, "APU-001", "Concreto", "m3", TipoMatriz.APU, 100m).Id;
            }

            var command = CreateCommand(
                dbPath, proyectoId, matrizId,
                proposedClave: "APU-002",
                proposedDescripcion: "Concreto editado",
                proposedUnidad: "m2",
                proposedTipo: TipoMatriz.APU);

            var result = BudgetPersistenceService.ExecuteMatrixHeaderEditCommand(
                new DisposeThrowingFactory(dbPath), dbPath, command);

            Assert.AreEqual(BudgetOperationStatus.OutcomeUnknown, result.Status);
            Assert.IsNull(result.Confirmed);
            Assert.IsNotNull(result.Diagnostic);
            StringAssert.Contains(result.Diagnostic!, "dispose_simulado");

            // El commit sí ocurrió antes de que fallara el cierre del contexto: la
            // reconciliación debe verlo. Un reintento a ciegas con la base vieja
            // detecta el cambio y no duplica la escritura.
            using (var verificacion = new SOPROContext(dbPath))
            {
                var matriz = verificacion.Matrices.AsNoTracking().Single(m => m.Id == matrizId);
                Assert.AreEqual("APU-002", matriz.Clave);
                Assert.AreEqual("Concreto editado", matriz.Descripcion);
            }

            var reintento = BudgetPersistenceService.ExecuteMatrixHeaderEditCommand(
                new ProjectDbContextFactory(), dbPath, command);

            Assert.AreEqual(BudgetOperationStatus.Conflict, reintento.Status, reintento.Message);
            Assert.IsNull(reintento.Confirmed);
        }
        finally
        {
            DeleteQuietly(dbPath);
        }
    }

    [TestMethod]
    public void ExecuteMatrixHeaderEditCommand_FabricaQueFalla_DevuelveFailedConDiagnostico()
    {
        var command = new BudgetMatrixHeaderEditCommand(
            1,
            new BudgetMatrixHeaderBase(1, 1, "APU-001", "Matriz", "m3", TipoMatriz.APU),
            "APU-002",
            "Matriz editada",
            "m2",
            TipoMatriz.APU);

        var result = BudgetPersistenceService.ExecuteMatrixHeaderEditCommand(
            new ThrowingFactory(), "ignorada.db", command);

        Assert.AreEqual(BudgetOperationStatus.Failed, result.Status);
        Assert.IsNull(result.Confirmed);
        Assert.IsNotNull(result.Diagnostic);
        StringAssert.Contains(result.Diagnostic!, "apertura_simulada");
    }

    [TestMethod]
    public void BudgetMatrixHeaderEditResult_ClasificacionesDelExecutor_SeExponenTalCual()
    {
        var incierto = new BudgetMatrixHeaderEditResult(
            BudgetOperationResult.OutcomeUnknown("resultado incierto"), null, "diagnostico");

        Assert.AreEqual(BudgetOperationStatus.OutcomeUnknown, incierto.Status);
        Assert.IsFalse(incierto.IsCommitted);
        Assert.AreEqual("resultado incierto", incierto.Message);
        Assert.AreEqual("diagnostico", incierto.Diagnostic);
        Assert.IsNull(incierto.Confirmed);
    }

    // ────────────────────────────────────────────────────────────────────────
    // Infraestructura
    // ────────────────────────────────────────────────────────────────────────

    private sealed class ThrowingFactory : IProjectDbContextFactory
    {
        public SOPROContext Create(string databasePath)
            => throw new InvalidOperationException("apertura_simulada");

        public Task<SOPROContext> CreateAsync(string databasePath, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("apertura_simulada");
    }

    private sealed class DisposeThrowingFactory : IProjectDbContextFactory
    {
        private readonly string _dbPath;

        public DisposeThrowingFactory(string dbPath) => _dbPath = dbPath;

        public SOPROContext Create(string databasePath) => new DisposeThrowingContext(_dbPath);

        public Task<SOPROContext> CreateAsync(string databasePath, CancellationToken cancellationToken = default)
            => Task.FromResult(Create(databasePath));
    }

    /// <summary>
    /// Simula un fallo al cerrar el contexto DESPUÉS de que el ejecutor confirmó la
    /// transacción: la operación debe clasificarse como resultado incierto.
    /// </summary>
    private sealed class DisposeThrowingContext : SOPROContext
    {
        public DisposeThrowingContext(string dbPath) : base(dbPath)
        {
        }

        public override void Dispose()
        {
            base.Dispose();
            throw new InvalidOperationException("dispose_simulado");
        }
    }

    private static BudgetMatrixHeaderEditCommand CreateCommand(
        string dbPath,
        int proyectoId,
        int matrizId,
        string proposedClave,
        string proposedDescripcion,
        string proposedUnidad,
        TipoMatriz proposedTipo)
    {
        return new BudgetMatrixHeaderEditCommand(
            proyectoId,
            ReadHeaderBase(dbPath, matrizId),
            proposedClave,
            proposedDescripcion,
            proposedUnidad,
            proposedTipo);
    }

    private static BudgetMatrixHeaderBase ReadHeaderBase(string dbPath, int matrizId)
    {
        using var context = new SOPROContext(dbPath);
        var matriz = context.Matrices.AsNoTracking().Single(m => m.Id == matrizId);
        return BudgetMatrixHeaderBase.FromMatriz(matriz);
    }

    private static Proyecto AddProject(SOPROContext context, string nombre)
    {
        var proyecto = new Proyecto
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
            FechaCreacion = new DateTime(2026, 1, 1),
            FechaModificacion = new DateTime(2026, 1, 1),
        };

        context.Proyectos.Add(proyecto);
        context.SaveChanges();
        return proyecto;
    }

    private static Matriz AddMatrix(
        SOPROContext context,
        int proyectoId,
        string clave,
        string descripcion,
        string unidad,
        TipoMatriz tipo,
        decimal costoDirecto)
    {
        var matriz = new Matriz
        {
            ProyectoId = proyectoId,
            Clave = clave,
            Descripcion = descripcion,
            Unidad = unidad,
            Tipo = tipo,
            CostoDirecto = costoDirecto,
            Notas = string.Empty,
            FechaModificacion = new DateTime(2026, 1, 1),
        };

        context.Matrices.Add(matriz);
        context.SaveChanges();
        return matriz;
    }

    private static ConceptoPresupuesto AddConcept(SOPROContext context, int proyectoId, string clave, int matrizId)
    {
        var concepto = new ConceptoPresupuesto
        {
            ProyectoId = proyectoId,
            Clave = clave,
            Descripcion = "Concepto referente",
            EsAgrupador = false,
            Nivel = 1,
            Orden = 1,
            Unidad = "m3",
            ColumnasPersonalizadasJSON = string.Empty,
            Notas = string.Empty,
            Cantidad = 1m,
            MatrizId = matrizId,
            CostoDirectoUnitario = 0m,
            CostoDirectoTotal = 0m,
            PrecioUnitario = 0m,
            ImporteTotal = 0m,
            FechaModificacion = new DateTime(2026, 1, 1),
        };

        context.ConceptosPresupuesto.Add(concepto);
        return concepto;
    }

    private static void DeleteQuietly(string dbPath)
    {
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        try
        {
            if (File.Exists(dbPath))
                File.Delete(dbPath);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
