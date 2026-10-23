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
/// Pegado de un bloque de celdas como UN comando atómico (ADR-0001): revalidación de
/// proyecto, bases esperadas, matrices APU y conceptos de origen; aritmética canónica;
/// orden Clave → borrador → overrides pegados; recálculo único de agrupadores y una
/// sola escritura. Un fallo en cualquier fila deja la base sin cambios.
/// </summary>
[TestClass]
public class BudgetPastePersistenceTests
{
    [TestMethod]
    public void ExecutePasteCommand_MatrizConOverrides_RespetaOrdenCanonicoYRecalculaAgrupadores()
    {
        var dbPath = TestDbFactory.CreateTempDbPath();
        try
        {
            int proyectoId;
            int conceptoId;
            int matrizDestinoId;
            int agrupadorId;
            int conceptoDosId;
            BudgetPasteCommand command;

            using (var context = TestDbFactory.CreateContextAt(dbPath))
            {
                var escenario = CrearEscenario(context);
                proyectoId = escenario.Proyecto.Id;
                conceptoId = escenario.Concepto.Id;
                matrizDestinoId = escenario.MatrizDestino.Id;
                agrupadorId = escenario.Agrupador.Id;
                conceptoDosId = escenario.ConceptoDos.Id;

                command = new BudgetPasteCommand(
                    proyectoId,
                    BudgetProjectCalculationBase.FromProyecto(escenario.Proyecto),
                    new[]
                    {
                        Row(escenario.Concepto,
                            clave: "APU-002",
                            descripcion: "Descripción pegada",
                            unidad: "kg",
                            cantidad: 7m,
                            keyTarget: BudgetPasteKeyTarget.ForMatrix(matrizDestinoId))
                    });
            }

            using (var contextoCompartido = new SOPROContext(dbPath))
            {
                var concepto = contextoCompartido.ConceptosPresupuesto.Single(c => c.Id == conceptoId);
                var agrupador = contextoCompartido.ConceptosPresupuesto.Single(c => c.Id == agrupadorId);
                var conceptoDos = contextoCompartido.ConceptosPresupuesto.Single(c => c.Id == conceptoDosId);

                var result = BudgetPersistenceService.ExecutePasteCommand(
                    new ProjectDbContextFactory(), dbPath, command);

                Assert.AreEqual(BudgetOperationStatus.Committed, result.Status, result.Message);
                Assert.IsNotNull(result.Confirmed);
                Assert.AreEqual(1, result.Confirmed!.Rows.Count);

                var fila = result.Confirmed.Rows[0];
                Assert.AreEqual("APU-002", fila.Clave);
                Assert.AreEqual("Descripción pegada", fila.Descripcion);
                Assert.AreEqual("kg", fila.Unidad);
                Assert.AreEqual(7m, fila.Cantidad);
                Assert.AreEqual(50m, fila.CostoDirectoUnitario);
                Assert.AreEqual(350m, fila.CostoDirectoTotal);
                Assert.AreEqual(50m, fila.PrecioUnitario);
                Assert.AreEqual(350m, fila.ImporteTotal);

                var agrupadorConfirmado = result.Confirmed.Aggregators.Single(a => a.ConceptId == agrupadorId);
                Assert.AreEqual(650m, agrupadorConfirmado.ImporteTotal,
                    "Los totales de agrupadores se recalculan sobre la lista final del lote.");
                Assert.AreEqual(650m, agrupadorConfirmado.CostoDirectoTotal);

                // El contexto compartido de la vista no escribe ni muta sus entidades.
                Assert.AreEqual("C-001", concepto.Clave);
                Assert.AreEqual(1000m, concepto.ImporteTotal);
                Assert.AreEqual(1300m, agrupador.ImporteTotal);
                Assert.AreEqual(EntityState.Unchanged, contextoCompartido.Entry(concepto).State);
                Assert.AreEqual(EntityState.Unchanged, contextoCompartido.Entry(conceptoDos).State);
                Assert.AreEqual(EntityState.Unchanged, contextoCompartido.Entry(agrupador).State);
                Assert.IsFalse(contextoCompartido.ChangeTracker.HasChanges());
            }

            using (var verificacion = new SOPROContext(dbPath))
            {
                var persistido = verificacion.ConceptosPresupuesto.AsNoTracking().Single(c => c.Id == conceptoId);
                Assert.AreEqual("APU-002", persistido.Clave);
                Assert.AreEqual("Descripción pegada", persistido.Descripcion);
                Assert.AreEqual("kg", persistido.Unidad);
                Assert.AreEqual(7m, persistido.Cantidad);
                Assert.AreEqual(50m, persistido.CostoDirectoUnitario);
                Assert.AreEqual(350m, persistido.CostoDirectoTotal);
                Assert.AreEqual(50m, persistido.PrecioUnitario);
                Assert.AreEqual(350m, persistido.ImporteTotal);
                Assert.AreEqual(matrizDestinoId, persistido.MatrizId);

                var agrupadorPersistido = verificacion.ConceptosPresupuesto.AsNoTracking().Single(c => c.Id == agrupadorId);
                Assert.AreEqual(650m, agrupadorPersistido.ImporteTotal);
                Assert.AreEqual(650m, agrupadorPersistido.CostoDirectoTotal);

                var conceptoDos = verificacion.ConceptosPresupuesto.AsNoTracking().Single(c => c.Id == conceptoDosId);
                Assert.AreEqual(300m, conceptoDos.ImporteTotal, "Las filas fuera del bloque no cambian.");
            }

            // Reejecutar el mismo comando no puede volver a escribir: la base cambió.
            var repetido = BudgetPersistenceService.ExecutePasteCommand(
                new ProjectDbContextFactory(), dbPath, command);
            Assert.AreEqual(BudgetOperationStatus.Conflict, repetido.Status, repetido.Message);
            Assert.IsNull(repetido.Confirmed);
        }
        finally
        {
            DeleteQuietly(dbPath);
        }
    }

    [TestMethod]
    public void ExecutePasteCommand_CopiaDesdeConceptoOrigen_UsaAritmeticaCanonicaDelOrigen()
    {
        var dbPath = TestDbFactory.CreateTempDbPath();
        try
        {
            int conceptoId;
            int conceptoOrigenId;
            BudgetPasteCommand command;

            using (var context = TestDbFactory.CreateContextAt(dbPath))
            {
                var escenario = CrearEscenario(context);
                conceptoId = escenario.Concepto.Id;
                conceptoOrigenId = escenario.ConceptoDos.Id;

                command = new BudgetPasteCommand(
                    escenario.Proyecto.Id,
                    BudgetProjectCalculationBase.FromProyecto(escenario.Proyecto),
                    new[]
                    {
                        Row(escenario.Concepto,
                            clave: "C-002",
                            keyTarget: BudgetPasteKeyTarget.ForSourceConcept(
                                BudgetPasteSourceConceptSnapshot.FromConcepto(escenario.ConceptoDos)))
                    });
            }

            var result = BudgetPersistenceService.ExecutePasteCommand(
                new ProjectDbContextFactory(), dbPath, command);

            Assert.AreEqual(BudgetOperationStatus.Committed, result.Status, result.Message);

            using (var verificacion = new SOPROContext(dbPath))
            {
                var persistido = verificacion.ConceptosPresupuesto.AsNoTracking().Single(c => c.Id == conceptoId);
                Assert.AreEqual("C-002", persistido.Clave);
                Assert.AreEqual("Acero en obra", persistido.Descripcion);
                Assert.AreEqual("kg", persistido.Unidad);
                Assert.AreEqual(3m, persistido.Cantidad,
                    "La copia toma la cantidad del concepto de origen, no la del destino.");
                Assert.AreEqual(100m, persistido.CostoDirectoUnitario);
                Assert.AreEqual(300m, persistido.CostoDirectoTotal);
                Assert.AreEqual(100m, persistido.PrecioUnitario);
                Assert.AreEqual(300m, persistido.ImporteTotal);

                var origen = verificacion.ConceptosPresupuesto.AsNoTracking().Single(c => c.Id == conceptoOrigenId);
                Assert.AreEqual("C-002", origen.Clave);
                Assert.AreEqual(300m, origen.ImporteTotal, "La fila de origen no se modifica.");
            }
        }
        finally
        {
            DeleteQuietly(dbPath);
        }
    }

    [TestMethod]
    public void ExecutePasteCommand_OrigenTambienEsDestinoDelLote_LaCopiaUsaElEstadoInicialDelPegado()
    {
        var dbPath = TestDbFactory.CreateTempDbPath();
        try
        {
            int conceptoId;
            int conceptoDosId;
            int matrizDestinoId;
            int agrupadorId;
            BudgetPasteCommand command;

            using (var context = TestDbFactory.CreateContextAt(dbPath))
            {
                var escenario = CrearEscenario(context);
                conceptoId = escenario.Concepto.Id;
                conceptoDosId = escenario.ConceptoDos.Id;
                matrizDestinoId = escenario.MatrizDestino.Id;
                agrupadorId = escenario.Agrupador.Id;

                // La fila de origen de la copia es TAMBIÉN destino de la primera fila
                // del lote: la instantánea debe conservar su estado previo al pegado.
                var snapshotOrigen = BudgetPasteSourceConceptSnapshot.FromConcepto(escenario.Concepto);

                command = new BudgetPasteCommand(
                    escenario.Proyecto.Id,
                    BudgetProjectCalculationBase.FromProyecto(escenario.Proyecto),
                    new[]
                    {
                        Row(escenario.Concepto,
                            clave: "APU-002",
                            keyTarget: BudgetPasteKeyTarget.ForMatrix(matrizDestinoId)),
                        Row(escenario.ConceptoDos,
                            clave: "C-001",
                            keyTarget: BudgetPasteKeyTarget.ForSourceConcept(snapshotOrigen))
                    });
            }

            var result = BudgetPersistenceService.ExecutePasteCommand(
                new ProjectDbContextFactory(), dbPath, command);

            Assert.AreEqual(BudgetOperationStatus.Committed, result.Status, result.Message);

            using (var verificacion = new SOPROContext(dbPath))
            {
                var origenActualizado = verificacion.ConceptosPresupuesto.AsNoTracking()
                    .Single(c => c.Id == conceptoId);
                Assert.AreEqual("APU-002", origenActualizado.Clave,
                    "La primera fila del lote sí se actualiza como destino.");
                Assert.AreEqual("Acero de refuerzo", origenActualizado.Descripcion);
                Assert.AreEqual(50m, origenActualizado.CostoDirectoUnitario);
                Assert.AreEqual(500m, origenActualizado.ImporteTotal);

                var copia = verificacion.ConceptosPresupuesto.AsNoTracking()
                    .Single(c => c.Id == conceptoDosId);
                Assert.AreEqual("C-001", copia.Clave);
                Assert.AreEqual("Concreto simple en obra", copia.Descripcion,
                    "La copia debe usar el origen tal como estaba al iniciar el pegado, " +
                    "no los valores que la primera fila del lote dejó en memoria.");
                Assert.AreEqual("m3", copia.Unidad);
                Assert.AreEqual(10m, copia.Cantidad);
                Assert.AreEqual(100m, copia.CostoDirectoUnitario);
                Assert.AreEqual(100m, copia.PrecioUnitario);
                Assert.AreEqual(1000m, copia.ImporteTotal);
                Assert.AreEqual(1000m, copia.CostoDirectoTotal);

                var agrupador = verificacion.ConceptosPresupuesto.AsNoTracking()
                    .Single(c => c.Id == agrupadorId);
                Assert.AreEqual(1500m, agrupador.ImporteTotal,
                    "Los agrupadores reflejan la lista final: destino actualizado + copia inicial.");
            }
        }
        finally
        {
            DeleteQuietly(dbPath);
        }
    }

    [TestMethod]
    public void ExecutePasteCommand_OrigenCambioEnLaBase_ConflictoSinEscribirDestino()
    {
        var dbPath = TestDbFactory.CreateTempDbPath();
        try
        {
            int conceptoId;
            int conceptoDosId;
            BudgetPasteCommand command;

            using (var context = TestDbFactory.CreateContextAt(dbPath))
            {
                var escenario = CrearEscenario(context);
                conceptoId = escenario.Concepto.Id;
                conceptoDosId = escenario.ConceptoDos.Id;

                command = new BudgetPasteCommand(
                    escenario.Proyecto.Id,
                    BudgetProjectCalculationBase.FromProyecto(escenario.Proyecto),
                    new[]
                    {
                        Row(escenario.Concepto,
                            clave: "C-002",
                            keyTarget: BudgetPasteKeyTarget.ForSourceConcept(
                                BudgetPasteSourceConceptSnapshot.FromConcepto(escenario.ConceptoDos)))
                    });
            }

            // Cambio ajeno del ORIGEN entre la resolución de la vista y el comando.
            using (var ajeno = new SOPROContext(dbPath))
            {
                var origen = ajeno.ConceptosPresupuesto.Single(c => c.Id == conceptoDosId);
                origen.Descripcion = "Acero cambiado fuera";
                ajeno.SaveChanges();
            }

            var result = BudgetPersistenceService.ExecutePasteCommand(
                new ProjectDbContextFactory(), dbPath, command);

            Assert.AreEqual(BudgetOperationStatus.Conflict, result.Status, result.Message);
            Assert.IsNull(result.Confirmed);
            StringAssert.Contains(result.Message, "origen");

            using (var verificacion = new SOPROContext(dbPath))
            {
                var destino = verificacion.ConceptosPresupuesto.AsNoTracking()
                    .Single(c => c.Id == conceptoId);
                Assert.AreEqual("C-001", destino.Clave, "El destino no puede escribirse si el origen cambió.");
                Assert.AreEqual("Concreto simple en obra", destino.Descripcion);
                Assert.AreEqual(1000m, destino.ImporteTotal);

                var origenPersistido = verificacion.ConceptosPresupuesto.AsNoTracking()
                    .Single(c => c.Id == conceptoDosId);
                Assert.AreEqual("Acero cambiado fuera", origenPersistido.Descripcion,
                    "El cambio ajeno del origen se conserva; la vista debe recargarse.");
            }
        }
        finally
        {
            DeleteQuietly(dbPath);
        }
    }

    [TestMethod]
    public void ExecutePasteCommand_ClaveSinMatrizNiOrigen_EsEdicionSimpleDentroDelLote()
    {
        var dbPath = TestDbFactory.CreateTempDbPath();
        try
        {
            int conceptoSinMatrizId;
            BudgetPasteCommand command;

            using (var context = TestDbFactory.CreateContextAt(dbPath))
            {
                var escenario = CrearEscenario(context);
                conceptoSinMatrizId = escenario.ConceptoSinMatriz.Id;

                command = new BudgetPasteCommand(
                    escenario.Proyecto.Id,
                    BudgetProjectCalculationBase.FromProyecto(escenario.Proyecto),
                    new[]
                    {
                        Row(escenario.ConceptoSinMatriz,
                            clave: "P-001",
                            descripcion: "Partida pegada",
                            unidad: "pza",
                            cantidad: 4m)
                    });
            }

            var result = BudgetPersistenceService.ExecutePasteCommand(
                new ProjectDbContextFactory(), dbPath, command);

            Assert.AreEqual(BudgetOperationStatus.Committed, result.Status, result.Message);

            using (var verificacion = new SOPROContext(dbPath))
            {
                var persistido = verificacion.ConceptosPresupuesto.AsNoTracking().Single(c => c.Id == conceptoSinMatrizId);
                Assert.AreEqual("P-001", persistido.Clave);
                Assert.AreEqual("Partida pegada", persistido.Descripcion);
                Assert.AreEqual("pza", persistido.Unidad);
                Assert.AreEqual(4m, persistido.Cantidad);
                Assert.IsNull(persistido.MatrizId, "Una clave sin APU/origen no asigna matriz.");
                Assert.AreEqual(0m, persistido.ImporteTotal);
            }
        }
        finally
        {
            DeleteQuietly(dbPath);
        }
    }

    [TestMethod]
    public void ExecutePasteCommand_FallaEnSegundaFila_NoEscribeNadaYElReintentoNoDuplica()
    {
        var dbPath = TestDbFactory.CreateTempDbPath();
        try
        {
            int conceptoId;
            int conceptoDosId;
            int agrupadorId;
            int filasIniciales;
            BudgetPasteCommand command;

            using (var context = TestDbFactory.CreateContextAt(dbPath))
            {
                var escenario = CrearEscenario(context);
                conceptoId = escenario.Concepto.Id;
                conceptoDosId = escenario.ConceptoDos.Id;
                agrupadorId = escenario.Agrupador.Id;
                filasIniciales = context.ConceptosPresupuesto.Count();

                command = new BudgetPasteCommand(
                    escenario.Proyecto.Id,
                    BudgetProjectCalculationBase.FromProyecto(escenario.Proyecto),
                    new[]
                    {
                        Row(escenario.Concepto,
                            clave: "APU-002",
                            descripcion: "Primera fila pegada",
                            unidad: "kg",
                            cantidad: 7m,
                            keyTarget: BudgetPasteKeyTarget.ForMatrix(escenario.MatrizDestino.Id)),
                        Row(escenario.ConceptoDos,
                            clave: "C-002",
                            descripcion: "Segunda fila pegada",
                            unidad: "kg",
                            cantidad: 2m)
                    });

                CrearTriggerDeFallo(context, conceptoDosId);
            }

            var fallido = BudgetPersistenceService.ExecutePasteCommand(
                new ProjectDbContextFactory(), dbPath, command);

            Assert.AreEqual(BudgetOperationStatus.Failed, fallido.Status, fallido.Message);
            Assert.IsNull(fallido.Confirmed);

            using (var verificacion = new SOPROContext(dbPath))
            {
                var concepto = verificacion.ConceptosPresupuesto.AsNoTracking().Single(c => c.Id == conceptoId);
                Assert.AreEqual("C-001", concepto.Clave, "La primera fila no puede quedar escrita.");
                Assert.AreEqual(1000m, concepto.ImporteTotal);

                var conceptoDos = verificacion.ConceptosPresupuesto.AsNoTracking().Single(c => c.Id == conceptoDosId);
                Assert.AreEqual("C-002", conceptoDos.Clave);
                Assert.AreEqual(300m, conceptoDos.ImporteTotal);

                var agrupador = verificacion.ConceptosPresupuesto.AsNoTracking().Single(c => c.Id == agrupadorId);
                Assert.AreEqual(1300m, agrupador.ImporteTotal,
                    "Los totales de agrupadores tampoco pueden quedar a medias.");

                Assert.AreEqual(filasIniciales, verificacion.ConceptosPresupuesto.Count(),
                    "Un lote fallido no inserta filas.");
            }

            EliminarTriggerDeFallo(dbPath);

            var reintento = BudgetPersistenceService.ExecutePasteCommand(
                new ProjectDbContextFactory(), dbPath, command);

            Assert.AreEqual(BudgetOperationStatus.Committed, reintento.Status, reintento.Message);

            using (var verificacion = new SOPROContext(dbPath))
            {
                Assert.AreEqual(filasIniciales, verificacion.ConceptosPresupuesto.Count(),
                    "El reintento del mismo lote no duplica filas.");

                var concepto = verificacion.ConceptosPresupuesto.AsNoTracking().Single(c => c.Id == conceptoId);
                Assert.AreEqual("APU-002", concepto.Clave);
                Assert.AreEqual(7m, concepto.Cantidad);
                Assert.AreEqual(350m, concepto.ImporteTotal);

                var conceptoDos = verificacion.ConceptosPresupuesto.AsNoTracking().Single(c => c.Id == conceptoDosId);
                Assert.AreEqual("C-002", conceptoDos.Clave);
                Assert.AreEqual(2m, conceptoDos.Cantidad);
                Assert.AreEqual(200m, conceptoDos.ImporteTotal);

                var agrupador = verificacion.ConceptosPresupuesto.AsNoTracking().Single(c => c.Id == agrupadorId);
                Assert.AreEqual(550m, agrupador.ImporteTotal);
            }
        }
        finally
        {
            DeleteQuietly(dbPath);
        }
    }

    [TestMethod]
    public void ExecutePasteCommand_FilaCambioEnLaBase_ConflictoSinEscribirOtrasFilas()
    {
        var dbPath = TestDbFactory.CreateTempDbPath();
        try
        {
            int conceptoId;
            int conceptoDosId;
            int agrupadorId;
            BudgetPasteCommand command;

            using (var context = TestDbFactory.CreateContextAt(dbPath))
            {
                var escenario = CrearEscenario(context);
                conceptoId = escenario.Concepto.Id;
                conceptoDosId = escenario.ConceptoDos.Id;
                agrupadorId = escenario.Agrupador.Id;

                command = new BudgetPasteCommand(
                    escenario.Proyecto.Id,
                    BudgetProjectCalculationBase.FromProyecto(escenario.Proyecto),
                    new[]
                    {
                        Row(escenario.Concepto, descripcion: "Descripción pegada", cantidad: 7m),
                        Row(escenario.ConceptoDos, descripcion: "Otra descripción", cantidad: 2m)
                    });
            }

            using (var ajeno = new SOPROContext(dbPath))
            {
                var concepto = ajeno.ConceptosPresupuesto.Single(c => c.Id == conceptoId);
                concepto.Cantidad = 99m;
                ajeno.SaveChanges();
            }

            var result = BudgetPersistenceService.ExecutePasteCommand(
                new ProjectDbContextFactory(), dbPath, command);

            Assert.AreEqual(BudgetOperationStatus.Conflict, result.Status, result.Message);

            using (var verificacion = new SOPROContext(dbPath))
            {
                var concepto = verificacion.ConceptosPresupuesto.AsNoTracking().Single(c => c.Id == conceptoId);
                Assert.AreEqual(99m, concepto.Cantidad, "El cambio ajeno se conserva; la vista debe recargarse.");
                Assert.AreEqual("Concreto simple en obra", concepto.Descripcion,
                    "La fila en conflicto no se escribe.");

                var conceptoDos = verificacion.ConceptosPresupuesto.AsNoTracking().Single(c => c.Id == conceptoDosId);
                Assert.AreEqual("Acero en obra", conceptoDos.Descripcion,
                    "Otra fila del lote tampoco se escribe si una base no coincide.");
                Assert.AreEqual(300m, conceptoDos.ImporteTotal);

                var agrupador = verificacion.ConceptosPresupuesto.AsNoTracking().Single(c => c.Id == agrupadorId);
                Assert.AreEqual(1300m, agrupador.ImporteTotal);
            }
        }
        finally
        {
            DeleteQuietly(dbPath);
        }
    }

    [TestMethod]
    public void ExecutePasteCommand_MatrizNoApu_RechazaSinEscribir()
    {
        var dbPath = TestDbFactory.CreateTempDbPath();
        try
        {
            int conceptoId;
            BudgetPasteCommand command;

            using (var context = TestDbFactory.CreateContextAt(dbPath))
            {
                var escenario = CrearEscenario(context);
                conceptoId = escenario.Concepto.Id;

                var matrizBasica = AddMatrix(context, escenario.Proyecto.Id, "BAS-001", "Básico", "m3", 10m, TipoMatriz.Basico);

                command = new BudgetPasteCommand(
                    escenario.Proyecto.Id,
                    BudgetProjectCalculationBase.FromProyecto(escenario.Proyecto),
                    new[]
                    {
                        Row(escenario.Concepto, clave: "BAS-001",
                            keyTarget: BudgetPasteKeyTarget.ForMatrix(matrizBasica.Id))
                    });
            }

            var result = BudgetPersistenceService.ExecutePasteCommand(
                new ProjectDbContextFactory(), dbPath, command);

            Assert.AreEqual(BudgetOperationStatus.Failed, result.Status, result.Message);

            using (var verificacion = new SOPROContext(dbPath))
            {
                var persistido = verificacion.ConceptosPresupuesto.AsNoTracking().Single(c => c.Id == conceptoId);
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
    public void ExecutePasteCommand_MatrizEliminadaEnLaBase_ConflictoSinEscribir()
    {
        var dbPath = TestDbFactory.CreateTempDbPath();
        try
        {
            int conceptoId;
            int matrizDestinoId;
            BudgetPasteCommand command;

            using (var context = TestDbFactory.CreateContextAt(dbPath))
            {
                var escenario = CrearEscenario(context);
                conceptoId = escenario.Concepto.Id;
                matrizDestinoId = escenario.MatrizDestino.Id;

                command = new BudgetPasteCommand(
                    escenario.Proyecto.Id,
                    BudgetProjectCalculationBase.FromProyecto(escenario.Proyecto),
                    new[]
                    {
                        Row(escenario.Concepto, clave: "APU-002",
                            keyTarget: BudgetPasteKeyTarget.ForMatrix(matrizDestinoId))
                    });
            }

            using (var ajeno = new SOPROContext(dbPath))
            {
                var matriz = ajeno.Matrices.Single(m => m.Id == matrizDestinoId);
                ajeno.Matrices.Remove(matriz);
                ajeno.SaveChanges();
            }

            var result = BudgetPersistenceService.ExecutePasteCommand(
                new ProjectDbContextFactory(), dbPath, command);

            Assert.AreEqual(BudgetOperationStatus.Conflict, result.Status, result.Message);

            using (var verificacion = new SOPROContext(dbPath))
            {
                var persistido = verificacion.ConceptosPresupuesto.AsNoTracking().Single(c => c.Id == conceptoId);
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
    public void ExecutePasteCommand_AgrupadorConCantidad_RechazaSinEscribir()
    {
        var dbPath = TestDbFactory.CreateTempDbPath();
        try
        {
            int agrupadorId;
            BudgetPasteCommand command;

            using (var context = TestDbFactory.CreateContextAt(dbPath))
            {
                var escenario = CrearEscenario(context);
                agrupadorId = escenario.Agrupador.Id;

                command = new BudgetPasteCommand(
                    escenario.Proyecto.Id,
                    BudgetProjectCalculationBase.FromProyecto(escenario.Proyecto),
                    new[]
                    {
                        Row(escenario.Agrupador, descripcion: "Capítulo pegado", cantidad: 5m)
                    });
            }

            var result = BudgetPersistenceService.ExecutePasteCommand(
                new ProjectDbContextFactory(), dbPath, command);

            Assert.AreEqual(BudgetOperationStatus.Failed, result.Status, result.Message);

            using (var verificacion = new SOPROContext(dbPath))
            {
                var agrupador = verificacion.ConceptosPresupuesto.AsNoTracking().Single(c => c.Id == agrupadorId);
                Assert.AreEqual("Capítulo 1", agrupador.Descripcion);
                Assert.AreEqual(0m, agrupador.Cantidad);
            }
        }
        finally
        {
            DeleteQuietly(dbPath);
        }
    }

    [TestMethod]
    public void ExecutePasteCommand_ProyectoCambioBaseCalculo_ConflictoSinEscribir()
    {
        var dbPath = TestDbFactory.CreateTempDbPath();
        try
        {
            int conceptoId;
            BudgetPasteCommand command;

            using (var context = TestDbFactory.CreateContextAt(dbPath))
            {
                var escenario = CrearEscenario(context);
                conceptoId = escenario.Concepto.Id;

                command = new BudgetPasteCommand(
                    escenario.Proyecto.Id,
                    BudgetProjectCalculationBase.FromProyecto(escenario.Proyecto),
                    new[] { Row(escenario.Concepto, descripcion: "Descripción pegada") });
            }

            using (var ajeno = new SOPROContext(dbPath))
            {
                var proyecto = ajeno.Proyectos.Single(p => p.Id == command.ProjectId);
                proyecto.PorcentajeUtilidad = 12m;
                ajeno.SaveChanges();
            }

            var result = BudgetPersistenceService.ExecutePasteCommand(
                new ProjectDbContextFactory(), dbPath, command);

            Assert.AreEqual(BudgetOperationStatus.Conflict, result.Status, result.Message);

            using (var verificacion = new SOPROContext(dbPath))
            {
                var persistido = verificacion.ConceptosPresupuesto.AsNoTracking().Single(c => c.Id == conceptoId);
                Assert.AreEqual("Concreto simple en obra", persistido.Descripcion);
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
        public Matriz MatrizDestino { get; init; } = null!;
        public ConceptoPresupuesto Concepto { get; init; } = null!;
        public ConceptoPresupuesto ConceptoDos { get; init; } = null!;
        public ConceptoPresupuesto ConceptoSinMatriz { get; init; } = null!;
        public ConceptoPresupuesto Agrupador { get; init; } = null!;
    }

    private static Escenario CrearEscenario(SOPROContext context)
    {
        var baseScenario = SoproCalculationScenarioBuilder.CreateBaseBudgetScenario(context);

        var matrizDestino = AddMatrix(
            context, baseScenario.Proyecto.Id, "APU-002", "Acero de refuerzo", "kg", 50m);

        var agrupador = new ConceptoPresupuesto
        {
            ProyectoId = baseScenario.Proyecto.Id,
            Clave = "CAP-1",
            Descripcion = "Capítulo 1",
            Unidad = string.Empty,
            Cantidad = 0m,
            CostoDirectoUnitario = 0m,
            CostoDirectoTotal = 1300m,
            PrecioUnitario = 0m,
            ImporteTotal = 1300m,
            Nivel = 0,
            Orden = 0,
            EsAgrupador = true,
            ColumnasPersonalizadasJSON = string.Empty,
            Notas = string.Empty
        };

        var conceptoDos = new ConceptoPresupuesto
        {
            ProyectoId = baseScenario.Proyecto.Id,
            Clave = "C-002",
            Descripcion = "Acero en obra",
            Unidad = "kg",
            Cantidad = 3m,
            MatrizId = baseScenario.MatrizApu.Id,
            CostoDirectoUnitario = 100m,
            CostoDirectoTotal = 300m,
            PrecioUnitario = 100m,
            ImporteTotal = 300m,
            Nivel = 1,
            Orden = 2,
            EsAgrupador = false,
            ColumnasPersonalizadasJSON = string.Empty,
            Notas = string.Empty
        };

        var conceptoSinMatriz = new ConceptoPresupuesto
        {
            ProyectoId = baseScenario.Proyecto.Id,
            Clave = "S-001",
            Descripcion = "Partida sin APU",
            Unidad = "pza",
            Cantidad = 0m,
            MatrizId = null,
            CostoDirectoUnitario = 0m,
            CostoDirectoTotal = 0m,
            PrecioUnitario = 0m,
            ImporteTotal = 0m,
            Nivel = 1,
            Orden = 3,
            EsAgrupador = false,
            ColumnasPersonalizadasJSON = string.Empty,
            Notas = string.Empty
        };

        context.ConceptosPresupuesto.AddRange(agrupador, conceptoDos, conceptoSinMatriz);
        context.SaveChanges();

        return new Escenario
        {
            Proyecto = baseScenario.Proyecto,
            MatrizDestino = matrizDestino,
            Concepto = baseScenario.Concepto,
            ConceptoDos = conceptoDos,
            ConceptoSinMatriz = conceptoSinMatriz,
            Agrupador = agrupador
        };
    }

    private static BudgetPasteRowCommand Row(
        ConceptoPresupuesto concepto,
        string? clave = null,
        string? descripcion = null,
        string? unidad = null,
        decimal? cantidad = null,
        BudgetPasteKeyTarget? keyTarget = null)
    {
        return new BudgetPasteRowCommand(
            concepto.Id,
            BudgetConceptAssignmentBase.FromConcepto(concepto),
            clave,
            descripcion,
            unidad,
            cantidad,
            keyTarget ?? BudgetPasteKeyTarget.None);
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

    private static void CrearTriggerDeFallo(SOPROContext context, int conceptoId)
    {
        context.Database.ExecuteSqlRaw(
            "CREATE TRIGGER trg_fail_paste BEFORE UPDATE ON ConceptosPresupuesto " +
            $"WHEN OLD.Id = {conceptoId} BEGIN SELECT RAISE(ABORT, 'fallo_simulado'); END;");
    }

    private static void EliminarTriggerDeFallo(string dbPath)
    {
        using var admin = new SOPROContext(dbPath);
        admin.Database.ExecuteSqlRaw("DROP TRIGGER trg_fail_paste;");
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
