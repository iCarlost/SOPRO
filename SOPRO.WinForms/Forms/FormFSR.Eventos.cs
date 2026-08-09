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
    /// Suscripción de eventos de los controles del FSR.
    /// </summary>
    public partial class FormFSR
    {

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
    }
}
