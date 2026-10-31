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
/// Contrato del comando único de alta en lote de componentes NUEVOS sobre una matriz
/// persistida (ADR-0001): intención inmutable con el estado COMPLETO de los
/// componentes existentes, bases de catálogo y rowset del presupuesto; admisión de
/// auxiliares válidos (básicos/cuadrillas) con revalidación de su matriz fuente y
/// rechazo de autorreferencias/ciclos; revalidación exacta contra la base; cálculo
/// canónico con las altas aún sin guardar; y un solo commit. La intención no depende
/// de ningún estado de UI (el dirty tracker de la vista queda fuera de Application).
/// </summary>
[TestClass]
public class BudgetMatrixComponentAdditionPersistenceTests
{
    [TestMethod]
    public void ExecuteMatrixComponentAdditionCommand_DosAltas_GeneraIdsOrdenYCalculaCierre()
    {
        var dbPath = TestDbFactory.CreateTempDbPath();
        try
        {
            int proyectoId;
            int matrizId;
            int hojaId;
            int agrupadorId;
            int materialNuevoId;
            int maquinariaNuevaId;

            using (var context = TestDbFactory.CreateContextAt(dbPath))
            {
                var scenario = SoproCalculationScenarioBuilder.CreateBaseBudgetScenario(context);
                proyectoId = scenario.Proyecto.Id;
                matrizId = scenario.MatrizApu.Id;
                hojaId = scenario.Concepto.Id;

                var materialNuevo = new Material
                {
                    ProyectoId = proyectoId,
                    Clave = "MAT-NEW",
                    Descripcion = "Arena nueva",
                    Unidad = "m3",
                    PrecioUnitario = 20m,
                    Notas = string.Empty
                };

                var maquinariaNueva = new Maquinaria
                {
                    ProyectoId = proyectoId,
                    Clave = "MAQ-NEW",
                    Descripcion = "Compactadora",
                    CostoHorario = 8m,
                    Notas = string.Empty
                };

                context.Materiales.Add(materialNuevo);
                context.Maquinaria.Add(maquinariaNueva);
                var agrupador = AddAggregator(context, proyectoId);
                context.SaveChanges();

                materialNuevoId = materialNuevo.Id;
                maquinariaNuevaId = maquinariaNueva.Id;
                agrupadorId = agrupador.Id;
            }

            var command = CreateCommand(
                dbPath,
                proyectoId,
                matrizId,
                CreateItem(dbPath, TipoComponenteMatriz.Material, materialNuevoId, 0.5m, "Nota material"),
                CreateItem(dbPath, TipoComponenteMatriz.Maquinaria, maquinariaNuevaId, 2m, "Nota maquinaria"));

            var result = BudgetPersistenceService.ExecuteMatrixComponentAdditionCommand(
                new ProjectDbContextFactory(), dbPath, command);

            Assert.AreEqual(BudgetOperationStatus.Committed, result.Status, result.Message);
            Assert.IsNull(result.Diagnostic);
            Assert.IsNotNull(result.Confirmed);

            var snapshot = result.Confirmed!;
            Assert.AreEqual(2, snapshot.AddedComponentIds.Count);
            Assert.IsTrue(snapshot.AddedComponentIds.All(id => id > 0),
                "Los Ids de las filas nuevas se generan en el único SaveChanges.");

            Assert.AreEqual(126m, snapshot.Matrix.CostoDirecto);
            Assert.AreEqual(7, snapshot.AffectedComponents.Count,
                "La instantánea contiene los 5 componentes previos y las 2 altas.");

            var altas = snapshot.AffectedComponents
                .Where(c => snapshot.AddedComponentIds.Contains(c.ComponentId))
                .OrderBy(c => c.Orden)
                .ToList();

            Assert.AreEqual(2, altas.Count);
            Assert.AreEqual(6, altas[0].Orden);
            Assert.AreEqual(7, altas[1].Orden);
            Assert.AreEqual(TipoComponenteMatriz.Material, altas[0].TipoComponente);
            Assert.AreEqual(10m, altas[0].Importe, "0.5 × 20 = 10, calculado por Application.");
            Assert.AreEqual("Nota material", altas[0].Notas);
            Assert.AreEqual(TipoComponenteMatriz.Maquinaria, altas[1].TipoComponente);
            Assert.AreEqual(16m, altas[1].Importe, "2 × 8 = 16, calculado por Application.");
            Assert.AreEqual(0.5m, altas[1].Rendimiento, "Rendimiento = 1/Cantidad como el diálogo.");
            Assert.AreEqual("Nota maquinaria", altas[1].Notas);

            Assert.AreEqual(1, snapshot.Leaves.Count);
            Assert.AreEqual(1260m, snapshot.Leaves[0].ImporteTotal,
                "La hoja se recalcula con la matriz ya cerrada.");
            Assert.AreEqual(1, snapshot.Aggregators.Count);
            Assert.AreEqual(1260m, snapshot.Aggregators[0].ImporteTotal);

            using var verificacion = new SOPROContext(dbPath);
            var componentes = verificacion.ComponentesMatriz
                .AsNoTracking()
                .Where(c => c.MatrizId == matrizId)
                .OrderBy(c => c.Orden)
                .ThenBy(c => c.Id)
                .ToList();

            Assert.AreEqual(7, componentes.Count);
            Assert.AreEqual(snapshot.AddedComponentIds[0], componentes[5].Id);
            Assert.AreEqual(snapshot.AddedComponentIds[1], componentes[6].Id);
            Assert.AreEqual(10m, componentes[5].Importe);
            Assert.AreEqual(16m, componentes[6].Importe);

            var matriz = verificacion.Matrices.AsNoTracking().Single(m => m.Id == matrizId);
            Assert.AreEqual(126m, matriz.CostoDirecto);

            var hoja = verificacion.ConceptosPresupuesto.AsNoTracking().Single(c => c.Id == hojaId);
            Assert.AreEqual(126m, hoja.CostoDirectoUnitario);
            Assert.AreEqual(1260m, hoja.ImporteTotal);

            var agrupadorDb = verificacion.ConceptosPresupuesto.AsNoTracking().Single(c => c.Id == agrupadorId);
            Assert.AreEqual(1260m, agrupadorDb.ImporteTotal);
        }
        finally
        {
            DeleteQuietly(dbPath);
        }
    }

    [TestMethod]
    public void ExecuteMatrixComponentAdditionCommand_ManoDeObraPorcentajeYMaquinaria_ParidadDeCalculoYRendimiento()
    {
        var dbPath = TestDbFactory.CreateTempDbPath();
        try
        {
            int proyectoId;
            int matrizId;
            int hojaId;
            int caboPorcentajeId;
            int maquinariaId;

            using (var context = TestDbFactory.CreateContextAt(dbPath))
            {
                var scenario = SoproCalculationScenarioBuilder.CreateBaseBudgetScenario(context);
                proyectoId = scenario.Proyecto.Id;
                matrizId = scenario.MatrizApu.Id;
                hojaId = scenario.Concepto.Id;
                caboPorcentajeId = scenario.CaboPorcentajeMo.Id;
                maquinariaId = scenario.Revolvedora.Id;
            }

            var command = CreateCommand(
                dbPath,
                proyectoId,
                matrizId,
                CreateItem(dbPath, TipoComponenteMatriz.ManoDeObra, caboPorcentajeId, 0.20m),
                CreateItem(dbPath, TipoComponenteMatriz.Maquinaria, maquinariaId, 4m));

            var result = BudgetPersistenceService.ExecuteMatrixComponentAdditionCommand(
                new ProjectDbContextFactory(), dbPath, command);

            Assert.AreEqual(BudgetOperationStatus.Committed, result.Status, result.Message);
            var snapshot = result.Confirmed!;
            Assert.AreEqual(122m, snapshot.Matrix.CostoDirecto);

            using (var verificacion = new SOPROContext(dbPath))
            {
                var manoDeObra = verificacion.ComponentesMatriz.AsNoTracking()
                    .Single(c => c.Id == snapshot.AddedComponentIds[0]);
                Assert.AreEqual(TipoComponenteMatriz.ManoDeObra, manoDeObra.TipoComponente);
                Assert.AreEqual(caboPorcentajeId, manoDeObra.ManoDeObraId);
                Assert.AreEqual(0.20m, manoDeObra.Cantidad);
                Assert.AreEqual(6m, manoDeObra.Importe,
                    "El %MO se calcula sobre la base de mano de obra (0.20 × 30).");
                Assert.AreEqual(0m, manoDeObra.Rendimiento,
                    "El rendimiento solo aplica a maquinaria.");

                var maquinaria = verificacion.ComponentesMatriz.AsNoTracking()
                    .Single(c => c.Id == snapshot.AddedComponentIds[1]);
                Assert.AreEqual(TipoComponenteMatriz.Maquinaria, maquinaria.TipoComponente);
                Assert.AreEqual(maquinariaId, maquinaria.MaquinariaId);
                Assert.AreEqual(4m, maquinaria.Cantidad);
                Assert.AreEqual(16m, maquinaria.Importe);
                Assert.AreEqual(0.25m, maquinaria.Rendimiento);

                var hoja = verificacion.ConceptosPresupuesto.AsNoTracking().Single(c => c.Id == hojaId);
                Assert.AreEqual(122m, hoja.CostoDirectoUnitario);
                Assert.AreEqual(1220m, hoja.ImporteTotal);
            }
        }
        finally
        {
            DeleteQuietly(dbPath);
        }
    }

    [TestMethod]
    public void ExecuteMatrixComponentAdditionCommand_Herramienta_SeCalculaConSuPrecioYNoGeneraRendimiento()
    {
        var dbPath = TestDbFactory.CreateTempDbPath();
        try
        {
            int proyectoId;
            int matrizId;
            int hojaId;
            int herramientaId;

            using (var context = TestDbFactory.CreateContextAt(dbPath))
            {
                var scenario = SoproCalculationScenarioBuilder.CreateBaseBudgetScenario(context);
                proyectoId = scenario.Proyecto.Id;
                matrizId = scenario.MatrizApu.Id;
                hojaId = scenario.Concepto.Id;

                var herramienta = new Herramienta
                {
                    ProyectoId = proyectoId,
                    Clave = "HER-NEW",
                    Descripcion = "Pala nueva",
                    Unidad = "pza",
                    PrecioUnitario = 2m,
                    Notas = string.Empty
                };

                context.Herramientas.Add(herramienta);
                context.SaveChanges();
                herramientaId = herramienta.Id;
            }

            var command = CreateCommand(
                dbPath,
                proyectoId,
                matrizId,
                CreateItem(dbPath, TipoComponenteMatriz.Herramienta, herramientaId, 3m));

            var result = BudgetPersistenceService.ExecuteMatrixComponentAdditionCommand(
                new ProjectDbContextFactory(), dbPath, command);

            Assert.AreEqual(BudgetOperationStatus.Committed, result.Status, result.Message);
            var snapshot = result.Confirmed!;
            Assert.AreEqual(106m, snapshot.Matrix.CostoDirecto);

            using var verificacion = new SOPROContext(dbPath);
            var nueva = verificacion.ComponentesMatriz.AsNoTracking()
                .Single(c => c.Id == snapshot.AddedComponentIds.Single());
            Assert.AreEqual(TipoComponenteMatriz.Herramienta, nueva.TipoComponente);
            Assert.AreEqual(herramientaId, nueva.HerramientaId);
            Assert.AreEqual(6m, nueva.Importe);
            Assert.AreEqual(0m, nueva.Rendimiento);

            var hoja = verificacion.ConceptosPresupuesto.AsNoTracking().Single(c => c.Id == hojaId);
            Assert.AreEqual(106m, hoja.CostoDirectoUnitario);
            Assert.AreEqual(1060m, hoja.ImporteTotal);
        }
        finally
        {
            DeleteQuietly(dbPath);
        }
    }

    [TestMethod]
    public void ExecuteMatrixComponentAdditionCommand_MatrizAnidada_IncluyeAltasSinGuardarEnElCierreDelAncestro()
    {
        var dbPath = TestDbFactory.CreateTempDbPath();
        try
        {
            int proyectoId;
            int basicoId;
            int apuId;
            int materialNuevoId;
            int hojaId;
            int agrupadorId;

            using (var context = TestDbFactory.CreateContextAt(dbPath))
            {
                var proyecto = AddProject(context, "Proyecto altas anidadas");
                proyectoId = proyecto.Id;

                var material = AddMaterial(context, proyectoId, "MAT-BASE", 10m);
                var materialNuevo = AddMaterial(context, proyectoId, "MAT-ALT", 5m);

                var basico = AddMatrix(context, proyectoId, "BAS-ALT", TipoMatriz.Basico, 10m);
                AddComponent(
                    context, basico.Id, TipoComponenteMatriz.Material, orden: 1,
                    cantidad: 1m, importe: 10m, materialId: material.Id);

                var apu = AddMatrix(context, proyectoId, "APU-ALT", TipoMatriz.APU, 10m);
                AddComponent(
                    context, apu.Id, TipoComponenteMatriz.Auxiliar, orden: 1,
                    cantidad: 1m, importe: 10m, auxiliarId: basico.Id);

                var hoja = AddLeaf(context, proyectoId, "C-ALT", apu.Id, 3m, "pza", 1);
                hoja.CostoDirectoUnitario = 10m;
                hoja.CostoDirectoTotal = 30m;
                hoja.PrecioUnitario = 10m;
                hoja.ImporteTotal = 30m;

                var agrupador = AddAggregator(context, proyectoId);
                context.SaveChanges();

                basicoId = basico.Id;
                apuId = apu.Id;
                materialNuevoId = materialNuevo.Id;
                hojaId = hoja.Id;
                agrupadorId = agrupador.Id;
            }

            var command = CreateCommand(
                dbPath,
                proyectoId,
                basicoId,
                CreateItem(dbPath, TipoComponenteMatriz.Material, materialNuevoId, 2m));

            var result = BudgetPersistenceService.ExecuteMatrixComponentAdditionCommand(
                new ProjectDbContextFactory(), dbPath, command);

            Assert.AreEqual(BudgetOperationStatus.Committed, result.Status, result.Message);
            var snapshot = result.Confirmed!;

            Assert.AreEqual(20m, snapshot.Matrix.CostoDirecto);
            Assert.AreEqual(2, snapshot.AffectedMatrices.Count);
            Assert.AreEqual(20m, snapshot.AffectedMatrices.Single(m => m.MatrixId == basicoId).CostoDirecto);
            Assert.AreEqual(20m, snapshot.AffectedMatrices.Single(m => m.MatrixId == apuId).CostoDirecto);
            Assert.AreEqual(3, snapshot.AffectedComponents.Count,
                "El básico tiene 2 filas (una nueva) y el APU su auxiliar.");
            Assert.AreEqual(1, snapshot.Leaves.Count);
            Assert.AreEqual(60m, snapshot.Leaves[0].ImporteTotal);
            Assert.AreEqual(1, snapshot.Aggregators.Count);
            Assert.AreEqual(60m, snapshot.Aggregators[0].ImporteTotal);

            using var verificacion = new SOPROContext(dbPath);
            var basicoDb = verificacion.Matrices.AsNoTracking().Single(m => m.Id == basicoId);
            var apuDb = verificacion.Matrices.AsNoTracking().Single(m => m.Id == apuId);
            Assert.AreEqual(20m, basicoDb.CostoDirecto, "La fila nueva entra al cálculo aunque aún no se haya guardado.");
            Assert.AreEqual(20m, apuDb.CostoDirecto, "El ancestro auxiliar se recalcula después del básico.");

            var nueva = verificacion.ComponentesMatriz.AsNoTracking()
                .Single(c => c.Id == snapshot.AddedComponentIds.Single());
            Assert.AreEqual(10m, nueva.Importe);

            var hojaDb = verificacion.ConceptosPresupuesto.AsNoTracking().Single(c => c.Id == hojaId);
            Assert.AreEqual(20m, hojaDb.CostoDirectoUnitario);
            Assert.AreEqual(60m, hojaDb.ImporteTotal);

            var agrupadorDb = verificacion.ConceptosPresupuesto.AsNoTracking().Single(c => c.Id == agrupadorId);
            Assert.AreEqual(60m, agrupadorDb.ImporteTotal);
        }
        finally
        {
            DeleteQuietly(dbPath);
        }
    }

    [TestMethod]
    public void ExecuteMatrixComponentAdditionCommand_CuadrillaEnApu_PersisteAuxiliarYRecalculaHojasYAgrupadores()
    {
        var dbPath = TestDbFactory.CreateTempDbPath();
        try
        {
            int proyectoId;
            int matrizId;
            int hojaId;
            int agrupadorId;
            int cuadrillaId;

            using (var context = TestDbFactory.CreateContextAt(dbPath))
            {
                var scenario = SoproCalculationScenarioBuilder.CreateBaseBudgetScenario(context);
                proyectoId = scenario.Proyecto.Id;
                matrizId = scenario.MatrizApu.Id;
                hojaId = scenario.Concepto.Id;

                var cuadrilla = AddMatrix(context, proyectoId, "CUAD-1", TipoMatriz.Cuadrilla, 50m);
                var agrupador = AddAggregator(context, proyectoId);
                context.SaveChanges();

                cuadrillaId = cuadrilla.Id;
                agrupadorId = agrupador.Id;
            }

            var command = CreateCommand(
                dbPath,
                proyectoId,
                matrizId,
                CreateItem(dbPath, TipoComponenteMatriz.Auxiliar, cuadrillaId, 1m, "cuadrilla"));

            var result = BudgetPersistenceService.ExecuteMatrixComponentAdditionCommand(
                new ProjectDbContextFactory(), dbPath, command);

            Assert.AreEqual(BudgetOperationStatus.Committed, result.Status, result.Message);
            var snapshot = result.Confirmed!;
            Assert.AreEqual(160m, snapshot.Matrix.CostoDirecto,
                "100 base + 50 de cuadrilla + 5 de cada %MO sobre la nueva base de 80.");
            Assert.AreEqual(6, snapshot.AffectedComponents.Count);
            Assert.AreEqual(1, snapshot.Leaves.Count);
            Assert.AreEqual(1600m, snapshot.Leaves[0].ImporteTotal);
            Assert.AreEqual(1, snapshot.Aggregators.Count);
            Assert.AreEqual(1600m, snapshot.Aggregators[0].ImporteTotal);

            var alta = snapshot.AffectedComponents.Single(c => c.ComponentId == snapshot.AddedComponentIds.Single());
            Assert.AreEqual(TipoComponenteMatriz.Auxiliar, alta.TipoComponente);
            Assert.AreEqual(50m, alta.Importe);
            Assert.AreEqual(6, alta.Orden);

            using var verificacion = new SOPROContext(dbPath);
            var nueva = verificacion.ComponentesMatriz.AsNoTracking()
                .Single(c => c.Id == snapshot.AddedComponentIds.Single());
            Assert.AreEqual(TipoComponenteMatriz.Auxiliar, nueva.TipoComponente);
            Assert.AreEqual(cuadrillaId, nueva.AuxiliarId);
            Assert.AreEqual(50m, nueva.Importe, "La cuadrilla entra como hoja con su costo directo.");
            Assert.AreEqual(160m, verificacion.Matrices.AsNoTracking().Single(m => m.Id == matrizId).CostoDirecto);
            Assert.AreEqual(1600m, verificacion.ConceptosPresupuesto.AsNoTracking().Single(c => c.Id == hojaId).ImporteTotal);
            Assert.AreEqual(1600m, verificacion.ConceptosPresupuesto.AsNoTracking().Single(c => c.Id == agrupadorId).ImporteTotal);
        }
        finally
        {
            DeleteQuietly(dbPath);
        }
    }

    [TestMethod]
    public void ExecuteMatrixComponentAdditionCommand_MezclaManoDeObraYCuadrilla_ConfirmaTodoElLote()
    {
        var dbPath = TestDbFactory.CreateTempDbPath();
        try
        {
            int proyectoId;
            int matrizId;
            int manoDeObraId;
            int cuadrillaId;

            using (var context = TestDbFactory.CreateContextAt(dbPath))
            {
                var scenario = SoproCalculationScenarioBuilder.CreateBaseBudgetScenario(context);
                proyectoId = scenario.Proyecto.Id;
                matrizId = scenario.MatrizApu.Id;

                var manoDeObra = AddManoDeObra(context, proyectoId, "MO-NEW", 20m);
                var cuadrilla = AddMatrix(context, proyectoId, "CUAD-MIX", TipoMatriz.Cuadrilla, 50m);
                context.SaveChanges();

                manoDeObraId = manoDeObra.Id;
                cuadrillaId = cuadrilla.Id;
            }

            var command = CreateCommand(
                dbPath,
                proyectoId,
                matrizId,
                CreateItem(dbPath, TipoComponenteMatriz.ManoDeObra, manoDeObraId, 1m),
                CreateItem(dbPath, TipoComponenteMatriz.Auxiliar, cuadrillaId, 1m));

            var result = BudgetPersistenceService.ExecuteMatrixComponentAdditionCommand(
                new ProjectDbContextFactory(), dbPath, command);

            Assert.AreEqual(BudgetOperationStatus.Committed, result.Status, result.Message);
            var snapshot = result.Confirmed!;
            Assert.AreEqual(184m, snapshot.Matrix.CostoDirecto,
                "100 base + 20 de mano de obra + 50 de cuadrilla + 7 de cada %MO sobre la nueva base de 100.");
            Assert.AreEqual(2, snapshot.AddedComponentIds.Count);
            Assert.AreEqual(7, snapshot.AffectedComponents.Count);

            var altas = snapshot.AffectedComponents
                .Where(c => snapshot.AddedComponentIds.Contains(c.ComponentId))
                .OrderBy(c => c.Orden)
                .ToList();
            Assert.AreEqual(TipoComponenteMatriz.ManoDeObra, altas[0].TipoComponente);
            Assert.AreEqual(20m, altas[0].Importe);
            Assert.AreEqual(TipoComponenteMatriz.Auxiliar, altas[1].TipoComponente);
            Assert.AreEqual(50m, altas[1].Importe);

            using var verificacion = new SOPROContext(dbPath);
            Assert.AreEqual(7, verificacion.ComponentesMatriz.AsNoTracking().Count(c => c.MatrizId == matrizId));
            Assert.AreEqual(184m, verificacion.Matrices.AsNoTracking().Single(m => m.Id == matrizId).CostoDirecto);
        }
        finally
        {
            DeleteQuietly(dbPath);
        }
    }

    [TestMethod]
    public void ExecuteMatrixComponentAdditionCommand_BasicoValido_ConfirmaComoAuxiliar()
    {
        var dbPath = TestDbFactory.CreateTempDbPath();
        try
        {
            int proyectoId;
            int matrizId;
            int basicoId;

            using (var context = TestDbFactory.CreateContextAt(dbPath))
            {
                var scenario = SoproCalculationScenarioBuilder.CreateBaseBudgetScenario(context);
                proyectoId = scenario.Proyecto.Id;
                matrizId = scenario.MatrizApu.Id;
                basicoId = AddMatrix(context, proyectoId, "BAS-NEW", TipoMatriz.Basico, 25m).Id;
                context.SaveChanges();
            }

            var command = CreateCommand(
                dbPath,
                proyectoId,
                matrizId,
                CreateItem(dbPath, TipoComponenteMatriz.Auxiliar, basicoId, 2m));

            var result = BudgetPersistenceService.ExecuteMatrixComponentAdditionCommand(
                new ProjectDbContextFactory(), dbPath, command);

            Assert.AreEqual(BudgetOperationStatus.Committed, result.Status, result.Message);
            var snapshot = result.Confirmed!;
            Assert.AreEqual(150m, snapshot.Matrix.CostoDirecto, "100 base + 2 × 25 del básico.");

            using var verificacion = new SOPROContext(dbPath);
            var nueva = verificacion.ComponentesMatriz.AsNoTracking()
                .Single(c => c.Id == snapshot.AddedComponentIds.Single());
            Assert.AreEqual(TipoComponenteMatriz.Auxiliar, nueva.TipoComponente);
            Assert.AreEqual(basicoId, nueva.AuxiliarId);
            Assert.AreEqual(50m, nueva.Importe);
        }
        finally
        {
            DeleteQuietly(dbPath);
        }
    }

    [TestMethod]
    public void ExecuteMatrixComponentAdditionCommand_AuxiliarMaestra_DevuelveConflictoSinEscribir()
    {
        var dbPath = TestDbFactory.CreateTempDbPath();
        try
        {
            int proyectoId;
            int matrizId;
            int auxiliarMaestroId;

            using (var context = TestDbFactory.CreateContextAt(dbPath))
            {
                var scenario = SoproCalculationScenarioBuilder.CreateBaseBudgetScenario(context);
                proyectoId = scenario.Proyecto.Id;
                matrizId = scenario.MatrizApu.Id;

                var auxiliarMaestro = new Matriz
                {
                    ProyectoId = null,
                    Clave = "BAS-MAESTRA",
                    Descripcion = "Básico maestro sin proyecto",
                    Unidad = "m3",
                    Tipo = TipoMatriz.Basico,
                    CostoDirecto = 50m,
                    Notas = string.Empty
                };
                context.Matrices.Add(auxiliarMaestro);
                context.SaveChanges();
                auxiliarMaestroId = auxiliarMaestro.Id;
            }

            var command = CreateCommand(
                dbPath,
                proyectoId,
                matrizId,
                CreateItem(dbPath, TipoComponenteMatriz.Auxiliar, auxiliarMaestroId, 1m));

            var result = BudgetPersistenceService.ExecuteMatrixComponentAdditionCommand(
                new ProjectDbContextFactory(), dbPath, command);

            Assert.AreEqual(BudgetOperationStatus.Conflict, result.Status, result.Message);
            Assert.IsNull(result.Confirmed);
            StringAssert.Contains(result.Message!, "no pertenece al proyecto");

            using var verificacion = new SOPROContext(dbPath);
            Assert.AreEqual(5, verificacion.ComponentesMatriz.Count(c => c.MatrizId == matrizId));
            Assert.AreEqual(100m, verificacion.Matrices.AsNoTracking().Single(m => m.Id == matrizId).CostoDirecto);
        }
        finally
        {
            DeleteQuietly(dbPath);
        }
    }

    [TestMethod]
    public void ExecuteMatrixComponentAdditionCommand_AuxiliarCostoCambio_DevuelveConflictoSinEscribir()
    {
        var dbPath = TestDbFactory.CreateTempDbPath();
        try
        {
            int proyectoId;
            int matrizId;
            int cuadrillaId;

            using (var context = TestDbFactory.CreateContextAt(dbPath))
            {
                var scenario = SoproCalculationScenarioBuilder.CreateBaseBudgetScenario(context);
                proyectoId = scenario.Proyecto.Id;
                matrizId = scenario.MatrizApu.Id;
                cuadrillaId = AddMatrix(context, proyectoId, "CUAD-STALE", TipoMatriz.Cuadrilla, 50m).Id;
                context.SaveChanges();
            }

            var command = CreateCommand(
                dbPath,
                proyectoId,
                matrizId,
                CreateItem(dbPath, TipoComponenteMatriz.Auxiliar, cuadrillaId, 1m));

            using (var otroModulo = new SOPROContext(dbPath))
            {
                otroModulo.Matrices.Single(m => m.Id == cuadrillaId).CostoDirecto = 80m;
                otroModulo.SaveChanges();
            }

            var result = BudgetPersistenceService.ExecuteMatrixComponentAdditionCommand(
                new ProjectDbContextFactory(), dbPath, command);

            Assert.AreEqual(BudgetOperationStatus.Conflict, result.Status, result.Message);
            Assert.IsNull(result.Confirmed);

            using var verificacion = new SOPROContext(dbPath);
            Assert.AreEqual(5, verificacion.ComponentesMatriz.Count(c => c.MatrizId == matrizId));
            Assert.AreEqual(100m, verificacion.Matrices.AsNoTracking().Single(m => m.Id == matrizId).CostoDirecto);
            Assert.AreEqual(80m, verificacion.Matrices.AsNoTracking().Single(m => m.Id == cuadrillaId).CostoDirecto);
        }
        finally
        {
            DeleteQuietly(dbPath);
        }
    }

    [TestMethod]
    public void ExecuteMatrixComponentAdditionCommand_AuxiliarTipoOPertenenciaCambio_DevuelveConflictoSinEscribir()
    {
        var dbPath = TestDbFactory.CreateTempDbPath();
        try
        {
            int proyectoId;
            int matrizId;
            int cuadrillaId;

            using (var context = TestDbFactory.CreateContextAt(dbPath))
            {
                var scenario = SoproCalculationScenarioBuilder.CreateBaseBudgetScenario(context);
                proyectoId = scenario.Proyecto.Id;
                matrizId = scenario.MatrizApu.Id;
                cuadrillaId = AddMatrix(context, proyectoId, "CUAD-MOVE", TipoMatriz.Cuadrilla, 50m).Id;
                context.SaveChanges();
            }

            var command = CreateCommand(
                dbPath,
                proyectoId,
                matrizId,
                CreateItem(dbPath, TipoComponenteMatriz.Auxiliar, cuadrillaId, 1m));

            using (var otroModulo = new SOPROContext(dbPath))
            {
                var otroProyecto = AddProject(otroModulo, "proyecto_ajeno_aux");
                var cuadrilla = otroModulo.Matrices.Single(m => m.Id == cuadrillaId);
                cuadrilla.ProyectoId = otroProyecto.Id;
                cuadrilla.Tipo = TipoMatriz.Basico;
                otroModulo.SaveChanges();
            }

            var result = BudgetPersistenceService.ExecuteMatrixComponentAdditionCommand(
                new ProjectDbContextFactory(), dbPath, command);

            Assert.AreEqual(BudgetOperationStatus.Conflict, result.Status, result.Message);
            Assert.IsNull(result.Confirmed);

            using var verificacion = new SOPROContext(dbPath);
            Assert.AreEqual(5, verificacion.ComponentesMatriz.Count(c => c.MatrizId == matrizId));
            Assert.AreEqual(100m, verificacion.Matrices.AsNoTracking().Single(m => m.Id == matrizId).CostoDirecto);
        }
        finally
        {
            DeleteQuietly(dbPath);
        }
    }

    [TestMethod]
    public void ExecuteMatrixComponentAdditionCommand_AuxiliarEliminada_DevuelveConflictoSinEscribir()
    {
        var dbPath = TestDbFactory.CreateTempDbPath();
        try
        {
            int proyectoId;
            int matrizId;
            int cuadrillaId;

            using (var context = TestDbFactory.CreateContextAt(dbPath))
            {
                var scenario = SoproCalculationScenarioBuilder.CreateBaseBudgetScenario(context);
                proyectoId = scenario.Proyecto.Id;
                matrizId = scenario.MatrizApu.Id;
                cuadrillaId = AddMatrix(context, proyectoId, "CUAD-DEL", TipoMatriz.Cuadrilla, 50m).Id;
                context.SaveChanges();
            }

            var command = CreateCommand(
                dbPath,
                proyectoId,
                matrizId,
                CreateItem(dbPath, TipoComponenteMatriz.Auxiliar, cuadrillaId, 1m));

            using (var otroModulo = new SOPROContext(dbPath))
            {
                otroModulo.Matrices.Remove(otroModulo.Matrices.Single(m => m.Id == cuadrillaId));
                otroModulo.SaveChanges();
            }

            var result = BudgetPersistenceService.ExecuteMatrixComponentAdditionCommand(
                new ProjectDbContextFactory(), dbPath, command);

            Assert.AreEqual(BudgetOperationStatus.Conflict, result.Status, result.Message);
            Assert.IsNull(result.Confirmed);

            using var verificacion = new SOPROContext(dbPath);
            Assert.AreEqual(5, verificacion.ComponentesMatriz.Count(c => c.MatrizId == matrizId));
            Assert.AreEqual(100m, verificacion.Matrices.AsNoTracking().Single(m => m.Id == matrizId).CostoDirecto);
        }
        finally
        {
            DeleteQuietly(dbPath);
        }
    }

    [TestMethod]
    public void ExecuteMatrixComponentAdditionCommand_Autorreferencia_RechazaSinEscribir()
    {
        var dbPath = TestDbFactory.CreateTempDbPath();
        try
        {
            int proyectoId;
            int matrizId;

            using (var context = TestDbFactory.CreateContextAt(dbPath))
            {
                var scenario = SoproCalculationScenarioBuilder.CreateBaseBudgetScenario(context);
                proyectoId = scenario.Proyecto.Id;
                matrizId = scenario.MatrizApu.Id;
            }

            var command = CreateCommand(
                dbPath,
                proyectoId,
                matrizId,
                CreateItem(dbPath, TipoComponenteMatriz.Auxiliar, matrizId, 1m));

            var result = BudgetPersistenceService.ExecuteMatrixComponentAdditionCommand(
                new ProjectDbContextFactory(), dbPath, command);

            Assert.AreEqual(BudgetOperationStatus.Failed, result.Status, result.Message);
            Assert.IsNull(result.Confirmed);
            StringAssert.Contains(result.Message!, "sí misma");

            using var verificacion = new SOPROContext(dbPath);
            Assert.AreEqual(5, verificacion.ComponentesMatriz.Count(c => c.MatrizId == matrizId));
            Assert.AreEqual(100m, verificacion.Matrices.AsNoTracking().Single(m => m.Id == matrizId).CostoDirecto);
        }
        finally
        {
            DeleteQuietly(dbPath);
        }
    }

    [TestMethod]
    public void ExecuteMatrixComponentAdditionCommand_Ciclo_RechazaSinEscribir()
    {
        var dbPath = TestDbFactory.CreateTempDbPath();
        try
        {
            int proyectoId;
            int matrizId;
            int basicoId;

            using (var context = TestDbFactory.CreateContextAt(dbPath))
            {
                var scenario = SoproCalculationScenarioBuilder.CreateBaseBudgetScenario(context);
                proyectoId = scenario.Proyecto.Id;
                matrizId = scenario.MatrizApu.Id;

                // La fuente ya depende del destino: agregarla al destino cerraría el ciclo.
                var basico = AddMatrix(context, proyectoId, "BAS-CICLO", TipoMatriz.Basico, 10m);
                context.ComponentesMatriz.Add(new ComponenteMatriz
                {
                    MatrizId = basico.Id,
                    TipoComponente = TipoComponenteMatriz.Auxiliar,
                    AuxiliarId = matrizId,
                    Cantidad = 1m,
                    Orden = 1,
                    Notas = string.Empty
                });
                context.SaveChanges();
                basicoId = basico.Id;
            }

            var command = CreateCommand(
                dbPath,
                proyectoId,
                matrizId,
                CreateItem(dbPath, TipoComponenteMatriz.Auxiliar, basicoId, 1m));

            var result = BudgetPersistenceService.ExecuteMatrixComponentAdditionCommand(
                new ProjectDbContextFactory(), dbPath, command);

            Assert.AreEqual(BudgetOperationStatus.Failed, result.Status, result.Message);
            Assert.IsNull(result.Confirmed);
            StringAssert.Contains(result.Message!, "ciclo");

            using var verificacion = new SOPROContext(dbPath);
            Assert.AreEqual(5, verificacion.ComponentesMatriz.Count(c => c.MatrizId == matrizId));
            Assert.AreEqual(1, verificacion.ComponentesMatriz.Count(c => c.MatrizId == basicoId));
            Assert.AreEqual(100m, verificacion.Matrices.AsNoTracking().Single(m => m.Id == matrizId).CostoDirecto);
        }
        finally
        {
            DeleteQuietly(dbPath);
        }
    }

    [TestMethod]
    public void ExecuteMatrixComponentAdditionCommand_MatrizCambioEnLaBase_DevuelveConflictoSinEscribir()
    {
        var dbPath = TestDbFactory.CreateTempDbPath();
        try
        {
            int proyectoId;
            int matrizId;
            int materialId;

            using (var context = TestDbFactory.CreateContextAt(dbPath))
            {
                var scenario = SoproCalculationScenarioBuilder.CreateBaseBudgetScenario(context);
                proyectoId = scenario.Proyecto.Id;
                matrizId = scenario.MatrizApu.Id;
                materialId = AddMaterial(context, proyectoId, "MAT-STALE-M", 10m).Id;
                context.SaveChanges();
            }

            var command = CreateCommand(
                dbPath,
                proyectoId,
                matrizId,
                CreateItem(dbPath, TipoComponenteMatriz.Material, materialId, 1m));

            using (var otroModulo = new SOPROContext(dbPath))
            {
                otroModulo.Matrices.Single(m => m.Id == matrizId).CostoDirecto = 999m;
                otroModulo.SaveChanges();
            }

            var result = BudgetPersistenceService.ExecuteMatrixComponentAdditionCommand(
                new ProjectDbContextFactory(), dbPath, command);

            Assert.AreEqual(BudgetOperationStatus.Conflict, result.Status, result.Message);
            Assert.IsNull(result.Confirmed);

            using var verificacion = new SOPROContext(dbPath);
            Assert.AreEqual(5, verificacion.ComponentesMatriz.Count(c => c.MatrizId == matrizId));
            Assert.AreEqual(999m, verificacion.Matrices.AsNoTracking().Single(m => m.Id == matrizId).CostoDirecto);
        }
        finally
        {
            DeleteQuietly(dbPath);
        }
    }

    [TestMethod]
    public void ExecuteMatrixComponentAdditionCommand_ComponenteExistenteCambioEnLaBase_DevuelveConflictoSinEscribir()
    {
        var dbPath = TestDbFactory.CreateTempDbPath();
        try
        {
            int proyectoId;
            int matrizId;
            int materialId;
            int componenteExistenteId;

            using (var context = TestDbFactory.CreateContextAt(dbPath))
            {
                var scenario = SoproCalculationScenarioBuilder.CreateBaseBudgetScenario(context);
                proyectoId = scenario.Proyecto.Id;
                matrizId = scenario.MatrizApu.Id;
                componenteExistenteId = scenario.MatrizApu.Componentes
                    .Single(c => c.TipoComponente == TipoComponenteMatriz.Material).Id;
                materialId = AddMaterial(context, proyectoId, "MAT-STALE-C", 10m).Id;
                context.SaveChanges();
            }

            var command = CreateCommand(
                dbPath,
                proyectoId,
                matrizId,
                CreateItem(dbPath, TipoComponenteMatriz.Material, materialId, 1m));

            using (var otroModulo = new SOPROContext(dbPath))
            {
                // Un cambio que el fingerprint viejo (solo Id/Orden) no detectaría.
                otroModulo.ComponentesMatriz.Single(c => c.Id == componenteExistenteId).Rendimiento = 0.5m;
                otroModulo.SaveChanges();
            }

            var result = BudgetPersistenceService.ExecuteMatrixComponentAdditionCommand(
                new ProjectDbContextFactory(), dbPath, command);

            Assert.AreEqual(BudgetOperationStatus.Conflict, result.Status, result.Message);
            Assert.IsNull(result.Confirmed);

            using var verificacion = new SOPROContext(dbPath);
            Assert.AreEqual(5, verificacion.ComponentesMatriz.Count(c => c.MatrizId == matrizId));
            Assert.AreEqual(0.5m, verificacion.ComponentesMatriz.AsNoTracking()
                .Single(c => c.Id == componenteExistenteId).Rendimiento);
        }
        finally
        {
            DeleteQuietly(dbPath);
        }
    }

    [TestMethod]
    public void ExecuteMatrixComponentAdditionCommand_CatalogoCambioEnLaBase_DevuelveConflictoSinEscribir()
    {
        var dbPath = TestDbFactory.CreateTempDbPath();
        try
        {
            int proyectoId;
            int matrizId;
            int materialId;

            using (var context = TestDbFactory.CreateContextAt(dbPath))
            {
                var scenario = SoproCalculationScenarioBuilder.CreateBaseBudgetScenario(context);
                proyectoId = scenario.Proyecto.Id;
                matrizId = scenario.MatrizApu.Id;
                materialId = AddMaterial(context, proyectoId, "MAT-STALE-K", 10m).Id;
                context.SaveChanges();
            }

            var command = CreateCommand(
                dbPath,
                proyectoId,
                matrizId,
                CreateItem(dbPath, TipoComponenteMatriz.Material, materialId, 1m));

            using (var otroModulo = new SOPROContext(dbPath))
            {
                otroModulo.Materiales.Single(m => m.Id == materialId).PrecioUnitario = 99m;
                otroModulo.SaveChanges();
            }

            var result = BudgetPersistenceService.ExecuteMatrixComponentAdditionCommand(
                new ProjectDbContextFactory(), dbPath, command);

            Assert.AreEqual(BudgetOperationStatus.Conflict, result.Status, result.Message);
            Assert.IsNull(result.Confirmed);

            using var verificacion = new SOPROContext(dbPath);
            Assert.AreEqual(5, verificacion.ComponentesMatriz.Count(c => c.MatrizId == matrizId));
            Assert.AreEqual(99m, verificacion.Materiales.AsNoTracking().Single(m => m.Id == materialId).PrecioUnitario);
        }
        finally
        {
            DeleteQuietly(dbPath);
        }
    }

    [TestMethod]
    public void ExecuteMatrixComponentAdditionCommand_RowsetDelPresupuestoCambio_DevuelveConflictoSinEscribir()
    {
        var dbPath = TestDbFactory.CreateTempDbPath();
        try
        {
            int proyectoId;
            int matrizId;
            int hojaId;
            int materialId;

            using (var context = TestDbFactory.CreateContextAt(dbPath))
            {
                var scenario = SoproCalculationScenarioBuilder.CreateBaseBudgetScenario(context);
                proyectoId = scenario.Proyecto.Id;
                matrizId = scenario.MatrizApu.Id;
                hojaId = scenario.Concepto.Id;
                materialId = AddMaterial(context, proyectoId, "MAT-STALE-R", 10m).Id;
                context.SaveChanges();
            }

            var command = CreateCommand(
                dbPath,
                proyectoId,
                matrizId,
                CreateItem(dbPath, TipoComponenteMatriz.Material, materialId, 1m));

            using (var otroModulo = new SOPROContext(dbPath))
            {
                otroModulo.ConceptosPresupuesto.Single(c => c.Id == hojaId).Cantidad = 11m;
                otroModulo.SaveChanges();
            }

            var result = BudgetPersistenceService.ExecuteMatrixComponentAdditionCommand(
                new ProjectDbContextFactory(), dbPath, command);

            Assert.AreEqual(BudgetOperationStatus.Conflict, result.Status, result.Message);
            Assert.IsNull(result.Confirmed);

            using var verificacion = new SOPROContext(dbPath);
            Assert.AreEqual(5, verificacion.ComponentesMatriz.Count(c => c.MatrizId == matrizId));
            Assert.AreEqual(11m, verificacion.ConceptosPresupuesto.AsNoTracking()
                .Single(c => c.Id == hojaId).Cantidad);
        }
        finally
        {
            DeleteQuietly(dbPath);
        }
    }

    [TestMethod]
    public void ExecuteMatrixComponentAdditionCommand_FalloDeTrigger_RevierteYElMismoComandoReintenta()
    {
        var dbPath = TestDbFactory.CreateTempDbPath();
        try
        {
            int proyectoId;
            int matrizId;
            int hojaId;
            int materialId;

            using (var context = TestDbFactory.CreateContextAt(dbPath))
            {
                var scenario = SoproCalculationScenarioBuilder.CreateBaseBudgetScenario(context);
                proyectoId = scenario.Proyecto.Id;
                matrizId = scenario.MatrizApu.Id;
                hojaId = scenario.Concepto.Id;
                materialId = AddMaterial(context, proyectoId, "MAT-TRIGGER", 20m).Id;
                context.SaveChanges();
            }

            var command = CreateCommand(
                dbPath,
                proyectoId,
                matrizId,
                CreateItem(dbPath, TipoComponenteMatriz.Material, materialId, 1m));

            using (var admin = new SOPROContext(dbPath))
            {
                admin.Database.ExecuteSqlRaw(
                    "CREATE TRIGGER trg_fail_componente_insert BEFORE INSERT ON ComponentesMatriz " +
                    "BEGIN SELECT RAISE(ABORT, 'fallo_simulado'); END;");
            }

            var primerIntento = BudgetPersistenceService.ExecuteMatrixComponentAdditionCommand(
                new ProjectDbContextFactory(), dbPath, command);

            Assert.AreEqual(BudgetOperationStatus.Failed, primerIntento.Status, primerIntento.Message);
            Assert.IsNull(primerIntento.Confirmed);
            StringAssert.Contains(primerIntento.Diagnostic!, "fallo_simulado");

            using (var verificacion = new SOPROContext(dbPath))
            {
                Assert.AreEqual(5, verificacion.ComponentesMatriz.Count(c => c.MatrizId == matrizId),
                    "El trigger revierte la inserción y todo el cierre.");
                Assert.AreEqual(100m, verificacion.Matrices.AsNoTracking().Single(m => m.Id == matrizId).CostoDirecto);
                Assert.AreEqual(1000m, verificacion.ConceptosPresupuesto.AsNoTracking()
                    .Single(c => c.Id == hojaId).ImporteTotal);
            }

            using (var admin = new SOPROContext(dbPath))
            {
                admin.Database.ExecuteSqlRaw("DROP TRIGGER trg_fail_componente_insert;");
            }

            var reintento = BudgetPersistenceService.ExecuteMatrixComponentAdditionCommand(
                new ProjectDbContextFactory(), dbPath, command);

            Assert.AreEqual(BudgetOperationStatus.Committed, reintento.Status, reintento.Message);

            using var verificacionFinal = new SOPROContext(dbPath);
            Assert.AreEqual(6, verificacionFinal.ComponentesMatriz.Count(c => c.MatrizId == matrizId));
            Assert.AreEqual(120m, verificacionFinal.Matrices.AsNoTracking()
                .Single(m => m.Id == matrizId).CostoDirecto);
            Assert.AreEqual(1200m, verificacionFinal.ConceptosPresupuesto.AsNoTracking()
                .Single(c => c.Id == hojaId).ImporteTotal);

            var nueva = verificacionFinal.ComponentesMatriz.AsNoTracking()
                .Single(c => c.MatrizId == matrizId && c.Id == reintento.Confirmed!.AddedComponentIds.Single());
            Assert.AreEqual(20m, nueva.Importe);
            Assert.AreEqual(6, nueva.Orden);
        }
        finally
        {
            DeleteQuietly(dbPath);
        }
    }

    [TestMethod]
    public void ExecuteMatrixComponentAdditionCommand_LoteVacio_EsNoOpYNoEscribe()
    {
        var dbPath = TestDbFactory.CreateTempDbPath();
        try
        {
            int proyectoId;
            int matrizId;
            int hojaId;
            int agrupadorId;

            using (var context = TestDbFactory.CreateContextAt(dbPath))
            {
                var scenario = SoproCalculationScenarioBuilder.CreateBaseBudgetScenario(context);
                proyectoId = scenario.Proyecto.Id;
                matrizId = scenario.MatrizApu.Id;
                hojaId = scenario.Concepto.Id;
                var agrupador = AddAggregator(context, proyectoId);
                context.SaveChanges();
                agrupadorId = agrupador.Id;
            }

            var command = CreateCommand(dbPath, proyectoId, matrizId);

            using (var admin = new SOPROContext(dbPath))
            {
                admin.Database.ExecuteSqlRaw(
                    "CREATE TRIGGER trg_noop_componente BEFORE INSERT ON ComponentesMatriz " +
                    "BEGIN SELECT RAISE(ABORT, 'noop'); END;");
                admin.Database.ExecuteSqlRaw(
                    "CREATE TRIGGER trg_noop_matriz BEFORE UPDATE ON Matrices " +
                    "BEGIN SELECT RAISE(ABORT, 'noop'); END;");
                admin.Database.ExecuteSqlRaw(
                    "CREATE TRIGGER trg_noop_concepto BEFORE UPDATE ON ConceptosPresupuesto " +
                    "BEGIN SELECT RAISE(ABORT, 'noop'); END;");
            }

            var result = BudgetPersistenceService.ExecuteMatrixComponentAdditionCommand(
                new ProjectDbContextFactory(), dbPath, command);

            Assert.AreEqual(BudgetOperationStatus.Committed, result.Status, result.Message);
            Assert.IsNotNull(result.Confirmed);
            Assert.AreEqual(0, result.Confirmed!.AddedComponentIds.Count);
            Assert.AreEqual(5, result.Confirmed.AffectedComponents.Count);
            Assert.AreEqual(1, result.Confirmed.AffectedMatrices.Count);
            Assert.AreEqual(100m, result.Confirmed.Matrix.CostoDirecto);

            using var verificacion = new SOPROContext(dbPath);
            Assert.AreEqual(5, verificacion.ComponentesMatriz.Count(c => c.MatrizId == matrizId));
            Assert.AreEqual(100m, verificacion.Matrices.AsNoTracking().Single(m => m.Id == matrizId).CostoDirecto);
            Assert.AreEqual(1000m, verificacion.ConceptosPresupuesto.AsNoTracking()
                .Single(c => c.Id == hojaId).ImporteTotal);
            Assert.AreEqual(1000m, verificacion.ConceptosPresupuesto.AsNoTracking()
                .Single(c => c.Id == agrupadorId).ImporteTotal);
        }
        finally
        {
            DeleteQuietly(dbPath);
        }
    }

    // ────────────────────────────────────────────────────────────────────────
    // Infraestructura
    // ────────────────────────────────────────────────────────────────────────

    private static BudgetMatrixComponentAdditionCommand CreateCommand(
        string dbPath,
        int proyectoId,
        int matrizId,
        params BudgetMatrixComponentAdditionItem[] additions)
    {
        using var context = new SOPROContext(dbPath);

        var proyecto = context.Proyectos.AsNoTracking().Single(p => p.Id == proyectoId);
        var matriz = context.Matrices
            .Include(m => m.Componentes)
            .AsNoTracking()
            .Single(m => m.Id == matrizId);

        var componentes = matriz.Componentes
            .OrderBy(c => c.Orden)
            .ThenBy(c => c.Id)
            .ToList();

        var filas = context.ConceptosPresupuesto
            .AsNoTracking()
            .Where(c => c.ProyectoId == proyectoId)
            .OrderBy(c => c.Orden)
            .ThenBy(c => c.Id)
            .ToList();

        return new BudgetMatrixComponentAdditionCommand(
            proyectoId,
            BudgetProjectCalculationBase.FromProyecto(proyecto),
            BudgetMatrixCalculationBase.FromMatriz(matriz),
            BudgetMatrixComponentStateFingerprint.FromComponentes(componentes),
            BudgetPriceRefreshRowset.FromConceptos(filas),
            additions);
    }

    private static BudgetMatrixComponentAdditionItem CreateItem(
        string dbPath,
        TipoComponenteMatriz tipo,
        int sourceId,
        decimal cantidad,
        string? notas = null)
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
            _ => throw new InvalidOperationException($"Tipo no soportado por la prueba: {tipo}.")
        };

        return new BudgetMatrixComponentAdditionItem(tipo, sourceId, source, cantidad, notas);
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
            FechaModificacion = new DateTime(2026, 1, 1)
        };

        context.Proyectos.Add(proyecto);
        context.SaveChanges();
        return proyecto;
    }

    private static Material AddMaterial(
        SOPROContext context,
        int proyectoId,
        string clave,
        decimal precioUnitario)
    {
        var material = new Material
        {
            ProyectoId = proyectoId,
            Clave = clave,
            Descripcion = "Material de prueba",
            Unidad = "kg",
            PrecioUnitario = precioUnitario,
            Notas = string.Empty
        };

        context.Materiales.Add(material);
        context.SaveChanges();
        return material;
    }

    private static Matriz AddMatrix(
        SOPROContext context,
        int proyectoId,
        string clave,
        TipoMatriz tipo,
        decimal costoDirecto)
    {
        var matriz = new Matriz
        {
            ProyectoId = proyectoId,
            Clave = clave,
            Descripcion = "Matriz de prueba",
            Unidad = "pza",
            Tipo = tipo,
            CostoDirecto = costoDirecto,
            Notas = string.Empty
        };

        context.Matrices.Add(matriz);
        context.SaveChanges();
        return matriz;
    }

    private static ManoDeObra AddManoDeObra(
        SOPROContext context,
        int proyectoId,
        string clave,
        decimal salarioReal)
    {
        var manoDeObra = new ManoDeObra
        {
            ProyectoId = proyectoId,
            Clave = clave,
            Descripcion = "Mano de obra de prueba",
            Unidad = "jor",
            SalarioBase = salarioReal,
            FactorSalarioReal = 1m,
            SalarioReal = salarioReal,
            Notas = string.Empty
        };

        context.ManoDeObra.Add(manoDeObra);
        context.SaveChanges();
        return manoDeObra;
    }

    private static ComponenteMatriz AddComponent(
        SOPROContext context,
        int matrizId,
        TipoComponenteMatriz tipo,
        int orden,
        decimal cantidad,
        decimal importe,
        int? materialId = null,
        int? auxiliarId = null)
    {
        var componente = new ComponenteMatriz
        {
            MatrizId = matrizId,
            TipoComponente = tipo,
            MaterialId = materialId,
            AuxiliarId = auxiliarId,
            Cantidad = cantidad,
            Importe = importe,
            Orden = orden,
            Notas = string.Empty
        };

        context.ComponentesMatriz.Add(componente);
        return componente;
    }

    private static ConceptoPresupuesto AddAggregator(SOPROContext context, int proyectoId)
    {
        var agrupador = new ConceptoPresupuesto
        {
            ProyectoId = proyectoId,
            Clave = "CAP-1",
            Descripcion = "Capítulo de prueba",
            Unidad = string.Empty,
            Cantidad = 0m,
            CostoDirectoUnitario = 0m,
            CostoDirectoTotal = 1000m,
            PrecioUnitario = 0m,
            ImporteTotal = 1000m,
            Nivel = 0,
            Orden = 0,
            EsAgrupador = true,
            ColumnasPersonalizadasJSON = string.Empty,
            Notas = string.Empty
        };

        context.ConceptosPresupuesto.Add(agrupador);
        return agrupador;
    }

    private static ConceptoPresupuesto AddLeaf(
        SOPROContext context,
        int proyectoId,
        string clave,
        int matrizId,
        decimal cantidad,
        string unidad,
        int orden)
    {
        var hoja = new ConceptoPresupuesto
        {
            ProyectoId = proyectoId,
            Clave = clave,
            Descripcion = "Concepto de prueba",
            Unidad = unidad,
            Cantidad = cantidad,
            MatrizId = matrizId,
            CostoDirectoUnitario = 100m,
            CostoDirectoTotal = cantidad * 100m,
            PrecioUnitario = 100m,
            ImporteTotal = cantidad * 100m,
            Nivel = 1,
            Orden = orden,
            EsAgrupador = false,
            ColumnasPersonalizadasJSON = string.Empty,
            Notas = string.Empty
        };

        context.ConceptosPresupuesto.Add(hoja);
        return hoja;
    }

    private static void DeleteQuietly(string dbPath)
    {
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        for (int intento = 0; intento < 3; intento++)
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
