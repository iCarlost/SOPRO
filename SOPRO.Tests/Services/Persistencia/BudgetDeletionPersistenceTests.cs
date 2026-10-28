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
/// Eliminación de filas persistidas del presupuesto como comando explícito (ADR-0001):
/// contexto nuevo por operación, validación de TODAS las filas, una sola escritura,
/// recálculo de los agrupadores que permanecen sin guardado intermedio y detección de
/// estado obsoleto o dependencias que impedirían un borrado completo.
/// </summary>
[TestClass]
public class BudgetDeletionPersistenceTests
{
    [TestMethod]
    public void ExecuteDeletionCommand_VariosConceptos_EliminaTodosYRecalculaElAgrupadorQueQueda()
    {
        var dbPath = TestDbFactory.CreateTempDbPath();
        try
        {
            int proyectoId;
            int capituloId;
            int concepto1Id;
            int concepto2Id;
            BudgetDeletionCommand command;

            using (var context = TestDbFactory.CreateContextAt(dbPath))
            {
                var escenario = CrearEscenarioJerarquico(context);
                proyectoId = escenario.ProyectoId;
                capituloId = escenario.Capitulo.Id;
                concepto1Id = escenario.Concepto1.Id;
                concepto2Id = escenario.Concepto2.Id;

                command = new BudgetDeletionCommand(
                    proyectoId,
                    new[]
                    {
                        BudgetDeletionTarget.FromConcepto(escenario.Concepto1),
                        BudgetDeletionTarget.FromConcepto(escenario.Concepto2),
                    });

                var result = BudgetPersistenceService.ExecuteDeletionCommand(
                    new ProjectDbContextFactory(), dbPath, command);

                Assert.AreEqual(BudgetOperationStatus.Committed, result.Status, result.Message);
                Assert.IsTrue(result.IsCommitted);
                Assert.IsNull(result.Diagnostic, "Un camino confirmado no deja diagnóstico de error.");
                CollectionAssert.AreEquivalent(
                    new[] { concepto1Id, concepto2Id },
                    result.Operation.ConfirmedConceptIds.ToArray());

                Assert.IsNotNull(result.Confirmed);
                CollectionAssert.AreEquivalent(
                    new[] { concepto1Id, concepto2Id },
                    result.Confirmed!.DeletedConceptIds.ToArray());

                var capituloConfirmado = result.Confirmed.Aggregators.Single(a => a.ConceptId == capituloId);
                Assert.AreEqual(0m, capituloConfirmado.ImporteTotal,
                    "El agrupador que queda se recalcula sin las filas eliminadas.");
                Assert.AreEqual(0m, capituloConfirmado.CostoDirectoTotal);
            }

            using (var verificacion = new SOPROContext(dbPath))
            {
                Assert.AreEqual(0, verificacion.ConceptosPresupuesto.Count(c => c.Id == concepto1Id));
                Assert.AreEqual(0, verificacion.ConceptosPresupuesto.Count(c => c.Id == concepto2Id));

                var capitulo = verificacion.ConceptosPresupuesto
                    .AsNoTracking()
                    .Single(c => c.Id == capituloId);

                Assert.IsTrue(capitulo.EsAgrupador);
                Assert.AreEqual(0m, capitulo.ImporteTotal);
                Assert.AreEqual(0m, capitulo.CostoDirectoTotal,
                    "El agrupador recalculado se confirma en la misma escritura.");
            }
        }
        finally
        {
            DeleteQuietly(dbPath);
        }
    }

    [TestMethod]
    public void ExecuteDeletionCommand_AgrupadorConSuBloque_EliminaElAncestroYTodoElBloqueEnUnaOperacion()
    {
        var dbPath = TestDbFactory.CreateTempDbPath();
        try
        {
            int proyectoId;

            using (var context = TestDbFactory.CreateContextAt(dbPath))
            {
                var escenario = CrearEscenarioJerarquico(context);
                proyectoId = escenario.ProyectoId;

                // Jerarquía explícita además de Orden/Nivel: el bloque completo debe
                // poder eliminarse sin que la FK de PadreId bloquee el orden de borrado.
                escenario.Concepto1.PadreId = escenario.Capitulo.Id;
                escenario.Concepto2.PadreId = escenario.Capitulo.Id;
                context.SaveChanges();

                var command = new BudgetDeletionCommand(
                    proyectoId,
                    new[]
                    {
                        BudgetDeletionTarget.FromConcepto(escenario.Capitulo),
                        BudgetDeletionTarget.FromConcepto(escenario.Concepto1),
                        BudgetDeletionTarget.FromConcepto(escenario.Concepto2),
                    });

                var result = BudgetPersistenceService.ExecuteDeletionCommand(
                    new ProjectDbContextFactory(), dbPath, command);

                Assert.AreEqual(BudgetOperationStatus.Committed, result.Status, result.Message);
                Assert.IsNotNull(result.Confirmed);
                Assert.AreEqual(3, result.Confirmed!.DeletedConceptIds.Count);
                Assert.AreEqual(0, result.Confirmed.Aggregators.Count,
                    "No queda ningún agrupador en el proyecto.");
            }

            using (var verificacion = new SOPROContext(dbPath))
            {
                Assert.AreEqual(0, verificacion.ConceptosPresupuesto.Count(c => c.ProyectoId == proyectoId),
                    "El bloque jerárquico completo se elimina con una sola confirmación.");
            }
        }
        finally
        {
            DeleteQuietly(dbPath);
        }
    }

    [TestMethod]
    public void ExecuteDeletionCommand_FalloDeTriggerEnLaEliminacion_NoEliminaParcialmenteNiResucitaConUnGuardadoAjeno()
    {
        var dbPath = TestDbFactory.CreateTempDbPath();
        try
        {
            int proyectoId;
            int concepto1Id;
            int concepto2Id;
            BudgetDeletionCommand command;

            using (var context = TestDbFactory.CreateContextAt(dbPath))
            {
                var escenario = CrearEscenarioJerarquico(context);
                proyectoId = escenario.ProyectoId;
                concepto1Id = escenario.Concepto1.Id;
                concepto2Id = escenario.Concepto2.Id;

                command = new BudgetDeletionCommand(
                    proyectoId,
                    new[]
                    {
                        BudgetDeletionTarget.FromConcepto(escenario.Concepto1),
                        BudgetDeletionTarget.FromConcepto(escenario.Concepto2),
                    });

                context.Database.ExecuteSqlRaw(
                    "CREATE TRIGGER trg_fail_concepto_delete BEFORE DELETE ON ConceptosPresupuesto " +
                    "BEGIN SELECT RAISE(ABORT, 'fallo_simulado_delete'); END;");

                var primerIntento = BudgetPersistenceService.ExecuteDeletionCommand(
                    new ProjectDbContextFactory(), dbPath, command);

                Assert.AreEqual(BudgetOperationStatus.Failed, primerIntento.Status,
                    primerIntento.Message);
                Assert.IsNull(primerIntento.Confirmed, "Un fallo no filtra una instantánea de borrado.");
                Assert.AreEqual(0, primerIntento.Operation.ConfirmedConceptIds.Count);
                Assert.IsNotNull(primerIntento.Diagnostic);
                StringAssert.Contains(primerIntento.Diagnostic!, "fallo_simulado_delete");
                Assert.IsFalse((primerIntento.Message ?? string.Empty).Contains(
                    "fallo_simulado_delete", StringComparison.Ordinal),
                    "El mensaje al usuario no expone detalles técnicos.");

                context.Database.ExecuteSqlRaw("DROP TRIGGER trg_fail_concepto_delete;");

                // Guardado ajeno posterior: no debe confirmar ningún DELETE parcial.
                escenario.Proyecto.Descripcion = "cambio ajeno posterior al fallo";
                context.SaveChanges();

                Assert.AreEqual(2, context.ConceptosPresupuesto.Count(c => c.Id == concepto1Id || c.Id == concepto2Id),
                    "El fallo revertido no elimina ninguna de las filas pedidas.");
            }

            using (var verificacion = new SOPROContext(dbPath))
            {
                Assert.AreEqual(1, verificacion.ConceptosPresupuesto.Count(c => c.Id == concepto1Id));
                Assert.AreEqual(1, verificacion.ConceptosPresupuesto.Count(c => c.Id == concepto2Id));
            }

            var reintento = BudgetPersistenceService.ExecuteDeletionCommand(
                new ProjectDbContextFactory(), dbPath, command);

            Assert.AreEqual(BudgetOperationStatus.Committed, reintento.Status, reintento.Message);

            using (var verificacion = new SOPROContext(dbPath))
            {
                Assert.AreEqual(0, verificacion.ConceptosPresupuesto.Count(c => c.Id == concepto1Id));
                Assert.AreEqual(0, verificacion.ConceptosPresupuesto.Count(c => c.Id == concepto2Id));
                Assert.AreEqual(1, verificacion.ConceptosPresupuesto.Count(c => c.ProyectoId == proyectoId),
                    "Solo queda el agrupador que no formaba parte de la eliminación.");
            }
        }
        finally
        {
            DeleteQuietly(dbPath);
        }
    }

    [TestMethod]
    public void ExecuteDeletionCommand_BaseEsperadaObsoleta_DevuelveConflictoSinEscribir()
    {
        var dbPath = TestDbFactory.CreateTempDbPath();
        try
        {
            int conceptoId;
            BudgetDeletionCommand command;

            using (var context = TestDbFactory.CreateContextAt(dbPath))
            {
                var escenario = CrearEscenarioJerarquico(context);
                conceptoId = escenario.Concepto1.Id;

                command = new BudgetDeletionCommand(
                    escenario.ProyectoId,
                    new[] { BudgetDeletionTarget.FromConcepto(escenario.Concepto1) });

                using (var otroModulo = new SOPROContext(dbPath))
                {
                    var concepto = otroModulo.ConceptosPresupuesto.Single(c => c.Id == conceptoId);
                    concepto.Orden = 7;
                    otroModulo.SaveChanges();
                }

                var result = BudgetPersistenceService.ExecuteDeletionCommand(
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
                Assert.AreEqual(1, verificacion.ConceptosPresupuesto.Count(c => c.Id == conceptoId),
                    "Un conflicto no elimina la fila.");
            }
        }
        finally
        {
            DeleteQuietly(dbPath);
        }
    }

    [TestMethod]
    public void ExecuteDeletionCommand_IdInexistente_DevuelveConflictoSinEscribir()
    {
        var dbPath = TestDbFactory.CreateTempDbPath();
        try
        {
            int proyectoId;
            int conceptoId;
            BudgetDeletionCommand command;

            using (var context = TestDbFactory.CreateContextAt(dbPath))
            {
                var escenario = CrearEscenarioJerarquico(context);
                proyectoId = escenario.ProyectoId;
                conceptoId = escenario.Concepto1.Id;

                command = new BudgetDeletionCommand(
                    proyectoId,
                    new[]
                    {
                        BudgetDeletionTarget.FromConcepto(escenario.Concepto1),
                        new BudgetDeletionTarget(999_999, 0, 0, false),
                    });

                var result = BudgetPersistenceService.ExecuteDeletionCommand(
                    new ProjectDbContextFactory(), dbPath, command);

                Assert.AreEqual(BudgetOperationStatus.Conflict, result.Status, result.Message);
                Assert.IsNull(result.Confirmed);
            }

            using (var verificacion = new SOPROContext(dbPath))
            {
                Assert.AreEqual(1, verificacion.ConceptosPresupuesto.Count(c => c.Id == conceptoId),
                    "La fila válida permanece porque la operación se rechaza completa.");
                Assert.AreEqual(3, verificacion.ConceptosPresupuesto.Count(c => c.ProyectoId == proyectoId));
            }
        }
        finally
        {
            DeleteQuietly(dbPath);
        }
    }

    [TestMethod]
    public void ExecuteDeletionCommand_SubFilaFueraDeLaSeleccion_SeRechazaEnVezDeEliminarParcialmente()
    {
        var dbPath = TestDbFactory.CreateTempDbPath();
        try
        {
            int proyectoId;
            int capituloId;
            int conceptoId;

            using (var context = TestDbFactory.CreateContextAt(dbPath))
            {
                var escenario = CrearEscenarioJerarquico(context);
                proyectoId = escenario.ProyectoId;
                capituloId = escenario.Capitulo.Id;
                conceptoId = escenario.Concepto1.Id;

                escenario.Concepto1.PadreId = capituloId;
                context.SaveChanges();

                var command = new BudgetDeletionCommand(
                    proyectoId,
                    new[] { BudgetDeletionTarget.FromConcepto(escenario.Capitulo) });

                var result = BudgetPersistenceService.ExecuteDeletionCommand(
                    new ProjectDbContextFactory(), dbPath, command);

                Assert.AreEqual(BudgetOperationStatus.Failed, result.Status, result.Message);
                Assert.IsNull(result.Confirmed);
            }

            using (var verificacion = new SOPROContext(dbPath))
            {
                Assert.AreEqual(1, verificacion.ConceptosPresupuesto.Count(c => c.Id == capituloId),
                    "El ancestro no se elimina si dejaría sub-filas fuera de la selección.");
                Assert.AreEqual(1, verificacion.ConceptosPresupuesto.Count(c => c.Id == conceptoId));
            }
        }
        finally
        {
            DeleteQuietly(dbPath);
        }
    }

    // ═════════════════════════════ Infraestructura ═════════════════════════════

    private sealed class EscenarioJerarquico
    {
        public Proyecto Proyecto { get; init; } = null!;
        public int ProyectoId { get; init; }
        public ConceptoPresupuesto Capitulo { get; init; } = null!;
        public ConceptoPresupuesto Concepto1 { get; init; } = null!;
        public ConceptoPresupuesto Concepto2 { get; init; } = null!;
    }

    private static EscenarioJerarquico CrearEscenarioJerarquico(SOPROContext context)
    {
        var scenario = SoproCalculationScenarioBuilder.CreateBaseBudgetScenario(context);

        var capitulo = new ConceptoPresupuesto
        {
            ProyectoId = scenario.Proyecto.Id,
            Clave = "CAP-01",
            Descripcion = "Capítulo de prueba",
            EsAgrupador = true,
            Nivel = 0,
            Orden = 0,
            Unidad = string.Empty,
            ColumnasPersonalizadasJSON = string.Empty,
            Notas = string.Empty,
            Cantidad = 0m,
            CostoDirectoUnitario = 0m,
            CostoDirectoTotal = 0m,
            PrecioUnitario = 0m,
            ImporteTotal = 0m,
        };

        var concepto2 = new ConceptoPresupuesto
        {
            ProyectoId = scenario.Proyecto.Id,
            Clave = "C-002",
            Descripcion = "Concepto adicional",
            EsAgrupador = false,
            Nivel = 1,
            Orden = 2,
            Unidad = "m3",
            ColumnasPersonalizadasJSON = string.Empty,
            Notas = string.Empty,
            Cantidad = 2m,
            CostoDirectoUnitario = 50m,
            CostoDirectoTotal = 100m,
            PrecioUnitario = 50m,
            ImporteTotal = 100m,
        };

        context.ConceptosPresupuesto.AddRange(capitulo, concepto2);
        context.SaveChanges();

        return new EscenarioJerarquico
        {
            Proyecto = scenario.Proyecto,
            ProyectoId = scenario.Proyecto.Id,
            Capitulo = capitulo,
            Concepto1 = scenario.Concepto,
            Concepto2 = concepto2,
        };
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
