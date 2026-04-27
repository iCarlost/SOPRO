using SOPRO.Application.Models.ExternalProjects;
using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace SOPRO.WinForms.Forms
{
    public sealed class FormPreviewImportacionMatrices : Form
    {
        private readonly ExternalMatrixImportPreview _preview;
        private readonly Label _lblResumen;
        private readonly TreeView _tree;
        private readonly ListBox _lstConflictos;
        private readonly ListBox _lstAcciones;
        private readonly ListBox _lstImpacto;
        private readonly Button _btnReemplazar;
        private readonly Button _btnMantener;
        private readonly Button _btnCancelar;
        private readonly TableLayoutPanel _bodyLayout;
        private readonly TableLayoutPanel _rightLayout;

        public ExternalMatrixImportConflictPolicy? SelectedPolicy { get; private set; }

        public FormPreviewImportacionMatrices(ExternalMatrixImportPreview preview)
        {
            _preview = preview ?? throw new ArgumentNullException(nameof(preview));
            Text = "Preview de importación";
            StartPosition = FormStartPosition.CenterParent;
            AutoScaleMode = AutoScaleMode.Font;
            MinimumSize = new Size(900, 560);
            ClientSize = new Size(980, 650);
            BackColor = Color.White;

            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 3,
                Padding = new Padding(12)
            };
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 118F));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            Controls.Add(root);

            _lblResumen = new Label
            {
                Dock = DockStyle.Fill,
                AutoSize = false,
                Font = new Font("Segoe UI", 9F),
                Padding = new Padding(4),
                TextAlign = ContentAlignment.TopLeft,
                AutoEllipsis = false,
                Text = BuildSummary()
            };
            root.Controls.Add(_lblResumen, 0, 0);

            _bodyLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 1,
                Margin = new Padding(0),
                Padding = new Padding(0)
            };
            _bodyLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 56F));
            _bodyLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 44F));
            _bodyLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            root.Controls.Add(_bodyLayout, 0, 1);

            var gbTree = new GroupBox { Text = "Árbol de dependencias", Dock = DockStyle.Fill, Margin = new Padding(0, 0, 6, 0) };
            _tree = new TreeView { Dock = DockStyle.Fill, HideSelection = false, FullRowSelect = true };
            gbTree.Controls.Add(_tree);
            _bodyLayout.Controls.Add(gbTree, 0, 0);

            _rightLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2,
                Margin = new Padding(0),
                Padding = new Padding(0)
            };
            _rightLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 68F));
            _rightLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 32F));
            _bodyLayout.Controls.Add(_rightLayout, 1, 0);

            var tabs = new TabControl { Dock = DockStyle.Fill, Margin = new Padding(0, 0, 0, 6) };
            _rightLayout.Controls.Add(tabs, 0, 0);

            _lstConflictos = new ListBox { Dock = DockStyle.Fill, HorizontalScrollbar = true };
            var pageConf = new TabPage("Conflictos");
            pageConf.Controls.Add(_lstConflictos);
            tabs.TabPages.Add(pageConf);

            _lstAcciones = new ListBox { Dock = DockStyle.Fill, HorizontalScrollbar = true };
            var pageAcc = new TabPage("Plan de importación");
            pageAcc.Controls.Add(_lstAcciones);
            tabs.TabPages.Add(pageAcc);

            _lstImpacto = new ListBox { Dock = DockStyle.Fill, HorizontalScrollbar = true };
            var gbImpacto = new GroupBox { Text = "Impacto de reemplazo", Dock = DockStyle.Fill, Margin = new Padding(0) };
            gbImpacto.Controls.Add(_lstImpacto);
            _rightLayout.Controls.Add(gbImpacto, 0, 1);

            var buttonPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.RightToLeft,
                AutoSize = true,
                Padding = new Padding(0, 8, 0, 0)
            };
            root.Controls.Add(buttonPanel, 0, 2);

            _btnCancelar = new Button { Text = "Cancelar", AutoSize = true, DialogResult = DialogResult.Cancel };
            _btnCancelar.Click += (_, __) => { SelectedPolicy = null; Close(); };
            buttonPanel.Controls.Add(_btnCancelar);

            _btnMantener = new Button { Text = _preview.TotalConflicts == 0 ? "Importar" : "Mantener ambas (TEMP)", AutoSize = true, BackColor = Color.FromArgb(33, 150, 243), ForeColor = Color.White, FlatStyle = FlatStyle.Flat };
            _btnMantener.Click += (_, __) => { SelectedPolicy = ExternalMatrixImportConflictPolicy.KeepBothWithTempKey; DialogResult = DialogResult.OK; Close(); };
            buttonPanel.Controls.Add(_btnMantener);

            _btnReemplazar = new Button { Text = "Reemplazar existentes", AutoSize = true, BackColor = Color.FromArgb(255, 152, 0), ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Visible = _preview.TotalConflicts > 0 };
            _btnReemplazar.Click += (_, __) => { SelectedPolicy = ExternalMatrixImportConflictPolicy.ReplaceExisting; DialogResult = DialogResult.OK; Close(); };
            buttonPanel.Controls.Add(_btnReemplazar);

            AcceptButton = _btnMantener;
            CancelButton = _btnCancelar;

            Load += (_, __) => Populate();
            Shown += (_, __) => BeginInvoke(new Action(() =>
            {
                PerformLayout();
                _bodyLayout.PerformLayout();
                _rightLayout.PerformLayout();
                Invalidate(true);
            }));
        }

        private string BuildSummary()
        {
            return
                $"Matriz: {_preview.MatrixKey} - {_preview.MatrixDescription}\n" +
                $"Proyecto origen: {_preview.SourceProjectName}\n\n" +
                $"Matrices: {_preview.TotalMatrices}  (Básicos: {_preview.TotalBasicos}, Cuadrillas: {_preview.TotalCuadrillas})\n" +
                $"Materiales: {_preview.TotalMateriales}   Mano de obra: {_preview.TotalManoDeObra}\n" +
                $"Maquinaria: {_preview.TotalMaquinaria}   Herramientas: {_preview.TotalHerramientas}\n" +
                $"Profundidad máxima del árbol: {_preview.MaxDepth}\n" +
                $"Conflictos totales: {_preview.TotalConflicts}";
        }

        private void Populate()
        {
            _tree.BeginUpdate();
            _tree.Nodes.Clear();
            var stack = new System.Collections.Generic.Stack<(int indent, TreeNode node)>();

            foreach (var rawLine in _preview.DependencyTreeLines)
            {
                var line = rawLine ?? string.Empty;
                var trimmed = line.TrimStart();
                var indent = line.Length - trimmed.Length;
                var node = new TreeNode(trimmed);

                while (stack.Count > 0 && stack.Peek().indent >= indent)
                    stack.Pop();

                if (stack.Count == 0)
                    _tree.Nodes.Add(node);
                else
                    stack.Peek().node.Nodes.Add(node);

                stack.Push((indent, node));
            }
            _tree.ExpandAll();
            _tree.EndUpdate();

            _lstConflictos.Items.Clear();
            if (_preview.ConflictKeyLines.Count == 0)
                _lstConflictos.Items.Add("Sin conflictos de clave.");
            else
                _lstConflictos.Items.AddRange(_preview.ConflictKeyLines.Cast<object>().ToArray());

            _lstAcciones.Items.Clear();
            if (_preview.ActionSummaryLines.Count == 0)
                _lstAcciones.Items.Add("Sin acciones registradas.");
            else
                _lstAcciones.Items.AddRange(_preview.ActionSummaryLines.Cast<object>().ToArray());

            _lstImpacto.Items.Clear();
            if (_preview.ImpactSummaryLines.Count == 0)
                _lstImpacto.Items.Add("Sin impacto de reemplazo detectado.");
            else
                _lstImpacto.Items.AddRange(_preview.ImpactSummaryLines.Cast<object>().ToArray());
        }
    }
}
