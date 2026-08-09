using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Microsoft.EntityFrameworkCore;
using SOPRO.Core.Entities;
using SOPRO.Data.Context;
using SOPRO.WinForms.Helpers;
using SOPRO.WinForms.UI.Controls;
using SOPRO.Application.Services;

namespace SOPRO.WinForms.Forms
{
    public partial class FormProyecto : Form
    {
        private SOPROContext _context;
        private readonly Proyecto _proyecto;

        private bool _cargandoRibbon = false;
        private IGridFormato _formActivo = null;
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

        public FormProyecto(SOPROContext context, Proyecto proyecto)
        {
            _context  = context  ?? throw new ArgumentNullException(nameof(context));
            _proyecto = proyecto ?? throw new ArgumentNullException(nameof(proyecto));
            InitializeComponent();
            // KeyPreview permite interceptar F9/F10 antes que los controles hijos
            this.KeyPreview = true;
            this.KeyDown += FormProyecto_KeyDown;
        }

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
