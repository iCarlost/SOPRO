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
/// Contrato del comando único de eliminación de UN componente persistido de matriz
/// (ADR-0001): intención inmutable con bases esperadas del proyecto, la matriz, el
/// fingerprint de componentes, el componente a eliminar y el rowset COMPLETO del
/// presupuesto; revalidación exacta contra la base, exclusión del componente del
/// cálculo, cierre topológico de matrices auxiliares, recálculo de TODAS las hojas
/// afectadas y de los agrupadores, y un solo commit.
/// </summary>
[TestClass]
public class BudgetMatrixComponentDeletionPersistenceTests
{
    [TestMethod]
    public void ExecuteMatrixComponentDeletionCommand_DosHojasYAgrupador_ActualizaValoresYNoTocaOtrosComponentes()
    {
        var dbPath = TestDbFactory.CreateTempDbPath();
        try
        {
            int proyectoId;
            int matrizId;
            int componenteEliminadoId;
            int primeraHojaId;
            int segundaHojaId;
            int agrupadorId;
            Dictionary<int, (TipoComponenteMatriz Tipo, int? MaterialId, int? ManoDeObraId,
                int? MaquinariaId, int? AuxiliarId, int? HerramientaId, decimal Cantidad,
                decimal Rendimiento, decimal Importe, int Orden)> otrosAntes;

            using (var context = TestDbFactory.CreateContextAt(dbPath))
            {
                var scenario = SoproCalculationScenarioBuilder.CreateBaseBudgetScenario(context);
                proyectoId = scenario.Proyecto.Id;
                matrizId = scenario.MatrizApu.Id;
                primeraHojaId = scenario.Concepto.Id;
                componenteEliminadoId = scenario.MatrizApu.Componentes
                    .Single(c => c.TipoComponente == TipoComponenteMatriz.Maquinaria).Id;

                var agrupador = AddAggregator(context, proyectoId);
                var segundaHoja = AddLeaf(context, proyectoId, "C-002", matrizId, 5m, "pza", 2);
                context.SaveChanges();

                agrupadorId = agrupador.Id;
                segundaHojaId = segundaHoja.Id;

                otrosAntes = context.ComponentesMatriz
                    .AsNoTracking()
                    .Where(c => c.MatrizId == matrizId && c.Id != componenteEliminadoId)
                    .ToDictionary(
                        c => c.Id,
                        c => (c.TipoComponente, c.MaterialId, c.ManoDeObraId, c.MaquinariaId,
                            c.AuxiliarId, c.HerramientaId, c.Cantidad, c.Rendimiento, c.Importe, c.Orden));
            }

            var command = CreateCommand(dbPath, proyectoId, matrizId, componenteEliminadoId);
            var factory = new SaveChangesSpyFactory();

            var result = BudgetPersistenceService.ExecuteMatrixComponentDeletionCommand(
                factory, dbPath, command);

            Assert.AreEqual(BudgetOperationStatus.Committed, result.Status, result.Message);
            Assert.IsNull(result.Diagnostic);
            Assert.IsNotNull(result.Confirmed);
            Assert.AreEqual(1, factory.LastContext!.SaveChangesCount, "El comando confirma con UN solo SaveChanges.");

            // 100 originales - 4 de maquinaria.
            const decimal costoDirectoEsperado = 96m;
            const decimal importePrimeraHoja = 960m;
            const decimal importeSegundaHoja = 480m;

            using (var verificacion = new SOPROContext(dbPath))
            {
                Assert.IsFalse(verificacion.ComponentesMatriz.AsNoTracking()
                    .Any(c => c.Id == componenteEliminadoId), "El componente objetivo deja de existir.");

                var matriz = verificacion.Matrices.AsNoTracking().Single(m => m.Id == matrizId);
                Assert.AreEqual(costoDirectoEsperado, matriz.CostoDirecto);

                var primera = verificacion.ConceptosPresupuesto.AsNoTracking().Single(c => c.Id == primeraHojaId);
                var segunda = verificacion.ConceptosPresupuesto.AsNoTracking().Single(c => c.Id == segundaHojaId);

                Assert.AreEqual(costoDirectoEsperado, primera.CostoDirectoUnitario);
                Assert.AreEqual(costoDirectoEsperado, primera.PrecioUnitario);
                Assert.AreEqual(importePrimeraHoja, primera.ImporteTotal);

                Assert.AreEqual(costoDirectoEsperado, segunda.CostoDirectoUnitario,
                    "TODAS las hojas que usan la matriz se recalculan, no solo la fila activa.");
                Assert.AreEqual(importeSegundaHoja, segunda.ImporteTotal);

                var agrupador = verificacion.ConceptosPresupuesto.AsNoTracking().Single(c => c.Id == agrupadorId);
                Assert.AreEqual(importePrimeraHoja + importeSegundaHoja, agrupador.ImporteTotal);
                Assert.AreEqual(importePrimeraHoja + importeSegundaHoja, agrupador.CostoDirectoTotal);

                var otrosDespues = verificacion.ComponentesMatriz
                    .AsNoTracking()
                    .Where(c => c.MatrizId == matrizId)
                    .ToDictionary(
                        c => c.Id,
                        c => (c.TipoComponente, c.MaterialId, c.ManoDeObraId, c.MaquinariaId,
                            c.AuxiliarId, c.HerramientaId, c.Cantidad, c.Rendimiento, c.Importe, c.Orden));

                CollectionAssert.AreEquivalent(otrosAntes.Keys.ToList(), otrosDespues.Keys.ToList(),
                    "No se eliminan ni se agregan otros componentes.");
                foreach (var par in otrosAntes)
                    Assert.AreEqual(par.Value, otrosDespues[par.Key], $"El componente {par.Key} no debe cambiar.");
            }

            var snapshot = result.Confirmed!;
            Assert.AreEqual(componenteEliminadoId, snapshot.DeletedComponentId);
            Assert.AreEqual(costoDirectoEsperado, snapshot.Matrix.CostoDirecto);
            Assert.AreEqual(1, snapshot.AffectedMatrices.Count);
            Assert.AreEqual(4, snapshot.AffectedComponents.Count, "El componente eliminado no viaja en la instantánea.");
            Assert.IsFalse(snapshot.AffectedComponents.Any(c => c.ComponentId == componenteEliminadoId));
            Assert.AreEqual(2, snapshot.Leaves.Count);
            Assert.AreEqual(1, snapshot.Aggregators.Count);
            Assert.AreEqual(importePrimeraHoja + importeSegundaHoja, snapshot.Aggregators[0].ImporteTotal);
        }
        finally
        {
            DeleteQuietly(dbPath);
        }
    }

    [TestMethod]
    public void ExecuteMatrixComponentDeletionCommand_MatrizAnidada_RecalculaCierreTopologicoYHojaDelAncestro()
    {
        var dbPath = TestDbFactory.CreateTempDbPath();
        try
        {
            int proyectoId;
            int matrizBasicoId;
            int matrizApuId;
            int componenteMaterialId;
            int componenteAuxiliarId;
            int hojaId;

            using (var context = TestDbFactory.CreateContextAt(dbPath))
            {
                var proyecto = AddProject(context, "Proyecto matrices anidadas");
                proyectoId = proyecto.Id;

                var material = AddMaterial(context, proyectoId, "MAT-NEST", 10m);

                var basico = AddMatrix(context, proyectoId, "BAS-001", TipoMatriz.Basico, 20m);
                var materialBasico = AddComponent(
                    context, basico.Id, TipoComponenteMatriz.Material, orden: 1,
                    cantidad: 2m, importe: 20m, materialId: material.Id);

                var apu = AddMatrix(context, proyectoId, "APU-NEST", TipoMatriz.APU, 60m);
                var auxiliar = AddComponent(
                    context, apu.Id, TipoComponenteMatriz.Auxiliar, orden: 1,
                    cantidad: 3m, importe: 60m, auxiliarId: basico.Id);

                var hoja = AddLeaf(context, proyectoId, "C-NEST", apu.Id, 1m, "pza", 1);
                hoja.CostoDirectoUnitario = 60m;
                hoja.CostoDirectoTotal = 60m;
                hoja.PrecioUnitario = 60m;
                hoja.ImporteTotal = 60m;

                context.SaveChanges();

                matrizBasicoId = basico.Id;
                matrizApuId = apu.Id;
                componenteMaterialId = materialBasico.Id;
                componenteAuxiliarId = auxiliar.Id;
                hojaId = hoja.Id;
            }

            var command = CreateCommand(dbPath, proyectoId, matrizBasicoId, componenteMaterialId);

            var result = BudgetPersistenceService.ExecuteMatrixComponentDeletionCommand(
                new ProjectDbContextFactory(), dbPath, command);

            Assert.AreEqual(BudgetOperationStatus.Committed, result.Status, result.Message);
            Assert.IsNotNull(result.Confirmed);

            using (var verificacion = new SOPROContext(dbPath))
            {
                Assert.IsFalse(verificacion.ComponentesMatriz.AsNoTracking()
                    .Any(c => c.Id == componenteMaterialId));

                var basico = verificacion.Matrices.AsNoTracking().Single(m => m.Id == matrizBasicoId);
                var apu = verificacion.Matrices.AsNoTracking().Single(m => m.Id == matrizApuId);

                Assert.AreEqual(0m, basico.CostoDirecto, "El básico queda sin componentes.");
                Assert.AreEqual(0m, apu.CostoDirecto, "El ancestro consume el costo ya fresco del básico.");

                var componenteAuxiliar = verificacion.ComponentesMatriz.AsNoTracking()
                    .Single(c => c.Id == componenteAuxiliarId);
                Assert.AreEqual(0m, componenteAuxiliar.Importe);

                var hoja = verificacion.ConceptosPresupuesto.AsNoTracking().Single(c => c.Id == hojaId);
                Assert.AreEqual(0m, hoja.CostoDirectoUnitario);
                Assert.AreEqual(0m, hoja.PrecioUnitario);
                Assert.AreEqual(0m, hoja.ImporteTotal);
            }

            var snapshot = result.Confirmed!;
            Assert.AreEqual(componenteMaterialId, snapshot.DeletedComponentId);
            Assert.AreEqual(2, snapshot.AffectedMatrices.Count);
            Assert.AreEqual(0m, snapshot.AffectedMatrices.Single(m => m.MatrixId == matrizBasicoId).CostoDirecto);
            Assert.AreEqual(0m, snapshot.AffectedMatrices.Single(m => m.MatrixId == matrizApuId).CostoDirecto);
            Assert.AreEqual(1, snapshot.AffectedComponents.Count, "Solo queda el componente auxiliar del APU.");
            Assert.AreEqual(componenteAuxiliarId, snapshot.AffectedComponents[0].ComponentId);
            Assert.AreEqual(0m, snapshot.AffectedComponents[0].Importe);
            Assert.AreEqual(1, snapshot.Leaves.Count);
            Assert.AreEqual(0m, snapshot.Leaves[0].ImporteTotal);
        }
        finally
        {
            DeleteQuietly(dbPath);
        }
    }

    [TestMethod]
    public void ExecuteMatrixComponentDeletionCommand_ComponenteCambioEnLaBase_DevuelveConflictoSinEscribir()
    {
        var dbPath = TestDbFactory.CreateTempDbPath();
        try
        {
            int proyectoId;
            int matrizId;
            int componenteEliminadoId;

            using (var context = TestDbFactory.CreateContextAt(dbPath))
            {
                var scenario = SoproCalculationScenarioBuilder.CreateBaseBudgetScenario(context);
                proyectoId = scenario.Proyecto.Id;
                matrizId = scenario.MatrizApu.Id;
                componenteEliminadoId = scenario.MatrizApu.Componentes
                    .Single(c => c.TipoComponente == TipoComponenteMatriz.Maquinaria).Id;
            }

            var command = CreateCommand(dbPath, proyectoId, matrizId, componenteEliminadoId);

            using (var otroModulo = new SOPROContext(dbPath))
            {
                otroModulo.ComponentesMatriz.Single(c => c.Id == componenteEliminadoId).Cantidad = 2m;
                otroModulo.SaveChanges();
            }

            var result = BudgetPersistenceService.ExecuteMatrixComponentDeletionCommand(
                new ProjectDbContextFactory(), dbPath, command);

            Assert.AreEqual(BudgetOperationStatus.Conflict, result.Status, result.Message);
            Assert.IsNull(result.Confirmed);

            using var verificacion = new SOPROContext(dbPath);
            var componente = verificacion.ComponentesMatriz.AsNoTracking().Single(c => c.Id == componenteEliminadoId);
            Assert.AreEqual(2m, componente.Cantidad, "El comando no escribe nada al detectar la obsolescencia.");

            var matriz = verificacion.Matrices.AsNoTracking().Single(m => m.Id == matrizId);
            Assert.AreEqual(100m, matriz.CostoDirecto);
        }
        finally
        {
            DeleteQuietly(dbPath);
        }
    }

    [TestMethod]
    public void ExecuteMatrixComponentDeletionCommand_FingerprintDeComponentesCambiado_DevuelveConflictoSinEscribir()
    {
        var dbPath = TestDbFactory.CreateTempDbPath();
        try
        {
            int proyectoId;
            int matrizId;
            int componenteEliminadoId;

            using (var context = TestDbFactory.CreateContextAt(dbPath))
            {
                var scenario = SoproCalculationScenarioBuilder.CreateBaseBudgetScenario(context);
                proyectoId = scenario.Proyecto.Id;
                matrizId = scenario.MatrizApu.Id;
                componenteEliminadoId = scenario.MatrizApu.Componentes
                    .Single(c => c.TipoComponente == TipoComponenteMatriz.Maquinaria).Id;
            }

            var command = CreateCommand(dbPath, proyectoId, matrizId, componenteEliminadoId);

            using (var otroModulo = new SOPROContext(dbPath))
            {
                var herramienta = otroModulo.ComponentesMatriz
                    .Single(c => c.MatrizId == matrizId && c.TipoComponente == TipoComponenteMatriz.Herramienta);
                otroModulo.ComponentesMatriz.Remove(herramienta);
                otroModulo.SaveChanges();
            }

            var result = BudgetPersistenceService.ExecuteMatrixComponentDeletionCommand(
                new ProjectDbContextFactory(), dbPath, command);

            Assert.AreEqual(BudgetOperationStatus.Conflict, result.Status, result.Message);
            Assert.IsNull(result.Confirmed);

            using var verificacion = new SOPROContext(dbPath);
            Assert.IsTrue(verificacion.ComponentesMatriz.AsNoTracking()
                .Any(c => c.Id == componenteEliminadoId), "El componente objetivo sigue en la base.");
        }
        finally
        {
            DeleteQuietly(dbPath);
        }
    }

    [TestMethod]
    public void ExecuteMatrixComponentDeletionCommand_MatrizCambioEnLaBase_DevuelveConflictoSinEscribir()
    {
        var dbPath = TestDbFactory.CreateTempDbPath();
        try
        {
            int proyectoId;
            int matrizId;
            int componenteEliminadoId;

            using (var context = TestDbFactory.CreateContextAt(dbPath))
            {
                var scenario = SoproCalculationScenarioBuilder.CreateBaseBudgetScenario(context);
                proyectoId = scenario.Proyecto.Id;
                matrizId = scenario.MatrizApu.Id;
                componenteEliminadoId = scenario.MatrizApu.Componentes
                    .Single(c => c.TipoComponente == TipoComponenteMatriz.Maquinaria).Id;
            }

            var command = CreateCommand(dbPath, proyectoId, matrizId, componenteEliminadoId);

            using (var otroModulo = new SOPROContext(dbPath))
            {
                otroModulo.Matrices.Single(m => m.Id == matrizId).CostoDirecto = 999m;
                otroModulo.SaveChanges();
            }

            var result = BudgetPersistenceService.ExecuteMatrixComponentDeletionCommand(
                new ProjectDbContextFactory(), dbPath, command);

            Assert.AreEqual(BudgetOperationStatus.Conflict, result.Status, result.Message);
            Assert.IsNull(result.Confirmed);

            using var verificacion = new SOPROContext(dbPath);
            Assert.IsTrue(verificacion.ComponentesMatriz.AsNoTracking()
                .Any(c => c.Id == componenteEliminadoId), "El componente objetivo sigue en la base.");
        }
        finally
        {
            DeleteQuietly(dbPath);
        }
    }

    [TestMethod]
    public void ExecuteMatrixComponentDeletionCommand_RowsetDelPresupuestoCambiado_DevuelveConflictoSinEscribir()
    {
        var dbPath = TestDbFactory.CreateTempDbPath();
        try
        {
            int proyectoId;
            int matrizId;
            int componenteEliminadoId;
            int hojaId;

            using (var context = TestDbFactory.CreateContextAt(dbPath))
            {
                var scenario = SoproCalculationScenarioBuilder.CreateBaseBudgetScenario(context);
                proyectoId = scenario.Proyecto.Id;
                matrizId = scenario.MatrizApu.Id;
                hojaId = scenario.Concepto.Id;
                componenteEliminadoId = scenario.MatrizApu.Componentes
                    .Single(c => c.TipoComponente == TipoComponenteMatriz.Maquinaria).Id;
            }

            var command = CreateCommand(dbPath, proyectoId, matrizId, componenteEliminadoId);

            using (var otroModulo = new SOPROContext(dbPath))
            {
                otroModulo.ConceptosPresupuesto.Single(c => c.Id == hojaId).Cantidad = 11m;
                otroModulo.SaveChanges();
            }

            var result = BudgetPersistenceService.ExecuteMatrixComponentDeletionCommand(
                new ProjectDbContextFactory(), dbPath, command);

            Assert.AreEqual(BudgetOperationStatus.Conflict, result.Status, result.Message);
            Assert.IsNull(result.Confirmed);

            using var verificacion = new SOPROContext(dbPath);
            Assert.IsTrue(verificacion.ComponentesMatriz.AsNoTracking()
                .Any(c => c.Id == componenteEliminadoId), "El componente objetivo sigue en la base.");

            var hoja = verificacion.ConceptosPresupuesto.AsNoTracking().Single(c => c.Id == hojaId);
            Assert.AreEqual(11m, hoja.Cantidad);
        }
        finally
        {
            DeleteQuietly(dbPath);
        }
    }

    [TestMethod]
    public void ExecuteMatrixComponentDeletionCommand_FalloDeTrigger_RevierteYElMismoComandoReintenta()
    {
        var dbPath = TestDbFactory.CreateTempDbPath();
        try
        {
            int proyectoId;
            int matrizId;
            int componenteEliminadoId;
            int hojaId;

            using (var context = TestDbFactory.CreateContextAt(dbPath))
            {
                var scenario = SoproCalculationScenarioBuilder.CreateBaseBudgetScenario(context);
                proyectoId = scenario.Proyecto.Id;
                matrizId = scenario.MatrizApu.Id;
                hojaId = scenario.Concepto.Id;
                componenteEliminadoId = scenario.MatrizApu.Componentes
                    .Single(c => c.TipoComponente == TipoComponenteMatriz.Maquinaria).Id;
            }

            var command = CreateCommand(dbPath, proyectoId, matrizId, componenteEliminadoId);

            using (var admin = new SOPROContext(dbPath))
            {
                admin.Database.ExecuteSqlRaw(
                    "CREATE TRIGGER trg_fail_delete_componente BEFORE DELETE ON ComponentesMatriz " +
                    "BEGIN SELECT RAISE(ABORT, 'fallo_simulado_delete'); END;");
            }

            var primerIntento = BudgetPersistenceService.ExecuteMatrixComponentDeletionCommand(
                new ProjectDbContextFactory(), dbPath, command);

            Assert.AreEqual(BudgetOperationStatus.Failed, primerIntento.Status, primerIntento.Message);
            Assert.IsNull(primerIntento.Confirmed);
            StringAssert.Contains(primerIntento.Diagnostic!, "fallo_simulado_delete");

            using (var verificacion = new SOPROContext(dbPath))
            {
                Assert.IsTrue(verificacion.ComponentesMatriz.AsNoTracking()
                    .Any(c => c.Id == componenteEliminadoId), "El trigger revierte TODA la escritura.");

                var matriz = verificacion.Matrices.AsNoTracking().Single(m => m.Id == matrizId);
                Assert.AreEqual(100m, matriz.CostoDirecto);

                var hoja = verificacion.ConceptosPresupuesto.AsNoTracking().Single(c => c.Id == hojaId);
                Assert.AreEqual(1000m, hoja.ImporteTotal);
            }

            using (var admin = new SOPROContext(dbPath))
            {
                admin.Database.ExecuteSqlRaw("DROP TRIGGER trg_fail_delete_componente;");
            }

            var reintento = BudgetPersistenceService.ExecuteMatrixComponentDeletionCommand(
                new ProjectDbContextFactory(), dbPath, command);

            Assert.AreEqual(BudgetOperationStatus.Committed, reintento.Status, reintento.Message);

            using var verificacionFinal = new SOPROContext(dbPath);
            Assert.IsFalse(verificacionFinal.ComponentesMatriz.AsNoTracking()
                .Any(c => c.Id == componenteEliminadoId));
            Assert.AreEqual(96m, verificacionFinal.Matrices.AsNoTracking().Single(m => m.Id == matrizId).CostoDirecto);
            Assert.AreEqual(960m,
                verificacionFinal.ConceptosPresupuesto.AsNoTracking().Single(c => c.Id == hojaId).ImporteTotal);
        }
        finally
        {
            DeleteQuietly(dbPath);
        }
    }

    // ────────────────────────────────────────────────────────────────────────
    // Infraestructura
    // ────────────────────────────────────────────────────────────────────────

    private static BudgetMatrixComponentDeletionCommand CreateCommand(
        string dbPath,
        int proyectoId,
        int matrizId,
        int componenteId)
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

        var componente = componentes.Single(c => c.Id == componenteId);

        var filas = context.ConceptosPresupuesto
            .AsNoTracking()
            .Where(c => c.ProyectoId == proyectoId)
            .OrderBy(c => c.Orden)
            .ThenBy(c => c.Id)
            .ToList();

        return new BudgetMatrixComponentDeletionCommand(
            proyectoId,
            BudgetProjectCalculationBase.FromProyecto(proyecto),
            BudgetMatrixCalculationBase.FromMatriz(matriz),
            BudgetMatrixComponentFingerprint.FromComponentes(componentes),
            BudgetMatrixComponentQuantityBase.FromComponente(componente),
            BudgetPriceRefreshRowset.FromConceptos(filas));
    }

    private sealed class SaveChangesSpyFactory : IProjectDbContextFactory
    {
        public SaveChangesSpyContext? LastContext { get; private set; }

        public SOPROContext Create(string databasePath)
        {
            LastContext = new SaveChangesSpyContext(databasePath);
            return LastContext;
        }

        public Task<SOPROContext> CreateAsync(string databasePath, CancellationToken cancellationToken = default)
            => Task.FromResult(Create(databasePath));
    }

    private sealed class SaveChangesSpyContext : SOPROContext
    {
        public SaveChangesSpyContext(string dbPath) : base(dbPath) { }

        public int SaveChangesCount { get; private set; }

        public override int SaveChanges(bool acceptAllChangesOnSuccess)
        {
            SaveChangesCount++;
            return base.SaveChanges(acceptAllChangesOnSuccess);
        }
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

    private static Material AddMaterial(SOPROContext context, int proyectoId, string clave, decimal precioUnitario)
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

    private static ComponenteMatriz AddComponent(
        SOPROContext context,
        int matrizId,
        TipoComponenteMatriz tipo,
        int orden,
        decimal cantidad,
        decimal importe,
        int? materialId = null,
        int? auxiliarId = null,
        decimal rendimiento = 0m)
    {
        var componente = new ComponenteMatriz
        {
            MatrizId = matrizId,
            TipoComponente = tipo,
            MaterialId = materialId,
            AuxiliarId = auxiliarId,
            Cantidad = cantidad,
            Rendimiento = rendimiento,
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
