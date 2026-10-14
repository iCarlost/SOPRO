using Microsoft.VisualStudio.TestTools.UnitTesting;
using SOPRO.Application.Services;

namespace SOPRO.Tests.Services.Presupuesto;

// Regresión: ConvertirEnteroALetras dejaba la unidad en minúscula tras los
// prefijos DIECI/VEINTI (p.ej. 29 → "VEINTInueve"). Todo el segmento de letras
// debe salir SIEMPRE en MAYÚSCULAS.
[TestClass]
public class BudgetPricingServiceNumerosALetrasTests
{
    [TestMethod]
    public void ConvertirALetras_Veintinueve_DebeSerMayusculas()
    {
        Assert.AreEqual("VEINTINUEVE PESOS 00/100 M.N.", BudgetPricingService.ConvertirALetras(29m));
    }

    [TestMethod]
    public void ConvertirALetras_CasosLimite()
    {
        var casos = new (decimal Numero, string Esperado)[]
        {
            (0m,       "CERO PESOS 00/100 M.N."),
            (15m,      "QUINCE PESOS 00/100 M.N."),
            (16m,      "DIECISEIS PESOS 00/100 M.N."),
            (21m,      "VEINTIUNO PESOS 00/100 M.N."),
            (22m,      "VEINTIDOS PESOS 00/100 M.N."),
            (26m,      "VEINTISEIS PESOS 00/100 M.N."),
            (29m,      "VEINTINUEVE PESOS 00/100 M.N."),
            (30m,      "TREINTA PESOS 00/100 M.N."),
            (100m,     "CIEN PESOS 00/100 M.N."),
            (101m,     "CIENTO UNO PESOS 00/100 M.N."),
            (1000m,    "MIL PESOS 00/100 M.N."),
            (1000000m, "UN MILLON PESOS 00/100 M.N."),
        };

        foreach (var (numero, esperado) in casos)
            Assert.AreEqual(esperado, BudgetPricingService.ConvertirALetras(numero), $"numero={numero}");
    }

    [TestMethod]
    public void ConvertirALetras_SegmentoLetras_SinMinusculas_0a999()
    {
        for (int n = 0; n <= 999; n++)
        {
            string texto = BudgetPricingService.ConvertirALetras(n);
            string letras = texto[..texto.IndexOf(" PESOS")];

            foreach (char c in letras)
                Assert.IsFalse(char.IsLower(c), $"n={n} → '{letras}' contiene minúscula '{c}'");
        }
    }

    [TestMethod]
    public void ConvertirALetras_SegmentoLetras_SinMinusculas_RangoAmplio()
    {
        // Rango secuencial amplio (incluye límites de centenas/miles) + muestra aleatoria.
        var numeros = new List<int>();
        for (int n = 0; n <= 2000; n++)
            numeros.Add(n);

        var rnd = new Random(20261014);
        for (int i = 0; i < 500; i++)
            numeros.Add(rnd.Next(0, 10_000_000));

        foreach (int n in numeros)
        {
            string texto = BudgetPricingService.ConvertirALetras(n);
            string letras = texto[..texto.IndexOf(" PESOS")];

            foreach (char c in letras)
                Assert.IsFalse(char.IsLower(c), $"n={n} → '{letras}' contiene minúscula '{c}'");
        }
    }
}
