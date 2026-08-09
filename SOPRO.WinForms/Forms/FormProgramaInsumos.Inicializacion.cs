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
    /// Inicialización del formulario, grid y gantt del programa de insumos.
    /// </summary>
    public partial class FormProgramaInsumos
    {

        private void OnDecimalesActualizados(object sender, EventArgs e)
        {
            if (IsDisposed) return;
            // Actualizar propiedades de decimales sin reasignar el campo readonly
            var proyFresco = _context.Proyectos.Find(_proyecto.Id);
            if (proyFresco != null)
            {
                _proyecto.DecimalesCantidad   = proyFresco.DecimalesCantidad;
                _proyecto.DecimalesImporte    = proyFresco.DecimalesImporte;
                _proyecto.DecimalesPorcentaje = proyFresco.DecimalesPorcentaje;
            }
            if (IsHandleCreated)
                BeginInvoke(new Action(() => { if (!IsDisposed) SolicitarRecargaProgramaInsumos(true); }));
        }

        private void ConfigurarFormulario()
        {
            FormRenderHelper.OptimizarRender(this);
            ControlRenderHelper.HabilitarDobleBuffer(panelTop);
            ControlRenderHelper.HabilitarDobleBuffer(toolStripTop);
            lblTitulo.Text = "PROGRAMA DE INSUMOS";
            new EditableReportTitleHelper(_context, panelTop, lblTitulo, () => _proyecto.Id, ReportTitleModuleKeys.ProgramaInsumos).Attach();
            lblSubtitulo.Text = $"Proyecto: {_proyecto.Nombre}";
            lblResumen.Text = "Consumos programados por período.";
            lblEstado.Text = "Módulo de insumos listo.";
            this.MinimumSize = new Size(1200, 700);
        }

        private void ConfigurarGrid()
        {
            dgvProgramaInsumos.AplicarEstiloSOPRO();
            dgvProgramaInsumos.MultiSelect = true;
            dgvProgramaInsumos.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None;
            dgvProgramaInsumos.AllowUserToAddRows = false;
            dgvProgramaInsumos.AllowUserToDeleteRows = false;
            dgvProgramaInsumos.AllowUserToResizeRows = false;
            dgvProgramaInsumos.ReadOnly = true;
            dgvProgramaInsumos.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            dgvProgramaInsumos.AllowUserToOrderColumns = false;
            dgvProgramaInsumos.ColumnHeadersHeight = 36;
            dgvProgramaInsumos.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
            dgvProgramaInsumos.RowHeadersVisible = false;
            DgvCeldaHelper.Aplicar(dgvProgramaInsumos);
            dgvProgramaInsumos.ColumnHeaderMouseClick += (s, e) => NotificarColumnaSeleccionada(e.ColumnIndex);
            dgvProgramaInsumos.ColumnWidthChanged += dgvProgramaInsumos_ColumnWidthChanged;
            dgvProgramaInsumos.SelectionChanged += dgvProgramaInsumos_SelectionChanged;
            dgvProgramaInsumos.VisibleChanged += (_, __) => SolicitarActualizacionVisualInsumos();
            ControlRenderHelper.HabilitarDobleBuffer(dgvProgramaInsumos);
        }

        private void ConfigurarGantt()
        {
            _ganttControl = new GanttTimelineControl
            {
                Dock = DockStyle.Fill,
                FooterDisplayMode = GanttFooterDisplayMode.Ninguno,
                RenderModel = new GanttRenderModel()
            };
            _ganttControl.BindGrid(dgvProgramaInsumos);
            _ganttControl.TimelineCellWidthChanged += (_, __) => GuardarLayoutPersistido();
            _ganttControl.VisualSettingsChanged += (_, __) => GuardarLayoutPersistido();
            panelGantt.Controls.Add(_ganttControl);
            splitPrincipal.SplitterMoved += splitPrincipal_SplitterMoved;
        }
    }
}
