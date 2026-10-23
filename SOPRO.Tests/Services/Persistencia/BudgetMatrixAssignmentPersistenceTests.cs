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
/// Asignación de una matriz APU a un concepto YA existente como comando explícito
/// (ADR-0001): contexto nuevo por operación, bases esperadas de proyecto y concepto,
/// recarga de la matriz, aritmética canónica, recálculo de agrupadores sin guardar y
/// una sola escritura.
/// </summary>
[TestClass]
public class BudgetMatrixAssignmentPersistenceTests
{
    [TestMethod]
    public void ExecuteMatrixAssignmentCommand_CaminoOk_ActualizaConceptoRecalculaAgrupadoresYNoTocaElContextoCompartido()
    {
        var dbPath = TestDbFactory.CreateTempDbPath();
        try
        {
            int proyectoId;
            int conceptoId;
            int matrizDestinoId;
            int agrupadorId;
            DateTime fechaModificacionPrevia;

            using (var context = TestDbFactory.CreateContextAt(dbPath))
            {
                var scenario = SoproCalculationScenarioBuilder.CreateBaseBudgetScenario(context);
                proyectoId = scenario.Proyecto.Id;
                conceptoId = scenario.Concepto.Id;
                fechaModificacionPrevia = scenario.Concepto.FechaModificacion;

                var matrizDestino = AddMatrix(
                    context, proyectoId, "APU-002", "Acero de refuerzo", "kg", 50m);
                matrizDestinoId = matrizDestino.Id;

                var agrupador = AddAggregator(context, proyectoId, importeInicial: 1000m);
                agrupadorId = agrupador.Id;

                scenario.Concepto.Notas = "nota original";
                context.SaveChanges();

                var command = Command(scenario.Proyecto, scenario.Concepto, matrizDestino.Id);

                var result = BudgetPersistenceService.ExecuteMatrixAssignmentCommand(
                    new ProjectDbContextFactory(), dbPath, command);

                Assert.AreEqual(BudgetOperationStatus.Committed, result.Status, result.Message);
                Assert.IsTrue(result.IsCommitted);
                Assert.IsNull(result.Diagnostic, "Un camino confirmado no deja diagnóstico de error.");
                Assert.AreEqual(1, result.Operation.ConfirmedConceptIds.Count);
                Assert.AreEqual(conceptoId, result.Operation.ConfirmedConceptIds[0]);

                Assert.IsNotNull(result.Confirmed);
                var confirmado = result.Confirmed!;
                Assert.AreEqual(conceptoId, confirmado.ConceptId);
                Assert.AreEqual("APU-002", confirmado.Clave);
                Assert.AreEqual("Acero de refuerzo", confirmado.Descripcion);
                Assert.AreEqual("kg", confirmado.Unidad);
                Assert.AreEqual(10m, confirmado.Cantidad);
                Assert.AreEqual(50m, confirmado.CostoDirectoUnitario);
                Assert.AreEqual(500m, confirmado.CostoDirectoTotal,
                    "El costo directo total usa la aritmética canónica del borrador.");
                Assert.AreEqual(50m, confirmado.PrecioUnitario);
                Assert.AreEqual(500m, confirmado.ImporteTotal);
                Assert.AreEqual(matrizDestinoId, confirmado.MatrizId);
                Assert.IsTrue(confirmado.FechaModificacion >= fechaModificacionPrevia,
                    "Se sella FechaModificacion sin retroceder la existente.");

                var agrupadorConfirmado = confirmado.Aggregators.Single(a => a.ConceptId == agrupadorId);
                Assert.AreEqual(500m, agrupadorConfirmado.ImporteTotal,
                    "El agrupador confirmado suma el importe de la hoja asignada.");
                Assert.AreEqual(500m, agrupadorConfirmado.CostoDirectoTotal,
                    "El agrupador guarda el mismo total en ambas columnas.");

                // El contexto compartido de la vista no escribe el presupuesto.
                Assert.AreEqual("C-001", scenario.Concepto.Clave,
                    "El comando no puede modificar entidades tracked del contexto compartido.");
                Assert.AreEqual(scenario.MatrizApu.Id, scenario.Concepto.MatrizId);
                Assert.AreEqual(1000m, agrupador.ImporteTotal,
                    "El comando no recalcula entidades del contexto compartido.");
                Assert.AreEqual(EntityState.Unchanged, context.Entry(scenario.Concepto).State);
                Assert.AreEqual(EntityState.Unchanged, context.Entry(agrupador).State);
            }

            using (var verificacion = new SOPROContext(dbPath))
            {
                var persistido = verificacion.ConceptosPresupuesto
                    .AsNoTracking()
                    .Single(c => c.Id == conceptoId);

                Assert.AreEqual("APU-002", persistido.Clave);
                Assert.AreEqual("Acero de refuerzo", persistido.Descripcion);
                Assert.AreEqual("kg", persistido.Unidad);
                Assert.AreEqual(10m, persistido.Cantidad);
                Assert.AreEqual(50m, persistido.CostoDirectoUnitario);
                Assert.AreEqual(500m, persistido.CostoDirectoTotal);
                Assert.AreEqual(50m, persistido.PrecioUnitario);
                Assert.AreEqual(500m, persistido.ImporteTotal);
                Assert.AreEqual(matrizDestinoId, persistido.MatrizId);
                Assert.AreEqual(1, persistido.Orden, "La asignación no reordena la fila.");
                Assert.AreEqual("nota original", persistido.Notas,
                    "La asignación no pisa columnas ajenas al borrador.");

                var agrupadorPersistido = verificacion.ConceptosPresupuesto
                    .AsNoTracking()
                    .Single(c => c.Id == agrupadorId);
                Assert.AreEqual(500m, agrupadorPersistido.ImporteTotal);
                Assert.AreEqual(500m, agrupadorPersistido.CostoDirectoTotal);
            }
        }
        finally
        {
            DeleteQuietly(dbPath);
        }
    }

    [TestMethod]
    [DataRow("Clave")]
    [DataRow("Cantidad")]
    [DataRow("Descripcion")]
    public void ExecuteMatrixAssignmentCommand_ConceptoCambioEnLaBase_DevuelveConflictoSinEscribir(string campo)
    {
        var dbPath = TestDbFactory.CreateTempDbPath();
        try
        {
            int conceptoId;
            int matrizDestinoId;
            BudgetMatrixAssignmentCommand command;

            using (var context = TestDbFactory.CreateContextAt(dbPath))
            {
                var scenario = SoproCalculationScenarioBuilder.CreateBaseBudgetScenario(context);
                conceptoId = scenario.Concepto.Id;
                matrizDestinoId = AddMatrix(
                    context, scenario.Proyecto.Id, "APU-002", "Acero de refuerzo", "kg", 50m).Id;

                command = Command(scenario.Proyecto, scenario.Concepto, matrizDestinoId);

                // La misma fila cambia en la base después de cargar la vista.
                using (var otroModulo = new SOPROContext(dbPath))
                {
                    var concepto = otroModulo.ConceptosPresupuesto.Single(c => c.Id == conceptoId);
                    switch (campo)
                    {
                        case "Clave":
                            concepto.Clave = "C-EDITADA";
                            break;
                        case "Cantidad":
                            concepto.Cantidad = 12m;
                            break;
                        case "Descripcion":
                            concepto.Descripcion = "Descripción de otro módulo";
                            break;
                        default:
                            Assert.Fail($"Campo no soportado por la prueba: {campo}");
                            break;
                    }

                    otroModulo.SaveChanges();
                }

                var result = BudgetPersistenceService.ExecuteMatrixAssignmentCommand(
                    new ProjectDbContextFactory(), dbPath, command);

                Assert.AreEqual(BudgetOperationStatus.Conflict, result.Status, result.Message);
                Assert.IsFalse(result.IsCommitted);
                Assert.IsNull(result.Confirmed, "Un conflicto no puede devolver valores confirmados.");
                Assert.AreEqual(0, result.Operation.ConfirmedConceptIds.Count);

                var mensajeUsuario = result.Message ?? string.Empty;
                Assert.IsFalse(string.IsNullOrWhiteSpace(mensajeUsuario));
                Assert.IsFalse(mensajeUsuario.Contains("SQLite", StringComparison.Ordinal));
                Assert.IsFalse(mensajeUsuario.Contains(dbPath, StringComparison.Ordinal));
            }

            using (var verificacion = new SOPROContext(dbPath))
            {
                var persistido = verificacion.ConceptosPresupuesto
                    .AsNoTracking().Single(c => c.Id == conceptoId);

                Assert.AreNotEqual(matrizDestinoId, persistido.MatrizId,
                    "Un conflicto no asigna la matriz propuesta.");
                Assert.AreEqual(100m, persistido.CostoDirectoUnitario,
                    "Un conflicto no reescribe los importes del concepto.");
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
    public void ExecuteMatrixAssignmentCommand_ProyectoCambiaSuBaseDeCalculo_DevuelveConflictoSinEscribir(string parametro)
    {
        var dbPath = TestDbFactory.CreateTempDbPath();
        try
        {
            int conceptoId;
            int matrizDestinoId;
            BudgetMatrixAssignmentCommand command;

            using (var context = TestDbFactory.CreateContextAt(dbPath))
            {
                var scenario = SoproCalculationScenarioBuilder.CreateBaseBudgetScenario(context);
                conceptoId = scenario.Concepto.Id;
                matrizDestinoId = AddMatrix(
                    context, scenario.Proyecto.Id, "APU-002", "Acero de refuerzo", "kg", 50m).Id;

                command = Command(scenario.Proyecto, scenario.Concepto, matrizDestinoId);

                using (var otroModulo = new SOPROContext(dbPath))
                {
                    var proyecto = otroModulo.Proyectos.Single(p => p.Id == scenario.Proyecto.Id);
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

                var result = BudgetPersistenceService.ExecuteMatrixAssignmentCommand(
                    new ProjectDbContextFactory(), dbPath, command);

                Assert.AreEqual(BudgetOperationStatus.Conflict, result.Status, result.Message);
                Assert.IsNull(result.Confirmed);
            }

            using (var verificacion = new SOPROContext(dbPath))
            {
                var persistido = verificacion.ConceptosPresupuesto
                    .AsNoTracking().Single(c => c.Id == conceptoId);
                Assert.AreNotEqual(matrizDestinoId, persistido.MatrizId);
                Assert.AreEqual(100m, persistido.CostoDirectoUnitario);
                Assert.AreEqual(1000m, persistido.ImporteTotal);
            }
        }
        finally
        {
            DeleteQuietly(dbPath);
        }
    }

    [TestMethod]
    public void ExecuteMatrixAssignmentCommand_MatrizDeOtroProyecto_DevuelveConflictoSinEscribir()
    {
        var dbPath = TestDbFactory.CreateTempDbPath();
        try
        {
            int conceptoId;
            int matrizAjenaId;
            BudgetMatrixAssignmentCommand command;

            using (var context = TestDbFactory.CreateContextAt(dbPath))
            {
                var scenario = SoproCalculationScenarioBuilder.CreateBaseBudgetScenario(context);
                conceptoId = scenario.Concepto.Id;

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

                matrizAjenaId = AddMatrix(
                    context, otroProyecto.Id, "APU-AJENA", "Matriz de otro proyecto", "m2", 25m).Id;

                // La vista apunta al proyecto propio pero a una matriz ajena.
                command = Command(scenario.Proyecto, scenario.Concepto, matrizAjenaId);

                var result = BudgetPersistenceService.ExecuteMatrixAssignmentCommand(
                    new ProjectDbContextFactory(), dbPath, command);

                Assert.AreEqual(BudgetOperationStatus.Conflict, result.Status, result.Message);
                Assert.IsNull(result.Confirmed);
            }

            using (var verificacion = new SOPROContext(dbPath))
            {
                var persistido = verificacion.ConceptosPresupuesto
                    .AsNoTracking().Single(c => c.Id == conceptoId);
                Assert.AreNotEqual(matrizAjenaId, persistido.MatrizId);
                Assert.AreEqual("C-001", persistido.Clave);
            }
        }
        finally
        {
            DeleteQuietly(dbPath);
        }
    }

    [TestMethod]
    public void ExecuteMatrixAssignmentCommand_MatrizEliminadaEnLaBase_DevuelveConflictoSinEscribir()
    {
        var dbPath = TestDbFactory.CreateTempDbPath();
        try
        {
            int conceptoId;
            int matrizDestinoId;
            BudgetMatrixAssignmentCommand command;

            using (var context = TestDbFactory.CreateContextAt(dbPath))
            {
                var scenario = SoproCalculationScenarioBuilder.CreateBaseBudgetScenario(context);
                conceptoId = scenario.Concepto.Id;
                matrizDestinoId = AddMatrix(
                    context, scenario.Proyecto.Id, "APU-002", "Acero de refuerzo", "kg", 50m).Id;

                command = Command(scenario.Proyecto, scenario.Concepto, matrizDestinoId);

                // La matriz elegida desaparece de la base antes de aplicar el comando.
                using (var otroModulo = new SOPROContext(dbPath))
                {
                    var matriz = otroModulo.Matrices.Single(m => m.Id == matrizDestinoId);
                    otroModulo.Matrices.Remove(matriz);
                    otroModulo.SaveChanges();
                }

                var result = BudgetPersistenceService.ExecuteMatrixAssignmentCommand(
                    new ProjectDbContextFactory(), dbPath, command);

                Assert.AreEqual(BudgetOperationStatus.Conflict, result.Status, result.Message);
                Assert.IsNull(result.Confirmed);
            }

            using (var verificacion = new SOPROContext(dbPath))
            {
                var persistido = verificacion.ConceptosPresupuesto
                    .AsNoTracking().Single(c => c.Id == conceptoId);
                Assert.AreEqual("C-001", persistido.Clave);
                Assert.AreEqual(1000m, persistido.ImporteTotal);
            }
        }
        finally
        {
            DeleteQuietly(dbPath);
        }
    }

    [TestMethod]
    public void ExecuteMatrixAssignmentCommand_MatrizNoApu_RechazaSinEscribir()
    {
        var dbPath = TestDbFactory.CreateTempDbPath();
        try
        {
            int conceptoId;
            int matrizBasicaId;

            using (var context = TestDbFactory.CreateContextAt(dbPath))
            {
                var scenario = SoproCalculationScenarioBuilder.CreateBaseBudgetScenario(context);
                conceptoId = scenario.Concepto.Id;
                matrizBasicaId = AddMatrix(
                    context, scenario.Proyecto.Id, "BAS-001", "Básico auxiliar", "m3", 20m,
                    TipoMatriz.Basico).Id;

                var command = Command(scenario.Proyecto, scenario.Concepto, matrizBasicaId);

                var result = BudgetPersistenceService.ExecuteMatrixAssignmentCommand(
                    new ProjectDbContextFactory(), dbPath, command);

                Assert.AreEqual(BudgetOperationStatus.Failed, result.Status, result.Message);
                Assert.IsNull(result.Confirmed);
                StringAssert.Contains(result.Message!, "APU");
            }

            using (var verificacion = new SOPROContext(dbPath))
            {
                var persistido = verificacion.ConceptosPresupuesto
                    .AsNoTracking().Single(c => c.Id == conceptoId);
                Assert.AreNotEqual(matrizBasicaId, persistido.MatrizId,
                    "Una matriz no APU no se asigna al concepto.");
            }
        }
        finally
        {
            DeleteQuietly(dbPath);
        }
    }

    [TestMethod]
    public void ExecuteMatrixAssignmentCommand_FalloDeTrigger_NoSeConfirmaPorGuardadoAjenoYElReintentoConfirma()
    {
        var dbPath = TestDbFactory.CreateTempDbPath();
        try
        {
            int conceptoId;
            int matrizDestinoId;
            BudgetMatrixAssignmentCommand command;

            using (var context = TestDbFactory.CreateContextAt(dbPath))
            {
                var scenario = SoproCalculationScenarioBuilder.CreateBaseBudgetScenario(context);
                conceptoId = scenario.Concepto.Id;
                matrizDestinoId = AddMatrix(
                    context, scenario.Proyecto.Id, "APU-002", "Acero de refuerzo", "kg", 50m).Id;

                command = Command(scenario.Proyecto, scenario.Concepto, matrizDestinoId);

                context.Database.ExecuteSqlRaw(
                    "CREATE TRIGGER trg_fail_concepto BEFORE UPDATE ON ConceptosPresupuesto " +
                    "BEGIN SELECT RAISE(ABORT, 'fallo_simulado'); END;");

                var primerIntento = BudgetPersistenceService.ExecuteMatrixAssignmentCommand(
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

                // Guardado ajeno posterior: no puede arrastrar la asignación fallida.
                scenario.Proyecto.Descripcion = "cambio ajeno posterior al fallo";
                context.SaveChanges();

                var reintento = BudgetPersistenceService.ExecuteMatrixAssignmentCommand(
                    new ProjectDbContextFactory(), dbPath, command);

                Assert.AreEqual(BudgetOperationStatus.Committed, reintento.Status,
                    reintento.Message);
                Assert.AreEqual(matrizDestinoId, reintento.Confirmed!.MatrizId);
                Assert.AreEqual("APU-002", reintento.Confirmed.Clave);
            }

            using (var verificacion = new SOPROContext(dbPath))
            {
                var proyecto = verificacion.Proyectos
                    .AsNoTracking().Single(p => p.Id == command.ProjectId);
                var concepto = verificacion.ConceptosPresupuesto
                    .AsNoTracking().Single(c => c.Id == conceptoId);

                Assert.AreEqual("cambio ajeno posterior al fallo", proyecto.Descripcion,
                    "El guardado ajeno se completa y es independiente de la asignación.");
                Assert.AreEqual(matrizDestinoId, concepto.MatrizId,
                    "El reintento asigna la matriz; el fallo anterior no se confirmó por el guardado ajeno.");
                Assert.AreEqual("APU-002", concepto.Clave);
                Assert.AreEqual(500m, concepto.ImporteTotal);
            }
        }
        finally
        {
            DeleteQuietly(dbPath);
        }
    }

    [TestMethod]
    public void ExecuteMatrixAssignmentCommand_RepetirComandoConfirmado_DevuelveConflictoSinReescribir()
    {
        var dbPath = TestDbFactory.CreateTempDbPath();
        try
        {
            int conceptoId;
            BudgetMatrixAssignmentCommand command;
            DateTime fechaConfirmada;

            using (var context = TestDbFactory.CreateContextAt(dbPath))
            {
                var scenario = SoproCalculationScenarioBuilder.CreateBaseBudgetScenario(context);
                conceptoId = scenario.Concepto.Id;
                int matrizDestinoId = AddMatrix(
                    context, scenario.Proyecto.Id, "APU-002", "Acero de refuerzo", "kg", 50m).Id;

                command = Command(scenario.Proyecto, scenario.Concepto, matrizDestinoId);

                var primero = BudgetPersistenceService.ExecuteMatrixAssignmentCommand(
                    new ProjectDbContextFactory(), dbPath, command);
                Assert.AreEqual(BudgetOperationStatus.Committed, primero.Status, primero.Message);
                fechaConfirmada = primero.Confirmed!.FechaModificacion;

                var segundo = BudgetPersistenceService.ExecuteMatrixAssignmentCommand(
                    new ProjectDbContextFactory(), dbPath, command);
                Assert.AreEqual(BudgetOperationStatus.Conflict, segundo.Status, segundo.Message);
                Assert.IsNull(segundo.Confirmed);
            }

            using (var verificacion = new SOPROContext(dbPath))
            {
                var concepto = verificacion.ConceptosPresupuesto
                    .AsNoTracking().Single(c => c.Id == conceptoId);

                Assert.AreEqual("APU-002", concepto.Clave);
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
    public void ExecuteMatrixAssignmentCommand_FilaNueva_InsercionMediaDesplazaOrdenesYRecalculaAgrupador()
    {
        var dbPath = TestDbFactory.CreateTempDbPath();
        try
        {
            int proyectoId;
            int conceptoExistenteId;
            int agrupadorId;
            int matrizDestinoId;
            BudgetMatrixAssignmentCommand command;

            using (var context = TestDbFactory.CreateContextAt(dbPath))
            {
                var scenario = SoproCalculationScenarioBuilder.CreateBaseBudgetScenario(context);
                proyectoId = scenario.Proyecto.Id;
                conceptoExistenteId = scenario.Concepto.Id;

                agrupadorId = AddAggregator(context, proyectoId, importeInicial: 1000m).Id;
                matrizDestinoId = AddMatrix(
                    context, proyectoId, "APU-002", "Acero de refuerzo", "kg", 50m).Id;

                var matrix = context.Matrices.Single(m => m.Id == matrizDestinoId);
                var rowset = BudgetAssignmentRowset.FromConceptos(
                    context.ConceptosPresupuesto.Where(c => c.ProyectoId == proyectoId));

                command = BudgetMatrixAssignmentCommand.ForNewRow(
                    proyectoId,
                    BudgetProjectCalculationBase.FromProyecto(scenario.Proyecto),
                    BudgetAssignmentTarget.ForMatrix(
                        matrix.Id, BudgetMatrixAssignmentBase.FromMatriz(matrix)),
                    "APU-002",
                    descriptionOverride: null,
                    unitOverride: null,
                    proposedQuantity: 10m,
                    expectedRowset: rowset,
                    proposedInsertionIndex: 1);

                var result = BudgetPersistenceService.ExecuteMatrixAssignmentCommand(
                    new ProjectDbContextFactory(), dbPath, command);

                Assert.AreEqual(BudgetOperationStatus.Committed, result.Status, result.Message);
                Assert.IsTrue(result.IsCommitted);
                Assert.IsTrue(result.Confirmed!.CreatedNewRow);
                Assert.AreNotEqual(0, result.Confirmed.ConceptId);
                Assert.AreEqual(result.Confirmed.ConceptId,
                    result.Operation.ConfirmedConceptIds.Single());

                var confirmado = result.Confirmed;
                Assert.AreEqual("APU-002", confirmado.Clave);
                Assert.AreEqual("Acero de refuerzo", confirmado.Descripcion);
                Assert.AreEqual(50m, confirmado.CostoDirectoUnitario);
                Assert.AreEqual(500m, confirmado.CostoDirectoTotal);
                Assert.AreEqual(500m, confirmado.ImporteTotal);

                var filas = confirmado.Rows;
                Assert.AreEqual(3, filas.Count);
                Assert.AreEqual(1, filas.Single(f => f.ConceptId == confirmado.ConceptId).Orden,
                    "La fila nueva ocupa la posición propuesta.");
                Assert.AreEqual(2, filas.Single(f => f.ConceptId == conceptoExistenteId).Orden,
                    "La fila posterior se desplaza en el mismo comando.");
                Assert.AreEqual(0, filas.Single(f => f.ConceptId == agrupadorId).Orden);

                var agrupadorConfirmado = confirmado.Aggregators.Single(a => a.ConceptId == agrupadorId);
                Assert.AreEqual(1500m, agrupadorConfirmado.ImporteTotal,
                    "El agrupador suma el concepto nuevo y el existente.");
                Assert.AreEqual(1500m, agrupadorConfirmado.CostoDirectoTotal);

                // El contexto compartido de la vista no escribe el presupuesto.
                Assert.AreEqual(1, scenario.Concepto.Orden,
                    "El comando no mueve la entidad tracked del contexto compartido.");
                Assert.AreEqual(EntityState.Unchanged, context.Entry(scenario.Concepto).State);
            }

            using (var verificacion = new SOPROContext(dbPath))
            {
                var filas = verificacion.ConceptosPresupuesto
                    .AsNoTracking()
                    .Where(c => c.ProyectoId == proyectoId)
                    .OrderBy(c => c.Orden)
                    .ThenBy(c => c.Id)
                    .ToList();

                Assert.AreEqual(3, filas.Count);
                var nuevo = filas.Single(c => c.Clave == "APU-002");
                Assert.AreEqual(1, nuevo.Orden);
                Assert.AreEqual(5, nuevo.Nivel);
                Assert.IsFalse(nuevo.EsAgrupador);
                Assert.AreEqual(matrizDestinoId, nuevo.MatrizId);
                Assert.AreEqual(500m, nuevo.ImporteTotal);

                Assert.AreEqual(2, filas.Single(c => c.Id == conceptoExistenteId).Orden);
                Assert.AreEqual(1500m, filas.Single(c => c.Id == agrupadorId).ImporteTotal);
            }
        }
        finally
        {
            DeleteQuietly(dbPath);
        }
    }

    [TestMethod]
    public void ExecuteMatrixAssignmentCommand_FilaNueva_FalloDeTriggerRevierteTodoYElReintentoInsertaUnaSolaVez()
    {
        var dbPath = TestDbFactory.CreateTempDbPath();
        try
        {
            int proyectoId;
            int conceptoExistenteId;
            int agrupadorId;
            BudgetMatrixAssignmentCommand command;

            using (var context = TestDbFactory.CreateContextAt(dbPath))
            {
                var scenario = SoproCalculationScenarioBuilder.CreateBaseBudgetScenario(context);
                proyectoId = scenario.Proyecto.Id;
                conceptoExistenteId = scenario.Concepto.Id;
                agrupadorId = AddAggregator(context, proyectoId, importeInicial: 1000m).Id;

                var matrix = AddMatrix(
                    context, proyectoId, "APU-002", "Acero de refuerzo", "kg", 50m);
                var rowset = BudgetAssignmentRowset.FromConceptos(
                    context.ConceptosPresupuesto.Where(c => c.ProyectoId == proyectoId));

                command = BudgetMatrixAssignmentCommand.ForNewRow(
                    proyectoId,
                    BudgetProjectCalculationBase.FromProyecto(scenario.Proyecto),
                    BudgetAssignmentTarget.ForMatrix(
                        matrix.Id, BudgetMatrixAssignmentBase.FromMatriz(matrix)),
                    "APU-002",
                    descriptionOverride: null,
                    unitOverride: null,
                    proposedQuantity: 10m,
                    expectedRowset: rowset,
                    proposedInsertionIndex: 1);

                context.Database.ExecuteSqlRaw(
                    "CREATE TRIGGER trg_fail_insert_concepto BEFORE INSERT ON ConceptosPresupuesto " +
                    "BEGIN SELECT RAISE(ABORT, 'fallo_simulado_insert'); END;");

                var primerIntento = BudgetPersistenceService.ExecuteMatrixAssignmentCommand(
                    new ProjectDbContextFactory(), dbPath, command);

                Assert.AreEqual(BudgetOperationStatus.Failed, primerIntento.Status,
                    primerIntento.Message);
                Assert.IsNull(primerIntento.Confirmed);
                Assert.IsNotNull(primerIntento.Diagnostic);
                StringAssert.Contains(primerIntento.Diagnostic!, "fallo_simulado_insert");
                Assert.IsFalse((primerIntento.Message ?? string.Empty).Contains(
                    "fallo_simulado_insert", StringComparison.Ordinal));

                using (var trasFallo = new SOPROContext(dbPath))
                {
                    Assert.AreEqual(2, trasFallo.ConceptosPresupuesto.Count(
                        c => c.ProyectoId == proyectoId),
                        "El fallo no deja la fila nueva.");
                    Assert.AreEqual(1, trasFallo.ConceptosPresupuesto
                        .AsNoTracking().Single(c => c.Id == conceptoExistenteId).Orden,
                        "El fallo no deja los Ordenes desplazados.");
                    Assert.AreEqual(1000m, trasFallo.ConceptosPresupuesto
                        .AsNoTracking().Single(c => c.Id == agrupadorId).ImporteTotal,
                        "El fallo no deja los totales del agrupador.");
                }

                context.Database.ExecuteSqlRaw("DROP TRIGGER trg_fail_insert_concepto;");

                var reintento = BudgetPersistenceService.ExecuteMatrixAssignmentCommand(
                    new ProjectDbContextFactory(), dbPath, command);

                Assert.AreEqual(BudgetOperationStatus.Committed, reintento.Status,
                    reintento.Message);
                Assert.IsTrue(reintento.Confirmed!.CreatedNewRow);
            }

            using (var verificacion = new SOPROContext(dbPath))
            {
                var filas = verificacion.ConceptosPresupuesto
                    .AsNoTracking()
                    .Where(c => c.ProyectoId == proyectoId)
                    .OrderBy(c => c.Orden)
                    .ToList();

                Assert.AreEqual(3, filas.Count, "El reintento inserta una sola fila.");
                Assert.AreEqual(1, filas.Single(c => c.Clave == "APU-002").Orden);
                Assert.AreEqual(2, filas.Single(c => c.Id == conceptoExistenteId).Orden);
                Assert.AreEqual(1500m, filas.Single(c => c.Id == agrupadorId).ImporteTotal);
            }
        }
        finally
        {
            DeleteQuietly(dbPath);
        }
    }

    [TestMethod]
    public void ExecuteMatrixAssignmentCommand_FilaNueva_RowsetUOrdenObsoletoDevuelveConflictoSinInsertar()
    {
        var dbPath = TestDbFactory.CreateTempDbPath();
        try
        {
            int proyectoId;
            int conceptoExistenteId;
            BudgetMatrixAssignmentCommand command;

            using (var context = TestDbFactory.CreateContextAt(dbPath))
            {
                var scenario = SoproCalculationScenarioBuilder.CreateBaseBudgetScenario(context);
                proyectoId = scenario.Proyecto.Id;
                conceptoExistenteId = scenario.Concepto.Id;

                AddAggregator(context, proyectoId, importeInicial: 1000m);
                var matrix = AddMatrix(
                    context, proyectoId, "APU-002", "Acero de refuerzo", "kg", 50m);
                var rowset = BudgetAssignmentRowset.FromConceptos(
                    context.ConceptosPresupuesto.Where(c => c.ProyectoId == proyectoId));

                command = BudgetMatrixAssignmentCommand.ForNewRow(
                    proyectoId,
                    BudgetProjectCalculationBase.FromProyecto(scenario.Proyecto),
                    BudgetAssignmentTarget.ForMatrix(
                        matrix.Id, BudgetMatrixAssignmentBase.FromMatriz(matrix)),
                    "APU-002",
                    descriptionOverride: null,
                    unitOverride: null,
                    proposedQuantity: 10m,
                    expectedRowset: rowset,
                    proposedInsertionIndex: 1);

                // La misma fila cambia de Orden después de cargar la vista.
                using (var otroModulo = new SOPROContext(dbPath))
                {
                    var concepto = otroModulo.ConceptosPresupuesto.Single(c => c.Id == conceptoExistenteId);
                    concepto.Orden = 5;
                    otroModulo.SaveChanges();
                }

                var result = BudgetPersistenceService.ExecuteMatrixAssignmentCommand(
                    new ProjectDbContextFactory(), dbPath, command);

                Assert.AreEqual(BudgetOperationStatus.Conflict, result.Status, result.Message);
                Assert.IsNull(result.Confirmed);
            }

            using (var verificacion = new SOPROContext(dbPath))
            {
                Assert.AreEqual(2, verificacion.ConceptosPresupuesto.Count(
                    c => c.ProyectoId == proyectoId));
                Assert.AreEqual(0, verificacion.ConceptosPresupuesto.Count(
                    c => c.ProyectoId == proyectoId && c.Clave == "APU-002"));
            }
        }
        finally
        {
            DeleteQuietly(dbPath);
        }
    }

    [TestMethod]
    public void ExecuteMatrixAssignmentCommand_FilaNueva_MatrizCambioCostoDevuelveConflictoSinInsertar()
    {
        var dbPath = TestDbFactory.CreateTempDbPath();
        try
        {
            int proyectoId;
            int matrizDestinoId;
            BudgetMatrixAssignmentCommand command;

            using (var context = TestDbFactory.CreateContextAt(dbPath))
            {
                var scenario = SoproCalculationScenarioBuilder.CreateBaseBudgetScenario(context);
                proyectoId = scenario.Proyecto.Id;

                AddAggregator(context, proyectoId, importeInicial: 1000m);
                var matrix = AddMatrix(
                    context, proyectoId, "APU-002", "Acero de refuerzo", "kg", 50m);
                matrizDestinoId = matrix.Id;

                var rowset = BudgetAssignmentRowset.FromConceptos(
                    context.ConceptosPresupuesto.Where(c => c.ProyectoId == proyectoId));

                command = BudgetMatrixAssignmentCommand.ForNewRow(
                    proyectoId,
                    BudgetProjectCalculationBase.FromProyecto(scenario.Proyecto),
                    BudgetAssignmentTarget.ForMatrix(
                        matrix.Id, BudgetMatrixAssignmentBase.FromMatriz(matrix)),
                    "APU-002",
                    descriptionOverride: null,
                    unitOverride: null,
                    proposedQuantity: 10m,
                    expectedRowset: rowset,
                    proposedInsertionIndex: rowset.Rows.Count);

                using (var otroModulo = new SOPROContext(dbPath))
                {
                    var matriz = otroModulo.Matrices.Single(m => m.Id == matrizDestinoId);
                    matriz.CostoDirecto = 60m;
                    otroModulo.SaveChanges();
                }

                var result = BudgetPersistenceService.ExecuteMatrixAssignmentCommand(
                    new ProjectDbContextFactory(), dbPath, command);

                Assert.AreEqual(BudgetOperationStatus.Conflict, result.Status, result.Message);
                Assert.IsNull(result.Confirmed);
            }

            using (var verificacion = new SOPROContext(dbPath))
            {
                Assert.AreEqual(0, verificacion.ConceptosPresupuesto.Count(
                    c => c.ProyectoId == proyectoId && c.Clave == "APU-002"));
            }
        }
        finally
        {
            DeleteQuietly(dbPath);
        }
    }

    [TestMethod]
    public void ExecuteMatrixAssignmentCommand_FilaNueva_CopiaDesdeConceptoConfirmaConAritmeticaCanonica()
    {
        var dbPath = TestDbFactory.CreateTempDbPath();
        try
        {
            int proyectoId;
            int conceptoOrigenId;
            BudgetMatrixAssignmentCommand command;

            using (var context = TestDbFactory.CreateContextAt(dbPath))
            {
                var scenario = SoproCalculationScenarioBuilder.CreateBaseBudgetScenario(context);
                proyectoId = scenario.Proyecto.Id;
                conceptoOrigenId = scenario.Concepto.Id;

                AddAggregator(context, proyectoId, importeInicial: 1000m);
                var rowset = BudgetAssignmentRowset.FromConceptos(
                    context.ConceptosPresupuesto.Where(c => c.ProyectoId == proyectoId));

                command = BudgetMatrixAssignmentCommand.ForNewRow(
                    proyectoId,
                    BudgetProjectCalculationBase.FromProyecto(scenario.Proyecto),
                    BudgetAssignmentTarget.ForSourceConcept(
                        BudgetPasteSourceConceptSnapshot.FromConcepto(scenario.Concepto)),
                    "C-COPIA",
                    descriptionOverride: null,
                    unitOverride: null,
                    proposedQuantity: scenario.Concepto.Cantidad,
                    expectedRowset: rowset,
                    proposedInsertionIndex: rowset.Rows.Count);

                var result = BudgetPersistenceService.ExecuteMatrixAssignmentCommand(
                    new ProjectDbContextFactory(), dbPath, command);

                Assert.AreEqual(BudgetOperationStatus.Committed, result.Status, result.Message);
                var confirmado = result.Confirmed!;
                Assert.IsTrue(confirmado.CreatedNewRow);
                Assert.AreEqual("C-COPIA", confirmado.Clave);
                Assert.AreEqual(scenario.Concepto.Descripcion, confirmado.Descripcion);
                Assert.AreEqual(100m, confirmado.CostoDirectoUnitario);
                Assert.AreEqual(1000m, confirmado.ImporteTotal);
            }

            using (var verificacion = new SOPROContext(dbPath))
            {
                var copia = verificacion.ConceptosPresupuesto
                    .AsNoTracking()
                    .Single(c => c.ProyectoId == proyectoId && c.Clave == "C-COPIA");
                Assert.AreNotEqual(conceptoOrigenId, copia.Id);
                Assert.AreEqual(1000m, copia.ImporteTotal);
            }
        }
        finally
        {
            DeleteQuietly(dbPath);
        }
    }

    [TestMethod]
    public void ExecuteMatrixAssignmentCommand_FilaNueva_OrigenCambiadoDevuelveConflictoSinInsertar()
    {
        var dbPath = TestDbFactory.CreateTempDbPath();
        try
        {
            int proyectoId;
            int conceptoOrigenId;
            BudgetMatrixAssignmentCommand command;

            using (var context = TestDbFactory.CreateContextAt(dbPath))
            {
                var scenario = SoproCalculationScenarioBuilder.CreateBaseBudgetScenario(context);
                proyectoId = scenario.Proyecto.Id;
                conceptoOrigenId = scenario.Concepto.Id;
                AddAggregator(context, proyectoId, importeInicial: 1000m);

                var source = BudgetPasteSourceConceptSnapshot.FromConcepto(scenario.Concepto);
                var rowset = BudgetAssignmentRowset.FromConceptos(
                    context.ConceptosPresupuesto.Where(c => c.ProyectoId == proyectoId));

                command = BudgetMatrixAssignmentCommand.ForNewRow(
                    proyectoId,
                    BudgetProjectCalculationBase.FromProyecto(scenario.Proyecto),
                    BudgetAssignmentTarget.ForSourceConcept(source),
                    "C-COPIA",
                    descriptionOverride: null,
                    unitOverride: null,
                    proposedQuantity: 10m,
                    expectedRowset: rowset,
                    proposedInsertionIndex: 1);

                using (var otroModulo = new SOPROContext(dbPath))
                {
                    var origen = otroModulo.ConceptosPresupuesto.Single(c => c.Id == conceptoOrigenId);
                    origen.CostoDirectoUnitario = 120m;
                    otroModulo.SaveChanges();
                }

                var result = BudgetPersistenceService.ExecuteMatrixAssignmentCommand(
                    new ProjectDbContextFactory(), dbPath, command);

                Assert.AreEqual(BudgetOperationStatus.Conflict, result.Status, result.Message);
                Assert.IsNull(result.Confirmed);
            }

            using (var verificacion = new SOPROContext(dbPath))
            {
                Assert.AreEqual(0, verificacion.ConceptosPresupuesto.Count(
                    c => c.ProyectoId == proyectoId && c.Clave == "C-COPIA"));
            }
        }
        finally
        {
            DeleteQuietly(dbPath);
        }
    }

    [TestMethod]
    public void ExecuteMatrixAssignmentCommand_FilaNueva_OrigenEliminadoDevuelveConflictoSinInsertar()
    {
        var dbPath = TestDbFactory.CreateTempDbPath();
        try
        {
            int proyectoId;
            int conceptoOrigenId;
            BudgetMatrixAssignmentCommand command;

            using (var context = TestDbFactory.CreateContextAt(dbPath))
            {
                var scenario = SoproCalculationScenarioBuilder.CreateBaseBudgetScenario(context);
                proyectoId = scenario.Proyecto.Id;
                conceptoOrigenId = scenario.Concepto.Id;
                AddAggregator(context, proyectoId, importeInicial: 1000m);

                var source = BudgetPasteSourceConceptSnapshot.FromConcepto(scenario.Concepto);

                // El rowset se toma después de la baja: así el conflicto es del origen,
                // no del rowset.
                using (var otroModulo = new SOPROContext(dbPath))
                {
                    var origen = otroModulo.ConceptosPresupuesto.Single(c => c.Id == conceptoOrigenId);
                    otroModulo.ConceptosPresupuesto.Remove(origen);
                    otroModulo.SaveChanges();
                }

                using var lectura = TestDbFactory.CreateContextAt(dbPath);
                var rowset = BudgetAssignmentRowset.FromConceptos(
                    lectura.ConceptosPresupuesto.Where(c => c.ProyectoId == proyectoId));

                command = BudgetMatrixAssignmentCommand.ForNewRow(
                    proyectoId,
                    BudgetProjectCalculationBase.FromProyecto(scenario.Proyecto),
                    BudgetAssignmentTarget.ForSourceConcept(source),
                    "C-COPIA",
                    descriptionOverride: null,
                    unitOverride: null,
                    proposedQuantity: 10m,
                    expectedRowset: rowset,
                    proposedInsertionIndex: rowset.Rows.Count);

                var result = BudgetPersistenceService.ExecuteMatrixAssignmentCommand(
                    new ProjectDbContextFactory(), dbPath, command);

                Assert.AreEqual(BudgetOperationStatus.Conflict, result.Status, result.Message);
                Assert.IsNull(result.Confirmed);
            }

            using (var verificacion = new SOPROContext(dbPath))
            {
                Assert.AreEqual(0, verificacion.ConceptosPresupuesto.Count(
                    c => c.ProyectoId == proyectoId && c.Clave == "C-COPIA"));
            }
        }
        finally
        {
            DeleteQuietly(dbPath);
        }
    }

    [TestMethod]
    public void ResolveByKey_SinCoincidencia_NoGeneraAsignacionNiAlta()
    {
        var dbPath = TestDbFactory.CreateTempDbPath();
        try
        {
            using (var context = TestDbFactory.CreateContextAt(dbPath))
            {
                var scenario = SoproCalculationScenarioBuilder.CreateBaseBudgetScenario(context);
                int filasIniciales = context.ConceptosPresupuesto.Count(
                    c => c.ProyectoId == scenario.Proyecto.Id);

                var result = BudgetConceptAssignmentService.ResolveByKey(
                    context,
                    scenario.Proyecto,
                    currentRowIndex: 0,
                    key: "NO-EXISTE",
                    existingConcept: null,
                    currentQuantityText: "1",
                    rows: Array.Empty<BudgetConceptKeyRowSnapshot>());

                Assert.IsFalse(result.HasAssignment);
                Assert.IsNull(result.Draft);
                Assert.AreEqual(filasIniciales, context.ConceptosPresupuesto.Count(
                    c => c.ProyectoId == scenario.Proyecto.Id),
                    "Una clave sin coincidencia no crea ni modifica filas.");
            }
        }
        finally
        {
            DeleteQuietly(dbPath);
        }
    }

    // ═════════════════════════════ Infraestructura ═════════════════════════════

    private static BudgetMatrixAssignmentCommand Command(
        Proyecto proyecto,
        ConceptoPresupuesto concepto,
        int matrizId)
    {
        return new BudgetMatrixAssignmentCommand(
            proyecto.Id,
            concepto.Id,
            BudgetProjectCalculationBase.FromProyecto(proyecto),
            BudgetConceptAssignmentBase.FromConcepto(concepto),
            matrizId,
            "APU-002",
            descriptionOverride: null,
            unitOverride: null,
            proposedQuantity: 10m);
    }

    private static Matriz AddMatrix(
        SOPROContext context,
        int proyectoId,
        string clave,
        string descripcion,
        string unidad,
        decimal costoDirecto,
        TipoMatriz tipo = TipoMatriz.APU)
    {
        var matriz = new Matriz
        {
            ProyectoId = proyectoId,
            Clave = clave,
            Descripcion = descripcion,
            Unidad = unidad,
            Tipo = tipo,
            CostoDirecto = costoDirecto,
            Notas = string.Empty
        };

        context.Matrices.Add(matriz);
        context.SaveChanges();
        return matriz;
    }

    private static ConceptoPresupuesto AddAggregator(
        SOPROContext context,
        int proyectoId,
        decimal importeInicial)
    {
        var agrupador = new ConceptoPresupuesto
        {
            ProyectoId = proyectoId,
            Clave = "CAP-1",
            Descripcion = "Capítulo de prueba",
            Unidad = string.Empty,
            Cantidad = 0m,
            CostoDirectoUnitario = 0m,
            CostoDirectoTotal = importeInicial,
            PrecioUnitario = 0m,
            ImporteTotal = importeInicial,
            Nivel = 0,
            Orden = 0,
            EsAgrupador = true,
            ColumnasPersonalizadasJSON = string.Empty,
            Notas = string.Empty
        };

        context.ConceptosPresupuesto.Add(agrupador);
        context.SaveChanges();
        return agrupador;
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
