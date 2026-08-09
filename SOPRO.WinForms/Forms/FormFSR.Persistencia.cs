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
    /// Persistencia y restauración de parámetros del FSR.
    /// </summary>
    public partial class FormFSR
    {

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
    }
}
