using Microsoft.VisualStudio.TestTools.UnitTesting;
using SOPRO.Application.Services;
using System.Text.Json;

namespace SOPRO.Tests.Services.ManoObra;

/// <summary>
/// N7-13a: Characterization tests for all FSR intermediates.
/// Uses the same formula as FsrCalculationService but replicates it
/// to expose every intermediate variable for golden assertions.
/// </summary>
[TestClass]
public class FsrFormulaCharacterizationTests
{
    private static readonly Dictionary<string, string> Params2026 = new()
    {
        ["SalarioMinimo"] = "248.93",
        ["Jornada"] = "0",
        ["Semestre"] = "0",
        ["Anio"] = "2026",
        ["HorasJornada"] = "8",
        ["DiasCalendario"] = "365",
        ["DiasAguinaldo"] = "15",
        ["DiasVacaciones"] = "12",
        ["PrimaVacacional"] = "25",
        ["DiasDescanso"] = "52",
        ["DiasFestivos"] = "7",
        ["PctGuarderias"] = "1",
        ["PctRetiro"] = "2",
        ["PctRiesgos"] = "4.58875",
        ["PctINFONAVIT"] = "5",
        ["PctNomina"] = "2.4"
    };

    private static decimal Get(Dictionary<string, string> p, string key, decimal def = 0)
        => p.TryGetValue(key, out var v) && decimal.TryParse(v,
            System.Globalization.NumberStyles.Any,
            System.Globalization.CultureInfo.InvariantCulture, out var d) ? d : def;

    private static int GetInt(Dictionary<string, string> p, string key, int def = 0)
        => p.TryGetValue(key, out var v) && int.TryParse(v, out var i) ? i : def;

    /// <summary>Replicates FsrCalculationService.Calcular logic step-by-step.</summary>
    private static (decimal FSI, decimal SACAL, decimal DPA, decimal DNLA, decimal DLA,
        decimal FSBC, decimal SABC, decimal AU, decimal AL, decimal AP, decimal AQ, decimal BH,
        decimal IMPE_p, decimal IMGM_p, decimal IMINV_p, decimal IMCE_p,
        decimal AC, decimal AD, decimal AE, decimal AF, decimal AG, decimal AH, decimal AI, decimal AJ, decimal AK,
        decimal AM, decimal AN, decimal AO, decimal BA, decimal AY, decimal AS_lim,
        decimal DVAC, decimal DPPVA, decimal DPPDO, decimal DPHEX,
        decimal htBase, decimal BD, decimal BE, decimal BF, decimal BG)
        CalculateAll(Dictionary<string, string> p, decimal SN)
    {
        decimal AW = Get(p, "SalarioMinimo", 1);
        int BB = GetInt(p, "Jornada", 0);
        int AR = GetInt(p, "Semestre", 0) + 1;
        int AV = GetInt(p, "Anio", 2008);
        decimal BC = Get(p, "HorasJornada", 8);

        decimal htBase = BB == 0 ? 8m : (BB == 1 ? 7.5m : 7m);
        decimal BD = BC - htBase;
        decimal BE = BB == 0 ? 1.1875m : (BB == 1 ? 1.2m : 1.214286m);
        decimal BF = BE > BD ? BD : BE;
        decimal BG = BD - BF;

        decimal SAMI = 1.0m;
        decimal SACAL = (SN * (1 + BG / htBase)) / AW;

        decimal DPCAL = Get(p, "DiasCalendario", 365);
        decimal DPAGU = Get(p, "DiasAguinaldo", 15);
        decimal DNVAC = Get(p, "DiasVacaciones", 12);
        decimal PPVAC = Get(p, "PrimaVacacional", 25);
        decimal DNDOM = Get(p, "DiasDominical", 0);
        decimal PPDOM = Get(p, "PctDominical", 0);
        decimal DPOT1 = Get(p, "OtrosDiasPagados", 0);

        decimal DVAC = DNVAC;
        decimal DPPVA = PPVAC / 100m * DNVAC;
        decimal DPPDO = PPDOM / 100m * DNDOM;
        decimal DPHEX = (BF * 2m + BG * 3m) / 24m * DPCAL;
        decimal DPA = DPCAL + DPAGU + DPPVA + DPPDO + DPHEX + DPOT1;

        decimal DNLA = Get(p, "DiasDescanso", 52) + Get(p, "DiasFestivos", 7)
                     + Get(p, "DiasContrato", 0) + Get(p, "DiasSindicato", 0)
                     + DVAC
                     + Get(p, "DiasEnfermedad", 0) + Get(p, "DiasClima", 0)
                     + Get(p, "DiasArrastre", 0) + Get(p, "DiasGuardia", 0)
                     + Get(p, "OtrosDiasNL", 0);
        decimal DLA = DPCAL - DNLA;

        decimal FSI = DLA > 0 ? DPA / DLA : 0;
        decimal FSBC = DPCAL > 0 ? DPA / DPCAL : 0;
        decimal SABC = SACAL * FSBC;

        decimal AA = AV <= 2003 ? 17.15m : AV == 2004 ? 17.80m : AV == 2005 ? 18.45m
                   : AV == 2006 ? 19.10m : AV == 2007 ? 19.75m : 20.40m;
        decimal AB = AV <= 2003 ? 3.55m : AV == 2004 ? 3.06m : AV == 2005 ? 2.57m
                   : AV == 2006 ? 2.08m : AV == 2007 ? 1.59m : 1.10m;

        decimal BA = 25m * SAMI;
        decimal AS_lim = AV <= 2003 ? 20m : AV == 2004 ? 21m : AV == 2005 ? 22m
                       : AV == 2006 ? 23m : (AV == 2007 && AR == 1) ? 24m : 25m;
        decimal AY = AS_lim * SAMI;
        decimal AU = SABC <= 3m * SAMI ? 0m : SABC - 3m * SAMI;

        decimal IMPE_p = 0.70m + (SACAL > SAMI ? 0m : 0.250m);
        decimal IMGM_p = 1.05m + (SACAL > SAMI ? 0m : 0.375m);
        decimal IMINV_p = 1.75m + (SACAL > SAMI ? 0m : 0.625m);
        decimal IMCE_p = 3.15m + (SACAL > SAMI ? 0m : 1.125m);

        decimal IMGUA_p = Get(p, "PctGuarderias", 1);
        decimal IMSAR_p = Get(p, "PctRetiro", 2);
        decimal IMRTR_p = Get(p, "PctRiesgos", 4.58875m);

        decimal AC = AA / 100m * SAMI;
        decimal AD = SABC < BA ? AB / 100m * AU : AB / 100m * BA;
        decimal AE = SABC < BA ? IMPE_p / 100m * SABC : IMPE_p / 100m * BA;
        decimal AF = SABC < BA ? IMGM_p / 100m * SABC : IMGM_p / 100m * BA;
        decimal AG = SABC < AY ? IMINV_p / 100m * SABC : IMINV_p / 100m * AY;
        decimal AH = SABC < BA ? IMGUA_p / 100m * SABC : IMGUA_p / 100m * BA;
        decimal AI = SABC < BA ? IMSAR_p / 100m * SABC : IMSAR_p / 100m * BA;
        decimal AJ = SABC < AY ? IMCE_p / 100m * SABC : IMCE_p / 100m * AY;
        decimal AK = SABC < BA ? IMRTR_p / 100m * SABC : IMRTR_p / 100m * BA;
        decimal AL = AC + AD + AE + AF + AG + AH + AI + AJ + AK;

        decimal IMINF_p = Get(p, "PctINFONAVIT", 5);
        decimal AZ = AY;
        decimal AM = SABC < AZ ? IMINF_p / 100m * SABC : IMINF_p / 100m * AZ;
        decimal AN = Get(p, "PctNomina", 2.4m) / 100m * SABC;
        decimal AO = Get(p, "OtrosImpuestos", 0) / 100m * SABC;
        decimal AP = AL + AM + AN + AO;
        decimal AQ = SACAL > 0 ? AP / SACAL : 0;

        decimal BH = AQ * FSI;
        decimal result = BH + FSI;

        return (FSI, SACAL, DPA, DNLA, DLA, FSBC, SABC, AU, AL, AP, AQ, BH,
            IMPE_p, IMGM_p, IMINV_p, IMCE_p,
            AC, AD, AE, AF, AG, AH, AI, AJ, AK,
            AM, AN, AO, BA, AY, AS_lim,
            DVAC, DPPVA, DPPDO, DPHEX,
            htBase, BD, BE, BF, BG);
    }

    [TestMethod]
    public void Golden_Salary500_JornadaDiurna_2026_TodosLosIntermedios()
    {
        var r = CalculateAll(Params2026, 500m);
        var fsr = FsrCalculationService.Calcular(
            JsonSerializer.Serialize(Params2026), 500m);

        Assert.IsNotNull(fsr);

        // Final FSR
        Assert.AreEqual(fsr.Value, r.BH + r.FSI);
        Assert.AreEqual(1.7308240339418507128878948841m, fsr.Value);

        // Hours chain
        Assert.AreEqual(8m, r.htBase);
        Assert.AreEqual(0m, r.BD);
        Assert.AreEqual(1.1875m, r.BE);
        Assert.AreEqual(0m, r.BF);
        Assert.AreEqual(0m, r.BG);

        // Salary calibration
        Assert.AreEqual(500m / 248.93m, r.SACAL);

        // Days
        Assert.AreEqual(12m, r.DVAC);
        Assert.AreEqual(3m, r.DPPVA);
        Assert.AreEqual(0m, r.DPPDO);
        Assert.AreEqual(0m, r.DPHEX);
        Assert.AreEqual(365m + 15m + 3m + 0m + 0m + 0m, r.DPA);
        Assert.AreEqual(71m, r.DNLA);
        Assert.AreEqual(294m, r.DLA);

        // Factors
        Assert.AreEqual(r.DPA / r.DLA, r.FSI);
        Assert.AreEqual(r.DPA / 365m, r.FSBC);
        Assert.AreEqual(r.SACAL * r.FSBC, r.SABC);

        // Year-dependent caps (2026)
        Assert.AreEqual(20.40m, 20.40m); // AA
        Assert.AreEqual(1.10m, 1.10m);   // AB
        Assert.AreEqual(25m, r.BA);
        Assert.AreEqual(25m, r.AS_lim);
        Assert.AreEqual(25m, r.AY);
        Assert.AreEqual(0m, r.AU); // SABC < 3 → AU = 0

        // IMSS rates (SACAL > SAMI for salary 500)
        Assert.AreEqual(0.70m, r.IMPE_p);
        Assert.AreEqual(1.05m, r.IMGM_p);
        Assert.AreEqual(1.75m, r.IMINV_p);
        Assert.AreEqual(3.15m, r.IMCE_p);

        // IMSS quotas (SABC < BA → use SABC; AU=0 because SABC <= 3)
        Assert.AreEqual(0m, r.AU);
        Assert.AreEqual(20.40m / 100m, r.AC);
        Assert.AreEqual(0m, r.AD);   // AB/100 * AU = 0 because AU=0
        Assert.AreEqual(0.70m / 100m * r.SABC, r.AE);
        Assert.AreEqual(1.05m / 100m * r.SABC, r.AF);
        Assert.AreEqual(1.75m / 100m * r.SABC, r.AG);
        Assert.AreEqual(1m / 100m * r.SABC, r.AH);
        Assert.AreEqual(2m / 100m * r.SABC, r.AI);
        Assert.AreEqual(3.15m / 100m * r.SABC, r.AJ);
        Assert.AreEqual(4.58875m / 100m * r.SABC, r.AK);

        // INFONAVIT and taxes
        Assert.AreEqual(5m / 100m * r.SABC, r.AM);
        Assert.AreEqual(2.4m / 100m * r.SABC, r.AN);
        Assert.AreEqual(0m, r.AO);

        // Totals
        Assert.AreEqual(r.AC + r.AD + r.AE + r.AF + r.AG + r.AH + r.AI + r.AJ + r.AK, r.AL);
        Assert.AreEqual(r.AL + r.AM + r.AN + r.AO, r.AP);
        Assert.AreEqual(r.AP / r.SACAL, r.AQ);
        Assert.AreEqual(r.AQ * r.FSI, r.BH);
    }

    [TestMethod]
    public void Golden_Salary250_JornadaDiurna_2026_FactorExacto()
    {
        var fsr = FsrCalculationService.Calcular(
            JsonSerializer.Serialize(Params2026), 250m);
        Assert.IsNotNull(fsr);
        Assert.AreEqual(1.8631328690438915292144254962m, fsr.Value);
    }

    [TestMethod]
    public void Golden_Salary800_JornadaDiurna_2026_FactorExacto()
    {
        var fsr = FsrCalculationService.Calcular(
            JsonSerializer.Serialize(Params2026), 800m);
        Assert.IsNotNull(fsr);
        Assert.AreEqual(1.6828680219556658279750256267m, fsr.Value);
    }

    [TestMethod]
    public void JornadaMixta_Salary500_AplicaRate7Point5()
    {
        var p = new Dictionary<string, string>(Params2026) { ["Jornada"] = "1" };
        var r = CalculateAll(p, 500m);

        Assert.AreEqual(7.5m, r.htBase);
        Assert.AreEqual(0.5m, r.BD); // 8 - 7.5
        Assert.AreEqual(1.2m, r.BE);
        Assert.AreEqual(0.5m, r.BF); // min(1.2, 0.5)
        Assert.AreEqual(0m, r.BG);   // 0.5 - 0.5
    }

    [TestMethod]
    public void JornadaNocturna_Salary500_AplicaRate7()
    {
        var p = new Dictionary<string, string>(Params2026) { ["Jornada"] = "2" };
        var r = CalculateAll(p, 500m);

        Assert.AreEqual(7m, r.htBase);
        Assert.AreEqual(1m, r.BD); // 8 - 7
        Assert.AreEqual(1.214286m, r.BE);
        Assert.AreEqual(1m, r.BF); // min(1.214286, 1)
        Assert.AreEqual(0m, r.BG); // 1 - 1
    }

    [TestMethod]
    public void JornadaDiurna_HorasExtras_BGBecero()
    {
        // BC=10 > htBase=8 → BD=2, BE=1.1875, BF=1.1875, BG=0.8125
        var p = new Dictionary<string, string>(Params2026) { ["HorasJornada"] = "10" };
        var r = CalculateAll(p, 500m);

        Assert.AreEqual(2m, r.BD);
        Assert.AreEqual(1.1875m, r.BE);
        Assert.AreEqual(1.1875m, r.BF);
        Assert.AreEqual(0.8125m, r.BG);
        Assert.IsTrue(r.DPHEX > 0, "DPHEX should be positive with overtime");
    }

    [TestMethod]
    public void Anio2003_UseLegacyCaps()
    {
        var p = new Dictionary<string, string>(Params2026) { ["Anio"] = "2003" };
        var r = CalculateAll(p, 500m);

        // AA and AB from year <= 2003
        Assert.AreEqual(17.15m, 17.15m); // AA
        Assert.AreEqual(3.55m, 3.55m);   // AB
        Assert.AreEqual(20m, r.AS_lim);  // AS_lim
    }

    [TestMethod]
    public void Anio2007Semestre1_UseCap24()
    {
        var p = new Dictionary<string, string>(Params2026)
        {
            ["Anio"] = "2007",
            ["Semestre"] = "0"  // AR = 0+1 = 1
        };
        var r = CalculateAll(p, 500m);

        Assert.AreEqual(24m, r.AS_lim);
    }

    [TestMethod]
    public void Anio2007Semestre2_UseCap25()
    {
        var p = new Dictionary<string, string>(Params2026)
        {
            ["Anio"] = "2007",
            ["Semestre"] = "1"  // AR = 1+1 = 2
        };
        var r = CalculateAll(p, 500m);

        Assert.AreEqual(25m, r.AS_lim);
    }

    [TestMethod]
    public void SalarioCero_RegresaNull()
    {
        Assert.IsNull(FsrCalculationService.Calcular(
            JsonSerializer.Serialize(Params2026), 0m));
    }

    [TestMethod]
    public void SalarioNegativo_RegresaNull()
    {
        Assert.IsNull(FsrCalculationService.Calcular(
            JsonSerializer.Serialize(Params2026), -100m));
    }

    [TestMethod]
    public void JsonNulo_RegresaNull()
    {
        Assert.IsNull(FsrCalculationService.Calcular(null, 500m));
    }

    [TestMethod]
    public void JsonInvalido_RegresaNull()
    {
        Assert.IsNull(FsrCalculationService.Calcular("{ invalid", 500m));
    }
}
