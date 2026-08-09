using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using SOPRO.Application.Services;
using SOPRO.Application.Models.Presupuesto;
using SOPRO.Core.Entities;
using SOPRO.Data.Context;
using SOPRO.WinForms.Helpers;
using SOPRO.WinForms.Services;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;

namespace SOPRO.WinForms.Forms
{
    /// <summary>
    /// Módulo de cálculo de financiamiento conforme al RLOPSRM.
    /// Calcula el %F desde el flujo de caja del programa de obra
    /// y lo guarda en ConfiguracionesFinanciamiento.
    /// El botón Transferir lo copia a Proyecto.PorcentajeFinanciamiento
    /// para que el módulo de Porcentajes lo recoja.
    /// </summary>
    public partial class FormFinanciamiento : Form, IGridFormato, IRecalculable, IBusquedaGrid
    {
        private readonly SOPROContext _context;
        private readonly Proyecto _proyecto;
        private readonly FinanciamientoCalculationService _service = new();
        private ConfiguracionFinanciamiento _config = null!;
        private bool _cargando;
        private bool _aplicandoLayoutColumnas;
        private ColumnaFinanciamiento? _colFinRibbon;
        private ColumnaPersonalizada? _columnaRibbon;
        private Label? _lblCostoDirectoRef;
        private Label? _lblCostoDirectoValor;
        private Label? _lblCostoIndirectoRef;
        private Label? _lblCostoIndirectoValor;

        public DataGridView GridPrincipal => dgvFlujo;
        public DataGridView GridBusqueda => dgvFlujo;
        public event EventHandler? ColumnaSeleccionadaCambiada;
        public ColumnaPersonalizada ColumnaSeleccionada => _columnaRibbon!;

        /// <summary>Notifica a FormPresupuesto que el porcentaje cambió.</summary>
        public static event EventHandler? FinanciamientoTransferido;

        public FormFinanciamiento(SOPROContext context, Proyecto proyecto)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _proyecto = proyecto ?? throw new ArgumentNullException(nameof(proyecto));
            FormatoHelper.EstablecerProyecto(_proyecto);
            _cargando = true;
            InitializeComponent();
            ConfigurarComboModeloFinanciamiento();
            new EditableReportTitleHelper(_context, panelTop, lblTitulo, () => _proyecto.Id, ReportTitleModuleKeys.Financiamiento).Attach();
            ConfigurarGrid();
            EnsureReferenceLabels();
            _cargando = false;
        }






        // ── Tabla de flujo de caja ────────────────────────────────────────────


        // ── Resultado ─────────────────────────────────────────────────────────




    }
}
