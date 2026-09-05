namespace Sopro.Calculation.Labor;

/// <summary>Work shift used to determine regular and overtime hours.</summary>
public enum WorkShiftType
{
    Day = 0,
    Mixed = 1,
    Night = 2
}

/// <summary>Immutable scalar inputs for the Mexican real salary factor calculation.</summary>
public sealed record RealSalaryFactorInput
{
    public decimal NominalSalary { get; init; }
    public decimal MinimumSalary { get; init; }
    public WorkShiftType WorkShift { get; init; }
    /// <summary>One-based semester number (1 or 2).</summary>
    public int Semester { get; init; }
    public int Year { get; init; }
    public decimal HoursPerShift { get; init; }
    public decimal CalendarDays { get; init; }
    public decimal ChristmasBonusDays { get; init; }
    public decimal VacationDays { get; init; }
    public decimal VacationPremiumPercentage { get; init; }
    public decimal SundayPremiumDays { get; init; }
    public decimal SundayPremiumPercentage { get; init; }
    public decimal OtherPaidDays { get; init; }
    public decimal RestDays { get; init; }
    public decimal HolidayDays { get; init; }
    public decimal ContractDays { get; init; }
    public decimal UnionDays { get; init; }
    public decimal IllnessDays { get; init; }
    public decimal WeatherDays { get; init; }
    public decimal CarryoverDays { get; init; }
    public decimal GuardDutyDays { get; init; }
    public decimal OtherNonWorkingDays { get; init; }
    public decimal DaycarePercentage { get; init; }
    public decimal RetirementPercentage { get; init; }
    public decimal OccupationalRiskPercentage { get; init; }
    public decimal InfonavitPercentage { get; init; }
    public decimal PayrollTaxPercentage { get; init; }
    public decimal OtherTaxesPercentage { get; init; }
}

/// <summary>Detailed, deterministic output of the real salary factor calculation.</summary>
public sealed record RealSalaryFactorBreakdown
{
    public decimal BaseHours { get; init; }
    public decimal OvertimeHours { get; init; }
    public decimal DoubleOvertimeLimit { get; init; }
    public decimal DoubleOvertimeHours { get; init; }
    public decimal TripleOvertimeHours { get; init; }
    public decimal MinimumSalaryUnit { get; init; }
    public decimal AdjustedNominalSalaryInMinimumSalaryUnits { get; init; }
    public decimal CalendarDays { get; init; }
    public decimal VacationDays { get; init; }
    public decimal VacationPremiumDays { get; init; }
    public decimal SundayPremiumEquivalentDays { get; init; }
    public decimal OvertimeEquivalentDays { get; init; }
    public decimal PaidDays { get; init; }
    public decimal NonWorkingDays { get; init; }
    public decimal WorkedDays { get; init; }
    public decimal PaidToWorkedDaysFactor { get; init; }
    public decimal ContributionBaseFactor { get; init; }
    public decimal ContributionBaseSalaryInMinimumSalaryUnits { get; init; }
    public decimal FixedFeePercentage { get; init; }
    public decimal ExcessPercentage { get; init; }
    public decimal GeneralContributionCap { get; init; }
    public decimal LifeAndRetirementCap { get; init; }
    public decimal LifeAndRetirementSalaryLimit { get; init; }
    public decimal InfonavitSalaryLimit { get; init; }
    public decimal ThreeMinimumSalaryExcess { get; init; }
    public decimal CashBenefitsPercentage { get; init; }
    public decimal PensionerMedicalExpensesPercentage { get; init; }
    public decimal DisabilityAndLifePercentage { get; init; }
    public decimal SeveranceAndOldAgePercentage { get; init; }
    public decimal FixedFee { get; init; }
    public decimal ThreeMinimumSalaryExcessContribution { get; init; }
    public decimal CashBenefitsContribution { get; init; }
    public decimal PensionerMedicalExpensesContribution { get; init; }
    public decimal MedicalBenefitsInKindContribution { get; init; }
    public decimal DisabilityAndLifeContribution { get; init; }
    public decimal DaycareContribution { get; init; }
    public decimal RetirementContribution { get; init; }
    public decimal SeveranceAndOldAgeContribution { get; init; }
    public decimal OccupationalRiskContribution { get; init; }
    public decimal EmployerImssTotal { get; init; }
    public decimal EmployerImssFactor { get; init; }
    public decimal InfonavitContribution { get; init; }
    public decimal PayrollTax { get; init; }
    public decimal OtherTaxes { get; init; }
    public decimal EmployerObligations { get; init; }
    public decimal EmployerObligationsFactor { get; init; }
    public decimal EmployerObligationsWeightedByPaidToWorkedDays { get; init; }
    public decimal Factor { get; init; }
}

/// <summary>
/// Pure implementation of the legacy Mexican real salary factor formula.
/// Parsing, defaults, formatting and application-level validation belong to adapters.
/// </summary>
public static class RealSalaryFactorCalculator
{
    public static RealSalaryFactorBreakdown Calculate(RealSalaryFactorInput input)
    {
        ArgumentNullException.ThrowIfNull(input);

        decimal baseHours = input.WorkShift == WorkShiftType.Day
            ? 8m
            : input.WorkShift == WorkShiftType.Mixed ? 7.5m : 7m;
        decimal overtimeHours = input.HoursPerShift - baseHours;
        decimal doubleOvertimeLimit = input.WorkShift == WorkShiftType.Day
            ? 1.1875m
            : input.WorkShift == WorkShiftType.Mixed ? 1.2m : 1.214286m;
        decimal doubleOvertimeHours = doubleOvertimeLimit > overtimeHours
            ? overtimeHours
            : doubleOvertimeLimit;
        decimal tripleOvertimeHours = overtimeHours - doubleOvertimeHours;

        const decimal minimumSalaryUnit = 1m;
        decimal calibratedSalary =
            (input.NominalSalary * (1m + tripleOvertimeHours / baseHours)) / input.MinimumSalary;

        decimal vacationDays = input.VacationDays;
        decimal vacationPremiumDays = input.VacationPremiumPercentage / 100m * input.VacationDays;
        decimal sundayPremiumEquivalentDays = input.SundayPremiumPercentage / 100m * input.SundayPremiumDays;
        decimal overtimeEquivalentDays =
            (doubleOvertimeHours * 2m + tripleOvertimeHours * 3m) / 24m * input.CalendarDays;
        decimal paidDays = input.CalendarDays + input.ChristmasBonusDays + vacationPremiumDays
                         + sundayPremiumEquivalentDays + overtimeEquivalentDays + input.OtherPaidDays;

        decimal nonWorkingDays = input.RestDays + input.HolidayDays + input.ContractDays + input.UnionDays
                               + vacationDays + input.IllnessDays + input.WeatherDays + input.CarryoverDays
                               + input.GuardDutyDays + input.OtherNonWorkingDays;
        decimal workedDays = input.CalendarDays - nonWorkingDays;

        decimal paidToWorkedDaysFactor = workedDays > 0m ? paidDays / workedDays : 0m;
        decimal contributionBaseFactor = input.CalendarDays > 0m ? paidDays / input.CalendarDays : 0m;
        decimal contributionBaseSalary = calibratedSalary * contributionBaseFactor;

        decimal fixedFeePercentage = input.Year <= 2003 ? 17.15m
            : input.Year == 2004 ? 17.80m
            : input.Year == 2005 ? 18.45m
            : input.Year == 2006 ? 19.10m
            : input.Year == 2007 ? 19.75m
            : 20.40m;
        decimal excessPercentage = input.Year <= 2003 ? 3.55m
            : input.Year == 2004 ? 3.06m
            : input.Year == 2005 ? 2.57m
            : input.Year == 2006 ? 2.08m
            : input.Year == 2007 ? 1.59m
            : 1.10m;

        decimal generalContributionCap = 25m * minimumSalaryUnit;
        decimal lifeAndRetirementCap = input.Year <= 2003 ? 20m
            : input.Year == 2004 ? 21m
            : input.Year == 2005 ? 22m
            : input.Year == 2006 ? 23m
            : input.Year == 2007 && input.Semester == 1 ? 24m
            : 25m;
        decimal lifeAndRetirementSalaryLimit = lifeAndRetirementCap * minimumSalaryUnit;
        decimal threeMinimumSalaryExcess = contributionBaseSalary <= 3m * minimumSalaryUnit
            ? 0m
            : contributionBaseSalary - 3m * minimumSalaryUnit;

        decimal cashBenefitsPercentage = 0.70m + (calibratedSalary > minimumSalaryUnit ? 0m : 0.250m);
        decimal pensionerMedicalExpensesPercentage = 1.05m + (calibratedSalary > minimumSalaryUnit ? 0m : 0.375m);
        decimal disabilityAndLifePercentage = 1.75m + (calibratedSalary > minimumSalaryUnit ? 0m : 0.625m);
        decimal severanceAndOldAgePercentage = 3.15m + (calibratedSalary > minimumSalaryUnit ? 0m : 1.125m);

        decimal fixedFee = fixedFeePercentage / 100m * minimumSalaryUnit;
        decimal threeMinimumSalaryExcessContribution = contributionBaseSalary < generalContributionCap
            ? excessPercentage / 100m * threeMinimumSalaryExcess
            : excessPercentage / 100m * generalContributionCap;
        decimal cashBenefitsContribution = contributionBaseSalary < generalContributionCap
            ? cashBenefitsPercentage / 100m * contributionBaseSalary
            : cashBenefitsPercentage / 100m * generalContributionCap;
        decimal pensionerMedicalExpensesContribution = contributionBaseSalary < generalContributionCap
            ? pensionerMedicalExpensesPercentage / 100m * contributionBaseSalary
            : pensionerMedicalExpensesPercentage / 100m * generalContributionCap;
        decimal disabilityAndLifeContribution = contributionBaseSalary < lifeAndRetirementSalaryLimit
            ? disabilityAndLifePercentage / 100m * contributionBaseSalary
            : disabilityAndLifePercentage / 100m * lifeAndRetirementSalaryLimit;
        decimal daycareContribution = contributionBaseSalary < generalContributionCap
            ? input.DaycarePercentage / 100m * contributionBaseSalary
            : input.DaycarePercentage / 100m * generalContributionCap;
        decimal retirementContribution = contributionBaseSalary < generalContributionCap
            ? input.RetirementPercentage / 100m * contributionBaseSalary
            : input.RetirementPercentage / 100m * generalContributionCap;
        decimal severanceAndOldAgeContribution = contributionBaseSalary < lifeAndRetirementSalaryLimit
            ? severanceAndOldAgePercentage / 100m * contributionBaseSalary
            : severanceAndOldAgePercentage / 100m * lifeAndRetirementSalaryLimit;
        decimal occupationalRiskContribution = contributionBaseSalary < generalContributionCap
            ? input.OccupationalRiskPercentage / 100m * contributionBaseSalary
            : input.OccupationalRiskPercentage / 100m * generalContributionCap;
        decimal employerImssTotal = fixedFee + threeMinimumSalaryExcessContribution + cashBenefitsContribution
                                  + pensionerMedicalExpensesContribution + disabilityAndLifeContribution
                                  + daycareContribution + retirementContribution + severanceAndOldAgeContribution
                                  + occupationalRiskContribution;
        decimal employerImssFactor = calibratedSalary > 0m ? employerImssTotal / calibratedSalary : 0m;

        decimal infonavitSalaryLimit = lifeAndRetirementSalaryLimit;
        decimal infonavitContribution = contributionBaseSalary < infonavitSalaryLimit
            ? input.InfonavitPercentage / 100m * contributionBaseSalary
            : input.InfonavitPercentage / 100m * infonavitSalaryLimit;
        decimal payrollTax = input.PayrollTaxPercentage / 100m * contributionBaseSalary;
        decimal otherTaxes = input.OtherTaxesPercentage / 100m * contributionBaseSalary;
        decimal employerObligations = employerImssTotal + infonavitContribution + payrollTax + otherTaxes;
        decimal employerObligationsFactor = calibratedSalary > 0m
            ? employerObligations / calibratedSalary
            : 0m;

        decimal employerObligationsOverPaidToWorkedDays = employerObligationsFactor * paidToWorkedDaysFactor;
        decimal factor = employerObligationsOverPaidToWorkedDays + paidToWorkedDaysFactor;

        return new RealSalaryFactorBreakdown
        {
            BaseHours = baseHours,
            OvertimeHours = overtimeHours,
            DoubleOvertimeLimit = doubleOvertimeLimit,
            DoubleOvertimeHours = doubleOvertimeHours,
            TripleOvertimeHours = tripleOvertimeHours,
            MinimumSalaryUnit = minimumSalaryUnit,
            AdjustedNominalSalaryInMinimumSalaryUnits = calibratedSalary,
            CalendarDays = input.CalendarDays,
            VacationDays = vacationDays,
            VacationPremiumDays = vacationPremiumDays,
            SundayPremiumEquivalentDays = sundayPremiumEquivalentDays,
            OvertimeEquivalentDays = overtimeEquivalentDays,
            PaidDays = paidDays,
            NonWorkingDays = nonWorkingDays,
            WorkedDays = workedDays,
            PaidToWorkedDaysFactor = paidToWorkedDaysFactor,
            ContributionBaseFactor = contributionBaseFactor,
            ContributionBaseSalaryInMinimumSalaryUnits = contributionBaseSalary,
            FixedFeePercentage = fixedFeePercentage,
            ExcessPercentage = excessPercentage,
            GeneralContributionCap = generalContributionCap,
            LifeAndRetirementCap = lifeAndRetirementCap,
            LifeAndRetirementSalaryLimit = lifeAndRetirementSalaryLimit,
            InfonavitSalaryLimit = infonavitSalaryLimit,
            ThreeMinimumSalaryExcess = threeMinimumSalaryExcess,
            CashBenefitsPercentage = cashBenefitsPercentage,
            PensionerMedicalExpensesPercentage = pensionerMedicalExpensesPercentage,
            DisabilityAndLifePercentage = disabilityAndLifePercentage,
            SeveranceAndOldAgePercentage = severanceAndOldAgePercentage,
            FixedFee = fixedFee,
            ThreeMinimumSalaryExcessContribution = threeMinimumSalaryExcessContribution,
            CashBenefitsContribution = cashBenefitsContribution,
            PensionerMedicalExpensesContribution = pensionerMedicalExpensesContribution,
            MedicalBenefitsInKindContribution = cashBenefitsContribution + pensionerMedicalExpensesContribution,
            DisabilityAndLifeContribution = disabilityAndLifeContribution,
            DaycareContribution = daycareContribution,
            RetirementContribution = retirementContribution,
            SeveranceAndOldAgeContribution = severanceAndOldAgeContribution,
            OccupationalRiskContribution = occupationalRiskContribution,
            EmployerImssTotal = employerImssTotal,
            EmployerImssFactor = employerImssFactor,
            InfonavitContribution = infonavitContribution,
            PayrollTax = payrollTax,
            OtherTaxes = otherTaxes,
            EmployerObligations = employerObligations,
            EmployerObligationsFactor = employerObligationsFactor,
            EmployerObligationsWeightedByPaidToWorkedDays = employerObligationsOverPaidToWorkedDays,
            Factor = factor
        };
    }
}
