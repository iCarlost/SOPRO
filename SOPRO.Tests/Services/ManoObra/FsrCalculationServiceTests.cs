using Microsoft.VisualStudio.TestTools.UnitTesting;
using SOPRO.Application.Services;

namespace SOPRO.Tests.Services.ManoObra;

[TestClass]
public class FsrCalculationServiceTests
{
    [TestMethod]
    public void Calcular_SinParametrosODatosInvalidos_DebeRegresarNull()
    {
        Assert.IsNull(FsrCalculationService.Calcular(null, 500m));
        Assert.IsNull(FsrCalculationService.Calcular(string.Empty, 500m));
        Assert.IsNull(FsrCalculationService.Calcular("{ json invalido", 500m));
        Assert.IsNull(FsrCalculationService.Calcular("{}", 0m));
    }

    [TestMethod]
    public void Calcular_ConParametrosBase_DebeRegresarFactorMayorQueUno()
    {
        const string parametros = """
        {
          "SalarioMinimo":"248.93",
          "Jornada":"0",
          "Semestre":"0",
          "Anio":"2026",
          "HorasJornada":"8",
          "DiasCalendario":"365",
          "DiasAguinaldo":"15",
          "DiasVacaciones":"12",
          "PrimaVacacional":"25",
          "DiasDescanso":"52",
          "DiasFestivos":"7",
          "PctGuarderias":"1",
          "PctRetiro":"2",
          "PctRiesgos":"4.58875",
          "PctINFONAVIT":"5",
          "PctNomina":"2.4"
        }
        """;

        var factor = FsrCalculationService.Calcular(parametros, 500m);

        Assert.IsTrue(factor.HasValue);
        Assert.IsTrue(factor.Value > 1m);
        Assert.IsTrue(factor.Value < 3m);
    }

    [TestMethod]
    public void Calcular_ConParametrosBase_DebeRegresarFactorDoradoExacto()
    {
        const string parametros = """
        {
          "SalarioMinimo":"248.93",
          "Jornada":"0",
          "Semestre":"0",
          "Anio":"2026",
          "HorasJornada":"8",
          "DiasCalendario":"365",
          "DiasAguinaldo":"15",
          "DiasVacaciones":"12",
          "PrimaVacacional":"25",
          "DiasDescanso":"52",
          "DiasFestivos":"7",
          "PctGuarderias":"1",
          "PctRetiro":"2",
          "PctRiesgos":"4.58875",
          "PctINFONAVIT":"5",
          "PctNomina":"2.4"
        }
        """;

        var factor = FsrCalculationService.Calcular(parametros, 500m);

        Assert.IsNotNull(factor);
        Assert.AreEqual(1.7308240339418507128878948841m, factor.Value);
    }

    [TestMethod]
    public void Calcular_ConDiferentesSalarios_DebeRegresarFactoresDorados()
    {
        const string parametros = """
        {
          "SalarioMinimo":"248.93",
          "Jornada":"0",
          "Semestre":"0",
          "Anio":"2026",
          "HorasJornada":"8",
          "DiasCalendario":"365",
          "DiasAguinaldo":"15",
          "DiasVacaciones":"12",
          "PrimaVacacional":"25",
          "DiasDescanso":"52",
          "DiasFestivos":"7",
          "PctGuarderias":"1",
          "PctRetiro":"2",
          "PctRiesgos":"4.58875",
          "PctINFONAVIT":"5",
          "PctNomina":"2.4"
        }
        """;

        var f250 = FsrCalculationService.Calcular(parametros, 250m);
        var f800 = FsrCalculationService.Calcular(parametros, 800m);

        Assert.IsNotNull(f250);
        Assert.IsNotNull(f800);
        Assert.AreEqual(1.8631328690438915292144254962m, f250.Value);
        Assert.AreEqual(1.6828680219556658279750256267m, f800.Value);
    }
}
