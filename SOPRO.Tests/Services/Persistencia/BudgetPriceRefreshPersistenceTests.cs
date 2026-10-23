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
/// Contrato del comando único de refresco de precios del presupuesto (ADR-0001):
/// intención inmutable con la base económica del proyecto y el rowset COMPLETO de
/// filas persistidas, revalidación exacta contra la base, recálculo canónico de
/// hojas (con o sin matriz), agrupadores con la política única y un solo commit.
/// </summary>
[TestClass]
public class BudgetPriceRefreshPersistenceTests
{
    [TestMethod]
    public void RefreshBudgetPrices_PrecioDeMatrizCambiado_RecalculaHojaYAgrupadorSinCompararElPrecioDeLaMatriz()
    {
        var dbPath = TestDbFactory.CreateTempDbPath();
        try
        {
            int proyectoId;
            int conceptoId;
            int agrupadorId;
            BudgetPriceRefreshCommand command;

            using (var context = TestDbFactory.CreateContextAt(dbPath))
            {
                var scenario = SoproCalculationScenarioBuilder.CreateBaseBudgetScenario(context);
                proyectoId = scenario.Proyecto.Id;
                conceptoId = scenario.Concepto.Id;

                scenario.Proyecto.PorcentajeUtilidad = 10m;
                var agrupador = AddAggregator(context, scenario.Proyecto.Id);
                context.SaveChanges();
                agrupadorId = agrupador.Id;

                command = CreateCommand(dbPath, proyectoId);

                // El precio de la matriz cambia DESPUÉS de capturar la intención: es
                // el motivo legítimo del refresco y no debe producir conflicto.
                using var otroModulo = new SOPROContext(dbPath);
                var matriz = otroModulo.Matrices.Single(m => m.Id == scenario.MatrizApu.Id);
                matriz.CostoDirecto = 250m;
                otroModulo.SaveChanges();
            }

            var result = BudgetPersistenceService.RefreshBudgetPrices(
                new ProjectDbContextFactory(), dbPath, command);

            Assert.AreEqual(BudgetOperationStatus.Committed, result.Status, result.Message);
            Assert.IsTrue(result.IsCommitted);
            Assert.IsNull(result.Diagnostic);
            Assert.IsNotNull(result.Confirmed);
            Assert.AreEqual(command.ExpectedRows.Rows.Count, result.Confirmed!.Rows.Count,
                "La instantánea contiene TODAS las filas persistidas.");

            using (var verificacion = new SOPROContext(dbPath))
            {
                var proyecto = verificacion.Proyectos.AsNoTracking().Single(p => p.Id == proyectoId);
                decimal cdUnit = BudgetPricingService.RoundImporte(proyecto, 250m);
                decimal pu = BudgetPricingService.CalculateUnitPrice(proyecto, cdUnit);
                decimal importe = BudgetPricingService.MultiplyUsingDisplayPrecision(proyecto, 10m, pu);

                var persistido = verificacion.ConceptosPresupuesto.AsNoTracking().Single(c => c.Id == conceptoId);
                Assert.AreEqual(cdUnit, persistido.CostoDirectoUnitario);
                Assert.AreEqual(10m * cdUnit, persistido.CostoDirectoTotal);
                Assert.AreEqual(pu, persistido.PrecioUnitario);
                Assert.AreEqual(importe, persistido.ImporteTotal);

                var agrupador = verificacion.ConceptosPresupuesto.AsNoTracking().Single(c => c.Id == agrupadorId);
                Assert.AreEqual(importe, agrupador.ImporteTotal,
                    "El agrupador se recalcula con la política única sobre la hoja refrescada.");
                Assert.AreEqual(importe, agrupador.CostoDirectoTotal);

                var filaConcepto = result.Confirmed.Rows.Single(r => r.ConceptId == conceptoId);
                Assert.AreEqual(pu, filaConcepto.PrecioUnitario);
                Assert.AreEqual(importe, filaConcepto.ImporteTotal);

                var filaAgrupador = result.Confirmed.Rows.Single(r => r.ConceptId == agrupadorId);
                Assert.AreEqual(importe, filaAgrupador.ImporteTotal,
                    "La instantánea incluye los agrupadores ya recalculados.");
            }
        }
        finally
        {
            DeleteQuietly(dbPath);
        }
    }

    [TestMethod]
    public void RefreshBudgetPrices_HojaSinMatriz_ConservaCostoDirectoYRecalculaPuEImporteConLosPorcentajes()
    {
        var dbPath = TestDbFactory.CreateTempDbPath();
        try
        {
            int proyectoId;
            int conceptoSinMatrizId;
            BudgetPriceRefreshCommand command;

            using (var context = TestDbFactory.CreateContextAt(dbPath))
            {
                var scenario = SoproCalculationScenarioBuilder.CreateBaseBudgetScenario(context);
                proyectoId = scenario.Proyecto.Id;

                scenario.Proyecto.PorcentajeUtilidad = 10m;
                var agrupador = AddAggregator(context, proyectoId);
                var sinMatriz = AddLeafWithoutMatrix(context, proyectoId);
                context.SaveChanges();
                conceptoSinMatrizId = sinMatriz.Id;

                command = CreateCommand(dbPath, proyectoId);
                Assert.IsTrue(command.ExpectedRows.Rows.Any(r => r.ConceptId == conceptoSinMatrizId));
                _ = agrupador;
            }

            var result = BudgetPersistenceService.RefreshBudgetPrices(
                new ProjectDbContextFactory(), dbPath, command);

            Assert.AreEqual(BudgetOperationStatus.Committed, result.Status, result.Message);

            using (var verificacion = new SOPROContext(dbPath))
            {
                var proyecto = verificacion.Proyectos.AsNoTracking().Single(p => p.Id == proyectoId);
                decimal pu = BudgetPricingService.CalculateUnitPrice(proyecto, 50m);
                decimal importe = BudgetPricingService.MultiplyUsingDisplayPrecision(proyecto, 4m, pu);

                var persistido = verificacion.ConceptosPresupuesto
                    .AsNoTracking().Single(c => c.Id == conceptoSinMatrizId);

                Assert.AreEqual(50m, persistido.CostoDirectoUnitario,
                    "Sin matriz no hay costo nuevo: se conserva el costo directo persistido.");
                Assert.AreEqual(200m, persistido.CostoDirectoTotal);
                Assert.AreEqual(pu, persistido.PrecioUnitario,
                    "El P.U. se reexpresa con los porcentajes vigentes del proyecto.");
                Assert.AreEqual(importe, persistido.ImporteTotal);
            }
        }
        finally
        {
            DeleteQuietly(dbPath);
        }
    }

    [TestMethod]
    public void RefreshBudgetPrices_JerarquiaObsoleta_DevuelveConflictoSinEscribir()
    {
        var dbPath = TestDbFactory.CreateTempDbPath();
        try
        {
            int conceptoId;
            BudgetPriceRefreshCommand command;

            using (var context = TestDbFactory.CreateContextAt(dbPath))
            {
                var scenario = SoproCalculationScenarioBuilder.CreateBaseBudgetScenario(context);
                conceptoId = scenario.Concepto.Id;
                AddAggregator(context, scenario.Proyecto.Id);
                context.SaveChanges();
                command = CreateCommand(dbPath, scenario.Proyecto.Id);
            }

            using (var otroModulo = new SOPROContext(dbPath))
            {
                var concepto = otroModulo.ConceptosPresupuesto.Single(c => c.Id == conceptoId);
                concepto.Nivel = 2;
                otroModulo.SaveChanges();
            }

            var result = BudgetPersistenceService.RefreshBudgetPrices(
                new ProjectDbContextFactory(), dbPath, command);

            Assert.AreEqual(BudgetOperationStatus.Conflict, result.Status, result.Message);
            Assert.IsNull(result.Confirmed);

            using var verificacion = new SOPROContext(dbPath);
            var persistido = verificacion.ConceptosPresupuesto.AsNoTracking().Single(c => c.Id == conceptoId);
            Assert.AreEqual(2, persistido.Nivel, "El conflicto no revierte el cambio jerárquico ajeno.");
            Assert.AreEqual(100m, persistido.PrecioUnitario, "El conflicto no escribe ningún precio.");
        }
        finally
        {
            DeleteQuietly(dbPath);
        }
    }

    [TestMethod]
    public void RefreshBudgetPrices_RowsetCambiado_DevuelveConflictoSinEscribir()
    {
        var dbPath = TestDbFactory.CreateTempDbPath();
        try
        {
            BudgetPriceRefreshCommand command;
            int proyectoId;

            using (var context = TestDbFactory.CreateContextAt(dbPath))
            {
                var scenario = SoproCalculationScenarioBuilder.CreateBaseBudgetScenario(context);
                proyectoId = scenario.Proyecto.Id;
                AddAggregator(context, proyectoId);
                context.SaveChanges();
                command = CreateCommand(dbPath, proyectoId);
            }

            using (var otroModulo = new SOPROContext(dbPath))
            {
                otroModulo.ConceptosPresupuesto.Add(new ConceptoPresupuesto
                {
                    ProyectoId = proyectoId,
                    Clave = "C-EXTRA",
                    Descripcion = "Fila agregada por otra ruta",
                    Unidad = "pza",
                    Cantidad = 1m,
                    MatrizId = null,
                    CostoDirectoUnitario = 1m,
                    CostoDirectoTotal = 1m,
                    PrecioUnitario = 1m,
                    ImporteTotal = 1m,
                    Nivel = 1,
                    Orden = 99,
                    EsAgrupador = false,
                    ColumnasPersonalizadasJSON = string.Empty,
                    Notas = string.Empty
                });
                otroModulo.SaveChanges();
            }

            var result = BudgetPersistenceService.RefreshBudgetPrices(
                new ProjectDbContextFactory(), dbPath, command);

            Assert.AreEqual(BudgetOperationStatus.Conflict, result.Status, result.Message);

            using var verificacion = new SOPROContext(dbPath);
            Assert.AreEqual(1m, verificacion.ConceptosPresupuesto.AsNoTracking()
                .Single(c => c.Clave == "C-EXTRA").PrecioUnitario);
            Assert.AreEqual(100m, verificacion.ConceptosPresupuesto.AsNoTracking()
                .Single(c => c.Clave == "C-001").PrecioUnitario);
        }
        finally
        {
            DeleteQuietly(dbPath);
        }
    }

    [TestMethod]
    public void RefreshBudgetPrices_ActualizacionYaPropagadaEnLaBase_DevuelveConflictoYNoLaPisa()
    {
        var dbPath = TestDbFactory.CreateTempDbPath();
        try
        {
            int conceptoId;
            BudgetPriceRefreshCommand command;

            using (var context = TestDbFactory.CreateContextAt(dbPath))
            {
                var scenario = SoproCalculationScenarioBuilder.CreateBaseBudgetScenario(context);
                conceptoId = scenario.Concepto.Id;
                AddAggregator(context, scenario.Proyecto.Id);
                context.SaveChanges();
                command = CreateCommand(dbPath, scenario.Proyecto.Id);
            }

            // Otra ruta ya propagó y guardó el nuevo precio del concepto.
            using (var otroModulo = new SOPROContext(dbPath))
            {
                var concepto = otroModulo.ConceptosPresupuesto.Single(c => c.Id == conceptoId);
                concepto.PrecioUnitario = 175m;
                concepto.ImporteTotal = 1750m;
                otroModulo.SaveChanges();
            }

            var result = BudgetPersistenceService.RefreshBudgetPrices(
                new ProjectDbContextFactory(), dbPath, command);

            Assert.AreEqual(BudgetOperationStatus.Conflict, result.Status, result.Message);

            using var verificacion = new SOPROContext(dbPath);
            var persistido = verificacion.ConceptosPresupuesto.AsNoTracking().Single(c => c.Id == conceptoId);
            Assert.AreEqual(175m, persistido.PrecioUnitario,
                "El comando no puede pisar una actualización ya propagada.");
            Assert.AreEqual(1750m, persistido.ImporteTotal);
        }
        finally
        {
            DeleteQuietly(dbPath);
        }
    }

    [TestMethod]
    public void RefreshBudgetPrices_MatrizInexistente_DevuelveConflictoSinEscribir()
    {
        var dbPath = TestDbFactory.CreateTempDbPath();
        try
        {
            int proyectoId;
            int matrizExtraId;
            int conceptoId;
            BudgetPriceRefreshCommand command;

            using (var context = TestDbFactory.CreateContextAt(dbPath))
            {
                var scenario = SoproCalculationScenarioBuilder.CreateBaseBudgetScenario(context);
                proyectoId = scenario.Proyecto.Id;

                var matrizExtra = AddMatrix(context, proyectoId, "APU-EXTRA", 80m);
                matrizExtraId = matrizExtra.Id;
                var hoja = AddLeaf(context, proyectoId, "C-EXTRA", matrizExtraId, 2m, 80m);
                context.SaveChanges();
                conceptoId = hoja.Id;
                command = CreateCommand(dbPath, proyectoId);
            }

            using (var otroModulo = new SOPROContext(dbPath))
            {
                otroModulo.Database.OpenConnection();
                otroModulo.Database.ExecuteSqlRaw("PRAGMA foreign_keys = OFF;");
                var matriz = otroModulo.Matrices.Single(m => m.Id == matrizExtraId);
                otroModulo.Matrices.Remove(matriz);
                otroModulo.SaveChanges();
                otroModulo.Database.CloseConnection();
            }

            var result = BudgetPersistenceService.RefreshBudgetPrices(
                new ProjectDbContextFactory(), dbPath, command);

            Assert.AreEqual(BudgetOperationStatus.Conflict, result.Status, result.Message);
            StringAssert.Contains(result.Message!, "matriz");

            using var verificacion = new SOPROContext(dbPath);
            var persistido = verificacion.ConceptosPresupuesto.AsNoTracking().Single(c => c.Id == conceptoId);
            Assert.AreEqual(80m, persistido.PrecioUnitario);
            Assert.AreEqual(160m, persistido.ImporteTotal);
        }
        finally
        {
            DeleteQuietly(dbPath);
        }
    }

    [TestMethod]
    public void RefreshBudgetPrices_MatrizDeOtroProyecto_DevuelveConflictoSinEscribir()
    {
        var dbPath = TestDbFactory.CreateTempDbPath();
        try
        {
            int conceptoId;
            BudgetPriceRefreshCommand command;

            using (var context = TestDbFactory.CreateContextAt(dbPath))
            {
                var scenario = SoproCalculationScenarioBuilder.CreateBaseBudgetScenario(context);

                var otroProyecto = AddProject(context, "Proyecto ajeno");
                var matrizAjena = AddMatrix(context, otroProyecto.Id, "APU-AJENA", 500m);
                var hoja = AddLeaf(context, scenario.Proyecto.Id, "C-AJENA", matrizAjena.Id, 1m, 500m);
                context.SaveChanges();
                conceptoId = hoja.Id;

                command = CreateCommand(dbPath, scenario.Proyecto.Id);
            }

            var result = BudgetPersistenceService.RefreshBudgetPrices(
                new ProjectDbContextFactory(), dbPath, command);

            Assert.AreEqual(BudgetOperationStatus.Conflict, result.Status, result.Message);
            Assert.IsNull(result.Confirmed);

            using var verificacion = new SOPROContext(dbPath);
            var persistido = verificacion.ConceptosPresupuesto.AsNoTracking().Single(c => c.Id == conceptoId);
            Assert.AreEqual(500m, persistido.PrecioUnitario);
            Assert.AreEqual(500m, persistido.ImporteTotal);
        }
        finally
        {
            DeleteQuietly(dbPath);
        }
    }

    [TestMethod]
    public void RefreshBudgetPrices_FalloDeTrigger_RevierteTodoYElReintentoConElMismoComandoConfirma()
    {
        var dbPath = TestDbFactory.CreateTempDbPath();
        try
        {
            int proyectoId;
            int conceptoId;
            BudgetPriceRefreshCommand command;

            using (var context = TestDbFactory.CreateContextAt(dbPath))
            {
                var scenario = SoproCalculationScenarioBuilder.CreateBaseBudgetScenario(context);
                proyectoId = scenario.Proyecto.Id;
                conceptoId = scenario.Concepto.Id;
                AddAggregator(context, proyectoId);
                context.SaveChanges();
                command = CreateCommand(dbPath, proyectoId);

                using var otroModulo = new SOPROContext(dbPath);
                otroModulo.Matrices.Single(m => m.Id == scenario.MatrizApu.Id).CostoDirecto = 300m;
                otroModulo.SaveChanges();
            }

            using (var admin = new SOPROContext(dbPath))
            {
                admin.Database.ExecuteSqlRaw(
                    "CREATE TRIGGER trg_fail_concepto BEFORE UPDATE ON ConceptosPresupuesto " +
                    "BEGIN SELECT RAISE(ABORT, 'fallo_simulado'); END;");
            }

            var primerIntento = BudgetPersistenceService.RefreshBudgetPrices(
                new ProjectDbContextFactory(), dbPath, command);

            Assert.AreEqual(BudgetOperationStatus.Failed, primerIntento.Status, primerIntento.Message);
            Assert.IsNull(primerIntento.Confirmed);
            StringAssert.Contains(primerIntento.Diagnostic!, "fallo_simulado");

            using (var verificacion = new SOPROContext(dbPath))
            {
                var persistido = verificacion.ConceptosPresupuesto.AsNoTracking().Single(c => c.Id == conceptoId);
                Assert.AreEqual(100m, persistido.PrecioUnitario, "El trigger revierte TODA la escritura.");
                Assert.AreEqual(1000m, persistido.ImporteTotal);
            }

            using (var admin = new SOPROContext(dbPath))
            {
                admin.Database.ExecuteSqlRaw("DROP TRIGGER trg_fail_concepto;");
            }

            var reintento = BudgetPersistenceService.RefreshBudgetPrices(
                new ProjectDbContextFactory(), dbPath, command);

            Assert.AreEqual(BudgetOperationStatus.Committed, reintento.Status, reintento.Message);

            using var verificacionFinal = new SOPROContext(dbPath);
            var final = verificacionFinal.ConceptosPresupuesto.AsNoTracking().Single(c => c.Id == conceptoId);
            Assert.AreEqual(300m, final.CostoDirectoUnitario);
            Assert.AreEqual(3000m, final.ImporteTotal);
        }
        finally
        {
            DeleteQuietly(dbPath);
        }
    }

    [TestMethod]
    public void RefreshBudgetPrices_PorcentajesYDecimales_SeAplicanConElMotorDelProyecto()
    {
        var dbPath = TestDbFactory.CreateTempDbPath();
        try
        {
            int proyectoId;
            int conceptoId;
            BudgetPriceRefreshCommand command;

            using (var context = TestDbFactory.CreateContextAt(dbPath))
            {
                var scenario = SoproCalculationScenarioBuilder.CreateBaseBudgetScenario(context);
                proyectoId = scenario.Proyecto.Id;
                conceptoId = scenario.Concepto.Id;

                scenario.Proyecto.PorcentajeIndirectosCentral = 5m;
                scenario.Proyecto.PorcentajeUtilidad = 10m;
                scenario.Proyecto.DecimalesImporte = 0;
                AddAggregator(context, proyectoId);
                context.SaveChanges();

                command = CreateCommand(dbPath, proyectoId);

                using var otroModulo = new SOPROContext(dbPath);
                otroModulo.Matrices.Single(m => m.Id == scenario.MatrizApu.Id).CostoDirecto = 250.4m;
                otroModulo.SaveChanges();
            }

            var result = BudgetPersistenceService.RefreshBudgetPrices(
                new ProjectDbContextFactory(), dbPath, command);

            Assert.AreEqual(BudgetOperationStatus.Committed, result.Status, result.Message);

            using var verificacion = new SOPROContext(dbPath);
            var proyecto = verificacion.Proyectos.AsNoTracking().Single(p => p.Id == proyectoId);
            decimal cdUnit = BudgetPricingService.RoundImporte(proyecto, 250.4m);
            decimal pu = BudgetPricingService.CalculateUnitPrice(proyecto, cdUnit);
            decimal importe = BudgetPricingService.MultiplyUsingDisplayPrecision(proyecto, 10m, pu);

            var persistido = verificacion.ConceptosPresupuesto.AsNoTracking().Single(c => c.Id == conceptoId);
            Assert.AreEqual(250m, cdUnit, "Los decimales de importe del proyecto redondean el CD unitario.");
            Assert.AreEqual(cdUnit, persistido.CostoDirectoUnitario);
            Assert.AreEqual(pu, persistido.PrecioUnitario);
            Assert.AreEqual(importe, persistido.ImporteTotal);
        }
        finally
        {
            DeleteQuietly(dbPath);
        }
    }

    [TestMethod]
    public void RefreshBudgetPrices_ProyectoCambioSuBaseDeCalculo_DevuelveConflictoSinEscribir()
    {
        var dbPath = TestDbFactory.CreateTempDbPath();
        try
        {
            int conceptoId;
            BudgetPriceRefreshCommand command;

            using (var context = TestDbFactory.CreateContextAt(dbPath))
            {
                var scenario = SoproCalculationScenarioBuilder.CreateBaseBudgetScenario(context);
                conceptoId = scenario.Concepto.Id;
                AddAggregator(context, scenario.Proyecto.Id);
                context.SaveChanges();
                command = CreateCommand(dbPath, scenario.Proyecto.Id);
            }

            using (var otroModulo = new SOPROContext(dbPath))
            {
                otroModulo.Proyectos.Single(p => p.Id == command.ProjectId).PorcentajeUtilidad = 25m;
                otroModulo.SaveChanges();
            }

            var result = BudgetPersistenceService.RefreshBudgetPrices(
                new ProjectDbContextFactory(), dbPath, command);

            Assert.AreEqual(BudgetOperationStatus.Conflict, result.Status, result.Message);

            using var verificacion = new SOPROContext(dbPath);
            var persistido = verificacion.ConceptosPresupuesto.AsNoTracking().Single(c => c.Id == conceptoId);
            Assert.AreEqual(100m, persistido.PrecioUnitario);
            Assert.AreEqual(1000m, persistido.ImporteTotal);
        }
        finally
        {
            DeleteQuietly(dbPath);
        }
    }

    // ────────────────────────────────────────────────────────────────────────
    // Infraestructura
    // ────────────────────────────────────────────────────────────────────────

    private static BudgetPriceRefreshCommand CreateCommand(string dbPath, int proyectoId)
    {
        using var context = new SOPROContext(dbPath);
        var proyecto = context.Proyectos.AsNoTracking().Single(p => p.Id == proyectoId);
        var filas = context.ConceptosPresupuesto
            .AsNoTracking()
            .Where(c => c.ProyectoId == proyectoId)
            .OrderBy(c => c.Orden)
            .ThenBy(c => c.Id)
            .ToList();

        return new BudgetPriceRefreshCommand(
            proyectoId,
            BudgetProjectCalculationBase.FromProyecto(proyecto),
            BudgetPriceRefreshRowset.FromConceptos(filas));
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

    private static ConceptoPresupuesto AddLeafWithoutMatrix(SOPROContext context, int proyectoId)
    {
        var hoja = new ConceptoPresupuesto
        {
            ProyectoId = proyectoId,
            Clave = "C-SIN-MATRIZ",
            Descripcion = "Concepto sin matriz",
            Unidad = "pza",
            Cantidad = 4m,
            MatrizId = null,
            CostoDirectoUnitario = 50m,
            CostoDirectoTotal = 200m,
            PrecioUnitario = 50m,
            ImporteTotal = 200m,
            Nivel = 1,
            Orden = 2,
            EsAgrupador = false,
            ColumnasPersonalizadasJSON = string.Empty,
            Notas = string.Empty
        };

        context.ConceptosPresupuesto.Add(hoja);
        return hoja;
    }

    private static ConceptoPresupuesto AddLeaf(
        SOPROContext context,
        int proyectoId,
        string clave,
        int matrizId,
        decimal cantidad,
        decimal costoDirecto)
    {
        var hoja = new ConceptoPresupuesto
        {
            ProyectoId = proyectoId,
            Clave = clave,
            Descripcion = "Concepto de prueba",
            Unidad = "pza",
            Cantidad = cantidad,
            MatrizId = matrizId,
            CostoDirectoUnitario = costoDirecto,
            CostoDirectoTotal = cantidad * costoDirecto,
            PrecioUnitario = costoDirecto,
            ImporteTotal = cantidad * costoDirecto,
            Nivel = 1,
            Orden = 2,
            EsAgrupador = false,
            ColumnasPersonalizadasJSON = string.Empty,
            Notas = string.Empty
        };

        context.ConceptosPresupuesto.Add(hoja);
        return hoja;
    }

    private static Matriz AddMatrix(SOPROContext context, int? proyectoId, string clave, decimal costoDirecto)
    {
        var matriz = new Matriz
        {
            ProyectoId = proyectoId,
            Clave = clave,
            Descripcion = "Matriz de prueba",
            Unidad = "pza",
            Tipo = TipoMatriz.APU,
            CostoDirecto = costoDirecto,
            Notas = string.Empty
        };

        context.Matrices.Add(matriz);
        context.SaveChanges();
        return matriz;
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
