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
/// Contrato del reajuste de costo agrupado (ADR-0001): bases capturadas antes del
/// diálogo (proyecto, rowset completo, agrupador, hojas visibles y unión de cierres
/// auxiliares), validación exacta contra la base, hojas sin matriz o no ajustables
/// como costo fijo, búsqueda acotada de un factor único evaluando el importe real
/// recalculado, ajuste de cada matriz una sola vez, cierre topológico completo y un
/// único SaveChanges.
/// </summary>
[TestClass]
public class BudgetGroupCostAdjustmentPersistenceTests
{
    [TestMethod]
    public void ExecuteGroupCostAdjustmentCommand_HojasMixtasYFijas_AjustaSoloRubrosAjustablesYConservaFijas()
    {
        var dbPath = TestDbFactory.CreateTempDbPath();
        try
        {
            int proyectoId;
            int matrizAjustableId;
            int matrizFijaId;
            int grupoId;
            int hojaAjustableId;
            int hojaFijaMatrizId;
            int hojaSinMatrizId;

            using (var context = TestDbFactory.CreateContextAt(dbPath))
            {
                var proyecto = AddProject(context, utilidad: 0m);
                proyectoId = proyecto.Id;

                var materialA = AddMaterial(context, proyectoId, "MAT-AJUSTE", 10m);
                var materialB = AddMaterial(context, proyectoId, "MAT-FIJA", 10m);
                var manoObra = AddManoDeObra(context, proyectoId, "MO-FIJA", 20m);

                var matrizAjustable = AddMatrix(context, proyectoId, "APU-AJUSTE", TipoMatriz.APU);
                AddComponent(context, matrizAjustable, TipoComponenteMatriz.Material, orden: 1,
                    cantidad: 10m, material: materialA);
                RecalculateMatrix(context, proyecto, matrizAjustable);

                var matrizFija = AddMatrix(context, proyectoId, "APU-FIJA", TipoMatriz.APU);
                AddComponent(context, matrizFija, TipoComponenteMatriz.ManoDeObra, orden: 1,
                    cantidad: 5m, manoDeObra: manoObra);
                RecalculateMatrix(context, proyecto, matrizFija);

                var grupo = AddAggregator(context, proyectoId, orden: 0, nivel: 0);
                var hojaAjustable = AddLeaf(context, proyecto, "C-AJUSTE", matrizAjustable.Id, 1m, orden: 1);
                var hojaFijaMatriz = AddLeaf(context, proyecto, "C-FIJA-MATRIZ", matrizFija.Id, 1m, orden: 2);
                var hojaSinMatriz = AddLeaf(context, proyecto, "C-SIN-MATRIZ", null, 2m, orden: 3,
                    costoDirectoUnitario: 50m);

                context.SaveChanges();
                RefreshAggregators(context, proyecto);

                matrizAjustableId = matrizAjustable.Id;
                matrizFijaId = matrizFija.Id;
                grupoId = grupo.Id;
                hojaAjustableId = hojaAjustable.Id;
                hojaFijaMatrizId = hojaFijaMatriz.Id;
                hojaSinMatrizId = hojaSinMatriz.Id;
            }

            var command = CreateCommand(
                dbPath, proyectoId, grupoId, MatrixAdjustmentScopes.Materiales, 250m);

            var result = BudgetPersistenceService.ExecuteGroupCostAdjustmentCommand(
                new ProjectDbContextFactory(), dbPath, command);

            Assert.AreEqual(BudgetOperationStatus.Committed, result.Status, result.Message);
            Assert.IsNull(result.Diagnostic);
            var snapshot = result.Confirmed!;

            Assert.AreEqual(300m, snapshot.CurrentAmount);
            Assert.AreEqual(250m, snapshot.AchievedAmount);
            Assert.AreEqual(200m, snapshot.MinimumAmount);
            Assert.IsTrue(Math.Abs(snapshot.FactorApplied - 0.5m) < 0.001m,
                $"El factor debe ser 0.5; fue {snapshot.FactorApplied}.");
            Assert.AreEqual(1, snapshot.AdjustableLeafCount);
            Assert.AreEqual(2, snapshot.FixedLeafCount);
            CollectionAssert.AreEqual(new[] { matrizAjustableId }, snapshot.AdjustedMatrixIds.ToArray());
            CollectionAssert.AreEquivalent(
                new[] { hojaAjustableId, hojaFijaMatrizId },
                snapshot.AffectedLeaves.Select(h => h.ConceptId).ToArray());
            Assert.AreEqual(2, snapshot.AffectedMatrices.Count);
            Assert.AreEqual(1, snapshot.Aggregators.Count);

            using var verificacion = new SOPROContext(dbPath);
            var matrizAjustableBd = verificacion.Matrices.AsNoTracking()
                .Include(m => m.Componentes)
                .Single(m => m.Id == matrizAjustableId);
            Assert.AreEqual(50m, matrizAjustableBd.CostoDirecto);
            Assert.AreEqual(5m, matrizAjustableBd.Componentes.Single().Cantidad);

            var matrizFijaBd = verificacion.Matrices.AsNoTracking()
                .Include(m => m.Componentes)
                .Single(m => m.Id == matrizFijaId);
            Assert.AreEqual(100m, matrizFijaBd.CostoDirecto);
            Assert.AreEqual(5m, matrizFijaBd.Componentes.Single().Cantidad,
                "La matriz sin rubros ajustables debe conservar sus cantidades.");

            var hojaAjustableBd = verificacion.ConceptosPresupuesto.AsNoTracking()
                .Single(c => c.Id == hojaAjustableId);
            Assert.AreEqual(50m, hojaAjustableBd.ImporteTotal);

            var hojaFijaBd = verificacion.ConceptosPresupuesto.AsNoTracking()
                .Single(c => c.Id == hojaFijaMatrizId);
            Assert.AreEqual(100m, hojaFijaBd.ImporteTotal,
                "La hoja cuya matriz no tiene rubros ajustables es costo fijo.");

            var hojaSinMatrizBd = verificacion.ConceptosPresupuesto.AsNoTracking()
                .Single(c => c.Id == hojaSinMatrizId);
            Assert.AreEqual(100m, hojaSinMatrizBd.ImporteTotal,
                "La hoja sin matriz es costo fijo y no se reescribe.");
            Assert.AreEqual(2m, hojaSinMatrizBd.Cantidad);

            var grupoBd = verificacion.ConceptosPresupuesto.AsNoTracking().Single(c => c.Id == grupoId);
            Assert.AreEqual(250m, grupoBd.ImporteTotal);
            Assert.AreEqual(250m, grupoBd.CostoDirectoTotal);
        }
        finally
        {
            DeleteQuietly(dbPath);
        }
    }

    [TestMethod]
    public void ExecuteGroupCostAdjustmentCommand_ObjetivoConCentavos_RedondeaConPrecisionDePantalla()
    {
        var dbPath = TestDbFactory.CreateTempDbPath();
        try
        {
            int proyectoId;
            int matrizId;
            int grupoId;
            int hojaId;

            using (var context = TestDbFactory.CreateContextAt(dbPath))
            {
                var proyecto = AddProject(context, utilidad: 0m);
                proyectoId = proyecto.Id;

                var material = AddMaterial(context, proyectoId, "MAT-REDONDEO", 1m);
                var matriz = AddMatrix(context, proyectoId, "APU-REDONDEO", TipoMatriz.APU);
                AddComponent(context, matriz, TipoComponenteMatriz.Material, orden: 1,
                    cantidad: 100m, material: material);
                RecalculateMatrix(context, proyecto, matriz);

                var grupo = AddAggregator(context, proyectoId, orden: 0, nivel: 0);
                var hoja = AddLeaf(context, proyecto, "C-REDONDEO", matriz.Id, 1m, orden: 1);

                context.SaveChanges();
                RefreshAggregators(context, proyecto);

                matrizId = matriz.Id;
                grupoId = grupo.Id;
                hojaId = hoja.Id;
            }

            var command = CreateCommand(
                dbPath, proyectoId, grupoId, MatrixAdjustmentScopes.Materiales, 133.33m);

            var result = BudgetPersistenceService.ExecuteGroupCostAdjustmentCommand(
                new ProjectDbContextFactory(), dbPath, command);

            Assert.AreEqual(BudgetOperationStatus.Committed, result.Status, result.Message);
            var snapshot = result.Confirmed!;

            Assert.AreEqual(133.33m, snapshot.AchievedAmount,
                "El importe debe resolverse con la precisión de pantalla del proyecto.");
            Assert.AreEqual(snapshot.AchievedAmount, decimal.Round(snapshot.AchievedAmount, 2),
                "El importe no debe exponer más decimales que la precisión de pantalla.");
            Assert.IsTrue(Math.Abs(snapshot.FactorApplied - 1.3333m) < 0.001m,
                $"El factor debe aproximarse a 1.3333; fue {snapshot.FactorApplied}.");

            using var verificacion = new SOPROContext(dbPath);
            var matrizBd = verificacion.Matrices.AsNoTracking()
                .Include(m => m.Componentes)
                .Single(m => m.Id == matrizId);
            Assert.AreEqual(133.33m, matrizBd.CostoDirecto);
            Assert.AreEqual(133.33m, matrizBd.Componentes.Single().Cantidad);

            var hojaBd = verificacion.ConceptosPresupuesto.AsNoTracking().Single(c => c.Id == hojaId);
            Assert.AreEqual(133.33m, hojaBd.ImporteTotal);

            var grupoBd = verificacion.ConceptosPresupuesto.AsNoTracking().Single(c => c.Id == grupoId);
            Assert.AreEqual(133.33m, grupoBd.ImporteTotal);
        }
        finally
        {
            DeleteQuietly(dbPath);
        }
    }

    [TestMethod]
    public void ExecuteGroupCostAdjustmentCommand_MatricesRelacionadasPorAuxiliar_AjustaCadaUnaUnaVezYActualizaHojasFueraDelGrupo()
    {
        var dbPath = TestDbFactory.CreateTempDbPath();
        try
        {
            int proyectoId;
            int matrizBasicoId;
            int matrizApuId;
            int grupoId;
            int grupoExternoId;
            int hojaBasicoId;
            int hojaApuId;
            int hojaExternaId;

            using (var context = TestDbFactory.CreateContextAt(dbPath))
            {
                var proyecto = AddProject(context, utilidad: 0m);
                proyectoId = proyecto.Id;

                var materialBasico = AddMaterial(context, proyectoId, "MAT-BASICO", 10m);
                var materialApu = AddMaterial(context, proyectoId, "MAT-APU", 5m);

                var basico = AddMatrix(context, proyectoId, "BAS-REL", TipoMatriz.Basico);
                AddComponent(context, basico, TipoComponenteMatriz.Material, orden: 1,
                    cantidad: 2m, material: materialBasico);
                RecalculateMatrix(context, proyecto, basico);

                var apu = AddMatrix(context, proyectoId, "APU-REL", TipoMatriz.APU);
                AddComponent(context, apu, TipoComponenteMatriz.Auxiliar, orden: 1,
                    cantidad: 3m, auxiliar: basico);
                AddComponent(context, apu, TipoComponenteMatriz.Material, orden: 2,
                    cantidad: 4m, material: materialApu);
                RecalculateMatrix(context, proyecto, apu);

                var grupo = AddAggregator(context, proyectoId, orden: 0, nivel: 0);
                var hojaBasico = AddLeaf(context, proyecto, "C-BAS-REL", basico.Id, 1m, orden: 1);
                var hojaApu = AddLeaf(context, proyecto, "C-APU-REL", apu.Id, 1m, orden: 2);

                var grupoExterno = AddAggregator(context, proyectoId, orden: 3, nivel: 0);
                var hojaExterna = AddLeaf(context, proyecto, "C-EXTERNA", apu.Id, 2m, orden: 4);

                context.SaveChanges();
                RefreshAggregators(context, proyecto);

                matrizBasicoId = basico.Id;
                matrizApuId = apu.Id;
                grupoId = grupo.Id;
                grupoExternoId = grupoExterno.Id;
                hojaBasicoId = hojaBasico.Id;
                hojaApuId = hojaApu.Id;
                hojaExternaId = hojaExterna.Id;
            }

            var bases = BudgetPersistenceService.LoadGroupCostAdjustmentBases(
                new ProjectDbContextFactory(), dbPath, proyectoId, grupoId);

            CollectionAssert.AreEquivalent(
                new[] { matrizBasicoId, matrizApuId },
                bases.SelectedMatrixIds.ToArray());
            CollectionAssert.AreEquivalent(
                new[] { matrizBasicoId, matrizApuId },
                bases.Closure.Entries.Select(e => e.Matrix.MatrixId).ToArray(),
                "La captura debe unir los cierres de las matrices distintas del grupo.");
            CollectionAssert.AreEquivalent(
                new[] { hojaBasicoId, hojaApuId },
                bases.VisibleLeaves.Select(h => h.ConceptId).ToArray());

            var command = new BudgetGroupCostAdjustmentCommand(
                bases, MatrixAdjustmentScopes.Materiales, 150m);

            var result = BudgetPersistenceService.ExecuteGroupCostAdjustmentCommand(
                new ProjectDbContextFactory(), dbPath, command);

            Assert.AreEqual(BudgetOperationStatus.Committed, result.Status, result.Message);
            var snapshot = result.Confirmed!;

            Assert.AreEqual(100m, snapshot.CurrentAmount);
            Assert.AreEqual(150m, snapshot.AchievedAmount);
            Assert.AreEqual(150m, snapshot.Group.ImporteTotal);
            Assert.AreEqual(2, snapshot.AdjustedMatrixIds.Count);
            Assert.AreEqual(3, snapshot.AffectedLeaves.Count,
                "La hoja externa que comparte la matriz también debe viajar en la instantánea.");
            Assert.AreEqual(2, snapshot.Aggregators.Count);

            using var verificacion = new SOPROContext(dbPath);
            var basicoBd = verificacion.Matrices.AsNoTracking()
                .Include(m => m.Componentes)
                .Single(m => m.Id == matrizBasicoId);
            Assert.AreEqual(30m, basicoBd.CostoDirecto);
            Assert.AreEqual(3m, basicoBd.Componentes.Single().Cantidad);

            var apuBd = verificacion.Matrices.AsNoTracking()
                .Include(m => m.Componentes)
                .Single(m => m.Id == matrizApuId);
            Assert.AreEqual(120m, apuBd.CostoDirecto,
                "Cada matriz se ajusta UNA sola vez: el auxiliar aporta 90 y el material 30.");
            Assert.AreEqual(3m, apuBd.Componentes.Single(c => c.TipoComponente == TipoComponenteMatriz.Auxiliar).Cantidad,
                "El auxiliar no es un rubro ajustable para Materiales.");
            Assert.AreEqual(6m, apuBd.Componentes.Single(c => c.TipoComponente == TipoComponenteMatriz.Material).Cantidad);

            var hojaBasicoBd = verificacion.ConceptosPresupuesto.AsNoTracking().Single(c => c.Id == hojaBasicoId);
            var hojaApuBd = verificacion.ConceptosPresupuesto.AsNoTracking().Single(c => c.Id == hojaApuId);
            var hojaExternaBd = verificacion.ConceptosPresupuesto.AsNoTracking().Single(c => c.Id == hojaExternaId);
            Assert.AreEqual(30m, hojaBasicoBd.ImporteTotal);
            Assert.AreEqual(120m, hojaApuBd.ImporteTotal);
            Assert.AreEqual(240m, hojaExternaBd.ImporteTotal,
                "La hoja fuera del grupo se recalcula por compartir la matriz ajustada.");

            var grupoExternoBd = verificacion.ConceptosPresupuesto.AsNoTracking().Single(c => c.Id == grupoExternoId);
            Assert.AreEqual(240m, grupoExternoBd.ImporteTotal);
        }
        finally
        {
            DeleteQuietly(dbPath);
        }
    }

    [TestMethod]
    public void ExecuteGroupCostAdjustmentCommand_AgrupadorNoCoincideConSusHojas_DevuelveConflictoSinEscribir()
    {
        var dbPath = TestDbFactory.CreateTempDbPath();
        try
        {
            int proyectoId;
            int matrizId;
            int grupoId;
            int hojaId;

            using (var context = TestDbFactory.CreateContextAt(dbPath))
            {
                var proyecto = AddProject(context, utilidad: 0m);
                proyectoId = proyecto.Id;

                var material = AddMaterial(context, proyectoId, "MAT-COHERENCIA", 10m);
                var matriz = AddMatrix(context, proyectoId, "APU-COHERENCIA", TipoMatriz.APU);
                AddComponent(context, matriz, TipoComponenteMatriz.Material, orden: 1,
                    cantidad: 10m, material: material);
                RecalculateMatrix(context, proyecto, matriz);

                var grupo = AddAggregator(context, proyectoId, orden: 0, nivel: 0);
                var hoja = AddLeaf(context, proyecto, "C-COHERENCIA", matriz.Id, 1m, orden: 1);

                context.SaveChanges();
                RefreshAggregators(context, proyecto);

                matrizId = matriz.Id;
                grupoId = grupo.Id;
                hojaId = hoja.Id;

                // El agrupador persistido deja de representar la suma de sus hojas.
                var grupoBd = context.ConceptosPresupuesto.Single(c => c.Id == grupoId);
                grupoBd.ImporteTotal = 999m;
                grupoBd.CostoDirectoTotal = 999m;
                context.SaveChanges();
            }

            var command = CreateCommand(
                dbPath, proyectoId, grupoId, MatrixAdjustmentScopes.Materiales, 120m);

            var factory = new CountingDbContextFactory();
            var result = BudgetPersistenceService.ExecuteGroupCostAdjustmentCommand(
                factory, dbPath, command);

            Assert.AreEqual(BudgetOperationStatus.Conflict, result.Status, result.Message);
            Assert.IsNull(result.Confirmed);
            StringAssert.Contains(result.Message!, "suma de sus hojas visibles");
            Assert.AreEqual(0, factory.Last!.SaveChangesCount,
                "Un conflicto se rechaza antes de cualquier escritura.");

            using var verificacion = new SOPROContext(dbPath);
            Assert.AreEqual(100m, verificacion.Matrices.AsNoTracking()
                .Single(m => m.Id == matrizId).CostoDirecto);
            Assert.AreEqual(100m, verificacion.ConceptosPresupuesto.AsNoTracking()
                .Single(c => c.Id == hojaId).ImporteTotal);
            Assert.AreEqual(999m, verificacion.ConceptosPresupuesto.AsNoTracking()
                .Single(c => c.Id == grupoId).ImporteTotal,
                "El conflicto no debe reparar ni reescribir la base.");
        }
        finally
        {
            DeleteQuietly(dbPath);
        }
    }

    [TestMethod]
    public void ExecuteGroupCostAdjustmentCommand_HojaNoReflejaElCostoVigenteDeSuMatriz_DevuelveConflictoSinEscribir()
    {
        var dbPath = TestDbFactory.CreateTempDbPath();
        try
        {
            int proyectoId;
            int matrizId;
            int grupoId;
            int hojaId;

            using (var context = TestDbFactory.CreateContextAt(dbPath))
            {
                var proyecto = AddProject(context, utilidad: 0m);
                proyectoId = proyecto.Id;

                var material = AddMaterial(context, proyectoId, "MAT-DESFASE", 10m);
                var matriz = AddMatrix(context, proyectoId, "APU-DESFASE", TipoMatriz.APU);
                AddComponent(context, matriz, TipoComponenteMatriz.Material, orden: 1,
                    cantidad: 10m, material: material);
                RecalculateMatrix(context, proyecto, matriz);

                var grupo = AddAggregator(context, proyectoId, orden: 0, nivel: 0);
                var hoja = AddLeaf(context, proyecto, "C-DESFASE", matriz.Id, 1m, orden: 1);
                context.SaveChanges();

                // La hoja quedó con un costo viejo respecto de su matriz vigente.
                var hojaBd = context.ConceptosPresupuesto.Single(c => c.Id == hoja.Id);
                hojaBd.CostoDirectoUnitario = 90m;
                hojaBd.CostoDirectoTotal = 90m;
                hojaBd.PrecioUnitario = 90m;
                hojaBd.ImporteTotal = 90m;
                context.SaveChanges();
                RefreshAggregators(context, proyecto);

                matrizId = matriz.Id;
                grupoId = grupo.Id;
                hojaId = hoja.Id;
            }

            var command = CreateCommand(
                dbPath, proyectoId, grupoId, MatrixAdjustmentScopes.Materiales, 110m);

            var result = BudgetPersistenceService.ExecuteGroupCostAdjustmentCommand(
                new ProjectDbContextFactory(), dbPath, command);

            Assert.AreEqual(BudgetOperationStatus.Conflict, result.Status, result.Message);
            Assert.IsNull(result.Confirmed);
            StringAssert.Contains(result.Message!, "no refleja el costo directo vigente");

            using var verificacion = new SOPROContext(dbPath);
            Assert.AreEqual(100m, verificacion.Matrices.AsNoTracking()
                .Single(m => m.Id == matrizId).CostoDirecto);
            Assert.AreEqual(90m, verificacion.ConceptosPresupuesto.AsNoTracking()
                .Single(c => c.Id == hojaId).ImporteTotal,
                "El conflicto no debe alinear la hoja con la matriz.");
            Assert.AreEqual(90m, verificacion.ConceptosPresupuesto.AsNoTracking()
                .Single(c => c.Id == grupoId).ImporteTotal);
        }
        finally
        {
            DeleteQuietly(dbPath);
        }
    }

    [TestMethod]
    public void ExecuteGroupCostAdjustmentCommand_FalloDeTrigger_RevierteYElMismoComandoReintenta()
    {
        var dbPath = TestDbFactory.CreateTempDbPath();
        try
        {
            int proyectoId;
            int matrizId;
            int grupoId;
            int hojaId;

            using (var context = TestDbFactory.CreateContextAt(dbPath))
            {
                var proyecto = AddProject(context, utilidad: 0m);
                proyectoId = proyecto.Id;

                var material = AddMaterial(context, proyectoId, "MAT-TRIGGER", 10m);
                var matriz = AddMatrix(context, proyectoId, "APU-TRIGGER", TipoMatriz.APU);
                AddComponent(context, matriz, TipoComponenteMatriz.Material, orden: 1,
                    cantidad: 10m, material: material);
                RecalculateMatrix(context, proyecto, matriz);

                var grupo = AddAggregator(context, proyectoId, orden: 0, nivel: 0);
                var hoja = AddLeaf(context, proyecto, "C-TRIGGER", matriz.Id, 1m, orden: 1);
                context.SaveChanges();
                RefreshAggregators(context, proyecto);

                matrizId = matriz.Id;
                grupoId = grupo.Id;
                hojaId = hoja.Id;
            }

            var command = CreateCommand(
                dbPath, proyectoId, grupoId, MatrixAdjustmentScopes.Materiales, 120m);

            using (var admin = new SOPROContext(dbPath))
            {
                admin.Database.ExecuteSqlRaw(
                    "CREATE TRIGGER trg_fail_grupo_componente BEFORE UPDATE ON ComponentesMatriz " +
                    "BEGIN SELECT RAISE(ABORT, 'fallo_simulado_grupo'); END;");
            }

            var primerIntento = BudgetPersistenceService.ExecuteGroupCostAdjustmentCommand(
                new ProjectDbContextFactory(), dbPath, command);

            Assert.AreEqual(BudgetOperationStatus.Failed, primerIntento.Status, primerIntento.Message);
            Assert.IsNull(primerIntento.Confirmed);
            StringAssert.Contains(primerIntento.Diagnostic!, "fallo_simulado_grupo");

            using (var verificacion = new SOPROContext(dbPath))
            {
                Assert.AreEqual(100m, verificacion.Matrices.AsNoTracking()
                    .Single(m => m.Id == matrizId).CostoDirecto,
                    "El trigger revierte TODA la escritura.");
                Assert.AreEqual(100m, verificacion.ConceptosPresupuesto.AsNoTracking()
                    .Single(c => c.Id == hojaId).ImporteTotal);
                Assert.AreEqual(100m, verificacion.ConceptosPresupuesto.AsNoTracking()
                    .Single(c => c.Id == grupoId).ImporteTotal);
            }

            using (var admin = new SOPROContext(dbPath))
            {
                admin.Database.ExecuteSqlRaw("DROP TRIGGER trg_fail_grupo_componente;");
            }

            var reintento = BudgetPersistenceService.ExecuteGroupCostAdjustmentCommand(
                new ProjectDbContextFactory(), dbPath, command);

            Assert.AreEqual(BudgetOperationStatus.Committed, reintento.Status, reintento.Message);

            using var verificacionFinal = new SOPROContext(dbPath);
            Assert.AreEqual(120m, verificacionFinal.Matrices.AsNoTracking()
                .Single(m => m.Id == matrizId).CostoDirecto);
            Assert.AreEqual(120m, verificacionFinal.ConceptosPresupuesto.AsNoTracking()
                .Single(c => c.Id == hojaId).ImporteTotal);
            Assert.AreEqual(120m, verificacionFinal.ConceptosPresupuesto.AsNoTracking()
                .Single(c => c.Id == grupoId).ImporteTotal);
        }
        finally
        {
            DeleteQuietly(dbPath);
        }
    }

    [TestMethod]
    public void ExecuteGroupCostAdjustmentCommand_Exito_UsaExactamenteUnSaveChanges()
    {
        var dbPath = TestDbFactory.CreateTempDbPath();
        try
        {
            int proyectoId;
            int grupoId;

            using (var context = TestDbFactory.CreateContextAt(dbPath))
            {
                var proyecto = AddProject(context, utilidad: 0m);
                proyectoId = proyecto.Id;

                var material = AddMaterial(context, proyectoId, "MAT-SAVECOUNT", 10m);
                var matriz = AddMatrix(context, proyectoId, "APU-SAVECOUNT", TipoMatriz.APU);
                AddComponent(context, matriz, TipoComponenteMatriz.Material, orden: 1,
                    cantidad: 10m, material: material);
                RecalculateMatrix(context, proyecto, matriz);

                var grupo = AddAggregator(context, proyectoId, orden: 0, nivel: 0);
                AddLeaf(context, proyecto, "C-SAVECOUNT", matriz.Id, 1m, orden: 1);
                context.SaveChanges();
                RefreshAggregators(context, proyecto);

                grupoId = grupo.Id;
            }

            var command = CreateCommand(
                dbPath, proyectoId, grupoId, MatrixAdjustmentScopes.Materiales, 120m);

            var factory = new CountingDbContextFactory();
            var result = BudgetPersistenceService.ExecuteGroupCostAdjustmentCommand(
                factory, dbPath, command);

            Assert.AreEqual(BudgetOperationStatus.Committed, result.Status, result.Message);
            Assert.AreEqual(1, factory.Last!.SaveChangesCount,
                "El reajuste agrupado debe confirmar con un único SaveChanges.");
        }
        finally
        {
            DeleteQuietly(dbPath);
        }
    }

    [TestMethod]
    public void ExecuteGroupCostAdjustmentCommand_MatrizSeleccionadaNoAjustableComoAncestro_CambiaPorSuAuxiliarYNoSeTrataComoFija()
    {
        var dbPath = TestDbFactory.CreateTempDbPath();
        try
        {
            int proyectoId;
            int matrizBasicoId;
            int matrizApuId;
            int grupoId;
            int hojaBasicoId;
            int hojaApuId;

            using (var context = TestDbFactory.CreateContextAt(dbPath))
            {
                var proyecto = AddProject(context, utilidad: 0m);
                proyectoId = proyecto.Id;

                var material = AddMaterial(context, proyectoId, "MAT-ANCESTRO", 10m);
                var manoObra = AddManoDeObra(context, proyectoId, "MO-ANCESTRO", 20m);

                var basico = AddMatrix(context, proyectoId, "BAS-ANCESTRO", TipoMatriz.Basico);
                AddComponent(context, basico, TipoComponenteMatriz.Material, orden: 1,
                    cantidad: 2m, material: material);
                RecalculateMatrix(context, proyecto, basico);

                var apu = AddMatrix(context, proyectoId, "APU-ANCESTRO", TipoMatriz.APU);
                AddComponent(context, apu, TipoComponenteMatriz.Auxiliar, orden: 1,
                    cantidad: 3m, auxiliar: basico);
                AddComponent(context, apu, TipoComponenteMatriz.ManoDeObra, orden: 2,
                    cantidad: 1m, manoDeObra: manoObra);
                RecalculateMatrix(context, proyecto, apu);

                var grupo = AddAggregator(context, proyectoId, orden: 0, nivel: 0);
                var hojaBasico = AddLeaf(context, proyecto, "C-BAS-ANCESTRO", basico.Id, 1m, orden: 1);
                var hojaApu = AddLeaf(context, proyecto, "C-APU-ANCESTRO", apu.Id, 1m, orden: 2);

                context.SaveChanges();
                RefreshAggregators(context, proyecto);

                matrizBasicoId = basico.Id;
                matrizApuId = apu.Id;
                grupoId = grupo.Id;
                hojaBasicoId = hojaBasico.Id;
                hojaApuId = hojaApu.Id;
            }

            var command = CreateCommand(
                dbPath, proyectoId, grupoId, MatrixAdjustmentScopes.Materiales, 140m);

            var result = BudgetPersistenceService.ExecuteGroupCostAdjustmentCommand(
                new ProjectDbContextFactory(), dbPath, command);

            Assert.AreEqual(BudgetOperationStatus.Committed, result.Status, result.Message);
            var snapshot = result.Confirmed!;

            Assert.AreEqual(100m, snapshot.CurrentAmount);
            Assert.AreEqual(140m, snapshot.AchievedAmount);
            CollectionAssert.AreEqual(new[] { matrizBasicoId }, snapshot.AdjustedMatrixIds.ToArray(),
                "Solo la matriz con rubros propios recibe el factor.");
            Assert.AreEqual(2, snapshot.AdjustableLeafCount,
                "La hoja del ancestro también cambia por el auxiliar reajustado.");
            Assert.AreEqual(0, snapshot.FixedLeafCount);

            using var verificacion = new SOPROContext(dbPath);
            var basicoBd = verificacion.Matrices.AsNoTracking()
                .Include(m => m.Componentes)
                .Single(m => m.Id == matrizBasicoId);
            Assert.AreEqual(30m, basicoBd.CostoDirecto);
            Assert.AreEqual(3m, basicoBd.Componentes.Single().Cantidad);

            var apuBd = verificacion.Matrices.AsNoTracking()
                .Include(m => m.Componentes)
                .Single(m => m.Id == matrizApuId);
            Assert.AreEqual(110m, apuBd.CostoDirecto,
                "El ancestro sin rubros propios se recalcula con su auxiliar ya reajustado.");
            Assert.AreEqual(3m, apuBd.Componentes.Single(c => c.TipoComponente == TipoComponenteMatriz.Auxiliar).Cantidad);
            Assert.AreEqual(1m, apuBd.Componentes.Single(c => c.TipoComponente == TipoComponenteMatriz.ManoDeObra).Cantidad);

            Assert.AreEqual(30m, verificacion.ConceptosPresupuesto.AsNoTracking()
                .Single(c => c.Id == hojaBasicoId).ImporteTotal);
            Assert.AreEqual(110m, verificacion.ConceptosPresupuesto.AsNoTracking()
                .Single(c => c.Id == hojaApuId).ImporteTotal);
            Assert.AreEqual(140m, verificacion.ConceptosPresupuesto.AsNoTracking()
                .Single(c => c.Id == grupoId).ImporteTotal);
        }
        finally
        {
            DeleteQuietly(dbPath);
        }
    }

    [TestMethod]
    public void ExecuteGroupCostAdjustmentCommand_CierreConIntermedioNoSeleccionado_PropagaYNoLoTrataComoFijo()
    {
        var dbPath = TestDbFactory.CreateTempDbPath();
        try
        {
            int proyectoId;
            int matrizBasicoId;
            int matrizIntermediaId;
            int matrizApuId;
            int grupoId;
            int hojaBasicoId;
            int hojaApuId;

            using (var context = TestDbFactory.CreateContextAt(dbPath))
            {
                var proyecto = AddProject(context, utilidad: 0m);
                proyectoId = proyecto.Id;

                var material = AddMaterial(context, proyectoId, "MAT-INTERMEDIO", 10m);
                var manoObra = AddManoDeObra(context, proyectoId, "MO-INTERMEDIO", 20m);

                var basico = AddMatrix(context, proyectoId, "BAS-INTERMEDIO", TipoMatriz.Basico);
                AddComponent(context, basico, TipoComponenteMatriz.Material, orden: 1,
                    cantidad: 2m, material: material);
                RecalculateMatrix(context, proyecto, basico);

                var intermedia = AddMatrix(context, proyectoId, "BAS-INTERMEDIO-2", TipoMatriz.Basico);
                AddComponent(context, intermedia, TipoComponenteMatriz.Auxiliar, orden: 1,
                    cantidad: 2m, auxiliar: basico);
                RecalculateMatrix(context, proyecto, intermedia);

                var apu = AddMatrix(context, proyectoId, "APU-INTERMEDIO", TipoMatriz.APU);
                AddComponent(context, apu, TipoComponenteMatriz.Auxiliar, orden: 1,
                    cantidad: 1m, auxiliar: intermedia);
                AddComponent(context, apu, TipoComponenteMatriz.ManoDeObra, orden: 2,
                    cantidad: 1m, manoDeObra: manoObra);
                RecalculateMatrix(context, proyecto, apu);

                var grupo = AddAggregator(context, proyectoId, orden: 0, nivel: 0);
                var hojaBasico = AddLeaf(context, proyecto, "C-BAS-INTERMEDIO", basico.Id, 1m, orden: 1);
                var hojaApu = AddLeaf(context, proyecto, "C-APU-INTERMEDIO", apu.Id, 1m, orden: 2);

                context.SaveChanges();
                RefreshAggregators(context, proyecto);

                matrizBasicoId = basico.Id;
                matrizIntermediaId = intermedia.Id;
                matrizApuId = apu.Id;
                grupoId = grupo.Id;
                hojaBasicoId = hojaBasico.Id;
                hojaApuId = hojaApu.Id;
            }

            var command = CreateCommand(
                dbPath, proyectoId, grupoId, MatrixAdjustmentScopes.Materiales, 110m);

            var result = BudgetPersistenceService.ExecuteGroupCostAdjustmentCommand(
                new ProjectDbContextFactory(), dbPath, command);

            Assert.AreEqual(BudgetOperationStatus.Committed, result.Status, result.Message);
            var snapshot = result.Confirmed!;

            Assert.AreEqual(80m, snapshot.CurrentAmount);
            Assert.AreEqual(110m, snapshot.AchievedAmount);
            CollectionAssert.AreEqual(new[] { matrizBasicoId }, snapshot.AdjustedMatrixIds.ToArray());
            Assert.AreEqual(3, snapshot.AffectedMatrices.Count,
                "La instantánea incluye la matriz intermedia no seleccionada del cierre.");
            Assert.AreEqual(2, snapshot.AdjustableLeafCount);
            Assert.AreEqual(0, snapshot.FixedLeafCount);

            using var verificacion = new SOPROContext(dbPath);
            Assert.AreEqual(30m, verificacion.Matrices.AsNoTracking()
                .Single(m => m.Id == matrizBasicoId).CostoDirecto);
            Assert.AreEqual(60m, verificacion.Matrices.AsNoTracking()
                .Single(m => m.Id == matrizIntermediaId).CostoDirecto,
                "La matriz intermedia se recalcula aunque no esté seleccionada.");
            Assert.AreEqual(80m, verificacion.Matrices.AsNoTracking()
                .Single(m => m.Id == matrizApuId).CostoDirecto);

            Assert.AreEqual(30m, verificacion.ConceptosPresupuesto.AsNoTracking()
                .Single(c => c.Id == hojaBasicoId).ImporteTotal);
            Assert.AreEqual(80m, verificacion.ConceptosPresupuesto.AsNoTracking()
                .Single(c => c.Id == hojaApuId).ImporteTotal);
            Assert.AreEqual(110m, verificacion.ConceptosPresupuesto.AsNoTracking()
                .Single(c => c.Id == grupoId).ImporteTotal);
        }
        finally
        {
            DeleteQuietly(dbPath);
        }
    }

    [TestMethod]
    public void ExecuteGroupCostAdjustmentCommand_GrupoSoloConCostoFijo_DevuelveFalloSinEscribir()
    {
        var dbPath = TestDbFactory.CreateTempDbPath();
        try
        {
            int proyectoId;
            int grupoId;
            int hojaId;

            using (var context = TestDbFactory.CreateContextAt(dbPath))
            {
                var proyecto = AddProject(context, utilidad: 0m);
                proyectoId = proyecto.Id;

                var grupo = AddAggregator(context, proyectoId, orden: 0, nivel: 0);
                var hoja = AddLeaf(context, proyecto, "C-SOLO-FIJA", null, 2m, orden: 1,
                    costoDirectoUnitario: 50m);

                context.SaveChanges();
                RefreshAggregators(context, proyecto);

                grupoId = grupo.Id;
                hojaId = hoja.Id;
            }

            var bases = BudgetPersistenceService.LoadGroupCostAdjustmentBases(
                new ProjectDbContextFactory(), dbPath, proyectoId, grupoId);

            Assert.AreEqual(0, bases.SelectedMatrixIds.Count);
            Assert.AreEqual(0, bases.Closure.Entries.Count);

            var command = new BudgetGroupCostAdjustmentCommand(
                bases, MatrixAdjustmentScopes.Materiales, 100m);

            var factory = new CountingDbContextFactory();
            var result = BudgetPersistenceService.ExecuteGroupCostAdjustmentCommand(
                factory, dbPath, command);

            Assert.AreEqual(BudgetOperationStatus.Failed, result.Status, result.Message);
            Assert.IsNull(result.Confirmed);
            StringAssert.Contains(result.Message!, "no contiene conceptos hoja con matriz");
            Assert.AreEqual(0, factory.Last!.SaveChangesCount);

            using var verificacion = new SOPROContext(dbPath);
            Assert.AreEqual(100m, verificacion.ConceptosPresupuesto.AsNoTracking()
                .Single(c => c.Id == hojaId).ImporteTotal);
            Assert.AreEqual(100m, verificacion.ConceptosPresupuesto.AsNoTracking()
                .Single(c => c.Id == grupoId).ImporteTotal);
        }
        finally
        {
            DeleteQuietly(dbPath);
        }
    }

    [TestMethod]
    public void ExecuteGroupCostAdjustmentCommand_ObjetivoFueraDelBracketTrasElLimite_DevuelveFalloSinEscribir()
    {
        var dbPath = TestDbFactory.CreateTempDbPath();
        try
        {
            int proyectoId;
            int matrizId;
            int componenteId;
            int grupoId;
            int hojaId;

            using (var context = TestDbFactory.CreateContextAt(dbPath))
            {
                var proyecto = AddProject(context, utilidad: 0m);
                proyectoId = proyecto.Id;

                var material = AddMaterial(context, proyectoId, "MAT-BRACKET", 10m);

                var matriz = AddMatrix(context, proyectoId, "APU-BRACKET", TipoMatriz.APU);
                var componente = AddComponent(context, matriz, TipoComponenteMatriz.Material, orden: 1,
                    cantidad: 10m, material: material);
                RecalculateMatrix(context, proyecto, matriz);

                var grupo = AddAggregator(context, proyectoId, orden: 0, nivel: 0);
                var hoja = AddLeaf(context, proyecto, "C-BRACKET", matriz.Id, 1m, orden: 1);

                context.SaveChanges();
                RefreshAggregators(context, proyecto);

                matrizId = matriz.Id;
                componenteId = componente.Id;
                grupoId = grupo.Id;
                hojaId = hoja.Id;
            }

            // Objetivo imposible dentro del rango explorado (factor máximo 2^21 sobre
            // un importe de 100): el extremo superior simulado nunca lo alcanza.
            const decimal objetivoInalcanzable = 1_000_000_000_000m;

            var command = CreateCommand(
                dbPath, proyectoId, grupoId, MatrixAdjustmentScopes.Materiales, objetivoInalcanzable);

            var factory = new CountingDbContextFactory();
            var result = BudgetPersistenceService.ExecuteGroupCostAdjustmentCommand(
                factory, dbPath, command);

            Assert.AreEqual(BudgetOperationStatus.Failed, result.Status, result.Message);
            Assert.IsFalse(result.IsCommitted, "Un objetivo fuera del bracket no puede confirmarse.");
            Assert.IsNull(result.Confirmed);
            StringAssert.Contains(result.Message!, "No se pudo acotar el factor");
            StringAssert.Contains(result.Message!, "No se guardó nada");
            Assert.AreEqual(0, factory.Last!.SaveChangesCount,
                "Un objetivo fuera del bracket no debe llegar a SaveChanges.");

            using var verificacion = new SOPROContext(dbPath);
            var matrizBd = verificacion.Matrices.AsNoTracking()
                .Include(m => m.Componentes)
                .Single(m => m.Id == matrizId);
            Assert.AreEqual(100m, matrizBd.CostoDirecto,
                "La matriz debe conservar su costo directo.");
            var componenteBd = matrizBd.Componentes.Single(c => c.Id == componenteId);
            Assert.AreEqual(10m, componenteBd.Cantidad,
                "La cantidad del componente no debe reajustarse.");
            Assert.AreEqual(100m, componenteBd.Importe,
                "El importe del componente no debe reajustarse.");

            var hojaBd = verificacion.ConceptosPresupuesto.AsNoTracking()
                .Single(c => c.Id == hojaId);
            Assert.AreEqual(100m, hojaBd.PrecioUnitario);
            Assert.AreEqual(100m, hojaBd.ImporteTotal);

            var grupoBd = verificacion.ConceptosPresupuesto.AsNoTracking()
                .Single(c => c.Id == grupoId);
            Assert.AreEqual(100m, grupoBd.ImporteTotal);
            Assert.AreEqual(100m, grupoBd.CostoDirectoTotal);
        }
        finally
        {
            DeleteQuietly(dbPath);
        }
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

    private static BudgetGroupCostAdjustmentCommand CreateCommand(
        string dbPath,
        int proyectoId,
        int grupoId,
        MatrixAdjustmentScopes scopes,
        decimal targetAmount)
    {
        var bases = BudgetPersistenceService.LoadGroupCostAdjustmentBases(
            new ProjectDbContextFactory(), dbPath, proyectoId, grupoId);

        return new BudgetGroupCostAdjustmentCommand(bases, scopes, targetAmount);
    }

    private static Proyecto AddProject(SOPROContext context, decimal utilidad)
    {
        var proyecto = new Proyecto
        {
            Nombre = "Proyecto prueba reajuste agrupado",
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

    private static ConceptoPresupuesto AddAggregator(
        SOPROContext context,
        int proyectoId,
        int orden,
        int nivel)
    {
        var agrupador = new ConceptoPresupuesto
        {
            ProyectoId = proyectoId,
            Clave = $"AGR-{orden}",
            Descripcion = "Agrupador de prueba",
            Unidad = string.Empty,
            Cantidad = 0m,
            CostoDirectoUnitario = 0m,
            CostoDirectoTotal = 0m,
            PrecioUnitario = 0m,
            ImporteTotal = 0m,
            Nivel = nivel,
            Orden = orden,
            EsAgrupador = true,
            ColumnasPersonalizadasJSON = string.Empty,
            Notas = string.Empty
        };

        context.ConceptosPresupuesto.Add(agrupador);
        return agrupador;
    }

    private static ConceptoPresupuesto AddLeaf(
        SOPROContext context,
        Proyecto proyecto,
        string clave,
        int? matrizId,
        decimal cantidad,
        int orden,
        decimal? costoDirectoUnitario = null)
    {
        decimal costoDirecto = costoDirectoUnitario
            ?? MatrixCosto(context, matrizId
                ?? throw new ArgumentNullException(nameof(matrizId), "Una hoja con matriz requiere su Id."));

        decimal precioUnitario = BudgetPricingService.CalculateUnitPrice(proyecto, costoDirecto);
        decimal costoDirectoTotal = BudgetPricingService.MultiplyUsingDisplayPrecision(
            proyecto, cantidad, costoDirecto);
        decimal importeTotal = BudgetPricingService.MultiplyUsingDisplayPrecision(
            proyecto, cantidad, precioUnitario);

        var hoja = new ConceptoPresupuesto
        {
            ProyectoId = proyecto.Id,
            Clave = clave,
            Descripcion = "Concepto de prueba",
            Unidad = "pza",
            Cantidad = cantidad,
            MatrizId = matrizId,
            CostoDirectoUnitario = costoDirecto,
            CostoDirectoTotal = costoDirectoTotal,
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

    private static void RefreshAggregators(SOPROContext context, Proyecto proyecto)
    {
        PricePropagationService.ActualizarAgrupadores(context, new List<Proyecto> { proyecto });
        context.SaveChanges();
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
}
