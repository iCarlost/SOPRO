using System;
using System.Drawing;
using System.Windows.Forms;

namespace SOPRO.WinForms.Controls
{
    public partial class PanelMatricesEmbebido
    {
        private void InitializeComponent()
        {
            this.Dock = DockStyle.Fill; this.BackColor = Color.White; this.AutoScaleMode = AutoScaleMode.Font;

            _panelHeader = new Panel { Dock = DockStyle.Top, Height = 50, BackColor = Color.FromArgb(40, 40, 65) };
            _lblTituloMatriz = new Label { Location = new Point(10, 5),  Size = new Size(770, 20), Font = new Font("Segoe UI", 10F, FontStyle.Bold), ForeColor = Color.White };
            _lblInfoMatriz   = new Label { Location = new Point(10, 28), Size = new Size(770, 16), Font = new Font("Segoe UI", 8.5F), ForeColor = Color.Silver };

            _btnAnterior  = new Button { Text = "▲", Size = new Size(30, 20), Location = new Point(888, 6),  FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(60,60,90), ForeColor = Color.White, Enabled = false };
            _btnSiguiente = new Button { Text = "▼", Size = new Size(30, 20), Location = new Point(920, 6),  FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(60,60,90), ForeColor = Color.White, Enabled = false };
            _btnAnterior.FlatAppearance.BorderColor = _btnSiguiente.FlatAppearance.BorderColor = Color.FromArgb(90,90,130);
            _btnAnterior.Click  += BtnAnterior_Click;
            _btnSiguiente.Click += BtnSiguiente_Click;

            _panelHeader.Resize += (s, e) => LayoutTopHeader();
            _panelHeader.Controls.AddRange(new Control[] { _lblTituloMatriz, _lblInfoMatriz, _btnAnterior, _btnSiguiente });

            _panelDatos = new Panel { Dock = DockStyle.Top, Height = 38, BackColor = Color.FromArgb(250,250,252), Visible = false };
            var lblClave = new Label { Text = "Clave", Location = new Point(10, 12), AutoSize = true, Font = new Font("Segoe UI", 8.5F, FontStyle.Bold) };
            _txtClaveMatriz = new TextBox { Location = new Point(60, 8), Size = new Size(120, 24), CharacterCasing = CharacterCasing.Upper, Anchor = AnchorStyles.Top | AnchorStyles.Left };
            var lblUnidad = new Label { Text = "Unidad", Location = new Point(190, 12), AutoSize = true, Font = new Font("Segoe UI", 8.5F, FontStyle.Bold) };
            _txtUnidadMatriz = new TextBox { Location = new Point(245, 8), Size = new Size(90, 24), Anchor = AnchorStyles.Top | AnchorStyles.Left };
            var lblDesc = new Label { Text = "Descripción", Location = new Point(345, 12), AutoSize = true, Font = new Font("Segoe UI", 8.5F, FontStyle.Bold) };
            _txtDescripcionMatriz = new TextBox { Location = new Point(425, 8), Size = new Size(430, 24), Anchor = AnchorStyles.Top | AnchorStyles.Left };
            _rbTipoApu = new RadioButton { Text = "APU", Location = new Point(875, 11), AutoSize = true, Checked = true, Anchor = AnchorStyles.Top | AnchorStyles.Left };
            _rbTipoBasico = new RadioButton { Text = "Básico", Location = new Point(935, 11), AutoSize = true, Anchor = AnchorStyles.Top | AnchorStyles.Left };
            _rbTipoCuadrilla = new RadioButton { Text = "Cuadrilla", Location = new Point(1025, 11), AutoSize = true, Anchor = AnchorStyles.Top | AnchorStyles.Left };
            _btnGuardarMatriz = new Button { Text = "Guardar", Location = new Point(1145, 7), Size = new Size(95, 26), BackColor = Color.FromArgb(31, 122, 67), ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Visible = false, Anchor = AnchorStyles.Top | AnchorStyles.Right };
            _btnGuardarMatriz.FlatAppearance.BorderSize = 0;
            _btnCancelarMatriz = new Button { Text = "Cancelar", Location = new Point(1245, 7), Size = new Size(95, 26), BackColor = Color.FromArgb(120,120,120), ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Visible = false, Anchor = AnchorStyles.Top | AnchorStyles.Right };
            _btnCancelarMatriz.FlatAppearance.BorderSize = 0;
            _btnGuardarMatriz.Click += BtnGuardarMatriz_Click;
            _btnCancelarMatriz.Click += BtnCancelarMatriz_Click;
            _panelDatos.Resize += (s, e) => LayoutHeaderControls();
            _panelDatos.Controls.AddRange(new Control[] { lblClave, _txtClaveMatriz, lblUnidad, _txtUnidadMatriz, lblDesc, _txtDescripcionMatriz, _rbTipoApu, _rbTipoBasico, _rbTipoCuadrilla, _btnGuardarMatriz, _btnCancelarMatriz });

            _panelBotonesAgregar = new Panel { Dock = DockStyle.Top, Height = 30, BackColor = Color.FromArgb(245,245,250), Enabled = false };
            var lblAg = new Label { Text = "Agregar:", Location = new Point(8,7), AutoSize = true, Font = new Font("Segoe UI", 8.5F), ForeColor = Color.FromArgb(80,80,80) };
            _btnAgregarMaterial     = CrearBtn("📦 Material",    new Point(70,  1), Color.FromArgb(76,175,80));
            _btnAgregarMO           = CrearBtn("👷 M.O.",         new Point(180, 1), Color.FromArgb(33,150,243));
            _btnAgregarMaquinaria   = CrearBtn("🚜 Maquinaria",   new Point(290, 1), Color.FromArgb(255,152,0));
            _btnAgregarHerramienta  = CrearBtn("🛠️ Herramienta", new Point(400, 1), Color.FromArgb(96,125,139));
            _btnAgregarBasico       = CrearBtn("🧩 Básico",       new Point(510, 1), Color.FromArgb(156,39,176));
            _btnAgregarMaterial.Click     += BtnAgregarMaterial_Click;
            _btnAgregarMO.Click           += BtnAgregarMO_Click;
            _btnAgregarMaquinaria.Click   += BtnAgregarMaquinaria_Click;
            _btnAgregarHerramienta.Click  += BtnAgregarHerramienta_Click;
            _btnAgregarBasico.Click       += BtnAgregarBasico_Click;
            _panelBotonesAgregar.Resize += (s, e) => LayoutAgregarButtons();
            _panelBotonesAgregar.Controls.AddRange(new Control[] { lblAg, _btnAgregarMaterial, _btnAgregarMO, _btnAgregarMaquinaria, _btnAgregarHerramienta, _btnAgregarBasico });

            _dgvComponentes = new DataGridView
            {
                Dock = DockStyle.Fill, BackgroundColor = Color.White, BorderStyle = BorderStyle.None,
                AllowUserToAddRows = false, AllowUserToDeleteRows = false, ReadOnly = false,
                EditMode = DataGridViewEditMode.EditOnKeystrokeOrF2,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect, RowHeadersVisible = false,
                ColumnHeadersHeight = 34, RowTemplate = { Height = 28 }, Font = new Font("Segoe UI", 8.5F),
                CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal, GridColor = Color.FromArgb(225,225,235)
            };
            _dgvComponentes.EnableHeadersVisualStyles = false;
            _dgvComponentes.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(55,55,85);
            _dgvComponentes.ColumnHeadersDefaultCellStyle.ForeColor = Color.White;
            _dgvComponentes.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 8.5F, FontStyle.Bold);
            _dgvComponentes.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(248,248,255);

            var right  = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleRight };
            var center = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleCenter };
            _dgvComponentes.Columns.AddRange(new DataGridViewColumn[]
            {
                new DataGridViewTextBoxColumn { Name = "ColTipo",     HeaderText = "Tipo",       Width = 105, ReadOnly = true },
                new DataGridViewTextBoxColumn { Name = "ColClave",    HeaderText = "C",           Width = 100, ReadOnly = true },
                new DataGridViewTextBoxColumn { Name = "ColDesc",     HeaderText = "Descripción", AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill, ReadOnly = false },
                new DataGridViewTextBoxColumn { Name = "ColUnidad",   HeaderText = "Unidad",      Width = 65,  DefaultCellStyle = center, ReadOnly = false },
                new DataGridViewTextBoxColumn { Name = "ColCantidad", HeaderText = "Cantidad",    Width = 95,  DefaultCellStyle = right, ReadOnly = false },
                new DataGridViewTextBoxColumn { Name = "ColPU",       HeaderText = "P.U.",        Width = 115, DefaultCellStyle = right, ReadOnly = false },
                new DataGridViewTextBoxColumn { Name = "ColImporte",  HeaderText = "Importe",     Width = 115, DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleRight, Font = new Font("Segoe UI", 8.5F, FontStyle.Bold) }, ReadOnly = true },
                new DataGridViewTextBoxColumn { Name = "ColId",       HeaderText = "ID",          Visible = false },
                new DataGridViewButtonColumn  { Name = "ColEliminar", HeaderText = "",            Width = 36 }
            });
            _dgvComponentes.CellClick       += DgvComponentes_CellClick;
            _dgvComponentes.CellDoubleClick += DgvComponentes_PanelCellDoubleClick;
            _dgvComponentes.CellBeginEdit   += DgvComponentes_PanelCellBeginEdit;
            _dgvComponentes.CellEndEdit     += DgvComponentes_PanelCellEndEdit;
            _dgvComponentes.CellMouseDown   += DgvComponentes_CellMouseDown;
            _dgvComponentes.MouseDown       += DgvComponentes_MouseDown;
            _dgvComponentes.CellMouseUp     += DgvComponentes_CellMouseUp;
            _dgvComponentes.MouseUp         += DgvComponentes_MouseUp;

            _panelTotales = new Panel { Dock = DockStyle.Bottom, Height = 26, BackColor = Color.FromArgb(235,235,248) };
            _lblMat = new Label { AutoSize = true, Location = new Point(8,   5), Font = new Font("Segoe UI", 8F), ForeColor = Color.FromArgb(50,50,50) };
            _lblMO  = new Label { AutoSize = true, Location = new Point(185, 5), Font = new Font("Segoe UI", 8F), ForeColor = Color.FromArgb(50,50,50) };
            _lblMaq = new Label { AutoSize = true, Location = new Point(362, 5), Font = new Font("Segoe UI", 8F), ForeColor = Color.FromArgb(50,50,50) };
            _lblBas = new Label { AutoSize = true, Location = new Point(539, 5), Font = new Font("Segoe UI", 8F), ForeColor = Color.FromArgb(50,50,50) };
            _lblDir = new Label { AutoSize = true, Location = new Point(716, 5), Font = new Font("Segoe UI", 8.5F, FontStyle.Bold), ForeColor = Color.FromArgb(20,100,20) };
            _panelTotales.Controls.AddRange(new Control[] { _lblMat, _lblMO, _lblMaq, _lblBas, _lblDir });

            this.Controls.Add(_dgvComponentes);
            this.Controls.Add(_panelBotonesAgregar);
            this.Controls.Add(_panelDatos);
            this.Controls.Add(_panelTotales);
            this.Controls.Add(_panelHeader);
        }

        private void LayoutHeaderControls()
        {
            int yText = 7;
            _txtClaveMatriz.Location = new Point(60, yText);
            _txtClaveMatriz.Size = new Size(120, 24);

            _txtUnidadMatriz.Location = new Point(245, yText);
            _txtUnidadMatriz.Size = new Size(90, 24);

            _txtDescripcionMatriz.Location = new Point(425, yText);

            int rightMargin = 12;
            int cancelWidth = 95;
            int saveWidth = 95;
            int gapButtons = 8;
            int gapBeforeButtons = 16;
            int radiosWidth = 220;
            int radiosGap = 16;

            int cancelX = Math.Max(760, _panelDatos.ClientSize.Width - rightMargin - cancelWidth);
            int saveX = cancelX - gapButtons - saveWidth;
            int radioStartX = saveX - gapBeforeButtons - radiosWidth;
            int maxDescRight = radioStartX - radiosGap;

            _txtDescripcionMatriz.Width = Math.Max(260, maxDescRight - _txtDescripcionMatriz.Left);

            _rbTipoApu.Location = new Point(radioStartX, 10);
            _rbTipoBasico.Location = new Point(radioStartX + 72, 10);
            _rbTipoCuadrilla.Location = new Point(radioStartX + 152, 10);

            int btnY = 6;
            _btnGuardarMatriz.Location = new Point(saveX, btnY);
            _btnCancelarMatriz.Location = new Point(cancelX, btnY);
        }

        private void LayoutTopHeader()
        {
            if (_panelHeader == null || _lblTituloMatriz == null || _lblInfoMatriz == null || _btnAnterior == null || _btnSiguiente == null) return;

            int margin = 10;
            int topY = 6;
            int infoY = 28;
            int btnW = 30;
            int btnH = 20;
            int gap = 2;
            int rightMargin = 10;

            int nextX = Math.Max(margin, _panelHeader.ClientSize.Width - rightMargin - btnW);
            int prevX = Math.Max(margin, nextX - gap - btnW);

            _btnAnterior.Size = new Size(btnW, btnH);
            _btnSiguiente.Size = new Size(btnW, btnH);
            _btnAnterior.Location = new Point(prevX, topY);
            _btnSiguiente.Location = new Point(nextX, topY);

            int labelsWidth = Math.Max(200, prevX - margin - 12);
            _lblTituloMatriz.Location = new Point(margin, 5);
            _lblTituloMatriz.Size = new Size(labelsWidth, 20);
            _lblInfoMatriz.Location = new Point(margin, infoY);
            _lblInfoMatriz.Size = new Size(labelsWidth, 16);
        }

        private void LayoutAgregarButtons()
        {
            if (_panelBotonesAgregar == null) return;
            var controls = _panelBotonesAgregar.Controls.OfType<Control>().Where(c => c != null).ToList();
            if (controls.Count == 0) return;

            var lbl = controls.FirstOrDefault(c => c is Label);
            int x = 8;
            if (lbl != null)
            {
                lbl.Location = new Point(x, 7);
                x = lbl.Right + 12;
            }

            int y = 1;
            int gap = 8;
            foreach (var btn in controls.Where(c => c is Button))
            {
                btn.Location = new Point(x, y);
                x += btn.Width + gap;
            }
        }

    }
}
