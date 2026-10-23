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
/// Cambio de tipo de una fila del presupuesto como comando explícito (ADR-0001):
/// contexto nuevo por operación, creación de la fila nueva dentro de ese contexto,
/// una sola escritura, recálculo de agrupadores sin guardado intermedio y detección
/// de estado obsoleto.
/// </summary>
[TestClass]
public class BudgetTypeChangePersistenceTests
{
    [TestMethod]
    public void ExecuteTypeChangeCommand_FilaExistente_CambiaEstadoRecalculaAgrupadoresYUndejaUnchangedElContextoCompartido()
    {
        var dbPath = TestDbFactory.CreateTempDbPath();
        try
        {
            int proyectoId;
            int conceptoId;
            BudgetTypeChangeCommand command;

            using (var context = TestDbFactory.CreateContextAt(dbPath))
            {
                var scenario = SoproCalculationScenarioBuilder.CreateBaseBudgetScenario(context);
                proyectoId = scenario.Proyecto.Id;
                conceptoId = scenario.Concepto.Id;

                command = new BudgetTypeChangeCommand(
                    proyectoId,
                    conceptoId,
                    BudgetConceptTypeBase.FromConcepto(scenario.Concepto),
                    "Capitulo",
                    scenario.Concepto.Clave,
                    scenario.Concepto.Descripcion,
                    scenario.Concepto.Unidad);

                var result = BudgetPersistenceService.ExecuteTypeChangeCommand(
                    new ProjectDbContextFactory(), dbPath, command);

                Assert.AreEqual(BudgetOperationStatus.Committed, result.Status, result.Message);
                Assert.IsTrue(result.IsCommitted);
                Assert.IsNull(result.Diagnostic, "Un camino confirmado no deja diagnóstico de error.");
                Assert.AreEqual(1, result.Operation.ConfirmedConceptIds.Count);
                Assert.AreEqual(conceptoId, result.Operation.ConfirmedConceptIds[0]);

                Assert.IsNotNull(result.Confirmed);
                var confirmado = result.Confirmed!.Concept;
                Assert.AreEqual(conceptoId, confirmado.ConceptId);
                Assert.IsTrue(confirmado.EsAgrupador);
                Assert.AreEqual(0, confirmado.Nivel);
                Assert.AreEqual(string.Empty, confirmado.Unidad);
                Assert.AreEqual(0m, confirmado.Cantidad);
                Assert.AreEqual(0m, confirmado.PrecioUnitario);
                Assert.AreEqual(0m, confirmado.ImporteTotal,
                    "Un capítulo sin hojas queda en cero tras el recálculo.");
                Assert.IsFalse(result.Confirmed.StateAlreadyCorrect);

                Assert.IsTrue(result.Confirmed.Aggregators.Any(a => a.ConceptId == conceptoId),
                    "La instantánea incluye el agrupador recalculado.");

                // El contexto compartido del formulario no escribe el presupuesto.
                Assert.IsFalse(scenario.Concepto.EsAgrupador);
                Assert.AreEqual(1, scenario.Concepto.Nivel);
                Assert.AreEqual(EntityState.Unchanged, context.Entry(scenario.Concepto).State);
            }

            using (var verificacion = new SOPROContext(dbPath))
            {
                var persistido = verificacion.ConceptosPresupuesto
                    .AsNoTracking()
                    .Single(c => c.Id == conceptoId);

                Assert.IsTrue(persistido.EsAgrupador);
                Assert.AreEqual(0, persistido.Nivel);
                Assert.AreEqual(string.Empty, persistido.Unidad);
                Assert.AreEqual(0m, persistido.Cantidad);
                Assert.AreEqual(0m, persistido.PrecioUnitario);
                Assert.AreEqual(0m, persistido.ImporteTotal);
                Assert.AreEqual(1, persistido.Orden, "El cambio de tipo no reordena la fila.");
            }
        }
        finally
        {
            DeleteQuietly(dbPath);
        }
    }

    [TestMethod]
    public void ExecuteTypeChangeCommand_MismoTipo_ConfirmaSinEscribirNiTocarFechaModificacion()
    {
        var dbPath = TestDbFactory.CreateTempDbPath();
        try
        {
            int conceptoId;
            DateTime fechaPrevia;

            using (var context = TestDbFactory.CreateContextAt(dbPath))
            {
                var scenario = SoproCalculationScenarioBuilder.CreateBaseBudgetScenario(context);
                conceptoId = scenario.Concepto.Id;

                fechaPrevia = new DateTime(2026, 1, 15, 10, 30, 0, DateTimeKind.Utc);
                scenario.Concepto.FechaModificacion = fechaPrevia;
                scenario.Concepto.Nivel = 5;
                context.SaveChanges();

                var command = new BudgetTypeChangeCommand(
                    scenario.Proyecto.Id,
                    conceptoId,
                    BudgetConceptTypeBase.FromConcepto(scenario.Concepto),
                    "Concepto",
                    scenario.Concepto.Clave,
                    scenario.Concepto.Descripcion,
                    scenario.Concepto.Unidad);

                var result = BudgetPersistenceService.ExecuteTypeChangeCommand(
                    new ProjectDbContextFactory(), dbPath, command);

                Assert.AreEqual(BudgetOperationStatus.Committed, result.Status, result.Message);
                Assert.IsTrue(result.Confirmed!.StateAlreadyCorrect,
                    "El estado ya era el propuesto: se confirma sin escritura.");
                Assert.AreEqual(fechaPrevia, result.Confirmed.Concept.FechaModificacion);
                Assert.AreEqual(EntityState.Unchanged, context.Entry(scenario.Concepto).State);
            }

            using (var verificacion = new SOPROContext(dbPath))
            {
                var persistido = verificacion.ConceptosPresupuesto
                    .AsNoTracking()
                    .Single(c => c.Id == conceptoId);

                Assert.IsFalse(persistido.EsAgrupador);
                Assert.AreEqual(5, persistido.Nivel, "El no-op conserva el nivel esperado.");
                Assert.AreEqual(fechaPrevia, persistido.FechaModificacion,
                    "Un no-op confirmado no debe reescribir la fila.");
            }
        }
        finally
        {
            DeleteQuietly(dbPath);
        }
    }

    [TestMethod]
    public void ExecuteTypeChangeCommand_FilaNueva_CreaConceptoConOrdenMaximoMasUnoYIdGeneradoEnElComando()
    {
        var dbPath = TestDbFactory.CreateTempDbPath();
        try
        {
            int proyectoId;
            int ordenExistente;
            BudgetTypeChangeCommand command;

            using (var context = TestDbFactory.CreateContextAt(dbPath))
            {
                var scenario = SoproCalculationScenarioBuilder.CreateBaseBudgetScenario(context);
                proyectoId = scenario.Proyecto.Id;
                ordenExistente = scenario.Concepto.Orden;

                command = new BudgetTypeChangeCommand(
                    proyectoId,
                    conceptId: null,
                    expectedConcept: null,
                    "Concepto",
                    "C-002",
                    "Concepto nuevo desde comando",
                    "pza");

                var result = BudgetPersistenceService.ExecuteTypeChangeCommand(
                    new ProjectDbContextFactory(), dbPath, command);

                Assert.AreEqual(BudgetOperationStatus.Committed, result.Status, result.Message);
                Assert.IsNotNull(result.Confirmed);

                var confirmado = result.Confirmed!.Concept;
                Assert.IsTrue(confirmado.ConceptId > 0, "El Id se genera en el contexto de escritura.");
                Assert.AreEqual(proyectoId, confirmado.ProjectId);
                Assert.AreEqual("C-002", confirmado.Clave);
                Assert.AreEqual("Concepto nuevo desde comando", confirmado.Descripcion);
                Assert.AreEqual("pza", confirmado.Unidad);
                Assert.IsFalse(confirmado.EsAgrupador);
                Assert.AreEqual(5, confirmado.Nivel);
                Assert.AreEqual(ordenExistente + 1, confirmado.Orden,
                    "Se conserva la semántica histórica max(Orden) + 1.");
                Assert.AreEqual(0m, confirmado.Cantidad);
                Assert.AreEqual(1, result.Operation.ConfirmedConceptIds.Count);
                Assert.AreEqual(confirmado.ConceptId, result.Operation.ConfirmedConceptIds[0]);

                // El comando no agrega la entidad nueva al contexto compartido.
                Assert.IsFalse(context.ChangeTracker.Entries<ConceptoPresupuesto>()
                    .Any(e => e.Entity.Id == confirmado.ConceptId));
                Assert.IsFalse(context.ChangeTracker.HasChanges());
            }

            using (var verificacion = new SOPROContext(dbPath))
            {
                var creados = verificacion.ConceptosPresupuesto
                    .AsNoTracking()
                    .Where(c => c.ProyectoId == proyectoId && c.Clave == "C-002")
                    .ToList();

                Assert.AreEqual(1, creados.Count, "La creación se confirma una sola vez.");
                Assert.IsTrue(creados[0].Id > 0);
                Assert.AreEqual(ordenExistente + 1, creados[0].Orden);
            }
        }
        finally
        {
            DeleteQuietly(dbPath);
        }
    }

    [TestMethod]
    public void ExecuteTypeChangeCommand_FilaNuevaAgrupador_CreaElAgrupadorYLoIncluyeEnLaInstantanea()
    {
        var dbPath = TestDbFactory.CreateTempDbPath();
        try
        {
            int proyectoId;

            using (var context = TestDbFactory.CreateContextAt(dbPath))
            {
                var scenario = SoproCalculationScenarioBuilder.CreateBaseBudgetScenario(context);
                proyectoId = scenario.Proyecto.Id;

                var command = new BudgetTypeChangeCommand(
                    proyectoId,
                    conceptId: null,
                    expectedConcept: null,
                    "Capitulo",
                    "CAP-2",
                    "Capítulo nuevo",
                    "m3");

                var result = BudgetPersistenceService.ExecuteTypeChangeCommand(
                    new ProjectDbContextFactory(), dbPath, command);

                Assert.AreEqual(BudgetOperationStatus.Committed, result.Status, result.Message);
                Assert.IsNotNull(result.Confirmed);

                var confirmado = result.Confirmed!.Concept;
                Assert.IsTrue(confirmado.ConceptId > 0);
                Assert.IsTrue(confirmado.EsAgrupador);
                Assert.AreEqual(0, confirmado.Nivel);
                Assert.AreEqual(string.Empty, confirmado.Unidad,
                    "Un agrupador nuevo no conserva la unidad propuesta.");
                Assert.AreEqual(0m, confirmado.Cantidad);
                Assert.AreEqual(0m, confirmado.ImporteTotal,
                    "Un capítulo nuevo sin hojas queda en cero.");
                Assert.IsTrue(result.Confirmed.Aggregators.Any(a => a.ConceptId == confirmado.ConceptId),
                    "La instantánea incluye el agrupador recién creado con su Id.");
            }

            using (var verificacion = new SOPROContext(dbPath))
            {
                var creado = verificacion.ConceptosPresupuesto
                    .AsNoTracking()
                    .Single(c => c.ProyectoId == proyectoId && c.Clave == "CAP-2");

                Assert.IsTrue(creado.EsAgrupador);
                Assert.AreEqual(0, creado.Nivel);
                Assert.AreEqual(string.Empty, creado.Unidad);
            }
        }
        finally
        {
            DeleteQuietly(dbPath);
        }
    }

    [TestMethod]
    public void ExecuteTypeChangeCommand_FalloDeTriggerEnFilaNueva_NoConfirmaIdNiDuplicaAlReintentar()
    {
        var dbPath = TestDbFactory.CreateTempDbPath();
        try
        {
            int proyectoId;
            BudgetTypeChangeCommand command;

            using (var context = TestDbFactory.CreateContextAt(dbPath))
            {
                var scenario = SoproCalculationScenarioBuilder.CreateBaseBudgetScenario(context);
                proyectoId = scenario.Proyecto.Id;

                command = new BudgetTypeChangeCommand(
                    proyectoId,
                    conceptId: null,
                    expectedConcept: null,
                    "Concepto",
                    "C-ROLLBACK",
                    "No debe persistirse en el primer intento",
                    "pza");

                context.Database.ExecuteSqlRaw(
                    "CREATE TRIGGER trg_fail_concepto_insert BEFORE INSERT ON ConceptosPresupuesto " +
                    "BEGIN SELECT RAISE(ABORT, 'fallo_simulado_insert'); END;");

                var primerIntento = BudgetPersistenceService.ExecuteTypeChangeCommand(
                    new ProjectDbContextFactory(), dbPath, command);

                Assert.AreEqual(BudgetOperationStatus.Failed, primerIntento.Status,
                    primerIntento.Message);
                Assert.IsNull(primerIntento.Confirmed, "Un fallo no filtra una instantánea con Id.");
                Assert.AreEqual(0, primerIntento.Operation.ConfirmedConceptIds.Count,
                    "Un fallo no filtra un Id generado.");
                Assert.IsNotNull(primerIntento.Diagnostic);
                StringAssert.Contains(primerIntento.Diagnostic!, "fallo_simulado_insert");
                Assert.IsFalse((primerIntento.Message ?? string.Empty).Contains(
                    "fallo_simulado_insert", StringComparison.Ordinal),
                    "El mensaje al usuario no expone detalles técnicos.");

                context.Database.ExecuteSqlRaw("DROP TRIGGER trg_fail_concepto_insert;");

                int trasFallo = context.ConceptosPresupuesto
                    .Count(c => c.ProyectoId == proyectoId && c.Clave == "C-ROLLBACK");
                Assert.AreEqual(0, trasFallo,
                    "El INSERT abortado no puede quedar en la base ni confirmarse por un guardado ajeno.");

                // Guardado ajeno posterior: no debe arrastrar el INSERT fallido.
                scenario.Proyecto.Descripcion = "cambio ajeno posterior al fallo";
                context.SaveChanges();

                var reintento = BudgetPersistenceService.ExecuteTypeChangeCommand(
                    new ProjectDbContextFactory(), dbPath, command);

                Assert.AreEqual(BudgetOperationStatus.Committed, reintento.Status,
                    reintento.Message);
                Assert.IsNotNull(reintento.Confirmed);
                Assert.IsTrue(reintento.Confirmed!.Concept.ConceptId > 0);
            }

            using (var verificacion = new SOPROContext(dbPath))
            {
                var creados = verificacion.ConceptosPresupuesto
                    .AsNoTracking()
                    .Where(c => c.ProyectoId == proyectoId && c.Clave == "C-ROLLBACK")
                    .ToList();

                Assert.AreEqual(1, creados.Count,
                    "El reintento del mismo comando crea exactamente una fila.");
            }
        }
        finally
        {
            DeleteQuietly(dbPath);
        }
    }

    [TestMethod]
    public void ExecuteTypeChangeCommand_EstadoCambioEnLaBase_DevuelveConflictoSinEscribir()
    {
        var dbPath = TestDbFactory.CreateTempDbPath();
        try
        {
            int conceptoId;
            BudgetTypeChangeCommand command;

            using (var context = TestDbFactory.CreateContextAt(dbPath))
            {
                var scenario = SoproCalculationScenarioBuilder.CreateBaseBudgetScenario(context);
                conceptoId = scenario.Concepto.Id;

                command = new BudgetTypeChangeCommand(
                    scenario.Proyecto.Id,
                    conceptoId,
                    BudgetConceptTypeBase.FromConcepto(scenario.Concepto),
                    "Capitulo",
                    scenario.Concepto.Clave,
                    scenario.Concepto.Descripcion,
                    scenario.Concepto.Unidad);

                using (var otroModulo = new SOPROContext(dbPath))
                {
                    var concepto = otroModulo.ConceptosPresupuesto.Single(c => c.Id == conceptoId);
                    concepto.Nivel = 2;
                    otroModulo.SaveChanges();
                }

                var result = BudgetPersistenceService.ExecuteTypeChangeCommand(
                    new ProjectDbContextFactory(), dbPath, command);

                Assert.AreEqual(BudgetOperationStatus.Conflict, result.Status, result.Message);
                Assert.IsNull(result.Confirmed);
                Assert.AreEqual(0, result.Operation.ConfirmedConceptIds.Count);

                var mensajeUsuario = result.Message ?? string.Empty;
                Assert.IsFalse(string.IsNullOrWhiteSpace(mensajeUsuario));
                Assert.IsFalse(mensajeUsuario.Contains(dbPath, StringComparison.Ordinal));
            }

            using (var verificacion = new SOPROContext(dbPath))
            {
                var persistido = verificacion.ConceptosPresupuesto
                    .AsNoTracking()
                    .Single(c => c.Id == conceptoId);

                Assert.IsFalse(persistido.EsAgrupador,
                    "Un conflicto no aplica el tipo propuesto.");
                Assert.AreEqual(2, persistido.Nivel,
                    "Queda el estado ajeno, no el que esperaba la vista.");
            }
        }
        finally
        {
            DeleteQuietly(dbPath);
        }
    }

    private static void DeleteQuietly(string dbPath)
    {
        try
        {
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
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
