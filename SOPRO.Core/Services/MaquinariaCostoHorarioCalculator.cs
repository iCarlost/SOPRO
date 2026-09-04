using System;

namespace SOPRO.Core.Services;

/// <summary>Immutable inputs for the hourly machinery cost calculation.</summary>
public sealed record MaquinariaCostoHorarioInput
{
    public decimal ValorAdquisicion { get; init; }
    public decimal ValorLlantas { get; init; }
    public decimal ValorPiezasEspeciales { get; init; }
    public decimal FactorRescate { get; init; }
    public decimal VidaEconomica { get; init; }
    public decimal TasaInteres { get; init; }
    public decimal HorasEfectivasAnio { get; init; }
    public decimal PrimaSeguro { get; init; }
    public decimal FactorMantenimiento { get; init; }
    public decimal CantidadCombustible { get; init; }
    public decimal PrecioCombustible { get; init; }
    public decimal CantidadAceite { get; init; }
    public decimal PrecioAceite { get; init; }
    public decimal VidaEconomicaLlantas { get; init; }
    public decimal VidaPiezasEspeciales { get; init; }
    public decimal SalarioOperador { get; init; }
    public decimal FactorSalarioReal { get; init; }
    public decimal HorasEfectivasTurno { get; init; }
}

/// <summary>Detailed, deterministic output of the hourly machinery cost calculation.</summary>
public sealed record MaquinariaCostoHorarioResult
{
    public decimal ValorNeto { get; init; }
    public decimal ValorRescate { get; init; }
    public decimal ValorNetoMedio { get; init; }
    public decimal Depreciacion { get; init; }
    public decimal Inversion { get; init; }
    public decimal Seguros { get; init; }
    public decimal Mantenimiento { get; init; }
    public decimal TotalCargosFijos { get; init; }
    public decimal Combustible { get; init; }
    public decimal Lubricantes { get; init; }
    public decimal Llantas { get; init; }
    public decimal PiezasEspeciales { get; init; }
    public decimal TotalConsumos { get; init; }
    public decimal SalarioReal { get; init; }
    public decimal Operacion { get; init; }
    public decimal CostoHorario { get; init; }
}

/// <summary>
/// Single implementation of the legacy OPUS/RLOPSRM hourly machinery cost formula.
/// The calculation is pure; persistence and audit timestamps remain outside it.
/// </summary>
public static class MaquinariaCostoHorarioCalculator
{
    public static MaquinariaCostoHorarioResult Calcular(MaquinariaCostoHorarioInput input)
    {
        ArgumentNullException.ThrowIfNull(input);

        decimal valorNeto = input.ValorAdquisicion - input.ValorLlantas - input.ValorPiezasEspeciales;
        decimal valorRescate = valorNeto * input.FactorRescate;
        decimal valorNetoMedio = (valorNeto + valorRescate) / 2m;

        decimal depreciacion = input.VidaEconomica > 0
            ? (valorNeto - valorRescate) / input.VidaEconomica
            : 0m;
        decimal inversion = input.HorasEfectivasAnio > 0
            ? valorNetoMedio * (input.TasaInteres / 100m) / input.HorasEfectivasAnio
            : 0m;
        decimal seguros = input.HorasEfectivasAnio > 0
            ? valorNetoMedio * (input.PrimaSeguro / 100m) / input.HorasEfectivasAnio
            : 0m;
        decimal mantenimiento = input.FactorMantenimiento * depreciacion;
        decimal totalCargosFijos = depreciacion + inversion + seguros + mantenimiento;

        decimal combustible = input.CantidadCombustible * input.PrecioCombustible;
        decimal lubricantes = input.CantidadAceite * input.PrecioAceite;
        decimal llantas = input.VidaEconomicaLlantas > 0
            ? input.ValorLlantas / input.VidaEconomicaLlantas
            : 0m;
        decimal piezasEspeciales = input.VidaPiezasEspeciales > 0
            ? input.ValorPiezasEspeciales / input.VidaPiezasEspeciales
            : 0m;
        decimal totalConsumos = combustible + lubricantes + llantas + piezasEspeciales;

        decimal salarioReal = input.SalarioOperador * input.FactorSalarioReal;
        decimal operacion = input.HorasEfectivasTurno > 0
            ? salarioReal / input.HorasEfectivasTurno
            : 0m;

        return new MaquinariaCostoHorarioResult
        {
            ValorNeto = valorNeto,
            ValorRescate = valorRescate,
            ValorNetoMedio = valorNetoMedio,
            Depreciacion = depreciacion,
            Inversion = inversion,
            Seguros = seguros,
            Mantenimiento = mantenimiento,
            TotalCargosFijos = totalCargosFijos,
            Combustible = combustible,
            Lubricantes = lubricantes,
            Llantas = llantas,
            PiezasEspeciales = piezasEspeciales,
            TotalConsumos = totalConsumos,
            SalarioReal = salarioReal,
            Operacion = operacion,
            CostoHorario = totalCargosFijos + totalConsumos + operacion
        };
    }
}
