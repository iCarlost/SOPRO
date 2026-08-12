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

        var esperados = new Dictionary<string, (Type Retorno, (Type Tipo, string Nombre)[] Parametros)>
        {
            ["RedondearCantidad"]    = (typeof(decimal), new[] { (typeof(decimal), "valor") }),
            ["RedondearImporte"]     = (typeof(decimal), new[] { (typeof(decimal), "valor") }),
            ["RedondearPorcentaje"]  = (typeof(decimal), new[] { (typeof(decimal), "valor") }),
            ["Multiplicar"]          = (typeof(decimal), new[] { (typeof(decimal), "cantidad"), (typeof(decimal), "precioUnitario") }),
            ["CalcularImporteSobreBase"] = (typeof(decimal), new[] { (typeof(decimal), "factor"), (typeof(decimal), "baseImporte") }),
            ["CalcularPrecioUnitario"]   = (typeof(DesglosePrecios), new[] { (typeof(decimal), "costoDirecto"), (typeof(BudgetPercentageInput), "pct") }),
            ["DistribuirImporte"]    = (typeof(IReadOnlyList<decimal>), new[] { (typeof(decimal), "total"), (typeof(IReadOnlyList<decimal>), "pesos") }),
            ["DistribuirCantidad"]   = (typeof(IReadOnlyList<decimal>), new[] { (typeof(decimal), "total"), (typeof(IReadOnlyList<decimal>), "pesos") }),
            ["SumarImportes"]        = (typeof(decimal), new[] { (typeof(IEnumerable<decimal>), "valores") }),
            ["SumarCantidades"]      = (typeof(decimal), new[] { (typeof(IEnumerable<decimal>), "valores") }),
            ["SumarCostoDirecto"]    = (typeof(decimal), new[] { (typeof(IEnumerable<ConceptoPresupuesto>), "conceptos") }),
            ["FormatCantidad"]       = (typeof(string), new[] { (typeof(decimal), "valor") }),
            ["FormatImporte"]        = (typeof(string), new[] { (typeof(decimal), "valor") }),
            ["FormatPorcentaje"]     = (typeof(string), new[] { (typeof(decimal), "valor") }),
            ["FormatNumero"]         = (typeof(string), new[] { (typeof(decimal), "valor"), (typeof(int), "decimales") }),
        };

        Assert.AreEqual(esperados.Count, metodos.Length,
            "La superficie pública de MotorCalculoSopro no debe ganar ni perder métodos");

        foreach (var (nombre, (retorno, parametros)) in esperados)
        {
            var m = metodos.Single(x => x.Name == nombre);

            Assert.AreEqual(retorno, m.ReturnType, $"Retorno de {nombre}");
            CollectionAssert.AreEqual(
                parametros.Select(p => $"{p.Tipo} {p.Nombre}").ToArray(),
                m.GetParameters().Select(p => $"{p.ParameterType} {p.Name}").ToArray(),
                $"Parámetros de {nombre}");
        }

        var ctors = type.GetConstructors(BindingFlags.Public | BindingFlags.Instance);
        Assert.AreEqual(2, ctors.Length, "Deben conservarse ambos constructores");

        Assert.IsTrue(ctors.Any(c =>
            c.GetParameters().Select(p => (p.ParameterType, p.Name!))
                .SequenceEqual(new[] { (typeof(Proyecto), "proyecto") })));
        Assert.IsTrue(ctors.Any(c =>
            c.GetParameters().Select(p => (p.ParameterType, p.Name!))
                .SequenceEqual(new[]
                {
                    (typeof(int), "decimalesCantidad"),
                    (typeof(int), "decimalesImporte"),
                    (typeof(int), "decimalesPorcentaje"),
                })));
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

        CollectionAssert.AreEqual(
            Enumerable.Repeat(typeof(decimal), 8).ToArray(),
            ctor.GetParameters().Select(p => p.ParameterType).ToArray());
        Assert.IsFalse(ctor.GetParameters()[5].HasDefaultValue);
        Assert.AreEqual(0m, ctor.GetParameters()[6].DefaultValue);
        Assert.AreEqual(0m, ctor.GetParameters()[7].DefaultValue);

        var propiedadesPosicionales = new[]
        {
            "CostoDirecto", "Indirectos", "Financiamiento", "Utilidad",
            "CargosAdicionales", "PrecioUnitario", "PctIndirectosCentral", "PctIndirectosCampo",
        };
        var propiedadesDerivadas = new[]
        {
            "IndirectosCentral", "IndirectosCampo", "Subtotal1", "Subtotal2", "Subtotal3",
        };

        foreach (var nombre in propiedadesPosicionales.Concat(propiedadesDerivadas))
        {
            var propiedad = type.GetProperty(nombre);
            Assert.IsNotNull(propiedad, $"Falta la propiedad {nombre}");
            Assert.AreEqual(typeof(decimal), propiedad.PropertyType, $"Tipo de {nombre}");
            Assert.IsTrue(propiedad.GetMethod?.IsPublic, $"Getter público de {nombre}");

            if (propiedadesPosicionales.Contains(nombre))
            {
                Assert.IsTrue(propiedad.SetMethod?.IsPublic, $"Setter público de {nombre}");
                CollectionAssert.Contains(
                    propiedad.SetMethod!.ReturnParameter.GetRequiredCustomModifiers(),
                    typeof(System.Runtime.CompilerServices.IsExternalInit),
                    $"{nombre} debe conservar su setter init");
            }
            else
            {
                Assert.IsNull(propiedad.SetMethod, $"{nombre} debe seguir siendo solo lectura");
            }
        }

        Assert.AreEqual(
            propiedadesPosicionales.Length + propiedadesDerivadas.Length,
            type.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly).Length);
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
        string?[] modos = { "SobreCD", "SOBRECD", "Acumulables", "Desconocido", null };

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
                ModoCalculoPorcentajes = modo!,
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

    [TestMethod]
    public void Distribuciones_PreservanLosTiposConcretosMutablesDelLegacy()
    {
        var motor = new MotorCalculoSopro(2, 2, 4);

        var importes = motor.DistribuirImporte(100m, new[] { 1m, 1m });
        var cantidades = motor.DistribuirCantidad(100m, new[] { 0m, 0m });

        Assert.IsInstanceOfType<decimal[]>(importes);
        ((IList<decimal>)importes)[0] = 49m;
        Assert.AreEqual(49m, importes[0]);

        Assert.IsInstanceOfType<List<decimal>>(cantidades);
        ((IList<decimal>)cantidades).Add(1m);
        Assert.AreEqual(3, cantidades.Count);

        var pesosDeUnaSolaEnumeracion = new SingleEnumerationReadOnlyList(1m, 1m);
        CollectionAssert.AreEqual(
            new[] { 50m, 50m },
            motor.DistribuirImporte(100m, pesosDeUnaSolaEnumeracion).ToArray());
        Assert.AreEqual(1, pesosDeUnaSolaEnumeracion.EnumerationCount);
    }

    [TestMethod]
    public void SumarCostoDirecto_MapeaHasMatrixSoloDesdeMatrizIdHasValue()
    {
        var motor = new MotorCalculoSopro(2, 2, 4);
        var conceptos = new[]
        {
            new ConceptoPresupuesto { Cantidad = 1m, CostoDirectoUnitario = 10m, MatrizId = 0 },
            new ConceptoPresupuesto { Cantidad = 1m, CostoDirectoUnitario = 100m, MatrizId = null, Matriz = new Matriz() },
            new ConceptoPresupuesto { Cantidad = 1m, CostoDirectoUnitario = 1000m, MatrizId = 1, EsAgrupador = true },
            new ConceptoPresupuesto { Cantidad = 2m, CostoDirectoUnitario = 10m, MatrizId = 2 },
        };

        Assert.AreEqual(30m, motor.SumarCostoDirecto(conceptos));
    }

    private sealed class SingleEnumerationReadOnlyList(params decimal[] values) : IReadOnlyList<decimal>
    {
        public int EnumerationCount { get; private set; }

        public int Count => values.Length;

        public decimal this[int index] => values[index];

        public IEnumerator<decimal> GetEnumerator()
        {
            if (++EnumerationCount > 1)
                throw new InvalidOperationException("La colección se enumeró más de una vez.");

            return ((IEnumerable<decimal>)values).GetEnumerator();
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
            => GetEnumerator();
    }
}
