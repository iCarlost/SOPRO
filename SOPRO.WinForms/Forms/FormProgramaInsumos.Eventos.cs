using SOPRO.Application.DTOs.Programacion;
using SOPRO.Application.DTOs.Programacion.Insumos;
using SOPRO.Application.Services;
using SOPRO.Application.Services.Programacion;
using SOPRO.Core.Entities;
using SOPRO.Data.Context;
using SOPRO.WinForms.Controls;
using SOPRO.WinForms.Helpers;
using SOPRO.WinForms.Services;
using SOPRO.WinForms.Models;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;

namespace SOPRO.WinForms.Forms
{
    /// <summary>
    /// Eventos del formulario y controles del programa de insumos.
    /// </summary>
    public partial class FormProgramaInsumos
    {

        private void dgvProgramaInsumos_ColumnWidthChanged(object? sender, DataGridViewColumnEventArgs e)
        {
            if (_cargandoColumnas || e?.Column == null || e.Column.Name == "colDummy") return;
            if (e.Column.Tag is not ColumnaProgramaInsumos cfg) return;
            if (e.Column.Width <= 20) return;
            var db = _context.ColumnasProgramaInsumos.Find(cfg.Id);
            if (db == null) return;
            db.AnchoColumna = e.Column.Width;
            db.FechaModificacion = DateTime.Now;
            _context.SaveChanges();
        }

        private void dgvProgramaInsumos_SelectionChanged(object? sender, EventArgs e)
        {
            if (_cargando)
                return;

            SolicitarActualizacionVisualInsumos();
        }

        private void splitPrincipal_SplitterMoved(object? sender, SplitterEventArgs e)
        {
            if (_splitterTrackingSuspended)
                return;

            GuardarLayoutPersistido();
            SolicitarActualizacionVisualInsumos();
        }

        private void btnConfigColumnas_Click(object sender, EventArgs e)
        {
            ColumnasProgramaInsumosHelper.SincronizarDesdeGrid(_context, _proyecto.Id, dgvProgramaInsumos.Columns);
            using var form = new FormColumnasAPU(_context, _proyecto.Id, FormColumnasAPU.ModoColumnas.ProgramaInsumos);
            if (form.ShowDialog(this) == DialogResult.OK || form.CambiosRealizados)
                SolicitarRecargaProgramaInsumos(true);
        }

        private void FormProgramaInsumos_Load(object sender, EventArgs e)
        {
            _cargando = true;
            _layoutPersistenceReady = false;
            _splitterTrackingSuspended = true;

            SuspendLayout();
            splitPrincipal.SuspendLayout();
            dgvProgramaInsumos.SuspendLayout();
            panelGantt.SuspendLayout();

            cboTipo.ComboBox.DataSource = Enum.GetValues(typeof(ProgramaInsumoTipo));
            cboVista.ComboBox.DataSource = Enum.GetValues(typeof(VistaProgramaInsumos));

            var state = FormProgramaInsumosLayoutStateStore.Load(_proyecto.Id);
            cboTipo.ComboBox.SelectedItem = state?.TipoInsumo is int tipoGuardado && Enum.IsDefined(typeof(ProgramaInsumoTipo), tipoGuardado)
                ? (ProgramaInsumoTipo)tipoGuardado
                : ProgramaInsumoTipo.Materiales;
            cboVista.ComboBox.SelectedItem = state?.Vista != null && Enum.TryParse<VistaProgramaInsumos>(state.Vista, out var vistaGuardada)
                ? vistaGuardada
                : VistaProgramaInsumos.Cantidades;

            if (state != null)
            {
                if (state.GanttPanelWidth > 120 && state.GanttPanelWidth < splitPrincipal.Width - 160)
                    splitPrincipal.SplitterDistance = Math.Max(320, splitPrincipal.Width - state.GanttPanelWidth - splitPrincipal.SplitterWidth);
                if (_ganttControl != null)
                {
                    if (state.GanttCellWidth.HasValue)
                        _ganttControl.TimelineCellWidth = state.GanttCellWidth.Value;
                    _ganttControl.VisualSettings = state.ToVisualSettings();
                }
            }

            CargarProgramaInsumos();

            panelGantt.ResumeLayout(true);
            dgvProgramaInsumos.ResumeLayout(true);
            splitPrincipal.ResumeLayout(true);
            ResumeLayout(true);

            _splitterTrackingSuspended = false;
            _cargando = false;
            _layoutPersistenceReady = true;
            SolicitarActualizacionVisualInsumos(true);
        }

        private void cboTipo_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (_cargando) return;
            SolicitarRecargaProgramaInsumos();
        }

        private void btnRecargar_Click(object sender, EventArgs e) => SolicitarRecargaProgramaInsumos(true);

        private void cboVista_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (_cargando) return;
            SolicitarRecargaProgramaInsumos();
        }

        private void btnCerrar_Click(object sender, EventArgs e) => Close();

        private string ObtenerTituloReporteProgramaInsumos()
        {
            var vista = cboVista.ComboBox.SelectedItem is VistaProgramaInsumos v ? v : VistaProgramaInsumos.Cantidades;
            var baseTitle = new ConfiguracionTituloReporteService(_context).ObtenerOCrear(_proyecto.Id, ReportTitleModuleKeys.ProgramaInsumos, lblTitulo.Text).TextoTitulo;
            return vista switch
            {
                VistaProgramaInsumos.Importes => $"{baseTitle} - Erogaciones",
                VistaProgramaInsumos.Mixto => $"{baseTitle} - Mixto",
                _ => $"{baseTitle} - Cantidades"
            };
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            GuardarLayoutPersistido();
            base.OnFormClosing(e);
        }
    }
}
