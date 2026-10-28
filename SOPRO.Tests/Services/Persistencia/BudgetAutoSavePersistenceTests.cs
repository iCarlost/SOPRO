using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SOPRO.Application.DTOs.Presupuesto;
using SOPRO.Application.Models.Presupuesto;
using SOPRO.Application.Services;
using SOPRO.Core.Entities;
using SOPRO.Data.Context;
using SOPRO.Data.Factories;
using SOPRO.Tests.TestInfrastructure;

namespace SOPRO.Tests.Services.Persistencia;

[TestClass]
public class BudgetAutoSavePersistenceTests
{
    [TestMethod]
    [Ignore("Caracterización del camino legacy inseguro; se eliminará al completar ADR-0001. El contrato nuevo está cubierto por las pruebas Execute... y FormPresupuesto...")]
    public void Legacy_ApplyAutoSaveChanges_FalloDeGuardado_DejaConceptoModificadoYUnGuardadoAjenoLoConfirma()
    {
        // DIAGNÓSTICO (RED esperado): si el SaveChanges del autoguardado falla,
        // ApplyAutoSaveChanges no revierte el estado de las entidades tracked.
        // El siguiente SaveChanges de un cambio ajeno (aquí, la Descripción del
        // Proyecto) arrastra ese UPDATE pendiente y lo confirma en silencio.
        var dbPath = TestDbFactory.CreateTempDbPath();
        try
        {
            int conceptoId;
            int proyectoId;

            using (var context = TestDbFactory.CreateContextAt(dbPath))
            {
                var scenario = SoproCalculationScenarioBuilder.CreateBaseBudgetScenario(context);
                conceptoId = scenario.Concepto.Id;
                proyectoId = scenario.Proyecto.Id;

                context.Database.ExecuteSqlRaw(
                    "CREATE TRIGGER trg_fail_concepto BEFORE UPDATE ON ConceptosPresupuesto " +
                    "BEGIN SELECT RAISE(ABORT, 'fallo_simulado'); END;");

                var row = new BudgetConceptRowDto
                {
                    ExistingConceptId = conceptoId,
                    Tipo = "Concepto",
                    Clave = scenario.Concepto.Clave,
                    Descripcion = scenario.Concepto.Descripcion,
                    Unidad = scenario.Concepto.Unidad,
                    Cantidad = 25m,
                    MatrizId = scenario.MatrizApu.Id,
                    CostoDirectoUnitario = scenario.Concepto.CostoDirectoUnitario,
                    CostoDirectoTotal = 2500m,
                    PrecioUnitario = scenario.Concepto.PrecioUnitario,
                    ImporteTotal = 2500m,
                    Orden = scenario.Concepto.Orden
                };

                // En la UI la edición ya modificó el Tag antes de GuardarCambios.
                scenario.Concepto.Cantidad = 25m;
                scenario.Concepto.CostoDirectoTotal = 2500m;
                scenario.Concepto.ImporteTotal = 2500m;

                var excepcion = Assert.ThrowsExactly<DbUpdateException>(
                    () => BudgetPersistenceService.ApplyAutoSaveChanges(context, new[] { row }));
                StringAssert.Contains(excepcion.ToString(), "fallo_simulado",
                    "El fallo debe provenir del trigger simulado, no de otro error.");

                Assert.AreEqual(EntityState.Modified, context.Entry(scenario.Concepto).State,
                    "El concepto queda tracked como Modified tras el fallo del SaveChanges.");
                Assert.AreEqual(25m, scenario.Concepto.Cantidad,
                    "El valor nuevo sigue en memoria pese a que el UPDATE fue abortado.");

                // El fallo ya ocurrió; retiramos el trigger para que el guardado
                // ajeno pueda completarse.
                context.Database.ExecuteSqlRaw("DROP TRIGGER trg_fail_concepto;");

                // Guardado de un dato NO relacionado con el presupuesto.
                scenario.Proyecto.Descripcion = "cambio ajeno posterior al fallo";
                context.SaveChanges();
            }

            using (var verificacion = new SOPROContext(dbPath))
            {
                var proyectoPersistido = verificacion.Proyectos
                    .AsNoTracking()
                    .Single(p => p.Id == proyectoId);
                var conceptoPersistido = verificacion.ConceptosPresupuesto
                    .AsNoTracking()
                    .Single(c => c.Id == conceptoId);

                Assert.AreEqual("cambio ajeno posterior al fallo", proyectoPersistido.Descripcion,
                    "El guardado ajeno sí se completó; sirve como disparador del commit involuntario.");

                Assert.AreEqual(10m, conceptoPersistido.Cantidad,
                    "DIAGNÓSTICO (RED esperado): el UPDATE abortado del autoguardado no debe " +
                    "persistirse por el SaveChanges de un dato ajeno. Se esperaba la cantidad " +
                    $"original 10, pero en disco quedó {conceptoPersistido.Cantidad}.");
            }
        }
        finally
        {
            DeleteQuietly(dbPath);
        }
    }

    [TestMethod]
    public void Legacy_ApplyAutoSaveChanges_SinFallo_PersisteLaCantidadNueva()
    {
        // Contraste del camino correcto: sin fallo inyectado, el autoguardado
        // persiste la fila completa.
        var dbPath = TestDbFactory.CreateTempDbPath();
        try
        {
            int conceptoId;

            using (var context = TestDbFactory.CreateContextAt(dbPath))
            {
                var scenario = SoproCalculationScenarioBuilder.CreateBaseBudgetScenario(context);
                conceptoId = scenario.Concepto.Id;

                var row = new BudgetConceptRowDto
                {
                    ExistingConceptId = conceptoId,
                    Tipo = "Concepto",
                    Clave = scenario.Concepto.Clave,
                    Descripcion = scenario.Concepto.Descripcion,
                    Unidad = scenario.Concepto.Unidad,
                    Cantidad = 25m,
                    MatrizId = scenario.MatrizApu.Id,
                    CostoDirectoUnitario = scenario.Concepto.CostoDirectoUnitario,
                    CostoDirectoTotal = 2500m,
                    PrecioUnitario = scenario.Concepto.PrecioUnitario,
                    ImporteTotal = 2500m,
                    Orden = scenario.Concepto.Orden
                };

                BudgetPersistenceService.ApplyAutoSaveChanges(context, new[] { row });
            }

            using (var verificacion = new SOPROContext(dbPath))
            {
                var conceptoPersistido = verificacion.ConceptosPresupuesto
                    .AsNoTracking()
                    .Single(c => c.Id == conceptoId);

                Assert.AreEqual(25m, conceptoPersistido.Cantidad);
                Assert.AreEqual(2500m, conceptoPersistido.CostoDirectoTotal);
                Assert.AreEqual(2500m, conceptoPersistido.ImporteTotal);
            }
        }
        finally
        {
            DeleteQuietly(dbPath);
        }
    }

    // ────────────────────────────────────────────────────────────────────────
    // Ruta vigente (ADR-0001): comando de intención sobre un contexto NUEVO por operación.
    // ────────────────────────────────────────────────────────────────────────

    [TestMethod]
    public void ExecuteQuantityEditCommand_CaminoOk_RecalculaConUtilidadYActualizaAgrupadores()
    {
        var dbPath = TestDbFactory.CreateTempDbPath();
        try
        {
            int proyectoId;
            int conceptoId;
            int agrupadorId;
            decimal importeSegundoConcepto;
            DateTime fechaModificacionPrevia;

            using (var context = TestDbFactory.CreateContextAt(dbPath))
            {
                var scenario = SoproCalculationScenarioBuilder.CreateBaseBudgetScenario(context);
                proyectoId = scenario.Proyecto.Id;
                conceptoId = scenario.Concepto.Id;

                // La utilidad afecta el Precio Unitario: el recálculo debe aplicarla.
                scenario.Proyecto.PorcentajeUtilidad = 10m;
                scenario.Concepto.Notas = "nota original";
                context.SaveChanges();

                var (agrupador, segundo) = AddAggregatorAndSecondConcept(context, scenario);
                agrupadorId = agrupador.Id;
                importeSegundoConcepto = segundo.ImporteTotal;

                fechaModificacionPrevia = scenario.Concepto.FechaModificacion;

                var command = Command(scenario.Proyecto, scenario.Concepto, 25m);

                // Referencia canónica: la MISMA función que usa la pantalla al editar la celda.
                var esperado = BudgetRowEditFlowService.HandleQuantityCellChange(
                    scenario.Proyecto, scenario.Concepto, "25");

                var result = BudgetPersistenceService.ExecuteQuantityEditCommand(
                    new ProjectDbContextFactory(), dbPath, command);

                Assert.AreEqual(BudgetOperationStatus.Committed, result.Status, result.Message);
                Assert.IsTrue(result.IsCommitted);
                Assert.IsNull(result.Diagnostic, "Un camino confirmado no deja diagnóstico de error.");
                Assert.AreEqual(1, result.Operation.ConfirmedConceptIds.Count);
                Assert.AreEqual(conceptoId, result.Operation.ConfirmedConceptIds[0]);

                Assert.IsNotNull(result.Confirmed);
                var confirmado = result.Confirmed!.Concept;
                Assert.AreEqual(conceptoId, confirmado.ConceptId);
                Assert.AreEqual(esperado.Cantidad, confirmado.Cantidad);
                Assert.AreEqual(esperado.PrecioUnitario, confirmado.PrecioUnitario,
                    "El Precio Unitario confirmado debe incluir la utilidad del proyecto.");
                Assert.AreEqual(esperado.Importe, confirmado.ImporteTotal);
                Assert.AreEqual(2500m, confirmado.CostoDirectoTotal,
                    "El Costo Directo Total usa la cantidad redondeada por el motor.");

                var agrupadorConfirmado = result.Confirmed.Aggregators.Single(a => a.ConceptId == agrupadorId);
                Assert.AreEqual(esperado.Importe + importeSegundoConcepto, agrupadorConfirmado.ImporteTotal,
                    "El agrupador confirmado suma los importes de sus hojas.");

                // El contexto compartido del formulario no escribe el presupuesto.
                Assert.AreEqual(10m, scenario.Concepto.Cantidad,
                    "El comando no puede modificar entidades tracked del contexto compartido.");
                Assert.AreEqual(EntityState.Unchanged, context.Entry(scenario.Concepto).State);
            }

            using (var verificacion = new SOPROContext(dbPath))
            {
                var persistido = verificacion.ConceptosPresupuesto
                    .AsNoTracking()
                    .Single(c => c.Id == conceptoId);

                Assert.AreEqual(25m, persistido.Cantidad);
                Assert.AreEqual(100m, persistido.CostoDirectoUnitario, "El costo unitario no se edita aquí.");
                Assert.AreEqual(2500m, persistido.CostoDirectoTotal);
                Assert.AreEqual(110m, persistido.PrecioUnitario, "Costo directo 100 con 10% de utilidad.");
                Assert.AreEqual(2750m, persistido.ImporteTotal);
                Assert.AreEqual("Concreto simple en obra", persistido.Descripcion,
                    "Una edición de cantidad no puede reescribir la descripción.");
                Assert.AreEqual("nota original", persistido.Notas);
                Assert.IsTrue(persistido.FechaModificacion >= fechaModificacionPrevia,
                    "Se sella FechaModificacion sin retroceder la existente.");

                var agrupadorPersistido = verificacion.ConceptosPresupuesto
                    .AsNoTracking()
                    .Single(c => c.Id == agrupadorId);
                Assert.AreEqual(3300m, agrupadorPersistido.ImporteTotal);
                Assert.AreEqual(3300m, agrupadorPersistido.CostoDirectoTotal,
                    "El agrupador guarda el mismo total en ambas columnas.");
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
    [DataRow("PorcentajeIVA")]
    [DataRow("ModoCalculoPorcentajes")]
    public void ExecuteQuantityEditCommand_ProyectoCambiaSuBaseDeCalculo_DevuelveConflictoSinEscribir(string parametro)
    {
        var dbPath = TestDbFactory.CreateTempDbPath();
        try
        {
            int proyectoId;
            int conceptoId;
            BudgetQuantityEditCommand command;

            using (var context = TestDbFactory.CreateContextAt(dbPath))
            {
                var scenario = SoproCalculationScenarioBuilder.CreateBaseBudgetScenario(context);
                proyectoId = scenario.Proyecto.Id;
                conceptoId = scenario.Concepto.Id;

                // La vista arma el comando con la base que cargó.
                command = Command(scenario.Proyecto, scenario.Concepto, 25m);

                // Otro módulo cambia la base económica del proyecto; la fila no cambia.
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
                        case "PorcentajeIVA":
                            proyecto.PorcentajeIVA = 0m;
                            break;
                        case "ModoCalculoPorcentajes":
                            proyecto.ModoCalculoPorcentajes = "SobreCD";
                            break;
                        default:
                            Assert.Fail($"Parámetro no soportado por la prueba: {parametro}");
                            break;
                    }

                    otroModulo.SaveChanges();
                }

                var result = BudgetPersistenceService.ExecuteQuantityEditCommand(
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

                Assert.AreEqual(10m, persistido.Cantidad,
                    "El conflicto por base económica no debe escribir la fila.");
                Assert.AreEqual(1000m, persistido.ImporteTotal);
            }
        }
        finally
        {
            DeleteQuietly(dbPath);
        }
    }

    [TestMethod]
    public void ExecuteQuantityEditCommand_FilaCambioEnLaBase_DevuelveConflictoSinEscribir()
    {
        var dbPath = TestDbFactory.CreateTempDbPath();
        try
        {
            int conceptoId;
            BudgetQuantityEditCommand command;

            using (var context = TestDbFactory.CreateContextAt(dbPath))
            {
                var scenario = SoproCalculationScenarioBuilder.CreateBaseBudgetScenario(context);
                conceptoId = scenario.Concepto.Id;

                command = Command(scenario.Proyecto, scenario.Concepto, 25m);

                // La misma fila cambia en la base después de cargar la vista (10 → 12).
                using (var otroModulo = new SOPROContext(dbPath))
                {
                    var concepto = otroModulo.ConceptosPresupuesto.Single(c => c.Id == conceptoId);
                    concepto.Cantidad = 12m;
                    otroModulo.SaveChanges();
                }

                var result = BudgetPersistenceService.ExecuteQuantityEditCommand(
                    new ProjectDbContextFactory(), dbPath, command);

                Assert.AreEqual(BudgetOperationStatus.Conflict, result.Status, result.Message);
                Assert.IsNull(result.Confirmed);
                Assert.AreEqual(0, result.Operation.ConfirmedConceptIds.Count);
            }

            using (var verificacion = new SOPROContext(dbPath))
            {
                var persistido = verificacion.ConceptosPresupuesto
                    .AsNoTracking().Single(c => c.Id == conceptoId);

                Assert.AreEqual(12m, persistido.Cantidad,
                    "Un conflicto no escribe: queda el valor ajeno, no el propuesto.");
            }
        }
        finally
        {
            DeleteQuietly(dbPath);
        }
    }

    [TestMethod]
    public void ExecuteQuantityEditCommand_ConceptoDeOtroProyecto_DevuelveConflictoSinEscribir()
    {
        var dbPath = TestDbFactory.CreateTempDbPath();
        try
        {
            int conceptoAjenoId;
            BudgetQuantityEditCommand command;

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
                command = new BudgetQuantityEditCommand(
                    scenario.Proyecto.Id,
                    conceptoAjenoId,
                    BudgetProjectCalculationBase.FromProyecto(scenario.Proyecto),
                    BudgetConceptQuantityBase.FromConcepto(conceptoAjeno),
                    99m);

                var result = BudgetPersistenceService.ExecuteQuantityEditCommand(
                    new ProjectDbContextFactory(), dbPath, command);

                Assert.AreEqual(BudgetOperationStatus.Conflict, result.Status, result.Message);
                Assert.IsNull(result.Confirmed);
            }

            using (var verificacion = new SOPROContext(dbPath))
            {
                var ajeno = verificacion.ConceptosPresupuesto
                    .AsNoTracking().Single(c => c.Id == conceptoAjenoId);
                Assert.AreEqual(3m, ajeno.Cantidad);
                Assert.AreEqual(30m, ajeno.ImporteTotal);
            }
        }
        finally
        {
            DeleteQuietly(dbPath);
        }
    }

    [TestMethod]
    public void ExecuteQuantityEditCommand_FalloDeTrigger_RevierteYNoSeConfirmaPorElGuardadoAjeno()
    {
        var dbPath = TestDbFactory.CreateTempDbPath();
        try
        {
            int conceptoId;
            int proyectoId;

            using (var context = TestDbFactory.CreateContextAt(dbPath))
            {
                var scenario = SoproCalculationScenarioBuilder.CreateBaseBudgetScenario(context);
                conceptoId = scenario.Concepto.Id;
                proyectoId = scenario.Proyecto.Id;

                context.Database.ExecuteSqlRaw(
                    "CREATE TRIGGER trg_fail_concepto BEFORE UPDATE ON ConceptosPresupuesto " +
                    "BEGIN SELECT RAISE(ABORT, 'fallo_simulado'); END;");

                // Cambio ajeno pendiente en el contexto compartido (otro módulo).
                scenario.Proyecto.Descripcion = "cambio ajeno pendiente";

                var command = Command(scenario.Proyecto, scenario.Concepto, 25m);

                var result = BudgetPersistenceService.ExecuteQuantityEditCommand(
                    new ProjectDbContextFactory(), dbPath, command);

                Assert.AreEqual(BudgetOperationStatus.Failed, result.Status, result.Message);
                Assert.IsFalse(result.IsCommitted);
                Assert.IsNull(result.Confirmed, "Un fallo revertido no confirma valores.");
                Assert.AreEqual(0, result.Operation.ConfirmedConceptIds.Count);

                var mensajeUsuario = result.Message ?? string.Empty;
                Assert.IsFalse(string.IsNullOrWhiteSpace(mensajeUsuario));
                Assert.IsFalse(mensajeUsuario.Contains("SQLite", StringComparison.Ordinal),
                    "El mensaje al usuario no debe exponer detalles técnicos.");
                Assert.IsFalse(mensajeUsuario.Contains("fallo_simulado", StringComparison.Ordinal),
                    "El mensaje al usuario no debe exponer el detalle de la base.");
                Assert.IsFalse(mensajeUsuario.Contains(dbPath, StringComparison.Ordinal),
                    "El mensaje al usuario no debe exponer rutas.");

                // El diagnóstico técnico sí conserva el detalle, separado del mensaje.
                Assert.IsNotNull(result.Diagnostic);
                StringAssert.Contains(result.Diagnostic!, "fallo_simulado",
                    "El diagnóstico interno debe permitir identificar la causa real.");

                // El contexto compartido solo arrastra su propio cambio: nada del
                // presupuesto fallido quedó pendiente en él.
                Assert.AreEqual(10m, scenario.Concepto.Cantidad);
                Assert.AreEqual(EntityState.Modified, context.Entry(scenario.Proyecto).State);
                Assert.AreEqual(EntityState.Unchanged, context.Entry(scenario.Concepto).State);

                context.SaveChanges();
            }

            using (var verificacion = new SOPROContext(dbPath))
            {
                var proyectoPersistido = verificacion.Proyectos
                    .AsNoTracking().Single(p => p.Id == proyectoId);
                var conceptoPersistido = verificacion.ConceptosPresupuesto
                    .AsNoTracking().Single(c => c.Id == conceptoId);

                Assert.AreEqual("cambio ajeno pendiente", proyectoPersistido.Descripcion,
                    "El cambio ajeno del contexto compartido debe persistirse completo.");
                Assert.AreEqual(10m, conceptoPersistido.Cantidad,
                    "La cantidad propuesta no puede confirmarse por el guardado de otro módulo.");
                Assert.AreEqual(1000m, conceptoPersistido.ImporteTotal);
            }
        }
        finally
        {
            DeleteQuietly(dbPath);
        }
    }

    [TestMethod]
    public void ExecuteQuantityEditCommand_ReintentoConElMismoComandoTrasFallo_ConfirmaSinDuplicar()
    {
        var dbPath = TestDbFactory.CreateTempDbPath();
        try
        {
            int conceptoId;
            int proyectoId;
            int totalConceptos;
            BudgetQuantityEditCommand command;

            using (var context = TestDbFactory.CreateContextAt(dbPath))
            {
                var scenario = SoproCalculationScenarioBuilder.CreateBaseBudgetScenario(context);
                conceptoId = scenario.Concepto.Id;
                proyectoId = scenario.Proyecto.Id;
                totalConceptos = context.ConceptosPresupuesto.Count(c => c.ProyectoId == proyectoId);

                context.Database.ExecuteSqlRaw(
                    "CREATE TRIGGER trg_fail_concepto BEFORE UPDATE ON ConceptosPresupuesto " +
                    "BEGIN SELECT RAISE(ABORT, 'fallo_simulado'); END;");

                command = Command(scenario.Proyecto, scenario.Concepto, 25m);

                var primerIntento = BudgetPersistenceService.ExecuteQuantityEditCommand(
                    new ProjectDbContextFactory(), dbPath, command);
                Assert.AreEqual(BudgetOperationStatus.Failed, primerIntento.Status,
                    primerIntento.Message);

                context.Database.ExecuteSqlRaw("DROP TRIGGER trg_fail_concepto;");

                // Misma instancia del comando: la operación original no cambió.
                var reintento = BudgetPersistenceService.ExecuteQuantityEditCommand(
                    new ProjectDbContextFactory(), dbPath, command);

                Assert.AreEqual(BudgetOperationStatus.Committed, reintento.Status, reintento.Message);
                Assert.AreEqual(1, reintento.Operation.ConfirmedConceptIds.Count);
                Assert.AreEqual(conceptoId, reintento.Operation.ConfirmedConceptIds[0]);
                Assert.AreEqual(25m, reintento.Confirmed!.Concept.Cantidad);
            }

            using (var verificacion = new SOPROContext(dbPath))
            {
                var conceptos = verificacion.ConceptosPresupuesto
                    .Where(c => c.ProyectoId == proyectoId)
                    .ToList();

                Assert.AreEqual(totalConceptos, conceptos.Count,
                    "Reintentar no puede duplicar conceptos.");
                var concepto = conceptos.Single(c => c.Id == conceptoId);
                Assert.AreEqual(25m, concepto.Cantidad);
                Assert.AreEqual(2500m, concepto.ImporteTotal);
            }
        }
        finally
        {
            DeleteQuietly(dbPath);
        }
    }

    [TestMethod]
    public void ExecuteQuantityEditCommand_RepetirComandoConfirmado_DevuelveConflictoSinDuplicarNiReescribir()
    {
        var dbPath = TestDbFactory.CreateTempDbPath();
        try
        {
            int conceptoId;
            int proyectoId;
            int totalConceptos;
            BudgetQuantityEditCommand command;
            BudgetConfirmedConceptRow confirmado;

            using (var context = TestDbFactory.CreateContextAt(dbPath))
            {
                var scenario = SoproCalculationScenarioBuilder.CreateBaseBudgetScenario(context);
                conceptoId = scenario.Concepto.Id;
                proyectoId = scenario.Proyecto.Id;
                totalConceptos = context.ConceptosPresupuesto.Count(c => c.ProyectoId == proyectoId);

                command = Command(scenario.Proyecto, scenario.Concepto, 25m);

                var primero = BudgetPersistenceService.ExecuteQuantityEditCommand(
                    new ProjectDbContextFactory(), dbPath, command);
                Assert.AreEqual(BudgetOperationStatus.Committed, primero.Status, primero.Message);
                confirmado = primero.Confirmed!.Concept;

                // La instantánea confirmada coincide con lo que quedó en la base.
                using (var lectura = new SOPROContext(dbPath))
                {
                    var persistido = lectura.ConceptosPresupuesto
                        .AsNoTracking().Single(c => c.Id == conceptoId);

                    Assert.AreEqual(persistido.Cantidad, confirmado.Cantidad);
                    Assert.AreEqual(persistido.CostoDirectoTotal, confirmado.CostoDirectoTotal);
                    Assert.AreEqual(persistido.PrecioUnitario, confirmado.PrecioUnitario);
                    Assert.AreEqual(persistido.ImporteTotal, confirmado.ImporteTotal);
                }

                // Reejecutar el mismo comando encuentra la base cambiada: conflicto, no
                // una segunda escritura ni una duplicación.
                var segundo = BudgetPersistenceService.ExecuteQuantityEditCommand(
                    new ProjectDbContextFactory(), dbPath, command);
                Assert.AreEqual(BudgetOperationStatus.Conflict, segundo.Status, segundo.Message);
                Assert.IsNull(segundo.Confirmed);
            }

            using (var verificacion = new SOPROContext(dbPath))
            {
                var conceptos = verificacion.ConceptosPresupuesto
                    .Where(c => c.ProyectoId == proyectoId)
                    .ToList();

                Assert.AreEqual(totalConceptos, conceptos.Count);
                var concepto = conceptos.Single(c => c.Id == conceptoId);
                Assert.AreEqual(confirmado.Cantidad, concepto.Cantidad);
                Assert.AreEqual(confirmado.ImporteTotal, concepto.ImporteTotal);
            }
        }
        finally
        {
            DeleteQuietly(dbPath);
        }
    }

    [TestMethod]
    public void ExecuteQuantityEditCommand_ConceptoSinMatriz_RechazaSinEscribir()
    {
        var dbPath = TestDbFactory.CreateTempDbPath();
        try
        {
            int conceptoSinMatrizId;
            BudgetQuantityEditCommand command;

            using (var context = TestDbFactory.CreateContextAt(dbPath))
            {
                var scenario = SoproCalculationScenarioBuilder.CreateBaseBudgetScenario(context);

                // La pantalla no recalcula cantidades de filas sin matriz.
                var conceptoSinMatriz = new ConceptoPresupuesto
                {
                    ProyectoId = scenario.Proyecto.Id,
                    Clave = "C-SIN-MATRIZ",
                    Descripcion = "Concepto sin matriz",
                    Unidad = "pza",
                    Cantidad = 1m,
                    CostoDirectoUnitario = 0m,
                    CostoDirectoTotal = 0m,
                    PrecioUnitario = 0m,
                    ImporteTotal = 0m,
                    Nivel = 1,
                    Orden = 2,
                    ColumnasPersonalizadasJSON = string.Empty,
                    Notas = string.Empty
                };
                context.ConceptosPresupuesto.Add(conceptoSinMatriz);
                context.SaveChanges();
                conceptoSinMatrizId = conceptoSinMatriz.Id;

                command = Command(scenario.Proyecto, conceptoSinMatriz, 5m);

                var result = BudgetPersistenceService.ExecuteQuantityEditCommand(
                    new ProjectDbContextFactory(), dbPath, command);

                Assert.AreEqual(BudgetOperationStatus.Failed, result.Status, result.Message);
                Assert.IsNull(result.Confirmed);
                StringAssert.Contains(result.Message!, "matriz",
                    "El mensaje debe explicar que el concepto no tiene matriz asignada.");
                Assert.IsFalse(result.Message!.Contains(dbPath, StringComparison.Ordinal));
            }

            using (var verificacion = new SOPROContext(dbPath))
            {
                var persistido = verificacion.ConceptosPresupuesto
                    .AsNoTracking().Single(c => c.Id == conceptoSinMatrizId);

                Assert.AreEqual(1m, persistido.Cantidad,
                    "Un concepto sin matriz no puede confirmar la cantidad propuesta.");
            }
        }
        finally
        {
            DeleteQuietly(dbPath);
        }
    }

    [TestMethod]
    public void ExecuteQuantityEditCommand_Agrupador_RechazaSinEscribir()
    {
        var dbPath = TestDbFactory.CreateTempDbPath();
        try
        {
            int agrupadorId;
            BudgetQuantityEditCommand command;

            using (var context = TestDbFactory.CreateContextAt(dbPath))
            {
                var scenario = SoproCalculationScenarioBuilder.CreateBaseBudgetScenario(context);
                var (agrupador, _) = AddAggregatorAndSecondConcept(context, scenario);
                agrupadorId = agrupador.Id;

                // En la pantalla la celda Cantidad de un agrupador es de solo lectura.
                command = Command(scenario.Proyecto, agrupador, 5m);

                var result = BudgetPersistenceService.ExecuteQuantityEditCommand(
                    new ProjectDbContextFactory(), dbPath, command);

                Assert.AreEqual(BudgetOperationStatus.Failed, result.Status, result.Message);
                Assert.IsNull(result.Confirmed);
                StringAssert.Contains(result.Message!, "agrupador");
            }

            using (var verificacion = new SOPROContext(dbPath))
            {
                var persistido = verificacion.ConceptosPresupuesto
                    .AsNoTracking().Single(c => c.Id == agrupadorId);

                Assert.AreEqual(0m, persistido.Cantidad,
                    "Un agrupador no puede confirmar una cantidad editada.");
            }
        }
        finally
        {
            DeleteQuietly(dbPath);
        }
    }

    private static BudgetQuantityEditCommand Command(
        Proyecto proyecto,
        ConceptoPresupuesto concepto,
        decimal cantidadPropuesta)
    {
        return new BudgetQuantityEditCommand(
            proyecto.Id,
            concepto.Id,
            BudgetProjectCalculationBase.FromProyecto(proyecto),
            BudgetConceptQuantityBase.FromConcepto(concepto),
            cantidadPropuesta);
    }

    /// <summary>
    /// Agrega al escenario un capítulo agrupador y un segundo concepto hoja (CD 50,
    /// cantidad 10) para verificar que los totales del agrupador se recalculan.
    /// </summary>
    private static (ConceptoPresupuesto Agrupador, ConceptoPresupuesto Segundo) AddAggregatorAndSecondConcept(
        SOPROContext context,
        SoproCalculationScenario scenario)
    {
        var agrupador = new ConceptoPresupuesto
        {
            ProyectoId = scenario.Proyecto.Id,
            Clave = "CAP-1",
            Descripcion = "Capítulo de prueba",
            Unidad = string.Empty,
            Cantidad = 0m,
            CostoDirectoUnitario = 0m,
            CostoDirectoTotal = 0m,
            PrecioUnitario = 0m,
            ImporteTotal = 0m,
            Nivel = 0,
            Orden = 0,
            EsAgrupador = true,
            ColumnasPersonalizadasJSON = string.Empty,
            Notas = string.Empty
        };

        var segundo = new ConceptoPresupuesto
        {
            ProyectoId = scenario.Proyecto.Id,
            Clave = "C-002",
            Descripcion = "Segundo concepto",
            Unidad = "m2",
            Cantidad = 10m,
            MatrizId = scenario.MatrizApu.Id,
            CostoDirectoUnitario = 50m,
            CostoDirectoTotal = 500m,
            PrecioUnitario = 55m,
            ImporteTotal = 550m,
            Nivel = 1,
            Orden = 2,
            ColumnasPersonalizadasJSON = string.Empty,
            Notas = string.Empty
        };

        context.ConceptosPresupuesto.AddRange(agrupador, segundo);
        context.SaveChanges();
        return (agrupador, segundo);
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
}
