using Microsoft.VisualStudio.TestTools.UnitTesting;
using Sopro.Calculation;
using SOPRO.Application.Models.Presupuesto;
using SOPRO.Application.Services;
using SOPRO.Core.Entities;

namespace SOPRO.Tests.Services;

[TestClass]
public class MotorCalculoSoproTests
{
    [TestMethod]
    public void ToPricePercentage_MapeaLosSieteCamposCorrectamente()
    {
        var input = new BudgetPercentageInput
        {
            CostoDirectoReferencia = 123.456m,
            IndirectosCentral      = 10m,
            IndirectosCampo        = 5m,
            Financiamiento         = 2m,
            Utilidad               = 3m,
            CargosAdicionales      = 1m,
            ModoCalculoPorcentajes = "SobreCD"
        };

        var result = input.ToPricePercentage();

        Assert.AreEqual(123.456m, result.ReferenceDirectCost);
        Assert.AreEqual(10m, result.CentralIndirectsPercentage);
        Assert.AreEqual(5m, result.FieldIndirectsPercentage);
        Assert.AreEqual(2m, result.FinancingPercentage);
        Assert.AreEqual(3m, result.ProfitPercentage);
        Assert.AreEqual(1m, result.AdditionalChargesPercentage);
        Assert.AreEqual(PercentageCalculationMode.OverDirectCost, result.Mode);
    }

    [TestMethod]
    public void ToPricePercentage_CaseInsensitive_SobreCD()
    {
        var input = new BudgetPercentageInput { ModoCalculoPorcentajes = "sobrecd" };
        Assert.AreEqual(PercentageCalculationMode.OverDirectCost, input.ToPricePercentage().Mode);
    }

    [TestMethod]
    public void ToPricePercentage_CaseInsensitive_SOBRECD()
    {
        var input = new BudgetPercentageInput { ModoCalculoPorcentajes = "SOBRECD" };
        Assert.AreEqual(PercentageCalculationMode.OverDirectCost, input.ToPricePercentage().Mode);
    }

    [TestMethod]
    public void ToPricePercentage_Acumulables()
    {
        var input = new BudgetPercentageInput { ModoCalculoPorcentajes = "Acumulables" };
        Assert.AreEqual(PercentageCalculationMode.Accumulative, input.ToPricePercentage().Mode);
    }

    [TestMethod]
    public void ToPricePercentage_Null_RetornaAcumulables()
    {
        var input = new BudgetPercentageInput { ModoCalculoPorcentajes = null! };
        Assert.AreEqual(PercentageCalculationMode.Accumulative, input.ToPricePercentage().Mode);
    }

    [TestMethod]
    public void FromProyecto_MapeaCamposYModoSobreCD()
    {
        var proyecto = new Proyecto
        {
            PorcentajeIndirectosCentral = 10m,
            PorcentajeIndirectosCampo = 5m,
            PorcentajeFinanciamiento = 2m,
            PorcentajeUtilidad = 3m,
            PorcentajeCargosAdicionales = 1m,
            ModoCalculoPorcentajes = "SobreCD"
        };

        var pct = BudgetPercentageInput.FromProyecto(proyecto);

        Assert.AreEqual(10m, pct.IndirectosCentral);
        Assert.AreEqual(5m, pct.IndirectosCampo);
        Assert.AreEqual(2m, pct.Financiamiento);
        Assert.AreEqual(3m, pct.Utilidad);
        Assert.AreEqual(1m, pct.CargosAdicionales);
        Assert.AreEqual("SobreCD", pct.ModoCalculoPorcentajes);
        Assert.AreEqual(PercentageCalculationMode.OverDirectCost, pct.ToPricePercentage().Mode);
    }

    [TestMethod]
    public void FromProyecto_ModoNulo_PreservaDefaultAcumulables()
    {
        var proyecto = new Proyecto { ModoCalculoPorcentajes = null! };

        var pct = BudgetPercentageInput.FromProyecto(proyecto);

        Assert.AreEqual("Acumulables", pct.ModoCalculoPorcentajes);
        Assert.AreEqual(PercentageCalculationMode.Accumulative, pct.ToPricePercentage().Mode);
    }

    [TestMethod]
    public void FromProyecto_ProyectoNulo_Lanza()
    {
        Assert.ThrowsException<ArgumentNullException>(
            () => BudgetPercentageInput.FromProyecto(null!));
    }

    [TestMethod]
    public void Multiplicar_DebeUsarPrecioUnitarioVisibleAntesDeMultiplicar()
    {
        // Arrange
        // Regla SOPRO: el P.U. se redondea primero a la precisión visible.
        // 13.3875 visible a 2 decimales = 13.39; 652 * 13.39 = 8,730.28.
        var motor = new MotorCalculoSopro(
            decimalesCantidad: 2,
            decimalesImporte: 2,
            decimalesPorcentaje: 4);

        // Act
        var resultado = motor.Multiplicar(652m, 13.3875m);

        // Assert
        Assert.AreEqual(8730.28m, resultado);
    }

    [TestMethod]
    public void RedondearImporte_DebeUsarAwayFromZero()
    {
        var motor = new MotorCalculoSopro(
            decimalesCantidad: 2,
            decimalesImporte: 2,
            decimalesPorcentaje: 4);

        Assert.AreEqual(10.01m, motor.RedondearImporte(10.005m));
        Assert.AreEqual(-10.01m, motor.RedondearImporte(-10.005m));
    }

    [TestMethod]
    public void DistribuirImporte_DebeAjustarResiduoEnUltimoPeriodo()
    {
        var motor = new MotorCalculoSopro(
            decimalesCantidad: 2,
            decimalesImporte: 2,
            decimalesPorcentaje: 4);

        var resultado = motor.DistribuirImporte(1000m, new List<decimal> { 33m, 33m, 34m });

        Assert.AreEqual(330.00m, resultado[0]);
        Assert.AreEqual(330.00m, resultado[1]);
        Assert.AreEqual(340.00m, resultado[2]);
        Assert.AreEqual(1000.00m, resultado.Sum());
    }

    [TestMethod]
    public void DistribuirCantidad_DebeRespetarPrecisionDeCantidadYAjustarResiduo()
    {
        var motor = new MotorCalculoSopro(
            decimalesCantidad: 3,
            decimalesImporte: 2,
            decimalesPorcentaje: 4);

        var resultado = motor.DistribuirCantidad(1m, new List<decimal> { 1m, 1m, 1m });

        Assert.AreEqual(0.333m, resultado[0]);
        Assert.AreEqual(0.333m, resultado[1]);
        Assert.AreEqual(0.334m, resultado[2]);
        Assert.AreEqual(1.000m, resultado.Sum());
    }

    [TestMethod]
    public void SumarCostoDirecto_DebeIgnorarAgrupadoresYConceptosSinMatriz()
    {
        var motor = new MotorCalculoSopro(
            decimalesCantidad: 2,
            decimalesImporte: 2,
            decimalesPorcentaje: 4);

        var conceptos = new List<ConceptoPresupuesto>
        {
            new()
            {
                EsAgrupador = true,
                Cantidad = 999m,
                CostoDirectoUnitario = 999m,
                MatrizId = 1
            },
            new()
            {
                EsAgrupador = false,
                Cantidad = 10m,
                CostoDirectoUnitario = 100.005m,
                MatrizId = 2
            },
            new()
            {
                EsAgrupador = false,
                Cantidad = 5m,
                CostoDirectoUnitario = 250m,
                MatrizId = null
            }
        };

        var total = motor.SumarCostoDirecto(conceptos);

        // Solo entra el concepto hoja con MatrizId.
        // P.U. visible: 100.01; 10 * 100.01 = 1,000.10.
        Assert.AreEqual(1000.10m, total);
    }

    [TestMethod]
    public void CalcularPrecioUnitario_DebeCuadrarSumaDePartesConPrecioFinal()
    {
        var motor = new MotorCalculoSopro(
            decimalesCantidad: 2,
            decimalesImporte: 2,
            decimalesPorcentaje: 4);

        var porcentajes = new BudgetPercentageInput
        {
            IndirectosCentral = 5m,
            IndirectosCampo = 10m,
            Financiamiento = 2m,
            Utilidad = 8m,
            CargosAdicionales = 1m,
            ModoCalculoPorcentajes = "Acumulables"
        };

        var desglose = motor.CalcularPrecioUnitario(1000m, porcentajes);

        var sumaPartes = desglose.CostoDirecto
            + desglose.Indirectos
            + desglose.Financiamiento
            + desglose.Utilidad
            + desglose.CargosAdicionales;

        Assert.AreEqual(desglose.PrecioUnitario, sumaPartes);
        Assert.AreEqual(1000.00m, desglose.CostoDirecto);
        Assert.AreEqual(150.00m, desglose.Indirectos);
        Assert.AreEqual(23.00m, desglose.Financiamiento);
        Assert.AreEqual(93.84m, desglose.Utilidad);
        Assert.AreEqual(12.67m, desglose.CargosAdicionales);
        Assert.AreEqual(1279.51m, desglose.PrecioUnitario);
    }

    [TestMethod]
    public void Constructor_ConProyectoNulo_LanzaArgumentNullException()
    {
        var ex = Assert.ThrowsException<ArgumentNullException>(
            () => new MotorCalculoSopro((Proyecto)null!));

        Assert.AreEqual("proyecto", ex.ParamName);
    }

    [TestMethod]
    public void CalcularPrecioUnitario_ConDecimalesPorcentajeDistintos_DesglosePreciosPermaneceIdentico()
    {
        // DecimalesPorcentaje NO debe participar en la cascada económica:
        // el desglose usa RedondearImporte en cada paso, nunca RedondearPorcentaje.
        var motorCon4 = new MotorCalculoSopro(
            decimalesCantidad: 2,
            decimalesImporte: 2,
            decimalesPorcentaje: 4);
        var motorCon0 = new MotorCalculoSopro(
            decimalesCantidad: 2,
            decimalesImporte: 2,
            decimalesPorcentaje: 0);

        var porcentajes = new BudgetPercentageInput
        {
            IndirectosCentral = 5.25m,
            IndirectosCampo = 10.75m,
            Financiamiento = 3.5m,
            Utilidad = 8.25m,
            CargosAdicionales = 1.5m,
            ModoCalculoPorcentajes = "Acumulables"
        };

        var con4 = motorCon4.CalcularPrecioUnitario(1234.56m, porcentajes);
        var con0 = motorCon0.CalcularPrecioUnitario(1234.56m, porcentajes);

        Assert.AreEqual(con4, con0);
        Assert.AreEqual(1234.56m, con4.CostoDirecto);
        Assert.AreEqual(197.53m, con4.Indirectos);
        Assert.AreEqual(50.12m, con4.Financiamiento);
        Assert.AreEqual(122.28m, con4.Utilidad);
        Assert.AreEqual(24.07m, con4.CargosAdicionales);
        Assert.AreEqual(1628.56m, con4.PrecioUnitario);
    }

    [TestMethod]
    public void RedondearPorcentaje_ConDecimalesPorcentajeDistintos_SiCambiaLaPrecision()
    {
        // Contraste del test anterior: RedondearPorcentaje SÍ depende de DecimalesPorcentaje.
        var motorCon4 = new MotorCalculoSopro(
            decimalesCantidad: 2,
            decimalesImporte: 2,
            decimalesPorcentaje: 4);
        var motorCon0 = new MotorCalculoSopro(
            decimalesCantidad: 2,
            decimalesImporte: 2,
            decimalesPorcentaje: 0);

        Assert.AreEqual(12.3457m, motorCon4.RedondearPorcentaje(12.34565m));
        Assert.AreEqual(12m, motorCon0.RedondearPorcentaje(12.34565m));
    }

    [TestMethod]
    public void Constructor_ConProyecto_UsaPrecisionesConfiguradas()
    {
        var proyecto = new Proyecto
        {
            DecimalesCantidad = 3,
            DecimalesImporte = 4,
            DecimalesPorcentaje = 5
        };

        var motor = new MotorCalculoSopro(proyecto);

        Assert.AreEqual(1.001m, motor.RedondearCantidad(1.0005m));
        Assert.AreEqual(1.0001m, motor.RedondearImporte(1.00005m));
        Assert.AreEqual(1.00001m, motor.RedondearPorcentaje(1.000005m));
    }

    [TestMethod]
    public void Constructor_ConPrecisionesNegativas_NormalizaACero()
    {
        var motor = new MotorCalculoSopro(
            decimalesCantidad: -1,
            decimalesImporte: -2,
            decimalesPorcentaje: -3);

        Assert.AreEqual(1m, motor.RedondearCantidad(0.5m));
        Assert.AreEqual(1m, motor.RedondearImporte(0.5m));
        Assert.AreEqual(1m, motor.RedondearPorcentaje(0.5m));
    }

    [TestMethod]
    public void Multiplicar_NoRedondeaLaCantidadPreviamente()
    {
        // Si la cantidad 1.234 se redondeara a 2 decimales daría 1.23 * 10 = 12.30;
        // el motor usa la cantidad completa: 1.234 * 10 = 12.34.
        var motor = new MotorCalculoSopro(
            decimalesCantidad: 2,
            decimalesImporte: 2,
            decimalesPorcentaje: 4);

        var resultado = motor.Multiplicar(1.234m, 10m);

        Assert.AreEqual(12.34m, resultado);
    }

    [TestMethod]
    public void CalcularImporteSobreBase_UsaPrecioVisibleAntesDeMultiplicar()
    {
        var motor = new MotorCalculoSopro(
            decimalesCantidad: 2,
            decimalesImporte: 2,
            decimalesPorcentaje: 4);

        var resultado = motor.CalcularImporteSobreBase(3m, 10.005m);

        Assert.AreEqual(30.03m, resultado);
    }

    [TestMethod]
    public void CalcularPrecioUnitario_ConPorcentajesNulos_LanzaArgumentNullException()
    {
        var motor = new MotorCalculoSopro(
            decimalesCantidad: 2,
            decimalesImporte: 2,
            decimalesPorcentaje: 4);

        var ex = Assert.ThrowsException<ArgumentNullException>(
            () => motor.CalcularPrecioUnitario(1000m, null!));

        Assert.AreEqual("pct", ex.ParamName);
    }

    [TestMethod]
    public void CalcularPrecioUnitario_ModoSobreCDConCasingDistinto_ProducePrecioEsperado()
    {
        var motor = new MotorCalculoSopro(
            decimalesCantidad: 2,
            decimalesImporte: 2,
            decimalesPorcentaje: 4);

        var porcentajes = new BudgetPercentageInput
        {
            IndirectosCentral = 5m,
            IndirectosCampo = 10m,
            Financiamiento = 2m,
            Utilidad = 8m,
            CargosAdicionales = 1m,
            ModoCalculoPorcentajes = "sObReCd"
        };

        var desglose = motor.CalcularPrecioUnitario(1000m, porcentajes);

        Assert.AreEqual(1000m, desglose.CostoDirecto);
        Assert.AreEqual(150m, desglose.Indirectos);
        Assert.AreEqual(20m, desglose.Financiamiento);
        Assert.AreEqual(80m, desglose.Utilidad);
        Assert.AreEqual(10m, desglose.CargosAdicionales);
        Assert.AreEqual(1260m, desglose.PrecioUnitario);
    }

    [TestMethod]
    public void DesglosePrecios_ProrrateaIndirectosConSeisDecimales()
    {
        var desglose = new DesglosePrecios(
            CostoDirecto: 100m,
            Indirectos: 0.01m,
            Financiamiento: 0m,
            Utilidad: 0m,
            CargosAdicionales: 0m,
            PrecioUnitario: 100m,
            PctIndirectosCentral: 5m,
            PctIndirectosCampo: 10m);

        Assert.AreEqual(0.003333m, desglose.IndirectosCentral);
        Assert.AreEqual(0.006667m, desglose.IndirectosCampo);
    }

    [TestMethod]
    public void Distribuciones_ConPesosNulosOVacios_DevuelvenColeccionesVacias()
    {
        var motor = new MotorCalculoSopro(
            decimalesCantidad: 2,
            decimalesImporte: 2,
            decimalesPorcentaje: 4);

        var importeNulo = motor.DistribuirImporte(100m, null!);
        var importeVacio = motor.DistribuirImporte(100m, new List<decimal>());
        var cantidadNula = motor.DistribuirCantidad(100m, null!);
        var cantidadVacia = motor.DistribuirCantidad(100m, new List<decimal>());

        Assert.AreEqual(0, importeNulo.Count);
        Assert.AreEqual(0, importeVacio.Count);
        Assert.AreEqual(0, cantidadNula.Count);
        Assert.AreEqual(0, cantidadVacia.Count);
    }

    [TestMethod]
    public void Distribuciones_ConSumaDePesosCero_DevuelvenUnCeroPorPeriodo()
    {
        var motor = new MotorCalculoSopro(
            decimalesCantidad: 2,
            decimalesImporte: 2,
            decimalesPorcentaje: 4);

        var importes = motor.DistribuirImporte(100m, new List<decimal> { 0m, 0m, 0m });
        var cantidades = motor.DistribuirCantidad(100m, new List<decimal> { 0m, 0m, 0m });

        Assert.AreEqual(3, importes.Count);
        Assert.AreEqual(3, cantidades.Count);
        CollectionAssert.AreEqual(new[] { 0m, 0m, 0m }, importes.ToArray());
        CollectionAssert.AreEqual(new[] { 0m, 0m, 0m }, cantidades.ToArray());
    }

    [TestMethod]
    public void DistribuirImporte_ConPesosIguales_AjustaResiduoEnUltimoPeriodo()
    {
        var motor = new MotorCalculoSopro(
            decimalesCantidad: 2,
            decimalesImporte: 2,
            decimalesPorcentaje: 4);

        var resultado = motor.DistribuirImporte(100.005m, new List<decimal> { 1m, 1m, 1m });

        // Comportamiento legacy: 1m/3m trunca a 28 dígitos (0.3333...333), por lo que
        // 100.005m * proporcion = 33.3349999...997 y redondea hacia abajo a 33.33.
        // El residuo real cae en el último periodo: 100.005 - 66.66 = 33.345 -> 33.35.
        Assert.AreEqual(33.33m, resultado[0]);
        Assert.AreEqual(33.33m, resultado[1]);
        Assert.AreEqual(33.35m, resultado[2]);
        Assert.AreEqual(100.01m, resultado.Sum());
    }

    [TestMethod]
    public void Sumas_ConColeccionesNulas_DevuelvenCero()
    {
        var motor = new MotorCalculoSopro(
            decimalesCantidad: 2,
            decimalesImporte: 2,
            decimalesPorcentaje: 4);

        Assert.AreEqual(0m, motor.SumarImportes(null!));
        Assert.AreEqual(0m, motor.SumarCantidades(null!));
        Assert.AreEqual(0m, motor.SumarCostoDirecto(null!));
    }

    [TestMethod]
    public void SumarCantidades_ConTresDecimales_RedondeaCadaElementoAntesDeAcumular()
    {
        var motor = new MotorCalculoSopro(
            decimalesCantidad: 3,
            decimalesImporte: 2,
            decimalesPorcentaje: 4);

        var resultado = motor.SumarCantidades(new[] { 1.2345m, 2.3455m });

        Assert.AreEqual(3.581m, resultado);
    }
}
