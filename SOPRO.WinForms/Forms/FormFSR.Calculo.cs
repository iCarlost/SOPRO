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

namespace SOPRO.WinForms.Forms
{
    /// <summary>
    /// Cálculo del factor de salario real según legislación mexicana.
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
            int     BB  = cboJornada.SelectedIndex;
            int     AR  = cboSemestre.SelectedIndex + 1;
            int     AV  = (int)nudAnio.Value;
            decimal BC  = nudHorasJornada.Value;
            decimal AW  = nudSalarioMinimo.Value;

            // Horas extras por jornada
            decimal htBase = BB == 0 ? 8m : (BB == 1 ? 7.5m : 7m);
            BD = BC - htBase;
            BE = BB == 0 ? 1.1875m : (BB == 1 ? 1.2m : 1.214286m);
            BF = BE > BD ? BD : BE;
            BG = BD - BF;

            FSR_SAMI  = 1.0m;
            FSR_SACAL = (SN * (1 + BG / htBase)) / AW;

            decimal FSR_DPCAL  = nudDiasCalendario.Value;
            decimal FSR_DPAGU  = nudDiasAguinaldo.Value;
            decimal FSR_DNVAC  = nudDiasVacaciones.Value;
            decimal FSR_PPVAC  = nudPrimaVacacional.Value;
            decimal FSR_DNDOM  = nudDiasDominical.Value;
            decimal FSR_PPDOM  = nudPctDominical.Value;
            decimal FSR_DPOT1  = nudOtrosDiasPagados.Value;

            FSR_DVAC  = FSR_DNVAC;
            FSR_DPPVA = FSR_PPVAC / 100m * FSR_DNVAC;
            FSR_DPPDO = FSR_PPDOM / 100m * FSR_DNDOM;
            FSR_DPHEX = (BF * 2m + BG * 3m) / 24m * FSR_DPCAL;
            FSR_DPA   = FSR_DPCAL + FSR_DPAGU + FSR_DPPVA + FSR_DPPDO + FSR_DPHEX + FSR_DPOT1;

            decimal FSR_DNSEP = nudDiasDescanso.Value;
            decimal FSR_DNFES = nudDiasFestivos.Value;
            decimal FSR_DNDCO = nudDiasContrato.Value;
            decimal FSR_DNSIN = nudDiasSindicato.Value;
            decimal FSR_DNPER = nudDiasEnfermedad.Value;
            decimal FSR_DNCLI = nudDiasClima.Value;
            decimal FSR_DNARR = nudDiasArrastre.Value;
            decimal FSR_DNGUA = nudDiasGuardia.Value;
            decimal FSR_DNOT3 = nudOtrosDiasNL.Value;

            FSR_DNLA = FSR_DNSEP + FSR_DNFES + FSR_DNDCO + FSR_DNSIN + FSR_DVAC
                      + FSR_DNPER + FSR_DNCLI + FSR_DNARR + FSR_DNGUA + FSR_DNOT3;
            FSR_DLA  = FSR_DPCAL - FSR_DNLA;

            FSR_FSI  = FSR_DLA > 0 ? FSR_DPA / FSR_DLA : 0;
            FSR_FSBC = FSR_DPCAL > 0 ? FSR_DPA / FSR_DPCAL : 0;
            FSR_SABC = FSR_SACAL * FSR_FSBC;

            AA = AV <= 2003 ? 17.15m : AV == 2004 ? 17.80m : AV == 2005 ? 18.45m
               : AV == 2006 ? 19.10m : AV == 2007 ? 19.75m : 20.40m;
            AB = AV <= 2003 ? 3.55m  : AV == 2004 ? 3.06m  : AV == 2005 ? 2.57m
               : AV == 2006 ? 2.08m  : AV == 2007 ? 1.59m  : 1.10m;

            BA = 25m * FSR_SAMI;
            AS_lim = AV <= 2003 ? 20m : AV == 2004 ? 21m : AV == 2005 ? 22m
                   : AV == 2006 ? 23m : (AV == 2007 && AR == 1) ? 24m : 25m;
            AY = AS_lim * FSR_SAMI;
            AU = FSR_SABC <= 3m * FSR_SAMI ? 0m : FSR_SABC - 3m * FSR_SAMI;

            FSR_IMPE_p  = 0.70m  + (FSR_SACAL > FSR_SAMI ? 0m : 0.250m);
            FSR_IMGM_p  = 1.05m  + (FSR_SACAL > FSR_SAMI ? 0m : 0.375m);
            FSR_IMINV_p = 1.75m  + (FSR_SACAL > FSR_SAMI ? 0m : 0.625m);
            FSR_IMCE_p  = 3.15m  + (FSR_SACAL > FSR_SAMI ? 0m : 1.125m);

            decimal FSR_IMGUA_p = nudPctGuarderias.Value;
            decimal FSR_IMSAR_p = nudPctRetiro.Value;
            decimal FSR_IMRTR_p = nudPctRiesgos.Value;

            AC = AA / 100m * FSR_SAMI;
            AD = FSR_SABC < BA ? AB / 100m * AU       : AB / 100m * BA;
            AE = FSR_SABC < BA ? FSR_IMPE_p  / 100m * FSR_SABC : FSR_IMPE_p  / 100m * BA;
            AF = FSR_SABC < BA ? FSR_IMGM_p  / 100m * FSR_SABC : FSR_IMGM_p  / 100m * BA;
            AG = FSR_SABC < AY ? FSR_IMINV_p / 100m * FSR_SABC : FSR_IMINV_p / 100m * AY;
            AH = FSR_SABC < BA ? FSR_IMGUA_p / 100m * FSR_SABC : FSR_IMGUA_p / 100m * BA;
            AI = FSR_SABC < BA ? FSR_IMSAR_p / 100m * FSR_SABC : FSR_IMSAR_p / 100m * BA;
            AJ = FSR_SABC < AY ? FSR_IMCE_p  / 100m * FSR_SABC : FSR_IMCE_p  / 100m * AY;
            AK = FSR_SABC < BA ? FSR_IMRTR_p / 100m * FSR_SABC : FSR_IMRTR_p / 100m * BA;
            AL = AC + AD + AE + AF + AG + AH + AI + AJ + AK;
            FSR_IMIMS = FSR_SACAL > 0 ? AL / FSR_SACAL : 0;

            AZ = AY;
            decimal FSR_IMINF_p = nudPctINFONAVIT.Value;
            AM = FSR_SABC < AZ ? FSR_IMINF_p / 100m * FSR_SABC : FSR_IMINF_p / 100m * AZ;
            AN = nudPctNomina.Value      / 100m * FSR_SABC;
            AO = nudOtrosImpuestos.Value / 100m * FSR_SABC;
            AP = AL + AM + AN + AO;
            AQ = FSR_SACAL > 0 ? AP / FSR_SACAL : 0;

            BH      = AQ * FSR_FSI;
            return BH + FSR_FSI;
        }
    }
}
