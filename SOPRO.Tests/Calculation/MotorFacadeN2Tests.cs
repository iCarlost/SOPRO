using System.Globalization;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Sopro.Calculation;
using SOPRO.Application.Models.Presupuesto;
using SOPRO.Application.Services;
using SOPRO.Core.Entities;

namespace SOPRO.Tests.Calculation;

/// <summary>
/// Gate N2 (PLAN-01 §11): la fachada legacy conserva su API pública y sus
/// comportamientos externos. Estas pruebas no validan aritmética (eso lo hacen
/// los diferenciales y el oráculo dorado independiente); validan CONTRATO:
///   - Por reflexión: nombres, firmas y tipos de retorno de MotorCalculoSopro
///     y DesglosePrecios (los callers actuales compilan sin cambios masivos).
///   - Excepciones con el mismo ParamName que el legacy.
///   - Formato con la cultura actual (permanece en la fachada: N0, fila 9).
///   - Mapeos legacy → paquete que viven en la fachada.
/// ATENCION: esto NO es un oráculo de resultados; si la aritmética cambiara,
/// LegacyOracleGoldenTests (constantes congeladas) es quien lo detectaría.
/// </summary>
[TestClass]
public class MotorFacadeN2Tests
{
    [TestMethod]
    public void MotorCalculoSopro_ConservaTodaLaAPI_PublicaLegacy()
    {
        var type = typeof(MotorCalculoSopro);

        Assert.IsTrue(type.IsSealed, "MotorCalculoSopro debe seguir sellada");
        Assert.AreEqual("SOPRO.Application.Services", type.Namespace);

        var metodos = type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);

        var esperados = new Dictionary<string, (Type Retorno, Type[] Params)>
        {
            ["RedondearCantidad"]    = (typeof(decimal), new[] { typeof(decimal) }),
            ["RedondearImporte"]     = (typeof(decimal), new[] { typeof(decimal) }),
            ["RedondearPorcentaje"]  = (typeof(decimal), new[] { typeof(decimal) }),
            ["Multiplicar"]          = (typeof(decimal), new[] { typeof(decimal), typeof(decimal) }),
            ["CalcularImporteSobreBase"] = (typeof(decimal), new[] { typeof(decimal), typeof(decimal) }),
            ["CalcularPrecioUnitario"]   = (typeof(DesglosePrecios), new[] { typeof(decimal), typeof(BudgetPercentageInput) }),
            ["DistribuirImporte"]    = (typeof(IReadOnlyList<decimal>), new[] { typeof(decimal), typeof(IReadOnlyList<decimal>) }),
            ["DistribuirCantidad"]   = (typeof(IReadOnlyList<decimal>), new[] { typeof(decimal), typeof(IReadOnlyList<decimal>) }),
            ["SumarImportes"]        = (typeof(decimal), new[] { typeof(IEnumerable<decimal>) }),
            ["SumarCantidades"]      = (typeof(decimal), new[] { typeof(IEnumerable<decimal>) }),
            ["SumarCostoDirecto"]    = (typeof(decimal), new[] { typeof(IEnumerable<ConceptoPresupuesto>) }),
            ["FormatCantidad"]       = (typeof(string), new[] { typeof(decimal) }),
            ["FormatImporte"]        = (typeof(string), new[] { typeof(decimal) }),
            ["FormatPorcentaje"]     = (typeof(string), new[] { typeof(decimal) }),
            ["FormatNumero"]         = (typeof(string), new[] { typeof(decimal), typeof(int) }),
        };

        Assert.AreEqual(esperados.Count, metodos.Length,
            "La superficie pública de MotorCalculoSopro no debe ganar ni perder métodos");

        foreach (var (nombre, (retorno, parametros)) in esperados)
        {
            var m = metodos.Single(x => x.Name == nombre);

            Assert.AreEqual(retorno, m.ReturnType, $"Retorno de {nombre}");
            CollectionAssert.AreEqual(
                parametros.Select(p => p.ToString()).ToArray(),
                m.GetParameters().Select(p => p.ParameterType.ToString()).ToArray(),
                $"Parámetros de {nombre}");
        }

        var ctors = type.GetConstructors(BindingFlags.Public | BindingFlags.Instance);
        Assert.AreEqual(2, ctors.Length, "Deben conservarse ambos constructores");

        Assert.IsTrue(ctors.Any(c =>
            c.GetParameters().Select(p => p.ParameterType).SequenceEqual(new[] { typeof(Proyecto) })));
        Assert.IsTrue(ctors.Any(c =>
            c.GetParameters().Select(p => p.ParameterType).SequenceEqual(new[] { typeof(int), typeof(int), typeof(int) })));
    }

    [TestMethod]
    public void DesglosePrecios_ConservaSuContratoPublico()
    {
        var type = typeof(DesglosePrecios);

        var ctor = type.GetConstructors(BindingFlags.Public | BindingFlags.Instance).Single();
        CollectionAssert.AreEqual(
            new[] { "CostoDirecto", "Indirectos", "Financiamiento", "Utilidad",
                    "CargosAdicionales", "PrecioUnitario", "PctIndirectosCentral", "PctIndirectosCampo" },
            ctor.GetParameters().Select(p => p.Name).ToArray());

        foreach (var prop in new[] { "CostoDirecto", "Indirectos", "Financiamiento", "Utilidad",
                                     "CargosAdicionales", "PrecioUnitario",
                                     "IndirectosCentral", "IndirectosCampo",
                                     "Subtotal1", "Subtotal2", "Subtotal3" })
        {
            Assert.IsNotNull(type.GetProperty(prop), $"Falta la propiedad {prop}");
        }
    }

    [TestMethod]
    public void CalcularPrecioUnitario_ConPctNulo_PreservaElParamNameLegacy()
    {
        var motor = new MotorCalculoSopro(2, 2, 4);

        var ex = Assert.ThrowsException<ArgumentNullException>(
            () => motor.CalcularPrecioUnitario(1000m, null!));

        Assert.AreEqual("pct", ex.ParamName);
    }

    [TestMethod]
    public void Constructor_ConProyectoNulo_PreservaElParamNameLegacy()
    {
        var ex = Assert.ThrowsException<ArgumentNullException>(
            () => new MotorCalculoSopro(null!));

        Assert.AreEqual("proyecto", ex.ParamName);
    }

    [TestMethod]
    public void MapeoDeModo_ViveEnLaFachada_YReproduceElLegacy()
    {
        var motor = new MotorCalculoSopro(2, 2, 4);
        string[] modos = { "SobreCD", "SOBRECD", "Acumulables", "Desconocido", null };

        // Oráculo congelado: Acumulables → 1297.06, SobreCD → 1270 (cualquier casing)
        foreach (var modo in modos)
        {
            var resultado = motor.CalcularPrecioUnitario(1000m, new BudgetPercentageInput
            {
                IndirectosCentral = 5m,
                IndirectosCampo = 5m,
                Financiamiento = 6m,
                Utilidad = 8m,
                CargosAdicionales = 3m,
                ModoCalculoPorcentajes = modo,
            });

            Assert.AreEqual(
                string.Equals(modo, "SobreCD", StringComparison.OrdinalIgnoreCase) ? 1270m : 1297.06m,
                resultado.PrecioUnitario,
                $"Modo '{modo ?? "null"}' no mapeado como el legacy");
        }
    }

    [TestMethod]
    public void Formato_PermaneceEnLaFachada_ConCulturaActual()
    {
        var original = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("en-US");

            var motor = new MotorCalculoSopro(2, 2, 4);

            Assert.AreEqual("1,234.50", motor.FormatCantidad(1234.5m));
            Assert.AreEqual("$1,234.50", motor.FormatImporte(1234.5m));
            Assert.AreEqual("12.3450", motor.FormatPorcentaje(12.345m));
            Assert.AreEqual("1,234.57", motor.FormatNumero(1234.567m, 2));
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }

    [TestMethod]
    public void Facade_Distribuye_Y_Suma_ConElMotorDelPaquete()
    {
        // Si la fachada tuviera una segunda cascada, los goldens de
        // LegacyOracleGoldenTests fallarían; aquí solo se confirma el cableado.
        var motor = new MotorCalculoSopro(2, 2, 4);
        var nuevo = new SoproCalculationEngine(2, 2, 4);

        CollectionAssert.AreEqual(
            nuevo.DistributeAmount(100.005m, new[] { 1m, 1m, 1m }).ToArray(),
            motor.DistribuirImporte(100.005m, new[] { 1m, 1m, 1m }).ToArray());
        CollectionAssert.AreEqual(
            nuevo.DistributeQuantity(10m, new[] { 1m, 1m, 1m }).ToArray(),
            motor.DistribuirCantidad(10m, new[] { 1m, 1m, 1m }).ToArray());
        Assert.AreEqual(nuevo.SumAmounts(new[] { 1.005m, 2.005m, 3.005m }), motor.SumarImportes(new[] { 1.005m, 2.005m, 3.005m }));
        Assert.AreEqual(nuevo.SumQuantities(new[] { 1.005m, 2.005m }), motor.SumarCantidades(new[] { 1.005m, 2.005m }));
    }
}