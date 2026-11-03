using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
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
/// Primera alta de una matriz nueva vinculada a un concepto persistido: matriz +
/// lote + vínculo + precios en una transacción, con revalidación de cabecera,
/// concepto, rowset, clave y fuentes. Cero escrituras ante cualquier rechazo.
/// </summary>
[TestClass]
public class BudgetNewMatrixWithComponentsPersistenceTests
{
    [TestMethod]
    public void ExecuteNewMatrixWithComponentsCommand_ConceptoSinMatriz_CreaMatrizLoteVinculoYPrecios()
    {
        var dbPath = TestDbFactory.CreateTempDbPath();
        try
        {
            int proyectoId;
            int conceptoId;
            int materialId;
            int agrupadorId;

            using (var context = TestDbFactory.CreateContextAt(dbPath))
            {
                var scenario = SoproCalculationScenarioBuilder.CreateBaseBudgetScenario(context);
                proyectoId = scenario.Proyecto.Id;
                var hoja = CrearConceptoSinMatriz(context, proyectoId, "C-NUEVA", orden: 2);
                var agrupadorCreado = CrearAgrupador(context, proyectoId);
                context.SaveChanges();
                conceptoId = hoja.Id;
                materialId = scenario.Cemento.Id;
                agrupadorId = agrupadorCreado.Id;
            }

            var command = CrearComando(dbPath, proyectoId, conceptoId,
                CrearItem(dbPath, TipoComponenteMatriz.Material, materialId, 2m));

            var result = BudgetPersistenceService.ExecuteNewMatrixWithComponentsCommand(
                new ProjectDbContextFactory(), dbPath, command);

            Assert.AreEqual(BudgetOperationStatus.Committed, result.Status, result.Diagnostic ?? result.Message);
            Assert.IsNotNull(result.Confirmed);

            var snapshot = result.Confirmed!;
            Assert.IsTrue(snapshot.MatrixId > 0);
            Assert.AreEqual(conceptoId, snapshot.ConceptId);
            Assert.AreEqual(1, snapshot.Addition.AddedComponentIds.Count);
            Assert.AreEqual(snapshot.MatrixId, snapshot.Assignment.MatrizId);

            using var verificacion = new SOPROContext(dbPath);
            var matriz = verificacion.Matrices.AsNoTracking().Single(m => m.Id == snapshot.MatrixId);
            Assert.AreEqual("APU-NUEVA", matriz.Clave);
            Assert.AreEqual(TipoMatriz.APU, matriz.Tipo);
            Assert.AreEqual(120m, matriz.CostoDirecto, "2 × 60 del material.");
            Assert.AreEqual(proyectoId, matriz.ProyectoId);

            var componente = verificacion.ComponentesMatriz.AsNoTracking()
                .Single(c => c.Id == snapshot.Addition.AddedComponentIds.Single());
            Assert.AreEqual(snapshot.MatrixId, componente.MatrizId);
            Assert.AreEqual(materialId, componente.MaterialId);
            Assert.AreEqual(120m, componente.Importe);

            var concepto = verificacion.ConceptosPresupuesto.AsNoTracking().Single(c => c.Id == conceptoId);
            Assert.AreEqual(snapshot.MatrixId, concepto.MatrizId);
            Assert.AreEqual(120m, concepto.CostoDirectoUnitario);
            Assert.AreEqual(1200m, concepto.CostoDirectoTotal);
            Assert.AreEqual(120m, concepto.PrecioUnitario);
            Assert.AreEqual(1200m, concepto.ImporteTotal);

            var agrupador = verificacion.ConceptosPresupuesto.AsNoTracking().Single(c => c.Id == agrupadorId);
            Assert.AreEqual(2200m, agrupador.ImporteTotal,
                "El agrupador suma la hoja nueva (1200) y la hoja existente C-001 (1000).");
        }
        finally
        {
            DeleteQuietly(dbPath);
        }
    }

    [TestMethod]
    public void BudgetNewMatrixWithComponentsCommand_ConceptoSinId_RechazaAntesDeEjecutar()
    {
        var dbPath = TestDbFactory.CreateTempDbPath();
        try
        {
            using var context = TestDbFactory.CreateContextAt(dbPath);
            var scenario = SoproCalculationScenarioBuilder.CreateBaseBudgetScenario(context);
            var filas = context.ConceptosPresupuesto.AsNoTracking()
                .Where(c => c.ProyectoId == scenario.Proyecto.Id)
                .ToList();
            var concepto = filas.First();

            var ex = Assert.ThrowsExactly<ArgumentOutOfRangeException>(() =>
                new BudgetNewMatrixWithComponentsCommand(
                    scenario.Proyecto.Id,
                    BudgetProjectCalculationBase.FromProyecto(scenario.Proyecto),
                    conceptId: 0,
                    BudgetConceptAssignmentBase.FromConcepto(concepto),
                    BudgetAssignmentRowset.FromConceptos(filas),
                    "APU-NUEVA",
                    "Matriz nueva",
                    "m3",
                    TipoMatriz.APU,
                    10m,
                    Array.Empty<BudgetMatrixComponentAdditionItem>()));

            StringAssert.Contains(ex.ParamName, "conceptId");
            Assert.IsFalse(context.Matrices.Any(m => m.Clave == "APU-NUEVA"));
        }
        finally
        {
            DeleteQuietly(dbPath);
        }
    }

    [TestMethod]
    public void ExecuteNewMatrixWithComponentsCommand_ConceptoYaTieneMatriz_ConflictoSinEscribir()
    {
        var dbPath = TestDbFactory.CreateTempDbPath();
        try
        {
            int proyectoId;
            int conceptoId;
            int materialId;

            using (var context = TestDbFactory.CreateContextAt(dbPath))
            {
                var scenario = SoproCalculationScenarioBuilder.CreateBaseBudgetScenario(context);
                proyectoId = scenario.Proyecto.Id;
                conceptoId = scenario.Concepto.Id;
                materialId = scenario.Cemento.Id;
                context.SaveChanges();
            }

            var command = CrearComando(dbPath, proyectoId, conceptoId,
                CrearItem(dbPath, TipoComponenteMatriz.Material, materialId, 1m));

            var result = BudgetPersistenceService.ExecuteNewMatrixWithComponentsCommand(
                new ProjectDbContextFactory(), dbPath, command);

            Assert.AreEqual(BudgetOperationStatus.Conflict, result.Status, result.Message);
            Assert.IsNull(result.Confirmed);

            using var verificacion = new SOPROContext(dbPath);
            Assert.IsFalse(verificacion.Matrices.Any(m => m.Clave == "APU-NUEVA"),
                "No se debe crear una matriz para reemplazar un vínculo existente.");
        }
        finally
        {
            DeleteQuietly(dbPath);
        }
    }

    [TestMethod]
    public void ExecuteNewMatrixWithComponentsCommand_CabeceraExcedeLongitud_RechazaSinEscribir()
    {
        var dbPath = TestDbFactory.CreateTempDbPath();
        try
        {
            int proyectoId;
            int conceptoId;
            int materialId;

            using (var context = TestDbFactory.CreateContextAt(dbPath))
            {
                var scenario = SoproCalculationScenarioBuilder.CreateBaseBudgetScenario(context);
                proyectoId = scenario.Proyecto.Id;
                conceptoId = CrearConceptoSinMatriz(context, proyectoId, "C-LARGO", orden: 2).Id;
                materialId = scenario.Cemento.Id;
                context.SaveChanges();
            }

            var item = CrearItem(dbPath, TipoComponenteMatriz.Material, materialId, 1m);

            var claveLarga = BudgetPersistenceService.ExecuteNewMatrixWithComponentsCommand(
                new ProjectDbContextFactory(), dbPath,
                CrearComandoConCabecera(dbPath, proyectoId, conceptoId,
                    new string('X', 51), "Descripción", "m3", TipoMatriz.APU, item));
            Assert.AreEqual(BudgetOperationStatus.Failed, claveLarga.Status, claveLarga.Message);

            var descripcionLarga = BudgetPersistenceService.ExecuteNewMatrixWithComponentsCommand(
                new ProjectDbContextFactory(), dbPath,
                CrearComandoConCabecera(dbPath, proyectoId, conceptoId,
                    "APU-LARGA", new string('D', 2001), "m3", TipoMatriz.APU, item));
            Assert.AreEqual(BudgetOperationStatus.Failed, descripcionLarga.Status, descripcionLarga.Message);

            var unidadLarga = BudgetPersistenceService.ExecuteNewMatrixWithComponentsCommand(
                new ProjectDbContextFactory(), dbPath,
                CrearComandoConCabecera(dbPath, proyectoId, conceptoId,
                    "APU-LARGA", "Descripción", new string('U', 21), TipoMatriz.APU, item));
            Assert.AreEqual(BudgetOperationStatus.Failed, unidadLarga.Status, unidadLarga.Message);

            using var verificacion = new SOPROContext(dbPath);
            Assert.IsFalse(verificacion.Matrices.Any(m => m.Clave == "APU-LARGA"));
            Assert.IsNull(verificacion.ConceptosPresupuesto.AsNoTracking()
                .Single(c => c.Id == conceptoId).MatrizId);
        }
        finally
        {
            DeleteQuietly(dbPath);
        }
    }

    [TestMethod]
    public void ExecuteNewMatrixWithComponentsCommand_LoteVacio_NoEscribe()
    {
        var dbPath = TestDbFactory.CreateTempDbPath();
        try
        {
            int proyectoId;
            int conceptoId;

            using (var context = TestDbFactory.CreateContextAt(dbPath))
            {
                var scenario = SoproCalculationScenarioBuilder.CreateBaseBudgetScenario(context);
                proyectoId = scenario.Proyecto.Id;
                conceptoId = CrearConceptoSinMatriz(context, proyectoId, "C-VACIA", orden: 2).Id;
                context.SaveChanges();
            }

            var command = CrearComando(dbPath, proyectoId, conceptoId);
            var result = BudgetPersistenceService.ExecuteNewMatrixWithComponentsCommand(
                new ProjectDbContextFactory(), dbPath, command);

            Assert.AreEqual(BudgetOperationStatus.Failed, result.Status, result.Message);
            Assert.IsNull(result.Confirmed);

            using var verificacion = new SOPROContext(dbPath);
            Assert.IsFalse(verificacion.Matrices.Any(m => m.Clave == "APU-NUEVA"));
            Assert.IsNull(verificacion.ConceptosPresupuesto.AsNoTracking().Single(c => c.Id == conceptoId).MatrizId);
        }
        finally
        {
            DeleteQuietly(dbPath);
        }
    }

    [TestMethod]
    public void ExecuteNewMatrixWithComponentsCommand_ClaveDuplicada_ConflictoSinEscribir()
    {
        var dbPath = TestDbFactory.CreateTempDbPath();
        try
        {
            int proyectoId;
            int conceptoId;
            int materialId;

            using (var context = TestDbFactory.CreateContextAt(dbPath))
            {
                var scenario = SoproCalculationScenarioBuilder.CreateBaseBudgetScenario(context);
                proyectoId = scenario.Proyecto.Id;
                conceptoId = CrearConceptoSinMatriz(context, proyectoId, "C-DUP", orden: 2).Id;
                materialId = scenario.Cemento.Id;
                context.Matrices.Add(new Matriz
                {
                    ProyectoId = proyectoId,
                    Clave = "APU-NUEVA",
                    Descripcion = "Duplicada",
                    Unidad = "m3",
                    Tipo = TipoMatriz.APU,
                    CostoDirecto = 0m,
                    Notas = string.Empty
                });
                context.SaveChanges();
            }

            var command = CrearComando(dbPath, proyectoId, conceptoId,
                CrearItem(dbPath, TipoComponenteMatriz.Material, materialId, 1m));
            var result = BudgetPersistenceService.ExecuteNewMatrixWithComponentsCommand(
                new ProjectDbContextFactory(), dbPath, command);

            Assert.AreEqual(BudgetOperationStatus.Conflict, result.Status, result.Message);
            Assert.IsNull(result.Confirmed);

            using var verificacion = new SOPROContext(dbPath);
            Assert.AreEqual(1, verificacion.Matrices.Count(m => m.Clave == "APU-NUEVA"));
            Assert.IsNull(verificacion.ConceptosPresupuesto.AsNoTracking().Single(c => c.Id == conceptoId).MatrizId);
        }
        finally
        {
            DeleteQuietly(dbPath);
        }
    }

    [TestMethod]
    public void ExecuteNewMatrixWithComponentsCommand_ConceptoCambiado_ConflictoSinEscribir()
    {
        var dbPath = TestDbFactory.CreateTempDbPath();
        try
        {
            int proyectoId;
            int conceptoId;
            int materialId;

            using (var context = TestDbFactory.CreateContextAt(dbPath))
            {
                var scenario = SoproCalculationScenarioBuilder.CreateBaseBudgetScenario(context);
                proyectoId = scenario.Proyecto.Id;
                conceptoId = CrearConceptoSinMatriz(context, proyectoId, "C-STALE", orden: 2).Id;
                materialId = scenario.Cemento.Id;
                context.SaveChanges();
            }

            var command = CrearComando(dbPath, proyectoId, conceptoId,
                CrearItem(dbPath, TipoComponenteMatriz.Material, materialId, 1m));

            using (var otro = new SOPROContext(dbPath))
            {
                otro.ConceptosPresupuesto.Single(c => c.Id == conceptoId).Cantidad = 11m;
                otro.SaveChanges();
            }

            var result = BudgetPersistenceService.ExecuteNewMatrixWithComponentsCommand(
                new ProjectDbContextFactory(), dbPath, command);

            Assert.AreEqual(BudgetOperationStatus.Conflict, result.Status, result.Message);
            Assert.IsNull(result.Confirmed);

            using var verificacion = new SOPROContext(dbPath);
            Assert.IsFalse(verificacion.Matrices.Any(m => m.Clave == "APU-NUEVA"));
            Assert.IsNull(verificacion.ConceptosPresupuesto.AsNoTracking().Single(c => c.Id == conceptoId).MatrizId);
        }
        finally
        {
            DeleteQuietly(dbPath);
        }
    }

    [TestMethod]
    public void ExecuteNewMatrixWithComponentsCommand_ProyectoRowsetOFuenteCambiados_ConflictoSinEscribir()
    {
        AssertConflictoConMutacion("proyecto", (db, _) =>
        {
            using var otro = new SOPROContext(db);
            otro.Proyectos.Single().DecimalesImporte = 3;
            otro.SaveChanges();
        }, 1m);

        AssertConflictoConMutacion("rowset", (db, _) =>
        {
            using var otro = new SOPROContext(db);
            var proyectoId = otro.Proyectos.Select(p => p.Id).Single();
            otro.ConceptosPresupuesto.Add(new ConceptoPresupuesto
            {
                ProyectoId = proyectoId,
                Clave = "C-EXTRA",
                Descripcion = "Fila concurrente",
                Unidad = "m3",
                Cantidad = 1m,
                MatrizId = null,
                CostoDirectoUnitario = 0m,
                CostoDirectoTotal = 0m,
                PrecioUnitario = 0m,
                ImporteTotal = 0m,
                Nivel = 5,
                Orden = 99,
                EsAgrupador = false,
                ColumnasPersonalizadasJSON = string.Empty,
                Notas = string.Empty
            });
            otro.SaveChanges();
        }, 1m);

        AssertConflictoConMutacion("fuente", (db, _) =>
        {
            using var otro = new SOPROContext(db);
            otro.Materiales.Single(m => m.Clave == "MAT-MUT").PrecioUnitario = 99m;
            otro.SaveChanges();
        }, 1m);

        AssertConflictoConMutacion("fuente-eliminada", (db, materialId) =>
        {
            using var otro = new SOPROContext(db);
            var material = otro.Materiales.Single(m => m.Id == materialId);
            otro.Materiales.Remove(material);
            otro.SaveChanges();
        }, 1m);
    }

    [TestMethod]
    public void ExecuteNewMatrixWithComponentsCommand_TipoNoApu_RechazaSinEscribir()
    {
        var dbPath = TestDbFactory.CreateTempDbPath();
        try
        {
            int proyectoId;
            int conceptoId;
            int materialId;
            int matrizBaseId;

            using (var context = TestDbFactory.CreateContextAt(dbPath))
            {
                var scenario = SoproCalculationScenarioBuilder.CreateBaseBudgetScenario(context);
                proyectoId = scenario.Proyecto.Id;
                conceptoId = CrearConceptoSinMatriz(context, proyectoId, "C-TIPO", orden: 2).Id;
                materialId = scenario.Cemento.Id;
                matrizBaseId = scenario.MatrizApu.Id;
                context.SaveChanges();
            }

            var command = CrearComando(dbPath, proyectoId, conceptoId, TipoMatriz.Basico,
                CrearItem(dbPath, TipoComponenteMatriz.Material, materialId, 1m));

            var result = BudgetPersistenceService.ExecuteNewMatrixWithComponentsCommand(
                new ProjectDbContextFactory(), dbPath, command);

            Assert.AreEqual(BudgetOperationStatus.Failed, result.Status, result.Message);
            Assert.IsNull(result.Confirmed);

            using var verificacion = new SOPROContext(dbPath);
            Assert.IsFalse(verificacion.Matrices.Any(m => m.Clave == "APU-NUEVA"));
            Assert.IsNull(verificacion.ConceptosPresupuesto.AsNoTracking().Single(c => c.Id == conceptoId).MatrizId);
        }
        finally
        {
            DeleteQuietly(dbPath);
        }
    }

    [TestMethod]
    public void ExecuteNewMatrixWithComponentsCommand_FalloDespuesDeCrearMatriz_RevierteTodo()
    {
        var dbPath = TestDbFactory.CreateTempDbPath();
        try
        {
            int proyectoId;
            int conceptoId;
            int materialId;

            using (var context = TestDbFactory.CreateContextAt(dbPath))
            {
                var scenario = SoproCalculationScenarioBuilder.CreateBaseBudgetScenario(context);
                proyectoId = scenario.Proyecto.Id;
                conceptoId = CrearConceptoSinMatriz(context, proyectoId, "C-ROLLBACK", orden: 2).Id;
                materialId = scenario.Cemento.Id;
                context.SaveChanges();
            }

            var command = CrearComando(dbPath, proyectoId, conceptoId,
                CrearItem(dbPath, TipoComponenteMatriz.Material, materialId, 1m));

            using (var trigger = new SOPROContext(dbPath))
            {
                trigger.Database.ExecuteSqlRaw(
                    "CREATE TRIGGER trg_fail_nueva_matriz BEFORE UPDATE ON Matrices " +
                    "BEGIN SELECT RAISE(ABORT, 'boom'); END;");
            }

            var result = BudgetPersistenceService.ExecuteNewMatrixWithComponentsCommand(
                new ProjectDbContextFactory(), dbPath, command);

            Assert.AreEqual(BudgetOperationStatus.Failed, result.Status, result.Message);
            Assert.IsNull(result.Confirmed);

            using var verificacion = new SOPROContext(dbPath);
            Assert.IsFalse(verificacion.Matrices.Any(m => m.Clave == "APU-NUEVA"),
                "El rollback elimina la matriz creada antes del fallo.");
            Assert.AreEqual(0, verificacion.ComponentesMatriz.Count(c => c.MatrizId == 0));
            Assert.IsNull(verificacion.ConceptosPresupuesto.AsNoTracking().Single(c => c.Id == conceptoId).MatrizId);
        }
        finally
        {
            DeleteQuietly(dbPath);
        }
    }

    [TestMethod]
    public void ExecuteNewMatrixWithComponentsCommand_DobleEjecucion_NoDuplica()
    {
        var dbPath = TestDbFactory.CreateTempDbPath();
        try
        {
            int proyectoId;
            int conceptoId;
            int materialId;

            using (var context = TestDbFactory.CreateContextAt(dbPath))
            {
                var scenario = SoproCalculationScenarioBuilder.CreateBaseBudgetScenario(context);
                proyectoId = scenario.Proyecto.Id;
                conceptoId = CrearConceptoSinMatriz(context, proyectoId, "C-IDEMP", orden: 2).Id;
                materialId = scenario.Cemento.Id;
                context.SaveChanges();
            }

            var command = CrearComando(dbPath, proyectoId, conceptoId,
                CrearItem(dbPath, TipoComponenteMatriz.Material, materialId, 1m));

            var first = BudgetPersistenceService.ExecuteNewMatrixWithComponentsCommand(
                new ProjectDbContextFactory(), dbPath, command);
            var second = BudgetPersistenceService.ExecuteNewMatrixWithComponentsCommand(
                new ProjectDbContextFactory(), dbPath, command);

            Assert.AreEqual(BudgetOperationStatus.Committed, first.Status, first.Diagnostic ?? first.Message);
            Assert.IsFalse(second.IsCommitted);
            Assert.AreEqual(BudgetOperationStatus.Conflict, second.Status, second.Message);

            using var verificacion = new SOPROContext(dbPath);
            Assert.AreEqual(1, verificacion.Matrices.Count(m => m.Clave == "APU-NUEVA"));
            Assert.AreEqual(1, verificacion.ComponentesMatriz.Count(c =>
                c.MatrizId == first.Confirmed!.MatrixId));
            Assert.AreEqual(first.Confirmed!.MatrixId,
                verificacion.ConceptosPresupuesto.AsNoTracking().Single(c => c.Id == conceptoId).MatrizId);
        }
        finally
        {
            DeleteQuietly(dbPath);
        }
    }

    // ---------- Infraestructura ----------

    private static void AssertConflictoConMutacion(
        string nombre,
        Action<string, int> mutacion,
        decimal cantidad)
    {
        var dbPath = TestDbFactory.CreateTempDbPath();
        try
        {
            int proyectoId;
            int conceptoId;
            int materialId;

            using (var context = TestDbFactory.CreateContextAt(dbPath))
            {
                var scenario = SoproCalculationScenarioBuilder.CreateBaseBudgetScenario(context);
                proyectoId = scenario.Proyecto.Id;
                conceptoId = CrearConceptoSinMatriz(context, proyectoId, "C-MUT", orden: 2).Id;
                var material = new Material
                {
                    ProyectoId = proyectoId,
                    Clave = "MAT-MUT",
                    Descripcion = "Material de mutación",
                    Unidad = "kg",
                    PrecioUnitario = 10m,
                    Notas = string.Empty
                };
                context.Materiales.Add(material);
                context.SaveChanges();
                materialId = material.Id;
            }

            var command = CrearComando(dbPath, proyectoId, conceptoId,
                CrearItem(dbPath, TipoComponenteMatriz.Material, materialId, cantidad));

            mutacion(dbPath, materialId);

            var result = BudgetPersistenceService.ExecuteNewMatrixWithComponentsCommand(
                new ProjectDbContextFactory(), dbPath, command);

            Assert.AreEqual(BudgetOperationStatus.Conflict, result.Status,
                $"{nombre}: {result.Message}");
            Assert.IsNull(result.Confirmed);

            using var verificacion = new SOPROContext(dbPath);
            Assert.IsFalse(verificacion.Matrices.Any(m => m.Clave == "APU-NUEVA"),
                $"{nombre}: no debe crear la matriz.");
            Assert.IsNull(verificacion.ConceptosPresupuesto.AsNoTracking().Single(c => c.Id == conceptoId).MatrizId,
                $"{nombre}: no debe vincular el concepto.");
        }
        finally
        {
            DeleteQuietly(dbPath);
        }
    }

    private static BudgetNewMatrixWithComponentsCommand CrearComando(
        string dbPath,
        int proyectoId,
        int conceptoId,
        params BudgetMatrixComponentAdditionItem[] additions)
        => CrearComando(dbPath, proyectoId, conceptoId, TipoMatriz.APU, additions);

    private static BudgetNewMatrixWithComponentsCommand CrearComando(
        string dbPath,
        int proyectoId,
        int conceptoId,
        TipoMatriz tipo,
        params BudgetMatrixComponentAdditionItem[] additions)
        => CrearComandoConCabecera(
            dbPath, proyectoId, conceptoId,
            "APU-NUEVA", "Matriz nueva de prueba", "m3", tipo, additions);

    private static BudgetNewMatrixWithComponentsCommand CrearComandoConCabecera(
        string dbPath,
        int proyectoId,
        int conceptoId,
        string clave,
        string descripcion,
        string unidad,
        TipoMatriz tipo,
        params BudgetMatrixComponentAdditionItem[] additions)
    {
        using var context = new SOPROContext(dbPath);
        var proyecto = context.Proyectos.AsNoTracking().Single(p => p.Id == proyectoId);
        var concepto = context.ConceptosPresupuesto.AsNoTracking().FirstOrDefault(c => c.Id == conceptoId)
            ?? throw new InvalidOperationException(
                $"Concepto {conceptoId} no encontrado. Total conceptos={context.ConceptosPresupuesto.Count()}, " +
                $"ids={string.Join(",", context.ConceptosPresupuesto.Select(c => c.Id))}");
        var filas = context.ConceptosPresupuesto
            .AsNoTracking()
            .Where(c => c.ProyectoId == proyectoId)
            .OrderBy(c => c.Orden)
            .ThenBy(c => c.Id)
            .ToList();

        return new BudgetNewMatrixWithComponentsCommand(
            proyectoId,
            BudgetProjectCalculationBase.FromProyecto(proyecto),
            conceptoId,
            BudgetConceptAssignmentBase.FromConcepto(concepto),
            BudgetAssignmentRowset.FromConceptos(filas),
            clave,
            descripcion,
            unidad,
            tipo,
            10m,
            additions);
    }

    private static BudgetMatrixComponentAdditionItem CrearItem(
        string dbPath,
        TipoComponenteMatriz tipo,
        int sourceId,
        decimal cantidad)
    {
        using var context = new SOPROContext(dbPath);
        var source = tipo switch
        {
            TipoComponenteMatriz.Material => BudgetMatrixComponentSourceBase.FromMaterial(
                context.Materiales.AsNoTracking().Single(m => m.Id == sourceId)),
            TipoComponenteMatriz.ManoDeObra => BudgetMatrixComponentSourceBase.FromManoDeObra(
                context.ManoDeObra.AsNoTracking().Single(m => m.Id == sourceId)),
            TipoComponenteMatriz.Maquinaria => BudgetMatrixComponentSourceBase.FromMaquinaria(
                context.Maquinaria.AsNoTracking().Single(m => m.Id == sourceId)),
            TipoComponenteMatriz.Herramienta => BudgetMatrixComponentSourceBase.FromHerramienta(
                context.Herramientas.AsNoTracking().Single(h => h.Id == sourceId)),
            TipoComponenteMatriz.Auxiliar => BudgetMatrixComponentSourceBase.FromAuxiliar(
                context.Matrices.AsNoTracking().Single(m => m.Id == sourceId)),
            _ => throw new InvalidOperationException($"Tipo no soportado: {tipo}")
        };

        return new BudgetMatrixComponentAdditionItem(tipo, sourceId, source, cantidad, null);
    }

    private static ConceptoPresupuesto CrearConceptoSinMatriz(
        SOPROContext context,
        int proyectoId,
        string clave,
        int orden)
    {
        var concepto = new ConceptoPresupuesto
        {
            ProyectoId = proyectoId,
            Clave = clave,
            Descripcion = "Concepto sin matriz",
            Unidad = "m3",
            Cantidad = 10m,
            MatrizId = null,
            CostoDirectoUnitario = 0m,
            CostoDirectoTotal = 0m,
            PrecioUnitario = 0m,
            ImporteTotal = 0m,
            Nivel = 1,
            Orden = orden,
            EsAgrupador = false,
            ColumnasPersonalizadasJSON = string.Empty,
            Notas = string.Empty
        };
        context.ConceptosPresupuesto.Add(concepto);
        context.SaveChanges();
        return concepto;
    }

    private static ConceptoPresupuesto CrearAgrupador(SOPROContext context, int proyectoId)
    {
        var agrupador = new ConceptoPresupuesto
        {
            ProyectoId = proyectoId,
            Clave = "CAP-NUEVO",
            Descripcion = "Capítulo de prueba",
            Unidad = string.Empty,
            Cantidad = 0m,
            MatrizId = null,
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
        context.ConceptosPresupuesto.Add(agrupador);
        return agrupador;
    }

    private static void DeleteQuietly(string databasePath)
    {
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        for (var intento = 0; intento < 3; intento++)
        {
            try
            {
                if (File.Exists(databasePath)) File.Delete(databasePath);
                return;
            }
            catch (IOException)
            {
                Thread.Sleep(150);
            }
        }
    }
}
