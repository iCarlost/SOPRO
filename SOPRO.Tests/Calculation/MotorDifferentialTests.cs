using Microsoft.VisualStudio.TestTools.UnitTesting;
using SOPRO.Application.Models.Presupuesto;
using SOPRO.Application.Services;
using SOPRO.Core.Entities;
using Sopro.Calculation;

namespace SOPRO.Tests.Calculation;

/// <summary>
/// Pruebas diferenciales exactas: el motor nuevo del paquete (Sopro.Calculation)
/// debe producir EXACTAMENTE los mismos resultados que el motor legacy
/// (MotorCalculoSopro) para los mismos insumos, en toda la matriz de precisiones
/// y casos de la tabla de divergencias N0.
///
/// ORACULO EN N2: cuando la fachada delegue al paquete, esta comparación dejaría de
/// ser independiente; los goldens exactos congelados en LegacyOracleGoldenTests
/// quedan como oráculo legacy independiente.
/// </summary>
[TestClass]
public class MotorDifferentialTests
{
    private static readonly (int Cantidad, int Importe, int Porcentaje)[] Precisiones =
    {
        (2, 2, 4),      // configuracion por defecto y del proyecto real
        (4, 2, 4),      // proyecto real (DecimalesCantidad=4)
        (2, 4, 4),      // precision monetaria 4 (AmountDecimals=4)
        (4, 4, 4),
        (3, 3, 3),
        (2, 2, 2),
        (1, 1, 1),
        (0, 0, 0),      // precision nula (fila 7)
        (28, 28, 28),   // maximo permitido por Math.Round
        (-1, -1, -1),   // negativas: se normalizan a cero en ambos motores (fila 7)
    };

    // Precision > 28 se prueba por separado: Math.Round lanza en ambas implementaciones (fila 8).
    private static readonly int PrecisionExcesiva = 29;

    private static (MotorCalculoSopro Legacy, SoproCalculationEngine Nuevo) Motores((int Cantidad, int Importe, int Porcentaje) p)
        => (new MotorCalculoSopro(p.Cantidad, p.Importe, p.Porcentaje),
            new SoproCalculationEngine(p.Cantidad, p.Importe, p.Porcentaje));

    /// <summary>
    /// Mapeo del texto legacy al enum (mismo arbol de decision que el legacy:
    /// "SobreCD" casing-insensitive, todo lo demas Acumulables). Pertenece a la
    /// fachada N2; aqui es un helper de prueba para poder usar los insumos legacy.
    /// </summary>
    private static PercentageCalculationMode ParseModo(string? modo)
        => string.Equals(modo, "SobreCD", StringComparison.OrdinalIgnoreCase)
            ? PercentageCalculationMode.OverDirectCost
            : PercentageCalculationMode.Accumulative;

    [TestMethod]
    public void Redondeo_EsIdenticoEnTodaLaMatrizDePrecisiones()
    {
        decimal[] valores = { 0m, 1m, 1.005m, 33.33335m, 60.005m, -1.005m, 123456.789m };

        foreach (var p in Precisiones)
        {
            var (legacy, nuevo) = Motores(p);

            foreach (var valor in valores)
            {
                Assert.AreEqual(legacy.RedondearCantidad(valor), nuevo.RoundQuantity(valor),
                    $"RoundQuantity({valor}) con ({p.Cantidad},{p.Importe},{p.Porcentaje})");
                Assert.AreEqual(legacy.RedondearImporte(valor), nuevo.RoundAmount(valor),
                    $"RoundAmount({valor}) con ({p.Cantidad},{p.Importe},{p.Porcentaje})");
                Assert.AreEqual(legacy.RedondearPorcentaje(valor), nuevo.RoundPercentage(valor),
                    $"RoundPercentage({valor}) con ({p.Cantidad},{p.Importe},{p.Porcentaje})");
            }
        }
    }

    [TestMethod]
    public void Multiply_EsIdenticoEnTodaLaMatriz()
    {
        (decimal Cantidad, decimal Precio)[] casos =
        {
            (652m, 13.3875m),
            (1.234m, 10m),
            (10m, 60.005m),               // caso discriminante del hallazgo 3
            (1.6666m, 60.005m),
            (1000m, 1.0005m),
            (1.5m, 100.005m),
            (0m, 12345.678m),
            (-3m, 2.501m),
            (49.10253m, 12.968m),
        };

        foreach (var p in Precisiones)
        {
            var (legacy, nuevo) = Motores(p);

            foreach (var (cantidad, precio) in casos)
            {
                Assert.AreEqual(
                    legacy.Multiplicar(cantidad, precio),
                    nuevo.Multiply(cantidad, precio),
                    $"Multiply({cantidad}, {precio}) con ({p.Cantidad},{p.Importe},{p.Porcentaje})");
            }
        }
    }

    [TestMethod]
    public void CalculateUnitPrice_EsIdenticoEnTodaLaMatriz()
    {
        (decimal Cd, decimal IndC, decimal IndCampo, decimal Fin, decimal Uti, decimal Cargos, string Modo)[] casos =
        {
            (1000m, 5m, 5m, 6m, 8m, 3m, "Acumulables"),
            (1000m, 5m, 5m, 6m, 8m, 3m, "SobreCD"),
            (1000m, 5m, 5m, 6m, 8m, 3m, "SOBRECD"),   // casing distinto (fila 6)
            (1000m, 5m, 5m, 6m, 8m, 3m, null),        // modo nulo → acumulables
            (1000m, 5m, 5m, 6m, 8m, 3m, "Desconocido"),
            (100m, 0m, 0m, 0m, 0m, 0m, "Acumulables"),
            (60.005m, 5m, 5m, 6m, 8m, 3m, "Acumulables"),
            (0m, 10m, 10m, 10m, 10m, 10m, "OverDirectCost"),
            (123456.789m, 2.5m, 2.5m, 1.25m, 0.75m, 4.5m, "Acumulables"),
            (1000m, -1m, 1m, 0m, 0m, 0m, "Acumulables"),  // indirectos netos cero
        };

        foreach (var p in Precisiones)
        {
            var (legacy, nuevo) = Motores(p);

            foreach (var c in casos)
            {
                var entrada = new BudgetPercentageInput
                {
                    CostoDirectoReferencia = c.Cd,
                    IndirectosCentral = c.IndC,
                    IndirectosCampo = c.IndCampo,
                    Financiamiento = c.Fin,
                    Utilidad = c.Uti,
                    CargosAdicionales = c.Cargos,
                    ModoCalculoPorcentajes = c.Modo,
                };

                var pct = new PricePercentageInput
                {
                    ReferenceDirectCost = c.Cd,
                    CentralIndirectsPercentage = c.IndC,
                    FieldIndirectsPercentage = c.IndCampo,
                    FinancingPercentage = c.Fin,
                    ProfitPercentage = c.Uti,
                    AdditionalChargesPercentage = c.Cargos,
                    Mode = ParseModo(c.Modo),
                };

                var legacyResult = legacy.CalcularPrecioUnitario(c.Cd, entrada);
                var nuevoResult = nuevo.CalculateUnitPrice(c.Cd, pct);

                string etiqueta = $"CalculateUnitPrice(cd={c.Cd}, modo={c.Modo}) con ({p.Cantidad},{p.Importe},{p.Porcentaje})";
                Assert.AreEqual(legacyResult.CostoDirecto, nuevoResult.DirectCost, etiqueta + " [CD]");
                Assert.AreEqual(legacyResult.Indirectos, nuevoResult.IndirectCosts, etiqueta + " [Indirectos]");
                Assert.AreEqual(legacyResult.Financiamiento, nuevoResult.Financing, etiqueta + " [Financiamiento]");
                Assert.AreEqual(legacyResult.Utilidad, nuevoResult.Profit, etiqueta + " [Utilidad]");
                Assert.AreEqual(legacyResult.CargosAdicionales, nuevoResult.AdditionalCharges, etiqueta + " [Cargos]");
                Assert.AreEqual(legacyResult.PrecioUnitario, nuevoResult.UnitPrice, etiqueta + " [P.U.]");
                Assert.AreEqual(legacyResult.PctIndirectosCentral, nuevoResult.CentralIndirectsPercentage, etiqueta + " [PctIndCentral]");
                Assert.AreEqual(legacyResult.PctIndirectosCampo, nuevoResult.FieldIndirectsPercentage, etiqueta + " [PctIndCampo]");
                Assert.AreEqual(legacyResult.IndirectosCentral, nuevoResult.CentralIndirectCosts, etiqueta + " [IndCentral]");
                Assert.AreEqual(legacyResult.IndirectosCampo, nuevoResult.FieldIndirectCosts, etiqueta + " [IndCampo]");
                Assert.AreEqual(legacyResult.Subtotal1, nuevoResult.Subtotal1, etiqueta + " [Sub1]");
                Assert.AreEqual(legacyResult.Subtotal2, nuevoResult.Subtotal2, etiqueta + " [Sub2]");
                Assert.AreEqual(legacyResult.Subtotal3, nuevoResult.Subtotal3, etiqueta + " [Sub3]");
            }
        }
    }

    [TestMethod]
    public void DistributeAmount_EsIdenticoEnTodaLaMatriz()
    {
        (decimal Total, decimal[] Pesos)[] casos =
        {
            (100.005m, new[] { 1m, 1m, 1m }),       // fila 1
            (1000m, new[] { 33m, 33m, 34m }),
            (1000m, new[] { 50m, 50m }),
            (0.02m, new[] { 3m, 3m, 3m, 3m }),       // residuo NEGATIVO real en el ultimo periodo (fila 5)
            (100m, new[] { -10m, 110m }),            // pesos negativos con suma distinta de cero (fila 10)
            (777.77m, new[] { 42m }),
            (0m, new[] { 0m, 0m, 0m }),              // guard de suma de pesos cero (fila 11)
            (100m, Array.Empty<decimal>()),
            (100m, null),
            (999999.99m, new[] { 20m, 15m, 10m, 5m, 50m }),
            (1.005m, new[] { 1m, 2m, 3m }),
        };

        foreach (var p in Precisiones)
        {
            var (legacy, nuevo) = Motores(p);

            foreach (var caso in casos)
            {
                CollectionAssert.AreEqual(
                    legacy.DistribuirImporte(caso.Total, caso.Pesos).ToArray(),
                    nuevo.DistributeAmount(caso.Total, caso.Pesos).ToArray(),
                    $"DistributeAmount({caso.Total}, [{string.Join(",", caso.Pesos ?? new decimal[0])}]) con ({p.Cantidad},{p.Importe},{p.Porcentaje})");
            }
        }
    }

    [TestMethod]
    public void DistributeQuantity_EsIdenticoEnTodaLaMatriz()
    {
        (decimal Total, decimal[] Pesos)[] casos =
        {
            (10m, new[] { 1m, 1m, 1m }),
            (49.10253m, new[] { 1m, 2m, 3m, 4m }),
            (1.005m, new[] { 1m, 1m }),
            (0m, new[] { 0m, 0m }),
            (5m, Array.Empty<decimal>()),
            (5m, null),
        };

        foreach (var p in Precisiones)
        {
            var (legacy, nuevo) = Motores(p);

            foreach (var caso in casos)
            {
                CollectionAssert.AreEqual(
                    legacy.DistribuirCantidad(caso.Total, caso.Pesos).ToArray(),
                    nuevo.DistributeQuantity(caso.Total, caso.Pesos).ToArray(),
                    $"DistributeQuantity({caso.Total}) con ({p.Cantidad},{p.Importe},{p.Porcentaje})");
            }
        }
    }

    [TestMethod]
    public void SumAmounts_EsIdenticoEnTodaLaMatriz()
    {
        decimal[][] casos =
        {
            new[] { 1.005m, 2.005m, 3.005m },
            new[] { 100m, 200m, 300m },
            Array.Empty<decimal>(),
            new[] { -1m, 1m },
            new[] { 0.1m, 0.2m, 0.3m },
        };

        foreach (var p in Precisiones)
        {
            var (legacy, nuevo) = Motores(p);

            foreach (var caso in casos)
            {
                Assert.AreEqual(legacy.SumarImportes(caso), nuevo.SumAmounts(caso),
                    $"SumAmounts([{string.Join(",", caso)}]) con ({p.Cantidad},{p.Importe},{p.Porcentaje})");
            }

            Assert.AreEqual(legacy.SumarImportes(null), nuevo.SumAmounts(null),
                $"SumAmounts(null) con ({p.Cantidad},{p.Importe},{p.Porcentaje})");
        }
    }

    [TestMethod]
    public void SumQuantities_EsIdenticoEnTodaLaMatriz()
    {
        decimal[][] casos =
        {
            new[] { 1.005m, 2.005m, 3.005m },
            new[] { 10.333m, 20.666m },
            Array.Empty<decimal>(),
        };

        foreach (var p in Precisiones)
        {
            var (legacy, nuevo) = Motores(p);

            foreach (var caso in casos)
            {
                Assert.AreEqual(legacy.SumarCantidades(caso), nuevo.SumQuantities(caso),
                    $"SumQuantities([{string.Join(",", caso)}]) con ({p.Cantidad},{p.Importe},{p.Porcentaje})");
            }

            Assert.AreEqual(legacy.SumarCantidades(null), nuevo.SumQuantities(null),
                $"SumQuantities(null) con ({p.Cantidad},{p.Importe},{p.Porcentaje})");
        }
    }

    [TestMethod]
    public void SumDirectCost_EsIdenticoEnTodaLaMatriz()
    {
        var conceptos = new List<ConceptoPresupuesto>
        {
            new ConceptoPresupuesto { Cantidad = 10m, CostoDirectoUnitario = 60.005m, EsAgrupador = false, MatrizId = 1 },
            new ConceptoPresupuesto { Cantidad = 2m, CostoDirectoUnitario = 100.25m, EsAgrupador = false, MatrizId = 2 },
            new ConceptoPresupuesto { Cantidad = 999m, CostoDirectoUnitario = 1m, EsAgrupador = true, MatrizId = 3 },   // se omite
            new ConceptoPresupuesto { Cantidad = 999m, CostoDirectoUnitario = 1m, EsAgrupador = false, MatrizId = null }, // se omite
            new ConceptoPresupuesto { Cantidad = 3m, CostoDirectoUnitario = 2.5m, EsAgrupador = false, MatrizId = 0 },     // Id 0: HasValue true, participa
            new ConceptoPresupuesto { Cantidad = -1m, CostoDirectoUnitario = 5m, EsAgrupador = false, MatrizId = 4 },
        };

        var lineas = new[]
        {
            new DirectCostLine(10m, 60.005m, IsGrouping: false, HasMatrix: true),
            new DirectCostLine(2m, 100.25m, IsGrouping: false, HasMatrix: true),
            new DirectCostLine(999m, 1m, IsGrouping: true, HasMatrix: true),
            new DirectCostLine(999m, 1m, IsGrouping: false, HasMatrix: false),
            new DirectCostLine(3m, 2.5m, IsGrouping: false, HasMatrix: true),
            new DirectCostLine(-1m, 5m, IsGrouping: false, HasMatrix: true),
        };

        foreach (var p in Precisiones)
        {
            var (legacy, nuevo) = Motores(p);

            Assert.AreEqual(legacy.SumarCostoDirecto(conceptos), nuevo.SumDirectCost(lineas),
                $"SumDirectCost con ({p.Cantidad},{p.Importe},{p.Porcentaje})");
            Assert.AreEqual(legacy.SumarCostoDirecto(null), nuevo.SumDirectCost(null),
                $"SumDirectCost(null) con ({p.Cantidad},{p.Importe},{p.Porcentaje})");
            Assert.AreEqual(legacy.SumarCostoDirecto(new List<ConceptoPresupuesto>()), nuevo.SumDirectCost(Array.Empty<DirectCostLine>()),
                $"SumDirectCost(vacio) con ({p.Cantidad},{p.Importe},{p.Porcentaje})");
        }
    }

    [TestMethod]
    public void Multiply_ConOverflow_AmbosMotoresLanzanOverflowException()
    {
        foreach (var p in Precisiones)
        {
            var (legacy, nuevo) = Motores(p);

            Assert.ThrowsException<OverflowException>(() => legacy.Multiplicar(decimal.MaxValue, 10m),
                $"legacy Overflow con ({p.Cantidad},{p.Importe},{p.Porcentaje})");
            Assert.ThrowsException<OverflowException>(() => nuevo.Multiply(decimal.MaxValue, 10m),
                $"nuevo Overflow con ({p.Cantidad},{p.Importe},{p.Porcentaje})");
            Assert.ThrowsException<OverflowException>(() => legacy.Multiplicar(-decimal.MaxValue, 10m));
            Assert.ThrowsException<OverflowException>(() => nuevo.Multiply(-decimal.MaxValue, 10m));
        }
    }

    [TestMethod]
    public void CalculateUnitPrice_ConPorcentajesNulos_AmbosLanzanArgumentNullException()
    {
        foreach (var p in Precisiones)
        {
            var (legacy, nuevo) = Motores(p);

            Assert.ThrowsException<ArgumentNullException>(() => legacy.CalcularPrecioUnitario(1000m, null));
            Assert.ThrowsException<ArgumentNullException>(() => nuevo.CalculateUnitPrice(1000m, null));
        }
    }

    [TestMethod]
    public void PrecisionMayorA28_AmbosMotoresLanzanEnCadaOperacionQueRedondea()
    {
        var legacy = new MotorCalculoSopro(PrecisionExcesiva, PrecisionExcesiva, PrecisionExcesiva);
        var nuevo = new SoproCalculationEngine(PrecisionExcesiva, PrecisionExcesiva, PrecisionExcesiva);

        Assert.ThrowsException<ArgumentOutOfRangeException>(() => legacy.RedondearImporte(1.5m));
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => nuevo.RoundAmount(1.5m));
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => legacy.RedondearCantidad(1.5m));
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => nuevo.RoundQuantity(1.5m));
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => legacy.RedondearPorcentaje(1.5m));
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => nuevo.RoundPercentage(1.5m));
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => legacy.Multiplicar(1m, 2m));
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => nuevo.Multiply(1m, 2m));
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => legacy.CalcularPrecioUnitario(100m, new BudgetPercentageInput()));
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => nuevo.CalculateUnitPrice(100m, new PricePercentageInput()));
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => legacy.DistribuirImporte(100m, new[] { 1m, 1m }));
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => nuevo.DistributeAmount(100m, new[] { 1m, 1m }));
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => legacy.SumarImportes(new[] { 1m }));
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => nuevo.SumAmounts(new[] { 1m }));
    }
}
