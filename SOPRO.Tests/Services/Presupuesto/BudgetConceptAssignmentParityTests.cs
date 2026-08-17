using Microsoft.VisualStudio.TestTools.UnitTesting;
using SOPRO.Application.Models.Presupuesto;
using SOPRO.Application.Services;
using SOPRO.Core.Entities;
using SOPRO.Tests.TestInfrastructure;

namespace SOPRO.Tests.Services.Presupuesto;

[TestClass]
public class BudgetConceptAssignmentParityTests
{
    // ── N5-4: BuildDraftFromMatrix y BuildDraftFromConcept migraron de
    //    MotorCalculoSopro a SoproCalculationEngine. Paridad exacta contra
    //    referencias compuestas con las primitivas de la fachada (oráculo
    //    diferencial) + dorados de los drafts.

    [TestMethod]
    public void BuildDraftFromSelectedMatrix_ParidadConFachada_BateriaAleatoriaConSemilla()
    {
        var rnd = new Random(20260817);

        for (int iter = 0; iter < 500; iter++)
        {
            var proyecto = CrearProyectoAleatorio(rnd);
            var matriz = new Matriz
            {
                Id = iter + 1,
                Clave = $"APU-{iter}",
                Descripcion = "Matriz de prueba",
                Unidad = "pza",
                Tipo = TipoMatriz.APU,
                CostoDirecto = ValorPrecio(rnd)
            };
            var cantidad = ValorCantidad(rnd);

            var servicio = BudgetConceptAssignmentService.BuildDraftFromSelectedMatrix(
                proyecto, matriz, cantidad, null, null, null);
            var referencia = BuildDraftFromMatrixConFachada(proyecto, matriz, cantidad);

            CompararDrafts(servicio, referencia, $"iter={iter}");
        }
    }

    [TestMethod]
    public void ResolveByKey_CopiaDesdeGrid_ParidadConFachada_BateriaAleatoriaConSemilla()
    {
        var rnd = new Random(20260817);

        for (int iter = 0; iter < 300; iter++)
        {
            using var context = TestDbFactory.CreateContext();
            var proyecto = CrearProyectoAleatorio(rnd);
            var sourceConcept = new ConceptoPresupuesto
            {
                Clave = "ORIGEN-001",
                Descripcion = "Concepto origen",
                Unidad = "pza",
                Cantidad = ValorCantidad(rnd),
                CostoDirectoUnitario = ValorPrecio(rnd),
                MatrizId = iter + 1
            };

            var rows = new List<BudgetConceptKeyRowSnapshot>
            {
                new()
                {
                    RowIndex = 5,
                    Key = "DESTINO-001",
                    Description = null,
                    Concept = sourceConcept
                }
            };

            var servicio = BudgetConceptAssignmentService.ResolveByKey(
                context, proyecto, currentRowIndex: 0, key: "DESTINO-001",
                existingConcept: null, currentQuantityText: "1", rows: rows);
            var referencia = BuildDraftFromConceptConFachada(proyecto, sourceConcept);

            Assert.IsTrue(servicio.HasAssignment, $"iter={iter} sin asignación");
            Assert.IsFalse(servicio.RequiresConfirmation, $"iter={iter} confirmación inesperada");
            CompararDrafts(servicio.Draft, referencia, $"iter={iter}");
        }
    }

    [TestMethod]
    public void BuildDraftFromSelectedMatrix_Dorado_MaterialConPrecioVisible()
    {
        var proyecto = CrearProyectoConPorcentajes(2, 2, 4);
        var matriz = new Matriz
        {
            Clave = "APU-001",
            Descripcion = "Concreto",
            Unidad = "m3",
            Tipo = TipoMatriz.APU,
            CostoDirecto = 13.3875m
        };

        var draft = BudgetConceptAssignmentService.BuildDraftFromSelectedMatrix(
            proyecto, matriz, 652m, "CLAVE-1", null, null);

        // cdUnit visible 13.39; PU con cascada 5/5/2/8/3: 16.71;
        // cdTotal 652×13.39 = 8730.28; importe 652×16.71 = 10894.92
        Assert.AreEqual("CLAVE-1", draft.Clave);
        Assert.AreEqual(13.39m, draft.CostoDirectoUnitario);
        Assert.AreEqual(8730.28m, draft.CostoDirectoTotal);
        Assert.AreEqual(16.71m, draft.PrecioUnitario);
        Assert.AreEqual(10894.92m, draft.ImporteTotal);
    }

    [TestMethod]
    public void ResolveByKey_CopiaDesdeGrid_Dorado()
    {
        using var context = TestDbFactory.CreateContext();
        var proyecto = CrearProyectoConPorcentajes(2, 2, 4);
        var sourceConcept = new ConceptoPresupuesto
        {
            Clave = "ORIGEN-001",
            Descripcion = "Concepto origen",
            Unidad = "pza",
            Cantidad = 10m,
            CostoDirectoUnitario = 100m,
            MatrizId = 42
        };

        var rows = new List<BudgetConceptKeyRowSnapshot>
        {
            new() { RowIndex = 5, Key = "DESTINO-001", Description = null, Concept = sourceConcept }
        };

        var result = BudgetConceptAssignmentService.ResolveByKey(
            context, proyecto, 0, "DESTINO-001", null, "1", rows);

        // cdUnit 100.00; PU con cascada 5/5/2/8/3: 124.82;
        // cdTotal 10×100 = 1000.00; importe 10×124.82 = 1248.20
        Assert.IsTrue(result.HasAssignment);
        Assert.AreEqual(5, result.SourceRowIndex);
        Assert.AreSame(sourceConcept, result.SourceConcept);
        Assert.AreEqual(100.00m, result.Draft.CostoDirectoUnitario);
        Assert.AreEqual(1000.00m, result.Draft.CostoDirectoTotal);
        Assert.AreEqual(124.82m, result.Draft.PrecioUnitario);
        Assert.AreEqual(1248.20m, result.Draft.ImporteTotal);
        Assert.AreEqual(42, result.Draft.MatrizId);
    }

    [TestMethod]
    public void ResolveByKey_CantidadInvalida_DebeUsarCantidadPorDefectoUno()
    {
        using var context = TestDbFactory.CreateContext();
        var proyecto = CrearProyectoConPorcentajes(2, 2, 4);
        context.Proyectos.Add(proyecto);
        context.SaveChanges();

        var matriz = new Matriz
        {
            ProyectoId = proyecto.Id,
            Clave = "APU-001",
            Descripcion = "Concreto",
            Unidad = "m3",
            Tipo = TipoMatriz.APU,
            CostoDirecto = 100m
        };
        context.Matrices.Add(matriz);
        context.SaveChanges();

        var result = BudgetConceptAssignmentService.ResolveByKey(
            context, proyecto, 0, "APU-001", null, "abc", Array.Empty<BudgetConceptKeyRowSnapshot>());

        Assert.IsTrue(result.HasAssignment);
        Assert.AreEqual(1m, result.Draft.Cantidad);
        Assert.AreEqual(100.00m, result.Draft.CostoDirectoTotal);
    }

    // ── Referencias compuestas con la fachada (mismo algoritmo, primitivas legacy) ──

    private static BudgetConceptAssignmentDraft BuildDraftFromMatrixConFachada(
        Proyecto proyecto, Matriz matriz, decimal cantidad)
    {
        var motor      = new MotorCalculoSopro(proyecto);
        decimal cdUnit = motor.RedondearImporte(matriz.CostoDirecto);
        decimal pu     = motor.CalcularPrecioUnitario(cdUnit, CrearInputDeProyecto(proyecto)).PrecioUnitario;
        decimal cdTotal = motor.Multiplicar(cantidad, cdUnit);
        decimal importe = motor.Multiplicar(cantidad, pu);

        return new BudgetConceptAssignmentDraft
        {
            Clave                = matriz.Clave ?? string.Empty,
            Descripcion          = matriz.Descripcion ?? string.Empty,
            Unidad               = matriz.Unidad ?? string.Empty,
            Cantidad             = cantidad,
            CostoDirectoUnitario = cdUnit,
            CostoDirectoTotal    = cdTotal,
            PrecioUnitario       = pu,
            ImporteTotal         = importe,
            MatrizId             = matriz.Id,
            Matriz               = matriz
        };
    }

    private static BudgetConceptAssignmentDraft BuildDraftFromConceptConFachada(
        Proyecto proyecto, ConceptoPresupuesto sourceConcept)
    {
        var motor      = new MotorCalculoSopro(proyecto);
        decimal cdUnit = motor.RedondearImporte(sourceConcept.CostoDirectoUnitario);
        decimal pu     = motor.CalcularPrecioUnitario(cdUnit, CrearInputDeProyecto(proyecto)).PrecioUnitario;
        decimal cantidad = sourceConcept.Cantidad;
        decimal cdTotal = motor.Multiplicar(cantidad, cdUnit);
        decimal importe = motor.Multiplicar(cantidad, pu);

        return new BudgetConceptAssignmentDraft
        {
            Clave                = "DESTINO-001",
            Descripcion          = sourceConcept.Descripcion ?? string.Empty,
            Unidad               = sourceConcept.Unidad ?? string.Empty,
            Cantidad             = cantidad,
            CostoDirectoUnitario = cdUnit,
            CostoDirectoTotal    = cdTotal,
            PrecioUnitario       = pu,
            ImporteTotal         = importe,
            MatrizId             = sourceConcept.MatrizId,
            Matriz               = sourceConcept.Matriz
        };
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static void CompararDrafts(BudgetConceptAssignmentDraft servicio, BudgetConceptAssignmentDraft referencia, string tag)
    {
        Assert.AreEqual(referencia.Clave, servicio.Clave, $"{tag} Clave");
        Assert.AreEqual(referencia.Descripcion, servicio.Descripcion, $"{tag} Descripcion");
        Assert.AreEqual(referencia.Unidad, servicio.Unidad, $"{tag} Unidad");
        Assert.AreEqual(referencia.Cantidad, servicio.Cantidad, $"{tag} Cantidad");
        Assert.AreEqual(referencia.CostoDirectoUnitario, servicio.CostoDirectoUnitario, $"{tag} CostoDirectoUnitario");
        Assert.AreEqual(referencia.CostoDirectoTotal, servicio.CostoDirectoTotal, $"{tag} CostoDirectoTotal");
        Assert.AreEqual(referencia.PrecioUnitario, servicio.PrecioUnitario, $"{tag} PrecioUnitario");
        Assert.AreEqual(referencia.ImporteTotal, servicio.ImporteTotal, $"{tag} ImporteTotal");
        Assert.AreEqual(referencia.MatrizId, servicio.MatrizId, $"{tag} MatrizId");
    }

    private static BudgetPercentageInput CrearInputDeProyecto(Proyecto proyecto) => new()
    {
        CostoDirectoReferencia = 0m,
        IndirectosCentral = proyecto.PorcentajeIndirectosCentral,
        IndirectosCampo = proyecto.PorcentajeIndirectosCampo,
        Financiamiento = proyecto.PorcentajeFinanciamiento,
        Utilidad = proyecto.PorcentajeUtilidad,
        CargosAdicionales = proyecto.PorcentajeCargosAdicionales,
        ModoCalculoPorcentajes = proyecto.ModoCalculoPorcentajes ?? "Acumulables"
    };

    private static Proyecto CrearProyectoConPorcentajes(int decQty, int decAmt, int decPct) => new()
    {
        Nombre = "Proyecto asignación pruebas",
        Descripcion = string.Empty,
        Ubicacion = string.Empty,
        Convocante = string.Empty,
        Contratista = string.Empty,
        ApoderadoLegal = string.Empty,
        FechaInicio = new DateTime(2026, 1, 1),
        FechaTermino = new DateTime(2026, 1, 31),
        PlazoEjecucion = 31,
        PorcentajeIndirectosCentral = 5m,
        PorcentajeIndirectosCampo = 5m,
        PorcentajeFinanciamiento = 2m,
        PorcentajeUtilidad = 8m,
        PorcentajeCargosAdicionales = 3m,
        ModoCalculoPorcentajes = "Acumulables",
        DecimalesCantidad = decQty,
        DecimalesImporte = decAmt,
        DecimalesPorcentaje = decPct
    };

    private static Proyecto CrearProyectoAleatorio(Random rnd)
    {
        var proyecto = CrearProyectoConPorcentajes(rnd.Next(0, 5), rnd.Next(0, 5), rnd.Next(0, 7));
        proyecto.ModoCalculoPorcentajes = rnd.Next(0, 4) switch
        {
            0 => "Acumulables",
            1 => "SobreCD",
            2 => "sobrecd",
            _ => null!
        };
        proyecto.PorcentajeIndirectosCentral = rnd.Next(0, 1500) / 100m;
        proyecto.PorcentajeIndirectosCampo = rnd.Next(0, 1500) / 100m;
        proyecto.PorcentajeFinanciamiento = rnd.Next(0, 500) / 100m;
        proyecto.PorcentajeUtilidad = rnd.Next(0, 2000) / 100m;
        proyecto.PorcentajeCargosAdicionales = rnd.Next(0, 500) / 100m;
        return proyecto;
    }

    private static decimal ValorPrecio(Random rnd)
        => rnd.Next(1, 1_000_000) / 1000m;

    private static decimal ValorCantidad(Random rnd)
        => rnd.Next(1, 100_000) / 1000m;
}