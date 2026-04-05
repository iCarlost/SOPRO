using SOPRO.Application.Models.ExternalProjects;
using System;
using System.Drawing;
using System.Windows.Forms;

namespace SOPRO.WinForms.Forms
{
    public sealed class FormPreviewImportacionInsumos : Form
    {
        private readonly ExternalInsumoImportPreview _preview;
        public ExternalMatrixImportConflictPolicy? SelectedPolicy { get; private set; }

        public FormPreviewImportacionInsumos(ExternalInsumoImportPreview preview)
        {
            _preview = preview ?? throw new ArgumentNullException(nameof(preview));
            Text = "Preview de importación de insumos";
            StartPosition = FormStartPosition.CenterParent;
            Size = new Size(900, 560);
            MinimumSize = new Size(760, 460);
            BackColor = Color.White;

            var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, Padding = new Padding(12) };
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            Controls.Add(root);

            var lbl = new Label { Dock = DockStyle.Fill, AutoSize = true, Padding = new Padding(4), Font = new Font("Segoe UI", 9F), Text = BuildSummary() };
            root.Controls.Add(lbl, 0, 0);

            var split = new SplitContainer { Dock = DockStyle.Fill, Orientation = Orientation.Vertical };
            root.Controls.Add(split, 0, 1);

            var lstItems = new ListBox { Dock = DockStyle.Fill, HorizontalScrollbar = true };
            lstItems.Items.AddRange(_preview.Items.ToArray());
            var gbItems = new GroupBox { Text = "Elementos seleccionados", Dock = DockStyle.Fill };
            gbItems.Controls.Add(lstItems);
            split.Panel1.Controls.Add(gbItems);

            var lstConf = new ListBox { Dock = DockStyle.Fill, HorizontalScrollbar = true };
            if (_preview.ConflictLines.Count == 0) lstConf.Items.Add("Sin conflictos detectados.");
            else lstConf.Items.AddRange(_preview.ConflictLines.ToArray());
            var gbConf = new GroupBox { Text = "Conflictos detectados", Dock = DockStyle.Fill };
            gbConf.Controls.Add(lstConf);
            split.Panel2.Controls.Add(gbConf);

            var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft, AutoSize = true, Padding = new Padding(0, 8, 0, 0) };
            root.Controls.Add(buttons, 0, 2);

            var btnCancelar = new Button { Text = "Cancelar", AutoSize = true, DialogResult = DialogResult.Cancel };
            btnCancelar.Click += (_, __) => { SelectedPolicy = null; Close(); };
            buttons.Controls.Add(btnCancelar);

            var btnMantener = new Button { Text = _preview.TotalConflicts == 0 ? "Importar" : "Mantener ambas (TEMP)", AutoSize = true, BackColor = Color.FromArgb(33, 150, 243), ForeColor = Color.White, FlatStyle = FlatStyle.Flat };
            btnMantener.Click += (_, __) => { SelectedPolicy = ExternalMatrixImportConflictPolicy.KeepBothWithTempKey; DialogResult = DialogResult.OK; Close(); };
            buttons.Controls.Add(btnMantener);

            var btnReemplazar = new Button { Text = "Reemplazar existentes", Visible = _preview.TotalConflicts > 0, AutoSize = true, BackColor = Color.FromArgb(255, 152, 0), ForeColor = Color.White, FlatStyle = FlatStyle.Flat };
            btnReemplazar.Click += (_, __) => { SelectedPolicy = ExternalMatrixImportConflictPolicy.ReplaceExisting; DialogResult = DialogResult.OK; Close(); };
            buttons.Controls.Add(btnReemplazar);

            Load += (_, __) =>
            {
                split.Panel1MinSize = 420;
                split.Panel2MinSize = 220;
                var maxDistance = Math.Max(split.Panel1MinSize, split.Width - split.Panel2MinSize - split.SplitterWidth);
                split.SplitterDistance = Math.Min(560, maxDistance);
            };
        }

        private string BuildSummary()
        {
            return $"Proyecto origen: {_preview.SourceProjectName}\n" +
                   $"Elementos seleccionados: {_preview.SelectedItems}\n" +
                   $"Matrices auxiliares dependientes: {_preview.MatrixDependencies}\n" +
                   $"Conflictos totales: {_preview.TotalConflicts}";
        }
    }
}
