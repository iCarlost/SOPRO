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
/// Importación de un lote de renglones nuevos como comando explícito (ADR-0001):
/// contexto nuevo por operación, validación de proyecto y base esperada, posición
/// inicial derivada del máximo Orden, INSERT de todo el lote en un único
/// SaveChanges, agrupadores recalculados sin guardar y una sola confirmación.
/// </summary>
[TestClass]
public class BudgetImportPersistenceTests
{
    [TestMethod]
    public void ExecuteImportCommand_LoteOk_InsertaFilasEnUnaOperacionYDevuelveIdsMapeadosSinGuardarAgrupadores()
    {
        var dbPath = TestDbFactory.CreateTempDbPath();
        try
        {
            int proyectoId;
            int conceptoExistenteId;
            int capituloExistenteId;
            BudgetImportCommand command;

            using (var context = TestDbFactory.CreateContextAt(dbPath))
            {
                var scenario = SoproCalculationScenarioBuilder.CreateBaseBudgetScenario(context);
                proyectoId = scenario.Proyecto.Id;
                conceptoExistenteId = scenario.Concepto.Id;

                var capituloExistente = new ConceptoPresupuesto
                {
                    ProyectoId = proyectoId,
                    Clave = "CAP-01",
                    Descripcion = "Capítulo existente",
                    EsAgrupador = true,
                    Nivel = 0,
                    Orden = 0,
                    Unidad = string.Empty,
                    ColumnasPersonalizadasJSON = string.Empty,
                    Notas = string.Empty,
                    Cantidad = 0m,
                    CostoDirectoUnitario = 0m,
                    CostoDirectoTotal = 999m,
                    PrecioUnitario = 0m,
                    ImporteTotal = 999m,
                    FechaModificacion = new DateTime(2026, 1, 1)
                };
                context.ConceptosPresupuesto.Add(capituloExistente);
                context.SaveChanges();
                capituloExistenteId = capituloExistente.Id;

                command = new BudgetImportCommand(
                    proyectoId,
                    BudgetProjectCalculationBase.FromProyecto(scenario.Proyecto),
                    new[]
                    {
                        new BudgetImportRow(10, "CAP-02", "Capítulo importado", string.Empty, 0m, "Capitulo"),
                        new BudgetImportRow(11, "C-100", "Concepto importado A", "m3", 4.5m, "Concepto"),
                        new BudgetImportRow(12, "C-101", "Concepto importado B", "ml", 2m, "Concepto")
                    });

                var resultado = BudgetPersistenceService.ExecuteImportCommand(
                    new ProjectDbContextFactory(), dbPath, command);

                Assert.AreEqual(BudgetOperationStatus.Committed, resultado.Status, resultado.Message);
                Assert.IsTrue(resultado.IsCommitted);
                Assert.IsNull(resultado.Diagnostic);
                Assert.IsNotNull(resultado.Confirmed);

                var confirmado = resultado.Confirmed!;
                Assert.AreEqual(proyectoId, confirmado.ProjectId);
                Assert.AreEqual(3, confirmado.Rows.Count);
                Assert.AreEqual(3, resultado.Operation.ConfirmedConceptIds.Count);

                CollectionAssert.AreEqual(
                    new[] { 10, 11, 12 },
                    confirmado.Rows.Select(r => r.SourceRowNumber).ToArray());
                CollectionAssert.AreEqual(
                    new[] { 2, 3, 4 },
                    confirmado.Rows.Select(r => r.Orden).ToArray(),
                    "La importación arranca después del máximo Orden persistido.");
                CollectionAssert.AreEqual(
                    resultado.Operation.ConfirmedConceptIds.ToArray(),
                    confirmado.Rows.Select(r => r.ConceptId).ToArray(),
                    "Cada renglón confirmado viaja con el Id generado por la base.");

                Assert.IsTrue(confirmado.Rows.All(r => r.ConceptId > 0));
                Assert.AreEqual(3, confirmado.Rows.Select(r => r.ConceptId).Distinct().Count());

                var capituloImportado = confirmado.Rows[0];
                Assert.IsTrue(capituloImportado.EsAgrupador);
                Assert.AreEqual(0, capituloImportado.Nivel);
                Assert.AreEqual(string.Empty, capituloImportado.Unidad);
                Assert.AreEqual(0m, capituloImportado.Cantidad);

                var conceptoImportado = confirmado.Rows[1];
                Assert.IsFalse(conceptoImportado.EsAgrupador);
                Assert.AreEqual(5, conceptoImportado.Nivel);
                Assert.AreEqual(4.5m, conceptoImportado.Cantidad);
                Assert.AreEqual("m3", conceptoImportado.Unidad);
                Assert.AreEqual(0m, conceptoImportado.ImporteTotal,
                    "La importación no trae P.U. ni importe.");

                // El agrupador existente tenía un total obsoleto en la base (999); la
                // instantánea lo recalcula sobre la lista final (1,000 del concepto real).
                Assert.AreEqual(1000m,
                    confirmado.Aggregators.Single(a => a.ConceptId == capituloExistenteId).ImporteTotal,
                    "Los agrupadores se recalculan sobre la lista final, incluido el lote nuevo.");
                Assert.AreEqual(0m,
                    confirmado.Aggregators.Single(a => a.ConceptId == capituloImportado.ConceptId).ImporteTotal);

                // El contexto compartido de la prueba no recibe las filas ni muta lo cargado.
                Assert.IsFalse(context.ChangeTracker.HasChanges());
                Assert.AreEqual(EntityState.Unchanged, context.Entry(scenario.Concepto).State);
                Assert.AreEqual(999m, capituloExistente.ImporteTotal,
                    "El comando no modifica entidades tracked del contexto compartido.");
            }

            using (var verificacion = new SOPROContext(dbPath))
            {
                var persistidos = verificacion.ConceptosPresupuesto
                    .AsNoTracking()
                    .Where(c => c.ProyectoId == proyectoId)
                    .OrderBy(c => c.Orden)
                    .ToList();

                Assert.AreEqual(5, persistidos.Count);
                CollectionAssert.AreEqual(new[] { 0, 1, 2, 3, 4 }, persistidos.Select(c => c.Orden).ToArray());

                var importados = persistidos.Where(c => c.Clave.StartsWith("CAP-02") || c.Clave.StartsWith("C-10")).ToList();
                Assert.AreEqual(3, importados.Count);
                Assert.IsTrue(importados.All(c => c.Id > 0));
                Assert.IsTrue(importados.All(c => c.CostoDirectoUnitario == 0m));
                Assert.IsTrue(importados.All(c => c.PrecioUnitario == 0m));
                Assert.IsTrue(importados.All(c => c.ImporteTotal == 0m));

                Assert.AreEqual(1000m,
                    verificacion.ConceptosPresupuesto.AsNoTracking().Single(c => c.Id == capituloExistenteId).ImporteTotal,
                    "El ÚNICO SaveChanges persiste las inserciones y los totales recalculados de los agrupadores.");
                Assert.AreEqual(1000m,
                    verificacion.ConceptosPresupuesto.AsNoTracking().Single(c => c.Id == conceptoExistenteId).ImporteTotal);
            }
        }
        finally
        {
            DeleteQuietly(dbPath);
        }
    }

    [TestMethod]
    public void ExecuteImportCommand_FalloDeTrigger_NoEscribeNadaYElReintentoDelMismoComandoNoDuplica()
    {
        var dbPath = TestDbFactory.CreateTempDbPath();
        try
        {
            int proyectoId;
            int capituloExistenteId;
            BudgetImportCommand command;

            using (var context = TestDbFactory.CreateContextAt(dbPath))
            {
                var scenario = SoproCalculationScenarioBuilder.CreateBaseBudgetScenario(context);
                proyectoId = scenario.Proyecto.Id;

                var capituloExistente = new ConceptoPresupuesto
                {
                    ProyectoId = proyectoId,
                    Clave = "CAP-01",
                    Descripcion = "Capítulo existente",
                    EsAgrupador = true,
                    Nivel = 0,
                    Orden = 0,
                    Unidad = string.Empty,
                    ColumnasPersonalizadasJSON = string.Empty,
                    Notas = string.Empty,
                    Cantidad = 0m,
                    CostoDirectoUnitario = 0m,
                    CostoDirectoTotal = 999m,
                    PrecioUnitario = 0m,
                    ImporteTotal = 999m,
                    FechaModificacion = new DateTime(2026, 1, 1)
                };
                context.ConceptosPresupuesto.Add(capituloExistente);
                context.SaveChanges();
                capituloExistenteId = capituloExistente.Id;

                command = new BudgetImportCommand(
                    proyectoId,
                    BudgetProjectCalculationBase.FromProyecto(scenario.Proyecto),
                    new[]
                    {
                        new BudgetImportRow(1, "CAP-10", "Capítulo nuevo", string.Empty, 0m, "Capitulo"),
                        new BudgetImportRow(2, "C-200", "Concepto nuevo A", "m2", 1m, "Concepto"),
                        new BudgetImportRow(3, "C-201", "Concepto nuevo B", "m2", 2m, "Concepto")
                    });

                context.Database.ExecuteSqlRaw(
                    "CREATE TRIGGER trg_fail_import BEFORE INSERT ON ConceptosPresupuesto " +
                    "BEGIN SELECT RAISE(ABORT, 'fallo_simulado_import'); END;");

                var primerIntento = BudgetPersistenceService.ExecuteImportCommand(
                    new ProjectDbContextFactory(), dbPath, command);

                Assert.AreEqual(BudgetOperationStatus.Failed, primerIntento.Status, primerIntento.Message);
                Assert.IsNull(primerIntento.Confirmed);
                Assert.AreEqual(0, primerIntento.Operation.ConfirmedConceptIds.Count);
                Assert.IsNotNull(primerIntento.Diagnostic);
                StringAssert.Contains(primerIntento.Diagnostic!, "fallo_simulado_import");
                Assert.IsFalse((primerIntento.Message ?? string.Empty).Contains(
                    "fallo_simulado_import", StringComparison.Ordinal),
                    "El mensaje al usuario no expone detalles técnicos.");

                // Todo o nada: ninguna fila del lote quedó en la base y el total del
                // agrupador que iba a recalcularse en la misma escritura también revirtió.
                Assert.AreEqual(2, context.ConceptosPresupuesto.Count(c => c.ProyectoId == proyectoId));
                Assert.AreEqual(999m,
                    context.ConceptosPresupuesto
                        .AsNoTracking()
                        .Single(c => c.Id == capituloExistenteId)
                        .ImporteTotal,
                    "El fallo revierte inserciones y totales de agrupadores en la misma escritura.");

                context.Database.ExecuteSqlRaw("DROP TRIGGER trg_fail_import;");

                var reintento = BudgetPersistenceService.ExecuteImportCommand(
                    new ProjectDbContextFactory(), dbPath, command);

                Assert.AreEqual(BudgetOperationStatus.Committed, reintento.Status, reintento.Message);
                Assert.IsNotNull(reintento.Confirmed);
                Assert.AreEqual(3, reintento.Confirmed!.Rows.Count);
                Assert.AreEqual(3, reintento.Confirmed.Rows.Select(r => r.ConceptId).Distinct().Count());
                Assert.IsTrue(reintento.Confirmed.Rows.All(r => r.ConceptId > 0));

                CollectionAssert.AreEquivalent(
                    new[] { 0, 1, 2 },
                    reintento.Confirmed.Rows.Select(r => r.Orden - 2).ToArray(),
                    "El reintento asignó posiciones contiguas después de la fila existente.");
            }

            using (var verificacion = new SOPROContext(dbPath))
            {
                var persistidos = verificacion.ConceptosPresupuesto
                    .AsNoTracking()
                    .Where(c => c.ProyectoId == proyectoId)
                    .ToList();

                Assert.AreEqual(5, persistidos.Count,
                    "El fallo revirtió todo el lote y el reintento insertó una sola vez.");
                Assert.AreEqual(1, persistidos.Count(c => c.Clave == "CAP-10"));
                Assert.AreEqual(1, persistidos.Count(c => c.Clave == "C-200"));
                Assert.AreEqual(1, persistidos.Count(c => c.Clave == "C-201"));
                CollectionAssert.AreEquivalent(new[] { 0, 1, 2, 3, 4 }, persistidos.Select(c => c.Orden).ToArray());
                Assert.AreEqual(1000m,
                    persistidos.Single(c => c.Id == capituloExistenteId).ImporteTotal,
                    "El reintento confirma inserciones y recálculo de agrupadores en la misma escritura.");
            }
        }
        finally
        {
            DeleteQuietly(dbPath);
        }
    }

    [TestMethod]
    [DataRow("PorcentajeUtilidad")]
    [DataRow("DecimalesImporte")]
    public void ExecuteImportCommand_ProyectoCambioSuBaseDeCalculo_DevuelveConflictoSinEscribir(string parametro)
    {
        var dbPath = TestDbFactory.CreateTempDbPath();
        try
        {
            int proyectoId;
            BudgetImportCommand command;

            using (var context = TestDbFactory.CreateContextAt(dbPath))
            {
                var scenario = SoproCalculationScenarioBuilder.CreateBaseBudgetScenario(context);
                proyectoId = scenario.Proyecto.Id;

                command = new BudgetImportCommand(
                    proyectoId,
                    BudgetProjectCalculationBase.FromProyecto(scenario.Proyecto),
                    new[] { new BudgetImportRow(1, "C-300", "Concepto importado", "m2", 1m, "Concepto") });

                using (var otroModulo = new SOPROContext(dbPath))
                {
                    var proyecto = otroModulo.Proyectos.Single(p => p.Id == proyectoId);
                    switch (parametro)
                    {
                        case "PorcentajeUtilidad":
                            proyecto.PorcentajeUtilidad += 5m;
                            break;
                        case "DecimalesImporte":
                            proyecto.DecimalesImporte -= 1;
                            break;
                        default:
                            Assert.Fail($"Parámetro no soportado por la prueba: {parametro}");
                            break;
                    }

                    otroModulo.SaveChanges();
                }

                var resultado = BudgetPersistenceService.ExecuteImportCommand(
                    new ProjectDbContextFactory(), dbPath, command);

                Assert.AreEqual(BudgetOperationStatus.Conflict, resultado.Status, resultado.Message);
                Assert.IsNull(resultado.Confirmed);
                Assert.AreEqual(0, resultado.Operation.ConfirmedConceptIds.Count);
                Assert.IsFalse(string.IsNullOrWhiteSpace(resultado.Message));
                Assert.IsFalse((resultado.Message ?? string.Empty).Contains(dbPath, StringComparison.Ordinal));
            }

            using (var verificacion = new SOPROContext(dbPath))
            {
                Assert.AreEqual(1, verificacion.ConceptosPresupuesto.Count(c => c.ProyectoId == proyectoId),
                    "Un conflicto de base no inserta ninguna fila.");
            }
        }
        finally
        {
            DeleteQuietly(dbPath);
        }
    }

    [TestMethod]
    public void BudgetImportCommand_LoteVacioOFilaInvalida_SeRechazaAntesDeEjecutar()
    {
        var expectedProject = BudgetProjectCalculationBase.FromProyecto(new Proyecto
        {
            Nombre = "Proyecto validación",
            Descripcion = string.Empty,
            Ubicacion = string.Empty,
            Convocante = string.Empty,
            Contratista = string.Empty,
            ApoderadoLegal = string.Empty,
            FechaInicio = new DateTime(2026, 1, 1),
            FechaTermino = new DateTime(2026, 1, 31),
            PlazoEjecucion = 31
        });

        Assert.ThrowsExactly<ArgumentException>(() =>
            new BudgetImportCommand(1, expectedProject, Array.Empty<BudgetImportRow>()));

        Assert.ThrowsExactly<ArgumentException>(() =>
            new BudgetImportRow(1, "   ", "   ", string.Empty, 0m, "Concepto"));

        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() =>
            new BudgetImportRow(1, "C-1", "Concepto", "m2", -1m, "Concepto"));

        Assert.ThrowsExactly<ArgumentException>(() =>
            new BudgetImportRow(1, "C-1", "Concepto", "m2", 1m, "Agrupador inventado"));
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
