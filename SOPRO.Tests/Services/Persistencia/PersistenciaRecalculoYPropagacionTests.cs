using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SOPRO.Application.Services;
using SOPRO.Core.Entities;
using SOPRO.Data.Context;
using SOPRO.Tests.TestInfrastructure;

namespace SOPRO.Tests.Services.Persistencia;

[TestClass]
public class PersistenciaRecalculoYPropagacionTests
{
    [TestMethod]
    public void RecalculoGlobal_PersisteCambios_AlReabrirLaBaseDatos()
    {
        var dbPath = TestDbFactory.CreateTempDbPath();
        try
        {
            int proyectoId;
            int matrizId;
            int conceptoId;

            using (var context = TestDbFactory.CreateContextAt(dbPath))
            {
                var scenario = SoproCalculationScenarioBuilder.CreateBaseBudgetScenario(context);
                SoproCalculationScenarioBuilder.AddTwoPeriodProgram(context, scenario);
                proyectoId = scenario.Proyecto.Id;
                matrizId = scenario.MatrizApu.Id;
                conceptoId = scenario.Concepto.Id;

                // Corromper ANTES del recálculo: importes de componentes, C.D. de la
                // matriz y valores económicos del concepto. Si el recálculo no
                // restaurara el valor exacto de cada fase, la corrupción se
                // persistiría y la reapertura lo revelaría.
                var componentes = context.ComponentesMatriz
                    .Where(c => c.MatrizId == matrizId)
                    .ToList();
                foreach (var comp in componentes)
                    comp.Importe = 999999m;
                scenario.MatrizApu.CostoDirecto = 999999m;

                scenario.Concepto.CostoDirectoUnitario = 999999m;
                scenario.Concepto.CostoDirectoTotal = 999999m;
                scenario.Concepto.PrecioUnitario = 999999m;
                scenario.Concepto.ImporteTotal = 999999m;
                context.SaveChanges();

                new RecalculoGlobalService().Ejecutar(context, proyectoId);
            }

            using (var context = new SOPROContext(dbPath))
            {
                var matriz = context.Matrices.AsNoTracking().Single(m => m.Id == matrizId);
                var concepto = context.ConceptosPresupuesto.AsNoTracking().Single(c => c.Id == conceptoId);
                var importesComponentes = context.ComponentesMatriz
                    .AsNoTracking()
                    .Where(c => c.MatrizId == matrizId)
                    .OrderBy(c => c.Orden)
                    .Select(c => c.Importe)
                    .ToList();
                var distribuciones = context.DistribucionesPeriodo
                    .AsNoTracking()
                    .Where(d => d.ActividadProgramada.ProgramaObra.ProyectoId == proyectoId)
                    .OrderBy(d => d.PeriodoProgramaId)
                    .ToList();

                // Valores exactos restaurados por fase y persistidos en disco.
                CollectionAssert.AreEqual(new[] { 60.00m, 30.00m, 3.00m, 3.00m, 4.00m },
                    importesComponentes, "Fase 1: importes de componentes restaurados y persistidos.");
                Assert.AreEqual(100.00m, matriz.CostoDirecto, "Fase 2: C.D. de la matriz restaurado y persistido.");
                Assert.AreEqual(100.00m, concepto.CostoDirectoUnitario, "Fase 3: C.D.U. restaurado y persistido.");
                Assert.AreEqual(1000.00m, concepto.CostoDirectoTotal, "Fase 3: C.D.T. restaurado y persistido.");
                Assert.AreEqual(100.00m, concepto.PrecioUnitario, "Fase 3: P.U. restaurado y persistido.");
                Assert.AreEqual(1000.00m, concepto.ImporteTotal, "Fase 3: importe total restaurado y persistido.");
                Assert.AreEqual(2, distribuciones.Count);
                Assert.AreEqual(1000.00m, distribuciones.Sum(d => d.ImporteProgramado));
                Assert.AreEqual(500.00m, distribuciones[0].ImporteProgramado, "Distribución uniforme persistida (P1).");
                Assert.AreEqual(500.00m, distribuciones[1].ImporteProgramado, "Distribución uniforme persistida (P2).");
            }
        }
        finally
        {
            DeleteQuietly(dbPath);
        }
    }

    [TestMethod]
    public void PropagacionMaterial_PersisteCambios_AlReabrirLaBaseDatos()
    {
        var dbPath = TestDbFactory.CreateTempDbPath();
        try
        {
            int materialId;
            int matrizId;
            int conceptoId;

            using (var context = TestDbFactory.CreateContextAt(dbPath))
            {
                var scenario = SoproCalculationScenarioBuilder.CreateBaseBudgetScenario(context);
                materialId = scenario.Cemento.Id;
                matrizId = scenario.MatrizApu.Id;
                conceptoId = scenario.Concepto.Id;

                scenario.Cemento.PrecioUnitario = 70m;
                context.SaveChanges();
                PricePropagationService.PropagarMaterial(context, materialId);
            }

            using (var context = new SOPROContext(dbPath))
            {
                var matriz = context.Matrices.AsNoTracking().Single(m => m.Id == matrizId);
                var concepto = context.ConceptosPresupuesto.AsNoTracking().Single(c => c.Id == conceptoId);

                Assert.AreEqual(110.00m, matriz.CostoDirecto, "La propagación debe persistir en la matriz.");
                Assert.AreEqual(110.00m, concepto.CostoDirectoUnitario);
                Assert.AreEqual(1100.00m, concepto.CostoDirectoTotal);
            }
        }
        finally
        {
            DeleteQuietly(dbPath);
        }
    }

    [TestMethod]
    public void PropagacionManoDeObra_PersisteCambios_AlReabrirLaBaseDatos()
    {
        var dbPath = TestDbFactory.CreateTempDbPath();
        try
        {
            int manoObraId;
            int matrizId;
            int conceptoId;

            using (var context = TestDbFactory.CreateContextAt(dbPath))
            {
                var scenario = SoproCalculationScenarioBuilder.CreateBaseBudgetScenario(context);
                manoObraId = scenario.Oficial.Id;
                matrizId = scenario.MatrizApu.Id;
                conceptoId = scenario.Concepto.Id;

                scenario.Oficial.SalarioReal = 40m;
                context.SaveChanges();
                PricePropagationService.PropagarManoDeObra(context, manoObraId);
            }

            using (var context = new SOPROContext(dbPath))
            {
                var matriz = context.Matrices.AsNoTracking().Single(m => m.Id == matrizId);
                var concepto = context.ConceptosPresupuesto.AsNoTracking().Single(c => c.Id == conceptoId);

                Assert.AreEqual(112.00m, matriz.CostoDirecto, "La propagación MO debe persistir en la matriz.");
                Assert.AreEqual(112.00m, concepto.CostoDirectoUnitario);
                Assert.AreEqual(1120.00m, concepto.CostoDirectoTotal);
            }
        }
        finally
        {
            DeleteQuietly(dbPath);
        }
    }

    [TestMethod]
    public void RecalculoGlobal_RecalculaAgrupadoresPorOrdenYNivel_AlReabrirLaBaseDatos()
    {
        var dbPath = TestDbFactory.CreateTempDbPath();
        try
        {
            int proyectoId;

            using (var context = TestDbFactory.CreateContextAt(dbPath))
            {
                var scenario = SoproCalculationScenarioBuilder.CreateBaseBudgetScenario(context);

                // Utilidad 10 %: Importe de la hoja (1,100) ≠ Costo Directo (1,000).
                scenario.Proyecto.PorcentajeUtilidad = 10m;
                scenario.Concepto.Nivel = 1; // Nivel legacy de una hoja: semánticamente 5.
                scenario.Concepto.Orden = 3;
                context.SaveChanges();

                context.ConceptosPresupuesto.AddRange(
                    CrearAgrupador(scenario.Proyecto.Id, "CAP-01", "Capítulo", nivel: 0, orden: 1),
                    CrearAgrupador(scenario.Proyecto.Id, "SUB-01", "Subcapítulo", nivel: 1, orden: 2),
                    CrearAgrupador(scenario.Proyecto.Id, "CAP-VACIO", "Capítulo vacío", nivel: 1, orden: 4));
                context.SaveChanges();

                proyectoId = scenario.Proyecto.Id;

                var resultado = new RecalculoGlobalService().Ejecutar(context, proyectoId);
                Assert.AreEqual(3, resultado.AgrupadoresTotalesRecalculados);
            }

            using (var context = new SOPROContext(dbPath))
            {
                var conceptos = context.ConceptosPresupuesto
                    .AsNoTracking()
                    .Where(c => c.ProyectoId == proyectoId)
                    .ToDictionary(c => c.Clave);

                Assert.AreEqual(1000.00m, conceptos["C-001"].CostoDirectoTotal, "C.D.T. de la hoja persistido.");
                Assert.AreEqual(1100.00m, conceptos["C-001"].ImporteTotal, "Importe de la hoja persistido.");

                Assert.AreEqual(1100.00m, conceptos["CAP-01"].CostoDirectoTotal, "El capítulo suma el Importe de la hoja.");
                Assert.AreEqual(1100.00m, conceptos["CAP-01"].ImporteTotal, "Ambos totales del capítulo coinciden.");
                Assert.AreEqual(1100.00m, conceptos["SUB-01"].CostoDirectoTotal, "El subcapítulo suma el Importe de la hoja.");
                Assert.AreEqual(1100.00m, conceptos["SUB-01"].ImporteTotal, "Ambos totales del subcapítulo coinciden.");
                Assert.AreEqual(0m, conceptos["CAP-VACIO"].CostoDirectoTotal, "Un agrupador sin hojas queda en cero.");
                Assert.AreEqual(0m, conceptos["CAP-VACIO"].ImporteTotal, "Un agrupador sin hojas queda en cero.");
            }
        }
        finally
        {
            DeleteQuietly(dbPath);
        }
    }

    private static ConceptoPresupuesto CrearAgrupador(int proyectoId, string clave, string descripcion, int nivel, int orden)
        => new()
        {
            ProyectoId = proyectoId,
            Clave = clave,
            Descripcion = descripcion,
            Unidad = string.Empty,
            Cantidad = 0m,
            Nivel = nivel,
            Orden = orden,
            EsAgrupador = true,
            CostoDirectoTotal = 999999m,
            ImporteTotal = 999999m,
            ColumnasPersonalizadasJSON = string.Empty,
            Notas = string.Empty
        };

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
