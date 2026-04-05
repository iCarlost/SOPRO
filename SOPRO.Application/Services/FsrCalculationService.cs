using System;
using System.Collections.Generic;
using System.Text.Json;

namespace SOPRO.Application.Services
{
    /// <summary>
    /// Calcula el Factor de Salario Real (FSR) para un salario base dado,
    /// usando los parámetros almacenados en Proyecto.ParametrosFSR.
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

                decimal Get(string key, decimal def = 0)
                    => p.TryGetValue(key, out var v) && decimal.TryParse(v,
                        System.Globalization.NumberStyles.Any,
                        System.Globalization.CultureInfo.InvariantCulture, out var d) ? d : def;
                int GetInt(string key, int def = 0)
                    => p.TryGetValue(key, out var v) && int.TryParse(v, out var i) ? i : def;

                decimal AW = Get("SalarioMinimo", 1);
                int BB = GetInt("Jornada", 0);
                int AR = GetInt("Semestre", 0) + 1;
                int AV = GetInt("Anio", 2008);
                decimal BC = Get("HorasJornada", 8);

                decimal htBase = BB == 0 ? 8m : (BB == 1 ? 7.5m : 7m);
                decimal BD = BC - htBase;
                decimal BE = BB == 0 ? 1.1875m : (BB == 1 ? 1.2m : 1.214286m);
                decimal BF = BE > BD ? BD : BE;
                decimal BG = BD - BF;

                decimal FSR_SAMI = 1.0m;
                decimal FSR_SACAL = (salarioNominal * (1 + BG / htBase)) / AW;

                decimal FSR_DPCAL = Get("DiasCalendario", 365);
                decimal FSR_DPAGU = Get("DiasAguinaldo", 15);
                decimal FSR_DNVAC = Get("DiasVacaciones", 12);
                decimal FSR_PPVAC = Get("PrimaVacacional", 25);
                decimal FSR_DNDOM = Get("DiasDominical", 0);
                decimal FSR_PPDOM = Get("PctDominical", 0);
                decimal FSR_DPOT1 = Get("OtrosDiasPagados", 0);

                decimal FSR_DVAC = FSR_DNVAC;
                decimal FSR_DPPVA = FSR_PPVAC / 100m * FSR_DNVAC;
                decimal FSR_DPPDO = FSR_PPDOM / 100m * FSR_DNDOM;
                decimal FSR_DPHEX = (BF * 2m + BG * 3m) / 24m * FSR_DPCAL;
                decimal FSR_DPA = FSR_DPCAL + FSR_DPAGU + FSR_DPPVA + FSR_DPPDO + FSR_DPHEX + FSR_DPOT1;

                decimal FSR_DNLA = Get("DiasDescanso", 52) + Get("DiasFestivos", 7)
                                 + Get("DiasContrato", 0) + Get("DiasSindicato", 0)
                                 + FSR_DVAC
                                 + Get("DiasEnfermedad", 0) + Get("DiasClima", 0)
                                 + Get("DiasArrastre", 0) + Get("DiasGuardia", 0)
                                 + Get("OtrosDiasNL", 0);
                decimal FSR_DLA = FSR_DPCAL - FSR_DNLA;

                decimal FSR_FSI = FSR_DLA > 0 ? FSR_DPA / FSR_DLA : 0;
                decimal FSR_FSBC = FSR_DPCAL > 0 ? FSR_DPA / FSR_DPCAL : 0;
                decimal FSR_SABC = FSR_SACAL * FSR_FSBC;

                decimal AA = AV <= 2003 ? 17.15m : AV == 2004 ? 17.80m : AV == 2005 ? 18.45m
                           : AV == 2006 ? 19.10m : AV == 2007 ? 19.75m : 20.40m;
                decimal AB = AV <= 2003 ? 3.55m : AV == 2004 ? 3.06m : AV == 2005 ? 2.57m
                           : AV == 2006 ? 2.08m : AV == 2007 ? 1.59m : 1.10m;

                decimal BA = 25m * FSR_SAMI;
                decimal AS_lim = AV <= 2003 ? 20m : AV == 2004 ? 21m : AV == 2005 ? 22m
                               : AV == 2006 ? 23m : (AV == 2007 && AR == 1) ? 24m : 25m;
                decimal AY = AS_lim * FSR_SAMI;
                decimal AU = FSR_SABC <= 3m * FSR_SAMI ? 0m : FSR_SABC - 3m * FSR_SAMI;

                decimal FSR_IMPE_p = 0.70m + (FSR_SACAL > FSR_SAMI ? 0m : 0.250m);
                decimal FSR_IMGM_p = 1.05m + (FSR_SACAL > FSR_SAMI ? 0m : 0.375m);
                decimal FSR_IMINV_p = 1.75m + (FSR_SACAL > FSR_SAMI ? 0m : 0.625m);
                decimal FSR_IMCE_p = 3.15m + (FSR_SACAL > FSR_SAMI ? 0m : 1.125m);

                decimal FSR_IMGUA_p = Get("PctGuarderias", 1);
                decimal FSR_IMSAR_p = Get("PctRetiro", 2);
                decimal FSR_IMRTR_p = Get("PctRiesgos", 4.58875m);

                decimal AC = AA / 100m * FSR_SAMI;
                decimal AD = FSR_SABC < BA ? AB / 100m * AU : AB / 100m * BA;
                decimal AE = FSR_SABC < BA ? FSR_IMPE_p / 100m * FSR_SABC : FSR_IMPE_p / 100m * BA;
                decimal AF = FSR_SABC < BA ? FSR_IMGM_p / 100m * FSR_SABC : FSR_IMGM_p / 100m * BA;
                decimal AG = FSR_SABC < AY ? FSR_IMINV_p / 100m * FSR_SABC : FSR_IMINV_p / 100m * AY;
                decimal AH = FSR_SABC < BA ? FSR_IMGUA_p / 100m * FSR_SABC : FSR_IMGUA_p / 100m * BA;
                decimal AI = FSR_SABC < BA ? FSR_IMSAR_p / 100m * FSR_SABC : FSR_IMSAR_p / 100m * BA;
                decimal AJ = FSR_SABC < AY ? FSR_IMCE_p / 100m * FSR_SABC : FSR_IMCE_p / 100m * AY;
                decimal AK = FSR_SABC < BA ? FSR_IMRTR_p / 100m * FSR_SABC : FSR_IMRTR_p / 100m * BA;
                decimal AL = AC + AD + AE + AF + AG + AH + AI + AJ + AK;

                decimal AZ = AY;
                decimal FSR_IMINF_p = Get("PctINFONAVIT", 5);
                decimal AM = FSR_SABC < AZ ? FSR_IMINF_p / 100m * FSR_SABC : FSR_IMINF_p / 100m * AZ;
                decimal AN = Get("PctNomina", 2.4m) / 100m * FSR_SABC;
                decimal AO = Get("OtrosImpuestos", 0) / 100m * FSR_SABC;
                decimal AP = AL + AM + AN + AO;
                decimal AQ = FSR_SACAL > 0 ? AP / FSR_SACAL : 0;

                decimal BH = AQ * FSR_FSI;
                return BH + FSR_FSI;
            }
            catch
            {
                return null;
            }
        }
    }
}
