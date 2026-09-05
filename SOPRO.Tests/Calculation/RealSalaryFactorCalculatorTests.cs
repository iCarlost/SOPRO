using Microsoft.VisualStudio.TestTools.UnitTesting;
using SOPRO.Application.Services;
using Sopro.Calculation.Labor;
using System.Text.Json;

namespace SOPRO.Tests.Calculation;

[TestClass]
public class RealSalaryFactorCalculatorTests
{
    [TestMethod]
    public void Calculate_BaseScenario_ReturnsExactBreakdown()
    {
        var result = RealSalaryFactorCalculator.Calculate(CreateBaseInput());

        Assert.AreEqual(8m, result.BaseHours);
        Assert.AreEqual(0m, result.OvertimeHours);
        Assert.AreEqual(1.1875m, result.DoubleOvertimeLimit);
        Assert.AreEqual(0m, result.DoubleOvertimeHours);
        Assert.AreEqual(0m, result.TripleOvertimeHours);
        Assert.AreEqual(1m, result.MinimumSalaryUnit);
        Assert.AreEqual(2.0085967942795163298919374925m, result.AdjustedNominalSalaryInMinimumSalaryUnits);
        Assert.AreEqual(365m, result.CalendarDays);
        Assert.AreEqual(12m, result.VacationDays);
        Assert.AreEqual(3m, result.VacationPremiumDays);
        Assert.AreEqual(0m, result.SundayPremiumEquivalentDays);
        Assert.AreEqual(0m, result.OvertimeEquivalentDays);
        Assert.AreEqual(383m, result.PaidDays);
        Assert.AreEqual(71m, result.NonWorkingDays);
        Assert.AreEqual(294m, result.WorkedDays);
        Assert.AreEqual(1.3027210884353741496598639456m, result.PaidToWorkedDaysFactor);
        Assert.AreEqual(1.0493150684931506849315068493m, result.ContributionBaseFactor);
        Assert.AreEqual(2.1076508827645335735578412592m, result.ContributionBaseSalaryInMinimumSalaryUnits);
        Assert.AreEqual(20.40m, result.FixedFeePercentage);
        Assert.AreEqual(1.10m, result.ExcessPercentage);
        Assert.AreEqual(25m, result.GeneralContributionCap);
        Assert.AreEqual(25m, result.LifeAndRetirementCap);
        Assert.AreEqual(25m, result.LifeAndRetirementSalaryLimit);
        Assert.AreEqual(25m, result.InfonavitSalaryLimit);
        Assert.AreEqual(0m, result.ThreeMinimumSalaryExcess);
        Assert.AreEqual(0.70m, result.CashBenefitsPercentage);
        Assert.AreEqual(1.05m, result.PensionerMedicalExpensesPercentage);
        Assert.AreEqual(1.75m, result.DisabilityAndLifePercentage);
        Assert.AreEqual(3.15m, result.SeveranceAndOldAgePercentage);
        Assert.AreEqual(0.204m, result.FixedFee);
        Assert.AreEqual(0m, result.ThreeMinimumSalaryExcessContribution);
        Assert.AreEqual(0.0147535561793517350149048888m, result.CashBenefitsContribution);
        Assert.AreEqual(0.0221303342690276025223573332m, result.PensionerMedicalExpensesContribution);
        Assert.AreEqual(0.0368838904483793375372622220m, result.MedicalBenefitsInKindContribution);
        Assert.AreEqual(0.0368838904483793375372622220m, result.DisabilityAndLifeContribution);
        Assert.AreEqual(0.0210765088276453357355784126m, result.DaycareContribution);
        Assert.AreEqual(0.0421530176552906714711568252m, result.RetirementContribution);
        Assert.AreEqual(0.0663910028070828075670719997m, result.SeveranceAndOldAgeContribution);
        Assert.AreEqual(0.0967148298828575343566354408m, result.OccupationalRiskContribution);
        Assert.AreEqual(0.5041031400696350242049671223m, result.EmployerImssTotal);
        Assert.AreEqual(0.2509727893150684931506849315m, result.EmployerImssFactor);
        Assert.AreEqual(0.1053825441382266786778920630m, result.InfonavitContribution);
        Assert.AreEqual(0.0505836211863488057653881902m, result.PayrollTax);
        Assert.AreEqual(0m, result.OtherTaxes);
        Assert.AreEqual(0.6600693053942105086482473755m, result.EmployerObligations);
        Assert.AreEqual(0.3286221043835616438356164384m, result.EmployerObligationsFactor);
        Assert.AreEqual(0.4281029455064765632280309385m, result.EmployerObligationsWeightedByPaidToWorkedDays);
        Assert.AreEqual(1.7308240339418507128878948841m, result.Factor);
    }

    [TestMethod]
    public void Calculate_WorkShiftVariants_PreserveLegacyResults()
    {
        var mixed = RealSalaryFactorCalculator.Calculate(CreateBaseInput() with
        {
            WorkShift = WorkShiftType.Mixed
        });
        var night = RealSalaryFactorCalculator.Calculate(CreateBaseInput() with
        {
            WorkShift = WorkShiftType.Night
        });
        var unknown = RealSalaryFactorCalculator.Calculate(CreateBaseInput() with
        {
            WorkShift = (WorkShiftType)99
        });

        Assert.AreEqual(1.8117642796659389592975698651m, mixed.Factor);
        Assert.AreEqual(1.8936373182554372586172977560m, night.Factor);
        Assert.AreEqual(night, unknown);
    }

    [TestMethod]
    public void Calculate_Overtime_PreservesDoubleAndTripleHourSplit()
    {
        var result = RealSalaryFactorCalculator.Calculate(CreateBaseInput() with
        {
            HoursPerShift = 10m
        });

        Assert.AreEqual(2m, result.OvertimeHours);
        Assert.AreEqual(1.1875m, result.DoubleOvertimeHours);
        Assert.AreEqual(0.8125m, result.TripleOvertimeHours);
        Assert.AreEqual(73.190104166666666666666666654m, result.OvertimeEquivalentDays);
        Assert.AreEqual(2.1143764300047069863859539774m, result.Factor);
    }

    [TestMethod]
    public void Calculate_YearAndSemesterCaps_PreserveLegacyResults()
    {
        var legacy2003 = RealSalaryFactorCalculator.Calculate(CreateBaseInput() with
        {
            NominalSalary = 6000m,
            Year = 2003
        });
        var firstSemester2007 = RealSalaryFactorCalculator.Calculate(CreateBaseInput() with
        {
            NominalSalary = 6000m,
            Year = 2007,
            Semester = 1
        });
        var secondSemester2007 = RealSalaryFactorCalculator.Calculate(CreateBaseInput() with
        {
            NominalSalary = 6000m,
            Year = 2007,
            Semester = 2
        });

        Assert.AreEqual(17.15m, legacy2003.FixedFeePercentage);
        Assert.AreEqual(3.55m, legacy2003.ExcessPercentage);
        Assert.AreEqual(20m, legacy2003.LifeAndRetirementCap);
        Assert.AreEqual(1.6259638389502036545211691982m, legacy2003.Factor);
        Assert.AreEqual(24m, firstSemester2007.LifeAndRetirementCap);
        Assert.AreEqual(1.6222885935307025207343211258m, firstSemester2007.Factor);
        Assert.AreEqual(25m, secondSemester2007.LifeAndRetirementCap);
        Assert.AreEqual(1.6276393184796821125710558196m, secondSemester2007.Factor);
    }

    [TestMethod]
    public void Calculate_MinimumSalaryBoundary_AppliesWorkerEmployerRates()
    {
        var result = RealSalaryFactorCalculator.Calculate(CreateBaseInput() with
        {
            NominalSalary = 248.93m
        });

        Assert.AreEqual(1m, result.AdjustedNominalSalaryInMinimumSalaryUnits);
        Assert.AreEqual(0.95m, result.CashBenefitsPercentage);
        Assert.AreEqual(1.425m, result.PensionerMedicalExpensesPercentage);
        Assert.AreEqual(2.375m, result.DisabilityAndLifePercentage);
        Assert.AreEqual(4.275m, result.SeveranceAndOldAgePercentage);
        Assert.AreEqual(1.8967357164989283384586711398m, result.Factor);
    }

    [TestMethod]
    public void Calculate_N0SalaryGoldens_ReturnExactFactors()
    {
        var salary250 = RealSalaryFactorCalculator.Calculate(CreateBaseInput() with { NominalSalary = 250m });
        var salary800 = RealSalaryFactorCalculator.Calculate(CreateBaseInput() with { NominalSalary = 800m });

        Assert.AreEqual(1.8631328690438915292144254962m, salary250.Factor);
        Assert.AreEqual(1.6828680219556658279750256267m, salary800.Factor);
    }

    [TestMethod]
    public void Calculate_YearTransitions_ReturnExactRatesAndCaps()
    {
        var cases = new[]
        {
            (Year: 2003, Semester: 1, FixedFee: 17.15m, Excess: 3.55m, Cap: 20m),
            (Year: 2004, Semester: 1, FixedFee: 17.80m, Excess: 3.06m, Cap: 21m),
            (Year: 2005, Semester: 1, FixedFee: 18.45m, Excess: 2.57m, Cap: 22m),
            (Year: 2006, Semester: 1, FixedFee: 19.10m, Excess: 2.08m, Cap: 23m),
            (Year: 2007, Semester: 1, FixedFee: 19.75m, Excess: 1.59m, Cap: 24m),
            (Year: 2007, Semester: 2, FixedFee: 19.75m, Excess: 1.59m, Cap: 25m),
            (Year: 2008, Semester: 1, FixedFee: 20.40m, Excess: 1.10m, Cap: 25m)
        };

        foreach (var item in cases)
        {
            var result = RealSalaryFactorCalculator.Calculate(CreateBaseInput() with
            {
                Year = item.Year,
                Semester = item.Semester
            });

            Assert.AreEqual(item.FixedFee, result.FixedFeePercentage, $"Fixed fee for {item.Year}/{item.Semester}");
            Assert.AreEqual(item.Excess, result.ExcessPercentage, $"Excess for {item.Year}/{item.Semester}");
            Assert.AreEqual(item.Cap, result.LifeAndRetirementCap, $"Cap for {item.Year}/{item.Semester}");
        }
    }

    [TestMethod]
    public void Calculate_FullyPopulatedInput_MatchesLegacyService()
    {
        var parameters = new Dictionary<string, string>
        {
            ["SalarioMinimo"] = "248.93",
            ["Jornada"] = "1",
            ["Semestre"] = "0",
            ["Anio"] = "2007",
            ["HorasJornada"] = "9",
            ["DiasCalendario"] = "360",
            ["DiasAguinaldo"] = "20",
            ["DiasVacaciones"] = "10",
            ["PrimaVacacional"] = "30",
            ["DiasDominical"] = "52",
            ["PctDominical"] = "25",
            ["OtrosDiasPagados"] = "2",
            ["DiasDescanso"] = "50",
            ["DiasFestivos"] = "8",
            ["DiasContrato"] = "3",
            ["DiasSindicato"] = "2",
            ["DiasEnfermedad"] = "4",
            ["DiasClima"] = "5",
            ["DiasArrastre"] = "1",
            ["DiasGuardia"] = "2",
            ["OtrosDiasNL"] = "3",
            ["PctGuarderias"] = "1.2",
            ["PctRetiro"] = "2.1",
            ["PctRiesgos"] = "5.25",
            ["PctINFONAVIT"] = "4.8",
            ["PctNomina"] = "3.1",
            ["OtrosImpuestos"] = "0.75"
        };
        var input = new RealSalaryFactorInput
        {
            NominalSalary = 400m,
            MinimumSalary = 248.93m,
            WorkShift = WorkShiftType.Mixed,
            Semester = 1,
            Year = 2007,
            HoursPerShift = 9m,
            CalendarDays = 360m,
            ChristmasBonusDays = 20m,
            VacationDays = 10m,
            VacationPremiumPercentage = 30m,
            SundayPremiumDays = 52m,
            SundayPremiumPercentage = 25m,
            OtherPaidDays = 2m,
            RestDays = 50m,
            HolidayDays = 8m,
            ContractDays = 3m,
            UnionDays = 2m,
            IllnessDays = 4m,
            WeatherDays = 5m,
            CarryoverDays = 1m,
            GuardDutyDays = 2m,
            OtherNonWorkingDays = 3m,
            DaycarePercentage = 1.2m,
            RetirementPercentage = 2.1m,
            OccupationalRiskPercentage = 5.25m,
            InfonavitPercentage = 4.8m,
            PayrollTaxPercentage = 3.1m,
            OtherTaxesPercentage = 0.75m
        };

        var legacy = FsrCalculationService.Calcular(JsonSerializer.Serialize(parameters), input.NominalSalary);
        var result = RealSalaryFactorCalculator.Calculate(input);

        Assert.IsNotNull(legacy);
        Assert.AreEqual(legacy.Value, result.Factor);
    }

    [TestMethod]
    public void Calculate_GeneralCapBoundary_PreservesStrictLegacyBranch()
    {
        var input = CreateBaseInput() with
        {
            MinimumSalary = 1m,
            NominalSalary = 25m,
            ChristmasBonusDays = 0m,
            VacationDays = 0m,
            VacationPremiumPercentage = 0m,
            RestDays = 0m,
            HolidayDays = 0m
        };

        var atCap = RealSalaryFactorCalculator.Calculate(input);
        var belowCap = RealSalaryFactorCalculator.Calculate(input with { NominalSalary = 24.999m });

        Assert.AreEqual(25m, atCap.ContributionBaseSalaryInMinimumSalaryUnits);
        Assert.AreEqual(0.275m, atCap.ThreeMinimumSalaryExcessContribution);
        Assert.AreEqual(0.241989m, belowCap.ThreeMinimumSalaryExcessContribution);
    }

    [TestMethod]
    public void Calculate_HoursBelowShiftBase_PreservesNegativeDoubleOvertime()
    {
        var result = RealSalaryFactorCalculator.Calculate(CreateBaseInput() with { HoursPerShift = 7m });

        Assert.AreEqual(-1m, result.OvertimeHours);
        Assert.AreEqual(-1m, result.DoubleOvertimeHours);
        Assert.AreEqual(0m, result.TripleOvertimeHours);
        Assert.AreEqual(-30.416666666666666666666666654m, result.OvertimeEquivalentDays);
    }

    [TestMethod]
    public void Calculate_NonPositiveWorkedDays_ReturnsZeroFactor()
    {
        var result = RealSalaryFactorCalculator.Calculate(CreateBaseInput() with
        {
            RestDays = 365m
        });

        Assert.IsTrue(result.WorkedDays < 0m);
        Assert.AreEqual(0m, result.PaidToWorkedDaysFactor);
        Assert.AreEqual(0m, result.Factor);
    }

    [TestMethod]
    public void Calculate_ZeroCalendarDays_GuardsBothDayFactors()
    {
        var result = RealSalaryFactorCalculator.Calculate(CreateBaseInput() with { CalendarDays = 0m });

        Assert.AreEqual(0m, result.PaidToWorkedDaysFactor);
        Assert.AreEqual(0m, result.ContributionBaseFactor);
        Assert.AreEqual(0m, result.Factor);
    }

    [TestMethod]
    public void Calculate_DoesNotMutateInput()
    {
        var input = CreateBaseInput();
        var snapshot = input with { };

        RealSalaryFactorCalculator.Calculate(input);

        Assert.AreEqual(snapshot, input);
    }

    [TestMethod]
    public void Calculate_NullInput_Throws()
    {
        Assert.ThrowsException<ArgumentNullException>(
            () => RealSalaryFactorCalculator.Calculate(null!));
    }

    [TestMethod]
    public void Calculate_ZeroMinimumSalary_PropagatesDivisionByZero()
    {
        var input = CreateBaseInput() with { MinimumSalary = 0m };

        Assert.ThrowsException<DivideByZeroException>(
            () => RealSalaryFactorCalculator.Calculate(input));
    }

    [TestMethod]
    public void Calculate_Overflow_Propagates()
    {
        var input = CreateBaseInput() with
        {
            NominalSalary = decimal.MaxValue,
            HoursPerShift = 10m
        };

        Assert.ThrowsException<OverflowException>(
            () => RealSalaryFactorCalculator.Calculate(input));
    }

    private static RealSalaryFactorInput CreateBaseInput() => new()
    {
        NominalSalary = 500m,
        MinimumSalary = 248.93m,
        WorkShift = WorkShiftType.Day,
        Semester = 1,
        Year = 2026,
        HoursPerShift = 8m,
        CalendarDays = 365m,
        ChristmasBonusDays = 15m,
        VacationDays = 12m,
        VacationPremiumPercentage = 25m,
        SundayPremiumDays = 0m,
        SundayPremiumPercentage = 0m,
        OtherPaidDays = 0m,
        RestDays = 52m,
        HolidayDays = 7m,
        ContractDays = 0m,
        UnionDays = 0m,
        IllnessDays = 0m,
        WeatherDays = 0m,
        CarryoverDays = 0m,
        GuardDutyDays = 0m,
        OtherNonWorkingDays = 0m,
        DaycarePercentage = 1m,
        RetirementPercentage = 2m,
        OccupationalRiskPercentage = 4.58875m,
        InfonavitPercentage = 5m,
        PayrollTaxPercentage = 2.4m,
        OtherTaxesPercentage = 0m
    };
}
