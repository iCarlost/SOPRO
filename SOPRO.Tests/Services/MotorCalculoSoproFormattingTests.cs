using System;
using System.Globalization;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SOPRO.Application.Services;

namespace SOPRO.Tests.Services;

[TestClass]
[DoNotParallelize]
public class MotorCalculoSoproFormattingTests
{
    [TestMethod]
    public void FormatCantidad_ConCulturaControlada_UsaSeparadoresPersonalizados()
    {
        using var scope = new CultureScope(BuildCulturaControlada());
        var motor = new MotorCalculoSopro(decimalesCantidad: 3, decimalesImporte: 2, decimalesPorcentaje: 4);

        var resultado = motor.FormatCantidad(1234.5678m);

        Assert.AreEqual("1_234~568", resultado);
    }

    [TestMethod]
    public void FormatImporte_ConCulturaControlada_UsaSimboloMonetarioPersonalizado()
    {
        using var scope = new CultureScope(BuildCulturaControlada());
        var motor = new MotorCalculoSopro(decimalesCantidad: 3, decimalesImporte: 2, decimalesPorcentaje: 4);

        var resultado = motor.FormatImporte(1234.5678m);

        Assert.AreEqual("MX$1_234~57", resultado);
    }

    [TestMethod]
    public void FormatPorcentaje_ConCulturaControlada_NoAgregaSimboloDePorcentaje()
    {
        using var scope = new CultureScope(BuildCulturaControlada());
        var motor = new MotorCalculoSopro(decimalesCantidad: 3, decimalesImporte: 2, decimalesPorcentaje: 4);

        var resultado = motor.FormatPorcentaje(12.34567m);

        Assert.AreEqual("12~3457", resultado);
        StringAssert.DoesNotMatch(resultado, new System.Text.RegularExpressions.Regex("%"));
    }

    [TestMethod]
    public void FormatNumero_ConUnDecimal_UsaSeparadoresPersonalizados()
    {
        using var scope = new CultureScope(BuildCulturaControlada());
        var motor = new MotorCalculoSopro(decimalesCantidad: 3, decimalesImporte: 2, decimalesPorcentaje: 4);

        var resultado = motor.FormatNumero(1234.5678m, 1);

        Assert.AreEqual("1_234~6", resultado);
    }

    [TestMethod]
    public void FormatPorcentaje_ConPrecisionesDistintas_ExponeLaDiferenciaVisible()
    {
        // 50.0000m == 50m como decimal; la diferencia solo es visible en el formato.
        using var scope = new CultureScope(CultureInfo.InvariantCulture);
        var motorCon4 = new MotorCalculoSopro(decimalesCantidad: 2, decimalesImporte: 2, decimalesPorcentaje: 4);
        var motorCon0 = new MotorCalculoSopro(decimalesCantidad: 2, decimalesImporte: 2, decimalesPorcentaje: 0);

        Assert.AreEqual("50.0000", motorCon4.FormatPorcentaje(50m));
        Assert.AreEqual("50", motorCon0.FormatPorcentaje(50m));
        Assert.AreEqual(50m, motorCon4.RedondearPorcentaje(50m));
        Assert.AreEqual(50m, motorCon0.RedondearPorcentaje(50m));
    }

    private static CultureInfo BuildCulturaControlada()
    {
        var cultura = (CultureInfo)CultureInfo.InvariantCulture.Clone();
        cultura.NumberFormat.NumberDecimalSeparator = "~";
        cultura.NumberFormat.NumberGroupSeparator = "_";
        cultura.NumberFormat.NumberGroupSizes = new[] { 3 };
        cultura.NumberFormat.CurrencySymbol = "MX$";
        cultura.NumberFormat.CurrencyDecimalSeparator = "~";
        cultura.NumberFormat.CurrencyGroupSeparator = "_";
        cultura.NumberFormat.CurrencyGroupSizes = new[] { 3 };
        cultura.NumberFormat.PercentDecimalSeparator = "~";
        cultura.NumberFormat.PercentGroupSeparator = "_";
        cultura.NumberFormat.PercentGroupSizes = new[] { 3 };
        return cultura;
    }

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
