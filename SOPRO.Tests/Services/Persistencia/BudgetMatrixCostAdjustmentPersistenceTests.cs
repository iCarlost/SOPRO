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
/// Contrato del comando único de reajuste de costo de una matriz a partir de un
/// concepto hoja (ADR-0001): intención inmutable con bases esperadas del proyecto y
/// de TODO el cierre ascendente (cada matriz con costo/tipo/propiedad, fingerprint
/// económico completo de sus componentes y fuentes relevantes), el rowset COMPLETO
/// del presupuesto y el concepto hoja; revalidación exacta contra la base, conversión
/// del objetivo (P.U. o importe) con la precisión de pantalla, semántica canónica de
/// reajuste, cierre topológico de auxiliares, recálculo de TODAS las hojas afectadas
/// y de los agrupadores, y un solo commit.
/// </summary>
[TestClass]
public class BudgetMatrixCostAdjustmentPersistenceTests
{
    [TestMethod]
    public void ExecuteMatrixCostAdjustmentCommand_ObjetivoPU_DosHojasMismaMatriz_RecalculaAmbasYElAgrupador()
    {
        var dbPath = TestDbFactory.CreateTempDbPath();
        try
        {
            int proyectoId;
            int matrizId;
            int primeraHojaId;
            int segundaHojaId;
            int agrupadorId;

            using (var context = TestDbFactory.CreateContextAt(dbPath))
            {
                var scenario = SoproCalculationScenarioBuilder.CreateBaseBudgetScenario(context);
                proyectoId = scenario.Proyecto.Id;
                matrizId = scenario.MatrizApu.Id;
                primeraHojaId = scenario.Concepto.Id;

                var agrupador = AddAggregator(context, proyectoId);
                var segundaHoja = AddLeaf(context, proyectoId, "C-002", matrizId, 5m, "pza", 2);
                context.SaveChanges();

                agrupadorId = agrupador.Id;
                segundaHojaId = segundaHoja.Id;
            }

            var command = CreateCommand(
                dbPath, proyectoId, matrizId, primeraHojaId,
                MatrixAdjustmentScopes.Materiales,
                BudgetMatrixCostAdjustmentTargetKind.PrecioUnitario,
                160m);

            var result = BudgetPersistenceService.ExecuteMatrixCostAdjustmentCommand(
                new ProjectDbContextFactory(), dbPath, command);

            Assert.AreEqual(BudgetOperationStatus.Committed, result.Status, result.Message);
            Assert.IsNull(result.Diagnostic);
            Assert.IsNotNull(result.Confirmed);

            // 120 material + 30 MO + 3 %MO + 3 herramienta + 4 maquinaria.
            const decimal costoDirectoEsperado = 160m;

            using (var verificacion = new SOPROContext(dbPath))
            {
                var matriz = verificacion.Matrices.AsNoTracking().Single(m => m.Id == matrizId);
                Assert.AreEqual(costoDirectoEsperado, matriz.CostoDirecto);

                var primera = verificacion.ConceptosPresupuesto.AsNoTracking().Single(c => c.Id == primeraHojaId);
                var segunda = verificacion.ConceptosPresupuesto.AsNoTracking().Single(c => c.Id == segundaHojaId);

                Assert.AreEqual(costoDirectoEsperado, primera.PrecioUnitario);
                Assert.AreEqual(1600m, primera.ImporteTotal);
                Assert.AreEqual(costoDirectoEsperado, segunda.PrecioUnitario);
                Assert.AreEqual(800m, segunda.ImporteTotal);

                var agrupador = verificacion.ConceptosPresupuesto.AsNoTracking().Single(c => c.Id == agrupadorId);
                Assert.AreEqual(2400m, agrupador.ImporteTotal,
                    "El agrupador se recalcula con la política única sobre TODAS las hojas afectadas.");
            }

            var snapshot = result.Confirmed!;
            Assert.AreEqual(100m, snapshot.CurrentCost);
            Assert.AreEqual(160m, snapshot.TargetCost);
            Assert.AreEqual(160m, snapshot.AchievedCost);
            Assert.IsTrue(Math.Abs(snapshot.FactorApplied - 2m) < 0.01m,
                $"El factor aplicado debe aproximarse a 2; fue {snapshot.FactorApplied}.");
            Assert.AreEqual(primeraHojaId, snapshot.SelectedLeafConceptId);
            Assert.AreEqual(BudgetMatrixCostAdjustmentTargetKind.PrecioUnitario, snapshot.TargetKind);
            Assert.AreEqual(160m, snapshot.RequestedTargetValue);
            Assert.AreEqual(100m, snapshot.CurrentTargetValue);
            Assert.AreEqual(160m, snapshot.AchievedTargetValue);
            Assert.AreEqual(160m, snapshot.Matrix.CostoDirecto);
            Assert.AreEqual(1, snapshot.AffectedMatrices.Count);
            Assert.AreEqual(2, snapshot.Leaves.Count);
            Assert.AreEqual(1, snapshot.Aggregators.Count);
            Assert.AreEqual(2400m, snapshot.Aggregators[0].ImporteTotal);
        }
        finally
        {
            DeleteQuietly(dbPath);
        }
    }

    [TestMethod]
    public void ExecuteMatrixCostAdjustmentCommand_ObjetivoImporte_ConviertePorCantidadConPrecisionDePantalla()
    {
        var dbPath = TestDbFactory.CreateTempDbPath();
        try
        {
            int proyectoId;
            int matrizId;
            int hojaId;

            using (var context = TestDbFactory.CreateContextAt(dbPath))
            {
                var scenario = CreateSimpleBudget(context, utilidad: 0m);
                proyectoId = scenario.ProjectId;
                matrizId = scenario.MatrixId;
                hojaId = scenario.LeafId;
            }

            // El concepto tiene cantidad 3 y el objetivo es 1,000.00 de importe.
            using (var ajuste = new SOPROContext(dbPath))
            {
                var hoja = ajuste.ConceptosPresupuesto.Single(c => c.Id == hojaId);
                hoja.Cantidad = 3m;
                hoja.CostoDirectoTotal = 300m;
                hoja.ImporteTotal = 300m;
                ajuste.SaveChanges();
            }

            var command = CreateCommand(
                dbPath, proyectoId, matrizId, hojaId,
                MatrixAdjustmentScopes.Materiales,
                BudgetMatrixCostAdjustmentTargetKind.Importe,
                1000m);

            var result = BudgetPersistenceService.ExecuteMatrixCostAdjustmentCommand(
                new ProjectDbContextFactory(), dbPath, command);

            Assert.AreEqual(BudgetOperationStatus.Committed, result.Status, result.Message);
            var snapshot = result.Confirmed!;

            // 1,000.00 / 3 se redondea a P.U. 333.33 con la precisión de pantalla;
            // con factor 1 el costo directo objetivo es ese mismo P.U.
            Assert.AreEqual(333.33m, snapshot.TargetCost);
            Assert.AreEqual(BudgetMatrixCostAdjustmentTargetKind.Importe, snapshot.TargetKind);
            Assert.AreEqual(1000m, snapshot.RequestedTargetValue);
        }
        finally
        {
            DeleteQuietly(dbPath);
        }
    }

    [TestMethod]
    public void ExecuteMatrixCostAdjustmentCommand_ObjetivoPU_RedondeaConPrecisionDePantalla()
    {
        var dbPath = TestDbFactory.CreateTempDbPath();
        try
        {
            int proyectoId;
            int matrizId;
            int hojaId;

            using (var context = TestDbFactory.CreateContextAt(dbPath))
            {
                var scenario = CreateSimpleBudget(context, utilidad: 0m);
                proyectoId = scenario.ProjectId;
                matrizId = scenario.MatrixId;
                hojaId = scenario.LeafId;
            }

            var command = CreateCommand(
                dbPath, proyectoId, matrizId, hojaId,
                MatrixAdjustmentScopes.Materiales,
                BudgetMatrixCostAdjustmentTargetKind.PrecioUnitario,
                120.004m);

            var result = BudgetPersistenceService.ExecuteMatrixCostAdjustmentCommand(
                new ProjectDbContextFactory(), dbPath, command);

            Assert.AreEqual(BudgetOperationStatus.Committed, result.Status, result.Message);
            var snapshot = result.Confirmed!;

            Assert.AreEqual(120m, snapshot.TargetCost,
                "El P.U. objetivo se redondea con los decimales de importe del proyecto.");
            Assert.AreEqual(120m, snapshot.AchievedCost);
        }
        finally
        {
            DeleteQuietly(dbPath);
        }
    }

    [TestMethod]
    public void ExecuteMatrixCostAdjustmentCommand_ObjetivoPU_ConvierteConFactorDelProyecto()
    {
        var dbPath = TestDbFactory.CreateTempDbPath();
        try
        {
            int proyectoId;
            int matrizId;
            int hojaId;

            using (var context = TestDbFactory.CreateContextAt(dbPath))
            {
                var scenario = CreateSimpleBudget(context, utilidad: 10m);
                proyectoId = scenario.ProjectId;
                matrizId = scenario.MatrixId;
                hojaId = scenario.LeafId;
            }

            // CD 100, factor 1.10 => P.U. 110. Objetivo P.U. 121 => CD objetivo 110.
            var command = CreateCommand(
                dbPath, proyectoId, matrizId, hojaId,
                MatrixAdjustmentScopes.Materiales,
                BudgetMatrixCostAdjustmentTargetKind.PrecioUnitario,
                121m);

            var result = BudgetPersistenceService.ExecuteMatrixCostAdjustmentCommand(
                new ProjectDbContextFactory(), dbPath, command);

            Assert.AreEqual(BudgetOperationStatus.Committed, result.Status, result.Message);
            var snapshot = result.Confirmed!;

            Assert.AreEqual(110m, snapshot.TargetCost);
            Assert.AreEqual(110m, snapshot.AchievedCost);
            Assert.AreEqual(121m, snapshot.AchievedTargetValue);

            using var verificacion = new SOPROContext(dbPath);
            var hoja = verificacion.ConceptosPresupuesto.AsNoTracking().Single(c => c.Id == hojaId);
            Assert.AreEqual(121m, hoja.PrecioUnitario);
            Assert.AreEqual(605m, hoja.ImporteTotal);
        }
        finally
        {
            DeleteQuietly(dbPath);
        }
    }

    [TestMethod]
    public void ExecuteMatrixCostAdjustmentCommand_ObjetivoImporte_ConvierteConFactorDelProyecto()
    {
        var dbPath = TestDbFactory.CreateTempDbPath();
        try
        {
            int proyectoId;
            int matrizId;
            int hojaId;

            using (var context = TestDbFactory.CreateContextAt(dbPath))
            {
                var scenario = CreateSimpleBudget(context, utilidad: 10m);
                proyectoId = scenario.ProjectId;
                matrizId = scenario.MatrixId;
                hojaId = scenario.LeafId;
            }

            // Hoja cantidad 5; importe objetivo 605 => P.U. 121 => CD objetivo 110.
            var command = CreateCommand(
                dbPath, proyectoId, matrizId, hojaId,
                MatrixAdjustmentScopes.Materiales,
                BudgetMatrixCostAdjustmentTargetKind.Importe,
                605m);

            var result = BudgetPersistenceService.ExecuteMatrixCostAdjustmentCommand(
                new ProjectDbContextFactory(), dbPath, command);

            Assert.AreEqual(BudgetOperationStatus.Committed, result.Status, result.Message);
            var snapshot = result.Confirmed!;

            Assert.AreEqual(110m, snapshot.TargetCost);
            Assert.AreEqual(605m, snapshot.AchievedTargetValue);

            using var verificacion = new SOPROContext(dbPath);
            var hoja = verificacion.ConceptosPresupuesto.AsNoTracking().Single(c => c.Id == hojaId);
            Assert.AreEqual(605m, hoja.ImporteTotal);
        }
        finally
        {
            DeleteQuietly(dbPath);
        }
    }

    [TestMethod]
    public void ExecuteMatrixCostAdjustmentCommand_MatrizAnidada_RecalculaCierreTopologicoYHojaDelAncestro()
    {
        var dbPath = TestDbFactory.CreateTempDbPath();
        try
        {
            int proyectoId;
            int matrizBasicoId;
            int matrizApuId;
            int hojaBasicoId;
            int hojaApuId;

            using (var context = TestDbFactory.CreateContextAt(dbPath))
            {
                var proyecto = AddProject(context, utilidad: 0m);
                proyectoId = proyecto.Id;

                var material = AddMaterial(context, proyectoId, "MAT-NEST", 10m);

                var basico = AddMatrix(context, proyectoId, "BAS-001", TipoMatriz.Basico);
                AddComponent(context, basico, TipoComponenteMatriz.Material, orden: 1,
                    cantidad: 2m, material: material);
                RecalculateMatrix(context, proyecto, basico);

                var apu = AddMatrix(context, proyectoId, "APU-NEST", TipoMatriz.APU);
                AddComponent(context, apu, TipoComponenteMatriz.Auxiliar, orden: 1,
                    cantidad: 3m, auxiliar: basico);
                RecalculateMatrix(context, proyecto, apu);

                var hojaBasico = AddLeaf(context, proyectoId, "C-BAS", basico.Id, 1m, "pza", 1,
                    proyecto: proyecto);
                var hojaApu = AddLeaf(context, proyectoId, "C-APU", apu.Id, 1m, "pza", 2,
                    proyecto: proyecto);
                context.SaveChanges();

                matrizBasicoId = basico.Id;
                matrizApuId = apu.Id;
                hojaBasicoId = hojaBasico.Id;
                hojaApuId = hojaApu.Id;
            }

            var command = CreateCommand(
                dbPath, proyectoId, matrizBasicoId, hojaBasicoId,
                MatrixAdjustmentScopes.Materiales,
                BudgetMatrixCostAdjustmentTargetKind.PrecioUnitario,
                40m);

            var result = BudgetPersistenceService.ExecuteMatrixCostAdjustmentCommand(
                new ProjectDbContextFactory(), dbPath, command);

            Assert.AreEqual(BudgetOperationStatus.Committed, result.Status, result.Message);

            using (var verificacion = new SOPROContext(dbPath))
            {
                var basico = verificacion.Matrices.AsNoTracking().Single(m => m.Id == matrizBasicoId);
                var apu = verificacion.Matrices.AsNoTracking().Single(m => m.Id == matrizApuId);

                Assert.AreEqual(40m, basico.CostoDirecto, "El básico se reajusta primero.");
                Assert.AreEqual(120m, apu.CostoDirecto, "El ancestro consume el costo ya fresco del básico.");

                var hojaBasico = verificacion.ConceptosPresupuesto.AsNoTracking().Single(c => c.Id == hojaBasicoId);
                Assert.AreEqual(40m, hojaBasico.PrecioUnitario);
                Assert.AreEqual(40m, hojaBasico.ImporteTotal);

                var hojaApu = verificacion.ConceptosPresupuesto.AsNoTracking().Single(c => c.Id == hojaApuId);
                Assert.AreEqual(120m, hojaApu.CostoDirectoUnitario);
                Assert.AreEqual(120m, hojaApu.PrecioUnitario);
                Assert.AreEqual(120m, hojaApu.ImporteTotal);
            }

            var snapshot = result.Confirmed!;
            Assert.AreEqual(2, snapshot.AffectedMatrices.Count);
            Assert.AreEqual(40m, snapshot.AffectedMatrices.Single(m => m.MatrixId == matrizBasicoId).CostoDirecto);
            Assert.AreEqual(120m, snapshot.AffectedMatrices.Single(m => m.MatrixId == matrizApuId).CostoDirecto);
            Assert.AreEqual(hojaBasicoId, snapshot.SelectedLeafConceptId);
            Assert.AreEqual(2, snapshot.Leaves.Count);
            Assert.AreEqual(40m, snapshot.Leaves.Single(l => l.ConceptId == hojaBasicoId).ImporteTotal);
            Assert.AreEqual(120m, snapshot.Leaves.Single(l => l.ConceptId == hojaApuId).ImporteTotal);
        }
        finally
        {
            DeleteQuietly(dbPath);
        }
    }

    [TestMethod]
    public void ExecuteMatrixCostAdjustmentCommand_AlcanceNoSoportado_RechazaSinEscribir()
    {
        var dbPath = TestDbFactory.CreateTempDbPath();
        try
        {
            int proyectoId;
            int matrizId;
            int hojaId;

            using (var context = TestDbFactory.CreateContextAt(dbPath))
            {
                var scenario = CreateSimpleBudget(context, utilidad: 0m);
                proyectoId = scenario.ProjectId;
                matrizId = scenario.MatrixId;
                hojaId = scenario.LeafId;
            }

            foreach (var scopes in new[]
            {
                MatrixAdjustmentScopes.None,
                (MatrixAdjustmentScopes)16
            })
            {
                var command = CreateCommand(
                    dbPath, proyectoId, matrizId, hojaId,
                    scopes,
                    BudgetMatrixCostAdjustmentTargetKind.PrecioUnitario,
                    120m);

                var result = BudgetPersistenceService.ExecuteMatrixCostAdjustmentCommand(
                    new ProjectDbContextFactory(), dbPath, command);

                Assert.AreEqual(BudgetOperationStatus.Failed, result.Status, result.Message);
                Assert.IsNull(result.Confirmed);
            }

            using var verificacion = new SOPROContext(dbPath);
            var matriz = verificacion.Matrices.AsNoTracking().Single(m => m.Id == matrizId);
            Assert.AreEqual(100m, matriz.CostoDirecto, "Un alcance inválido no escribe nada.");
        }
        finally
        {
            DeleteQuietly(dbPath);
        }
    }

    [TestMethod]
    public void ExecuteMatrixCostAdjustmentCommand_MontoMenorAlMinimo_RechazaSinEscribir()
    {
        var dbPath = TestDbFactory.CreateTempDbPath();
        try
        {
            int proyectoId;
            int matrizId;
            int hojaId;

            using (var context = TestDbFactory.CreateContextAt(dbPath))
            {
                var proyecto = AddProject(context, utilidad: 0m);
                proyectoId = proyecto.Id;

                var material = AddMaterial(context, proyectoId, "MAT-MIN", 10m);
                var manoObra = AddManoDeObra(context, proyectoId, "MO-MIN", 50m);

                var matriz = AddMatrix(context, proyectoId, "APU-MIN", TipoMatriz.APU);
                AddComponent(context, matriz, TipoComponenteMatriz.Material, orden: 1,
                    cantidad: 10m, material: material);
                AddComponent(context, matriz, TipoComponenteMatriz.ManoDeObra, orden: 2,
                    cantidad: 1m, manoDeObra: manoObra);
                RecalculateMatrix(context, proyecto, matriz);

                var hoja = AddLeaf(context, proyectoId, "C-MIN", matriz.Id, 1m, "pza", 1,
                    proyecto: proyecto);
                context.SaveChanges();

                matrizId = matriz.Id;
                hojaId = hoja.Id;
            }

            // El mínimo con solo materiales es el costo fijo de la mano de obra (50).
            var command = CreateCommand(
                dbPath, proyectoId, matrizId, hojaId,
                MatrixAdjustmentScopes.Materiales,
                BudgetMatrixCostAdjustmentTargetKind.PrecioUnitario,
                40m);

            var result = BudgetPersistenceService.ExecuteMatrixCostAdjustmentCommand(
                new ProjectDbContextFactory(), dbPath, command);

            Assert.AreEqual(BudgetOperationStatus.Failed, result.Status, result.Message);
            Assert.IsNull(result.Confirmed);
            StringAssert.Contains(result.Message!, "mínimo alcanzable");

            using var verificacion = new SOPROContext(dbPath);
            var matrizVerificada = verificacion.Matrices.AsNoTracking().Single(m => m.Id == matrizId);
            Assert.AreEqual(150m, matrizVerificada.CostoDirecto, "El fallo no deja cantidades parciales.");
        }
        finally
        {
            DeleteQuietly(dbPath);
        }
    }

    [TestMethod]
    public void ExecuteMatrixCostAdjustmentCommand_ComponenteCambioEnLaBase_DevuelveConflictoSinEscribir()
    {
        var dbPath = TestDbFactory.CreateTempDbPath();
        try
        {
            int proyectoId;
            int matrizId;
            int hojaId;
            int componenteId;

            using (var context = TestDbFactory.CreateContextAt(dbPath))
            {
                var scenario = CreateSimpleBudget(context, utilidad: 0m);
                proyectoId = scenario.ProjectId;
                matrizId = scenario.MatrixId;
                hojaId = scenario.LeafId;
                componenteId = scenario.MaterialComponentId;
            }

            var command = CreateCommand(
                dbPath, proyectoId, matrizId, hojaId,
                MatrixAdjustmentScopes.Materiales,
                BudgetMatrixCostAdjustmentTargetKind.PrecioUnitario,
                120m);

            using (var otroModulo = new SOPROContext(dbPath))
            {
                otroModulo.ComponentesMatriz.Single(c => c.Id == componenteId).Importe = 99m;
                otroModulo.SaveChanges();
            }

            var result = BudgetPersistenceService.ExecuteMatrixCostAdjustmentCommand(
                new ProjectDbContextFactory(), dbPath, command);

            Assert.AreEqual(BudgetOperationStatus.Conflict, result.Status, result.Message);
            Assert.IsNull(result.Confirmed);

            using var verificacion = new SOPROContext(dbPath);
            var matriz = verificacion.Matrices.AsNoTracking().Single(m => m.Id == matrizId);
            Assert.AreEqual(100m, matriz.CostoDirecto);
        }
        finally
        {
            DeleteQuietly(dbPath);
        }
    }

    [TestMethod]
    public void ExecuteMatrixCostAdjustmentCommand_InsumoCambioEnLaBase_DevuelveConflictoSinEscribir()
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
                var scenario = CreateSimpleBudget(context, utilidad: 0m);
                proyectoId = scenario.ProjectId;
                matrizId = scenario.MatrixId;
                hojaId = scenario.LeafId;
                materialId = scenario.MaterialId;
            }

            var command = CreateCommand(
                dbPath, proyectoId, matrizId, hojaId,
                MatrixAdjustmentScopes.Materiales,
                BudgetMatrixCostAdjustmentTargetKind.PrecioUnitario,
                120m);

            using (var otroModulo = new SOPROContext(dbPath))
            {
                otroModulo.Materiales.Single(m => m.Id == materialId).PrecioUnitario = 11m;
                otroModulo.SaveChanges();
            }

            var result = BudgetPersistenceService.ExecuteMatrixCostAdjustmentCommand(
                new ProjectDbContextFactory(), dbPath, command);

            Assert.AreEqual(BudgetOperationStatus.Conflict, result.Status, result.Message);
            Assert.IsNull(result.Confirmed);

            using var verificacion = new SOPROContext(dbPath);
            var matriz = verificacion.Matrices.AsNoTracking().Single(m => m.Id == matrizId);
            Assert.AreEqual(100m, matriz.CostoDirecto);
        }
        finally
        {
            DeleteQuietly(dbPath);
        }
    }

    [TestMethod]
    public void ExecuteMatrixCostAdjustmentCommand_ProyectoCambioEnLaBase_DevuelveConflictoSinEscribir()
    {
        var dbPath = TestDbFactory.CreateTempDbPath();
        try
        {
            int proyectoId;
            int matrizId;
            int hojaId;

            using (var context = TestDbFactory.CreateContextAt(dbPath))
            {
                var scenario = CreateSimpleBudget(context, utilidad: 0m);
                proyectoId = scenario.ProjectId;
                matrizId = scenario.MatrixId;
                hojaId = scenario.LeafId;
            }

            var command = CreateCommand(
                dbPath, proyectoId, matrizId, hojaId,
                MatrixAdjustmentScopes.Materiales,
                BudgetMatrixCostAdjustmentTargetKind.PrecioUnitario,
                120m);

            using (var otroModulo = new SOPROContext(dbPath))
            {
                otroModulo.Proyectos.Single(p => p.Id == proyectoId).PorcentajeUtilidad = 10m;
                otroModulo.SaveChanges();
            }

            var result = BudgetPersistenceService.ExecuteMatrixCostAdjustmentCommand(
                new ProjectDbContextFactory(), dbPath, command);

            Assert.AreEqual(BudgetOperationStatus.Conflict, result.Status, result.Message);
            Assert.IsNull(result.Confirmed);

            using var verificacion = new SOPROContext(dbPath);
            var matriz = verificacion.Matrices.AsNoTracking().Single(m => m.Id == matrizId);
            Assert.AreEqual(100m, matriz.CostoDirecto);
        }
        finally
        {
            DeleteQuietly(dbPath);
        }
    }

    [TestMethod]
    public void ExecuteMatrixCostAdjustmentCommand_RowsetCambioEnLaBase_DevuelveConflictoSinEscribir()
    {
        var dbPath = TestDbFactory.CreateTempDbPath();
        try
        {
            int proyectoId;
            int matrizId;
            int hojaId;

            using (var context = TestDbFactory.CreateContextAt(dbPath))
            {
                var scenario = CreateSimpleBudget(context, utilidad: 0m);
                proyectoId = scenario.ProjectId;
                matrizId = scenario.MatrixId;
                hojaId = scenario.LeafId;
            }

            var command = CreateCommand(
                dbPath, proyectoId, matrizId, hojaId,
                MatrixAdjustmentScopes.Materiales,
                BudgetMatrixCostAdjustmentTargetKind.PrecioUnitario,
                120m);

            using (var otroModulo = new SOPROContext(dbPath))
            {
                otroModulo.ConceptosPresupuesto.Single(c => c.Id == hojaId).ImporteTotal = 999m;
                otroModulo.SaveChanges();
            }

            var result = BudgetPersistenceService.ExecuteMatrixCostAdjustmentCommand(
                new ProjectDbContextFactory(), dbPath, command);

            Assert.AreEqual(BudgetOperationStatus.Conflict, result.Status, result.Message);
            Assert.IsNull(result.Confirmed);

            using var verificacion = new SOPROContext(dbPath);
            var matriz = verificacion.Matrices.AsNoTracking().Single(m => m.Id == matrizId);
            Assert.AreEqual(100m, matriz.CostoDirecto);
        }
        finally
        {
            DeleteQuietly(dbPath);
        }
    }

    [TestMethod]
    public void ExecuteMatrixCostAdjustmentCommand_MatrizCambioEnLaBase_DevuelveConflictoSinEscribir()
    {
        var dbPath = TestDbFactory.CreateTempDbPath();
        try
        {
            int proyectoId;
            int matrizId;
            int hojaId;

            using (var context = TestDbFactory.CreateContextAt(dbPath))
            {
                var scenario = CreateSimpleBudget(context, utilidad: 0m);
                proyectoId = scenario.ProjectId;
                matrizId = scenario.MatrixId;
                hojaId = scenario.LeafId;
            }

            var command = CreateCommand(
                dbPath, proyectoId, matrizId, hojaId,
                MatrixAdjustmentScopes.Materiales,
                BudgetMatrixCostAdjustmentTargetKind.PrecioUnitario,
                120m);

            using (var otroModulo = new SOPROContext(dbPath))
            {
                otroModulo.Matrices.Single(m => m.Id == matrizId).CostoDirecto = 999m;
                otroModulo.SaveChanges();
            }

            var result = BudgetPersistenceService.ExecuteMatrixCostAdjustmentCommand(
                new ProjectDbContextFactory(), dbPath, command);

            Assert.AreEqual(BudgetOperationStatus.Conflict, result.Status, result.Message);
            Assert.IsNull(result.Confirmed);

            using var verificacion = new SOPROContext(dbPath);
            var componente = verificacion.ComponentesMatriz.AsNoTracking()
                .Single(c => c.MatrizId == matrizId);
            Assert.AreEqual(10m, componente.Cantidad);
        }
        finally
        {
            DeleteQuietly(dbPath);
        }
    }

    [TestMethod]
    public void ExecuteMatrixCostAdjustmentCommand_ConceptoHojaCambioEnLaBase_DevuelveConflictoSinEscribir()
    {
        var dbPath = TestDbFactory.CreateTempDbPath();
        try
        {
            int proyectoId;
            int matrizId;
            int hojaId;

            using (var context = TestDbFactory.CreateContextAt(dbPath))
            {
                var scenario = CreateSimpleBudget(context, utilidad: 0m);
                proyectoId = scenario.ProjectId;
                matrizId = scenario.MatrixId;
                hojaId = scenario.LeafId;
            }

            var command = CreateCommand(
                dbPath, proyectoId, matrizId, hojaId,
                MatrixAdjustmentScopes.Materiales,
                BudgetMatrixCostAdjustmentTargetKind.PrecioUnitario,
                120m);

            using (var otroModulo = new SOPROContext(dbPath))
            {
                otroModulo.ConceptosPresupuesto.Single(c => c.Id == hojaId).Cantidad = 9m;
                otroModulo.SaveChanges();
            }

            var result = BudgetPersistenceService.ExecuteMatrixCostAdjustmentCommand(
                new ProjectDbContextFactory(), dbPath, command);

            Assert.AreEqual(BudgetOperationStatus.Conflict, result.Status, result.Message);
            Assert.IsNull(result.Confirmed);

            using var verificacion = new SOPROContext(dbPath);
            var matriz = verificacion.Matrices.AsNoTracking().Single(m => m.Id == matrizId);
            Assert.AreEqual(100m, matriz.CostoDirecto);
        }
        finally
        {
            DeleteQuietly(dbPath);
        }
    }

    [TestMethod]
    public void ExecuteMatrixCostAdjustmentCommand_AuxiliarCambioEnLaBase_DevuelveConflictoSinEscribir()
    {
        var dbPath = TestDbFactory.CreateTempDbPath();
        try
        {
            int proyectoId;
            int matrizApuId;
            int matrizBasicoId;
            int hojaId;

            using (var context = TestDbFactory.CreateContextAt(dbPath))
            {
                var proyecto = AddProject(context, utilidad: 0m);
                proyectoId = proyecto.Id;

                var material = AddMaterial(context, proyectoId, "MAT-AUX", 10m);

                var basico = AddMatrix(context, proyectoId, "BAS-AUX", TipoMatriz.Basico);
                AddComponent(context, basico, TipoComponenteMatriz.Material, orden: 1,
                    cantidad: 2m, material: material);
                RecalculateMatrix(context, proyecto, basico);

                var matrizApu = AddMatrix(context, proyectoId, "APU-AUX", TipoMatriz.APU);
                AddComponent(context, matrizApu, TipoComponenteMatriz.Auxiliar, orden: 1,
                    cantidad: 3m, auxiliar: basico);
                RecalculateMatrix(context, proyecto, matrizApu);

                var hoja = AddLeaf(context, proyectoId, "C-AUX", matrizApu.Id, 1m, "pza", 1,
                    proyecto: proyecto);
                context.SaveChanges();

                matrizApuId = matrizApu.Id;
                matrizBasicoId = basico.Id;
                hojaId = hoja.Id;
            }

            var command = CreateCommand(
                dbPath, proyectoId, matrizApuId, hojaId,
                MatrixAdjustmentScopes.Materiales,
                BudgetMatrixCostAdjustmentTargetKind.PrecioUnitario,
                90m);

            using (var otroModulo = new SOPROContext(dbPath))
            {
                otroModulo.Matrices.Single(m => m.Id == matrizBasicoId).CostoDirecto = 25m;
                otroModulo.SaveChanges();
            }

            var result = BudgetPersistenceService.ExecuteMatrixCostAdjustmentCommand(
                new ProjectDbContextFactory(), dbPath, command);

            Assert.AreEqual(BudgetOperationStatus.Conflict, result.Status, result.Message);
            Assert.IsNull(result.Confirmed);

            using var verificacion = new SOPROContext(dbPath);
            var apu = verificacion.Matrices.AsNoTracking().Single(m => m.Id == matrizApuId);
            Assert.AreEqual(60m, apu.CostoDirecto);
        }
        finally
        {
            DeleteQuietly(dbPath);
        }
    }

    [TestMethod]
    public void ExecuteMatrixCostAdjustmentCommand_FalloDeTrigger_RevierteYElMismoComandoReintenta()
    {
        var dbPath = TestDbFactory.CreateTempDbPath();
        try
        {
            int proyectoId;
            int matrizId;
            int hojaId;

            using (var context = TestDbFactory.CreateContextAt(dbPath))
            {
                var scenario = CreateSimpleBudget(context, utilidad: 0m);
                proyectoId = scenario.ProjectId;
                matrizId = scenario.MatrixId;
                hojaId = scenario.LeafId;
            }

            var command = CreateCommand(
                dbPath, proyectoId, matrizId, hojaId,
                MatrixAdjustmentScopes.Materiales,
                BudgetMatrixCostAdjustmentTargetKind.PrecioUnitario,
                120m);

            using (var admin = new SOPROContext(dbPath))
            {
                admin.Database.ExecuteSqlRaw(
                    "CREATE TRIGGER trg_fail_componente BEFORE UPDATE ON ComponentesMatriz " +
                    "BEGIN SELECT RAISE(ABORT, 'fallo_simulado'); END;");
            }

            var primerIntento = BudgetPersistenceService.ExecuteMatrixCostAdjustmentCommand(
                new ProjectDbContextFactory(), dbPath, command);

            Assert.AreEqual(BudgetOperationStatus.Failed, primerIntento.Status, primerIntento.Message);
            Assert.IsNull(primerIntento.Confirmed);
            StringAssert.Contains(primerIntento.Diagnostic!, "fallo_simulado");

            using (var verificacion = new SOPROContext(dbPath))
            {
                var matriz = verificacion.Matrices.AsNoTracking().Single(m => m.Id == matrizId);
                Assert.AreEqual(100m, matriz.CostoDirecto, "El trigger revierte TODA la escritura.");
            }

            using (var admin = new SOPROContext(dbPath))
            {
                admin.Database.ExecuteSqlRaw("DROP TRIGGER trg_fail_componente;");
            }

            var reintento = BudgetPersistenceService.ExecuteMatrixCostAdjustmentCommand(
                new ProjectDbContextFactory(), dbPath, command);

            Assert.AreEqual(BudgetOperationStatus.Committed, reintento.Status, reintento.Message);

            using var verificacionFinal = new SOPROContext(dbPath);
            var matrizFinal = verificacionFinal.Matrices.AsNoTracking().Single(m => m.Id == matrizId);
            Assert.AreEqual(120m, matrizFinal.CostoDirecto);

            var hojaFinal = verificacionFinal.ConceptosPresupuesto.AsNoTracking().Single(c => c.Id == hojaId);
            Assert.AreEqual(600m, hojaFinal.ImporteTotal);
        }
        finally
        {
            DeleteQuietly(dbPath);
        }
    }

    [TestMethod]
    public void ExecuteMatrixCostAdjustmentCommand_Exito_UsaExactamenteUnSaveChanges()
    {
        var dbPath = TestDbFactory.CreateTempDbPath();
        try
        {
            int proyectoId;
            int matrizId;
            int hojaId;

            using (var context = TestDbFactory.CreateContextAt(dbPath))
            {
                var scenario = CreateSimpleBudget(context, utilidad: 0m);
                proyectoId = scenario.ProjectId;
                matrizId = scenario.MatrixId;
                hojaId = scenario.LeafId;
            }

            var command = CreateCommand(
                dbPath, proyectoId, matrizId, hojaId,
                MatrixAdjustmentScopes.Materiales,
                BudgetMatrixCostAdjustmentTargetKind.PrecioUnitario,
                120m);

            var factory = new CountingDbContextFactory();

            var result = BudgetPersistenceService.ExecuteMatrixCostAdjustmentCommand(
                factory, dbPath, command);

            Assert.AreEqual(BudgetOperationStatus.Committed, result.Status, result.Message);
            Assert.IsNotNull(factory.Last);
            Assert.AreEqual(1, factory.Last!.SaveChangesCount,
                "El comando debe confirmar con un único SaveChanges.");
        }
        finally
        {
            DeleteQuietly(dbPath);
        }
    }

    [TestMethod]
    public void ExecuteMatrixCostAdjustmentCommand_AncestroConInsumoCambiadoSinPropagar_DevuelveConflictoSinEscribir()
    {
        var dbPath = TestDbFactory.CreateTempDbPath();
        try
        {
            int proyectoId;
            int matrizBasicoId;
            int matrizApuId;
            int hojaBasicoId;
            int hojaApuId;
            int materialApuId;

            using (var context = TestDbFactory.CreateContextAt(dbPath))
            {
                var scenario = CreateNestedBudget(context);
                proyectoId = scenario.ProjectId;
                matrizBasicoId = scenario.BasicMatrixId;
                matrizApuId = scenario.ApuMatrixId;
                hojaBasicoId = scenario.BasicLeafId;
                hojaApuId = scenario.ApuLeafId;
                materialApuId = scenario.AncestorMaterialId;
            }

            var command = CreateCommand(
                dbPath, proyectoId, matrizBasicoId, hojaBasicoId,
                MatrixAdjustmentScopes.Materiales,
                BudgetMatrixCostAdjustmentTargetKind.PrecioUnitario,
                30m);

            // El insumo del ANCESTRO cambia sin propagarse: su matriz almacenada y las
            // hojas siguen mostrando el costo viejo. El cierre lo recalcularía con el
            // precio nuevo e introduciría un cambio que el usuario nunca vio.
            using (var otroModulo = new SOPROContext(dbPath))
            {
                otroModulo.Materiales.Single(m => m.Id == materialApuId).PrecioUnitario = 7m;
                otroModulo.SaveChanges();
            }

            var result = BudgetPersistenceService.ExecuteMatrixCostAdjustmentCommand(
                new ProjectDbContextFactory(), dbPath, command);

            Assert.AreEqual(BudgetOperationStatus.Conflict, result.Status, result.Message);
            Assert.IsNull(result.Confirmed);
            StringAssert.Contains(result.Message!, "cambió en la base",
                "La fuente esperada del ancestro debe revalidarse antes de mutar.");

            using var verificacion = new SOPROContext(dbPath);
            Assert.AreEqual(20m, verificacion.Matrices.AsNoTracking()
                .Single(m => m.Id == matrizBasicoId).CostoDirecto);
            Assert.AreEqual(65m, verificacion.Matrices.AsNoTracking()
                .Single(m => m.Id == matrizApuId).CostoDirecto);
            Assert.AreEqual(65m, verificacion.ConceptosPresupuesto.AsNoTracking()
                .Single(c => c.Id == hojaApuId).ImporteTotal,
                "El conflicto no debe reescribir la hoja del ancestro.");
        }
        finally
        {
            DeleteQuietly(dbPath);
        }
    }

    [TestMethod]
    public void ExecuteMatrixCostAdjustmentCommand_AncestroConComponenteCambiadoSinPropagar_DevuelveConflictoSinEscribir()
    {
        var dbPath = TestDbFactory.CreateTempDbPath();
        try
        {
            int proyectoId;
            int matrizBasicoId;
            int matrizApuId;
            int hojaBasicoId;
            int hojaApuId;

            using (var context = TestDbFactory.CreateContextAt(dbPath))
            {
                var scenario = CreateNestedBudget(context);
                proyectoId = scenario.ProjectId;
                matrizBasicoId = scenario.BasicMatrixId;
                matrizApuId = scenario.ApuMatrixId;
                hojaBasicoId = scenario.BasicLeafId;
                hojaApuId = scenario.ApuLeafId;
            }

            var command = CreateCommand(
                dbPath, proyectoId, matrizBasicoId, hojaBasicoId,
                MatrixAdjustmentScopes.Materiales,
                BudgetMatrixCostAdjustmentTargetKind.PrecioUnitario,
                30m);

            // Un componente del ancestro cambia sin pasar por su recálculo: el
            // importe y el costo almacenados quedan obsoletos. El comando debe
            // rechazar antes de reajustar la matriz seleccionada.
            using (var otroModulo = new SOPROContext(dbPath))
            {
                var componente = otroModulo.ComponentesMatriz
                    .Single(c => c.MatrizId == matrizApuId
                        && c.TipoComponente == TipoComponenteMatriz.Material);
                componente.Cantidad = 2m;
                otroModulo.SaveChanges();
            }

            var result = BudgetPersistenceService.ExecuteMatrixCostAdjustmentCommand(
                new ProjectDbContextFactory(), dbPath, command);

            Assert.AreEqual(BudgetOperationStatus.Conflict, result.Status, result.Message);
            Assert.IsNull(result.Confirmed);
            StringAssert.Contains(result.Message!, "cambiaron",
                "El fingerprint esperado del ancestro debe revalidarse antes de mutar.");

            using var verificacion = new SOPROContext(dbPath);
            Assert.AreEqual(20m, verificacion.Matrices.AsNoTracking()
                .Single(m => m.Id == matrizBasicoId).CostoDirecto);
            Assert.AreEqual(65m, verificacion.Matrices.AsNoTracking()
                .Single(m => m.Id == matrizApuId).CostoDirecto,
                "El costo almacenado del ancestro no se propaga en este comando.");
            Assert.AreEqual(65m, verificacion.ConceptosPresupuesto.AsNoTracking()
                .Single(c => c.Id == hojaApuId).ImporteTotal);
        }
        finally
        {
            DeleteQuietly(dbPath);
        }
    }

    [TestMethod]
    public void ExecuteMatrixCostAdjustmentCommand_AncestroCambiadoCoherenteFueraDeLaVista_DevuelveConflictoSinEscribir()
    {
        var dbPath = TestDbFactory.CreateTempDbPath();
        try
        {
            int proyectoId;
            int matrizBasicoId;
            int matrizApuId;
            int hojaBasicoId;
            int hojaApuId;
            int materialApuId;

            using (var context = TestDbFactory.CreateContextAt(dbPath))
            {
                var scenario = CreateNestedBudget(context);
                proyectoId = scenario.ProjectId;
                matrizBasicoId = scenario.BasicMatrixId;
                matrizApuId = scenario.ApuMatrixId;
                hojaBasicoId = scenario.BasicLeafId;
                hojaApuId = scenario.ApuLeafId;
                materialApuId = scenario.AncestorMaterialId;
            }

            // La base vista se captura ANTES del cambio externo.
            var command = CreateCommand(
                dbPath, proyectoId, matrizBasicoId, hojaBasicoId,
                MatrixAdjustmentScopes.Materiales,
                BudgetMatrixCostAdjustmentTargetKind.PrecioUnitario,
                30m);

            // Cambio COHERENTE fuera de la vista: precio del insumo, importe del
            // componente y costo de la matriz ancestral quedan sincronizados entre sí,
            // pero el usuario nunca vio esos valores. La mera coherencia con lo
            // persistido no basta: debe compararse contra la base esperada.
            using (var otroModulo = new SOPROContext(dbPath))
            {
                otroModulo.Materiales.Single(m => m.Id == materialApuId).PrecioUnitario = 7m;

                var componente = otroModulo.ComponentesMatriz
                    .Single(c => c.MatrizId == matrizApuId
                        && c.TipoComponente == TipoComponenteMatriz.Material);
                componente.Importe = 7m;

                otroModulo.Matrices.Single(m => m.Id == matrizApuId).CostoDirecto = 67m;
                otroModulo.SaveChanges();
            }

            var result = BudgetPersistenceService.ExecuteMatrixCostAdjustmentCommand(
                new ProjectDbContextFactory(), dbPath, command);

            Assert.AreEqual(BudgetOperationStatus.Conflict, result.Status, result.Message);
            Assert.IsNull(result.Confirmed);
            StringAssert.Contains(result.Message!, "del cierre cambió",
                "Un cambio coherente en un ancestro sigue siendo distinto de la base vista.");

            using var verificacion = new SOPROContext(dbPath);
            Assert.AreEqual(20m, verificacion.Matrices.AsNoTracking()
                .Single(m => m.Id == matrizBasicoId).CostoDirecto,
                "La matriz seleccionada no debe reajustarse tras el conflicto.");
            Assert.AreEqual(67m, verificacion.Matrices.AsNoTracking()
                .Single(m => m.Id == matrizApuId).CostoDirecto,
                "El cambio externo coherente permanece; el comando no lo revierte.");
            Assert.AreEqual(65m, verificacion.ConceptosPresupuesto.AsNoTracking()
                .Single(c => c.Id == hojaApuId).ImporteTotal,
                "El conflicto no reescribe leaves con la base que el usuario no vio.");
        }
        finally
        {
            DeleteQuietly(dbPath);
        }
    }

    [TestMethod]
    public void ExecuteMatrixCostAdjustmentCommand_AuxiliarCambiaDeTipoEnLaBase_DevuelveConflictoSinEscribir()
    {
        var dbPath = TestDbFactory.CreateTempDbPath();
        try
        {
            int proyectoId;
            int matrizBasicoId;
            int matrizApuId;
            int hojaApuId;

            using (var context = TestDbFactory.CreateContextAt(dbPath))
            {
                var scenario = CreateNestedBudget(context);
                proyectoId = scenario.ProjectId;
                matrizBasicoId = scenario.BasicMatrixId;
                matrizApuId = scenario.ApuMatrixId;
                hojaApuId = scenario.ApuLeafId;
            }

            var command = CreateCommand(
                dbPath, proyectoId, matrizApuId, hojaApuId,
                MatrixAdjustmentScopes.Materiales,
                BudgetMatrixCostAdjustmentTargetKind.PrecioUnitario,
                100m);

            // El costo del auxiliar no cambia, pero APU/Básico -> Cuadrilla altera su
            // elegibilidad de ajuste por mano de obra: la base esperada ya no vale.
            using (var otroModulo = new SOPROContext(dbPath))
            {
                otroModulo.Matrices.Single(m => m.Id == matrizBasicoId).Tipo = TipoMatriz.Cuadrilla;
                otroModulo.SaveChanges();
            }

            var result = BudgetPersistenceService.ExecuteMatrixCostAdjustmentCommand(
                new ProjectDbContextFactory(), dbPath, command);

            Assert.AreEqual(BudgetOperationStatus.Conflict, result.Status, result.Message);
            Assert.IsNull(result.Confirmed);
            StringAssert.Contains(result.Message!, "cambió en la base",
                "El tipo de matriz auxiliar pertenece a la base de la fuente revalidada.");

            using var verificacion = new SOPROContext(dbPath);
            Assert.AreEqual(65m, verificacion.Matrices.AsNoTracking()
                .Single(m => m.Id == matrizApuId).CostoDirecto);
            Assert.AreEqual(65m, verificacion.ConceptosPresupuesto.AsNoTracking()
                .Single(c => c.Id == hojaApuId).ImporteTotal);
        }
        finally
        {
            DeleteQuietly(dbPath);
        }
    }

    [TestMethod]
    public void ExecuteMatrixCostAdjustmentCommand_BasesYSnapshot_SonColeccionesInmutables()
    {
        var dbPath = TestDbFactory.CreateTempDbPath();
        try
        {
            int proyectoId;
            int matrizId;
            int hojaId;

            using (var context = TestDbFactory.CreateContextAt(dbPath))
            {
                var scenario = CreateSimpleBudget(context, utilidad: 0m);
                proyectoId = scenario.ProjectId;
                matrizId = scenario.MatrixId;
                hojaId = scenario.LeafId;
                AddAggregator(context, proyectoId);
                context.SaveChanges();
            }

            var command = CreateCommand(
                dbPath, proyectoId, matrizId, hojaId,
                MatrixAdjustmentScopes.Materiales,
                BudgetMatrixCostAdjustmentTargetKind.PrecioUnitario,
                120m);

            AssertImmutableCollection(command.ExpectedSources, command.ExpectedSources[0]);
            AssertImmutableCollection(command.ExpectedClosure.Entries, command.ExpectedClosure.Entries[0]);
            AssertImmutableCollection(
                command.ExpectedClosure.Entries[0].Sources,
                command.ExpectedClosure.Entries[0].Sources[0]);

            var result = BudgetPersistenceService.ExecuteMatrixCostAdjustmentCommand(
                new ProjectDbContextFactory(), dbPath, command);

            Assert.AreEqual(BudgetOperationStatus.Committed, result.Status, result.Message);
            var snapshot = result.Confirmed!;

            AssertImmutableCollection(snapshot.AffectedMatrices, snapshot.Matrix);
            AssertImmutableCollection(snapshot.AffectedComponents, snapshot.AffectedComponents[0]);
            AssertImmutableCollection(snapshot.Leaves, snapshot.Leaves[0]);
            AssertImmutableCollection(snapshot.Aggregators, snapshot.Aggregators[0]);
        }
        finally
        {
            DeleteQuietly(dbPath);
        }
    }

    private static void AssertImmutableCollection<T>(IReadOnlyList<T> items, T sample)
    {
        Assert.IsFalse(items is T[], "La colección no debe exponer el array subyacente.");
        var list = items as IList<T>;
        Assert.IsNotNull(list, "La colección debe exponer una lista de solo lectura.");
        Assert.IsTrue(list!.IsReadOnly);
        Assert.ThrowsExactly<NotSupportedException>(() => list.Add(sample));
    }

    // ────────────────────────────────────────────────────────────────────────
    // Infraestructura
    // ────────────────────────────────────────────────────────────────────────

    private sealed class CountingDbContextFactory : IProjectDbContextFactory
    {
        public CountingContext? Last { get; private set; }

        public SOPROContext Create(string databasePath)
        {
            Last = new CountingContext(databasePath);
            return Last;
        }

        public Task<SOPROContext> CreateAsync(
            string databasePath,
            CancellationToken cancellationToken = default)
            => Task.FromResult(Create(databasePath));
    }

    private sealed class CountingContext : SOPROContext
    {
        public CountingContext(string databasePath)
            : base(databasePath)
        {
        }

        public int SaveChangesCount { get; private set; }

        public override int SaveChanges()
        {
            SaveChangesCount++;
            return base.SaveChanges();
        }
    }

    private static BudgetMatrixCostAdjustmentCommand CreateCommand(
        string dbPath,
        int proyectoId,
        int matrizId,
        int hojaId,
        MatrixAdjustmentScopes scopes,
        BudgetMatrixCostAdjustmentTargetKind targetKind,
        decimal targetValue)
    {
        using var context = new SOPROContext(dbPath);

        var proyecto = context.Proyectos.AsNoTracking().Single(p => p.Id == proyectoId);
        var closure = BudgetPersistenceService.CaptureMatrixCostAdjustmentClosure(
            context, proyectoId, matrizId);

        var filas = context.ConceptosPresupuesto
            .AsNoTracking()
            .Where(c => c.ProyectoId == proyectoId)
            .OrderBy(c => c.Orden)
            .ThenBy(c => c.Id)
            .ToList();

        var hoja = filas.Single(c => c.Id == hojaId);

        return new BudgetMatrixCostAdjustmentCommand(
            proyectoId,
            BudgetProjectCalculationBase.FromProyecto(proyecto),
            closure,
            BudgetPriceRefreshRowset.FromConceptos(filas),
            BudgetMatrixCostAdjustmentLeaf.FromConcepto(hoja),
            scopes,
            targetKind,
            targetValue);
    }

    private static SimpleBudgetScenario CreateSimpleBudget(SOPROContext context, decimal utilidad)
    {
        var proyecto = AddProject(context, utilidad);

        var material = AddMaterial(context, proyecto.Id, "MAT-SIMPLE", 10m);

        var matriz = AddMatrix(context, proyecto.Id, "APU-SIMPLE", TipoMatriz.APU);
        var componente = AddComponent(
            context, matriz, TipoComponenteMatriz.Material, orden: 1,
            cantidad: 10m, material: material);
        RecalculateMatrix(context, proyecto, matriz);

        var hoja = AddLeaf(
            context, proyecto.Id, "C-SIMPLE", matriz.Id, 5m, "pza", 1,
            proyecto: proyecto);
        context.SaveChanges();

        return new SimpleBudgetScenario(
            proyecto.Id,
            matriz.Id,
            hoja.Id,
            material.Id,
            componente.Id);
    }

    private static NestedBudgetScenario CreateNestedBudget(SOPROContext context)
    {
        var proyecto = AddProject(context, utilidad: 0m);

        var materialBasico = AddMaterial(context, proyecto.Id, "MAT-CIERRE-BAS", 10m);
        var materialApu = AddMaterial(context, proyecto.Id, "MAT-CIERRE-APU", 5m);

        var basico = AddMatrix(context, proyecto.Id, "BAS-CIERRE", TipoMatriz.Basico);
        AddComponent(context, basico, TipoComponenteMatriz.Material, orden: 1,
            cantidad: 2m, material: materialBasico);
        RecalculateMatrix(context, proyecto, basico);

        var apu = AddMatrix(context, proyecto.Id, "APU-CIERRE", TipoMatriz.APU);
        AddComponent(context, apu, TipoComponenteMatriz.Auxiliar, orden: 1,
            cantidad: 3m, auxiliar: basico);
        AddComponent(context, apu, TipoComponenteMatriz.Material, orden: 2,
            cantidad: 1m, material: materialApu);
        RecalculateMatrix(context, proyecto, apu);

        var hojaBasico = AddLeaf(
            context, proyecto.Id, "C-CIERRE-BAS", basico.Id, 1m, "pza", 1,
            proyecto: proyecto);
        var hojaApu = AddLeaf(
            context, proyecto.Id, "C-CIERRE-APU", apu.Id, 1m, "pza", 2,
            proyecto: proyecto);
        context.SaveChanges();

        return new NestedBudgetScenario(
            proyecto.Id,
            basico.Id,
            apu.Id,
            hojaBasico.Id,
            hojaApu.Id,
            materialApu.Id);
    }

    private static Proyecto AddProject(SOPROContext context, decimal utilidad)
    {
        var proyecto = new Proyecto
        {
            Nombre = "Proyecto prueba reajuste",
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
            PorcentajeUtilidad = utilidad,
            PorcentajeCargosAdicionales = 0m,
            PorcentajeIVA = 0m,
            ModoCalculoPorcentajes = "Acumulables",
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

    private static ManoDeObra AddManoDeObra(
        SOPROContext context,
        int proyectoId,
        string clave,
        decimal salarioReal)
    {
        var manoObra = new ManoDeObra
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

        context.ManoDeObra.Add(manoObra);
        context.SaveChanges();
        return manoObra;
    }

    private static Matriz AddMatrix(
        SOPROContext context,
        int proyectoId,
        string clave,
        TipoMatriz tipo)
    {
        var matriz = new Matriz
        {
            ProyectoId = proyectoId,
            Clave = clave,
            Descripcion = "Matriz de prueba",
            Unidad = "pza",
            Tipo = tipo,
            CostoDirecto = 0m,
            Notas = string.Empty
        };

        context.Matrices.Add(matriz);
        context.SaveChanges();
        return matriz;
    }

    private static ComponenteMatriz AddComponent(
        SOPROContext context,
        Matriz matriz,
        TipoComponenteMatriz tipo,
        int orden,
        decimal cantidad,
        Material? material = null,
        ManoDeObra? manoDeObra = null,
        Matriz? auxiliar = null)
    {
        var componente = new ComponenteMatriz
        {
            MatrizId = matriz.Id,
            TipoComponente = tipo,
            MaterialId = material?.Id,
            Material = material,
            ManoDeObraId = manoDeObra?.Id,
            ManoDeObra = manoDeObra,
            AuxiliarId = auxiliar?.Id,
            Auxiliar = auxiliar,
            Cantidad = cantidad,
            Orden = orden,
            Notas = string.Empty
        };

        context.ComponentesMatriz.Add(componente);
        return componente;
    }

    private static void RecalculateMatrix(SOPROContext context, Proyecto proyecto, Matriz matriz)
    {
        var totales = MatrixComponentCalculationService.Recalculate(
            matriz.Componentes.ToList(), proyecto.DecimalesImporte);
        matriz.CostoDirecto = totales.CostoDirectoTotal;
        context.SaveChanges();
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

    private static ConceptoPresupuesto AddLeaf(
        SOPROContext context,
        int proyectoId,
        string clave,
        int matrizId,
        decimal cantidad,
        string unidad,
        int orden,
        Proyecto? proyecto = null)
    {
        var costoDirecto = proyecto == null ? 100m : MatrixCosto(context, matrizId);
        var precioUnitario = proyecto == null
            ? costoDirecto
            : BudgetPricingService.CalculateUnitPrice(proyecto, costoDirecto);

        var importeTotal = proyecto == null
            ? cantidad * precioUnitario
            : BudgetPricingService.MultiplyUsingDisplayPrecision(proyecto, cantidad, precioUnitario);

        var hoja = new ConceptoPresupuesto
        {
            ProyectoId = proyectoId,
            Clave = clave,
            Descripcion = "Concepto de prueba",
            Unidad = unidad,
            Cantidad = cantidad,
            MatrizId = matrizId,
            CostoDirectoUnitario = costoDirecto,
            CostoDirectoTotal = cantidad * costoDirecto,
            PrecioUnitario = precioUnitario,
            ImporteTotal = importeTotal,
            Nivel = 1,
            Orden = orden,
            EsAgrupador = false,
            ColumnasPersonalizadasJSON = string.Empty,
            Notas = string.Empty
        };

        context.ConceptosPresupuesto.Add(hoja);
        return hoja;
    }

    private static decimal MatrixCosto(SOPROContext context, int matrizId)
        => context.Matrices.AsNoTracking().Single(m => m.Id == matrizId).CostoDirecto;

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

    private sealed record SimpleBudgetScenario(
        int ProjectId,
        int MatrixId,
        int LeafId,
        int MaterialId,
        int MaterialComponentId);

    private sealed record NestedBudgetScenario(
        int ProjectId,
        int BasicMatrixId,
        int ApuMatrixId,
        int BasicLeafId,
        int ApuLeafId,
        int AncestorMaterialId);
}
