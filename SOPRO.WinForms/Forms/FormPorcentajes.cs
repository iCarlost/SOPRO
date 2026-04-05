using Microsoft.EntityFrameworkCore;
using SOPRO.Application.Models.Presupuesto;
using SOPRO.Application.Services;
using SOPRO.Core.Entities;
using SOPRO.Data.Context;
using SOPRO.WinForms.Helpers;
using System;
using System.Linq;
using System.Windows.Forms;

namespace SOPRO.WinForms.Forms
{
    public partial class FormPorcentajes : Form
    {
        private readonly SOPROContext _context;
        private readonly Proyecto _proyecto;
        private Button? _btnFinanciamiento;

        public static event EventHandler? PorcentajesActualizados;

        public FormPorcentajes(SOPROContext context, Proyecto proyecto)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _proyecto = proyecto ?? throw new ArgumentNullException(nameof(proyecto));
            FormatoHelper.EstablecerProyecto(_proyecto);
            InitializeComponent();
            //CrearBotonFinanciamiento();
        }

        private void FormPorcentajes_Load(object sender, EventArgs e)
        {
            RecargarDesdeProyecto();

            var cdActual = new SOPRO.Application.Services.MotorCalculoSopro(_proyecto).SumarCostoDirecto(
                _context.ConceptosPresupuesto
                    .Where(c => c.ProyectoId == _proyecto.Id)
                    .AsNoTracking()
                    .AsEnumerable());

            nudCDRef.Value = cdActual > 0 ? Math.Min(cdActual, nudCDRef.Maximum) : 0;
            ActualizarPreview();

            FormIndirectos.IndirectosTransferidos += FormIndirectos_IndirectosTransferidos;
            FormFinanciamiento.FinanciamientoTransferido += FormFinanciamiento_FinanciamientoTransferido;
            FormUtilidad.UtilidadTransferida += FormUtilidad_UtilidadTransferida;
        }

        private void FormIndirectos_IndirectosTransferidos(object? sender, EventArgs e) => RecargarDesdeProyecto();
        private void FormFinanciamiento_FinanciamientoTransferido(object? sender, EventArgs e) => RecargarDesdeProyecto();
        private void FormUtilidad_UtilidadTransferida(object? sender, EventArgs e) => RecargarDesdeProyecto();

        private void RecargarDesdeProyecto()
        {
            var proy = _context.Proyectos.Find(_proyecto.Id) ?? _proyecto;
            _proyecto.PorcentajeIndirectosCentral = proy.PorcentajeIndirectosCentral;
            _proyecto.PorcentajeIndirectosCampo = proy.PorcentajeIndirectosCampo;
            _proyecto.PorcentajeFinanciamiento = proy.PorcentajeFinanciamiento;
            _proyecto.PorcentajeUtilidad = proy.PorcentajeUtilidad;
            _proyecto.PorcentajeCargosAdicionales = proy.PorcentajeCargosAdicionales;
            _proyecto.ModoCalculoPorcentajes = proy.ModoCalculoPorcentajes;

            nudOC.Value = Math.Min(nudOC.Maximum, proy.PorcentajeIndirectosCentral);
            nudCampo.Value = Math.Min(nudCampo.Maximum, proy.PorcentajeIndirectosCampo);
            nudFin.Value = Math.Min(nudFin.Maximum, proy.PorcentajeFinanciamiento);
            nudUtil.Value = Math.Min(nudUtil.Maximum, proy.PorcentajeUtilidad);
            nudCargos.Value = Math.Min(nudCargos.Maximum, proy.PorcentajeCargosAdicionales);

            chkOC.Checked = proy.PorcentajeIndirectosCentral > 0;
            chkCampo.Checked = proy.PorcentajeIndirectosCampo > 0;
            chkFin.Checked = proy.PorcentajeFinanciamiento > 0;
            chkUtil.Checked = proy.PorcentajeUtilidad > 0;
            chkCargos.Checked = proy.PorcentajeCargosAdicionales > 0;
            rbSobreCD.Checked = proy.ModoCalculoPorcentajes == "SobreCD";
            rbAcumulables.Checked = !rbSobreCD.Checked;
            ActualizarPreview();
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            FormIndirectos.IndirectosTransferidos -= FormIndirectos_IndirectosTransferidos;
            FormFinanciamiento.FinanciamientoTransferido -= FormFinanciamiento_FinanciamientoTransferido;
            FormUtilidad.UtilidadTransferida -= FormUtilidad_UtilidadTransferida;
            base.OnFormClosed(e);
        }

        private void ActualizarPreview()
        {
            var input = new BudgetPercentageInput
            {
                CostoDirectoReferencia = nudCDRef.Value,
                IndirectosCentral = chkOC.Checked ? nudOC.Value : 0m,
                IndirectosCampo = chkCampo.Checked ? nudCampo.Value : 0m,
                Financiamiento = chkFin.Checked ? nudFin.Value : 0m,
                Utilidad = chkUtil.Checked ? nudUtil.Value : 0m,
                CargosAdicionales = chkCargos.Checked ? nudCargos.Value : 0m,
                ModoCalculoPorcentajes = rbSobreCD.Checked ? "SobreCD" : "Acumulables"
            };

            var preview = BudgetPreviewCalculationService.BuildPreview(_context, _proyecto, input);
            lblCDValor.Text = preview.CostoDirecto.ToStringImporte();
            lblOCMonto.Text = preview.MontoIndirectosCentral.ToStringImporte();
            lblCampoMonto.Text = preview.MontoIndirectosCampo.ToStringImporte();
            lblSub1Valor.Text = preview.Subtotal1.ToStringImporte();
            lblFinMonto.Text = preview.MontoFinanciamiento.ToStringImporte();
            lblSub2Valor.Text = preview.Subtotal2.ToStringImporte();
            lblUtilMonto.Text = preview.MontoUtilidad.ToStringImporte();
            lblSub3Valor.Text = preview.Subtotal3.ToStringImporte();
            lblCargosMonto.Text = preview.MontoCargosAdicionales.ToStringImporte();
            lblPUValor.Text = preview.PrecioUnitarioFinal.ToStringImporte();
        }

        private void BtnAplicar_Click(object sender, EventArgs e)
        {
            try
            {
                var proy = _context.Proyectos.Find(_proyecto.Id);
                if (proy == null) return;

                proy.PorcentajeIndirectosCentral = chkOC.Checked ? nudOC.Value : 0m;
                proy.PorcentajeIndirectosCampo = chkCampo.Checked ? nudCampo.Value : 0m;
                proy.PorcentajeFinanciamiento = chkFin.Checked ? nudFin.Value : 0m;
                proy.PorcentajeUtilidad = chkUtil.Checked ? nudUtil.Value : 0m;
                proy.PorcentajeCargosAdicionales = chkCargos.Checked ? nudCargos.Value : 0m;
                proy.ModoCalculoPorcentajes = rbAcumulables.Checked ? "Acumulables" : "SobreCD";
                _context.SaveChanges();

                _proyecto.PorcentajeIndirectosCentral = proy.PorcentajeIndirectosCentral;
                _proyecto.PorcentajeIndirectosCampo = proy.PorcentajeIndirectosCampo;
                _proyecto.PorcentajeFinanciamiento = proy.PorcentajeFinanciamiento;
                _proyecto.PorcentajeUtilidad = proy.PorcentajeUtilidad;
                _proyecto.PorcentajeCargosAdicionales = proy.PorcentajeCargosAdicionales;
                _proyecto.ModoCalculoPorcentajes = proy.ModoCalculoPorcentajes;

                PorcentajesActualizados?.Invoke(this, EventArgs.Empty);
                DialogResult = DialogResult.OK;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al guardar: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void BtnDesdeIndirectos_Click(object sender, EventArgs e)
        {
            var config = _context.ConfiguracionesIndirectos.FirstOrDefault(c => c.ProyectoId == _proyecto.Id);
            if (config == null || (config.PorcentajeOficinaCentral == 0 && config.PorcentajeCampo == 0))
            {
                MessageBox.Show("No hay porcentajes calculados en el módulo de Indirectos.", "Sin datos", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            nudOC.Value = Math.Min(config.PorcentajeOficinaCentral, nudOC.Maximum);
            nudCampo.Value = Math.Min(config.PorcentajeCampo, nudCampo.Maximum);
            chkOC.Checked = config.PorcentajeOficinaCentral > 0;
            chkCampo.Checked = config.PorcentajeCampo > 0;
            ActualizarPreview();
        }

        private void chkOC_CheckedChanged(object sender, EventArgs e) { nudOC.Enabled = chkOC.Checked; ActualizarPreview(); }
        private void chkCampo_CheckedChanged(object sender, EventArgs e) { nudCampo.Enabled = chkCampo.Checked; ActualizarPreview(); }
        private void chkFin_CheckedChanged(object sender, EventArgs e) { nudFin.Enabled = chkFin.Checked; ActualizarPreview(); }
        private void chkUtil_CheckedChanged(object sender, EventArgs e) { nudUtil.Enabled = chkUtil.Checked; ActualizarPreview(); }
        private void chkCargos_CheckedChanged(object sender, EventArgs e) { nudCargos.Enabled = chkCargos.Checked; ActualizarPreview(); }
        private void nudCDRef_ValueChanged(object sender, EventArgs e) => ActualizarPreview();
        private void nudOC_ValueChanged(object sender, EventArgs e) => ActualizarPreview();
        private void nudCampo_ValueChanged(object sender, EventArgs e) => ActualizarPreview();
        private void nudFin_ValueChanged(object sender, EventArgs e) => ActualizarPreview();
        private void nudUtil_ValueChanged(object sender, EventArgs e) => ActualizarPreview();
        private void nudCargos_ValueChanged(object sender, EventArgs e) => ActualizarPreview();
        private void rbSobreCD_CheckedChanged(object sender, EventArgs e) { if (rbSobreCD.Checked) ActualizarPreview(); }
        private void rbAcumulables_CheckedChanged(object sender, EventArgs e) { if (rbAcumulables.Checked) ActualizarPreview(); }

        private void CrearBotonFinanciamiento()
        {
            _btnFinanciamiento = new Button
            {
                Name = "btnCalcularFinanciamiento",
                Text = "💸 Calcular Financ.",
                Width = 180,
                Height = 45,
                Left = 210,
                Top = 575,
                FlatStyle = FlatStyle.Flat,
                BackColor = System.Drawing.Color.FromArgb(255, 152, 0),
                ForeColor = System.Drawing.Color.White,
                Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Bold),
                Anchor = AnchorStyles.Left | AnchorStyles.Bottom
            };
            _btnFinanciamiento.Click += BtnCalcularFinanciamiento_Click;
            Controls.Add(_btnFinanciamiento);
            _btnFinanciamiento.BringToFront();
        }

        private void BtnCalcularFinanciamiento_Click(object? sender, EventArgs e)
        {
            using var form = new FormFinanciamiento(_context, _proyecto);
            form.ShowDialog(this);
            RecargarDesdeProyecto();
        }

        private void btnCancelar_Click(object sender, EventArgs e)
        {
            DialogResult = DialogResult.Cancel;
            Close();
        }
    }
}
