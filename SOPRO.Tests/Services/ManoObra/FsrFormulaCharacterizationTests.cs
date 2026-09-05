using Microsoft.VisualStudio.TestTools.UnitTesting;
using SOPRO.Application.Services;
using System.Text.Json;

namespace SOPRO.Tests.Services.ManoObra;

/// <summary>
/// N7-13a: Characterization tests for the legacy FSR formula.
/// The test-only breakdown exposes intermediates that the current production
/// API does not return; every relevant value is frozen with an exact golden.
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
    private static (decimal FSI, decimal SAMI, decimal SACAL, decimal DPCAL, decimal DPA, decimal DNLA, decimal DLA,
        decimal FSBC, decimal SABC, decimal AA, decimal AB, decimal AU, decimal AL, decimal AP, decimal AQ, decimal BH,
        decimal IMPE_p, decimal IMGM_p, decimal IMINV_p, decimal IMCE_p,
        decimal AC, decimal AD, decimal AE, decimal AF, decimal AG, decimal AH, decimal AI, decimal AJ, decimal AK,
        decimal AM, decimal AN, decimal AO, decimal BA, decimal AY, decimal AZ, decimal AS_lim, decimal IMIMS,
        decimal DVAC, decimal DPPVA, decimal DPPDO, decimal DPHEX,
        decimal htBase, decimal BD, decimal BE, decimal BF, decimal BG, decimal FSR)
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
        decimal IMIMS = SACAL > 0 ? AL / SACAL : 0;

        decimal IMINF_p = Get(p, "PctINFONAVIT", 5);
        decimal AZ = AY;
        decimal AM = SABC < AZ ? IMINF_p / 100m * SABC : IMINF_p / 100m * AZ;
        decimal AN = Get(p, "PctNomina", 2.4m) / 100m * SABC;
        decimal AO = Get(p, "OtrosImpuestos", 0) / 100m * SABC;
        decimal AP = AL + AM + AN + AO;
        decimal AQ = SACAL > 0 ? AP / SACAL : 0;

        decimal BH = AQ * FSI;
        decimal result = BH + FSI;

        return (FSI, SAMI, SACAL, DPCAL, DPA, DNLA, DLA, FSBC, SABC, AA, AB, AU, AL, AP, AQ, BH,
            IMPE_p, IMGM_p, IMINV_p, IMCE_p,
            AC, AD, AE, AF, AG, AH, AI, AJ, AK,
            AM, AN, AO, BA, AY, AZ, AS_lim, IMIMS,
            DVAC, DPPVA, DPPDO, DPHEX,
            htBase, BD, BE, BF, BG, result);
    }

    [TestMethod]
    public void Golden_Salary500_JornadaDiurna_2026_TodosLosIntermedios()
    {
        var r = CalculateAll(Params2026, 500m);
        var fsr = FsrCalculationService.Calcular(
            JsonSerializer.Serialize(Params2026), 500m);

        Assert.IsNotNull(fsr);

        Assert.AreEqual(fsr.Value, r.BH + r.FSI);
        Assert.AreEqual(1.7308240339418507128878948841m, fsr.Value);

        Assert.AreEqual(8m, r.htBase);
        Assert.AreEqual(0m, r.BD);
        Assert.AreEqual(1.1875m, r.BE);
        Assert.AreEqual(0m, r.BF);
        Assert.AreEqual(0m, r.BG);

        Assert.AreEqual(1m, r.SAMI);
        Assert.AreEqual(2.0085967942795163298919374925m, r.SACAL);

        Assert.AreEqual(365m, r.DPCAL);
        Assert.AreEqual(12m, r.DVAC);
        Assert.AreEqual(3m, r.DPPVA);
        Assert.AreEqual(0m, r.DPPDO);
        Assert.AreEqual(0m, r.DPHEX);
        Assert.AreEqual(383m, r.DPA);
        Assert.AreEqual(71m, r.DNLA);
        Assert.AreEqual(294m, r.DLA);

        Assert.AreEqual(1.3027210884353741496598639456m, r.FSI);
        Assert.AreEqual(1.0493150684931506849315068493m, r.FSBC);
        Assert.AreEqual(2.1076508827645335735578412592m, r.SABC);

        Assert.AreEqual(20.40m, r.AA);
        Assert.AreEqual(1.10m, r.AB);
        Assert.AreEqual(25m, r.BA);
        Assert.AreEqual(25m, r.AS_lim);
        Assert.AreEqual(25m, r.AY);
        Assert.AreEqual(25m, r.AZ);
        Assert.AreEqual(0m, r.AU);

        Assert.AreEqual(0.70m, r.IMPE_p);
        Assert.AreEqual(1.05m, r.IMGM_p);
        Assert.AreEqual(1.75m, r.IMINV_p);
        Assert.AreEqual(3.15m, r.IMCE_p);

        Assert.AreEqual(0.204m, r.AC);
        Assert.AreEqual(0m, r.AD);
        Assert.AreEqual(0.0147535561793517350149048888m, r.AE);
        Assert.AreEqual(0.0221303342690276025223573332m, r.AF);
        Assert.AreEqual(0.0368838904483793375372622220m, r.AG);
        Assert.AreEqual(0.0210765088276453357355784126m, r.AH);
        Assert.AreEqual(0.0421530176552906714711568252m, r.AI);
        Assert.AreEqual(0.0663910028070828075670719997m, r.AJ);
        Assert.AreEqual(0.0967148298828575343566354408m, r.AK);
        Assert.AreEqual(0.5041031400696350242049671223m, r.AL);
        Assert.AreEqual(0.2509727893150684931506849315m, r.IMIMS);

        Assert.AreEqual(0.1053825441382266786778920630m, r.AM);
        Assert.AreEqual(0.0505836211863488057653881902m, r.AN);
        Assert.AreEqual(0m, r.AO);
        Assert.AreEqual(0.6600693053942105086482473755m, r.AP);
        Assert.AreEqual(0.3286221043835616438356164384m, r.AQ);
        Assert.AreEqual(0.4281029455064765632280309385m, r.BH);
        Assert.AreEqual(1.7308240339418507128878948841m, r.FSR);
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
        Assert.AreEqual(15.208333333333333333333333346m, r.DPHEX);
        Assert.AreEqual(1.8117642796659389592975698651m,
            FsrCalculationService.Calcular(JsonSerializer.Serialize(p), 500m));
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
        Assert.AreEqual(30.416666666666666666666666654m, r.DPHEX);
        Assert.AreEqual(1.8936373182554372586172977560m,
            FsrCalculationService.Calcular(JsonSerializer.Serialize(p), 500m));
    }

    [TestMethod]
    public void JornadaDiurna_HorasExtras_BGNoEsCero()
    {
        // BC=10 > htBase=8 → BD=2, BE=1.1875, BF=1.1875, BG=0.8125
        var p = new Dictionary<string, string>(Params2026) { ["HorasJornada"] = "10" };
        var r = CalculateAll(p, 500m);

        Assert.AreEqual(2m, r.BD);
        Assert.AreEqual(1.1875m, r.BE);
        Assert.AreEqual(1.1875m, r.BF);
        Assert.AreEqual(0.8125m, r.BG);
        Assert.AreEqual(73.190104166666666666666666654m, r.DPHEX);
        Assert.AreEqual(2.1143764300047069863859539774m,
            FsrCalculationService.Calcular(JsonSerializer.Serialize(p), 500m));
    }

    [TestMethod]
    public void Anio2003_UseLegacyCaps()
    {
        var p = new Dictionary<string, string>(Params2026) { ["Anio"] = "2003" };
        var r = CalculateAll(p, 6000m);

        Assert.AreEqual(17.15m, r.AA);
        Assert.AreEqual(3.55m, r.AB);
        Assert.AreEqual(20m, r.AS_lim);
        Assert.AreEqual(1.6259638389502036545211691982m,
            FsrCalculationService.Calcular(JsonSerializer.Serialize(p), 6000m));
    }

    [TestMethod]
    public void Anio2007Semestre1_UseCap24()
    {
        var p = new Dictionary<string, string>(Params2026)
        {
            ["Anio"] = "2007",
            ["Semestre"] = "0"  // AR = 0+1 = 1
        };
        var r = CalculateAll(p, 6000m);

        Assert.AreEqual(24m, r.AS_lim);
        Assert.AreEqual(1.6222885935307025207343211258m,
            FsrCalculationService.Calcular(JsonSerializer.Serialize(p), 6000m));
    }

    [TestMethod]
    public void Anio2007Semestre2_UseCap25()
    {
        var p = new Dictionary<string, string>(Params2026)
        {
            ["Anio"] = "2007",
            ["Semestre"] = "1"  // AR = 1+1 = 2
        };
        var r = CalculateAll(p, 6000m);

        Assert.AreEqual(25m, r.AS_lim);
        Assert.AreEqual(1.6276393184796821125710558196m,
            FsrCalculationService.Calcular(JsonSerializer.Serialize(p), 6000m));
    }

    [TestMethod]
    public void SalarioIgualAlMinimo_AplicaTasasObreroPatronales()
    {
        var r = CalculateAll(Params2026, 248.93m);

        Assert.AreEqual(1m, r.SACAL);
        Assert.AreEqual(0.95m, r.IMPE_p);
        Assert.AreEqual(1.425m, r.IMGM_p);
        Assert.AreEqual(2.375m, r.IMINV_p);
        Assert.AreEqual(4.275m, r.IMCE_p);
        Assert.AreEqual(1.8967357164989283384586711398m,
            FsrCalculationService.Calcular(JsonSerializer.Serialize(Params2026), 248.93m));
    }

    [TestMethod]
    public void JornadaDesconocida_SeTrataComoNocturna()
    {
        var nocturna = new Dictionary<string, string>(Params2026) { ["Jornada"] = "2" };
        var desconocida = new Dictionary<string, string>(Params2026) { ["Jornada"] = "99" };

        Assert.AreEqual(
            FsrCalculationService.Calcular(JsonSerializer.Serialize(nocturna), 500m),
            FsrCalculationService.Calcular(JsonSerializer.Serialize(desconocida), 500m));
    }

    [TestMethod]
    public void ParametrosOmitidos_UsanDefaultsLegacy()
    {
        Assert.AreEqual(1.3493076147435001397819401734m,
            FsrCalculationService.Calcular("{}", 500m));
    }

    [TestMethod]
    public void DiasLaboradosNoPositivos_RegresaFactorCero()
    {
        var p = new Dictionary<string, string>(Params2026) { ["DiasDescanso"] = "365" };

        Assert.AreEqual(0m,
            FsrCalculationService.Calcular(JsonSerializer.Serialize(p), 500m));
    }

    [TestMethod]
    public void SalarioMinimoCero_RegresaNullPorDivisionEntreCero()
    {
        var p = new Dictionary<string, string>(Params2026) { ["SalarioMinimo"] = "0" };

        Assert.IsNull(FsrCalculationService.Calcular(JsonSerializer.Serialize(p), 500m));
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
