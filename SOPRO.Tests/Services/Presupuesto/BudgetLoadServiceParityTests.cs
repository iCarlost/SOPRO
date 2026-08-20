using System.Globalization;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SOPRO.Application.Models.Presupuesto;
using SOPRO.Application.Services;
using SOPRO.Core.Entities;

namespace SOPRO.Tests.Services.Presupuesto;

[TestClass]
public class BudgetLoadServiceParityTests
{
    // ── N5-6: BuildRowDisplay migró su aritmética de MotorCalculoSopro a
    //    SoproCalculationEngine (CalculateUnitPrice con PricePercentageInput
    //    inline, Multiply, RoundAmount). El formato (FormatCantidad/Importe/
    //    Porcentaje) permanece en la fachada por N0 fila 9.
    //    Paridad exacta contra referencia compuesta con primitivas de la
    //    fachada (oráculo diferencial, 23 entradas del diccionario) + dorados.

    [TestMethod]
    public void BuildRowDisplay_ConceptoHoja_ParidadConFachada_BateriaAleatoriaConSemilla()
    {
        var rnd = new Random(161803398);

        for (int iter = 0; iter < 600; iter++)
        {
            var proyecto = CrearProyectoAleatorio(rnd);
            var concepto = new ConceptoPresupuesto
            {
                MatrizId = iter + 1,
                EsAgrupador = false,
                Nivel = rnd.Next(0, 6),
                Clave = "C" + iter,
                Descripcion = "Concepto " + iter,
                Unidad = rnd.Next(0, 2) == 0 ? "pza" : "ml",
                Cantidad = Valor(rnd),
                CostoDirectoUnitario = Valor(rnd),
                CostoDirectoTotal = Valor(rnd)
            };

            var actual = BudgetLoadService.BuildRowDisplay(proyecto, concepto);
            var esperado = BuildRowDisplayConFachada(proyecto, concepto);

            CompararDiccionarios(esperado, actual.ValuesByInternalName, iter);
        }
    }

    [TestMethod]
    public void BuildRowDisplay_Agrupador_ParidadConFachada_BateriaAleatoriaConSemilla()
    {
        var rnd = new Random(271828183);

        for (int iter = 0; iter < 200; iter++)
        {
            var proyecto = CrearProyectoAleatorio(rnd);
            var concepto = new ConceptoPresupuesto
            {
                MatrizId = iter + 1,
                EsAgrupador = true,
                Nivel = rnd.Next(0, 8),
                Clave = "G" + iter,
                Descripcion = "Agrupador " + iter,
                CostoDirectoTotal = Valor(rnd)
            };

            var actual = BudgetLoadService.BuildRowDisplay(proyecto, concepto);
            var esperado = BuildRowDisplayConFachada(proyecto, concepto);

            CompararDiccionarios(esperado, actual.ValuesByInternalName, iter);
        }
    }

    [TestMethod]
    public void BuildRowDisplay_Dorado_ConceptoHoja()
    {
        using var cultura = new CultureScope(CultureInfo.GetCultureInfo("es-MX"));

        var proyecto = CrearProyecto(2, 2, 2, 16m);
        var concepto = new ConceptoPresupuesto
        {
            MatrizId = 1,
            EsAgrupador = false,
            Nivel = 0,
            Clave = "K01",
            Descripcion = "Concepto dorado",
            Unidad = "pza",
            Cantidad = 10m,
            CostoDirectoUnitario = 100m
        };

        var actual = BudgetLoadService.BuildRowDisplay(proyecto, concepto).ValuesByInternalName;

        // CD 100; 5/5/2/8/3 acumulables → Indirectos 10.00, Financiamiento 2.20,
        // Utilidad 8.98, PU 124.82; 10×124.82 = 1248.20; IVA 16% → 199.71; total 1447.91
        Assert.AreEqual("Concepto", actual["Tipo"]);
        Assert.AreEqual("K01", actual["Clave"]);
        Assert.AreEqual("Concepto dorado", actual["Descripcion"]);
        Assert.AreEqual("pza", actual["Unidad"]);
        Assert.AreEqual("10.00", actual["Cantidad"]);
        Assert.AreEqual("$124.82", actual["PrecioUnitario"]);
        Assert.AreEqual("$1,248.20", actual["Importe"]);
        Assert.AreEqual("10.00%", actual["PorcentajeIndirectos"]);
        Assert.AreEqual("2.00%", actual["PorcentajeFinanciamiento"]);
        Assert.AreEqual("8.00%", actual["PorcentajeUtilidad"]);
        Assert.AreEqual(string.Empty, actual["CargosAdicionales"]);
        Assert.AreEqual(string.Empty, actual["Observaciones"]);
        Assert.AreEqual("$10.00", actual["Indirectos"]);
        Assert.AreEqual("$2.20", actual["Financiamiento"]);
        Assert.AreEqual("$8.98", actual["Utilidad"]);
        Assert.AreEqual("$124.82", actual["PrecioUnitarioFinal"]);
        Assert.AreEqual("$1,248.20", actual["Subtotal"]);
        Assert.AreEqual("$199.71", actual["IVA"]);
        Assert.AreEqual("$1,447.91", actual["Total"]);
        Assert.AreEqual(BudgetPricingService.ConvertirALetras(124.82m), actual["PrecioUnitarioLetra"]);
        Assert.AreEqual(BudgetPricingService.ConvertirALetras(1447.91m), actual["TotalLetra"]);
        Assert.AreEqual(string.Empty, actual["IncidenciaPorcentaje"]);
        Assert.AreEqual(string.Empty, actual["ImporteManoObra"]);
    }

    [TestMethod]
    public void BuildRowDisplay_Dorado_Agrupador()
    {
        using var cultura = new CultureScope(CultureInfo.GetCultureInfo("es-MX"));

        var proyecto = CrearProyecto(2, 2, 2, 16m);
        var concepto = new ConceptoPresupuesto
        {
            MatrizId = 1,
            EsAgrupador = true,
            Nivel = 2,
            Clave = "G01",
            Descripcion = "Capítulo dorado",
            CostoDirectoTotal = 5432.10m
        };

        var actual = BudgetLoadService.BuildRowDisplay(proyecto, concepto).ValuesByInternalName;

        Assert.AreEqual("Nivel 1", actual["Tipo"]);
        Assert.AreEqual("G01", actual["Clave"]);
        Assert.AreEqual("Capítulo dorado", actual["Descripcion"]);
        Assert.AreEqual(string.Empty, actual["Unidad"]);
        Assert.AreEqual(string.Empty, actual["Cantidad"]);
        Assert.AreEqual(string.Empty, actual["PrecioUnitario"]);
        Assert.AreEqual("$5,432.10", actual["Importe"]);
        Assert.AreEqual(string.Empty, actual["PorcentajeIndirectos"]);
        Assert.AreEqual(string.Empty, actual["PorcentajeFinanciamiento"]);
        Assert.AreEqual(string.Empty, actual["PorcentajeUtilidad"]);
        Assert.AreEqual(string.Empty, actual["CargosAdicionales"]);
        Assert.AreEqual(string.Empty, actual["Observaciones"]);
        Assert.AreEqual(string.Empty, actual["Indirectos"]);
        Assert.AreEqual(string.Empty, actual["Financiamiento"]);
        Assert.AreEqual(string.Empty, actual["Utilidad"]);
        Assert.AreEqual(string.Empty, actual["PrecioUnitarioFinal"]);
        Assert.AreEqual(string.Empty, actual["Subtotal"]);
        Assert.AreEqual(string.Empty, actual["IVA"]);
        Assert.AreEqual(string.Empty, actual["Total"]);
        Assert.AreEqual(string.Empty, actual["PrecioUnitarioLetra"]);
        Assert.AreEqual(string.Empty, actual["TotalLetra"]);
        Assert.AreEqual(string.Empty, actual["IncidenciaPorcentaje"]);
        Assert.AreEqual(string.Empty, actual["ImporteManoObra"]);
    }

    [TestMethod]
    public void BuildRowDisplay_Dorado_SinIva()
    {
        using var cultura = new CultureScope(CultureInfo.GetCultureInfo("es-MX"));

        var proyecto = CrearProyecto(2, 2, 2, 0m);
        var concepto = new ConceptoPresupuesto
        {
            MatrizId = 1,
            EsAgrupador = false,
            Cantidad = 10m,
            CostoDirectoUnitario = 100m
        };

        var actual = BudgetLoadService.BuildRowDisplay(proyecto, concepto).ValuesByInternalName;

        Assert.AreEqual("$1,248.20", actual["Subtotal"]);
        Assert.AreEqual("$0.00", actual["IVA"]);
        Assert.AreEqual("$1,248.20", actual["Total"]);
    }

    // ── Referencia compuesta con la fachada (código pre-migración) ────────────

    private static Dictionary<string, object?> BuildRowDisplayConFachada(
        Proyecto proyecto, ConceptoPresupuesto concepto)
    {
        var motor = new MotorCalculoSopro(proyecto);

        var pctInput = new BudgetPercentageInput
        {
            IndirectosCentral      = proyecto.PorcentajeIndirectosCentral,
            IndirectosCampo        = proyecto.PorcentajeIndirectosCampo,
            Financiamiento         = proyecto.PorcentajeFinanciamiento,
            Utilidad               = proyecto.PorcentajeUtilidad,
            CargosAdicionales      = proyecto.PorcentajeCargosAdicionales,
            ModoCalculoPorcentajes = proyecto.ModoCalculoPorcentajes ?? "Acumulables"
        };

        var values = new Dictionary<string, object?>();

        if (concepto.EsAgrupador)
        {
            values["Tipo"]           = TipoConFachada(concepto.Nivel, concepto.EsAgrupador);
            values["Clave"]          = concepto.Clave        ?? string.Empty;
            values["Descripcion"]    = concepto.Descripcion  ?? string.Empty;
            values["Unidad"]         = string.Empty;
            values["Cantidad"]       = string.Empty;
            values["PrecioUnitario"] = string.Empty;
            values["Importe"]        = motor.FormatImporte(concepto.CostoDirectoTotal);

            values["PorcentajeIndirectos"]     = string.Empty;
            values["PorcentajeFinanciamiento"] = string.Empty;
            values["PorcentajeUtilidad"]       = string.Empty;
            values["CargosAdicionales"]        = string.Empty;
            values["Observaciones"]            = string.Empty;
            values["Indirectos"]               = string.Empty;
            values["Financiamiento"]           = string.Empty;
            values["Utilidad"]                 = string.Empty;
            values["PrecioUnitarioFinal"]      = string.Empty;
            values["Subtotal"]                 = string.Empty;
            values["IVA"]                      = string.Empty;
            values["Total"]                    = string.Empty;
            values["PrecioUnitarioLetra"]      = string.Empty;
            values["TotalLetra"]               = string.Empty;
            values["IncidenciaPorcentaje"]     = string.Empty;
            values["ImporteManoObra"]          = string.Empty;
        }
        else
        {
            var desglose = motor.CalcularPrecioUnitario(concepto.CostoDirectoUnitario, pctInput);

            decimal pu      = desglose.PrecioUnitario;
            decimal importe = motor.Multiplicar(concepto.Cantidad, pu);

            decimal subtotal = importe;
            decimal tasaIva  = proyecto.PorcentajeIVA / 100m;
            decimal iva      = motor.RedondearImporte(subtotal * tasaIva);
            decimal total    = motor.RedondearImporte(subtotal + iva);

            decimal pctIndTotal = proyecto.PorcentajeIndirectosCentral
                                + proyecto.PorcentajeIndirectosCampo;

            values["Tipo"]           = TipoConFachada(concepto.Nivel, concepto.EsAgrupador);
            values["Clave"]          = concepto.Clave       ?? string.Empty;
            values["Descripcion"]    = concepto.Descripcion ?? string.Empty;
            values["Unidad"]         = concepto.Unidad      ?? string.Empty;
            values["Cantidad"]       = motor.FormatCantidad(concepto.Cantidad);
            values["PrecioUnitario"] = motor.FormatImporte(pu);
            values["Importe"]        = motor.FormatImporte(importe);

            values["PorcentajeIndirectos"]     = motor.FormatPorcentaje(pctIndTotal) + "%";
            values["PorcentajeFinanciamiento"] = motor.FormatPorcentaje(proyecto.PorcentajeFinanciamiento) + "%";
            values["PorcentajeUtilidad"]       = motor.FormatPorcentaje(proyecto.PorcentajeUtilidad) + "%";
            values["CargosAdicionales"]        = string.Empty;
            values["Observaciones"]            = string.Empty;

            values["Indirectos"]          = motor.FormatImporte(desglose.Indirectos);
            values["Financiamiento"]      = motor.FormatImporte(desglose.Financiamiento);
            values["Utilidad"]            = motor.FormatImporte(desglose.Utilidad);
            values["PrecioUnitarioFinal"] = motor.FormatImporte(pu);
            values["Subtotal"]            = motor.FormatImporte(subtotal);
            values["IVA"]                 = motor.FormatImporte(iva);
            values["Total"]               = motor.FormatImporte(total);
            values["PrecioUnitarioLetra"] = BudgetPricingService.ConvertirALetras(pu);
            values["TotalLetra"]          = BudgetPricingService.ConvertirALetras(total);
            values["IncidenciaPorcentaje"]= string.Empty;
            values["ImporteManoObra"]     = string.Empty;
        }

        return values;
    }

    private static string TipoConFachada(int nivel, bool esAgrupador)
    {
        if (!esAgrupador) return "Concepto";

        return nivel switch
        {
            0 => "Capitulo",
            1 => "Subcapitulo",
            2 => "Nivel 1",
            3 => "Nivel 2",
            4 => "Nivel 3",
            _ => "Nivel " + nivel
        };
    }

    private static void CompararDiccionarios(
        Dictionary<string, object?> esperado, Dictionary<string, object?> actual, int iter)
    {
        Assert.AreEqual(esperado.Count, actual.Count, $"iter={iter} cantidad de entradas");

        foreach (var clave in esperado.Keys)
        {
            Assert.IsTrue(actual.TryGetValue(clave, out var valorActual),
                $"iter={iter} falta la clave '{clave}'");
            Assert.AreEqual(esperado[clave], valorActual, $"iter={iter} clave '{clave}'");
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static Proyecto CrearProyecto(int decQty, int decAmt, int decPct, decimal iva) => new()
    {
        Nombre = "Proyecto carga de filas pruebas",
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
        proyecto.ModoCalculoPorcentajes = rnd.Next(0, 5) switch
        {
            0 => "Acumulables",
            1 => "SobreCD",
            2 => "sobrecd",
            3 => "SOBRECD",
            _ => null!
        };
        proyecto.PorcentajeIndirectosCentral = rnd.Next(0, 1500) / 100m;
        proyecto.PorcentajeIndirectosCampo = rnd.Next(0, 1500) / 100m;
        proyecto.PorcentajeFinanciamiento = rnd.Next(0, 500) / 100m;
        proyecto.PorcentajeUtilidad = rnd.Next(0, 2000) / 100m;
        proyecto.PorcentajeCargosAdicionales = rnd.Next(0, 500) / 100m;
        return proyecto;
    }

    private static decimal Valor(Random rnd)
        => rnd.Next(1, 1_000_000) / 1000m;

    private sealed class CultureScope : IDisposable
    {
        private readonly CultureInfo _original;

        public CultureScope(CultureInfo cultura)
        {
            _original = CultureInfo.CurrentCulture;
            CultureInfo.CurrentCulture = cultura;
        }

        public void Dispose()
        {
            CultureInfo.CurrentCulture = _original;
        }
    }
}
