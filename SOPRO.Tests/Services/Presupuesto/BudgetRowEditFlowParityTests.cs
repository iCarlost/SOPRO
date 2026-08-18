using Microsoft.VisualStudio.TestTools.UnitTesting;
using SOPRO.Application.Models.Presupuesto;
using SOPRO.Application.Services;
using SOPRO.Core.Entities;

namespace SOPRO.Tests.Services.Presupuesto;

[TestClass]
public class BudgetRowEditFlowParityTests
{
    // ── N5-5: HandleQuantityCellChange migró de MotorCalculoSopro a
    //    SoproCalculationEngine (RoundQuantity, Multiply, RoundAmount).
    //    Paridad exacta contra referencia compuesta con primitivas de la
    //    fachada (oráculo diferencial) + dorados.

    [TestMethod]
    public void HandleQuantityCellChange_ParidadConFachada_BateriaAleatoriaConSemilla()
    {
        var rnd = new Random(20260817);

        for (int iter = 0; iter < 800; iter++)
        {
            var proyecto = CrearProyectoAleatorio(rnd);
            var concepto = new ConceptoPresupuesto
            {
                MatrizId = iter + 1,
                CostoDirectoUnitario = ValorPrecio(rnd)
            };
            string cantidadTexto = (iter % 10 == 0 ? rnd.Next(0, 2) == 0 ? "" : "abc" : null) ?? ValorCantidad(rnd).ToString();

            var servicio = BudgetRowEditFlowService.HandleQuantityCellChange(proyecto, concepto, cantidadTexto);
            var referencia = HandleQuantityCellChangeConFachada(proyecto, concepto, cantidadTexto);

            Assert.AreEqual(referencia.HasChanges, servicio.HasChanges, $"iter={iter} HasChanges");
            if (!referencia.HasChanges)
                continue;

            Assert.AreEqual(referencia.Cantidad, servicio.Cantidad, $"iter={iter} Cantidad");
            Assert.AreEqual(referencia.PrecioUnitario, servicio.PrecioUnitario, $"iter={iter} PrecioUnitario");
            Assert.AreEqual(referencia.Importe, servicio.Importe, $"iter={iter} Importe");
            Assert.AreEqual(referencia.Subtotal, servicio.Subtotal, $"iter={iter} Subtotal");
            Assert.AreEqual(referencia.Iva, servicio.Iva, $"iter={iter} Iva");
            Assert.AreEqual(referencia.Total, servicio.Total, $"iter={iter} Total");
            Assert.AreEqual(referencia.PrecioUnitarioLetra, servicio.PrecioUnitarioLetra, $"iter={iter} PrecioUnitarioLetra");
            Assert.AreEqual(referencia.TotalLetra, servicio.TotalLetra, $"iter={iter} TotalLetra");
        }
    }

    [TestMethod]
    public void HandleQuantityCellChange_Dorado_ConIva()
    {
        var proyecto = CrearProyecto(2, 2, 4, 16m);
        var concepto = new ConceptoPresupuesto { MatrizId = 1, CostoDirectoUnitario = 100m };

        var result = BudgetRowEditFlowService.HandleQuantityCellChange(proyecto, concepto, "652");

        // Cantidad 652; PU con cascada 5/5/2/8/3 sobre CD 100 → 124.82;
        // importe 652×124.82 = 81382.64; IVA 16% → 13021.22; total 94403.86
        Assert.IsTrue(result.HasChanges);
        Assert.AreEqual(652m, result.Cantidad);
        Assert.AreEqual(124.82m, result.PrecioUnitario);
        Assert.AreEqual(81382.64m, result.Importe);
        Assert.AreEqual(81382.64m, result.Subtotal);
        Assert.AreEqual(13021.22m, result.Iva);
        Assert.AreEqual(94403.86m, result.Total);
    }

    [TestMethod]
    public void HandleQuantityCellChange_Dorado_SinIva()
    {
        var proyecto = CrearProyecto(2, 2, 4, 0m);
        var concepto = new ConceptoPresupuesto { MatrizId = 1, CostoDirectoUnitario = 100m };

        var result = BudgetRowEditFlowService.HandleQuantityCellChange(proyecto, concepto, "10");

        Assert.AreEqual(1248.20m, result.Importe);
        Assert.AreEqual(1248.20m, result.Subtotal);
        Assert.AreEqual(0.00m, result.Iva);
        Assert.AreEqual(1248.20m, result.Total);
    }

    [TestMethod]
    public void HandleQuantityCellChange_Dorado_RoundQuantitySobreTextoConMasDecimales()
    {
        var proyecto = CrearProyecto(2, 2, 4, 16m);
        var concepto = new ConceptoPresupuesto { MatrizId = 1, CostoDirectoUnitario = 100m };

        // 652.5555 se normaliza a la precisión visible de cantidad (2 decimales) → 652.56
        var result = BudgetRowEditFlowService.HandleQuantityCellChange(proyecto, concepto, "652.5555");

        Assert.AreEqual(652.56m, result.Cantidad);
    }

    [TestMethod]
    public void HandleQuantityCellChange_TextoInvalido_DebeDevolverResultadoVacio()
    {
        var proyecto = CrearProyecto(2, 2, 4, 16m);
        var concepto = new ConceptoPresupuesto { MatrizId = 1, CostoDirectoUnitario = 100m };

        var vacio = BudgetRowEditFlowService.HandleQuantityCellChange(proyecto, concepto, "");
        var noNumerico = BudgetRowEditFlowService.HandleQuantityCellChange(proyecto, concepto, "abc");
        var sinMatriz = BudgetRowEditFlowService.HandleQuantityCellChange(proyecto, new ConceptoPresupuesto(), "10");

        Assert.IsFalse(vacio.HasChanges);
        Assert.IsFalse(noNumerico.HasChanges);
        Assert.IsFalse(sinMatriz.HasChanges);
    }

    // ── Referencia compuesta con la fachada (mismo algoritmo, primitivas legacy) ──

    private static BudgetQuantityChangeResult HandleQuantityCellChangeConFachada(
        Proyecto proyecto, ConceptoPresupuesto concepto, string cantidadTexto)
    {
        if (concepto == null || !concepto.MatrizId.HasValue)
            return new BudgetQuantityChangeResult();

        if (string.IsNullOrWhiteSpace(cantidadTexto) || !decimal.TryParse(cantidadTexto, out decimal cantidad))
            return new BudgetQuantityChangeResult();

        var motor = new MotorCalculoSopro(proyecto);
        cantidad = motor.RedondearCantidad(cantidad);

        decimal puFinal = motor.CalcularPrecioUnitario(concepto.CostoDirectoUnitario,
            new BudgetPercentageInput
            {
                IndirectosCentral      = proyecto.PorcentajeIndirectosCentral,
                IndirectosCampo        = proyecto.PorcentajeIndirectosCampo,
                Financiamiento         = proyecto.PorcentajeFinanciamiento,
                Utilidad               = proyecto.PorcentajeUtilidad,
                CargosAdicionales      = proyecto.PorcentajeCargosAdicionales,
                ModoCalculoPorcentajes = proyecto.ModoCalculoPorcentajes ?? "Acumulables"
            }).PrecioUnitario;
        decimal importe  = motor.Multiplicar(cantidad, puFinal);
        decimal subtotal = importe;
        decimal tasaIva  = proyecto.PorcentajeIVA / 100m;
        decimal iva      = motor.RedondearImporte(subtotal * tasaIva);
        decimal total    = motor.RedondearImporte(subtotal + iva);

        return new BudgetQuantityChangeResult
        {
            HasChanges = true,
            Cantidad = cantidad,
            PrecioUnitario = puFinal,
            Importe = importe,
            Subtotal = subtotal,
            Iva = iva,
            Total = total,
            PrecioUnitarioLetra = BudgetPricingService.ConvertirALetras(puFinal),
            TotalLetra = BudgetPricingService.ConvertirALetras(total)
        };
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static Proyecto CrearProyecto(int decQty, int decAmt, int decPct, decimal iva) => new()
    {
        Nombre = "Proyecto edición fila pruebas",
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
        PorcentajeIVA = iva,
        DecimalesCantidad = decQty,
        DecimalesImporte = decAmt,
        DecimalesPorcentaje = decPct
    };

    private static Proyecto CrearProyectoAleatorio(Random rnd)
    {
        var proyecto = CrearProyecto(rnd.Next(0, 5), rnd.Next(0, 5), rnd.Next(0, 7),
            rnd.Next(0, 5) == 0 ? 0m : rnd.Next(0, 2000) / 100m);
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
        => rnd.Next(1, 1_000_000) / 1000m;
}