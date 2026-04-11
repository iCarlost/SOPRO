using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using SOPRO.Application.Services;

namespace SOPRO.WinForms.Forms
{
    public sealed class FormConsolidarInsumos : Form
    {
        private readonly List<ConsolidacionInsumoItem> _items;
        private readonly Func<int, ConsolidacionImpactoPreview> _previewFactory;
        private readonly Label _lblResumen;
        private readonly ListBox _lstSeleccionados;
        private readonly ComboBox _cboBase;
        private readonly Label _lblImpacto;
        private readonly Button _btnAceptar;

        public int InsumoBaseId => (_cboBase.SelectedItem as ConsolidacionInsumoItem)?.Id ?? 0;

        public FormConsolidarInsumos(string tipoNombre, IReadOnlyList<ConsolidacionInsumoItem> items, Func<int, ConsolidacionImpactoPreview> previewFactory)
        {
            _items = items?.ToList() ?? new List<ConsolidacionInsumoItem>();
            _previewFactory = previewFactory ?? throw new ArgumentNullException(nameof(previewFactory));

            Text = $"Consolidar {tipoNombre}";
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            ClientSize = new Size(760, 470);
            Font = new Font("Segoe UI", 9F);
            BackColor = Color.White;

            var lblTitulo = new Label
            {
                AutoSize = true,
                Font = new Font("Segoe UI", 11F, FontStyle.Bold),
                Text = $"Consolidar {tipoNombre}",
                Location = new Point(18, 16)
            };
            Controls.Add(lblTitulo);

            _lblResumen = new Label
            {
                AutoSize = true,
                ForeColor = Color.DimGray,
                Location = new Point(19, 48),
                Text = ""
            };
            Controls.Add(_lblResumen);

            var lblLista = new Label
            {
                AutoSize = true,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                Text = "Elementos seleccionados",
                Location = new Point(18, 83)
            };
            Controls.Add(lblLista);

            _lstSeleccionados = new ListBox
            {
                Location = new Point(22, 108),
                Size = new Size(712, 190),
                HorizontalScrollbar = true
            };
            _lstSeleccionados.DataSource = null;
            _lstSeleccionados.DisplayMember = nameof(ConsolidacionInsumoItem.TextoLista);
            _lstSeleccionados.DataSource = _items;
            Controls.Add(_lstSeleccionados);

            var lblBase = new Label
            {
                AutoSize = true,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                Text = "Elemento que gobernará la consolidación",
                Location = new Point(18, 317)
            };
            Controls.Add(lblBase);

            _cboBase = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Location = new Point(22, 342),
                Size = new Size(712, 28),
                FormattingEnabled = true
            };
            _cboBase.DisplayMember = nameof(ConsolidacionInsumoItem.TextoLista);
            foreach (var item in _items)
                _cboBase.Items.Add(item);
            _cboBase.SelectedIndexChanged += (_, __) => ActualizarImpacto();
            Controls.Add(_cboBase);

            _lblImpacto = new Label
            {
                AutoSize = false,
                BorderStyle = BorderStyle.FixedSingle,
                Location = new Point(22, 384),
                Size = new Size(712, 46),
                Padding = new Padding(8, 8, 8, 8)
            };
            Controls.Add(_lblImpacto);

            _btnAceptar = new Button
            {
                Text = "Consolidar",
                DialogResult = DialogResult.OK,
                Location = new Point(544, 438),
                Size = new Size(92, 28)
            };
            _btnAceptar.Click += (_, __) =>
            {
                if (InsumoBaseId <= 0)
                {
                    DialogResult = DialogResult.None;
                    MessageBox.Show("Seleccione el elemento que gobernará la consolidación.", "Consolidar", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            };
            Controls.Add(_btnAceptar);

            var btnCancelar = new Button
            {
                Text = "Cancelar",
                DialogResult = DialogResult.Cancel,
                Location = new Point(642, 438),
                Size = new Size(92, 28)
            };
            Controls.Add(btnCancelar);

            AcceptButton = _btnAceptar;
            CancelButton = btnCancelar;

            ActualizarResumen();
            if (_cboBase.Items.Count > 0)
                _cboBase.SelectedIndex = 0;
        }

        private void ActualizarResumen()
        {
            _lblResumen.Text = $"Se consolidarán {_items.Count} registros seleccionados. Elija cuál quedará como base. Los demás serán reemplazados en sus referencias y eliminados automáticamente.";
        }

        private void ActualizarImpacto()
        {
            var baseId = InsumoBaseId;
            if (baseId <= 0)
            {
                _lblImpacto.Text = string.Empty;
                return;
            }

            var preview = _previewFactory(baseId);
            _lblImpacto.Text =
                $"A consolidar: {preview.RegistrosAConsolidar}  |  Componentes afectados: {preview.ComponentesAfectados}  |  Matrices afectadas: {preview.MatricesAfectadas}  |  Conceptos impactados: {preview.ConceptosAfectados}";
        }
    }
}
