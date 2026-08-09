using SOPRO.Application.DTOs.Catalog;
using SOPRO.Application.DTOs.Matrices;
using SOPRO.Application.Models;
using SOPRO.Application.Models.ExternalProjects;
using SOPRO.Application.Services;
using SOPRO.Core.Entities;
using SOPRO.Data.Context;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using SOPRO.WinForms.Helpers;
using System.Reflection;

namespace SOPRO.WinForms.Forms
{
    /// <summary>
    /// Host embebido (workspace de presupuesto) y layout del selector.
    /// </summary>
    public partial class FormSeleccionarAPU
    {

        public void ConfigureForEmbeddedHost()
        {
            EmbeddedMode = true;
            TopLevel = false;
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            ControlBox = false;
            MinimizeBox = false;
            MaximizeBox = false;
            Dock = DockStyle.Fill;
            Resize -= FormSeleccionarAPU_EmbeddedResize;
            Resize += FormSeleccionarAPU_EmbeddedResize;
            ApplyEmbeddedHostLayout();
        }

        private void FormSeleccionarAPU_EmbeddedResize(object? sender, EventArgs e) => ApplyEmbeddedHostLayout();

        public void ConfigureForWorkspaceContentHost()
        {
            ConfigureForEmbeddedHost();
            WorkspaceChromeHidden = true;
            ApplyEmbeddedHostLayout();
        }

        public void TriggerAcceptSelection() => btnAceptar_Click(this, EventArgs.Empty);
        public void TriggerCancelSelection() => btnCancelar_Click(this, EventArgs.Empty);
        public void TriggerNuevaMatriz() => btnNuevaMatriz_Click(this, EventArgs.Empty);
        public void TriggerEditarMatriz() => btnEditarMatriz_Click(this, EventArgs.Empty);
        public TipoMatriz WorkspaceSelectedTipo => GetSelectedTipoFiltro() ?? TipoMatriz.APU;
        public int? WorkspaceSelectedMatrixId => dgvMatrices.SelectedRows.Count == 0 ? null : Convert.ToInt32(dgvMatrices.SelectedRows[0].Cells["colId"].Value);

        private void ApplyEmbeddedHostLayout()
        {
            if (!EmbeddedMode || !IsHandleCreated) return;

            BackColor = Color.White;
            panelTop.Visible = false;
            statusStrip.Visible = false;
            btnCancelar.Text = "Volver";

            lblBuscar.Location = new Point(12, 14);
            lblBuscar.Font = new Font("Segoe UI", 10F, FontStyle.Bold);

            int rightWidth = _includeAuxiliaries ? 360 : 250;
            txtBuscar.Location = new Point(95, 11);
            txtBuscar.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            txtBuscar.Width = Math.Max(240, ClientSize.Width - rightWidth - 110);

            if (_includeAuxiliaries)
            {
                _cboTipo.Visible = true;
                _cboTipo.Anchor = AnchorStyles.Top | AnchorStyles.Right;
                _cboTipo.Location = new Point(Math.Max(360, ClientSize.Width - 340), 11);
                _cboTipo.Width = 92;
                _cboProyecto.Anchor = AnchorStyles.Top | AnchorStyles.Right;
                _cboProyecto.Location = new Point(Math.Max(460, ClientSize.Width - 238), 11);
                _cboProyecto.Width = 226;
            }
            else
            {
                _cboProyecto.Anchor = AnchorStyles.Top | AnchorStyles.Right;
                _cboProyecto.Location = new Point(Math.Max(430, ClientSize.Width - 238), 11);
                _cboProyecto.Width = 226;
            }

            dgvMatrices.Location = new Point(12, 46);
            dgvMatrices.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            dgvMatrices.Size = new Size(Math.Max(400, ClientSize.Width - 24), Math.Max(180, ClientSize.Height - 178));
            dgvMatrices.BorderStyle = BorderStyle.None;
            dgvMatrices.BackgroundColor = Color.White;
            dgvMatrices.EnableHeadersVisualStyles = false;
            dgvMatrices.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(55, 55, 85);
            dgvMatrices.ColumnHeadersDefaultCellStyle.ForeColor = Color.White;
            dgvMatrices.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 8.5F, FontStyle.Bold);
            dgvMatrices.DefaultCellStyle.Font = new Font("Segoe UI", 8.5F, FontStyle.Regular);
            dgvMatrices.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(248, 248, 255);
            dgvMatrices.GridColor = Color.FromArgb(224, 224, 224);

            panelInfo.Location = new Point(12, ClientSize.Height - 124);
            panelInfo.Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom;
            panelInfo.Size = new Size(Math.Max(400, ClientSize.Width - 24), 72);
            panelInfo.BackColor = Color.FromArgb(250, 250, 250);
            panelInfo.BorderStyle = BorderStyle.None;

            btnNuevaMatriz.Anchor = AnchorStyles.Left | AnchorStyles.Bottom;
            btnNuevaMatriz.Location = new Point(12, ClientSize.Height - 46);
            btnNuevaMatriz.Size = new Size(150, 34);
            btnEditarMatriz.Anchor = AnchorStyles.Left | AnchorStyles.Bottom;
            btnEditarMatriz.Location = new Point(170, ClientSize.Height - 46);
            btnEditarMatriz.Size = new Size(150, 34);
            btnCancelar.Anchor = AnchorStyles.Right | AnchorStyles.Bottom;
            btnCancelar.Location = new Point(ClientSize.Width - 230, ClientSize.Height - 46);
            btnCancelar.Size = new Size(100, 34);
            btnAceptar.Anchor = AnchorStyles.Right | AnchorStyles.Bottom;
            btnAceptar.Location = new Point(ClientSize.Width - 120, ClientSize.Height - 46);
            btnAceptar.Size = new Size(108, 34);

            if (WorkspaceChromeHidden)
            {
                btnNuevaMatriz.Visible = false;
                btnEditarMatriz.Visible = false;
                btnCancelar.Visible = false;
                btnAceptar.Visible = false;
                dgvMatrices.Size = new Size(Math.Max(400, ClientSize.Width - 24), Math.Max(180, ClientSize.Height - 136));
                panelInfo.Location = new Point(12, ClientSize.Height - 86);
                panelInfo.Size = new Size(Math.Max(400, ClientSize.Width - 24), 62);
            }
            else
            {
                btnNuevaMatriz.Visible = true;
                btnEditarMatriz.Visible = true;
                btnCancelar.Visible = true;
                btnAceptar.Visible = true;
            }

            lblTitulo.Text = "Seleccionar Matriz";
        }
    }
}
