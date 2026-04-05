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
    /// Cálculo del Factor de Salario Real (FSR)
    /// Basado en Arts. 160 y 161 RLOPSRM y legislación laboral mexicana vigente.
    /// Metodología OPUS Planet — fórmulas verificadas contra PDF de ejemplo.
    /// </summary>
    public partial class FormFSR : Form, IGridFormato
    {

        public DataGridView GridPrincipal => null;
        public ColumnaPersonalizada ColumnaSeleccionada => null;
        public event EventHandler ColumnaSeleccionadaCambiada;

        public void AplicarFormato(ColumnaPersonalizada fmt)
        {
            // FSR no expone grid formateable en ribbon.
        }

        public void AplicarFormatoGlobal(ColumnaPersonalizada fmt)
        {
            // FSR no expone grid formateable en ribbon.
        }

        public bool GenerarReporteExcel() => GenerarReporteExcelRibbon();
        private readonly SOPROContext _context;
        private readonly Proyecto _proyecto;

        // ── Variables intermedias de cálculo ──────────────────────────────────
        // Básicos
        private decimal BD, BE, BF, BG;
        private decimal FSR_SAMI, FSR_SACAL;
        // Días
        private decimal FSR_DVAC, FSR_DPPVA, FSR_DPPDO, FSR_DPHEX;
        private decimal FSR_DPA, FSR_DNLA, FSR_DLA;
        private decimal FSR_FSI, FSR_FSBC, FSR_SABC;
        // IMSS cuotas
        private decimal AA, AB, AU;
        private decimal FSR_IMPE_p, FSR_IMGM_p, FSR_IMINV_p, FSR_IMCE_p;
        private decimal BA, AS_lim, AY;
        private decimal AC, AD, AE, AF, AG, AH, AI, AJ, AK, AL;
        private decimal FSR_IMIMS;
        // INFONAVIT y otros
        private decimal AZ, AM, AN, AO, AP, AQ;
        // FSR final
        private decimal BH, FSR_FSR;

        public FormFSR(SOPROContext context, Proyecto proyecto)
        {
            InitializeComponent();
            _context  = context  ?? throw new ArgumentNullException(nameof(context));
            _proyecto = proyecto ?? throw new ArgumentNullException(nameof(proyecto));

            CargarValoresPorDefecto();
            RestaurarParametros();
            SuscribirEventos();
            Recalcular();
            new EditableReportTitleHelper(_context, panelTop, lblTitulo, () => _proyecto.Id, ReportTitleModuleKeys.FSR).Attach();

        }

        // ──────────────────────────────────────────────────────────────────────
        // VALORES POR DEFECTO (datos típicos de obra pública en México)
        // ──────────────────────────────────────────────────────────────────────
        private string GuardarParametros()
        {
            var p = new Dictionary<string, string>
            {
                ["SalarioNominal"]   = nudSalarioNominal.Value.ToString(),
                ["SalarioMinimo"]    = nudSalarioMinimo.Value.ToString(),
                ["Anio"]             = nudAnio.Value.ToString(),
                ["Semestre"]         = cboSemestre.SelectedIndex.ToString(),
                ["Jornada"]          = cboJornada.SelectedIndex.ToString(),
                ["HorasJornada"]     = nudHorasJornada.Value.ToString(),
                ["DiasCalendario"]   = nudDiasCalendario.Value.ToString(),
                ["DiasAguinaldo"]    = nudDiasAguinaldo.Value.ToString(),
                ["DiasVacaciones"]   = nudDiasVacaciones.Value.ToString(),
                ["PrimaVacacional"]  = nudPrimaVacacional.Value.ToString(),
                ["DiasDominical"]    = nudDiasDominical.Value.ToString(),
                ["PctDominical"]     = nudPctDominical.Value.ToString(),
                ["OtrosDiasPagados"] = nudOtrosDiasPagados.Value.ToString(),
                ["DiasDescanso"]     = nudDiasDescanso.Value.ToString(),
                ["DiasFestivos"]     = nudDiasFestivos.Value.ToString(),
                ["DiasContrato"]     = nudDiasContrato.Value.ToString(),
                ["DiasSindicato"]    = nudDiasSindicato.Value.ToString(),
                ["DiasEnfermedad"]   = nudDiasEnfermedad.Value.ToString(),
                ["DiasClima"]        = nudDiasClima.Value.ToString(),
                ["DiasArrastre"]     = nudDiasArrastre.Value.ToString(),
                ["DiasGuardia"]      = nudDiasGuardia.Value.ToString(),
                ["OtrosDiasNL"]      = nudOtrosDiasNL.Value.ToString(),
                ["PctGuarderias"]    = nudPctGuarderias.Value.ToString(),
                ["PctRetiro"]        = nudPctRetiro.Value.ToString(),
                ["PctRiesgos"]       = nudPctRiesgos.Value.ToString(),
                ["PctINFONAVIT"]     = nudPctINFONAVIT.Value.ToString(),
                ["PctNomina"]        = nudPctNomina.Value.ToString(),
                ["OtrosImpuestos"]   = nudOtrosImpuestos.Value.ToString(),
            };
            return JsonSerializer.Serialize(p);
        }

        private void RestaurarParametros()
        {
            if (string.IsNullOrEmpty(_proyecto.ParametrosFSR)) return;
            try
            {
                var p = JsonSerializer.Deserialize<Dictionary<string, string>>(_proyecto.ParametrosFSR);
                decimal Get(string k) => p.TryGetValue(k, out var v) && decimal.TryParse(v, out var d) ? d : -1;
                int     GetI(string k) => p.TryGetValue(k, out var v) && int.TryParse(v, out var i) ? i : -1;

                if (Get("SalarioNominal")   >= 0) nudSalarioNominal.Value   = Get("SalarioNominal");
                if (Get("SalarioMinimo")    >= 0) nudSalarioMinimo.Value    = Get("SalarioMinimo");
                if (Get("Anio")             >= 0) nudAnio.Value             = Get("Anio");
                if (GetI("Semestre")        >= 0) cboSemestre.SelectedIndex = GetI("Semestre");
                if (GetI("Jornada")         >= 0) cboJornada.SelectedIndex  = GetI("Jornada");
                if (Get("HorasJornada")     >= 0) nudHorasJornada.Value     = Get("HorasJornada");
                if (Get("DiasCalendario")   >= 0) nudDiasCalendario.Value   = Get("DiasCalendario");
                if (Get("DiasAguinaldo")    >= 0) nudDiasAguinaldo.Value    = Get("DiasAguinaldo");
                if (Get("DiasVacaciones")   >= 0) nudDiasVacaciones.Value   = Get("DiasVacaciones");
                if (Get("PrimaVacacional")  >= 0) nudPrimaVacacional.Value  = Get("PrimaVacacional");
                if (Get("DiasDominical")    >= 0) nudDiasDominical.Value    = Get("DiasDominical");
                if (Get("PctDominical")     >= 0) nudPctDominical.Value     = Get("PctDominical");
                if (Get("OtrosDiasPagados") >= 0) nudOtrosDiasPagados.Value = Get("OtrosDiasPagados");
                if (Get("DiasDescanso")     >= 0) nudDiasDescanso.Value     = Get("DiasDescanso");
                if (Get("DiasFestivos")     >= 0) nudDiasFestivos.Value     = Get("DiasFestivos");
                if (Get("DiasContrato")     >= 0) nudDiasContrato.Value     = Get("DiasContrato");
                if (Get("DiasSindicato")    >= 0) nudDiasSindicato.Value    = Get("DiasSindicato");
                if (Get("DiasEnfermedad")   >= 0) nudDiasEnfermedad.Value   = Get("DiasEnfermedad");
                if (Get("DiasClima")        >= 0) nudDiasClima.Value        = Get("DiasClima");
                if (Get("DiasArrastre")     >= 0) nudDiasArrastre.Value     = Get("DiasArrastre");
                if (Get("DiasGuardia")      >= 0) nudDiasGuardia.Value      = Get("DiasGuardia");
                if (Get("OtrosDiasNL")      >= 0) nudOtrosDiasNL.Value      = Get("OtrosDiasNL");
                if (Get("PctGuarderias")    >= 0) nudPctGuarderias.Value    = Get("PctGuarderias");
                if (Get("PctRetiro")        >= 0) nudPctRetiro.Value        = Get("PctRetiro");
                if (Get("PctRiesgos")       >= 0) nudPctRiesgos.Value       = Get("PctRiesgos");
                if (Get("PctINFONAVIT")     >= 0) nudPctINFONAVIT.Value     = Get("PctINFONAVIT");
                if (Get("PctNomina")        >= 0) nudPctNomina.Value        = Get("PctNomina");
                if (Get("OtrosImpuestos")   >= 0) nudOtrosImpuestos.Value   = Get("OtrosImpuestos");
            }
            catch { /* Si falla la deserialización quedan los defaults */ }
        }

        private void CargarValoresPorDefecto()
        {
            lblProyectoVal.Text = _proyecto.Nombre;

            // Básicos
            nudSalarioNominal.Value  = 100.00m;
            nudSalarioMinimo.Value   = 278.80m;  // UMA 2025 aprox referencia, ajustable
            nudAnio.Value            = DateTime.Now.Year;
            cboSemestre.SelectedIndex= DateTime.Now.Month <= 6 ? 0 : 1; // 1=ene-jun, 2=jul-dic
            cboJornada.SelectedIndex = 0;   // 0=Diurna
            nudHorasJornada.Value    = 8m;

            // Días pagados
            nudDiasAguinaldo.Value   = 15.00m;
            nudDiasVacaciones.Value  = 6.00m;
            nudPrimaVacacional.Value = 25.00m;
            nudDiasDominical.Value   = 0.00m;
            nudPctDominical.Value    = 0.00m;
            nudOtrosDiasPagados.Value= 0.00m;

            // Días no laborados
            nudDiasDescanso.Value    = 52.18m;
            nudDiasFestivos.Value    = 7.17m;
            nudDiasContrato.Value    = 0.00m;
            nudDiasSindicato.Value   = 1.00m;
            nudDiasEnfermedad.Value  = 0.45m;
            nudDiasClima.Value       = 3.85m;
            nudDiasArrastre.Value    = 0.00m;
            nudDiasGuardia.Value     = 0.00m;
            nudOtrosDiasNL.Value     = 5.00m;

            // IMSS
            nudPctGuarderias.Value   = 1.00m;
            nudPctRetiro.Value       = 2.00m;
            nudPctRiesgos.Value      = 7.58875m;

            // Otros
            nudPctINFONAVIT.Value    = 5.00m;
            nudPctNomina.Value       = 0.00m;
            nudOtrosImpuestos.Value  = 0.00m;
        }

        private void SuscribirEventos()
        {
            // Básicos
            nudSalarioNominal.ValueChanged  += (s, e) => Recalcular();
            nudSalarioMinimo.ValueChanged   += (s, e) => Recalcular();
            nudAnio.ValueChanged            += (s, e) => Recalcular();
            cboSemestre.SelectedIndexChanged+= (s, e) => Recalcular();
            cboJornada.SelectedIndexChanged += (s, e) => Recalcular();
            nudHorasJornada.ValueChanged    += (s, e) => Recalcular();
            nudDiasCalendario.ValueChanged  += (s, e) => Recalcular();
            // Días pagados
            nudDiasAguinaldo.ValueChanged   += (s, e) => Recalcular();
            nudDiasVacaciones.ValueChanged  += (s, e) => Recalcular();
            nudPrimaVacacional.ValueChanged += (s, e) => Recalcular();
            nudDiasDominical.ValueChanged   += (s, e) => Recalcular();
            nudPctDominical.ValueChanged    += (s, e) => Recalcular();
            nudOtrosDiasPagados.ValueChanged+= (s, e) => Recalcular();
            // Días no laborados
            nudDiasDescanso.ValueChanged    += (s, e) => Recalcular();
            nudDiasFestivos.ValueChanged    += (s, e) => Recalcular();
            nudDiasContrato.ValueChanged    += (s, e) => Recalcular();
            nudDiasSindicato.ValueChanged   += (s, e) => Recalcular();
            nudDiasEnfermedad.ValueChanged  += (s, e) => Recalcular();
            nudDiasClima.ValueChanged       += (s, e) => Recalcular();
            nudDiasArrastre.ValueChanged    += (s, e) => Recalcular();
            nudDiasGuardia.ValueChanged     += (s, e) => Recalcular();
            nudOtrosDiasNL.ValueChanged     += (s, e) => Recalcular();
            // IMSS
            nudPctGuarderias.ValueChanged   += (s, e) => Recalcular();
            nudPctRetiro.ValueChanged       += (s, e) => Recalcular();
            nudPctRiesgos.ValueChanged      += (s, e) => Recalcular();
            // Otros
            nudPctINFONAVIT.ValueChanged    += (s, e) => Recalcular();
            nudPctNomina.ValueChanged       += (s, e) => Recalcular();
            nudOtrosImpuestos.ValueChanged  += (s, e) => Recalcular();
        }

        // ──────────────────────────────────────────────────────────────────────
        // CÁLCULO — Fórmulas OPUS / RLOPSRM Arts. 160-161
        // ──────────────────────────────────────────────────────────────────────
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


        // ──────────────────────────────────────────────────────────────────────
        // ACTUALIZAR UI
        // ──────────────────────────────────────────────────────────────────────
        private void ActualizarUI()
        {
            // Sección días
            lblDPAVal.Text   = $"{FSR_DPA:N5} días";
            lblDNLAVal.Text  = $"{FSR_DNLA:N5} días";
            lblDLAVal.Text   = $"{FSR_DLA:N5} días";
            lblFSIVal.Text   = $"{FSR_FSI:N5}";
            lblFSBCVal.Text  = $"{FSR_FSBC:N5}";
            lblSACBVal.Text  = $"{FSR_SABC:N5}";
            lblSACALVal.Text = $"{FSR_SACAL:N5}";

            // Sección IMSS
            lblACVal.Text    = $"{AC:N5}";
            lblADVal.Text    = $"{AD:N5}";
            lblAEVal.Text    = $"{AE:N5}";
            lblAFVal.Text    = $"{AF:N5}";
            lblAGVal.Text    = $"{AG:N5}";
            lblAHVal.Text    = $"{AH:N5}";
            lblAIVal.Text    = $"{AI:N5}";
            lblAJVal.Text    = $"{AJ:N5}";
            lblAKVal.Text    = $"{AK:N5}";
            lblALVal.Text    = $"{AL:N5}";
            lblIMIMSVal.Text = $"{FSR_IMIMS:N5}";

            // INFONAVIT y otros
            lblAMVal.Text    = $"{AM:N5}";
            lblANVal.Text    = $"{AN:N5}";
            lblAOVal.Text    = $"{AO:N5}";
            lblAPVal.Text    = $"{AP:N5}";
            lblAQVal.Text    = $"{AQ:N5}";

            // FSR Final
            lblBHVal.Text   = $"{BH:N5}";
            lblFSRVal.Text  = $"{FSR_FSR:N5}";

            // Resultado grande en panel bottom
            lblResultadoFSR.Text = $"{FSR_FSR:N5}";

            // Color advertencia si días no laborados > días calendario
            lblDNLAVal.ForeColor = FSR_DNLA >= nudDiasCalendario.Value
                ? System.Drawing.Color.Red
                : System.Drawing.Color.FromArgb(33, 33, 33);
        }



        public void GenerarPdfFSR()
        {
            try
            {
                _proyecto.ParametrosFSR = GuardarParametros();
                Recalcular();

                using var dlg = new SaveFileDialog
                {
                    Title = "Guardar reporte PDF",
                    Filter = "PDF (*.pdf)|*.pdf",
                    FileName = $"CalculoFSR_{DateTime.Now:yyyyMMdd_HHmm}.pdf",
                    DefaultExt = "pdf"
                };
                if (dlg.ShowDialog(this) != DialogResult.OK)
                    return;

                var svcRep = new ReporteService(_context);
                var plantilla = svcRep.ObtenerOCrearPlantilla(_proyecto.Id);
                var tituloCfg = new ConfiguracionTituloReporteService(_context).ObtenerOCrear(_proyecto.Id, ReportTitleModuleKeys.FSR, lblTitulo.Text);

                var filas = new List<GeneradorPdfFSR.FsrPdfRow>
                {
                    GeneradorPdfFSR.FsrPdfRow.Seccion("DATOS BÁSICOS"),
                    GeneradorPdfFSR.FsrPdfRow.Subseccion("Para el cálculo de días pagados"),
                    GeneradorPdfFSR.FsrPdfRow.Numero("Días Calendario (DC)", "", "días", nudDiasCalendario.Value),
                    GeneradorPdfFSR.FsrPdfRow.Numero("Días Aguinaldo", "", "días", nudDiasAguinaldo.Value),
                    GeneradorPdfFSR.FsrPdfRow.Numero("Días de vacaciones para calcular prima vacacional", "", "días", nudDiasVacaciones.Value),
                    GeneradorPdfFSR.FsrPdfRow.Numero("Prima vacacional", "", "%", nudPrimaVacacional.Value),
                    GeneradorPdfFSR.FsrPdfRow.Numero("Otros días", "", "días", nudOtrosDiasPagados.Value),
                    GeneradorPdfFSR.FsrPdfRow.Subseccion("Para el cálculo de días no laborados"),
                    GeneradorPdfFSR.FsrPdfRow.Numero("Descansos semanales", "", "días", nudDiasDescanso.Value),
                    GeneradorPdfFSR.FsrPdfRow.Numero("Días festivos", "", "días", nudDiasFestivos.Value),
                    GeneradorPdfFSR.FsrPdfRow.Numero("Días por costumbre", "", "días", nudDiasContrato.Value),
                    GeneradorPdfFSR.FsrPdfRow.Numero("Días sindicales", "", "días", nudDiasSindicato.Value),
                    GeneradorPdfFSR.FsrPdfRow.Numero("Enfermedad no profesional", "", "días", nudDiasEnfermedad.Value),
                    GeneradorPdfFSR.FsrPdfRow.Numero("Mal tiempo", "", "días", nudDiasClima.Value),
                    GeneradorPdfFSR.FsrPdfRow.Numero("Días por costumbre", "", "días", nudDiasArrastre.Value),
                    GeneradorPdfFSR.FsrPdfRow.Numero("Guardias", "", "días", nudDiasGuardia.Value),
                    GeneradorPdfFSR.FsrPdfRow.Numero("Otros días por costumbre", "", "días", nudOtrosDiasNL.Value),
                    GeneradorPdfFSR.FsrPdfRow.Subseccion("Para el calculo de cuotas del IMSS"),
                    GeneradorPdfFSR.FsrPdfRow.Numero("Guarderías", "", "%", nudPctGuarderias.Value),
                    GeneradorPdfFSR.FsrPdfRow.Numero("Retiro", "", "%", nudPctRetiro.Value),
                    GeneradorPdfFSR.FsrPdfRow.Numero("Riesgos de trabajo", "", "%", nudPctRiesgos.Value),
                    GeneradorPdfFSR.FsrPdfRow.Numero("Impuesto INFONAVIT", "", "%", nudPctINFONAVIT.Value),
                    GeneradorPdfFSR.FsrPdfRow.Numero("Impuesto Nómina", "", "%", nudPctNomina.Value),
                    GeneradorPdfFSR.FsrPdfRow.Numero("Otros impuestos", "", "%", nudOtrosImpuestos.Value),

                    GeneradorPdfFSR.FsrPdfRow.Seccion("CÁLCULO"),
                    GeneradorPdfFSR.FsrPdfRow.Subseccion("De datos básicos a utilizar"),
                    GeneradorPdfFSR.FsrPdfRow.Texto("Salario Mínimo General (D.F.)", "", "", $"{FSR_SAMI:N5}"),
                    GeneradorPdfFSR.FsrPdfRow.Texto("Salario Nominal por jornada (SND)", "", "", $"{FSR_SACAL:N5}"),

                    GeneradorPdfFSR.FsrPdfRow.Subseccion("De días realmente pagados y SBC"),
                    GeneradorPdfFSR.FsrPdfRow.Numero("Vacaciones", "", "días", FSR_DVAC),
                    GeneradorPdfFSR.FsrPdfRow.Numero("Prima vacacional", "", "días", FSR_DPPVA),
                    GeneradorPdfFSR.FsrPdfRow.Numero("Prima Dominical", "", "días", FSR_DPPDO),
                    GeneradorPdfFSR.FsrPdfRow.Numero("Días equivalentes por horas extras al año", "", "días", FSR_DPHEX),
                    GeneradorPdfFSR.FsrPdfRow.Numero("SUMA de días pagados", "", "días", FSR_DPA),
                    GeneradorPdfFSR.FsrPdfRow.Numero("SUMA de días no laborados", "", "días", FSR_DNLA),
                    GeneradorPdfFSR.FsrPdfRow.Numero("Días realmente laborados  (TL = DC - DNLA)", $"{nudDiasCalendario.Value:N6} días - {FSR_DNLA:N6} días", "días", FSR_DLA),
                    GeneradorPdfFSR.FsrPdfRow.Texto("TP/TL", "", "", $"{FSR_FSI:N5}"),
                    GeneradorPdfFSR.FsrPdfRow.Numero("(FSBC = DPA/DPCAL)", $"{FSR_DPA:N6} días / {nudDiasCalendario.Value:N6} días", "", FSR_FSBC),
                    GeneradorPdfFSR.FsrPdfRow.Numero("Salario Base de Cotización (SB = FSBC * SN)", $"{FSR_SACAL:N6} * {FSR_FSBC:N6}", "", FSR_SABC),

                    GeneradorPdfFSR.FsrPdfRow.Subseccion("De cuotas del IMSS"),
                    GeneradorPdfFSR.FsrPdfRow.Numero("Porcentaje sobre salario mínimo para cuota fija", "", "%", AA),
                    GeneradorPdfFSR.FsrPdfRow.Numero("Porcentaje para Excedente a 3 SMGDF", "", "%", AB),
                    GeneradorPdfFSR.FsrPdfRow.Texto("Excedente de 3 SMGDF", "", "", $"{AU:N5}"),
                    GeneradorPdfFSR.FsrPdfRow.Texto("Prestaciones en dinero (Patron+obrero)", $".7+IIF({FSR_SACAL:N6}>1.000000,0,0.25)", "%", $"{FSR_IMPE_p:N5}"),
                    GeneradorPdfFSR.FsrPdfRow.Texto("Gastos medicos. Pensionados (Patrón-Obrero)", $"1.05+IIF({FSR_SACAL:N6}>1.000000,0,0.375)", "%", $"{FSR_IMGM_p:N5}"),
                    GeneradorPdfFSR.FsrPdfRow.Texto("Invalidez y vida", $"1.75+IIF({FSR_SACAL:N6}>1.000000,0,0.625)", "%", $"{FSR_IMINV_p:N5}"),
                    GeneradorPdfFSR.FsrPdfRow.Texto("Cesantía en edad avanzada y vejez", $"3.15+IIF({FSR_SACAL:N6}>1.000000,0,1.125)", "%", $"{FSR_IMCE_p:N5}"),
                    GeneradorPdfFSR.FsrPdfRow.Texto("Límite de prest. Inv., vida, cesantía y vejez", "", "", $"{AS_lim:N5}"),
                    GeneradorPdfFSR.FsrPdfRow.Numero("Enfermedad y maternidad. Cuota fija especie", "", "", AC),
                    GeneradorPdfFSR.FsrPdfRow.Numero("Enferm.-matern. Exc. a 3 S.M.D.F. especie", "", "", AD),
                    GeneradorPdfFSR.FsrPdfRow.Numero("Enfermedad y maternidad. Prestaciones en dinero", "", "", AE),
                    GeneradorPdfFSR.FsrPdfRow.Numero("Enfermedad y maternidad gastos médicos pensionados", "", "", AF),
                    GeneradorPdfFSR.FsrPdfRow.Numero("Invalidez y vida", "", "", AG),
                    GeneradorPdfFSR.FsrPdfRow.Numero("Guarderías", "", "", AH),
                    GeneradorPdfFSR.FsrPdfRow.Numero("Retiro", "", "", AI),
                    GeneradorPdfFSR.FsrPdfRow.Numero("Cesantía en edad avanzada y vejez", "", "", AJ),
                    GeneradorPdfFSR.FsrPdfRow.Numero("Riesgos de trabajo", "", "", AK),
                    GeneradorPdfFSR.FsrPdfRow.Numero("Cuota patronal del IMSS", "", "", AL),
                    GeneradorPdfFSR.FsrPdfRow.Numero("Factor de cuota patronal del IMSS = IMSS/SND", $"{AL:N6}/{FSR_SACAL:N6}", "factor", FSR_IMIMS),

                    GeneradorPdfFSR.FsrPdfRow.Subseccion("De INFONAVIT y otras cuotas"),
                    GeneradorPdfFSR.FsrPdfRow.Texto("Limite de Aportaciones INFONAVIT", "", "", $"{AZ:N0}"),
                    GeneradorPdfFSR.FsrPdfRow.Numero("INFONAVIT", "", "", AM),
                    GeneradorPdfFSR.FsrPdfRow.Numero("Impuesto sobre Nómina", "", "", AN),
                    GeneradorPdfFSR.FsrPdfRow.Numero("Otros impuestos", "", "", AO),
                    GeneradorPdfFSR.FsrPdfRow.Numero("Obligaciones patronales (IOP)", "", "", AP),
                    GeneradorPdfFSR.FsrPdfRow.Numero("Obligaciones patronales entre SN", $"{AP:N6}/{FSR_SACAL:N6}", "", AQ),

                    GeneradorPdfFSR.FsrPdfRow.Subseccion("Del TP/TL y del FSR"),
                    GeneradorPdfFSR.FsrPdfRow.Numero("FSR = Ps (Tp/Tl) + Tp/Tl", $"{BH:N6}+{FSR_FSI:N6}", "", FSR_FSR),
                };

                Cursor = Cursors.WaitCursor;
                var generador = new GeneradorPdfFSR(svcRep);
                string ruta = generador.Generar(_proyecto, plantilla, filas, FSR_FSR, dlg.FileName, tituloCfg);
                Cursor = Cursors.Default;

                if (MessageBox.Show("Reporte PDF generado.¿Desea abrirlo?", "Reporte generado",
                    MessageBoxButtons.YesNo, MessageBoxIcon.Information) == DialogResult.Yes)
                {
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(ruta) { UseShellExecute = true });
                }
            }
            catch (Exception ex)
            {
                Cursor = Cursors.Default;
                MessageBox.Show($"Error al generar el reporte PDF:{ex.Message}",
                    "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // ──────────────────────────────────────────────────────────────────────
        // BOTÓN — Aplicar FSR al proyecto (guarda en ManoDeObra del proyecto)
        // ──────────────────────────────────────────────────────────────────────
        private async void btnAplicar_Click(object sender, EventArgs e)
        {
            var manoObras = _context.ManoDeObra
                .Where(m => m.ProyectoId == _proyecto.Id)
                .ToList();

            var r = MessageBox.Show(
                $"¿Calcular y aplicar FSR individual a {manoObras.Count} insumo(s) de Mano de Obra?\n\n" +
                $"Cada insumo recibirá su propio FSR calculado con su Salario Base.\n" +
                $"Los parámetros de cálculo (días, porcentajes IMSS, etc.) son los actuales.",
                "Aplicar FSR",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);

            if (r != DialogResult.Yes) return;

            try
            {
                int actualizados = 0;
                foreach (var mo in manoObras)
                {
                    // Cada insumo usa su propio SalarioBase como Salario Nominal
                    decimal sn = mo.SalarioBase;
                    if (sn <= 0) continue; // Si no tiene salario, saltar

                    mo.FactorSalarioReal = CalcularFSR(sn);
                    mo.CalcularSalarioReal();
                    actualizados++;
                }

                await _context.SaveChangesAsync();

                // Guardar el FSR de referencia del formulario (con nudSalarioNominal) en el proyecto
                _proyecto.FactorSalarioReal = FSR_FSR;
                _proyecto.FechaCalculoFSR   = DateTime.Now;
                _proyecto.ParametrosFSR     = GuardarParametros();
                await _context.SaveChangesAsync();

                MessageBox.Show(
                    $"FSR individual aplicado exitosamente.\n" +
                    $"Se actualizaron {actualizados} insumo(s) de Mano de Obra con su propio FSR.",
                    "Aplicado",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al aplicar FSR:\n{ex.Message}",
                    "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        public bool GenerarReporteExcelRibbon()
        {
            if (string.IsNullOrEmpty(_proyecto.ParametrosFSR))
            {
                MessageBox.Show("Primero aplique el FSR al proyecto para guardar los parámetros.",
                    "Sin parámetros", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return false;
            }

            using var dlg = new SaveFileDialog
            {
                Title    = "Guardar reporte",
                Filter   = "Excel (*.xlsx)|*.xlsx",
                FileName = $"CalculoFSR_{DateTime.Now:yyyyMMdd_HHmm}.xlsx"
            };
            if (dlg.ShowDialog(this) != DialogResult.OK) return false;

            try
            {
                _proyecto.ParametrosFSR = GuardarParametros();

                var svcRep    = new Services.ReporteService(_context);
                var plantilla = svcRep.ObtenerOCrearPlantilla(_proyecto.Id);
                var tituloCfg = new ConfiguracionTituloReporteService(_context).ObtenerOCrear(_proyecto.Id, ReportTitleModuleKeys.FSR, lblTitulo.Text);

                using var wb = new ClosedXML.Excel.XLWorkbook();
                Services.GeneradorExcelFSR.GenerarAE2A(wb, _proyecto, plantilla, svcRep, tituloCfg);
                wb.SaveAs(dlg.FileName);

                if (MessageBox.Show("Reporte generado exitosamente.\n¿Desea abrirlo?",
                    "Reporte generado", MessageBoxButtons.YesNo, MessageBoxIcon.Information) == DialogResult.Yes)
                    System.Diagnostics.Process.Start(
                        new System.Diagnostics.ProcessStartInfo(dlg.FileName) { UseShellExecute = true });

                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al generar reporte:\n{ex.Message}",
                    "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }
        }

        private void btnExportarFSR_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(_proyecto.ParametrosFSR))
            {
                MessageBox.Show("Primero aplique el FSR al proyecto para guardar los parámetros.",
                    "Sin parámetros", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            using var dlg = new SaveFileDialog
            {
                Title    = "Guardar reporte",
                Filter   = "Excel (*.xlsx)|*.xlsx",
                FileName = $"CalculoFSR_{DateTime.Now:yyyyMMdd_HHmm}.xlsx"
            };
            if (dlg.ShowDialog() != DialogResult.OK) return;

            try
            {
                // Guardar parámetros actuales antes de exportar
                _proyecto.ParametrosFSR = GuardarParametros();

                var svcRep    = new Services.ReporteService(_context);
                var plantilla = svcRep.ObtenerOCrearPlantilla(_proyecto.Id);
                var tituloCfg = new ConfiguracionTituloReporteService(_context).ObtenerOCrear(_proyecto.Id, ReportTitleModuleKeys.FSR, lblTitulo.Text);

                using var wb = new ClosedXML.Excel.XLWorkbook();
                Services.GeneradorExcelFSR.GenerarAE2A(wb, _proyecto, plantilla, svcRep, tituloCfg);
                wb.SaveAs(dlg.FileName);

                if (MessageBox.Show("Reporte generado exitosamente.\n¿Desea abrirlo?",
                    "Reporte generado", MessageBoxButtons.YesNo, MessageBoxIcon.Information) == DialogResult.Yes)
                    System.Diagnostics.Process.Start(
                        new System.Diagnostics.ProcessStartInfo(dlg.FileName) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al generar reporte:\n{ex.Message}",
                    "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void btnCerrar_Click(object sender, EventArgs e)
        {
            this.Close();
        }

        private void btnRestaurar_Click(object sender, EventArgs e)
        {
            var r = MessageBox.Show(
                "¿Restaurar todos los valores a los datos por defecto?",
                "Restaurar", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (r == DialogResult.Yes)
                CargarValoresPorDefecto();
        }
    }
}
