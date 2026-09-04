using Microsoft.VisualStudio.TestTools.UnitTesting;
using SOPRO.Core.Entities;

namespace SOPRO.Tests.Services.CostoHorario;

/// <summary>
/// Caracterización N0 de la implementación legacy del costo horario.
/// La fórmula canónica vive en <see cref="Maquinaria.CalcularCostoHorario"/> (SOPRO.Core),
/// que es la misma aritmética que replica FormCalculoCostoHorario.Recalcular (WinForms).
/// </summary>
[TestClass]
public class MaquinariaCostoHorarioLegacyTests
{
    private static Maquinaria CrearMaquinaria(
        decimal valorAdquisicion,
        decimal valorLlantas,
        decimal valorPiezasEspeciales,
        decimal vidaEconomica,
        decimal horasAnio,
        decimal vidaEconomicaLlantas,
        decimal vidaPiezasEspeciales,
        decimal horasTurno)
    {
        return new Maquinaria
        {
            Clave = "MAQ-1",
            Descripcion = "Caracterización N0",
            ValorAdquisicion = valorAdquisicion,
            ValorLlantas = valorLlantas,
            ValorPiezasEspeciales = valorPiezasEspeciales,
            FactorRescate = 0.10m,
            VidaEconomica = vidaEconomica,
            TasaInteres = 21.24m,
            HorasEfectivasAnio = horasAnio,
            PrimaSeguro = 3.00m,
            FactorMantenimiento = 0.20m,
            CantidadCombustible = 10m,
            PrecioCombustible = 20m,
            CantidadAceite = 1m,
            PrecioAceite = 40m,
            VidaEconomicaLlantas = vidaEconomicaLlantas,
            VidaPiezasEspeciales = vidaPiezasEspeciales,
            SalarioOperador = 300m,
            FactorSalarioReal = 1.60m,
            HorasEfectivasTurno = horasTurno,
            Notas = string.Empty
        };
    }

    [TestMethod]
    public void CalcularCostoHorario_ConValoresHandComputed_DebeRegresarElGolden()
    {
        // Vm = 800,000 - 50,000 - 60,000 = 690,000
        // Vr = 690,000 × 0.10 = 69,000 ; (Vm+Vr)/2 = 379,500
        // Depreciación = (690,000 - 69,000) / 12,000 = 51.75
        // Inversión = 379,500 × 0.2124 / 1,600 = 50.378625
        // Seguros = 379,500 × 0.03 / 1,600 = 7.115625
        // Mantenimiento = 0.20 × 51.75 = 10.35
        // Cargos fijos = 119.59425
        // Combustible = 10 × 20 = 200 ; Lubricantes = 1 × 40 = 40
        // Llantas = 50,000 / 3,000 = 16.666666666666666666666666667
        // Piezas = 60,000 / 5,000 = 12 ; Consumos = 268.66666666666666666666666667
        // Operación = (300 × 1.60) / 8 = 60
        // TOTAL = 448.26091666666666666666666667
        var maquinaria = CrearMaquinaria(
            valorAdquisicion: 800000m,
            valorLlantas: 50000m,
            valorPiezasEspeciales: 60000m,
            vidaEconomica: 12000m,
            horasAnio: 1600m,
            vidaEconomicaLlantas: 3000m,
            vidaPiezasEspeciales: 5000m,
            horasTurno: 8m);

        maquinaria.CalcularCostoHorario();

        Assert.AreEqual(448.26091666666666666666666667m, maquinaria.CostoHorario);
    }

    [TestMethod]
    public void CalcularCostoHorario_CuandoVidaEconomicaEsCero_DepreciacionYMantenimientoSonCero()
    {
        // VN = 90,000 - 9,000 - 0 = 81,000 ; VR = 8,100 ; (VN+VR)/2 = 44,550
        // Depreciación = 0 (guard Ve > 0) ; Mantenimiento = 0.20 × 0 = 0
        // Inversión = 44,550 × 0.2124 / 1,600 = 5.9140125
        // Seguros = 44,550 × 0.03 / 1,600 = 0.8353125
        // Cargos fijos = 6.749325
        // Combustible 200 + Lubricantes 40 + Llantas 9,000/2,000 = 4.5 + Piezas 0 = 244.5
        // Operación = (300 × 1.60) / 8 = 60
        // TOTAL = 6.749325 + 244.5 + 60 = 311.249325
        var maquinaria = CrearMaquinaria(
            valorAdquisicion: 90000m,
            valorLlantas: 9000m,
            valorPiezasEspeciales: 0m,
            vidaEconomica: 0m,
            horasAnio: 1600m,
            vidaEconomicaLlantas: 2000m,
            vidaPiezasEspeciales: 0m,
            horasTurno: 8m);

        maquinaria.CalcularCostoHorario();

        Assert.AreEqual(311.249325m, maquinaria.CostoHorario);
    }

    [TestMethod]
    public void CalcularCostoHorario_CuandoHorasAnioEsCero_InversionYSegurosSonCero()
    {
        // VN = 81,000 ; VR = 8,100 ; (VN+VR)/2 = 44,550
        // Depreciación = (81,000 - 8,100) / 12,000 = 6.075 ; Mantenimiento = 0.20 × 6.075 = 1.215
        // Inversión = 0 (guard Hea > 0) ; Seguros = 0 (guard Hea > 0)
        // Cargos fijos = 7.29
        // Consumos = 200 + 40 + 4.5 + 0 = 244.5
        // Operación = 60
        // TOTAL = 7.29 + 244.5 + 60 = 311.79
        var maquinaria = CrearMaquinaria(
            valorAdquisicion: 90000m,
            valorLlantas: 9000m,
            valorPiezasEspeciales: 0m,
            vidaEconomica: 12000m,
            horasAnio: 0m,
            vidaEconomicaLlantas: 2000m,
            vidaPiezasEspeciales: 0m,
            horasTurno: 8m);

        maquinaria.CalcularCostoHorario();

        Assert.AreEqual(311.79m, maquinaria.CostoHorario);
    }

    [TestMethod]
    public void CalcularCostoHorario_CuandoHorasTurnoEsCero_OperacionEsCero()
    {
        // VN = 81,000 ; VR = 8,100 ; (VN+VR)/2 = 44,550
        // Depreciación = 6.075 ; Inversión = 5.9140125 ; Seguros = 0.8353125 ; Mantenimiento = 1.215
        // Cargos fijos = 14.039325
        // Consumos = 244.5
        // Operación = 0 (guard Ht > 0)
        // TOTAL = 14.039325 + 244.5 + 0 = 258.539325
        var maquinaria = CrearMaquinaria(
            valorAdquisicion: 90000m,
            valorLlantas: 9000m,
            valorPiezasEspeciales: 0m,
            vidaEconomica: 12000m,
            horasAnio: 1600m,
            vidaEconomicaLlantas: 2000m,
            vidaPiezasEspeciales: 0m,
            horasTurno: 0m);

        maquinaria.CalcularCostoHorario();

        Assert.AreEqual(258.539325m, maquinaria.CostoHorario);
    }

    [TestMethod]
    public void CalcularCostoHorario_DebeMarcarEsCostoCalculadoComoVerdadero()
    {
        var maquinaria = CrearMaquinaria(
            valorAdquisicion: 90000m,
            valorLlantas: 9000m,
            valorPiezasEspeciales: 0m,
            vidaEconomica: 12000m,
            horasAnio: 1600m,
            vidaEconomicaLlantas: 2000m,
            vidaPiezasEspeciales: 0m,
            horasTurno: 8m);
        maquinaria.EsCostoCalculado = false;

        maquinaria.CalcularCostoHorario();

        Assert.IsTrue(maquinaria.EsCostoCalculado);
    }

    [TestMethod]
    public void CalcularCostoHorario_DebeRegistrarFechaCalculoCosto()
    {
        var antes = DateTime.Now.AddSeconds(-5);
        var maquinaria = CrearMaquinaria(
            valorAdquisicion: 90000m,
            valorLlantas: 9000m,
            valorPiezasEspeciales: 0m,
            vidaEconomica: 12000m,
            horasAnio: 1600m,
            vidaEconomicaLlantas: 2000m,
            vidaPiezasEspeciales: 0m,
            horasTurno: 8m);

        maquinaria.CalcularCostoHorario();
        var despues = DateTime.Now.AddSeconds(5);

        Assert.IsNotNull(maquinaria.FechaCalculoCosto);
        Assert.IsTrue(maquinaria.FechaCalculoCosto >= antes);
        Assert.IsTrue(maquinaria.FechaCalculoCosto <= despues);
    }

    [TestMethod]
    public void CalcularCostoHorario_CuandoVidasDeComponentesSonCero_LlantasYPiezasSonCero()
    {
        // VN = 81,000 ; VR = 8,100 ; (VN+VR)/2 = 44,550
        // Depreciación = 6.075 ; Inversión = 5.9140125 ; Seguros = 0.8353125 ; Mantenimiento = 1.215
        // Cargos fijos = 14.039325
        // Combustible 200 + Lubricantes 40 + Llantas 0 (guard) + Piezas 0 (guard) = 240
        // Operación = 60
        // TOTAL = 14.039325 + 240 + 60 = 314.039325
        var maquinaria = CrearMaquinaria(
            valorAdquisicion: 90000m,
            valorLlantas: 9000m,
            valorPiezasEspeciales: 0m,
            vidaEconomica: 12000m,
            horasAnio: 1600m,
            vidaEconomicaLlantas: 0m,
            vidaPiezasEspeciales: 0m,
            horasTurno: 8m);

        maquinaria.CalcularCostoHorario();

        Assert.AreEqual(314.039325m, maquinaria.CostoHorario);
    }

    [DataTestMethod]
    [DataRow(1.0, 1.00000)]
    [DataRow(2.0, 0.50000)]
    [DataRow(3.0, 0.33333)]
    [DataRow(0.5, 2.00000)]
    public void CalcularRendimiento_CantidadPositiva_DevuelveUnoSobreCantidad(double cantidad, double esperado)
    {
        Assert.AreEqual((decimal)esperado, Maquinaria.CalcularRendimiento((decimal)cantidad));
    }

    [DataTestMethod]
    [DataRow(0.0)]
    [DataRow(-1.0)]
    [DataRow(-2.5)]
    public void CalcularRendimiento_CantidadNoPositiva_RetornaCero(double cantidad)
    {
        Assert.AreEqual(0m, Maquinaria.CalcularRendimiento((decimal)cantidad));
    }
}
