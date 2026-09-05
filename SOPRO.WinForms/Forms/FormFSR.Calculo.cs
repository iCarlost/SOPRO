using SOPRO.Core.Entities;
using SOPRO.Data.Context;
using SOPRO.Data.Repositories;
using SOPRO.WinForms.Services;
using SOPRO.WinForms.Helpers;
using System.Drawing;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Windows.Forms;
using SOPRO.Application.Services;
using Sopro.Calculation.Labor;

namespace SOPRO.WinForms.Forms
{
    /// <summary>
    /// Cálculo del factor de salario real según legislación mexicana.
    /// Delega en <see cref="RealSalaryFactorCalculator"/> (motor puro, N7-13b).
    /// </summary>
    public partial class FormFSR
    {

        private void Recalcular()
        {
            try
            {
                FSR_FSR = CalcularFSR(nudSalarioNominal.Value);
                ActualizarUI();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error en Recalcular FSR: {ex.Message}");
            }
        }

        /// <summary>
        /// Calcula el FSR para un salario nominal específico usando los parámetros
        /// actuales del formulario. Permite calcular FSR individual por insumo.
        /// </summary>
        private decimal CalcularFSR(decimal SN)
        {
            var input = new RealSalaryFactorInput
            {
                NominalSalary = SN,
                MinimumSalary = nudSalarioMinimo.Value,
                WorkShift = (WorkShiftType)cboJornada.SelectedIndex,
                Semester = cboSemestre.SelectedIndex + 1,
                Year = (int)nudAnio.Value,
                HoursPerShift = nudHorasJornada.Value,
                CalendarDays = nudDiasCalendario.Value,
                ChristmasBonusDays = nudDiasAguinaldo.Value,
                VacationDays = nudDiasVacaciones.Value,
                VacationPremiumPercentage = nudPrimaVacacional.Value,
                SundayPremiumDays = nudDiasDominical.Value,
                SundayPremiumPercentage = nudPctDominical.Value,
                OtherPaidDays = nudOtrosDiasPagados.Value,
                RestDays = nudDiasDescanso.Value,
                HolidayDays = nudDiasFestivos.Value,
                ContractDays = nudDiasContrato.Value,
                UnionDays = nudDiasSindicato.Value,
                IllnessDays = nudDiasEnfermedad.Value,
                WeatherDays = nudDiasClima.Value,
                CarryoverDays = nudDiasArrastre.Value,
                GuardDutyDays = nudDiasGuardia.Value,
                OtherNonWorkingDays = nudOtrosDiasNL.Value,
                DaycarePercentage = nudPctGuarderias.Value,
                RetirementPercentage = nudPctRetiro.Value,
                OccupationalRiskPercentage = nudPctRiesgos.Value,
                InfonavitPercentage = nudPctINFONAVIT.Value,
                PayrollTaxPercentage = nudPctNomina.Value,
                OtherTaxesPercentage = nudOtrosImpuestos.Value
            };

            var b = RealSalaryFactorCalculator.Calculate(input);

            BD = b.OvertimeHours;
            BE = b.DoubleOvertimeLimit;
            BF = b.DoubleOvertimeHours;
            BG = b.TripleOvertimeHours;
            FSR_SAMI = b.MinimumSalaryUnit;
            FSR_SACAL = b.AdjustedNominalSalaryInMinimumSalaryUnits;
            FSR_DVAC = b.VacationDays;
            FSR_DPPVA = b.VacationPremiumDays;
            FSR_DPPDO = b.SundayPremiumEquivalentDays;
            FSR_DPHEX = b.OvertimeEquivalentDays;
            FSR_DPA = b.PaidDays;
            FSR_DNLA = b.NonWorkingDays;
            FSR_DLA = b.WorkedDays;
            FSR_FSI = b.PaidToWorkedDaysFactor;
            FSR_FSBC = b.ContributionBaseFactor;
            FSR_SABC = b.ContributionBaseSalaryInMinimumSalaryUnits;
            AA = b.FixedFeePercentage;
            AB = b.ExcessPercentage;
            BA = b.GeneralContributionCap;
            AS_lim = b.LifeAndRetirementCap;
            AY = b.LifeAndRetirementSalaryLimit;
            AU = b.ThreeMinimumSalaryExcess;
            FSR_IMPE_p = b.CashBenefitsPercentage;
            FSR_IMGM_p = b.PensionerMedicalExpensesPercentage;
            FSR_IMINV_p = b.DisabilityAndLifePercentage;
            FSR_IMCE_p = b.SeveranceAndOldAgePercentage;
            AC = b.FixedFee;
            AD = b.ThreeMinimumSalaryExcessContribution;
            AE = b.CashBenefitsContribution;
            AF = b.PensionerMedicalExpensesContribution;
            AG = b.DisabilityAndLifeContribution;
            AH = b.DaycareContribution;
            AI = b.RetirementContribution;
            AJ = b.SeveranceAndOldAgeContribution;
            AK = b.OccupationalRiskContribution;
            AL = b.EmployerImssTotal;
            FSR_IMIMS = b.EmployerImssFactor;
            AZ = b.InfonavitSalaryLimit;
            AM = b.InfonavitContribution;
            AN = b.PayrollTax;
            AO = b.OtherTaxes;
            AP = b.EmployerObligations;
            AQ = b.EmployerObligationsFactor;
            BH = b.EmployerObligationsWeightedByPaidToWorkedDays;

            return b.Factor;
        }
    }
}
