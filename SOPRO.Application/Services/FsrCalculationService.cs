using System;
using System.Text.Json;
using Sopro.Calculation.Labor;

namespace SOPRO.Application.Services
{
    /// <summary>
    /// Calcula el Factor de Salario Real (FSR) para un salario base dado,
    /// usando los parámetros almacenados en Proyecto.ParametrosFSR.
    /// Delega en <see cref="RealSalaryFactorCalculator"/> (motor puro, N7-13b).
    /// </summary>
    public static class FsrCalculationService
    {
        public static decimal? Calcular(string? parametrosFSRJson, decimal salarioNominal)
        {
            if (string.IsNullOrEmpty(parametrosFSRJson)) return null;
            if (salarioNominal <= 0) return null;

            try
            {
                var p = JsonSerializer.Deserialize<Dictionary<string, string>>(parametrosFSRJson);
                if (p == null) return null;

                var input = FsrInputAdapter.FromDictionary(p, salarioNominal);
                var result = RealSalaryFactorCalculator.Calculate(input);
                return result.Factor;
            }
            catch
            {
                return null;
            }
        }
    }
}
