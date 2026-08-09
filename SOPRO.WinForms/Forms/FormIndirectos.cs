using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using Microsoft.EntityFrameworkCore;
using SOPRO.Core.Entities;
using SOPRO.Data.Context;
using SOPRO.WinForms.Helpers;
using SOPRO.WinForms.Services;
using SOPRO.Application.Services;
using SOPRO.WinForms.Undo;

namespace SOPRO.WinForms.Forms
{
    public partial class FormIndirectos : Form, IGridFormato, IRecalculable, IBusquedaGrid
    {
        private readonly SOPROContext _context;
        private readonly Proyecto _proyecto;
        private ConfiguracionIndirectos _configuracion;
        private readonly UndoManager _undoManager = new();
        private bool _isUndoRedo;
        private DataGridView? _undoGrid;
        private int _undoRowIndex = -1;
        private int _undoColumnIndex = -1;
        private string _undoOldValue = string.Empty;
        private string _undoTextBoxOldValue = string.Empty;
        private TextBox? _undoTextBox;

        /// <summary>
        /// Evento que se dispara cuando se transfieren porcentajes al proyecto.
        /// </summary>
        public static event EventHandler IndirectosTransferidos;


        // ── IGridFormato ──────────────────────────────────────────────────────
        // Ambos grids comparten el mismo esquema de columnas.
        // GridPrincipal apunta a dgvOficinaCentral; el formato se aplica a ambos.
        public System.Windows.Forms.DataGridView GridPrincipal => dgvOficinaCentral;
        public System.Windows.Forms.DataGridView GridBusqueda => dgvOficinaCentral;


        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == (Keys.Control | Keys.Z) && TryUndoIndirectos())
                return true;

            if (keyData == (Keys.Control | Keys.Y) && TryRedoIndirectos())
                return true;

            return base.ProcessCmdKey(ref msg, keyData);
        }

        public FormIndirectos(SOPROContext context, Proyecto proyecto)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _proyecto = proyecto ?? throw new ArgumentNullException(nameof(proyecto));

            // Establecer proyecto para formateo
            FormatoHelper.EstablecerProyecto(_proyecto);

            InitializeComponent();
            new EditableReportTitleHelper(_context, panelTop, lblTitulo, () => _proyecto.Id, ReportTitleModuleKeys.Indirectos).Attach();

            // Aplicar estilo consistente
            dgvCampo.AplicarEstiloSOPRO();
            dgvOficinaCentral.AplicarEstiloSOPRO();

            ConfigurarGrids();
        }

        private void FormIndirectos_Load(object sender, EventArgs e)
        {
            lblProyecto.Text = $"📁 {_proyecto.Nombre}";
            CargarDatos();
        }









    }
}
