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
/// Contrato del comando único de edición de la Cantidad de un componente persistido
/// de matriz (ADR-0001): intención inmutable con bases esperadas del proyecto, la
/// matriz, el fingerprint de componentes y el rowset COMPLETO del presupuesto;
/// revalidación exacta contra la base, aplicación canónica de la cantidad, cierre
/// topológico de matrices auxiliares, recálculo de TODAS las hojas afectadas y de
/// los agrupadores, y un solo commit.
/// </summary>
[TestClass]
public class BudgetMatrixComponentQuantityPersistenceTests
{
    [TestMethod]
    public void ExecuteMatrixComponentQuantityCommand_DosHojasMismaMatriz_RecalculaAmbasYElAgrupador()
    {
        var dbPath = TestDbFactory.CreateTempDbPath();
        try
        {
            int proyectoId;
            int matrizId;
            int componenteMaterialId;
            int primeraHojaId;
            int segundaHojaId;
            int agrupadorId;

            using (var context = TestDbFactory.CreateContextAt(dbPath))
            {
                var scenario = SoproCalculationScenarioBuilder.CreateBaseBudgetScenario(context);
                proyectoId = scenario.Proyecto.Id;
                matrizId = scenario.MatrizApu.Id;
                primeraHojaId = scenario.Concepto.Id;
                componenteMaterialId = scenario.MatrizApu.Componentes
                    .Single(c => c.TipoComponente == TipoComponenteMatriz.Material).Id;

                var agrupador = AddAggregator(context, proyectoId);
                var segundaHoja = AddLeaf(context, proyectoId, "C-002", matrizId, 5m, "pza", 2);
                context.SaveChanges();

                agrupadorId = agrupador.Id;
                segundaHojaId = segundaHoja.Id;
            }

            var command = CreateCommand(dbPath, proyectoId, matrizId, componenteMaterialId, 2m);

            var result = BudgetPersistenceService.ExecuteMatrixComponentQuantityCommand(
                new ProjectDbContextFactory(), dbPath, command);

            Assert.AreEqual(BudgetOperationStatus.Committed, result.Status, result.Message);
            Assert.IsNull(result.Diagnostic);
            Assert.IsNotNull(result.Confirmed);

            // 120 material + 30 MO + 3 %MO + 3 herramienta + 4 maquinaria, 0 % de indirectos.
            const decimal costoDirectoEsperado = 160m;
            const decimal importePrimeraHoja = 1600m;
            const decimal importeSegundaHoja = 800m;

            using (var verificacion = new SOPROContext(dbPath))
            {
                var componente = verificacion.ComponentesMatriz.AsNoTracking().Single(c => c.Id == componenteMaterialId);
                Assert.AreEqual(2m, componente.Cantidad);
                Assert.AreEqual(120m, componente.Importe);

                var matriz = verificacion.Matrices.AsNoTracking().Single(m => m.Id == matrizId);
                Assert.AreEqual(costoDirectoEsperado, matriz.CostoDirecto);

                var primera = verificacion.ConceptosPresupuesto.AsNoTracking().Single(c => c.Id == primeraHojaId);
                var segunda = verificacion.ConceptosPresupuesto.AsNoTracking().Single(c => c.Id == segundaHojaId);

                Assert.AreEqual(costoDirectoEsperado, primera.CostoDirectoUnitario,
                    "TODAS las hojas que usan la matriz se recalculan, no solo la fila activa.");
                Assert.AreEqual(costoDirectoEsperado, primera.PrecioUnitario);
                Assert.AreEqual(importePrimeraHoja, primera.ImporteTotal);

                Assert.AreEqual(costoDirectoEsperado, segunda.CostoDirectoUnitario);
                Assert.AreEqual(costoDirectoEsperado, segunda.PrecioUnitario);
                Assert.AreEqual(importeSegundaHoja, segunda.ImporteTotal);

                var agrupador = verificacion.ConceptosPresupuesto.AsNoTracking().Single(c => c.Id == agrupadorId);
                Assert.AreEqual(importePrimeraHoja + importeSegundaHoja, agrupador.ImporteTotal,
                    "El agrupador se recalcula con la política única sobre TODAS las hojas afectadas.");
                Assert.AreEqual(importePrimeraHoja + importeSegundaHoja, agrupador.CostoDirectoTotal);
            }

            var snapshot = result.Confirmed!;
            Assert.AreEqual(costoDirectoEsperado, snapshot.Matrix.CostoDirecto);
            Assert.AreEqual(1, snapshot.AffectedMatrices.Count,
                "Sin ascendientes auxiliares, el cierre es solo la matriz editada.");
            Assert.AreEqual(2, snapshot.Leaves.Count, "La instantánea incluye las dos hojas afectadas.");
            Assert.AreEqual(1, snapshot.Aggregators.Count);
            Assert.AreEqual(importePrimeraHoja + importeSegundaHoja, snapshot.Aggregators[0].ImporteTotal);
            Assert.AreEqual(120m, snapshot.AffectedComponents.Single(c => c.ComponentId == componenteMaterialId).Importe);
        }
        finally
        {
            DeleteQuietly(dbPath);
        }
    }

    [TestMethod]
    public void ExecuteMatrixComponentQuantityCommand_MatrizAnidada_RecalculaCierreTopologicoYHojaDelAncestro()
    {
        var dbPath = TestDbFactory.CreateTempDbPath();
        try
        {
            int proyectoId;
            int matrizBasicoId;
            int matrizApuId;
            int componenteMaterialId;
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
                AddComponent(
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
                hojaId = hoja.Id;
            }

            var command = CreateCommand(dbPath, proyectoId, matrizBasicoId, componenteMaterialId, 4m);

            var result = BudgetPersistenceService.ExecuteMatrixComponentQuantityCommand(
                new ProjectDbContextFactory(), dbPath, command);

            Assert.AreEqual(BudgetOperationStatus.Committed, result.Status, result.Message);
            Assert.IsNotNull(result.Confirmed);

            using (var verificacion = new SOPROContext(dbPath))
            {
                var basico = verificacion.Matrices.AsNoTracking().Single(m => m.Id == matrizBasicoId);
                var apu = verificacion.Matrices.AsNoTracking().Single(m => m.Id == matrizApuId);

                Assert.AreEqual(40m, basico.CostoDirecto, "El básico se recalcula primero.");
                Assert.AreEqual(120m, apu.CostoDirecto, "El ancestro consume el costo ya fresco del básico.");

                var componenteAuxiliar = verificacion.ComponentesMatriz.AsNoTracking()
                    .Single(c => c.MatrizId == matrizApuId && c.TipoComponente == TipoComponenteMatriz.Auxiliar);
                Assert.AreEqual(120m, componenteAuxiliar.Importe);

                var hoja = verificacion.ConceptosPresupuesto.AsNoTracking().Single(c => c.Id == hojaId);
                Assert.AreEqual(120m, hoja.CostoDirectoUnitario);
                Assert.AreEqual(120m, hoja.PrecioUnitario);
                Assert.AreEqual(120m, hoja.ImporteTotal);
            }

            var snapshot = result.Confirmed!;
            Assert.AreEqual(2, snapshot.AffectedMatrices.Count);
            Assert.AreEqual(40m, snapshot.AffectedMatrices.Single(m => m.MatrixId == matrizBasicoId).CostoDirecto);
            Assert.AreEqual(120m, snapshot.AffectedMatrices.Single(m => m.MatrixId == matrizApuId).CostoDirecto);
            Assert.AreEqual(2, snapshot.AffectedComponents.Count);
            Assert.AreEqual(1, snapshot.Leaves.Count);
            Assert.AreEqual(120m, snapshot.Leaves[0].ImporteTotal);
        }
        finally
        {
            DeleteQuietly(dbPath);
        }
    }

    [TestMethod]
    public void ExecuteMatrixComponentQuantityCommand_Maquinaria_SincronizaRendimientoComoElDialogo()
    {
        var dbPath = TestDbFactory.CreateTempDbPath();
        try
        {
            int proyectoId;
            int matrizId;
            int componenteMaquinariaId;
            int hojaId;

            using (var context = TestDbFactory.CreateContextAt(dbPath))
            {
                var scenario = SoproCalculationScenarioBuilder.CreateBaseBudgetScenario(context);
                proyectoId = scenario.Proyecto.Id;
                matrizId = scenario.MatrizApu.Id;
                hojaId = scenario.Concepto.Id;
                componenteMaquinariaId = scenario.MatrizApu.Componentes
                    .Single(c => c.TipoComponente == TipoComponenteMatriz.Maquinaria).Id;
            }

            var command = CreateCommand(dbPath, proyectoId, matrizId, componenteMaquinariaId, 4m);

            var result = BudgetPersistenceService.ExecuteMatrixComponentQuantityCommand(
                new ProjectDbContextFactory(), dbPath, command);

            Assert.AreEqual(BudgetOperationStatus.Committed, result.Status, result.Message);

            using (var verificacion = new SOPROContext(dbPath))
            {
                var componente = verificacion.ComponentesMatriz.AsNoTracking().Single(c => c.Id == componenteMaquinariaId);
                Assert.AreEqual(4m, componente.Cantidad);
                Assert.AreEqual(0.25m, componente.Rendimiento, "Rendimiento = 1/Cantidad, la semántica del diálogo.");
                Assert.AreEqual(16m, componente.Importe);

                var matriz = verificacion.Matrices.AsNoTracking().Single(m => m.Id == matrizId);
                Assert.AreEqual(112m, matriz.CostoDirecto);

                var hoja = verificacion.ConceptosPresupuesto.AsNoTracking().Single(c => c.Id == hojaId);
                Assert.AreEqual(112m, hoja.CostoDirectoUnitario);
                Assert.AreEqual(1120m, hoja.ImporteTotal);
            }
        }
        finally
        {
            DeleteQuietly(dbPath);
        }
    }

    [TestMethod]
    public void ExecuteMatrixComponentQuantityCommand_ComponenteCambioEnLaBase_DevuelveConflictoSinEscribir()
    {
        var dbPath = TestDbFactory.CreateTempDbPath();
        try
        {
            int proyectoId;
            int matrizId;
            int componenteMaterialId;
            int hojaId;

            using (var context = TestDbFactory.CreateContextAt(dbPath))
            {
                var scenario = SoproCalculationScenarioBuilder.CreateBaseBudgetScenario(context);
                proyectoId = scenario.Proyecto.Id;
                matrizId = scenario.MatrizApu.Id;
                hojaId = scenario.Concepto.Id;
                componenteMaterialId = scenario.MatrizApu.Componentes
                    .Single(c => c.TipoComponente == TipoComponenteMatriz.Material).Id;
            }

            var command = CreateCommand(dbPath, proyectoId, matrizId, componenteMaterialId, 2m);

            using (var otroModulo = new SOPROContext(dbPath))
            {
                otroModulo.ComponentesMatriz.Single(c => c.Id == componenteMaterialId).Cantidad = 1.5m;
                otroModulo.SaveChanges();
            }

            var result = BudgetPersistenceService.ExecuteMatrixComponentQuantityCommand(
                new ProjectDbContextFactory(), dbPath, command);

            Assert.AreEqual(BudgetOperationStatus.Conflict, result.Status, result.Message);
            Assert.IsNull(result.Confirmed);

            using var verificacion = new SOPROContext(dbPath);
            var componente = verificacion.ComponentesMatriz.AsNoTracking().Single(c => c.Id == componenteMaterialId);
            Assert.AreEqual(1.5m, componente.Cantidad);
            Assert.AreEqual(60m, componente.Importe, "El comando no escribe nada al detectar la obsolescencia.");

            var matriz = verificacion.Matrices.AsNoTracking().Single(m => m.Id == matrizId);
            Assert.AreEqual(100m, matriz.CostoDirecto);

            var hoja = verificacion.ConceptosPresupuesto.AsNoTracking().Single(c => c.Id == hojaId);
            Assert.AreEqual(100m, hoja.CostoDirectoUnitario);
        }
        finally
        {
            DeleteQuietly(dbPath);
        }
    }

    [TestMethod]
    public void ExecuteMatrixComponentQuantityCommand_MatrizCambioEnLaBase_DevuelveConflictoSinEscribir()
    {
        var dbPath = TestDbFactory.CreateTempDbPath();
        try
        {
            int proyectoId;
            int matrizId;
            int componenteMaterialId;

            using (var context = TestDbFactory.CreateContextAt(dbPath))
            {
                var scenario = SoproCalculationScenarioBuilder.CreateBaseBudgetScenario(context);
                proyectoId = scenario.Proyecto.Id;
                matrizId = scenario.MatrizApu.Id;
                componenteMaterialId = scenario.MatrizApu.Componentes
                    .Single(c => c.TipoComponente == TipoComponenteMatriz.Material).Id;
            }

            var command = CreateCommand(dbPath, proyectoId, matrizId, componenteMaterialId, 2m);

            using (var otroModulo = new SOPROContext(dbPath))
            {
                otroModulo.Matrices.Single(m => m.Id == matrizId).CostoDirecto = 999m;
                otroModulo.SaveChanges();
            }

            var result = BudgetPersistenceService.ExecuteMatrixComponentQuantityCommand(
                new ProjectDbContextFactory(), dbPath, command);

            Assert.AreEqual(BudgetOperationStatus.Conflict, result.Status, result.Message);
            Assert.IsNull(result.Confirmed);

            using var verificacion = new SOPROContext(dbPath);
            var componente = verificacion.ComponentesMatriz.AsNoTracking().Single(c => c.Id == componenteMaterialId);
            Assert.AreEqual(1m, componente.Cantidad);
        }
        finally
        {
            DeleteQuietly(dbPath);
        }
    }

    [TestMethod]
    public void ExecuteMatrixComponentQuantityCommand_FingerprintDeComponentesCambiado_DevuelveConflictoSinEscribir()
    {
        var dbPath = TestDbFactory.CreateTempDbPath();
        try
        {
            int proyectoId;
            int matrizId;
            int componenteMaterialId;

            using (var context = TestDbFactory.CreateContextAt(dbPath))
            {
                var scenario = SoproCalculationScenarioBuilder.CreateBaseBudgetScenario(context);
                proyectoId = scenario.Proyecto.Id;
                matrizId = scenario.MatrizApu.Id;
                componenteMaterialId = scenario.MatrizApu.Componentes
                    .Single(c => c.TipoComponente == TipoComponenteMatriz.Material).Id;
            }

            var command = CreateCommand(dbPath, proyectoId, matrizId, componenteMaterialId, 2m);

            using (var otroModulo = new SOPROContext(dbPath))
            {
                var herramienta = otroModulo.ComponentesMatriz
                    .Single(c => c.MatrizId == matrizId && c.TipoComponente == TipoComponenteMatriz.Herramienta);
                otroModulo.ComponentesMatriz.Remove(herramienta);
                otroModulo.SaveChanges();
            }

            var result = BudgetPersistenceService.ExecuteMatrixComponentQuantityCommand(
                new ProjectDbContextFactory(), dbPath, command);

            Assert.AreEqual(BudgetOperationStatus.Conflict, result.Status, result.Message);
            Assert.IsNull(result.Confirmed);

            using var verificacion = new SOPROContext(dbPath);
            var componente = verificacion.ComponentesMatriz.AsNoTracking().Single(c => c.Id == componenteMaterialId);
            Assert.AreEqual(1m, componente.Cantidad);
        }
        finally
        {
            DeleteQuietly(dbPath);
        }
    }

    [TestMethod]
    public void ExecuteMatrixComponentQuantityCommand_RowsetDelPresupuestoCambiado_DevuelveConflictoSinEscribir()
    {
        var dbPath = TestDbFactory.CreateTempDbPath();
        try
        {
            int proyectoId;
            int matrizId;
            int componenteMaterialId;
            int hojaId;

            using (var context = TestDbFactory.CreateContextAt(dbPath))
            {
                var scenario = SoproCalculationScenarioBuilder.CreateBaseBudgetScenario(context);
                proyectoId = scenario.Proyecto.Id;
                matrizId = scenario.MatrizApu.Id;
                hojaId = scenario.Concepto.Id;
                componenteMaterialId = scenario.MatrizApu.Componentes
                    .Single(c => c.TipoComponente == TipoComponenteMatriz.Material).Id;
            }

            var command = CreateCommand(dbPath, proyectoId, matrizId, componenteMaterialId, 2m);

            using (var otroModulo = new SOPROContext(dbPath))
            {
                otroModulo.ConceptosPresupuesto.Single(c => c.Id == hojaId).Cantidad = 11m;
                otroModulo.SaveChanges();
            }

            var result = BudgetPersistenceService.ExecuteMatrixComponentQuantityCommand(
                new ProjectDbContextFactory(), dbPath, command);

            Assert.AreEqual(BudgetOperationStatus.Conflict, result.Status, result.Message);
            Assert.IsNull(result.Confirmed);

            using var verificacion = new SOPROContext(dbPath);
            var componente = verificacion.ComponentesMatriz.AsNoTracking().Single(c => c.Id == componenteMaterialId);
            Assert.AreEqual(1m, componente.Cantidad);
        }
        finally
        {
            DeleteQuietly(dbPath);
        }
    }

    [TestMethod]
    public void ExecuteMatrixComponentQuantityCommand_FalloDeTrigger_RevierteYElMismoComandoReintenta()
    {
        var dbPath = TestDbFactory.CreateTempDbPath();
        try
        {
            int proyectoId;
            int matrizId;
            int componenteMaterialId;
            int hojaId;

            using (var context = TestDbFactory.CreateContextAt(dbPath))
            {
                var scenario = SoproCalculationScenarioBuilder.CreateBaseBudgetScenario(context);
                proyectoId = scenario.Proyecto.Id;
                matrizId = scenario.MatrizApu.Id;
                hojaId = scenario.Concepto.Id;
                componenteMaterialId = scenario.MatrizApu.Componentes
                    .Single(c => c.TipoComponente == TipoComponenteMatriz.Material).Id;
            }

            var command = CreateCommand(dbPath, proyectoId, matrizId, componenteMaterialId, 2m);

            using (var admin = new SOPROContext(dbPath))
            {
                admin.Database.ExecuteSqlRaw(
                    "CREATE TRIGGER trg_fail_componente BEFORE UPDATE ON ComponentesMatriz " +
                    "BEGIN SELECT RAISE(ABORT, 'fallo_simulado'); END;");
            }

            var primerIntento = BudgetPersistenceService.ExecuteMatrixComponentQuantityCommand(
                new ProjectDbContextFactory(), dbPath, command);

            Assert.AreEqual(BudgetOperationStatus.Failed, primerIntento.Status, primerIntento.Message);
            Assert.IsNull(primerIntento.Confirmed);
            StringAssert.Contains(primerIntento.Diagnostic!, "fallo_simulado");

            using (var verificacion = new SOPROContext(dbPath))
            {
                var componente = verificacion.ComponentesMatriz.AsNoTracking().Single(c => c.Id == componenteMaterialId);
                Assert.AreEqual(1m, componente.Cantidad, "El trigger revierte TODA la escritura.");
                Assert.AreEqual(60m, componente.Importe);

                var hoja = verificacion.ConceptosPresupuesto.AsNoTracking().Single(c => c.Id == hojaId);
                Assert.AreEqual(1000m, hoja.ImporteTotal);
            }

            using (var admin = new SOPROContext(dbPath))
            {
                admin.Database.ExecuteSqlRaw("DROP TRIGGER trg_fail_componente;");
            }

            var reintento = BudgetPersistenceService.ExecuteMatrixComponentQuantityCommand(
                new ProjectDbContextFactory(), dbPath, command);

            Assert.AreEqual(BudgetOperationStatus.Committed, reintento.Status, reintento.Message);

            using var verificacionFinal = new SOPROContext(dbPath);
            var final = verificacionFinal.ComponentesMatriz.AsNoTracking().Single(c => c.Id == componenteMaterialId);
            Assert.AreEqual(2m, final.Cantidad);
            Assert.AreEqual(120m, final.Importe);

            var hojaFinal = verificacionFinal.ConceptosPresupuesto.AsNoTracking().Single(c => c.Id == hojaId);
            Assert.AreEqual(1600m, hojaFinal.ImporteTotal);
        }
        finally
        {
            DeleteQuietly(dbPath);
        }
    }

    [TestMethod]
    public void ExecuteMatrixComponentQuantityCommand_CicloDeAuxiliares_RechazaSinEscribir()
    {
        var dbPath = TestDbFactory.CreateTempDbPath();
        try
        {
            int proyectoId;
            int matrizAId;
            int componenteMaterialId;
            int hojaId;

            using (var context = TestDbFactory.CreateContextAt(dbPath))
            {
                var proyecto = AddProject(context, "Proyecto ciclo");
                proyectoId = proyecto.Id;

                var material = AddMaterial(context, proyectoId, "MAT-CICLO", 10m);

                var matrizA = AddMatrix(context, proyectoId, "BAS-A", TipoMatriz.Basico, 15m);
                var matrizB = AddMatrix(context, proyectoId, "BAS-B", TipoMatriz.Basico, 15m);

                var materialA = AddComponent(
                    context, matrizA.Id, TipoComponenteMatriz.Material, orden: 1,
                    cantidad: 1m, importe: 10m, materialId: material.Id);
                AddComponent(
                    context, matrizA.Id, TipoComponenteMatriz.Auxiliar, orden: 2,
                    cantidad: 1m, importe: 5m, auxiliarId: matrizB.Id);
                AddComponent(
                    context, matrizB.Id, TipoComponenteMatriz.Auxiliar, orden: 1,
                    cantidad: 1m, importe: 5m, auxiliarId: matrizA.Id);

                var hojaCiclo = AddLeaf(context, proyectoId, "C-CICLO", matrizA.Id, 2m, "pza", 1);
                hojaCiclo.CostoDirectoUnitario = 15m;
                hojaCiclo.CostoDirectoTotal = 30m;
                hojaCiclo.PrecioUnitario = 15m;
                hojaCiclo.ImporteTotal = 30m;

                context.SaveChanges();

                matrizAId = matrizA.Id;
                componenteMaterialId = materialA.Id;
                hojaId = hojaCiclo.Id;
            }

            var command = CreateCommand(dbPath, proyectoId, matrizAId, componenteMaterialId, 2m);

            var result = BudgetPersistenceService.ExecuteMatrixComponentQuantityCommand(
                new ProjectDbContextFactory(), dbPath, command);

            Assert.AreEqual(BudgetOperationStatus.Failed, result.Status, result.Message);
            Assert.IsNull(result.Confirmed);
            StringAssert.Contains(result.Diagnostic!, "Ciclo");

            using var verificacion = new SOPROContext(dbPath);
            var componente = verificacion.ComponentesMatriz.AsNoTracking().Single(c => c.Id == componenteMaterialId);
            Assert.AreEqual(1m, componente.Cantidad);
            Assert.AreEqual(10m, componente.Importe);

            var matriz = verificacion.Matrices.AsNoTracking().Single(m => m.Id == matrizAId);
            Assert.AreEqual(15m, matriz.CostoDirecto);

            var hoja = verificacion.ConceptosPresupuesto.AsNoTracking().Single(c => c.Id == hojaId);
            Assert.AreEqual(30m, hoja.ImporteTotal);
        }
        finally
        {
            DeleteQuietly(dbPath);
        }
    }

    // ────────────────────────────────────────────────────────────────────────
    // Infraestructura
    // ────────────────────────────────────────────────────────────────────────

    private static BudgetMatrixComponentQuantityCommand CreateCommand(
        string dbPath,
        int proyectoId,
        int matrizId,
        int componenteId,
        decimal cantidadPropuesta)
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

        return new BudgetMatrixComponentQuantityCommand(
            proyectoId,
            BudgetProjectCalculationBase.FromProyecto(proyecto),
            BudgetMatrixCalculationBase.FromMatriz(matriz),
            BudgetMatrixComponentFingerprint.FromComponentes(componentes),
            BudgetMatrixComponentQuantityBase.FromComponente(componente),
            BudgetPriceRefreshRowset.FromConceptos(filas),
            cantidadPropuesta);
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
