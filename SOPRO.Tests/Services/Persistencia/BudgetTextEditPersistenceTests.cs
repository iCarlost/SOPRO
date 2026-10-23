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
/// Edición de Descripción/Unidad de un concepto existente como comando explícito
/// (ADR-0001): contexto nuevo por operación, validación del valor esperado y de la
/// pertenencia al proyecto, y una sola escritura sin recalcular importes.
/// </summary>
[TestClass]
public class BudgetTextEditPersistenceTests
{
    [TestMethod]
    [DataRow(BudgetTextEditField.Descripcion, "Descripción editada")]
    [DataRow(BudgetTextEditField.Unidad, "ml")]
    public void ExecuteTextEditCommand_CaminoOk_EscribeSoloElCampoYNoTocaImportes(
        BudgetTextEditField campo,
        string propuesta)
    {
        var dbPath = TestDbFactory.CreateTempDbPath();
        try
        {
            int proyectoId;
            int conceptoId;
            string valorPrevio;
            DateTime fechaPrevia;

            using (var context = TestDbFactory.CreateContextAt(dbPath))
            {
                var scenario = SoproCalculationScenarioBuilder.CreateBaseBudgetScenario(context);
                proyectoId = scenario.Proyecto.Id;
                conceptoId = scenario.Concepto.Id;
                valorPrevio = campo == BudgetTextEditField.Descripcion
                    ? scenario.Concepto.Descripcion
                    : scenario.Concepto.Unidad;
                fechaPrevia = scenario.Concepto.FechaModificacion;

                var command = new BudgetTextEditCommand(
                    proyectoId, conceptoId, campo, valorPrevio, propuesta);

                var result = BudgetPersistenceService.ExecuteTextEditCommand(
                    new ProjectDbContextFactory(), dbPath, command);

                Assert.AreEqual(BudgetOperationStatus.Committed, result.Status, result.Message);
                Assert.IsTrue(result.IsCommitted);
                Assert.IsNull(result.Diagnostic, "Un camino confirmado no deja diagnóstico de error.");
                Assert.AreEqual(1, result.Operation.ConfirmedConceptIds.Count);
                Assert.AreEqual(conceptoId, result.Operation.ConfirmedConceptIds[0]);

                Assert.IsNotNull(result.Confirmed);
                Assert.AreEqual(conceptoId, result.Confirmed!.ConceptId);
                Assert.AreEqual(campo, result.Confirmed.Field);
                Assert.AreEqual(propuesta, result.Confirmed.Value);

                // El contexto compartido del formulario no escribe el presupuesto.
                Assert.AreEqual(valorPrevio,
                    campo == BudgetTextEditField.Descripcion
                        ? scenario.Concepto.Descripcion
                        : scenario.Concepto.Unidad,
                    "El comando no puede modificar entidades tracked del contexto compartido.");
                Assert.AreEqual(EntityState.Unchanged, context.Entry(scenario.Concepto).State);
            }

            using (var verificacion = new SOPROContext(dbPath))
            {
                var persistido = verificacion.ConceptosPresupuesto
                    .AsNoTracking()
                    .Single(c => c.Id == conceptoId);

                Assert.AreEqual(propuesta,
                    campo == BudgetTextEditField.Descripcion
                        ? persistido.Descripcion
                        : persistido.Unidad,
                    "Solo el campo editado cambia en la base.");
                Assert.AreEqual(campo == BudgetTextEditField.Unidad ? propuesta : "m3",
                    persistido.Unidad, "La unidad no se toca al editar la descripción.");
                Assert.AreEqual(campo == BudgetTextEditField.Descripcion ? propuesta : "Concreto simple en obra",
                    persistido.Descripcion, "La descripción no se toca al editar la unidad.");
                Assert.AreEqual(10m, persistido.Cantidad);
                Assert.AreEqual(100m, persistido.CostoDirectoUnitario);
                Assert.AreEqual(1000m, persistido.CostoDirectoTotal);
                Assert.AreEqual(100m, persistido.PrecioUnitario);
                Assert.AreEqual(1000m, persistido.ImporteTotal,
                    "Una edición de texto no recalcula importes.");
                Assert.IsTrue(persistido.FechaModificacion >= fechaPrevia,
                    "Se sella FechaModificacion sin retroceder la existente.");
            }
        }
        finally
        {
            DeleteQuietly(dbPath);
        }
    }

    [TestMethod]
    public void ExecuteTextEditCommand_CampoCambioEnLaBase_DevuelveConflictoSinEscribir()
    {
        var dbPath = TestDbFactory.CreateTempDbPath();
        try
        {
            int conceptoId;
            BudgetTextEditCommand command;

            using (var context = TestDbFactory.CreateContextAt(dbPath))
            {
                var scenario = SoproCalculationScenarioBuilder.CreateBaseBudgetScenario(context);
                conceptoId = scenario.Concepto.Id;

                command = new BudgetTextEditCommand(
                    scenario.Proyecto.Id,
                    conceptoId,
                    BudgetTextEditField.Descripcion,
                    scenario.Concepto.Descripcion,
                    "Descripción local");

                using (var otroModulo = new SOPROContext(dbPath))
                {
                    var concepto = otroModulo.ConceptosPresupuesto.Single(c => c.Id == conceptoId);
                    concepto.Descripcion = "Descripción de otro módulo";
                    otroModulo.SaveChanges();
                }

                var result = BudgetPersistenceService.ExecuteTextEditCommand(
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
                    .AsNoTracking().Single(c => c.Id == conceptoId);

                Assert.AreEqual("Descripción de otro módulo", persistido.Descripcion,
                    "Un conflicto no escribe: queda el valor ajeno, no el propuesto.");
            }
        }
        finally
        {
            DeleteQuietly(dbPath);
        }
    }

    [TestMethod]
    public void ExecuteTextEditCommand_ConceptoDeOtroProyecto_DevuelveConflictoSinEscribir()
    {
        var dbPath = TestDbFactory.CreateTempDbPath();
        try
        {
            int conceptoAjenoId;
            BudgetTextEditCommand command;

            using (var context = TestDbFactory.CreateContextAt(dbPath))
            {
                var scenario = SoproCalculationScenarioBuilder.CreateBaseBudgetScenario(context);

                var otroProyecto = new Proyecto
                {
                    Nombre = "Otro proyecto",
                    Descripcion = string.Empty,
                    Ubicacion = string.Empty,
                    Convocante = string.Empty,
                    Contratista = string.Empty,
                    ApoderadoLegal = string.Empty,
                    FechaInicio = new DateTime(2026, 1, 1),
                    FechaTermino = new DateTime(2026, 1, 31),
                    PlazoEjecucion = 31
                };
                context.Proyectos.Add(otroProyecto);
                context.SaveChanges();

                var conceptoAjeno = new ConceptoPresupuesto
                {
                    ProyectoId = otroProyecto.Id,
                    Clave = "AJENO-1",
                    Descripcion = "Concepto de otro proyecto",
                    Unidad = "pza",
                    Cantidad = 3m,
                    CostoDirectoUnitario = 10m,
                    CostoDirectoTotal = 30m,
                    PrecioUnitario = 10m,
                    ImporteTotal = 30m,
                    Nivel = 1,
                    Orden = 1,
                    ColumnasPersonalizadasJSON = string.Empty,
                    Notas = string.Empty
                };
                context.ConceptosPresupuesto.Add(conceptoAjeno);
                context.SaveChanges();
                conceptoAjenoId = conceptoAjeno.Id;

                // El comando apunta al proyecto de la vista pero a una fila ajena.
                command = new BudgetTextEditCommand(
                    scenario.Proyecto.Id,
                    conceptoAjenoId,
                    BudgetTextEditField.Descripcion,
                    conceptoAjeno.Descripcion,
                    "Texto no permitido");

                var result = BudgetPersistenceService.ExecuteTextEditCommand(
                    new ProjectDbContextFactory(), dbPath, command);

                Assert.AreEqual(BudgetOperationStatus.Conflict, result.Status, result.Message);
                Assert.IsNull(result.Confirmed);
            }

            using (var verificacion = new SOPROContext(dbPath))
            {
                var ajeno = verificacion.ConceptosPresupuesto
                    .AsNoTracking().Single(c => c.Id == conceptoAjenoId);
                Assert.AreEqual("Concepto de otro proyecto", ajeno.Descripcion);
            }
        }
        finally
        {
            DeleteQuietly(dbPath);
        }
    }

    [TestMethod]
    public void ExecuteTextEditCommand_FalloDeTrigger_NoSeConfirmaPorUnGuardadoAjeno_YElReintentoConfirma()
    {
        var dbPath = TestDbFactory.CreateTempDbPath();
        try
        {
            int conceptoId;
            int proyectoId;
            BudgetTextEditCommand command;

            using (var context = TestDbFactory.CreateContextAt(dbPath))
            {
                var scenario = SoproCalculationScenarioBuilder.CreateBaseBudgetScenario(context);
                conceptoId = scenario.Concepto.Id;
                proyectoId = scenario.Proyecto.Id;

                command = new BudgetTextEditCommand(
                    proyectoId,
                    conceptoId,
                    BudgetTextEditField.Descripcion,
                    scenario.Concepto.Descripcion,
                    "Descripción que no debe persistirse todavía");

                context.Database.ExecuteSqlRaw(
                    "CREATE TRIGGER trg_fail_concepto BEFORE UPDATE ON ConceptosPresupuesto " +
                    "BEGIN SELECT RAISE(ABORT, 'fallo_simulado'); END;");

                var primerIntento = BudgetPersistenceService.ExecuteTextEditCommand(
                    new ProjectDbContextFactory(), dbPath, command);

                Assert.AreEqual(BudgetOperationStatus.Failed, primerIntento.Status,
                    primerIntento.Message);
                Assert.IsNull(primerIntento.Confirmed);
                Assert.IsNotNull(primerIntento.Diagnostic);
                StringAssert.Contains(primerIntento.Diagnostic!, "fallo_simulado",
                    "El diagnóstico interno conserva el detalle de la base.");
                Assert.IsFalse((primerIntento.Message ?? string.Empty).Contains(
                    "fallo_simulado", StringComparison.Ordinal),
                    "El mensaje al usuario no expone detalles técnicos.");

                context.Database.ExecuteSqlRaw("DROP TRIGGER trg_fail_concepto;");

                // Guardado ajeno posterior: no puede arrastrar el texto fallido.
                scenario.Proyecto.Descripcion = "cambio ajeno posterior al fallo";
                context.SaveChanges();

                var reintento = BudgetPersistenceService.ExecuteTextEditCommand(
                    new ProjectDbContextFactory(), dbPath, command);

                Assert.AreEqual(BudgetOperationStatus.Committed, reintento.Status,
                    reintento.Message);
                Assert.AreEqual(command.ProposedValue, reintento.Confirmed!.Value);
            }

            using (var verificacion = new SOPROContext(dbPath))
            {
                var proyecto = verificacion.Proyectos
                    .AsNoTracking().Single(p => p.Id == proyectoId);
                var concepto = verificacion.ConceptosPresupuesto
                    .AsNoTracking().Single(c => c.Id == conceptoId);

                Assert.AreEqual("cambio ajeno posterior al fallo", proyecto.Descripcion,
                    "El guardado ajeno se completa y es independiente del presupuesto.");
                Assert.AreEqual("Descripción que no debe persistirse todavía", concepto.Descripcion,
                    "El reintento guarda el texto; el fallo anterior no se confirmó por el guardado ajeno.");
                Assert.AreEqual(1000m, concepto.ImporteTotal,
                    "La edición de texto no altera importes.");
            }
        }
        finally
        {
            DeleteQuietly(dbPath);
        }
    }

    [TestMethod]
    public void ExecuteTextEditCommand_RepetirComandoConfirmado_DevuelveConflictoSinReescribir()
    {
        var dbPath = TestDbFactory.CreateTempDbPath();
        try
        {
            int conceptoId;
            BudgetTextEditCommand command;
            DateTime fechaConfirmada;

            using (var context = TestDbFactory.CreateContextAt(dbPath))
            {
                var scenario = SoproCalculationScenarioBuilder.CreateBaseBudgetScenario(context);
                conceptoId = scenario.Concepto.Id;

                command = new BudgetTextEditCommand(
                    scenario.Proyecto.Id,
                    conceptoId,
                    BudgetTextEditField.Unidad,
                    scenario.Concepto.Unidad,
                    "ml");

                var primero = BudgetPersistenceService.ExecuteTextEditCommand(
                    new ProjectDbContextFactory(), dbPath, command);
                Assert.AreEqual(BudgetOperationStatus.Committed, primero.Status, primero.Message);
                fechaConfirmada = primero.Confirmed!.FechaModificacion;

                var segundo = BudgetPersistenceService.ExecuteTextEditCommand(
                    new ProjectDbContextFactory(), dbPath, command);
                Assert.AreEqual(BudgetOperationStatus.Conflict, segundo.Status, segundo.Message);
                Assert.IsNull(segundo.Confirmed);
            }

            using (var verificacion = new SOPROContext(dbPath))
            {
                var concepto = verificacion.ConceptosPresupuesto
                    .AsNoTracking().Single(c => c.Id == conceptoId);

                Assert.AreEqual("ml", concepto.Unidad);
                Assert.AreEqual(fechaConfirmada, concepto.FechaModificacion,
                    "Reejecutar un comando confirmado no reescribe la fila.");
            }
        }
        finally
        {
            DeleteQuietly(dbPath);
        }
    }

    [TestMethod]
    public void ExecuteTextEditCommand_Observaciones_EscribeNotasTalCualEnUnaSolaEscrituraSinTocarImportesNiFilasAjenas()
    {
        var dbPath = TestDbFactory.CreateTempDbPath();
        try
        {
            const string notaPrevia = "Nota previa";
            const string propuesta = "  Observación editada  ";
            int proyectoId;
            int conceptoId;
            int agrupadorId;
            DateTime fechaPrevia;
            DateTime fechaAgrupadorPrevia;

            using (var context = TestDbFactory.CreateContextAt(dbPath))
            {
                var scenario = SoproCalculationScenarioBuilder.CreateBaseBudgetScenario(context);
                proyectoId = scenario.Proyecto.Id;
                conceptoId = scenario.Concepto.Id;

                var agrupador = new ConceptoPresupuesto
                {
                    ProyectoId = proyectoId,
                    Clave = "CAP-01",
                    Descripcion = "Capítulo ajeno a la edición",
                    Unidad = string.Empty,
                    Cantidad = 0m,
                    CostoDirectoTotal = 1000m,
                    ImporteTotal = 1000m,
                    Nivel = 0,
                    Orden = 0,
                    EsAgrupador = true,
                    ColumnasPersonalizadasJSON = string.Empty,
                    Notas = "Nota del agrupador"
                };
                context.ConceptosPresupuesto.Add(agrupador);
                context.SaveChanges();
                agrupadorId = agrupador.Id;

                scenario.Concepto.Notas = notaPrevia;
                context.SaveChanges();
                fechaPrevia = scenario.Concepto.FechaModificacion;
                fechaAgrupadorPrevia = agrupador.FechaModificacion;

                // Auditoría de filas realmente actualizadas: permite comprobar que el
                // comando hace UNA sola escritura y solo sobre la fila objetivo.
                context.Database.ExecuteSqlRaw(
                    "CREATE TABLE auditoria_texto (ConceptoId INTEGER NOT NULL);");
                context.Database.ExecuteSqlRaw(
                    "CREATE TRIGGER trg_auditoria_concepto AFTER UPDATE ON ConceptosPresupuesto " +
                    "BEGIN INSERT INTO auditoria_texto (ConceptoId) VALUES (NEW.Id); END;");

                var command = new BudgetTextEditCommand(
                    proyectoId,
                    conceptoId,
                    BudgetTextEditField.Observaciones,
                    notaPrevia,
                    propuesta);

                var result = BudgetPersistenceService.ExecuteTextEditCommand(
                    new ProjectDbContextFactory(), dbPath, command);

                Assert.AreEqual(BudgetOperationStatus.Committed, result.Status, result.Message);
                Assert.IsNull(result.Diagnostic, "Un camino confirmado no deja diagnóstico de error.");
                Assert.AreEqual(BudgetTextEditField.Observaciones, result.Confirmed!.Field);
                Assert.AreEqual(propuesta, result.Confirmed.Value,
                    "Las Observaciones se confirman sin recortar ni normalizar.");
                Assert.AreEqual(1, result.Operation.ConfirmedConceptIds.Count);
                Assert.AreEqual(conceptoId, result.Operation.ConfirmedConceptIds[0]);

                var filasAuditadas = context.Database
                    .SqlQueryRaw<int>("SELECT ConceptoId AS Value FROM auditoria_texto")
                    .ToList();
                Assert.AreEqual(1, filasAuditadas.Count,
                    "El comando debe ejecutar una sola escritura.");
                Assert.AreEqual(conceptoId, filasAuditadas[0],
                    "La única fila escrita es el concepto editado.");

                // El contexto compartido del formulario no escribe la nota.
                Assert.AreEqual(notaPrevia, scenario.Concepto.Notas,
                    "El comando no puede modificar entidades tracked del contexto compartido.");
                Assert.AreEqual(EntityState.Unchanged, context.Entry(scenario.Concepto).State);
            }

            using (var verificacion = new SOPROContext(dbPath))
            {
                var persistido = verificacion.ConceptosPresupuesto
                    .AsNoTracking().Single(c => c.Id == conceptoId);

                Assert.AreEqual(propuesta, persistido.Notas,
                    "Solo las Observaciones (Notas) cambian en la base.");
                Assert.AreEqual("Concreto simple en obra", persistido.Descripcion);
                Assert.AreEqual("m3", persistido.Unidad);
                Assert.AreEqual(10m, persistido.Cantidad);
                Assert.AreEqual(100m, persistido.CostoDirectoUnitario);
                Assert.AreEqual(1000m, persistido.CostoDirectoTotal);
                Assert.AreEqual(100m, persistido.PrecioUnitario);
                Assert.AreEqual(1000m, persistido.ImporteTotal,
                    "Una edición de texto no recalcula importes.");
                Assert.IsTrue(persistido.FechaModificacion >= fechaPrevia,
                    "Se sella FechaModificacion sin retroceder la existente.");

                var agrupador = verificacion.ConceptosPresupuesto
                    .AsNoTracking().Single(c => c.Id == agrupadorId);
                Assert.AreEqual("Nota del agrupador", agrupador.Notas,
                    "Las filas ajenas quedan intactas.");
                Assert.AreEqual(fechaAgrupadorPrevia, agrupador.FechaModificacion,
                    "Una fila ajena no se marca como modificada.");

                var proyecto = verificacion.Proyectos
                    .AsNoTracking().Single(p => p.Id == proyectoId);
                var display = BudgetLoadService.BuildRowDisplay(proyecto, persistido);
                Assert.AreEqual(propuesta, display.ValuesByInternalName["Observaciones"],
                    "La carga de la fila expone Notas como Observaciones.");
            }
        }
        finally
        {
            DeleteQuietly(dbPath);
        }
    }

    [TestMethod]
    public void ExecuteTextEditCommand_ObservacionesCambioEnLaBase_DevuelveConflictoSinEscribir()
    {
        var dbPath = TestDbFactory.CreateTempDbPath();
        try
        {
            int conceptoId;
            BudgetTextEditCommand command;

            using (var context = TestDbFactory.CreateContextAt(dbPath))
            {
                var scenario = SoproCalculationScenarioBuilder.CreateBaseBudgetScenario(context);
                conceptoId = scenario.Concepto.Id;

                scenario.Concepto.Notas = "Nota que la vista cargó";
                context.SaveChanges();

                command = new BudgetTextEditCommand(
                    scenario.Proyecto.Id,
                    conceptoId,
                    BudgetTextEditField.Observaciones,
                    "Nota que la vista cargó",
                    "Nota local");

                using (var otroModulo = new SOPROContext(dbPath))
                {
                    var concepto = otroModulo.ConceptosPresupuesto.Single(c => c.Id == conceptoId);
                    concepto.Notas = "Nota de otro módulo";
                    otroModulo.SaveChanges();
                }

                var result = BudgetPersistenceService.ExecuteTextEditCommand(
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
                    .AsNoTracking().Single(c => c.Id == conceptoId);

                Assert.AreEqual("Nota de otro módulo", persistido.Notas,
                    "Un conflicto no escribe: queda la nota ajena, no la propuesta.");
            }
        }
        finally
        {
            DeleteQuietly(dbPath);
        }
    }

    [TestMethod]
    public void ExecuteTextEditCommand_ObservacionesFalloDeTrigger_RevierteYElReintentoConfirma()
    {
        var dbPath = TestDbFactory.CreateTempDbPath();
        try
        {
            int conceptoId;
            int proyectoId;
            BudgetTextEditCommand command;

            using (var context = TestDbFactory.CreateContextAt(dbPath))
            {
                var scenario = SoproCalculationScenarioBuilder.CreateBaseBudgetScenario(context);
                conceptoId = scenario.Concepto.Id;
                proyectoId = scenario.Proyecto.Id;

                command = new BudgetTextEditCommand(
                    proyectoId,
                    conceptoId,
                    BudgetTextEditField.Observaciones,
                    scenario.Concepto.Notas,
                    "Nota que no debe persistirse todavía");

                context.Database.ExecuteSqlRaw(
                    "CREATE TRIGGER trg_fail_concepto BEFORE UPDATE ON ConceptosPresupuesto " +
                    "BEGIN SELECT RAISE(ABORT, 'fallo_simulado'); END;");

                var primerIntento = BudgetPersistenceService.ExecuteTextEditCommand(
                    new ProjectDbContextFactory(), dbPath, command);

                Assert.AreEqual(BudgetOperationStatus.Failed, primerIntento.Status,
                    primerIntento.Message);
                Assert.IsNull(primerIntento.Confirmed);
                Assert.IsNotNull(primerIntento.Diagnostic);
                StringAssert.Contains(primerIntento.Diagnostic!, "fallo_simulado",
                    "El diagnóstico interno conserva el detalle de la base.");
                Assert.IsFalse((primerIntento.Message ?? string.Empty).Contains(
                    "fallo_simulado", StringComparison.Ordinal),
                    "El mensaje al usuario no expone detalles técnicos.");

                context.Database.ExecuteSqlRaw("DROP TRIGGER trg_fail_concepto;");

                // Guardado ajeno posterior: no puede arrastrar la nota fallida.
                scenario.Proyecto.Descripcion = "cambio ajeno posterior al fallo";
                context.SaveChanges();

                var reintento = BudgetPersistenceService.ExecuteTextEditCommand(
                    new ProjectDbContextFactory(), dbPath, command);

                Assert.AreEqual(BudgetOperationStatus.Committed, reintento.Status,
                    reintento.Message);
                Assert.AreEqual(command.ProposedValue, reintento.Confirmed!.Value);
            }

            using (var verificacion = new SOPROContext(dbPath))
            {
                var proyecto = verificacion.Proyectos
                    .AsNoTracking().Single(p => p.Id == proyectoId);
                var concepto = verificacion.ConceptosPresupuesto
                    .AsNoTracking().Single(c => c.Id == conceptoId);

                Assert.AreEqual("cambio ajeno posterior al fallo", proyecto.Descripcion,
                    "El guardado ajeno se completa y es independiente del presupuesto.");
                Assert.AreEqual("Nota que no debe persistirse todavía", concepto.Notas,
                    "El reintento guarda la nota; el fallo anterior no se confirmó por el guardado ajeno.");
                Assert.AreEqual(1000m, concepto.ImporteTotal,
                    "La edición de texto no altera importes.");
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
