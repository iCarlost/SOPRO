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
/// Reordenamiento de filas persistidas del presupuesto como comando explícito
/// (ADR-0001): contexto nuevo por operación, validación de TODAS las filas, una sola
/// escritura, recálculo de agrupadores con la jerarquía resultante sin guardado
/// intermedio y detección de estado obsoleto o vistas parciales.
/// </summary>
[TestClass]
public class BudgetReorderPersistenceTests
{
    [TestMethod]
    public void ExecuteReorderCommand_OrdenValido_ConfirmaOrdenYRecalculaAgrupadoresConLaNuevaJerarquia()
    {
        var dbPath = TestDbFactory.CreateTempDbPath();
        try
        {
            int proyectoId;
            int capitulo1Id;
            int capitulo2Id;
            int conceptoAId;
            int conceptoBId;
            BudgetReorderCommand command;

            using (var context = TestDbFactory.CreateContextAt(dbPath))
            {
                var escenario = CrearEscenario(context);
                proyectoId = escenario.ProyectoId;
                capitulo1Id = escenario.Capitulo1.Id;
                capitulo2Id = escenario.Capitulo2.Id;
                conceptoAId = escenario.ConceptoA.Id;
                conceptoBId = escenario.ConceptoB.Id;

                // Estado inicial: A (1000) dentro del capítulo 1; B (200) dentro del 2.
                command = new BudgetReorderCommand(
                    proyectoId,
                    new[]
                    {
                        new BudgetReorderTarget(capitulo1Id, 0, 0, 0, true),
                        new BudgetReorderTarget(conceptoAId, 1, 1, 1, false),
                        new BudgetReorderTarget(conceptoBId, 3, 2, 1, false),
                        new BudgetReorderTarget(capitulo2Id, 2, 3, 0, true),
                    });

                var result = BudgetPersistenceService.ExecuteReorderCommand(
                    new ProjectDbContextFactory(), dbPath, command);

                Assert.AreEqual(BudgetOperationStatus.Committed, result.Status, result.Message);
                Assert.IsTrue(result.IsCommitted);
                Assert.IsNull(result.Diagnostic, "Un camino confirmado no deja diagnóstico de error.");
                CollectionAssert.AreEquivalent(
                    new[] { capitulo1Id, capitulo2Id, conceptoAId, conceptoBId },
                    result.Operation.ConfirmedConceptIds.ToArray());

                Assert.IsNotNull(result.Confirmed);
                CollectionAssert.AreEqual(
                    new[] { 0, 1, 2, 3 },
                    result.Confirmed!.Order.Select(o => o.Order).ToArray());
                CollectionAssert.AreEqual(
                    new[] { capitulo1Id, conceptoAId, conceptoBId, capitulo2Id },
                    result.Confirmed.Order.Select(o => o.ConceptId).ToArray());

                var capitulo1 = result.Confirmed.Aggregators.Single(a => a.ConceptId == capitulo1Id);
                var capitulo2 = result.Confirmed.Aggregators.Single(a => a.ConceptId == capitulo2Id);
                Assert.AreEqual(1200m, capitulo1.ImporteTotal,
                    "El capítulo 1 absorbe el concepto B tras el reordenamiento.");
                Assert.AreEqual(1200m, capitulo1.CostoDirectoTotal);
                Assert.AreEqual(0m, capitulo2.ImporteTotal,
                    "El capítulo 2 queda sin hojas después del reordenamiento.");
            }

            using (var verificacion = new SOPROContext(dbPath))
            {
                var conceptos = verificacion.ConceptosPresupuesto
                    .AsNoTracking()
                    .Where(c => c.ProyectoId == proyectoId)
                    .OrderBy(c => c.Orden)
                    .ThenBy(c => c.Id)
                    .ToList();

                CollectionAssert.AreEqual(
                    new[] { capitulo1Id, conceptoAId, conceptoBId, capitulo2Id },
                    conceptos.Select(c => c.Id).ToArray(),
                    "El Orden persistido refleja el arrastre completo.");
                CollectionAssert.AreEqual(
                    new[] { 0, 1, 2, 3 },
                    conceptos.Select(c => c.Orden).ToArray());

                Assert.AreEqual(1200m, conceptos.Single(c => c.Id == capitulo1Id).ImporteTotal);
                Assert.AreEqual(0m, conceptos.Single(c => c.Id == capitulo2Id).ImporteTotal,
                    "Los agrupadores recalculados se confirman en la misma escritura.");
            }
        }
        finally
        {
            DeleteQuietly(dbPath);
        }
    }

    [TestMethod]
    public void ExecuteReorderCommand_OrdenEsperadoObsoleto_DevuelveConflictoSinEscribir()
    {
        var dbPath = TestDbFactory.CreateTempDbPath();
        try
        {
            int proyectoId;
            BudgetReorderCommand command;

            using (var context = TestDbFactory.CreateContextAt(dbPath))
            {
                var escenario = CrearEscenario(context);
                proyectoId = escenario.ProyectoId;
                command = CrearComandoCompleto(escenario);

                using (var otroModulo = new SOPROContext(dbPath))
                {
                    var concepto = otroModulo.ConceptosPresupuesto.Single(c => c.Id == escenario.ConceptoB.Id);
                    concepto.Orden = 4;
                    otroModulo.SaveChanges();
                }

                var result = BudgetPersistenceService.ExecuteReorderCommand(
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
                var conceptos = verificacion.ConceptosPresupuesto
                    .AsNoTracking()
                    .Where(c => c.ProyectoId == proyectoId)
                    .ToList();

                Assert.AreEqual(0, conceptos.Single(c => c.Clave == "CAP-01").Orden);
                Assert.AreEqual(1, conceptos.Single(c => c.Clave == "C-001").Orden);
                Assert.AreEqual(4, conceptos.Single(c => c.Clave == "C-002").Orden,
                    "Un conflicto no reordena ninguna fila.");
                Assert.AreEqual(1000m, conceptos.Single(c => c.EsAgrupador && c.Clave == "CAP-01").ImporteTotal,
                    "Un conflicto no recalcula agrupadores.");
                Assert.AreEqual(200m, conceptos.Single(c => c.EsAgrupador && c.Clave == "CAP-02").ImporteTotal);
            }
        }
        finally
        {
            DeleteQuietly(dbPath);
        }
    }

    [TestMethod]
    public void ExecuteReorderCommand_IdInexistente_DevuelveConflictoSinEscribir()
    {
        var dbPath = TestDbFactory.CreateTempDbPath();
        try
        {
            int proyectoId;
            int cantidadFilas;

            using (var context = TestDbFactory.CreateContextAt(dbPath))
            {
                var escenario = CrearEscenario(context);
                proyectoId = escenario.ProyectoId;
                cantidadFilas = context.ConceptosPresupuesto.Count(c => c.ProyectoId == proyectoId);

                var command = new BudgetReorderCommand(
                    proyectoId,
                    new[]
                    {
                        new BudgetReorderTarget(escenario.Capitulo1.Id, 0, 0, 0, true),
                        new BudgetReorderTarget(escenario.ConceptoA.Id, 1, 1, 1, false),
                        new BudgetReorderTarget(escenario.Capitulo2.Id, 2, 2, 0, true),
                        new BudgetReorderTarget(999_999, 3, 3, 1, false),
                    });

                var result = BudgetPersistenceService.ExecuteReorderCommand(
                    new ProjectDbContextFactory(), dbPath, command);

                Assert.AreEqual(BudgetOperationStatus.Conflict, result.Status, result.Message);
                Assert.IsNull(result.Confirmed);
            }

            using (var verificacion = new SOPROContext(dbPath))
            {
                Assert.AreEqual(cantidadFilas, verificacion.ConceptosPresupuesto.Count(c => c.ProyectoId == proyectoId));
                Assert.AreEqual(1000m, verificacion.ConceptosPresupuesto
                    .AsNoTracking()
                    .Single(c => c.Clave == "CAP-01").ImporteTotal,
                    "La fila válida permanece porque la operación se rechaza completa.");
            }
        }
        finally
        {
            DeleteQuietly(dbPath);
        }
    }

    [TestMethod]
    public void ExecuteReorderCommand_VistaParcial_DevuelveConflictoSinEscribir()
    {
        var dbPath = TestDbFactory.CreateTempDbPath();
        try
        {
            int proyectoId;

            using (var context = TestDbFactory.CreateContextAt(dbPath))
            {
                var escenario = CrearEscenario(context);
                proyectoId = escenario.ProyectoId;

                var command = new BudgetReorderCommand(
                    proyectoId,
                    new[]
                    {
                        new BudgetReorderTarget(escenario.Capitulo1.Id, 0, 0, 0, true),
                        new BudgetReorderTarget(escenario.ConceptoA.Id, 1, 1, 1, false),
                        new BudgetReorderTarget(escenario.Capitulo2.Id, 2, 2, 0, true),
                    });

                var result = BudgetPersistenceService.ExecuteReorderCommand(
                    new ProjectDbContextFactory(), dbPath, command);

                Assert.AreEqual(BudgetOperationStatus.Conflict, result.Status, result.Message);
                Assert.IsNull(result.Confirmed);
            }

            using (var verificacion = new SOPROContext(dbPath))
            {
                CollectionAssert.AreEqual(
                    new[] { 0, 1, 2, 3 },
                    verificacion.ConceptosPresupuesto
                        .AsNoTracking()
                        .Where(c => c.ProyectoId == proyectoId)
                        .OrderBy(c => c.Orden)
                        .Select(c => c.Orden)
                        .ToArray(),
                    "Una vista parcial no reordena ni deja huecos.");
            }
        }
        finally
        {
            DeleteQuietly(dbPath);
        }
    }

    [TestMethod]
    public void BudgetReorderCommand_PosicionesInvalidas_SeRechazaAntesDeConstruirse()
    {
        Assert.ThrowsExactly<ArgumentException>(() => new BudgetReorderCommand(
            1,
            new[]
            {
                new BudgetReorderTarget(1, 0, 0, 0, true),
                new BudgetReorderTarget(2, 1, 0, 1, false),
            }));

        Assert.ThrowsExactly<ArgumentException>(() => new BudgetReorderCommand(
            1,
            new[]
            {
                new BudgetReorderTarget(1, 0, 0, 0, true),
                new BudgetReorderTarget(2, 1, 2, 1, false),
            }));

        Assert.ThrowsExactly<ArgumentException>(() => new BudgetReorderCommand(
            1,
            new[]
            {
                new BudgetReorderTarget(1, 0, 0, 0, true),
                new BudgetReorderTarget(1, 1, 1, 1, false),
            }));
    }

    [TestMethod]
    public void ExecuteReorderCommand_FalloDeTriggerEnElUpdate_RevierteYElReintentoConLaMismaIntencionSeConfirma()
    {
        var dbPath = TestDbFactory.CreateTempDbPath();
        try
        {
            int proyectoId;
            int capitulo1Id;
            int capitulo2Id;
            Escenario escenario;
            BudgetReorderCommand command;

            using (var context = TestDbFactory.CreateContextAt(dbPath))
            {
                escenario = CrearEscenario(context);
                proyectoId = escenario.ProyectoId;
                capitulo1Id = escenario.Capitulo1.Id;
                capitulo2Id = escenario.Capitulo2.Id;
                command = CrearComandoCompleto(escenario);

                context.Database.ExecuteSqlRaw(
                    "CREATE TRIGGER trg_fail_concepto_orden BEFORE UPDATE OF Orden ON ConceptosPresupuesto " +
                    "WHEN NEW.Orden = 3 BEGIN SELECT RAISE(ABORT, 'fallo_simulado_orden'); END;");

                var primerIntento = BudgetPersistenceService.ExecuteReorderCommand(
                    new ProjectDbContextFactory(), dbPath, command);

                Assert.AreEqual(BudgetOperationStatus.Failed, primerIntento.Status, primerIntento.Message);
                Assert.IsNull(primerIntento.Confirmed, "Un fallo no filtra una instantánea de reordenamiento.");
                Assert.AreEqual(0, primerIntento.Operation.ConfirmedConceptIds.Count);
                Assert.IsNotNull(primerIntento.Diagnostic);
                StringAssert.Contains(primerIntento.Diagnostic!, "fallo_simulado_orden");
                Assert.IsFalse((primerIntento.Message ?? string.Empty).Contains(
                    "fallo_simulado_orden", StringComparison.Ordinal),
                    "El mensaje al usuario no expone detalles técnicos.");

                context.Database.ExecuteSqlRaw("DROP TRIGGER trg_fail_concepto_orden;");

                // Guardado ajeno posterior: no debe confirmar un reordenamiento parcial.
                escenario.Proyecto.Descripcion = "cambio ajeno posterior al fallo";
                context.SaveChanges();
            }

            using (var verificacion = new SOPROContext(dbPath))
            {
                var conceptos = verificacion.ConceptosPresupuesto
                    .AsNoTracking()
                    .Where(c => c.ProyectoId == proyectoId)
                    .ToList();

                Assert.AreEqual(3, conceptos.Single(c => c.Clave == "C-002").Orden,
                    "El fallo revertido no deja el reordenamiento a medias.");
                Assert.AreEqual(1000m, conceptos.Single(c => c.Id == capitulo1Id).ImporteTotal);
                Assert.AreEqual(200m, conceptos.Single(c => c.Id == capitulo2Id).ImporteTotal);
            }

            var reintento = BudgetPersistenceService.ExecuteReorderCommand(
                new ProjectDbContextFactory(), dbPath, command);

            Assert.AreEqual(BudgetOperationStatus.Committed, reintento.Status, reintento.Message);

            using (var verificacion = new SOPROContext(dbPath))
            {
                Assert.AreEqual(2, verificacion.ConceptosPresupuesto
                    .AsNoTracking()
                    .Single(c => c.Clave == "C-002").Orden,
                    "El reintento de la misma intención sí confirma el orden propuesto.");
                Assert.AreEqual(1200m, verificacion.ConceptosPresupuesto
                    .AsNoTracking()
                    .Single(c => c.Id == capitulo1Id).ImporteTotal);
                Assert.AreEqual(0m, verificacion.ConceptosPresupuesto
                    .AsNoTracking()
                    .Single(c => c.Id == capitulo2Id).ImporteTotal);
            }
        }
        finally
        {
            DeleteQuietly(dbPath);
        }
    }

    // ═════════════════════════════ Infraestructura ═════════════════════════════

    private sealed class Escenario
    {
        public Proyecto Proyecto { get; init; } = null!;
        public int ProyectoId { get; init; }
        public ConceptoPresupuesto Capitulo1 { get; init; } = null!;
        public ConceptoPresupuesto Capitulo2 { get; init; } = null!;
        public ConceptoPresupuesto ConceptoA { get; init; } = null!;
        public ConceptoPresupuesto ConceptoB { get; init; } = null!;
    }

    private static BudgetReorderCommand CrearComandoCompleto(Escenario escenario)
    {
        return new BudgetReorderCommand(
            escenario.ProyectoId,
            new[]
            {
                new BudgetReorderTarget(escenario.Capitulo1.Id, 0, 0, 0, true),
                new BudgetReorderTarget(escenario.ConceptoA.Id, 1, 1, 1, false),
                new BudgetReorderTarget(escenario.ConceptoB.Id, 3, 2, 1, false),
                new BudgetReorderTarget(escenario.Capitulo2.Id, 2, 3, 0, true),
            });
    }

    /// <summary>
    /// Capítulo 1 (Orden 0) → Concepto A (Orden 1, 1000) → Capítulo 2 (Orden 2) →
    /// Concepto B (Orden 3, 200). Los totales iniciales de los agrupadores ya están
    /// al día para que el reordenamiento sea el único cambio verificable.
    /// </summary>
    private static Escenario CrearEscenario(SOPROContext context)
    {
        var scenario = SoproCalculationScenarioBuilder.CreateBaseBudgetScenario(context);
        var proyecto = scenario.Proyecto;

        var capitulo1 = new ConceptoPresupuesto
        {
            ProyectoId = proyecto.Id,
            Clave = "CAP-01",
            Descripcion = "Capítulo 1",
            EsAgrupador = true,
            Nivel = 0,
            Orden = 0,
            Unidad = string.Empty,
            ColumnasPersonalizadasJSON = string.Empty,
            Notas = string.Empty,
            Cantidad = 0m,
            CostoDirectoUnitario = 0m,
            CostoDirectoTotal = scenario.Concepto.ImporteTotal,
            PrecioUnitario = 0m,
            ImporteTotal = scenario.Concepto.ImporteTotal,
        };

        var capitulo2 = new ConceptoPresupuesto
        {
            ProyectoId = proyecto.Id,
            Clave = "CAP-02",
            Descripcion = "Capítulo 2",
            EsAgrupador = true,
            Nivel = 0,
            Orden = 2,
            Unidad = string.Empty,
            ColumnasPersonalizadasJSON = string.Empty,
            Notas = string.Empty,
            Cantidad = 0m,
            CostoDirectoUnitario = 0m,
            CostoDirectoTotal = 200m,
            PrecioUnitario = 0m,
            ImporteTotal = 200m,
        };

        var conceptoB = new ConceptoPresupuesto
        {
            ProyectoId = proyecto.Id,
            Clave = "C-002",
            Descripcion = "Concepto adicional",
            EsAgrupador = false,
            Nivel = 1,
            Orden = 3,
            Unidad = "m3",
            ColumnasPersonalizadasJSON = string.Empty,
            Notas = string.Empty,
            Cantidad = 2m,
            CostoDirectoUnitario = 100m,
            CostoDirectoTotal = 200m,
            PrecioUnitario = 100m,
            ImporteTotal = 200m,
        };

        context.ConceptosPresupuesto.AddRange(capitulo1, capitulo2, conceptoB);
        context.SaveChanges();

        return new Escenario
        {
            Proyecto = proyecto,
            ProyectoId = proyecto.Id,
            Capitulo1 = capitulo1,
            Capitulo2 = capitulo2,
            ConceptoA = scenario.Concepto,
            ConceptoB = conceptoB,
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
