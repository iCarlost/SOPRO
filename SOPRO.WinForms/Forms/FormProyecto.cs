using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Microsoft.EntityFrameworkCore;
using SOPRO.Core.Entities;
using SOPRO.Data.Context;
using SOPRO.Data.Factories;
using SOPRO.WinForms.Helpers;
using SOPRO.WinForms.UI.Controls;
using SOPRO.Application.Contracts;
using SOPRO.Application.Services;

namespace SOPRO.WinForms.Forms
{
    public partial class FormProyecto : Form
    {
        private SOPROContext _context;
        private readonly Proyecto _proyecto;
        private readonly IProjectDbContextFactory _factory = new ProjectDbContextFactory();
        private readonly IDiagnosticsSink? _diagnosticsSink;
        private readonly string? _productVersion;

        private bool _cargandoRibbon = false;
        private IGridFormato _formActivo = null;
        // Grid cuyo evento SelectionChanged está suscrito para refrescar el estado
        // contextual del ribbon al cambiar la selección de CELDAS (P1).
        private DataGridView _gridActivoSeleccion = null;
        private IRecalculable _formActivoRecalculable = null;
        private IBusquedaGrid _formActivoBuscable = null;
        private IConsolidacionInsumos _formActivoConsolidable = null;
        private bool _restaurandoEstadoArbol = false;
        private const int SidebarExpandedWidth = 250;
        private const int SidebarCollapsedWidth = 60;
        private const int SidebarExpandedMaxWidth = 420;
        private readonly ToolTip _treeMenuToolTip = new ToolTip();
        private string _ultimoTooltipNodo = string.Empty;
        private FormBuscarEnGrid _formBuscarEnGrid = null;
        private bool _ajustandoBarraLateral = false;
        private Color _colorMuestraFondoRibbon = Color.White;
        private Color _colorMuestraTextoRibbon = Color.Black;
        private bool _ajusteHostTabsPendiente = false;
        private bool _actualizandoOverflowRibbon = false;
        // Abrir() selecciona la pestaña antes de Show() y del layout final: el
        // SelectedIndexChanged automático se ignora durante esa ventana y la
        // sincronización única del contexto del ribbon se hace al final.
        private bool _aperturaModuloEnCurso = false;

        public FormProyecto(SOPROContext context, Proyecto proyecto)
            : this(context, proyecto, diagnosticsSink: null, productVersion: null)
        {
        }

        /// <summary>
        /// Seam de composición: propaga el sink/productVersion opcionales a los módulos
        /// que emiten diagnóstico de resultados inciertos. El constructor público
        /// conserva el comportamiento sin diagnóstico.
        /// </summary>
        internal FormProyecto(
            SOPROContext context,
            Proyecto proyecto,
            IDiagnosticsSink? diagnosticsSink,
            string? productVersion)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _proyecto = proyecto ?? throw new ArgumentNullException(nameof(proyecto));
            _diagnosticsSink = diagnosticsSink;
            _productVersion = productVersion;
            InitializeComponent();
            SoproTheme.ApplyFormProyecto(this);
            // KeyPreview permite interceptar F9/F10 antes que los controles hijos
            this.KeyPreview = true;
            this.KeyDown += FormProyecto_KeyDown;
        }

        /// <summary>
        /// Seam de composición: el catálogo de mano de obra recibe el sink/versión
        /// de diagnóstico para propagarlos al editor y al cálculo de FSR.
        /// </summary>
        internal FormCatalogoManoObra CrearCatalogoManoObra()
            => new(_context, _proyecto.Id, _diagnosticsSink, _productVersion);

        internal FormCatalogoMateriales CrearCatalogoMateriales()
            => new(_context, _proyecto.Id, factory: null,
                diagnosticsSink: _diagnosticsSink,
                productVersion: _productVersion);

        internal FormCatalogoMaquinaria CrearCatalogoMaquinaria()
            => new(_context, _proyecto.Id, _diagnosticsSink, _productVersion);

        internal FormCatalogoHerramientas CrearCatalogoHerramientas()
            => new(_context, _proyecto.Id, _diagnosticsSink, _productVersion);

        /// <summary>
        /// Seam de composición: el catálogo de matrices recibe el sink/versión de
        /// diagnóstico para propagarlos a los editores de matriz que abre.
        /// </summary>
        internal FormMatrices CrearCatalogoMatrices()
            => new(_context, _proyecto.Id, false, _diagnosticsSink, _productVersion);

        private void FormProyecto_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.F9 && !e.Control && !e.Shift && !e.Alt)
            {
                e.Handled = true;
                e.SuppressKeyPress = true;
                btnRecalcularRibbon_Click(sender, EventArgs.Empty);
                return;
            }
            if (e.KeyCode == Keys.F10 && !e.Control && !e.Shift && !e.Alt)
            {
                e.Handled = true;
                e.SuppressKeyPress = true;
                btnDepurarRibbon_Click(sender, EventArgs.Empty);
            }
        }

        private void FormProyecto_Load(object sender, EventArgs e)
        {
            this.Text = "SOPRO - Sistema de Presupuestos de Obra - " + $"📁 {_proyecto.Nombre} | {_proyecto.Ubicacion}";
            InicializarRibbon();
            DesactivarRibbon();
            btnDepurarRibbon.Enabled = true;
            btnConsolidarInsumos.Enabled = false;
            HabilitarControlesFormato(false);

            treeMenu.AfterExpand += treeMenu_AfterExpand;
            treeMenu.AfterCollapse += treeMenu_AfterCollapse;
            treeMenu.MouseMove += treeMenu_MouseMove;
            treeMenu.MouseLeave += treeMenu_MouseLeave;

            _treeMenuToolTip.InitialDelay = 150;
            _treeMenuToolTip.ReshowDelay = 100;
            _treeMenuToolTip.AutoPopDelay = 6000;
            _treeMenuToolTip.ShowAlways = true;

            tabControl.Padding = new Point(0, 0);
            tabControl.SizeMode = TabSizeMode.Normal;
            tabControl.Resize += (_, __) =>
            {
                if (_ajustandoBarraLateral)
                    ProgramarAjusteHostTabsDiferido();
                else
                    AjustarHostTabs();
            };
            Resize += (_, __) =>
            {
                if (_ajustandoBarraLateral)
                    ProgramarAjusteHostTabsDiferido();
                else
                    AjustarHostTabs();
            };

            RestaurarEstadoBarraYLateral();
            AjustarHostTabs();
            ActualizarOverflowRibbon();
        }


        // =====================================================================
        // RIBBON
        // =====================================================================





        /// <summary>
        /// Notifica a todos los FormProyecto abiertos que un insumo cambió:
        /// recarga el catálogo de matrices y recalcula el presupuesto.
        /// Llamar desde los catálogos tras eliminar o editar un insumo.
        /// </summary>
    }
}
