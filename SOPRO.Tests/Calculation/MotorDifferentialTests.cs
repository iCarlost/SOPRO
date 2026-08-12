using Microsoft.VisualStudio.TestTools.UnitTesting;
using SOPRO.Application.Models.Presupuesto;
using SOPRO.Application.Services;
using Sopro.Calculation;

namespace SOPRO.Tests.Calculation;

/// <summary>
/// Pruebas diferenciales exactas: el motor nuevo del paquete (Sopro.Calculation)
/// debe producir EXACTAMENTE los mismos resultados que el motor legacy
/// (MotorCalculoSopro) para los mismos insumos, en toda la matriz de precisiones
/// y casos de la tabla de divergencias N0.
/// </summary>
[TestClass]
public class MotorDifferentialTests
{
    private static readonly (int Cantidad, int Importe, int Porcentaje)[] Precisiones =
    {
        (2, 2, 4),      // configuracion por defecto y del proyecto real
        (4, 2, 4),      // proyecto real (DecimalesCantidad=4)
        (3, 3, 3),
        (0, 0, 0),      // precision nula (fila 7)
        (2, 2, 2),
    };

    private static (MotorCalculoSopro Legacy, SoproCalculationEngine Nuevo) Motores((int Cantidad, int Importe, int Porcentaje) p)
        => (new MotorCalculoSopro(p.Cantidad, p.Importe, p.Porcentaje),
            new SoproCalculationEngine(p.Cantidad, p.Importe, p.Porcentaje));

    [TestMethod]
    public void Redondeo_EsIdenticoEnTodaLaMatrizDePrecisiones()
    {
        decimal[] valores = { 0m, 1m, 1.005m, 33.33335m, 60.005m, -1.005m, 123456.789m };

        foreach (var p in Precisiones)
        {
            var (legacy, nuevo) = Motores(p);

            foreach (var valor in valores)
            {
                Assert.AreEqual(legacy.RedondearCantidad(valor), nuevo.RedondearCantidad(valor),
                    $"RedondearCantidad({valor}) con ({p.Cantidad},{p.Importe},{p.Porcentaje})");
                Assert.AreEqual(legacy.RedondearImporte(valor), nuevo.RedondearImporte(valor),
                    $"RedondearImporte({valor}) con ({p.Cantidad},{p.Importe},{p.Porcentaje})");
                Assert.AreEqual(legacy.RedondearPorcentaje(valor), nuevo.RedondearPorcentaje(valor),
                    $"RedondearPorcentaje({valor}) con ({p.Cantidad},{p.Importe},{p.Porcentaje})");
            }
        }
    }

    [TestMethod]
    public void Multiplicar_EsIdenticoEnTodaLaMatriz()
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
                    nuevo.Multiplicar(cantidad, precio),
                    $"Multiplicar({cantidad}, {precio}) con ({p.Cantidad},{p.Importe},{p.Porcentaje})");
            }
        }
    }

    [TestMethod]
    public void CalcularPrecioUnitario_EsIdenticoEnTodaLaMatriz()
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
            (0m, 10m, 10m, 10m, 10m, 10m, "SobreCD"),
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
                    CostoDirectoReferencia = c.Cd,
                    IndirectosCentral = c.IndC,
                    IndirectosCampo = c.IndCampo,
                    Financiamiento = c.Fin,
                    Utilidad = c.Uti,
                    CargosAdicionales = c.Cargos,
                    ModoCalculoPorcentajes = PercentageCalculationModes.Parse(c.Modo),
                };

                var legacyResult = legacy.CalcularPrecioUnitario(c.Cd, entrada);
                var nuevoResult = nuevo.CalcularPrecioUnitario(c.Cd, pct);

                string etiqueta = $"CalcularPrecioUnitario(cd={c.Cd}, modo={c.Modo}) con ({p.Cantidad},{p.Importe},{p.Porcentaje})";
                Assert.AreEqual(legacyResult.CostoDirecto, nuevoResult.CostoDirecto, etiqueta + " [CD]");
                Assert.AreEqual(legacyResult.Indirectos, nuevoResult.Indirectos, etiqueta + " [Indirectos]");
                Assert.AreEqual(legacyResult.Financiamiento, nuevoResult.Financiamiento, etiqueta + " [Financiamiento]");
                Assert.AreEqual(legacyResult.Utilidad, nuevoResult.Utilidad, etiqueta + " [Utilidad]");
                Assert.AreEqual(legacyResult.CargosAdicionales, nuevoResult.CargosAdicionales, etiqueta + " [Cargos]");
                Assert.AreEqual(legacyResult.PrecioUnitario, nuevoResult.PrecioUnitario, etiqueta + " [P.U.]");
                Assert.AreEqual(legacyResult.PctIndirectosCentral, nuevoResult.PctIndirectosCentral, etiqueta + " [PctIndCentral]");
                Assert.AreEqual(legacyResult.PctIndirectosCampo, nuevoResult.PctIndirectosCampo, etiqueta + " [PctIndCampo]");
                Assert.AreEqual(legacyResult.IndirectosCentral, nuevoResult.IndirectosCentral, etiqueta + " [IndCentral]");
                Assert.AreEqual(legacyResult.IndirectosCampo, nuevoResult.IndirectosCampo, etiqueta + " [IndCampo]");
                Assert.AreEqual(legacyResult.Subtotal1, nuevoResult.Subtotal1, etiqueta + " [Sub1]");
                Assert.AreEqual(legacyResult.Subtotal2, nuevoResult.Subtotal2, etiqueta + " [Sub2]");
                Assert.AreEqual(legacyResult.Subtotal3, nuevoResult.Subtotal3, etiqueta + " [Sub3]");
            }
        }
    }

    [TestMethod]
    public void DistribuirImporte_EsIdenticoEnTodaLaMatriz()
    {
        (decimal Total, decimal[] Pesos)[] casos =
        {
            (100.005m, new[] { 1m, 1m, 1m }),       // fila 1
            (1000m, new[] { 33m, 33m, 34m }),
            (1000m, new[] { 50m, 50m }),
            (1m, new[] { 1m, 1000000m, 1m }),        // residuo negativo (fila 5)
            (100m, new[] { -1m, 3m, -2m }),          // pesos negativos (fila 10)
            (777.77m, new[] { 42m }),
            (0m, new[] { 0m, 0m, 0m }),
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
                    nuevo.DistribuirImporte(caso.Total, caso.Pesos).ToArray(),
                    $"DistribuirImporte({caso.Total}, [{string.Join(",", caso.Pesos ?? new decimal[0])}]) con ({p.Cantidad},{p.Importe},{p.Porcentaje})");
            }
        }
    }

    [TestMethod]
    public void DistribuirCantidad_EsIdenticoEnTodaLaMatriz()
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
                    nuevo.DistribuirCantidad(caso.Total, caso.Pesos).ToArray(),
                    $"DistribuirCantidad({caso.Total}) con ({p.Cantidad},{p.Importe},{p.Porcentaje})");
            }
        }
    }

    [TestMethod]
    public void SumarImportes_EsIdenticoEnTodaLaMatriz()
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
                Assert.AreEqual(legacy.SumarImportes(caso), nuevo.SumarImportes(caso),
                    $"SumarImportes([{string.Join(",", caso)}]) con ({p.Cantidad},{p.Importe},{p.Porcentaje})");
            }

            Assert.AreEqual(legacy.SumarImportes(null), nuevo.SumarImportes(null),
                $"SumarImportes(null) con ({p.Cantidad},{p.Importe},{p.Porcentaje})");
        }
    }

    [TestMethod]
    public void SumarCantidades_EsIdenticoEnTodaLaMatriz()
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
                Assert.AreEqual(legacy.SumarCantidades(caso), nuevo.SumarCantidades(caso),
                    $"SumarCantidades([{string.Join(",", caso)}]) con ({p.Cantidad},{p.Importe},{p.Porcentaje})");
            }

            Assert.AreEqual(legacy.SumarCantidades(null), nuevo.SumarCantidades(null),
                $"SumarCantidades(null) con ({p.Cantidad},{p.Importe},{p.Porcentaje})");
        }
    }

    [TestMethod]
    public void CalcularImporteSobreBase_EsIdentico()
    {
        foreach (var p in Precisiones)
        {
            var (legacy, nuevo) = Motores(p);

            Assert.AreEqual(
                legacy.CalcularImporteSobreBase(652m, 13.3875m),
                nuevo.CalcularImporteSobreBase(652m, 13.3875m),
                $"CalcularImporteSobreBase con ({p.Cantidad},{p.Importe},{p.Porcentaje})");
        }
    }
}