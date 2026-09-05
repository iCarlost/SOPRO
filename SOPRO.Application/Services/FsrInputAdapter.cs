using System.Globalization;
using Sopro.Calculation.Labor;

namespace SOPRO.Application.Services
{
    /// <summary>
    /// Bridges legacy FSR parameter dictionaries to <see cref="RealSalaryFactorInput"/>.
    /// </summary>
    public static class FsrInputAdapter
    {
        public static RealSalaryFactorInput FromDictionary(Dictionary<string, string> p, decimal salarioNominal)
        {
            return new RealSalaryFactorInput
            {
                NominalSalary = salarioNominal,
                MinimumSalary = Get(p, "SalarioMinimo", 1m),
                WorkShift = (WorkShiftType)GetInt(p, "Jornada", 0),
                Semester = GetInt(p, "Semestre", 0) + 1,
                Year = GetInt(p, "Anio", 2008),
                HoursPerShift = Get(p, "HorasJornada", 8m),
                CalendarDays = Get(p, "DiasCalendario", 365m),
                ChristmasBonusDays = Get(p, "DiasAguinaldo", 15m),
                VacationDays = Get(p, "DiasVacaciones", 12m),
                VacationPremiumPercentage = Get(p, "PrimaVacacional", 25m),
                SundayPremiumDays = Get(p, "DiasDominical", 0m),
                SundayPremiumPercentage = Get(p, "PctDominical", 0m),
                OtherPaidDays = Get(p, "OtrosDiasPagados", 0m),
                RestDays = Get(p, "DiasDescanso", 52m),
                HolidayDays = Get(p, "DiasFestivos", 7m),
                ContractDays = Get(p, "DiasContrato", 0m),
                UnionDays = Get(p, "DiasSindicato", 0m),
                IllnessDays = Get(p, "DiasEnfermedad", 0m),
                WeatherDays = Get(p, "DiasClima", 0m),
                CarryoverDays = Get(p, "DiasArrastre", 0m),
                GuardDutyDays = Get(p, "DiasGuardia", 0m),
                OtherNonWorkingDays = Get(p, "OtrosDiasNL", 0m),
                DaycarePercentage = Get(p, "PctGuarderias", 1m),
                RetirementPercentage = Get(p, "PctRetiro", 2m),
                OccupationalRiskPercentage = Get(p, "PctRiesgos", 4.58875m),
                InfonavitPercentage = Get(p, "PctINFONAVIT", 5m),
                PayrollTaxPercentage = Get(p, "PctNomina", 2.4m),
                OtherTaxesPercentage = Get(p, "OtrosImpuestos", 0m)
            };
        }

        private static decimal Get(Dictionary<string, string> p, string key, decimal def = 0)
            => p.TryGetValue(key, out var v) && decimal.TryParse(v,
                NumberStyles.Any, CultureInfo.InvariantCulture, out var d) ? d : def;

        private static int GetInt(Dictionary<string, string> p, string key, int def = 0)
            => p.TryGetValue(key, out var v) && int.TryParse(v, out var i) ? i : def;
    }
}
